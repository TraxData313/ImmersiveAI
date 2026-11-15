using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TaleWorlds.Library;

namespace ImmersiveAI.Llm
{
    /// <summary>
    /// LM Studio as a backend of its own (2026.11.15, the owner: "we say to the players: install LM
    /// Studio, and handle all the rest from the game"). When a game is entered on the LMStudio
    /// backend the mod itself: starts LM Studio's server (its own command line, lms.exe, which also
    /// wakes the app headless), offers to download the chosen model through LM Studio when it is not
    /// on disk yet, and loads it at the mod's own context length. When the game closes it unloads the
    /// copy it loaded, so the graphics card is free again. The player never opens LM Studio's pages.
    /// </summary>
    public static class LmStudio
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        /// <summary>Where LM Studio is downloaded from, for the one message a player without it sees.</summary>
        public const string DownloadUrl = "https://lmstudio.ai";

        /// <summary>The model the mod loaded this session (null when it found it loaded already or
        /// loaded nothing) — the only one it unloads on exit.</summary>
        private static string? _loadedByMod;
        private static string? _root;
        private static int _exitHooked;

        /// <summary>The sizes shown in the download question, for the models the dropdown offers.</summary>
        private static string SizeOf(string key) =>
            key.IndexOf("gemma-4-12b", StringComparison.OrdinalIgnoreCase) >= 0 ? "7.6 GB"
            : key.IndexOf("gemma-4-e4b", StringComparison.OrdinalIgnoreCase) >= 0 ? "6.3 GB"
            : "several GB";

        /// <summary>LM Studio's command line, installed beside every LM Studio since 0.3.</summary>
        public static string? CliPath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var p = Path.Combine(home, ".lmstudio", "bin", "lms.exe");
            return File.Exists(p) ? p : null;
        }

        /// <summary>The LM Studio app itself: where it last recorded its own install, else the usual place.</summary>
        public static string? AppPath()
        {
            try
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var rec = Path.Combine(home, ".lmstudio", ".internal", "app-install-location.json");
                if (File.Exists(rec))
                {
                    var p = (string?)JObject.Parse(File.ReadAllText(rec))["path"];
                    if (!string.IsNullOrWhiteSpace(p) && File.Exists(p)) return p;
                }
            }
            catch { /* fall through to the usual place */ }
            var usual = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "LM Studio", "LM Studio.exe");
            return File.Exists(usual) ? usual : null;
        }

        /// <summary>
        /// Everything before the first word: server up, model on disk, loaded at the mod's length.
        /// <paramref name="say"/> shows a line to the player; <paramref name="ask"/> asks one yes/no.
        /// False when the player must do something first (the message has said what).
        /// </summary>
        public static async Task<bool> PrepareAsync(ModConfig config, Action<string> say, Func<string, string, Task<bool>> ask)
        {
            var root = Core.Llm.LocalModelLoad.ApiRoot(config.LocalEndpoint) ?? "http://localhost:1234";
            _root = root;
            var key = (config.LocalModel ?? string.Empty).Trim();
            if (key.Length == 0) { say("Immersive AI: pick a model under Local model in the mod's settings."); return false; }

            var models = await ListAsync(root).ConfigureAwait(false);
            if (models == null)
            {
                var cli = CliPath();
                var app = AppPath();
                if (cli == null && app == null)
                {
                    say("Immersive AI: LM Studio is not installed. Get it free from " + DownloadUrl
                        + ", open it once, then restart the game; the mod does the rest.");
                    return false;
                }
                say("Immersive AI: starting LM Studio…");
                if (cli != null) await RunCliAsync(cli, "server start", 60).ConfigureAwait(false);
                models = await WaitForServerAsync(root, 20).ConfigureAwait(false);
                if (models == null && app != null)
                {
                    // An LM Studio that was never opened may need its app up once before its server.
                    try { Process.Start(new ProcessStartInfo(app) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized }); }
                    catch (Exception ex) { ModLog.Warn("LM Studio: could not open the app: " + ex.Message); }
                    models = await WaitForServerAsync(root, 30).ConfigureAwait(false);
                    if (models == null && cli != null)
                    {
                        await RunCliAsync(cli, "server start", 60).ConfigureAwait(false);
                        models = await WaitForServerAsync(root, 30).ConfigureAwait(false);
                    }
                }
                if (models == null)
                {
                    say("Immersive AI: LM Studio did not answer at " + root + ". Open LM Studio, then restart the game.");
                    return false;
                }
            }

            if (!OnDisk(models, key))
            {
                var yes = await ask("Immersive AI — the voice of Calradia",
                    "The NPCs will speak through " + key + ", which LM Studio has not downloaded yet (" + SizeOf(key)
                    + "). Download it now? It happens in the background while you play; the NPCs answer once it is in.").ConfigureAwait(false);
                if (!yes)
                {
                    say("Immersive AI: no model yet, so the NPCs cannot speak. Restart the game to be asked again, or pick another model in the mod's settings.");
                    return false;
                }
                if (!await DownloadAsync(root, key, say).ConfigureAwait(false)) return false;
                models = await ListAsync(root).ConfigureAwait(false);
            }

            var plan = Core.Llm.LocalModelLoad.Decide(models, key, config.LocalContextWindow);
            if (plan.Step == Core.Llm.LocalModelLoad.Step.Load || plan.Step == Core.Llm.LocalModelLoad.Step.Reload)
                say("Immersive AI: loading " + key + " into your graphics card…");
            var did = await LocalModelLoader.EnsureAsync(config, CancellationToken.None).ConfigureAwait(false);
            if (did != null)
            {
                ModLog.Info(did);
                _loadedByMod = key;
                // On the way out of the GAME (as the voice app is closed), not of a campaign: the next
                // save loaded wants the model again.
                if (Interlocked.Exchange(ref _exitHooked, 1) == 0)
                    AppDomain.CurrentDomain.ProcessExit += (_, __) => ReleaseOnExit();
            }
            return true;
        }

        /// <summary>On game exit: unload the copy the mod loaded, quickly, so the card is free again.
        /// A model the player had loaded themselves is left as it was.</summary>
        public static void ReleaseOnExit()
        {
            var key = Interlocked.Exchange(ref _loadedByMod, null);
            var root = _root;
            if (key == null || root == null) return;
            try
            {
                var models = ListAsync(root, 2).GetAwaiter().GetResult();
                var rec = (models?["models"] as JArray)?.OfType<JObject>()
                    .FirstOrDefault(m => string.Equals((string?)m["key"], key, StringComparison.OrdinalIgnoreCase));
                foreach (var inst in (rec?["loaded_instances"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    var id = (string?)inst["id"];
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    PostAsync(root + "/api/v1/models/unload", new JObject { ["instance_id"] = id }, 5).GetAwaiter().GetResult();
                }
                ModLog.Info("LM Studio: unloaded " + key + " as the game closed.");
            }
            catch (Exception ex) { ModLog.Warn("LM Studio: could not unload on exit: " + ex.Message); }
        }

        // ------------------------------ the pieces ------------------------------

        private static bool OnDisk(JObject? models, string key) =>
            (models?["models"] as JArray)?.OfType<JObject>()
                .Any(m => string.Equals((string?)m["key"], key, StringComparison.OrdinalIgnoreCase)) == true;

        private static async Task<bool> DownloadAsync(string root, string key, Action<string> say)
        {
            JObject start;
            try { start = await PostAsync(root + "/api/v1/models/download", new JObject { ["model"] = key }, 30).ConfigureAwait(false); }
            catch (Exception ex)
            {
                say("Immersive AI: LM Studio refused to download " + key + " (" + ex.Message + ").");
                return false;
            }
            var job = (string?)start["job_id"];
            if ((string?)start["status"] != "already_downloaded" && !string.IsNullOrWhiteSpace(job))
            {
                say("Immersive AI: LM Studio is downloading " + key + ". You can play meanwhile.");
                int lastTenth = -1;
                while (true)
                {
                    await Task.Delay(2000).ConfigureAwait(false);
                    JObject st;
                    try { st = await GetAsync(root + "/api/v1/models/download/status/" + job, 10).ConfigureAwait(false); }
                    catch (Exception ex) { say("Immersive AI: lost sight of the download (" + ex.Message + ")."); return false; }
                    var status = (string?)st["status"];
                    if (status == "failed") { say("Immersive AI: the download of " + key + " failed. Restart the game to try again."); return false; }
                    if (status == "completed") break;
                    var total = (double?)st["total_size_bytes"] ?? 0;
                    var done = (double?)st["downloaded_bytes"] ?? 0;
                    if (total > 0)
                    {
                        int tenth = (int)(done * 10 / total);
                        if (tenth > lastTenth && tenth < 10)
                        {
                            lastTenth = tenth;
                            say($"Immersive AI: downloading {key}: {tenth * 10}% ({done / 1e9:0.0} of {total / 1e9:0.0} GB)");
                        }
                    }
                }
            }
            // LM Studio can take a moment to list a model it has just finished.
            for (int i = 0; i < 45; i++)
            {
                if (OnDisk(await ListAsync(root).ConfigureAwait(false), key)) { say("Immersive AI: " + key + " is downloaded."); return true; }
                await Task.Delay(2000).ConfigureAwait(false);
            }
            say("Immersive AI: " + key + " finished downloading but LM Studio does not list it yet. Restart the game.");
            return false;
        }

        private static async Task<JObject?> WaitForServerAsync(string root, int seconds)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < until)
            {
                var m = await ListAsync(root).ConfigureAwait(false);
                if (m != null) return m;
                await Task.Delay(1500).ConfigureAwait(false);
            }
            return null;
        }

        private static async Task<JObject?> ListAsync(string root, int seconds = 5)
        {
            try { return await GetAsync(root + "/api/v1/models", seconds).ConfigureAwait(false); }
            catch { return null; }
        }

        private static async Task<JObject> GetAsync(string url, int seconds)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds)))
            {
                var resp = await Http.GetAsync(url, cts.Token).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) throw new InvalidOperationException((int)resp.StatusCode + " " + body);
                return JObject.Parse(body);
            }
        }

        private static async Task<JObject> PostAsync(string url, JObject payload, int seconds)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds)))
            {
                var resp = await Http.PostAsync(url,
                    new StringContent(payload.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json"),
                    cts.Token).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) throw new InvalidOperationException((int)resp.StatusCode + " " + body);
                return string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
            }
        }

        private static Task RunCliAsync(string cli, string args, int seconds) => Task.Run(() =>
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo(cli, args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                }))
                {
                    if (p == null) return;
                    if (!p.WaitForExit(seconds * 1000)) { try { p.Kill(); } catch { } }
                    ModLog.Info("LM Studio: lms " + args + " → " + (p.HasExited ? p.ExitCode.ToString() : "still running")
                        + " " + p.StandardOutput.ReadToEnd().Trim());
                }
            }
            catch (Exception ex) { ModLog.Warn("LM Studio: lms " + args + " failed: " + ex.Message); }
        });

        /// <summary>One yes/no popup on the game thread, answered as a task.</summary>
        public static Task<bool> AskOnGameThread(string title, string text, string yes, string no)
        {
            var tcs = new TaskCompletionSource<bool>();
            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    TaleWorlds.Library.InformationManager.ShowInquiry(new InquiryData(title, text,
                        isAffirmativeOptionShown: true, isNegativeOptionShown: true,
                        affirmativeText: yes, negativeText: no,
                        affirmativeAction: () => tcs.TrySetResult(true),
                        negativeAction: () => tcs.TrySetResult(false)), pauseGameActiveState: true);
                }
                catch (Exception ex) { ModLog.Error("LM Studio popup", ex); tcs.TrySetResult(false); }
            });
            return tcs.Task;
        }
    }
}
