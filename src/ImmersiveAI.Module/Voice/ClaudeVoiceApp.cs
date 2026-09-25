using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// Where claude-voice stands, as the game sees it: not on this machine, being installed, installed
    /// and asleep, waking, running — and everything the game may do about each (install it without a
    /// window of its own, follow that install step by step, cancel it, start it, close it, change its
    /// engine, show where its files are, and take it off the computer again).
    /// <para>
    /// A snapshot, refreshed off the game thread at most every few seconds and only while somebody is
    /// looking or speaking. Nothing here ever blocks a frame: the page reads <see cref="Now"/>, which is
    /// always whole, and asks <see cref="Poll"/> to bring it up to date.
    /// </para>
    /// <para>
    /// THE INSTALL IS FOLLOWED THROUGH A FILE (2026.09.24, Anton: "I want the users to know what's
    /// happening"). The setup program runs with <c>--quiet</c> — no window popping over a full-screen
    /// game — and writes <c>%LOCALAPPDATA%\claude-voice\setup-status.json</c> as it goes: which of four
    /// steps, what it is fetching, how much, how fast, how long is left. The page draws that. It is a
    /// separate process, so quitting the game does not stop it, and the next game picks the progress
    /// up from the same file.
    /// </para>
    /// <para>
    /// THE GRAPHICS CARD IS HANDED BACK. The app holds its model in video memory for as long as it
    /// runs, so the game closes it on the way out whenever the game was what opened it — at a
    /// campaign's start, from the page, or by installing it — and never an app the player started
    /// themselves.
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
            /// <summary>The last install's own account of itself — running, done, failed, cancelled — or null.</summary>
            public SetupProgress? Setup;

            public bool Running => State == AppState.Running;
            public string Engine => Health?.Engine ?? string.Empty;
        }

        /// <summary>One read of setup-status.json.</summary>
        public sealed class SetupProgress
        {
            public string State = string.Empty;     // running / done / failed / cancelled
            public string Engine = string.Empty;
            public string Phase = "app";             // app / engine / model / start
            public string Headline = string.Empty;
            public string Detail = string.Empty;
            public double? PhaseFraction;
            public double? Fraction;
            public string Error = string.Empty;
            public string Log = string.Empty;
            public string DataDir = string.Empty;
            public DateTime PhaseStartedUtc;
            public DateTime StartedUtc;
            public DateTime UpdatedUtc;
            public int Pid;
            /// <summary>Started with --quiet — which only a game does.</summary>
            public bool Quiet;
            /// <summary>Installed, and only waiting for the engine to answer.</summary>
            public bool Final;

            public bool IsRunning => State == "running";
            public bool IsFailed => State == "failed";
            public bool IsCancelled => State == "cancelled";
        }

        /// <summary>The engines claude-voice knows, in the order the page offers them.</summary>
        public static readonly (string Id, string Name, string Blurb)[] Engines =
            VoiceMachine.All.Select(e => (e.Id, e.Name, e.Tagline)).ToArray();

        public static string EngineName(string id) => VoiceMachine.ById(id)?.Name ?? (string.IsNullOrEmpty(id) ? "?" : id);

        private static Snapshot _now = new Snapshot();
        public static Snapshot Now => _now;

        /// <summary>Raised on the game thread whenever the snapshot changes shape (not on every poll).</summary>
        public static event Action? Changed;

        /// <summary>Raised on the game thread the moment an install the game started comes up running.</summary>
        public static event Action? JustInstalled;

        private static int _polling;
        private static DateTime _lastPollUtc = DateTime.MinValue;
        private static DateTime _startingSinceUtc = DateTime.MinValue;
        private static bool _weStartedIt;
        private static bool _weInstalledIt;
        private static bool _toldRunning, _toldMissing;
        private static int _rootsToldPid = -1, _rootsWarnedPid = -1;
        private static DateTime _rootsRetryUtc = DateTime.MinValue;

        private static ModConfig? Config => SubModule.Config;
        private static bool VoicesOn => Config?.EnableVoice ?? false;

        // ------------------------------------------------------------------ looking

        /// <summary>Brings <see cref="Now"/> up to date in the background. Cheap to call every
        /// frame: it does nothing unless the last look is old enough, or <paramref name="force"/>.
        /// While an install runs it looks every second, so the bar moves.</summary>
        public static void Poll(bool force = false)
        {
            var rest = _now.State == AppState.Installing ? TimeSpan.FromSeconds(1)
                     : _now.Running ? TimeSpan.FromSeconds(4) : TimeSpan.FromSeconds(2);
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
            var setup = ReadSetupStatus();
            var installing = SetupRunning || (setup != null && setup.IsRunning && PidAlive(setup.Pid));
            var health = await ClaudeVoiceClient.HealthAsync().ConfigureAwait(false);
            var next = new Snapshot
            {
                Health = health,
                Voices = was.Voices,
                CatalogEngine = was.CatalogEngine,
                CatalogUtc = was.CatalogUtc,
                Setup = setup,
            };

            // A quiet install still running from an earlier session was a game's too: follow it as ours.
            if (installing && setup != null && setup.IsRunning && setup.Quiet) _weInstalledIt = true;

            // Mid-install the app may still be answering (an engine being added beside a running one), so
            // the install's own account wins — until its last step, when the app answering IS the end.
            var finishing = installing && health != null && health.Ready && setup != null && setup.IsRunning && setup.Final;
            if (installing && !finishing)
                next.State = AppState.Installing;
            else if (health != null)
            {
                next.State = health.Ready ? AppState.Running : AppState.Starting;
                _startingSinceUtc = DateTime.MinValue;

                // The catalogue is read when the app comes up, when its engine changes (each engine
                // has its own voices), and otherwise now and then — a voice added in its panel should
                // reach the game without a restart.
                var cameUp = was.Health == null;
                var engineMoved = !string.Equals(was.CatalogEngine, health.Engine, StringComparison.OrdinalIgnoreCase);

                // Our voices are told to every app PROCESS once, however it came up. "Came up" alone
                // missed the install the game ran itself (2026.09.25, the fresh-start test: the new app
                // knew not one Calradian voice): it answers during the install's last step, while the
                // page still says Installing, so by the time it counted as running it had long been seen.
                var rootsDue = health.Pid > 0 ? health.Pid != _rootsToldPid : cameUp;
                var rootsTold = false;
                if (rootsDue && DateTime.UtcNow >= _rootsRetryUtc)
                {
                    rootsTold = await RegisterOurVoicesAsync(health.Pid).ConfigureAwait(false);
                    if (rootsTold) _rootsToldPid = health.Pid;
                    else _rootsRetryUtc = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                }

                if (cameUp || engineMoved || rootsTold || DateTime.UtcNow - was.CatalogUtc > TimeSpan.FromSeconds(30))
                {
                    var voices = await ClaudeVoiceClient.VoicesAsync().ConfigureAwait(false);
                    if (voices.Count > 0 || cameUp || engineMoved || rootsTold)
                    {
                        next.Voices = voices;
                        next.CatalogEngine = health.Engine;
                        next.CatalogUtc = DateTime.UtcNow;
                    }
                }
                if (engineMoved || cameUp) _storage = null;
            }
            else if (_startingSinceUtc != DateTime.MinValue && DateTime.UtcNow - _startingSinceUtc < TimeSpan.FromMinutes(3))
                next.State = AppState.Starting;
            else
                next.State = FindInstall() != null ? AppState.Installed : AppState.NotInstalled;

            // An install the game started, now answering: it was the game that opened it, so the game
            // closes it again on the way out — and the player is told it worked.
            var justInstalled = was.State == AppState.Installing && next.Running && _weInstalledIt && (finishing || !installing);
            if (justInstalled)
            {
                _weInstalledIt = false;
                _weStartedIt = true;
            }

            _now = next;

            var shapeChanged = was.State != next.State
                               || !ReferenceEquals(was.Voices, next.Voices)
                               || was.Engine != next.Engine
                               || was.Health?.EngineLoaded != next.Health?.EngineLoaded
                               || !SameSetup(was.Setup, next.Setup);
            if (shapeChanged || justInstalled)
                MainThreadDispatcher.Enqueue(() =>
                {
                    Tell(was, next);
                    try { Changed?.Invoke(); } catch (Exception ex) { ModLog.Error("voice: telling the panel", ex); }
                    if (justInstalled)
                    {
                        // They installed voices: they want to hear them — whether or not a page is open to say so.
                        if (Config != null && !Config.EnableVoice) { Config.EnableVoice = true; Config.Save(); }
                        // And they should SEE that a program now lives on their computer (Anton, 2026.09.25:
                        // "I don't see the app opened anywhere, so how does it work?"): its own window, once.
                        OpenPanel();
                        try { JustInstalled?.Invoke(); } catch (Exception ex) { ModLog.Error("voice: after the install", ex); }
                    }
                });
        }

        private static bool SameSetup(SetupProgress? a, SetupProgress? b)
        {
            if (a == null || b == null) return a == b;
            return a.State == b.State && a.Phase == b.Phase && a.Headline == b.Headline && a.Detail == b.Detail
                   && a.PhaseFraction == b.PhaseFraction && a.Error == b.Error;
        }

        /// <summary>The few lines the player hears about the app, and only while voices are on.</summary>
        private static void Tell(Snapshot was, Snapshot now)
        {
            var soft = new Color(0.62f, 0.72f, 0.66f);

            if (was.State == AppState.Installing && now.State != AppState.Installing && now.Setup != null)
            {
                if (now.Setup.IsFailed)
                    Notice("The voice app's install stopped: " + now.Setup.Error + " Open Voices to try again.", new Color(0.93f, 0.55f, 0.45f));
                else if (now.Running)
                    Notice($"The voices are ready — {EngineName(now.Engine)} is running. Press ♪ beside any words to hear them.", soft);
                return;
            }

            if (!VoicesOn) return;

            if (now.Running && !_toldRunning)
            {
                _toldRunning = true;
                _toldMissing = false;
                Notice($"The voice app is running ({EngineName(now.Engine)}) — the characters can speak.", soft);
                return;
            }

            if (was.Running && !now.Running && now.State != AppState.Starting && now.State != AppState.Installing)
            {
                _toldRunning = false;
                Notice("The voice app has closed, so the characters are quiet. Voices → Start brings it back.", soft);
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

        private static string NotesFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "claude-voice");

        /// <summary>The claude-voice note every running engine leaves behind (voice_lib.write_where).</summary>
        private static string WherePath => Path.Combine(NotesFolder, "where.json");
        private static string StatusPath => Path.Combine(NotesFolder, "setup-status.json");
        private static string CancelPath => Path.Combine(NotesFolder, "setup-cancel");

        public sealed class Install
        {
            public string Root = string.Empty;
            public string Python = string.Empty;

            /// <summary>Somebody's own git checkout: never uninstalled from here.</summary>
            public bool IsGitCopy => Directory.Exists(Path.Combine(Root, ".git"));

            /// <summary>Where its setup put the big voice files, when a folder was chosen; else empty.</summary>
            public string DataDir
            {
                get
                {
                    try
                    {
                        var claims = Path.Combine(Root, "installed.json");
                        return File.Exists(claims) ? (string?)JObject.Parse(File.ReadAllText(claims))["data"] ?? string.Empty : string.Empty;
                    }
                    catch { return string.Empty; }
                }
            }
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
                _now = new Snapshot { State = AppState.Starting, Voices = _now.Voices, CatalogEngine = _now.CatalogEngine, Setup = _now.Setup };
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

        /// <summary>Closes it — the model leaves the graphics card and the memory with it. Everything else it knows stays.</summary>
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

        /// <summary>Whether leaving the game will also close the app — said on the page, so nobody wonders.</summary>
        public static bool ClosesWithTheGame => _weStartedIt;

        public static void OpenPanel() => Task.Run(() => ClaudeVoiceClient.OpenPanelAsync());

        public static void SetEngine(string engine)
        {
            Task.Run(async () =>
            {
                var ok = await ClaudeVoiceClient.SetEngineAsync(engine).ConfigureAwait(false);
                if (!ok) MainThreadDispatcher.Enqueue(() => Notice(
                    $"The voice app would not switch to {EngineName(engine)} — it may not be installed yet.", Colors.Red));
                _storage = null;
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

        // ------------------------------------------------------------------ where its files are

        private static ClaudeVoiceClient.Storage? _storage;
        private static int _storageFetching;

        /// <summary>The folders behind each engine and their sizes, as the app reports them. Null until asked
        /// for with <see cref="RefreshStorage"/> — walking twenty gigabytes is not a thing to do every poll.</summary>
        public static ClaudeVoiceClient.Storage? Storage => _storage;

        public static void RefreshStorage()
        {
            if (!_now.Running || Interlocked.Exchange(ref _storageFetching, 1) == 1) return;
            Task.Run(async () =>
            {
                try
                {
                    _storage = await ClaudeVoiceClient.StorageAsync().ConfigureAwait(false);
                    MainThreadDispatcher.Enqueue(() => { try { Changed?.Invoke(); } catch { } });
                }
                finally { Interlocked.Exchange(ref _storageFetching, 0); }
            });
        }

        // ------------------------------------------------------------------ our voices, heard there

        /// <summary>
        /// Tells the app where this mod's voices are, so it reads them where they lie: the ninety-odd
        /// that ship in the module (each people's women and men), and the player's own shelf from
        /// the days when the mod carried its own engine — so a voice they made then still speaks.
        /// Idempotent on the app's side; asked once of every app process. False when a folder was
        /// refused, so the caller asks again a little later.
        /// </summary>
        private static async Task<bool> RegisterOurVoicesAsync(int pid)
        {
            var all = true;
            foreach (var folder in new[] { ShippedVoicesFolder(), VoiceService.VoicesRoot })
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
                if (await ClaudeVoiceClient.AddVoiceRootAsync(folder).ConfigureAwait(false)) continue;
                all = false;
                if (_rootsWarnedPid != pid)
                {
                    _rootsWarnedPid = pid;
                    ModLog.Warn("voice: claude-voice did not take the voices in " + folder + " (an older version? its update brings /voice-roots).");
                }
            }
            if (all) ModLog.Info("voice: claude-voice (process " + pid + ") knows where our voices are");
            return all;
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

        /// <summary>What the game itself is doing before the setup has started writing its own account.</summary>
        public static string SetupLine => Volatile.Read(ref _fetching) == 1 ? _setupLine : string.Empty;

        /// <summary>
        /// Installs the voice app with the engine the player chose, its big files where they chose, and
        /// no window of its own: the page follows it through the status file.
        /// <para>
        /// The setup program is fetched here, from claude-voice's releases, the moment the player
        /// asks — the mod carries no executable, which is what got it quarantined on Nexus. Fetched
        /// by the game rather than a browser, Windows does not stamp it as "from the internet", so no
        /// SmartScreen stands in the doorway: they already said yes, in the game, one click ago.
        /// </para>
        /// </summary>
        public static void RunSetup(string engine, string dataDir) => LaunchSetup(engine, dataDir, quiet: true);

        /// <summary>The same program with its own window — the way round anything the quiet road cannot say.</summary>
        public static void OpenSetupWindow(string? engine) => LaunchSetup(engine, null, quiet: false);

        private static void LaunchSetup(string? engine, string? dataDir, bool quiet)
        {
            if (SetupRunning) return;
            Interlocked.Exchange(ref _fetching, 1);
            _setupLine = "Getting the installer…";
            _weInstalledIt = true;
            TryDelete(CancelPath);
            _now = new Snapshot { State = AppState.Installing, Voices = _now.Voices, CatalogEngine = _now.CatalogEngine, Health = _now.Health };
            MainThreadDispatcher.Enqueue(() => Changed?.Invoke());

            Task.Run(async () =>
            {
                try
                {
                    var exe = await FetchSetupAsync().ConfigureAwait(false);
                    var args = "--for \"Immersive AI\"";
                    if (!string.IsNullOrWhiteSpace(engine)) args += " --engine " + engine;
                    if (!string.IsNullOrWhiteSpace(dataDir)) args += " --data \"" + dataDir!.TrimEnd('\\') + "\"";
                    var codeFrom = (Config?.ClaudeVoiceSetupSource ?? string.Empty).Trim();
                    if (codeFrom.Length > 0) args += " --source \"" + codeFrom.TrimEnd('\\') + "\"";
                    if (quiet) args += " --quiet";
                    _setup = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = !quiet, CreateNoWindow = quiet });
                    ModLog.Info($"voice: claude-voice setup started ({(quiet ? "quiet" : "window")}) with {args}");
                }
                catch (Exception ex)
                {
                    _weInstalledIt = false;
                    ModLog.Error("voice: fetching claude-voice's setup", ex);
                    WriteOwnFailure("The installer could not be downloaded — check your internet connection and try again.");
                }
                finally
                {
                    Interlocked.Exchange(ref _fetching, 0);
                    Poll(force: true);
                }
            });
        }

        private static async Task<string> FetchSetupAsync()
        {
            var source = (Config?.ClaudeVoiceSetupUrl ?? string.Empty).Trim();
            if (source.Length == 0) source = DefaultSetupUrl;

            var dir = Path.Combine(Path.GetTempPath(), "ImmersiveAI");
            Directory.CreateDirectory(dir);
            var exe = Path.Combine(dir, "ClaudeVoiceSetup.exe");

            if (File.Exists(source)) File.Copy(source, exe, true);      // a local build — how it is tested before a release
            else await DownloadAsync(source, exe).ConfigureAwait(false);
            return exe;
        }

        /// <summary>Asks the running install to stop. It stops between two breaths and loses nothing:
        /// installing again carries on where it got to.</summary>
        public static void CancelSetup()
        {
            try
            {
                Directory.CreateDirectory(NotesFolder);
                File.WriteAllText(CancelPath, "stop");
            }
            catch (Exception ex) { ModLog.Error("voice: asking the install to stop", ex); }
            Poll(force: true);
        }

        /// <summary>Puts a finished install's failure (or cancellation) out of sight once it has been read.</summary>
        public static void DismissSetupResult()
        {
            var setup = _now.Setup;
            if (setup == null || setup.IsRunning) return;
            TryDelete(StatusPath);
            _now = new Snapshot { State = _now.State, Health = _now.Health, Voices = _now.Voices, CatalogEngine = _now.CatalogEngine, CatalogUtc = _now.CatalogUtc };
            Poll(force: true);
            try { Changed?.Invoke(); } catch { }
        }

        public static void OpenSetupLog()
        {
            var log = _now.Setup?.Log;
            if (string.IsNullOrEmpty(log) || !File.Exists(log)) log = Path.Combine(NotesFolder, "setup.log");
            OpenPath(File.Exists(log) ? log : NotesFolder);
        }

        public static SetupProgress? ReadSetupStatus()
        {
            try
            {
                if (!File.Exists(StatusPath)) return null;
                var doc = JObject.Parse(File.ReadAllText(StatusPath));
                return new SetupProgress
                {
                    State = (string?)doc["state"] ?? string.Empty,
                    Engine = (string?)doc["engine"] ?? string.Empty,
                    Phase = (string?)doc["phase"] ?? "app",
                    Headline = (string?)doc["headline"] ?? string.Empty,
                    Detail = (string?)doc["detail"] ?? string.Empty,
                    PhaseFraction = (double?)doc["phaseFraction"],
                    Fraction = (double?)doc["fraction"],
                    Error = (string?)doc["error"] ?? string.Empty,
                    Log = (string?)doc["log"] ?? string.Empty,
                    DataDir = (string?)doc["data"] ?? string.Empty,
                    PhaseStartedUtc = When(doc["phaseStarted"]),
                    StartedUtc = When(doc["started"]),
                    UpdatedUtc = When(doc["updated"]),
                    Pid = (int?)doc["pid"] ?? 0,
                    Quiet = (bool?)doc["quiet"] ?? false,
                    Final = (bool?)doc["final"] ?? false,
                };
            }
            catch { return null; }        // mid-write: the next look finds it whole
        }

        private static DateTime When(JToken? token)
        {
            var text = (string?)token;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
                ? t : DateTime.MinValue;
        }

        private static bool PidAlive(int pid)
        {
            if (pid <= 0) return false;
            try { return !Process.GetProcessById(pid).HasExited; } catch { return false; }
        }

        /// <summary>The game's own failure — the installer never ran — written in the installer's own shape.</summary>
        private static void WriteOwnFailure(string error)
        {
            try
            {
                Directory.CreateDirectory(NotesFolder);
                var doc = new JObject
                {
                    ["state"] = "failed", ["phase"] = "app", ["headline"] = "The install could not start",
                    ["error"] = error, ["updated"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                };
                File.WriteAllText(StatusPath, doc.ToString());
            }
            catch { }
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

        // ------------------------------------------------------------------ taking it away

        private static Process? _uninstall;
        private static DateTime _uninstallSettleUntilUtc = DateTime.MinValue;

        /// <summary>While the uninstaller runs — and a few seconds after, until the next look has seen
        /// the app gone, so the page never flickers back to what was just removed.</summary>
        public static bool Uninstalling
        {
            get
            {
                try { if (_uninstall != null && !_uninstall.HasExited) return true; } catch { }
                return DateTime.UtcNow < _uninstallSettleUntilUtc;
            }
        }

        /// <summary>
        /// Takes claude-voice off the computer: its own uninstaller, the same one Settings → Apps runs,
        /// told not to ask again (the game already asked). It removes only what its setup put there —
        /// never a Python or a Studio that was here before — and refuses a developer's git checkout.
        /// </summary>
        public static bool Uninstall()
        {
            var install = FindInstall();
            if (install == null || install.IsGitCopy) return false;
            var script = Path.Combine(install.Root, "uninstall.ps1");
            if (!File.Exists(script)) return false;
            try
            {
                _weStartedIt = false;
                _uninstall = Process.Start(new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\" -Yes")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                ModLog.Info("voice: uninstalling claude-voice from " + install.Root);
                Task.Run(async () =>
                {
                    try { _uninstall?.WaitForExit(); } catch { }
                    _uninstallSettleUntilUtc = DateTime.UtcNow + TimeSpan.FromSeconds(7);
                    await Task.Delay(4000).ConfigureAwait(false);   // the folder itself goes a moment after
                    _storage = null;
                    TryDelete(StatusPath);
                    Poll(force: true);
                    // And once more when the settling ends. The look above still counted as "removing",
                    // and nothing after it was going to tell the page it was over, so the page sat on
                    // "Removing the voice app…" until it was closed and opened again (2026.09.25 playtest).
                    await Task.Delay(3500).ConfigureAwait(false);
                    _uninstallSettleUntilUtc = DateTime.MinValue;
                    MainThreadDispatcher.Enqueue(() => { try { Changed?.Invoke(); } catch { } });
                });
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: uninstalling claude-voice", ex);
                return false;
            }
        }

        // ------------------------------------------------------------------ bits

        public static void OpenPath(string path)
        {
            try
            {
                if (File.Exists(path))
                    Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
                else if (Directory.Exists(path))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { ModLog.Error("voice: opening " + path, ex); }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
