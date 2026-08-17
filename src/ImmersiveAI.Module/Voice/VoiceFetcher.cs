using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Voices;
using TaleWorlds.Library;

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// The button that replaces the instructions: fetches the speech engine and its models, in the
    /// background, while the player carries on playing.
    /// <para>
    /// Setting voices up used to be four manual steps — install a Java application, open it, find
    /// its model list, pick the right one out of it. None of that was ever load-bearing: the mod has
    /// never needed Qwen-TTS Studio installed and never launches it. It needs eight native libraries
    /// out of that package and two model files from Hugging Face, and those it can perfectly well
    /// fetch itself. Studio's own window is now wanted for exactly one thing — CLONING a voice — and
    /// most players never will, because the mod ships a shelf.
    /// </para>
    /// <para>
    /// THE WORK HAPPENS IN THE HOST, not here. Same reasoning as the engine itself: 2.8 GB of
    /// streaming download and a 632 MB unzip do not belong in the process holding somebody's
    /// campaign. This class is a window onto a child process — it starts one, reads its progress
    /// lines, and keeps the last of them where the panel can draw it.
    /// </para>
    /// <para>
    /// NOTHING HERE MAY COST A LINE, in this file's usual sense: a failed errand leaves the mod
    /// exactly as it was, with one plain sentence saying what happened and an invitation to press
    /// the button again — which resumes rather than starts over.
    /// </para>
    /// </summary>
    public static class VoiceFetcher
    {
        /// <summary>Roughly what comes down the wire, for the sentence the player agrees to. Measured
        /// 2026.08.17: 632 MB of engine package, 1.94 GB and 265 MB of models.</summary>
        public const string DownloadSize = "2.8 GB";

        /// <summary>And roughly what stays on disk afterwards: 662 MB of libraries, 2.2 GB of models.
        /// Smaller than the download's own footprint would suggest because the Java application the
        /// libraries arrive inside is never written out.</summary>
        public const string DiskSize = "2.9 GB";

        private static readonly object Gate = new object();
        private static readonly Color NoticeColor = new Color(0.62f, 0.66f, 0.72f, 1f);

        private static Process? _process;
        private static string _line = string.Empty;
        private static double _fraction;
        private static string _error = string.Empty;
        private static bool _finishedWell;
        /// <summary>Stopped by the player rather than by a fault — the difference between "here is
        /// what went wrong" and a line saying nothing went wrong at all.</summary>
        private static bool _cancelled;

        public static bool IsRunning
        {
            get
            {
                lock (Gate)
                {
                    var p = _process;
                    try { return p != null && !p.HasExited; }
                    catch { return false; }
                }
            }
        }

        /// <summary>What it is doing, in the player's words. Empty when it has never run.</summary>
        public static string Line { get { lock (Gate) return _line; } }

        /// <summary>How far through the file in hand, 0..1.</summary>
        public static double Fraction { get { lock (Gate) return _fraction; } }

        /// <summary>Why the last errand failed, in words to act on. Empty when it did not.</summary>
        public static string LastError { get { lock (Gate) return _error; } }

        /// <summary>True once an errand has finished and left the local road open.</summary>
        public static bool FinishedWell { get { lock (Gate) return _finishedWell; } }

        // ------------------------------------------------------------------

        /// <summary>
        /// Whether there is anything to offer. False when the local road is already open, when the
        /// mod's own host program is missing (a different problem, with a different remedy), or when
        /// there is no NVIDIA card — see <see cref="HasNvidiaCard"/>.
        /// </summary>
        public static bool CanOffer(ModConfig? config)
        {
            try
            {
                if (IsRunning) return false;

                var setup = VoiceEngineDiscovery.Resolve(config);
                if (setup.IsComplete) return false;
                if (setup.HostExePath.Length == 0) return false;
                return HasNvidiaCard;
            }
            catch { return false; }
        }

        /// <summary>
        /// Whether this machine can run the speech engine at all.
        /// <para>
        /// Both builds of the engine package are CUDA builds; there is no CPU build to fall back to,
        /// and the engine's own CPU path has never been measured (if it lands below real time it is
        /// no use for live dialogue anyway). So on an AMD or Intel machine the 2.8 GB would be spent
        /// only to fail at model load — which is exactly the kind of thing a player should be told at
        /// the door rather than twenty minutes in.
        /// </para>
        /// <para>
        /// The test is the NVIDIA display driver's own library, which nothing else on Windows
        /// installs. Not a hardware query: WMI wants a management scope, several seconds and a
        /// dependency, to answer the same question this answers with a file check.
        /// </para>
        /// </summary>
        public static bool HasNvidiaCard
        {
            get
            {
                try
                {
                    var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    if (system.Length == 0) return false;
                    return File.Exists(Path.Combine(system, "nvcuda.dll"));
                }
                catch { return false; }
            }
        }

        /// <summary>Where the pieces will land, for the confirming sentence. Nothing is written
        /// anywhere else, and both are inside the player's own user folder — so no administrator
        /// rights are wanted at any point.</summary>
        public static string EngineTarget =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Programs", "qwen-tts-studio");

        public static string ModelTarget =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                         ".qwen-tts-studio", "models");

        // ------------------------------------------------------------------

        /// <summary>
        /// Starts the errand. Returns false when there is nothing to start or one is already running.
        /// Safe to call again after a failure: the download resumes from where it stopped.
        /// </summary>
        public static bool Start(ModConfig? config)
        {
            lock (Gate)
            {
                if (_process != null)
                {
                    try { if (!_process.HasExited) return false; }
                    catch { }
                }

                string host;
                try { host = VoiceEngineDiscovery.Resolve(config).HostExePath; }
                catch { host = string.Empty; }

                if (host.Length == 0)
                {
                    _error = "the mod's own voice program is missing from its folder — reinstalling Immersive AI puts it back";
                    ModLog.Warn("voice fetch: no host exe to run the errand with");
                    return false;
                }

                _line = "Starting…";
                _fraction = 0;
                _error = string.Empty;
                _finishedWell = false;
                _cancelled = false;

                var info = new ProcessStartInfo
                {
                    FileName = host,
                    // The parent pid matters as much here as it does for the engine, for a different
                    // reason: an errand outliving the game would be a hidden process pulling
                    // gigabytes over somebody's connection with no window to close. It dies with the
                    // game, and the next launch resumes it — which is exactly what the button
                    // promises anyway.
                    Arguments = "--fetch --engine-dir \"" + EngineTarget + "\" --model-dir \"" + ModelTarget + "\"" +
                                " --parent " + OwnPid(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = new UTF8Encoding(false),
                    StandardErrorEncoding = new UTF8Encoding(false),
                };

                try
                {
                    var process = new Process { StartInfo = info, EnableRaisingEvents = false };
                    if (!process.Start())
                    {
                        _error = "the download could not be started";
                        return false;
                    }
                    _process = process;
                    ModLog.Info($"voice fetch: started — engine to {EngineTarget}, models to {ModelTarget}");
                    Task.Run(() => Read(process));
                    return true;
                }
                catch (Exception ex)
                {
                    ModLog.Error("voice fetch: starting the errand", ex);
                    _error = ex.Message;
                    _process = null;
                    return false;
                }
            }
        }

        /// <summary>Stops it where it stands. The part-finished download stays on disk, so pressing
        /// the button again carries on rather than starting over — which is the whole reason a
        /// player may cancel this without it costing them anything.</summary>
        public static void Cancel()
        {
            Process? process;
            lock (Gate) process = _process;
            if (process == null) return;

            try { if (!process.HasExited) process.Kill(); }
            catch (Exception ex) { ModLog.Warn("voice fetch: could not stop the errand — " + ex.Message); }

            lock (Gate)
            {
                _cancelled = true;
                _line = "Stopped. Pressing it again carries on from here.";
                _fraction = 0;
            }
        }

        // ------------------------------------------------------------------

        private static void Read(Process process)
        {
            try
            {
                while (true)
                {
                    var raw = process.StandardOutput.ReadLine();
                    if (raw == null) break;

                    var message = VoiceHostProtocol.ParseMessage(raw);
                    if (!(message is VoiceFetchEvent fetch)) continue;

                    if (fetch.IsFinished) { Finish(fetch); continue; }

                    lock (Gate)
                    {
                        _line = fetch.Describe();
                        _fraction = fetch.Fraction;
                    }
                }

                // The child's stderr is the engine's usual passenger channel; in this mode it should
                // be empty, and anything on it is worth having in the log when something is wrong.
                var noise = process.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(noise)) ModLog.Warn("voice fetch: child said — " + noise.Trim());

                process.WaitForExit(5000);

                // A child that died without a verdict — killed, crashed, out of disk mid-write — must
                // still close the door, or the panel shows a bar that will never move again.
                bool stranded;
                lock (Gate) stranded = !_finishedWell && !_cancelled && _error.Length == 0;
                if (stranded)
                {
                    lock (Gate)
                    {
                        _error = "the download stopped unexpectedly. Pressing it again carries on from where it stopped";
                        _line = _error;
                    }
                    ModLog.Warn($"voice fetch: the errand exited with {Code(process)} and no verdict");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("voice fetch: reading the errand", ex);
                lock (Gate) { _error = ex.Message; _line = ex.Message; }
            }
            finally
            {
                lock (Gate) { if (ReferenceEquals(_process, process)) _process = null; }
                try { process.Dispose(); } catch { }
            }
        }

        private static void Finish(VoiceFetchEvent fetch)
        {
            lock (Gate)
            {
                _finishedWell = fetch.Ok;
                _error = fetch.Ok ? string.Empty : fetch.Error;
                _line = fetch.Describe();
                _fraction = fetch.Ok ? 1 : 0;
            }

            if (fetch.Ok)
            {
                ModLog.Info("voice fetch: the engine and its models are in place");
                // Both of these matter and neither is optional: discovery remembers what it found
                // (nothing), and the gate may already have given up for the session on that basis.
                // Without them the player would fetch 2.8 GB and still have to restart the game.
                try { VoiceEngineDiscovery.Forget(); } catch { }
                try { VoiceEngineGate.Reopen(); } catch { }
                Say("Immersive AI: the voices are ready. Open the talk screen, press Voices, and give somebody a voice.");
            }
            else
            {
                ModLog.Warn("voice fetch: failed — " + fetch.Error);
                Say("Immersive AI: the voice download did not finish — " + fetch.Error);
            }
        }

        private static void Say(string message)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                try { InformationManager.DisplayMessage(new InformationMessage(message, NoticeColor)); }
                catch { /* a notice that cannot be shown is not worth a second failure */ }
            });
        }

        private static int OwnPid()
        {
            try { using (var me = Process.GetCurrentProcess()) return me.Id; }
            catch { return 0; }
        }

        private static string Code(Process process)
        {
            try { return process.HasExited ? process.ExitCode.ToString() : "(still running)"; }
            catch { return "(unknown)"; }
        }
    }
}
