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
            /// <summary>Something that is not claude-voice answers on its port, so it cannot start.</summary>
            public bool PortTaken;
            /// <summary>Where it was found, and by which source — null when it is not on this computer.</summary>
            public Install? Install;

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
        public static void Poll(bool force = false) => Poll(force, lookOnly: false);

        /// <summary>
        /// One look that only FINDS it — installed or not, asleep or running, on which port — and
        /// touches nothing: no start, no voices told, no catalogue read. Made once as a campaign
        /// loads even with voices off (the default), so the page knows from its first frame whether
        /// claude-voice is on this computer, and turning voices on later needs no restart.
        /// Off the game thread, like every look.
        /// </summary>
        public static void Look() => Poll(force: true, lookOnly: true);

        private static void Poll(bool force, bool lookOnly)
        {
            var rest = _now.State == AppState.Installing ? TimeSpan.FromSeconds(1)
                     : _now.Running ? TimeSpan.FromSeconds(4) : TimeSpan.FromSeconds(2);
            if (!force && DateTime.UtcNow - _lastPollUtc < rest) return;
            if (Interlocked.Exchange(ref _polling, 1) == 1) return;
            _lastPollUtc = DateTime.UtcNow;

            Task.Run(async () =>
            {
                try { await RefreshAsync(lookOnly).ConfigureAwait(false); }
                catch (Exception ex) { ModLog.Error("voice: looking for claude-voice", ex); }
                finally { Interlocked.Exchange(ref _polling, 0); }
            });
        }

        private static async Task RefreshAsync(bool lookOnly)
        {
            var was = _now;
            NoticeDeadSetup();
            var setup = ReadSetupStatus();
            var installing = SetupRunning || (setup != null && setup.IsRunning && PidAlive(setup.Pid));
            // Found first, so the knock goes to the port the app says it got — not only the one in
            // our config (claude-voice moves off a taken 8765 and says where it went).
            var install = FindInstall();
            var portWas = ClaudeVoiceClient.CurrentPort;
            ClaudeVoiceClient.UsePort(install?.Port ?? 0);
            if (ClaudeVoiceClient.CurrentPort != portWas)
                ModLog.Info($"voice: claude-voice says it listens on port {ClaudeVoiceClient.CurrentPort} (was knocking on {portWas})");
            if (install != null && !SameInstall(was.Install, install))
                ModLog.Info($"voice: claude-voice found by {install.Source} in {install.Root}"
                            + (install.Version.Length > 0 ? " (" + install.Version + ")" : string.Empty)
                            + (install.IsDevCopy ? " — a developer's copy, never installed into from here" : string.Empty));
            var health = await ClaudeVoiceClient.HealthAsync().ConfigureAwait(false);
            var next = new Snapshot
            {
                Install = install,
                Health = health,
                Voices = was.Voices,
                CatalogEngine = was.CatalogEngine,
                CatalogUtc = was.CatalogUtc,
                Setup = setup,
                PortTaken = health == null && ClaudeVoiceClient.PortHeldByOther,
            };
            if (next.PortTaken && !was.PortTaken)
                ModLog.Warn("voice: port " + ClaudeVoiceClient.CurrentPort + " answers, but not as claude-voice — another program holds it");

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
                if (lookOnly) { /* only finding it: the voices and the catalogue wait until somebody wants them */ }
                else if (rootsDue && DateTime.UtcNow >= _rootsRetryUtc)
                {
                    rootsTold = await RegisterOurVoicesAsync(health.Pid).ConfigureAwait(false);
                    if (rootsTold) _rootsToldPid = health.Pid;
                    else _rootsRetryUtc = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                }

                if (!lookOnly && (cameUp || engineMoved || rootsTold || DateTime.UtcNow - was.CatalogUtc > TimeSpan.FromSeconds(30)))
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
            else if (!next.PortTaken && _startingSinceUtc != DateTime.MinValue && DateTime.UtcNow - _startingSinceUtc < TimeSpan.FromMinutes(3))
                next.State = AppState.Starting;
            else
                next.State = install != null ? AppState.Installed : AppState.NotInstalled;

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
                               || !SameSetup(was.Setup, next.Setup)
                               || (was.Install == null) != (next.Install == null);
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

        private static bool SameInstall(Install? a, Install? b)
            => a != null && b != null && a.Source == b.Source && a.Port == b.Port && VoiceAppLocator.SamePath(a.Root, b.Root);

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

            if (now.PortTaken && !was.PortTaken)
            {
                Notice($"Another program is using the voice app's port ({ClaudeVoiceClient.CurrentPort}), so the voices cannot start. "
                       + "Close that program, or give claude-voice and the game another port.", new Color(0.93f, 0.55f, 0.45f));
                return;
            }

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

        /// <summary>The key claude-voice registers itself under (its docs/discovery.md): InstallDir, Kind,
        /// Version, Launcher, Port, PortHome. Written by its installer, or by <c>voice_cli register</c>
        /// in a developer's checkout; the engine refreshes the port on every start.</summary>
        public const string RegistryKey = @"Software\claude-voice";

        public sealed class Install
        {
            public string Root = string.Empty;
            /// <summary>Which source found it: registry, where.json, apps or folder.</summary>
            public string Source = string.Empty;
            public string Version = string.Empty;
            /// <summary>The port the app says it listens on; 0 when nothing said.</summary>
            public int Port;
            internal VoiceAppRecord Record = new VoiceAppRecord();

            /// <summary>Somebody's own copy — registered as "dev", or a git checkout. The game never
            /// installs into it, rewrites it, or uninstalls it.</summary>
            public bool IsDevCopy => VoiceAppLocator.IsDevCopy(Record, p => Directory.Exists(p) || File.Exists(p));   // a worktree's .git is a file

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
        /// Where claude-voice is on this machine, or null — in the order of its discovery contract
        /// (Core <see cref="VoiceAppLocator"/>): its registry key; the where.json every running engine
        /// leaves; its Settings → Apps entry; then the places its setup and README put it. A registry
        /// read and one small file — cheap enough for every look, so a copy installed, moved or
        /// registered while the game runs is found at the next one.
        /// </summary>
        public static Install? FindInstall()
        {
            var registry = ReadRegistry();
            var where = ReadWhere();
            var candidates = new List<VoiceAppRecord?> { registry, where, ReadAppsEntry() };
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var guess in new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "claude-voice"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "claude-voice"),
                Path.Combine(home, "claude-voice"),
            })
                candidates.Add(new VoiceAppRecord { Source = "folder", Root = guess });

            var chosen = VoiceAppLocator.Choose(candidates, File.Exists);
            if (chosen == null) return null;

            // The Python a guessed folder never names: where.json's, when the note is about this copy.
            if (chosen.Python.Length == 0 && where != null && VoiceAppLocator.SamePath(where.Root, chosen.Root))
                chosen.Python = where.Python;
            return new Install
            {
                Root = chosen.Root,
                Source = chosen.Source,
                Version = chosen.Version,
                Port = VoiceAppLocator.ChoosePort(chosen, new[] { registry, where }, 0, 0),
                Record = chosen,
            };
        }

        private static VoiceAppRecord? ReadRegistry()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKey))
                {
                    if (key == null) return null;
                    return new VoiceAppRecord
                    {
                        Source = "registry",
                        Root = key.GetValue("InstallDir") as string ?? string.Empty,
                        Kind = key.GetValue("Kind") as string ?? string.Empty,
                        Launcher = key.GetValue("Launcher") as string ?? string.Empty,
                        Version = key.GetValue("Version") as string ?? string.Empty,
                        Port = key.GetValue("Port") is int p ? p : 0,
                    };
                }
            }
            catch { return null; }
        }

        private static VoiceAppRecord? ReadWhere()
        {
            try
            {
                if (!File.Exists(WherePath)) return null;
                var doc = JObject.Parse(File.ReadAllText(WherePath));
                // pythonw first: a start with no console. Older notes have only "python".
                var python = (string?)doc["pythonw"] ?? string.Empty;
                if (python.Length == 0 || !File.Exists(python)) python = (string?)doc["python"] ?? string.Empty;
                return new VoiceAppRecord
                {
                    Source = "where.json",
                    Root = (string?)doc["root"] ?? string.Empty,
                    Python = python,
                    Version = (string?)doc["version"] ?? string.Empty,
                    // The port it actually got (since claude-voice moved off a taken 8765); none on a failed start.
                    Port = doc["error"] != null ? 0 : (int?)doc["port"] ?? 0,
                };
            }
            catch { return null; }      // a torn note is no note
        }

        /// <summary>Its Settings → Apps entry, which its setup has written for every non-git install.</summary>
        private static VoiceAppRecord? ReadAppsEntry()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\claude-voice"))
                {
                    var dir = key?.GetValue("InstallLocation") as string;
                    return string.IsNullOrWhiteSpace(dir) ? null : new VoiceAppRecord { Source = "apps", Root = dir! };
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// A Python that Windows knows about (PEP 514: <c>Software\Python\PythonCore\&lt;version&gt;\InstallPath</c>,
        /// the user's first, the newest version), for a copy found with no interpreter of its own.
        /// Windowed (pythonw) where there is one. Empty when there is none — and then the game does
        /// not start anything rather than try bare "python".
        /// </summary>
        private static string RegisteredPython()
        {
            foreach (var hive in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
            {
                try
                {
                    using (var core = hive.OpenSubKey(@"Software\Python\PythonCore"))
                    {
                        if (core == null) continue;
                        var versions = core.GetSubKeyNames()
                            .Select(v => (name: v, ver: Version.TryParse(new string(v.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()), out var parsed) ? parsed : null))
                            .Where(v => v.ver != null)
                            .OrderByDescending(v => v.ver);
                        foreach (var (name, _) in versions)
                        {
                            using (var path = core.OpenSubKey(name + @"\InstallPath"))
                            {
                                if (path == null) continue;
                                var dir = path.GetValue(null) as string ?? string.Empty;
                                foreach (var exe in new[]
                                {
                                    path.GetValue("WindowedExecutablePath") as string,
                                    dir.Length > 0 ? Path.Combine(dir, "pythonw.exe") : null,
                                    path.GetValue("ExecutablePath") as string,
                                    dir.Length > 0 ? Path.Combine(dir, "python.exe") : null,
                                })
                                    if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe)) return exe!;
                            }
                        }
                    }
                }
                catch { /* a registry we may not read is a registry with no Python in it */ }
            }
            return string.Empty;
        }

        // ------------------------------------------------------------------ doing

        /// <summary>Wakes it, with no window. The first start loads a model — up to a minute — so
        /// this returns at once and <see cref="Now"/> says "starting" until it answers.</summary>
        public static bool Start()
        {
            var install = FindInstall();
            if (install == null) return false;
            // A stranger on the port: a second start would only fail to bind. Look again instead.
            if (_now.PortTaken) { Poll(force: true); return true; }
            // What its registration says starts it; else the Python its last start ran on; else one
            // Windows knows about. Never bare "python" — a game's PATH predates any Python installed since.
            var launch = VoiceAppLocator.LaunchCommand(install.Record, File.Exists)
                         ?? VoiceAppLocator.LaunchCommand(install.Record, File.Exists, RegisteredPython());
            if (launch == null)
            {
                ModLog.Warn("voice: claude-voice is in " + install.Root + " (found by " + install.Source + "), but no Python is known to start it with. "
                            + "Running 'voice_cli.py register' there, or starting it once by hand, records one.");
                return false;
            }
            try
            {
                Process.Start(new ProcessStartInfo(launch.Value.Exe, launch.Value.Args)
                {
                    WorkingDirectory = install.Root,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                _startingSinceUtc = DateTime.UtcNow;
                _weStartedIt = true;
                _now = new Snapshot { State = AppState.Starting, Install = _now.Install, Voices = _now.Voices, CatalogEngine = _now.CatalogEngine, Setup = _now.Setup };
                ModLog.Info("voice: starting claude-voice in " + install.Root + " (found by " + install.Source + ") with " + launch.Value.Exe);
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
            if (!VoicesOn) { Look(); return; }
            if (!(Config?.VoiceStartAppWithGame ?? true) || _now.State == AppState.Starting) { Poll(force: true); return; }
            Task.Run(async () =>
            {
                // Found before knocking, so the knock goes to the port it says it has.
                var install = FindInstall();
                ClaudeVoiceClient.UsePort(install?.Port ?? 0);
                if (await ClaudeVoiceClient.HealthAsync().ConfigureAwait(false) != null) { Poll(force: true); return; }
                if (install != null && !ClaudeVoiceClient.PortHeldByOther) MainThreadDispatcher.Enqueue(() => Start());
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
        /// Since claude-voice 2.0 (2026.10.17) there is no setup .exe to fetch: Smart App Control, on by
        /// default on a clean Windows 11, refuses an unsigned executable outright, and the old one also
        /// ran PowerShell hidden with its execution policy bypassed. <see cref="ClaudeVoiceInstaller"/>
        /// does what claude-voice's install.ps1 does — python.org's signed Python, checked against the
        /// hash claude-voice pins, then claude-voice's installer.py on it. The mod still carries no
        /// executable (what got it quarantined on Nexus); everything is fetched when the player asks.
        /// </para>
        /// </summary>
        public static void RunSetup(string engine, string dataDir) => LaunchSetup(engine, dataDir, quiet: true);

        /// <summary>The same program with its own window — the way round anything the quiet road cannot say.</summary>
        public static void OpenSetupWindow(string? engine) => LaunchSetup(engine, null, quiet: false);

        private static void LaunchSetup(string? engine, string? dataDir, bool quiet)
        {
            if (SetupRunning) return;
            // A developer's own copy is theirs: its setup, run from here, would reinstall packages into
            // their Python and rewrite what it records about itself (2026.10.16 — the game's quiet
            // install found Anton's clone through where.json and did both). Said, not done.
            var existing = FindInstall();
            if (existing != null && existing.IsDevCopy)
            {
                ModLog.Warn("voice: not running claude-voice's setup — " + existing.Root + " is a developer's copy");
                var why = "The voice app on this computer is a developer's own copy (" + existing.Root + "), so the game does not install "
                          + "into it. Add engines with its own setup (setup.ps1 there), then come back — the game finds them by itself.";
                MainThreadDispatcher.Enqueue(() => Notice(why, new Color(0.93f, 0.55f, 0.45f)));
                // Asleep, the page shows it as a stopped install; running, the notice is enough.
                if (!_now.Running) WriteOwnFailure(why);
                Poll(force: true);
                return;
            }
            Interlocked.Exchange(ref _fetching, 1);
            _setupLine = "Getting the installer…";
            _weInstalledIt = true;
            TryDelete(CancelPath);
            // A status file from an earlier run would otherwise stand for this one until the
            // installer writes its own -- and for good, if it dies before it does.
            TryDelete(StatusPath);
            _now = new Snapshot { State = AppState.Installing, Install = _now.Install, Voices = _now.Voices, CatalogEngine = _now.CatalogEngine, Health = _now.Health };
            MainThreadDispatcher.Enqueue(() => Changed?.Invoke());

            Task.Run(async () =>
            {
                try
                {
                    var codeFrom = (Config?.ClaudeVoiceSetupSource ?? string.Empty).Trim();
                    _setup = await ClaudeVoiceInstaller.StartAsync(engine, dataDir, quiet, codeFrom, CancelPath,
                                                                  line => _setupLine = line).ConfigureAwait(false);
                    ModLog.Info($"voice: claude-voice's installer started ({(quiet ? "quiet" : "window")}), engine {engine}");
                }
                catch (OperationCanceledException) when (File.Exists(CancelPath))
                {
                    _weInstalledIt = false;
                    WriteOwnFailure("Stopped before anything was installed.", "cancelled");
                }
                catch (Exception ex)
                {
                    _weInstalledIt = false;
                    ModLog.Error("voice: getting claude-voice's installer ready", ex);
                    WriteOwnFailure(ex is InvalidOperationException ? ex.Message
                        : "The installer could not be downloaded — check your internet connection and try again.");
                }
                finally
                {
                    Interlocked.Exchange(ref _fetching, 0);
                    Poll(force: true);
                }
            });
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
            _now = new Snapshot { State = _now.State, Install = _now.Install, Health = _now.Health, Voices = _now.Voices, CatalogEngine = _now.CatalogEngine, CatalogUtc = _now.CatalogUtc };
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

        /// <summary>Once the installer we started has exited: its downloaded source goes. If it exited with an
        /// error without saying so in its status file
        /// (killed, or it failed before writing one): written for it, so the page shows a failure rather
        /// than an install that never ends or a stale result.</summary>
        private static void NoticeDeadSetup()
        {
            var p = _setup;
            if (p == null) return;
            int code;
            try
            {
                if (!p.HasExited) return;
                code = p.ExitCode;
            }
            catch { return; }
            _setup = null;
            ClaudeVoiceInstaller.CleanUp();
            if (code == 0) return;
            var status = ReadSetupStatus();
            if (status != null && !status.IsRunning) return;
            ModLog.Warn("voice: claude-voice's installer exited with code " + code + " and left no result");
            WriteOwnFailure("The voice app's installer stopped unexpectedly (exit code " + code + "). The setup log may say why.");
        }

        /// <summary>The game's own failure — the installer never ran — written in the installer's own shape.</summary>
        private static void WriteOwnFailure(string error, string state = "failed")
        {
            try
            {
                Directory.CreateDirectory(NotesFolder);
                var doc = new JObject
                {
                    ["state"] = state, ["phase"] = "app", ["headline"] = "The install could not start",
                    ["error"] = error, ["updated"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                };
                File.WriteAllText(StatusPath, doc.ToString());
            }
            catch { }
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
        /// told not to ask again (the game already asked). It removes only what its install put there,
        /// keeps the player's own settings and voices, and refuses a developer's git checkout.
        /// <para>
        /// Only a 2.0 install's — its signed pythonw running installer.py (VoiceAppInstall.UninstallCommand).
        /// An older install's entry runs PowerShell hidden with its execution policy bypassed, which the
        /// game no longer starts: Settings → Apps opens instead, and the player removes it there.
        /// </para>
        /// </summary>
        public static bool Uninstall()
        {
            var install = FindInstall();
            if (install == null || install.IsDevCopy) return false;
            string? quiet = null;
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\claude-voice"))
                    quiet = key?.GetValue("QuietUninstallString") as string;
            }
            catch { }
            var programRoot = VoiceAppInstall.ProgramRoot(install.Record, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), File.Exists);
            var command = VoiceAppInstall.UninstallCommand(quiet, programRoot, File.Exists);
            if (command == null)
            {
                ModLog.Info("voice: claude-voice in " + install.Root + " is an older install; opening Settings → Apps to remove it");
                try { Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true }); } catch { }
                Notice("This voice app is an older install: remove \"claude-voice\" in the Settings window that just opened.",
                       new Color(0.93f, 0.55f, 0.45f));
                return false;
            }
            try
            {
                _weStartedIt = false;
                _uninstall = Process.Start(new ProcessStartInfo(command.Value.Exe, command.Value.Args)
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
