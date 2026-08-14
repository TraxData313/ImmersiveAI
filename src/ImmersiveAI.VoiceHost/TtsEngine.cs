using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ImmersiveAI.VoiceHost;

public sealed class SynthOutcome
{
    public bool Ok;
    public string Error = "";
    public string Path = "";
    public long Ms;
    public int Samples;
    public int Rate;
}

/// <summary>
/// The engine, wrapped so that no single bad request can take the host down.
/// Everything that can be checked before the native call IS checked before the
/// native call: this process is the only thing standing between a ggml assert
/// and the player's campaign, so we would rather answer an honest error than
/// find out what the DLL does with a missing file.
/// </summary>
public sealed class TtsEngine : IDisposable
{
    // The engine answers 24000 Hz mono; the protocol's "rate" is reported from
    // the real result once we have one, and this is what we promise beforehand.
    public const int DefaultRate = 24000;

    // A voice line is a sentence or two (the game already cuts replies into
    // bites). Anything past the soft cap is trimmed at a word boundary rather
    // than refused - the player would rather hear most of a line than nothing.
    private const int SoftMaxChars = 1500;
    private const int HardMaxChars = 6000;

    private IntPtr _ctx;
    private int _maxAudioTokens = 4096;
    private string[] _speakers = Array.Empty<string>();

    public string BackendName { get; private set; } = "unknown";
    public string ModelName { get; private set; } = "unknown";
    public int Rate { get; private set; } = DefaultRate;
    public QwenCaps Caps { get; private set; }

    /// <summary>Loads the DLLs and the model. Throws with a plain sentence if it cannot.</summary>
    public void Bring(EngineLocation loc, HostOptions o)
    {
        _maxAudioTokens = o.MaxAudioTokens;

        Loader.Preload(loc.EngineDir);

        int pref = o.BackendCode;
        SafeCall("set_backend_preference", () => Native.SetBackendPreference(pref));
        HostLog.Info($"compiled backend mask = 0x{SafeCall("get_compiled_backend_mask", Native.GetCompiledBackendMask):x}");
        if (o.Threads > 0) SafeCall("set_cpu_threads", () => Native.SetCpuThreads(o.Threads));

        _ctx = Native.Init();
        if (_ctx == IntPtr.Zero) throw new InvalidOperationException("the engine would not start (init returned null)");

        var sw = Stopwatch.StartNew();
        int ok = Native.LoadModelsWithName(_ctx, Native.Utf8(loc.ModelDir), Native.Utf8(loc.ModelName));
        sw.Stop();
        if (ok == 0)
        {
            string why = Native.LastError(_ctx);
            Dispose();
            throw new InvalidOperationException($"the model could not be loaded ({loc.ModelName}): {why}");
        }

        ModelName = loc.ModelName;
        BackendName = Native.ActiveBackend();
        Caps = Native.GetModelCapabilities(_ctx);
        try { _speakers = Native.Speakers(_ctx); } catch { _speakers = Array.Empty<string>(); }

        HostLog.Info($"loaded {ModelName} in {sw.ElapsedMilliseconds} ms on {BackendName}; " +
                     $"caps[cloning={Caps.SupportsCloning} named={Caps.SupportsNamedSpeakers} " +
                     $"instr={Caps.SupportsInstruction} dim={Caps.SpeakerEmbeddingDim} " +
                     $"speakers={Caps.SpeakerCount} kind={Caps.ModelKind}]");
        if (_speakers.Length > 0) HostLog.Info("built-in speakers: " + string.Join(" | ", _speakers));
    }

    /// <summary>
    /// One synthesis, start to finished file. Never throws: every failure comes
    /// back as an outcome the caller can put on the wire.
    /// </summary>
    public SynthOutcome Synthesize(SynthRequest req)
    {
        var outcome = new SynthOutcome { Rate = Rate };
        if (_ctx == IntPtr.Zero) { outcome.Error = "the engine is not loaded"; return outcome; }

        string text;
        string outPath;
        try
        {
            text = Sanitize(req.Text, out string? complaint);
            if (complaint is not null) { outcome.Error = complaint; return outcome; }

            outPath = Path.GetFullPath(req.OutPath);
            if (string.IsNullOrWhiteSpace(Path.GetFileName(outPath)))
            { outcome.Error = "the output path names no file"; return outcome; }

            string? why = ValidateVoice(req);
            if (why is not null) { outcome.Error = why; return outcome; }
        }
        catch (Exception ex)
        {
            outcome.Error = "the request could not be made sense of: " + ex.Message;
            return outcome;
        }

        var p = QwenParams.StudioDefaults(req.LanguageId, _maxAudioTokens);

        // A reply is spoken in several calls, one per sentence, and Studio's 0.9 sampling makes each
        // call a fresh roll of the dice — so the same embedding came back as a recognisably DIFFERENT
        // person from one sentence to the next (playtest, 2026.08.14: "when she speaks the next she
        // is like another person"). Studio never hit this because it synthesizes one block at a time
        // and nobody compares two of them back to back. Cooling the sampling holds the timbre steady
        // across the seams; it costs a little expressive variety, which is the right trade when the
        // alternative is the listener noticing the machinery.
        p.Temperature = 0.55f;
        p.TopP = 0.85f;
        var textUtf8 = Native.Utf8(text);
        GCHandle speakerPin = default;
        var result = default(QwenResult);
        bool haveResult = false;
        var sw = Stopwatch.StartNew();

        try
        {
            switch (req.Kind)
            {
                case VoiceKind.Icl:
                    result = Native.SynthIcl(_ctx, textUtf8, Native.Utf8(req.VoicePath), ref p);
                    break;
                case VoiceKind.Embedding:
                    result = Native.SynthEmbedding(_ctx, textUtf8, Native.Utf8(req.VoicePath), ref p);
                    break;
                case VoiceKind.Speaker:
                    var name = Native.Utf8(req.Speaker)!;
                    speakerPin = GCHandle.Alloc(name, GCHandleType.Pinned);
                    p.Speaker = speakerPin.AddrOfPinnedObject();
                    result = Native.Synth(_ctx, textUtf8, ref p);
                    break;
                default:
                    result = Native.Synth(_ctx, textUtf8, ref p);
                    break;
            }
            haveResult = true;
            sw.Stop();

            if (result.Success == 0 || result.NumSamples <= 0 || result.Audio == IntPtr.Zero)
            {
                string why = Native.PtrToUtf8(result.Error) ?? Native.LastError(_ctx);
                outcome.Error = string.IsNullOrWhiteSpace(why) ? "the engine produced no sound" : why;
                return outcome;
            }

            int rate = result.SampleRate > 0 ? result.SampleRate : DefaultRate;
            float[] samples = Wav.Samples(result);
            Rate = rate;

            // Lift it to a normal speaking level BEFORE the level report below, so the log tells
            // the truth about what the game will actually play rather than about what the engine
            // handed us. The gain is logged too — a line that needed the full limit is a line worth
            // looking at.
            float gain = Wav.Normalize(samples);

            Wav.WritePcm16Atomic(outPath, samples, rate);

            outcome.Ok = true;
            outcome.Path = outPath;
            outcome.Ms = sw.ElapsedMilliseconds;
            outcome.Samples = samples.Length;
            outcome.Rate = rate;

            double secs = (double)samples.Length / rate;
            double rtf = sw.Elapsed.TotalSeconds > 0 ? secs / sw.Elapsed.TotalSeconds : 0;
            HostLog.Info($"spoke {req.Id}: {secs:F2}s of audio in {sw.ElapsedMilliseconds} ms ({rtf:F2}x) " +
                         $"[{Wav.Describe(samples, rate)} gain={gain:F2}] -> {outPath}");
            return outcome;
        }
        catch (Exception ex)
        {
            sw.Stop();
            HostLog.Error("synthesis failed for " + req.Id, ex);
            outcome.Ok = false;
            outcome.Error = ex.GetType().Name + ": " + ex.Message;
            return outcome;
        }
        finally
        {
            if (haveResult)
            {
                try { Native.FreeResult(ref result); }
                catch (Exception ex) { HostLog.Error("free_result threw", ex); }
            }
            if (speakerPin.IsAllocated) speakerPin.Free();
            GC.KeepAlive(textUtf8);
        }
    }

    // ------------------------------------------------------------ guards

    private string? ValidateVoice(SynthRequest req)
    {
        switch (req.Kind)
        {
            case VoiceKind.Icl:
            case VoiceKind.Embedding:
                if (string.IsNullOrWhiteSpace(req.VoicePath))
                    return $"the {req.Kind.ToString().ToLowerInvariant()} voice carries no path";
                if (!File.Exists(req.VoicePath))
                    return "the voice file is not there: " + req.VoicePath;
                return null;

            case VoiceKind.Speaker:
                if (string.IsNullOrWhiteSpace(req.Speaker))
                    return "the speaker voice carries no name";
                if (Caps.SupportsNamedSpeakers == 0)
                    return "this model has no named speakers";
                if (_speakers.Length > 0 &&
                    !_speakers.Any(s => string.Equals(s, req.Speaker, StringComparison.OrdinalIgnoreCase)))
                    return $"no such speaker '{req.Speaker}' (this model has: {string.Join(", ", _speakers)})";
                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Embedded NULs would cut the C string short, and control characters have no
    /// business in speech. Over-long text is trimmed at a word boundary; absurd
    /// text is refused outright.
    /// </summary>
    private static string Sanitize(string? raw, out string? complaint)
    {
        complaint = null;
        string s = raw ?? "";
        if (s.Length > HardMaxChars)
        {
            complaint = $"the text is far too long to speak ({s.Length} characters)";
            return "";
        }

        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c == '\0') continue;
            sb.Append(char.IsControl(c) && c != '\t' ? ' ' : c);
        }
        s = sb.ToString().Trim();

        if (s.Length == 0) { complaint = "there is nothing to say (empty text)"; return ""; }

        if (s.Length > SoftMaxChars)
        {
            int cut = s.LastIndexOf(' ', Math.Min(SoftMaxChars, s.Length - 1));
            if (cut < SoftMaxChars / 2) cut = SoftMaxChars;
            s = s.Substring(0, cut).TrimEnd();
            HostLog.Warn($"text trimmed to {s.Length} characters before speaking");
        }
        return s;
    }

    // ------------------------------------------------------------ plumbing

    private static int SafeCall(string what, Func<int> call)
    {
        try { return call(); }
        catch (Exception ex) { HostLog.Warn($"{what} threw: {ex.Message}"); return -1; }
    }

    public void Dispose()
    {
        IntPtr ctx = Interlocked.Exchange(ref _ctx, IntPtr.Zero);
        if (ctx == IntPtr.Zero) return;
        try { Native.Free(ctx); HostLog.Info("engine freed"); }
        catch (Exception ex) { HostLog.Error("engine free threw", ex); }
    }

    /// <summary>
    /// Speaks a whole reply in ONE generation, handing back each piece of audio the moment the
    /// engine has made it.
    /// <para>
    /// This exists because the obvious alternative is wrong twice over. Cutting a reply into
    /// sentences and synthesizing each separately gives the model a fresh roll of the dice every
    /// time, so the voice audibly changes person at each seam — the playtest word for it was
    /// "definitely an immersion breaker" — and it also pays the prefill and the speaker encode once
    /// per sentence instead of once per reply. Measured on the same card with the game running:
    /// 3.60x realtime streamed against 0.34x sentence-by-sentence. It is both the faithful road and
    /// the fast one.
    /// </para>
    /// <para>
    /// <paramref name="onChunk"/> is called from inside the native callback, in engine time. Keep it
    /// short: write the file, report it, return. It must never throw — an exception crossing back
    /// into native code is undefined, and this process exists precisely so that nothing takes the
    /// game down with it.
    /// </para>
    /// </summary>
    public SynthOutcome SynthesizeStreaming(SynthRequest req, Action<int, string, int> onChunk)
    {
        var outcome = new SynthOutcome { Rate = Rate };
        if (_ctx == IntPtr.Zero) { outcome.Error = "the engine is not loaded"; return outcome; }

        string text, firstPath;
        try
        {
            text = Sanitize(req.Text, out string? complaint);
            if (complaint is not null) { outcome.Error = complaint; return outcome; }

            firstPath = Path.GetFullPath(req.OutPath);
            if (string.IsNullOrWhiteSpace(Path.GetFileName(firstPath)))
            { outcome.Error = "the output path names no file"; return outcome; }

            string? why = ValidateVoice(req);
            if (why is not null) { outcome.Error = why; return outcome; }
        }
        catch (Exception ex) { outcome.Error = "the request could not be made sense of: " + ex.Message; return outcome; }

        string dir = Path.GetDirectoryName(firstPath) ?? ".";
        var p = QwenParams.StudioDefaults(req.LanguageId, _maxAudioTokens);
        p.Temperature = 0.55f;      // see Synthesize: cooler sampling holds the timbre steady
        p.TopP = 0.85f;
        p.ChunkSeconds = ChunkSeconds;
        p.LeftContextSeconds = LeftContextSeconds;
        p.CollectAudio = 0;         // the chunks ARE the output; no need to keep a second copy

        int index = 0, totalSamples = 0, rate = Rate;
        float gain = 0f;            // decided by the first chunk, then held for the whole reply
        Exception? inCallback = null;
        var sw = Stopwatch.StartNew();

        Native.QwenChunkCallback cb = (chunkPtr, _) =>
        {
            try
            {
                if (chunkPtr == IntPtr.Zero) return 1;
                var c = Marshal.PtrToStructure<Native.QwenChunk>(chunkPtr);
                if (c.Audio == IntPtr.Zero || c.NumSamples <= 0 || c.NumSamples > 50_000_000) return 1;

                var buf = new float[c.NumSamples];
                Marshal.Copy(c.Audio, buf, 0, c.NumSamples);
                if (c.SampleRate > 0) rate = c.SampleRate;

                // ONE gain for the whole utterance, taken from the first piece. Normalising each
                // chunk to its own peak would make the loudness breathe from sentence to sentence —
                // the same defect as the voice changing, wearing different clothes.
                if (gain <= 0f) gain = Wav.GainFor(buf);
                Wav.ApplyGain(buf, gain);

                string path = Path.Combine(dir, $"{index:000}.wav");
                Wav.WritePcm16Atomic(path, buf, rate);
                totalSamples += buf.Length;
                onChunk(index, path, buf.Length);
                index++;
                return 1;
            }
            catch (Exception ex)
            {
                // Remember it, stop the generation politely, and let the caller report it. Never
                // let it unwind into native code.
                inCallback ??= ex;
                return 0;
            }
        };

        var result = default(QwenResult);
        bool haveResult = false;
        try
        {
            switch (req.Kind)
            {
                case VoiceKind.Embedding:
                    result = Native.SynthEmbeddingStreaming(_ctx, Native.Utf8(text), Native.Utf8(req.VoicePath), ref p, cb, IntPtr.Zero);
                    break;
                case VoiceKind.Icl:
                    result = Native.SynthIclStreaming(_ctx, Native.Utf8(text), Native.Utf8(req.VoicePath), ref p, cb, IntPtr.Zero);
                    break;
                default:
                    result = Native.SynthStreaming(_ctx, Native.Utf8(text), ref p, cb, IntPtr.Zero);
                    break;
            }
            haveResult = true;
            sw.Stop();

            if (inCallback is not null)
            { outcome.Error = "a piece of the reply could not be written: " + inCallback.Message; return outcome; }

            if (result.Success == 0)
            {
                outcome.Error = Native.PtrToUtf8(result.Error) ?? "the engine gave no reason";
                return outcome;
            }
            if (index == 0) { outcome.Error = "the engine produced no audio"; return outcome; }

            outcome.Ok = true;
            outcome.Path = Path.Combine(dir, "000.wav");
            outcome.Ms = sw.ElapsedMilliseconds;
            outcome.Samples = totalSamples;
            outcome.Rate = rate;

            double secs = (double)totalSamples / Math.Max(1, rate);
            double rtf = sw.Elapsed.TotalSeconds > 0 ? secs / sw.Elapsed.TotalSeconds : 0;
            HostLog.Info($"streamed {req.Id}: {secs:F2}s of audio in {sw.ElapsedMilliseconds} ms ({rtf:F2}x) " +
                         $"in {index} pieces, gain={gain:F2}");
            return outcome;
        }
        catch (Exception ex)
        {
            sw.Stop();
            HostLog.Error("streaming synthesis failed for " + req.Id, ex);
            outcome.Error = ex.Message;
            return outcome;
        }
        finally
        {
            if (haveResult) { try { Native.FreeResult(ref result); } catch { } }
            GC.KeepAlive(cb);   // the engine holds this pointer for the whole call
        }
    }

    /// <summary>How much audio each streamed piece carries. About a second is the sweet spot found
    /// live: long enough that the ~240 ms it takes to make one never starves playback, short enough
    /// that the first words arrive in well under a second.</summary>
    public static float ChunkSeconds = 1.0f;

    /// <summary>How much already-spoken audio the engine keeps as context for the next piece. This
    /// is what carries the voice across the seams.</summary>
    public static float LeftContextSeconds = 2.0f;
}
