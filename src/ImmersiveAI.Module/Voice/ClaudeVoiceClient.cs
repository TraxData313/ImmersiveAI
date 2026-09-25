using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Voices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// The wire to claude-voice: a small HTTP server on 127.0.0.1 that owns the speech engines,
    /// the models, the voice folders and the speaker. Everything this mod asks of a voice is one of
    /// the requests below — the whole contract is claude-voice's docs/api.md.
    /// <para>
    /// WHY IT IS NOT IN HERE ANY MORE (2026.09.24, Anton's call). The mod carried its own engine
    /// host for a month: a separate exe, eight native DLLs, a streaming WAV chain into FMOD. It
    /// worked, and it cost two things that could not be fixed from inside the mod — Nexus
    /// quarantines any archive with an executable in it, so four releases sat undownloadable; and
    /// the engine was CUDA-only, so most machines got nothing. claude-voice already ran the same
    /// engine better, plus one for any PC and one that laughs, and Abby's own app speaks through
    /// it the same way. So the mod stopped being a speech engine and became a polite caller.
    /// </para>
    /// <para>
    /// Every call is off the game thread, bounded by a short timeout, and answers null or false on
    /// ANY failure rather than throwing. A voice app that is closed, busy or half-started is the
    /// normal case, not an error, and must never cost a word of the conversation.
    /// </para>
    /// </summary>
    public static class ClaudeVoiceClient
    {
        public const int DefaultPort = 8765;

        /// <summary>Localhost only, so no proxy: a corporate proxy asked to reach 127.0.0.1 answers
        /// with its own error page, which looks exactly like the app being down.</summary>
        private static readonly HttpClient Http = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };

        private static int Port
        {
            get
            {
                var p = SubModule.Config?.ClaudeVoicePort ?? DefaultPort;
                return p > 0 && p < 65536 ? p : DefaultPort;
            }
        }

        /// <summary>One POST, its JSON answer, or null. <paramref name="status"/> carries the HTTP
        /// code when there was one (0 when nothing answered at all).</summary>
        private static async Task<(JObject? body, int status)> PostAsync(string route, object? payload, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    var json = payload == null ? "{}" : JsonConvert.SerializeObject(payload);
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    using (var res = await Http.PostAsync($"http://127.0.0.1:{Port}{route}", content, cts.Token).ConfigureAwait(false))
                    {
                        var text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                        JObject? body = null;
                        try { body = string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text); }
                        catch { /* a page that is not ours: somebody else owns the port */ }
                        return (body, (int)res.StatusCode);
                    }
                }
                catch
                {
                    return (null, 0);
                }
            }
        }

        // ------------------------------------------------------------------ what it is doing

        public sealed class Health
        {
            public bool Ready;
            public string Version = string.Empty;
            public string Engine = string.Empty;          // the one the next line comes out of
            public string EngineLoaded = string.Empty;    // the one in memory now
            public bool TakesMood;
            public List<string> Sounds = new List<string>();
            public List<EngineRow> Engines = new List<EngineRow>();
            public string Error = string.Empty;
            public int Pid;                               // the app's own process: a new one has to be told our voices again
        }

        public sealed class EngineRow
        {
            public string Id = string.Empty;
            public string Label = string.Empty;
            public bool Installed;
            public bool TakesMood;
            public List<string> Sounds = new List<string>();
        }

        /// <summary>Up, warm, which engine, and what it can do. Null when nothing is listening.</summary>
        public static async Task<Health?> HealthAsync()
        {
            var (health, status) = await PostAsync("/health", null, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            if (health == null || status != 200) return null;

            var h = new Health
            {
                Ready = (bool?)health["ready"] ?? false,
                Version = (string?)health["version"] ?? string.Empty,
                Error = (string?)health["error"] ?? string.Empty,
                Pid = (int?)health["pid"] ?? 0,
            };

            // The rest from /capabilities, which answers for the engine the NEXT line comes out of.
            // An older claude-voice has no such route; it is simply told nothing about acting.
            var (caps, capsStatus) = await PostAsync("/capabilities", null, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            if (caps != null && capsStatus == 200)
            {
                h.Engine = (string?)caps["engine"] ?? string.Empty;
                h.EngineLoaded = (string?)caps["engineLoaded"] ?? string.Empty;
                h.TakesMood = (bool?)caps["instruction"] ?? false;
                h.Sounds = Strings(caps["events"]);
                if (caps["engines"] is JObject engines)
                {
                    foreach (var prop in engines.Properties())
                    {
                        var e = prop.Value as JObject;
                        h.Engines.Add(new EngineRow
                        {
                            Id = prop.Name,
                            Label = (string?)e?["label"] ?? prop.Name,
                            Installed = (bool?)e?["installed"] ?? false,
                            TakesMood = (bool?)e?["instruction"] ?? false,
                            Sounds = Strings(e?["events"]),
                        });
                    }
                }
            }
            return h;
        }

        /// <summary>Every voice the engine speaking now can speak in. Empty on any failure.</summary>
        public static async Task<List<VoicePreset>> VoicesAsync()
        {
            var list = new List<VoicePreset>();
            var (body, status) = await PostAsync("/voices", null, TimeSpan.FromSeconds(6)).ConfigureAwait(false);
            if (body == null || status != 200 || !(body["voices"] is JArray rows)) return list;
            foreach (var row in rowsOf(rows))
            {
                var id = (string?)row["id"] ?? string.Empty;
                if (id.Length == 0) continue;
                var sex = ((string?)row["sex"] ?? string.Empty).Trim().ToLowerInvariant();
                var culture = ((string?)row["culture"] ?? string.Empty).Trim().ToLowerInvariant();
                list.Add(new VoicePreset
                {
                    Id = id,
                    Name = (string?)row["name"] ?? id,
                    Gender = sex == "female" ? VoiceGender.Female : sex == "male" ? VoiceGender.Male : VoiceGender.Unknown,
                    // claude-voice files a voice of nobody in particular under "other", and a
                    // Pocket voice under its language; neither is a people of Calradia.
                    Culture = culture == "other" || culture == "english" ? string.Empty : culture,
                    Style = (string?)row["style"] ?? string.Empty,
                });
            }
            return list;

            static IEnumerable<JObject> rowsOf(JArray a)
            {
                foreach (var t in a) if (t is JObject o) yield return o;
            }
        }

        // ------------------------------------------------------------------ asking it to speak

        public enum SpeakOutcome { Spoken, NotRunning, UnknownVoice, Unreadable, Refused }

        /// <summary>
        /// Says a line. Takes the floor: whatever this mod was saying is cut off and replaced, which
        /// is the newest-words-win rule the voice has always had — while anyone ELSE's line it cuts
        /// off is said again straight after (claude-voice keeps that promise for us).
        /// </summary>
        public static async Task<SpeakOutcome> SpeakAsync(string text, string voice, string mood, string instruction = "")
        {
            var payload = new Dictionary<string, object>
            {
                ["text"] = text,
                ["voice"] = voice,
                ["project"] = "Immersive AI",
                // The screen already shows who is talking; without this every soul would be
                // introduced as the mod's name first, whenever a Claude session had spoken last.
                ["announce"] = false,
                // Silence, and our own word on screen, rather than claude-voice's spoken note about
                // characters it skipped — which in the middle of a scene is worse than nothing.
                ["unreadable"] = "refuse",
            };
            if (!string.IsNullOrWhiteSpace(mood)) payload["mood"] = mood;
            // Her own words for how it sounds; the app lets them win over a mood by name.
            if (!string.IsNullOrWhiteSpace(instruction)) payload["instruction"] = instruction;

            var (body, status) = await PostAsync("/speak", payload, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (status == 0 || body == null) return SpeakOutcome.NotRunning;
            if (status == 202 || status == 200) return SpeakOutcome.Spoken;
            if (status == 404) return SpeakOutcome.UnknownVoice;
            if (status == 422 && ((bool?)body["unreadable"] ?? false)) return SpeakOutcome.Unreadable;
            ModLog.Warn($"voice: claude-voice refused a line ({status}): {(string?)body["error"] ?? "no reason given"}");
            return SpeakOutcome.Refused;
        }

        public static Task StopAsync() => PostAsync("/stop", null, TimeSpan.FromSeconds(2));

        public static async Task<bool> SetEngineAsync(string engine)
        {
            var (_, status) = await PostAsync("/set-engine", new { engine }, TimeSpan.FromSeconds(4)).ConfigureAwait(false);
            return status == 200;
        }

        /// <summary>Asks it to read a folder of voices where it lies. See voice_lib.add_voice_root.</summary>
        public static async Task<bool> AddVoiceRootAsync(string folder)
        {
            var (_, status) = await PostAsync("/voice-roots", new { add = folder }, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            return status == 200;
        }

        public static async Task<bool> OpenPanelAsync()
        {
            var (_, status) = await PostAsync("/panel", null, TimeSpan.FromSeconds(4)).ConfigureAwait(false);
            return status == 200;
        }

        /// <summary>Where each engine's files are and how much room they take, as the app reports it.</summary>
        public sealed class Storage
        {
            public sealed class Place
            {
                public List<string> Dirs = new List<string>();
                public long Bytes;
                public bool Installed;
            }

            public Place App = new Place();
            public Dictionary<string, Place> Engines = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The /storage route (claude-voice 1.15+). Null from an older app, or on any failure.
        /// Walking a Breeze folder takes a few seconds the first time, so it is given room.</summary>
        public static async Task<Storage?> StorageAsync()
        {
            var (body, status) = await PostAsync("/storage", null, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            if (body == null || status != 200) return null;
            var s = new Storage { App = PlaceOf(body["app"] as JObject) };
            if (body["engines"] is JObject engines)
                foreach (var prop in engines.Properties())
                    s.Engines[prop.Name] = PlaceOf(prop.Value as JObject);
            return s;

            static Storage.Place PlaceOf(JObject? o) => new Storage.Place
            {
                Dirs = Strings(o?["dirs"]),
                Bytes = (long?)o?["bytes"] ?? 0,
                Installed = (bool?)o?["installed"] ?? false,
            };
        }

        public static async Task<bool> QuitAsync()
        {
            var (_, status) = await PostAsync("/quit", null, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            return status == 200;
        }

        private static List<string> Strings(JToken? token)
        {
            var list = new List<string>();
            if (token is JArray a)
                foreach (var t in a)
                    if (t.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string?)t)) list.Add(((string)t!).Trim());
            return list;
        }
    }
}
