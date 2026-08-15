using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// THE STRANGER'S ROAD (2026.08.15): speech from a hosted service, on a key the player already
    /// has, for the very many people who have no graphics card to spare and no wish to fetch several
    /// gigabytes of model.
    /// <para>
    /// It sits behind exactly the same seam as the local engine — a request goes in, a WAV comes out,
    /// and everything above (the cache, the pouring, the playback chain, the ▶ marks) cannot tell the
    /// difference. What it CANNOT do is clone anybody, and that is the honest division of the two
    /// roads: Qwen is for the player who wants their companion to sound like a particular person,
    /// this is for the player who wants their companions to sound like anybody at all.
    /// </para>
    /// <para>
    /// Verified against the live API documentation on 2026.08.15: <c>POST /v1/audio/speech</c> with
    /// <c>{model, input, voice, response_format}</c>, thirteen voices, and — the field that matters
    /// most here — <c>wav</c> among the response formats, so what comes back is exactly what the
    /// playback chain already knows how to read. Priced at $0.015 the minute for gpt-4o-mini-tts,
    /// which is why the ledger below bills by the MINUTE OF AUDIO WE ACTUALLY RECEIVED rather than by
    /// an estimate: we can read the length out of the WAV's own header, so the cost notice is
    /// measured rather than guessed.
    /// </para>
    /// <para>
    /// Raw <see cref="HttpClient"/> for the same reason every other client here is: the official SDK
    /// wants a modern .NET and the game runs mods on 4.7.2.
    /// </para>
    /// </summary>
    internal static class CloudVoiceClient
    {
        /// <summary>The thirteen, from the live documentation. gpt-4o-mini-tts carries all of them;
        /// tts-1 and tts-1-hd carry the first nine (no marin, cedar or ballad), which is why the
        /// newer model is the default.</summary>
        public static readonly string[] KnownVoices =
        {
            "alloy", "ash", "ballad", "coral", "echo", "fable", "nova",
            "onyx", "sage", "shimmer", "verse", "marin", "cedar",
        };

        /// <summary>A hosted line is a network round trip, not a graphics card. Generous enough for a
        /// long reply on a poor connection, short enough that a dead endpoint does not hold a voice
        /// job open for minutes.</summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(90);

        // One client for the process, as the LLM clients do: a new HttpClient per request is the
        // classic way to exhaust sockets.
        private static readonly HttpClient Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        /// <summary>Whether a hosted voice could be spoken at all right now.</summary>
        public static bool IsConfigured(ModConfig? config)
            => config != null && !string.IsNullOrWhiteSpace(config.CloudVoiceApiKey);

        /// <summary>Plainly why not, when it is not configured.</summary>
        public static string MissingReason(ModConfig? config)
        {
            if (config == null) return "the mod is still starting up";
            if (string.IsNullOrWhiteSpace(config.CloudVoiceApiKey))
                return "no key for the hosted voices — add CloudVoiceApiKey to config.json, or pick a voice made on your own machine";
            return string.Empty;
        }

        /// <summary>
        /// Speaks one line and writes it as a WAV at <paramref name="outPath"/>. Never throws: a
        /// hosted voice that cannot be reached is a line that is read instead of heard, exactly as
        /// with the local engine.
        /// </summary>
        public static async Task<VoiceHostReply> SynthesizeAsync(
            ModConfig config, string voiceId, string text, string outPath, CancellationToken cancel = default)
        {
            if (!IsConfigured(config)) return VoiceHostReply.Failed(MissingReason(config));
            if (string.IsNullOrWhiteSpace(text)) return VoiceHostReply.Failed("nothing to say");
            if (string.IsNullOrWhiteSpace(outPath)) return VoiceHostReply.Failed("nowhere to write it");

            try
            {
                var body = BuildBody(config.CloudVoiceModel, voiceId, text);

                using (var request = new HttpRequestMessage(HttpMethod.Post, config.CloudVoiceEndpoint))
                using (var timeout = new CancellationTokenSource(RequestTimeout))
                using (var either = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancel))
                {
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", config.CloudVoiceApiKey.Trim());
                    request.Content = new StringContent(body, new UTF8Encoding(false), "application/json");

                    var started = DateTime.UtcNow;
                    using (var response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, either.Token)
                                                    .ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            var complaint = await ReadComplaintAsync(response).ConfigureAwait(false);
                            return VoiceHostReply.Failed(Describe((int)response.StatusCode, complaint));
                        }

                        var audio = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        if (audio == null || audio.Length <= 44)
                            return VoiceHostReply.Failed("the hosted voice answered with no sound");

                        WriteAtomic(outPath, audio);

                        // FIRST, and it is not optional: a streamed answer arrives with 0xFFFFFFFF
                        // where its data size should be, because the service wrote the header before
                        // it knew the length. Our own reader clamps that and reports the truth — so
                        // every number in the log was right and the clip was still SILENT, because
                        // the audio engine believes the header and had been told four gigabytes.
                        if (WavFiles.TryRepairSizes(outPath))
                            ModLog.Info("voice: the hosted clip's header did not know its own length; repaired.");

                        Normalize(outPath);

                        // Measured from the file's own header, so the cost notice below is a fact
                        // rather than an estimate.
                        var info = WavFiles.TryRead(outPath);
                        var rate = info?.SampleRate ?? VoiceHostProtocol.DefaultSampleRate;
                        var samples = info == null || info.BitsPerSample <= 0 || info.Channels <= 0
                            ? 0
                            : info.DataBytes / (info.BitsPerSample / 8) / info.Channels;

                        UsageLedger.NoteVoiceMinutes(
                            config, info?.Duration ?? TimeSpan.Zero, config.CloudVoiceModel);

                        return new VoiceHostReply
                        {
                            Ok = true,
                            Path = outPath,
                            Samples = samples,
                            Rate = rate,
                            Ms = (long)(DateTime.UtcNow - started).TotalMilliseconds,
                        };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return VoiceHostReply.Failed(cancel.IsCancellationRequested
                    ? "the line was let go"
                    : "the hosted voice took too long");
            }
            catch (Exception ex)
            {
                return VoiceHostReply.Failed(ex.Message);
            }
        }

        /// <summary>Hand-built rather than serialized: three fields, and it keeps this file free of a
        /// JSON dependency the rest of the voice code does not have.</summary>
        private static string BuildBody(string model, string voice, string text)
        {
            var sb = new StringBuilder(text.Length + 128);
            sb.Append("{\"model\":").Append(Quote(model));
            sb.Append(",\"voice\":").Append(Quote(string.IsNullOrWhiteSpace(voice) ? "alloy" : voice));
            sb.Append(",\"input\":").Append(Quote(text));
            // WAV, so what comes back is exactly what the playback chain already reads — and so a
            // cached line is the same shape whichever road made it.
            sb.Append(",\"response_format\":\"wav\"}");
            return sb.ToString();
        }

        private static string Quote(string? s)
        {
            var sb = new StringBuilder((s?.Length ?? 0) + 16);
            sb.Append('"');
            foreach (var c in s ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        // Escaped rather than sent raw: this mod is played in Bulgarian, and a
                        // request body is the classic place for an encoding to be lost.
                        if (c < 0x20 || c > 0x7E) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static async Task<string> ReadComplaintAsync(HttpResponseMessage response)
        {
            try
            {
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(text)) return string.Empty;
                return text.Length <= 300 ? text.Trim() : text.Substring(0, 300).Trim() + "…";
            }
            catch { return string.Empty; }
        }

        /// <summary>The same plain-words treatment the LLM clients give a bad answer: the player is
        /// told what to DO, not what the status code was.</summary>
        private static string Describe(int status, string complaint)
        {
            switch (status)
            {
                case 401:
                case 403:
                    return "the hosted voice refused the key — check CloudVoiceApiKey in config.json";
                case 404:
                    return "the hosted voice does not know that model — check CloudVoiceModel";
                case 429:
                    return "the hosted voice is rate-limited or out of credit";
                default:
                    if (status >= 500) return "the hosted voice service is having trouble (" + status + ")";
                    return "the hosted voice refused the line (" + status + ")"
                           + (complaint.Length > 0 ? " — " + complaint : string.Empty);
            }
        }

        /// <summary>
        /// Brings a hosted clip up to the same loudness the local engine's clips are given, so the
        /// two roads sound like the same feature.
        /// <para>
        /// The host normalises everything it makes to a peak of 0.89 (it must — the engine's own
        /// output sits 10-20 dB low). A hosted clip arrives at a sensible level already, around 0.60
        /// of full scale, which is a good three decibels quieter than everything beside it — enough
        /// that casting one hosted voice among cloned ones sounds like that character mumbling.
        /// Peak normalisation only: the loudest sample is brought to the same ceiling and everything
        /// scales with it, so the performance is untouched.
        /// </para>
        /// <para>Anything unexpected leaves the file exactly as it came. A slightly quiet voice is a
        /// shrug; a corrupted one is a bug.</para>
        /// </summary>
        private static void Normalize(string path)
        {
            const double targetPeak = 0.89;
            try
            {
                var info = WavFiles.TryRead(path);
                if (info == null || info.BitsPerSample != 16 || info.DataBytes < 2) return;

                var bytes = File.ReadAllBytes(path);
                var start = (int)info.DataOffset;
                var end = start + (int)info.DataBytes;
                if (start < 0 || end > bytes.Length) return;

                var peak = 0;
                for (var i = start; i + 1 < end; i += 2)
                {
                    int sample = (short)(bytes[i] | (bytes[i + 1] << 8));
                    if (sample == short.MinValue) sample = short.MaxValue;   // -32768 has no positive twin
                    var magnitude = sample < 0 ? -sample : sample;
                    if (magnitude > peak) peak = magnitude;
                }
                if (peak <= 0) return;

                var gain = targetPeak * short.MaxValue / peak;
                if (gain <= 1.02 && gain >= 0.98) return;                    // already where it should be
                if (gain > 8) gain = 8;                                      // never amplify a whisper into hiss

                for (var i = start; i + 1 < end; i += 2)
                {
                    var scaled = (short)(bytes[i] | (bytes[i + 1] << 8)) * gain;
                    if (scaled > short.MaxValue) scaled = short.MaxValue;
                    else if (scaled < short.MinValue) scaled = short.MinValue;
                    var value = (short)Math.Round(scaled);
                    bytes[i] = (byte)(value & 0xFF);
                    bytes[i + 1] = (byte)((value >> 8) & 0xFF);
                }

                File.WriteAllBytes(path, bytes);
            }
            catch
            {
                // A clip we could not lift is a clip that plays a little quietly. Never worth more.
            }
        }

        private static void WriteAtomic(string path, byte[] bytes)
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var temp = path + ".part";
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        /// <summary>The shelf every player has without downloading anything — one preset per hosted
        /// voice, made in memory rather than written to disk, so nothing has to be installed and
        /// nothing is left behind if the key is removed.</summary>
        public static IReadOnlyList<VoicePreset> Shelf()
        {
            var shelf = new List<VoicePreset>(KnownVoices.Length);
            foreach (var name in KnownVoices)
            {
                shelf.Add(new VoicePreset
                {
                    Id = "cloud-" + name,
                    // Bare, on purpose: the panel says where a voice comes from itself, for every
                    // shelf alike (VoiceRowVM.NameFor), so a mark baked in here would double up.
                    Name = char.ToUpperInvariant(name[0]) + name.Substring(1),
                    Backend = VoiceBackend.Remote,
                    RemoteVoiceId = name,
                    Source = "hosted speech service",
                    Gender = GenderOf(name),
                });
            }
            return shelf;
        }

        /// <summary>
        /// Only ever a hint for a soul's FIRST voice, never a claim about the voice itself — the same
        /// rule the local shelf keeps. Taken from how each hosted voice actually reads rather than
        /// from anything the service promises, and any of them can be cast on anybody.
        /// </summary>
        private static VoiceGender GenderOf(string name)
        {
            switch (name)
            {
                case "nova":
                case "shimmer":
                case "coral":
                case "sage":
                case "marin":
                    return VoiceGender.Female;
                case "onyx":
                case "echo":
                case "ash":
                case "verse":
                case "cedar":
                    return VoiceGender.Male;
                default:
                    return VoiceGender.Unknown;    // alloy, fable, ballad read either way
            }
        }
    }
}
