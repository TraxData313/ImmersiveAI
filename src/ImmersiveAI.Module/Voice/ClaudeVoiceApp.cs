using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Voices;
using Newtonsoft.Json.Linq;
using TaleWorlds.Library;

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// Where claude-voice stands, as the game sees it: not on this machine, installed and asleep,
    /// waking, running, or being installed right now — and the few things the game may do about
    /// each (install it, start it, close it, open its window, change its engine).
    /// <para>
    /// A snapshot, refreshed off the game thread at most every few seconds and only while somebody
    /// is looking or speaking. Nothing here ever blocks a frame: the panel reads <see cref="Now"/>,
    /// which is always whole, and asks <see cref="Poll"/> to bring it up to date.
    /// </para>
    /// <para>
    /// THE PLAYER HEARS ABOUT IT ONCE (Anton's ask): when the app is first seen running in a
    /// session, when it closes, and — if voices are on and it is not there — one line pointing at
    /// the Voices page. Never a stream of notices, and never anything while voices are off.
    /// </para>
    /// </summary>
    public static class ClaudeVoiceApp
    {
        public enum AppState { Checking, NotInstalled, Installed, Starting, Running, Installing }

        public sealed class Snapshot
        {
            public AppState State = AppState.Checking;
            public ClaudeVoiceClient.Health? Health;
            public IReadOnlyList<VoicePreset> Voices = new List<VoicePreset>();
            public string CatalogEngine = string.Empty;
            public DateTime CatalogUtc = DateTime.MinValue;

            public bool Running => State == AppState.Running;
            public string Engine => Health?.Engine ?? string.Empty;
        }

        /// <summary>The engines claude-voice knows, in the order the panel offers them, with the
        /// words a player needs to choose — and nothing about CUDA.</summary>
        public static readonly (string Id, string Name, string Blurb)[] Engines =
        {
            ("breeze", "Breeze", "acts: laughs, sighs, whispers · English · NVIDIA 16 GB"),
            ("qwen", "Qwen", "reads every language · NVIDIA 4 GB"),
            ("pocket", "Pocket", "any PC, no graphics card · English + 5 European"),
        };

        public static string EngineName(string id)
        {
            foreach (var e in Engines) if (string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)) return e.Name;
            return string.IsNullOrEmpty(id) ? "?" : id;
        }

        private static Snapshot _now = new Snapshot();
        public static Snapshot Now => _now;

        /// <summary>Raised on the game thread whenever the snapshot changes shape (not on every poll).</summary>
        public static event Action? Changed;

        private static int _polling;
        private static DateTime _lastPollUtc = DateTime.MinValue;
        private static DateTime _startingSinceUtc = DateTime.MinValue;
        private static bool _weStartedIt;
        private static bool _toldRunning, _toldMissing;

        private static ModConfig? Config => SubModule.Config;
        private static bool VoicesOn => Config?.EnableVoice ?? false;

        // ------------------------------------------------------------------ looking

        /// <summary>Brings <see cref="Now"/> up to date in the background. Cheap to call every
        /// frame: it does nothing unless the last look is old enough, or <paramref name="force"/>.</summary>
        public static void Poll(bool force = false)
        {
            var rest = _now.Running ? TimeSpan.FromSeconds(4) : TimeSpan.FromSeconds(2);
            if (!force && DateTime.UtcNow - _lastPollUtc < rest) return;
            if (Interlocked.Exchange(ref _polling, 1) == 1) return;
            _lastPollUtc = DateTime.UtcNow;

            Task.Run(async () =>
            {
                try { await RefreshAsync().ConfigureAwait(false); }
                catch (Exception ex) { ModLog.Error("voice: looking for claude-voice", ex); }
                finally { Interlocked.Exchange(ref _polling, 0); }
            });
        }

        private static async Task RefreshAsync()
        {
            var was = _now;
            var health = await ClaudeVoiceClient.HealthAsync().ConfigureAwait(false);
            var next = new Snapshot
            {
                Health = health,
                Voices = was.Voices,
                CatalogEngine = was.CatalogEngine,
                CatalogUtc = was.CatalogUtc,
            };

            if (health != null)
            {
                next.State = health.Ready ? AppState.Running : AppState.Starting;
                _startingSinceUtc = DateTime.MinValue;

                // The catalogue is read when the app comes up, when its engine changes (each engine
                // has its own voices), and otherwise now and then — a voice added in its panel should
                // reach the game without a restart.
                var cameUp = was.Health == null;
                var engineMoved = !string.Equals(was.CatalogEngine, health.Engine, StringComparison.OrdinalIgnoreCase);
                if (cameUp || engineMoved || DateTime.UtcNow - was.CatalogUtc > TimeSpan.FromSeconds(30))
                {
                    if (cameUp) await RegisterOurVoicesAsync().ConfigureAwait(false);
                    var voices = await ClaudeVoiceClient.VoicesAsync().ConfigureAwait(false);
                    if (voices.Count > 0 || cameUp || engineMoved)
                    {
                        next.Voices = voices;
                        next.CatalogEngine = health.Engine;
                        next.CatalogUtc = DateTime.UtcNow;
                    }
                }
            }
            else if (SetupRunning)
                next.State = AppState.Installing;
            else if (_startingSinceUtc != DateTime.MinValue && DateTime.UtcNow - _startingSinceUtc < TimeSpan.FromMinutes(3))
                next.State = AppState.Starting;
            else
                next.State = FindInstall() != null ? AppState.Installed : AppState.NotInstalled;

            _now = next;

            var shapeChanged = was.State != next.State
                               || !ReferenceEquals(was.Voices, next.Voices)
                               || was.Engine != next.Engine
                               || was.Health?.EngineLoaded != next.Health?.EngineLoaded;
            if (shapeChanged)
                MainThreadDispatcher.Enqueue(() =>
                {
                    Tell(was, next);
                    try { Changed?.Invoke(); } catch (Exception ex) { ModLog.Error("voice: telling the panel", ex); }
                });
        }

        /// <summary>The few lines the player hears about the app, and only while voices are on.</summary>
        private static void Tell(Snapshot was, Snapshot now)
        {
            if (!VoicesOn) return;
            var soft = new Color(0.62f, 0.72f, 0.66f);

            if (now.Running && !_toldRunning)
            {
                _toldRunning = true;
                _toldMissing = false;
                Notice($"claude-voice is running ({EngineName(now.Engine)}) — the characters can speak.", soft);
                return;
            }

            if (was.Running && !now.Running && now.State != AppState.Starting)
            {
                _toldRunning = false;
                Notice("claude-voice has closed, so the characters are quiet. Voices → Start brings it back.", soft);
                return;
            }

            if (!_toldMissing && was.State == AppState.Checking
                && (now.State == AppState.NotInstalled || now.State == AppState.Installed))
            {
                _toldMissing = true;
                Notice(now.State == AppState.NotInstalled
                    ? "Voices are on, but the voice app isn't installed yet. Open a talk and press Voices to set it up."
                    : "Voices are on, but the voice app isn't running. Open a talk and press Voices to start it.", soft);
            }
        }

        private static void Notice(string text, Color color)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(text, color)); }
            catch { /* a notice is a courtesy */ }
        }

        // ------------------------------------------------------------------ where it lives

        /// <summary>The claude-voice note every running engine leaves behind (voice_lib.write_where).</summary>
        private static string WherePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "claude-voice", "where.json");

        public sealed class Install
        {
            public string Root = string.Empty;
            public string Python = string.Empty;
        }

        /// <summary>
        /// Where claude-voice is on this machine, or null. The note first; then the places its
        /// setup and its README put it, so a copy that has never yet been started by its new version
        /// (and so has written no note) is still found.
        /// </summary>
        public static Install? FindInstall()
        {
            try
            {
                if (File.Exists(WherePath))
                {
                    var doc = JObject.Parse(File.ReadAllText(WherePath));
                    var root = (string?)doc["root"] ?? string.Empty;
                    if (IsRoot(root))
                        return new Install { Root = root, Python = Existing((string?)doc["python"]) };
                }
            }
            catch { /* a torn note is no note */ }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var guess in new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "claude-voice"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "claude-voice"),
                Path.Combine(home, "claude-voice"),
            })
                if (IsRoot(guess)) return new Install { Root = guess };
            return null;
        }

        private static bool IsRoot(string path)
            => !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "voice_cli.py"));

        private static string Existing(string? path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path! : string.Empty;

        // ------------------------------------------------------------------ doing

        /// <summary>Wakes it, with no window. The first start loads a model — up to a minute — so
        /// this returns at once and <see cref="Now"/> says "starting" until it answers.</summary>
        public static bool Start()
        {
            var install = FindInstall();
            if (install == null) return false;
            try
            {
                var python = install.Python.Length > 0 ? install.Python : "python";
                Process.Start(new ProcessStartInfo(python, "\"" + Path.Combine(install.Root, "voice_cli.py") + "\" start")
                {
                    WorkingDirectory = install.Root,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                _startingSinceUtc = DateTime.UtcNow;
                _weStartedIt = true;
                _now = new Snapshot { State = AppState.Starting, Voices = _now.Voices, CatalogEngine = _now.CatalogEngine };
                ModLog.Info("voice: starting claude-voice in " + install.Root);
                Poll(force: true);
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: starting claude-voice", ex);
                return false;
            }
        }

        /// <summary>Closes it — the model and its memory go with it. Everything else it knows stays.</summary>
        public static void Close()
        {
            _weStartedIt = false;
            Task.Run(async () =>
            {
                await ClaudeVoiceClient.QuitAsync().ConfigureAwait(false);
                await Task.Delay(800).ConfigureAwait(false);
                Poll(force: true);
            });
        }

        /// <summary>On the way out of the game: close it only if the game opened it. A voice app the
        /// player started themselves, for Claude Code or for Abby, is theirs and stays.</summary>
        public static void CloseIfWeStartedIt()
        {
            if (!_weStartedIt) return;
            _weStartedIt = false;
            try { ClaudeVoiceClient.QuitAsync().Wait(TimeSpan.FromSeconds(2)); } catch { }
        }

        public static void OpenPanel() => Task.Run(() => ClaudeVoiceClient.OpenPanelAsync());

        public static void SetEngine(string engine)
        {
            Task.Run(async () =>
            {
                var ok = await ClaudeVoiceClient.SetEngineAsync(engine).ConfigureAwait(false);
                if (!ok) MainThreadDispatcher.Enqueue(() => Notice(
                    $"claude-voice would not switch to {EngineName(engine)} — it may not be installed yet.", Colors.Red));
                Poll(force: true);
            });
        }

        /// <summary>At the start of a session, with voices on: wake it if it is installed and asleep.</summary>
        public static void WakeIfWanted()
        {
            if (!VoicesOn || !(Config?.VoiceStartAppWithGame ?? true)) { Poll(force: true); return; }
            Task.Run(async () =>
            {
                if (await ClaudeVoiceClient.HealthAsync().ConfigureAwait(false) != null) { Poll(force: true); return; }
                if (FindInstall() != null) MainThreadDispatcher.Enqueue(() => Start());
                else Poll(force: true);
            });
        }

        // ------------------------------------------------------------------ our voices, heard there

        /// <summary>
        /// Tells the app where this mod's voices are, so it reads them where they lie: the ninety-odd
        /// that ship in the module (each people's women and men), and the player's own shelf from
        /// the days when the mod carried its own engine — so a voice they made then still speaks.
        /// Idempotent on the app's side; asked every time it comes up.
        /// </summary>
        private static async Task RegisterOurVoicesAsync()
        {
            foreach (var folder in new[] { ShippedVoicesFolder(), VoiceService.VoicesRoot })
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
                if (!await ClaudeVoiceClient.AddVoiceRootAsync(folder).ConfigureAwait(false))
                    ModLog.Warn("voice: claude-voice did not take the voices in " + folder + " (an older version? its update brings /voice-roots).");
            }
        }

        /// <summary><c>…\Modules\ImmersiveAI\Voices</c>, found by walking up from our own DLL —
        /// the folder this was built in is not the folder it runs in.</summary>
        public static string ShippedVoicesFolder()
        {
            try
            {
                var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var moduleRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(location)));
                var voices = string.IsNullOrEmpty(moduleRoot) ? string.Empty : Path.Combine(moduleRoot, "Voices");
                return Directory.Exists(voices) ? voices : string.Empty;
            }
            catch { return string.Empty; }
        }

        // ------------------------------------------------------------------ installing it

        public const string DefaultSetupUrl =
            "https://github.com/TraxData313/claude-voice/releases/latest/download/ClaudeVoiceSetup.exe";

        private static Process? _setup;
        private static int _fetching;
        private static string _setupLine = string.Empty;

        public static bool SetupRunning
        {
            get
            {
                if (Volatile.Read(ref _fetching) == 1) return true;
                try { return _setup != null && !_setup.HasExited; } catch { return false; }
            }
        }

        /// <summary>What the setup is doing, for the panel — empty when nothing is.</summary>
        public static string SetupLine => SetupRunning ? _setupLine : string.Empty;

        /// <summary>
        /// Fetches claude-voice's own setup program and opens it. The mod carries no executable of
        /// its own — that is what got it quarantined on Nexus — so the program is downloaded here,
        /// from claude-voice's releases, the moment the player asks for it.
        /// <para>
        /// Fetched by the game rather than a browser, which also means Windows does not stamp it as
        /// "from the internet" and SmartScreen does not stand in the doorway asking whether the
        /// player is sure. They already said so, in the game, one click ago.
        /// </para>
        /// </summary>
        public static void RunSetup(string? engine = null)
        {
            if (SetupRunning) return;
            Interlocked.Exchange(ref _fetching, 1);
            _setupLine = "fetching the setup…";
            _now = new Snapshot { State = AppState.Installing, Voices = _now.Voices, CatalogEngine = _now.CatalogEngine };
            MainThreadDispatcher.Enqueue(() => Changed?.Invoke());

            Task.Run(async () =>
            {
                try
                {
                    var source = (Config?.ClaudeVoiceSetupUrl ?? string.Empty).Trim();
                    if (source.Length == 0) source = DefaultSetupUrl;

                    var dir = Path.Combine(Path.GetTempPath(), "ImmersiveAI");
                    Directory.CreateDirectory(dir);
                    var exe = Path.Combine(dir, "ClaudeVoiceSetup.exe");

                    if (File.Exists(source))
                        File.Copy(source, exe, true);          // a local build — how it is tested before a release
                    else
                        await DownloadAsync(source, exe).ConfigureAwait(false);

                    var args = "--for \"Immersive AI\"";
                    if (!string.IsNullOrWhiteSpace(engine)) args += " --engine " + engine;
                    _setup = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
                    _setupLine = "the setup window is open — follow it there (Alt+Tab if it is hidden behind the game)";
                    ModLog.Info("voice: claude-voice setup opened from " + source);
                }
                catch (Exception ex)
                {
                    ModLog.Error("voice: fetching claude-voice's setup", ex);
                    MainThreadDispatcher.Enqueue(() => Notice(
                        "The voice app's setup could not be fetched — check your internet connection and try again. "
                        + "Or get it yourself: github.com/TraxData313/claude-voice", Colors.Red));
                }
                finally
                {
                    Interlocked.Exchange(ref _fetching, 0);
                    Poll(force: true);
                }
            });
        }

        private static async Task DownloadAsync(string url, string dest)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("ImmersiveAI");
                using (var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    res.EnsureSuccessStatusCode();
                    var part = dest + ".part";
                    using (var input = await res.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = File.Create(part))
                        await input.CopyToAsync(output).ConfigureAwait(false);
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(part, dest);
                }
            }
        }
    }
}
