using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Voice
{
    /// <summary>What came back for one utterance.</summary>
    internal sealed class VoiceHostReply
    {
        public bool Ok { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long Ms { get; set; }
        public long Samples { get; set; }
        public int Rate { get; set; }

        /// <summary>The host cut this reading short for running away. Play it — what was made before
        /// the runaway is good — but never keep it.</summary>
        public bool Derailed { get; set; }

        public static VoiceHostReply Failed(string error) => new VoiceHostReply { Ok = false, Error = error };
    }

    /// <summary>
    /// The child process that holds the speech engine, and the newline-delimited JSON we speak to it.
    /// <para>
    /// WHY A CHILD AT ALL, since it is by far the most expensive thing in this subsystem: ggml's
    /// failure mode is <c>GGML_ASSERT</c> → <c>abort()</c>, and on the game's runtime an abort is
    /// fail-fast and uncatchable. In our own process a bad tensor takes the campaign with it. Out
    /// here it takes the voices until the next line, and the player never learns it happened.
    /// </para>
    /// <para>
    /// Everything below therefore assumes the child may die at any instant, and nothing waits on it
    /// forever: every request carries a timeout, a death fails whatever was in flight rather than
    /// stranding it, and the restart ladder gives up rather than hammering. The game thread is never
    /// blocked anywhere in this file.
    /// </para>
    /// </summary>
    internal sealed class VoiceHostClient : IDisposable
    {
        // ---- how the child is told where everything is ------------------------------------
        // THE CONTRACT WITH ImmersiveAI.VoiceHost (src\ImmersiveAI.VoiceHost\HostOptions.cs), which
        // is a separate program and cannot be checked by the compiler — the names below are the
        // whole of the coupling and are verified against its parser, not guessed. It reads argv
        // first and the environment second, so both are given: the environment costs nothing and
        // means a mistyped switch still starts a working host instead of a mute one. The host
        // ignores unknown switches rather than refusing, so a mismatch here would show up as
        // "it discovered something else" rather than as an error — hence the log line at start-up
        // naming the exact command line.
        private const string ArgEngine = "--engine-dir";
        private const string ArgModels = "--model-dir";
        private const string ArgModel = "--model";
        private const string ArgParent = "--parent";
        private const string ArgTemperature = "--temperature";
        private const string ArgTopP = "--top-p";
        private const string EnvEngine = "IMMERSIVEAI_TTS_ENGINE_DIR";
        private const string EnvModels = "IMMERSIVEAI_TTS_MODEL_DIR";
        private const string EnvModel = "IMMERSIVEAI_TTS_MODEL";
        private const string EnvParent = "IMMERSIVEAI_TTS_PARENT_PID";

        /// <summary>How long bring-up may take. The measured load is ~1.5 s, but the first load of a
        /// multi-gigabyte model off a cold disk (or a CUDA context being built) is another matter
        /// entirely, and a false "it did not start" would cost the session's voices for nothing.</summary>
        private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

        /// <summary>How long one utterance may take. At the measured 4.15× realtime a full bite is a
        /// couple of seconds; this is the ceiling for something that has gone wrong, not a target.</summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(2);

        /// <summary>The restart ladder. After the last rung the voice is down for the session — a
        /// host that has died three times will die a fourth, and the player is owed one honest
        /// notice rather than a stutter of them.</summary>
        private static readonly TimeSpan[] Backoff =
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30),
        };

        private readonly object _writeGate = new object();
        private readonly SemaphoreSlim _startGate = new SemaphoreSlim(1, 1);
        /// <summary>Who wants to hear about the pieces of a streamed reply as they land, by request
        /// id. Separate from <see cref="_pending"/> because a request now produces MANY messages: a
        /// chunk line per piece and then one result line that closes it.</summary>
        private readonly ConcurrentDictionary<string, Action<int, string, long>> _chunkListeners =
            new ConcurrentDictionary<string, Action<int, string, long>>();

        private readonly ConcurrentDictionary<string, TaskCompletionSource<VoiceHostReply>> _pending =
            new ConcurrentDictionary<string, TaskCompletionSource<VoiceHostReply>>();

        private Process? _process;
        private Process? _mourned;      // the host whose death has already been counted
        private StreamWriter? _stdin;
        private TaskCompletionSource<string>? _ready;   // "" = up, otherwise the failure it reported
        private int _attempts;
        private DateTime _nextAttemptUtc = DateTime.MinValue;
        // Two counters, not one: stderr chatter must never use up the budget that would have told
        // us about a malformed reply on stdout, which is the line that actually explains a silence.
        private int _badLines;
        private int _childLines;
        private bool _disposed;
        /// <summary>Set while <see cref="StartAsync"/> is waiting on a brand-new host. A child that
        /// dies in that window is counted by StartAsync's own failure road; without this flag
        /// <see cref="OnStreamEnded"/> would count the SAME death a second time and the three-rung
        /// ladder would be spent in two tries.</summary>
        private volatile bool _startingUp;

        /// <summary>What the host said of itself when it came up.</summary>
        public string Backend { get; private set; } = string.Empty;
        public string Model { get; private set; } = string.Empty;
        public int SampleRate { get; private set; }

        public bool IsRunning
        {
            get
            {
                var p = _process;
                try { return p != null && !p.HasExited; }
                catch { return false; }
            }
        }

        public bool IsReady => IsRunning && _ready != null && _ready.Task.IsCompleted
                               && _ready.Task.Status == TaskStatus.RanToCompletion
                               && _ready.Task.Result.Length == 0;

        // ------------------------------------------------------------------
        // Bring-up
        // ------------------------------------------------------------------

        /// <summary>
        /// Makes sure the host is up, starting it if need be. Safe to call from anywhere and as
        /// often as you like: one starter at a time, and a refusal is remembered so a dead engine is
        /// not re-launched on every line.
        /// </summary>
        public async Task<bool> EnsureStartedAsync(VoiceEngineSetup setup)
        {
            if (_disposed) return false;
            if (IsReady) return true;
            if (VoiceEngineGate.DownForSession) return false;

            await _startGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed) return false;
                if (IsReady) return true;
                if (VoiceEngineGate.DownForSession) return false;
                if (DateTime.UtcNow < _nextAttemptUtc) return false;   // resting between rungs

                Teardown();                                            // a half-dead host helps nobody
                return await StartAsync(setup).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: starting the speech host", ex);
                NoteFailedStart(ex.Message);
                return false;
            }
            finally
            {
                try { _startGate.Release(); } catch { }
            }
        }

        private async Task<bool> StartAsync(VoiceEngineSetup setup)
        {
            if (!setup.IsComplete)
            {
                VoiceEngineGate.ReportDown(setup.Missing);
                return false;
            }

            var info = new ProcessStartInfo
            {
                FileName = setup.HostExePath,
                Arguments = BuildArguments(setup),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                // The engine's ggml/CUDA siblings sit beside it and are found by the loader's own
                // rules; standing in that folder (and putting it first on PATH) is what makes a
                // plain DllNotFoundException into a successful load.
                WorkingDirectory = setup.EnginePath,
            };

            var path = info.EnvironmentVariables.ContainsKey("PATH") ? info.EnvironmentVariables["PATH"] : string.Empty;
            info.EnvironmentVariables["PATH"] = setup.EnginePath + ";" + path;
            info.EnvironmentVariables[EnvEngine] = setup.EnginePath;
            info.EnvironmentVariables[EnvModels] = setup.ModelDir;
            info.EnvironmentVariables[EnvModel] = setup.ModelName;
            info.EnvironmentVariables[EnvParent] = OwnPid().ToString();

            ModLog.Info($"voice: starting host — \"{info.FileName}\" {info.Arguments}");

            var ready = new TaskCompletionSource<string>();
            _ready = ready;
            _badLines = 0;
            _childLines = 0;

            var process = new Process { StartInfo = info, EnableRaisingEvents = false };
            _startingUp = true;
            try
            {
                return await StartAndWaitAsync(process, ready).ConfigureAwait(false);
            }
            finally
            {
                _startingUp = false;
            }
        }

        private async Task<bool> StartAndWaitAsync(Process process, TaskCompletionSource<string> ready)
        {
            if (!process.Start())
            {
                NoteFailedStart("the voice host would not start");
                return false;
            }

            _process = process;
            _stdin = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = false };

            Pump("ImmersiveAI voice host stdout", () => ReadLines(process, process.StandardOutput, HandleLine));
            Pump("ImmersiveAI voice host stderr", () => ReadLines(process, process.StandardError, LogChildError));

            var settled = await Task.WhenAny(ready.Task, Task.Delay(StartupTimeout)).ConfigureAwait(false);
            if (settled != ready.Task)
            {
                Teardown();
                NoteFailedStart("the speech engine did not answer within " + StartupTimeout.TotalMinutes.ToString("0") + " minutes");
                return false;
            }

            var error = ready.Task.Result;
            if (error.Length > 0)
            {
                Teardown();
                NoteFailedStart(error);
                return false;
            }

            _attempts = 0;
            _nextAttemptUtc = DateTime.MinValue;
            ModLog.Info($"voice: host ready — {Backend} · {Model} · {SampleRate} Hz");
            VoiceEngineGate.ReportSuccess();
            return true;
        }

        private static string BuildArguments(VoiceEngineSetup setup)
        {
            var parts = new List<string>();

            // An EMPTY value is left out rather than passed as "": the host reads an empty string as
            // "I was told, and told nothing", which would stop it discovering for itself.
            void Add(string flag, string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                parts.Add(flag);
                parts.Add(Quote(value));
            }

            Add(ArgEngine, setup.EnginePath);
            Add(ArgModels, setup.ModelDir);
            Add(ArgModel, setup.ModelName);
            parts.Add(ArgParent);
            parts.Add(OwnPid().ToString());

            // Only when the player has moved them off the host's own defaults, so an untouched
            // config never pins a number the host may later want to change on its own.
            var config = VoiceService.Config;
            if (config != null)
            {
                if (config.VoiceTemperature > 0f)
                {
                    parts.Add(ArgTemperature);
                    parts.Add(config.VoiceTemperature.ToString("0.###", CultureInfo.InvariantCulture));
                }
                if (config.VoiceTopP > 0f)
                {
                    parts.Add(ArgTopP);
                    parts.Add(config.VoiceTopP.ToString("0.###", CultureInfo.InvariantCulture));
                }
            }

            return string.Join(" ", parts);
        }

        /// <summary>Quoting for a Windows command line. The trailing backslash is the trap: a folder
        /// path ending in one turns <c>"C:\dir\"</c> into an escaped quote, and the child receives a
        /// mangled argument and every following one.</summary>
        private static string Quote(string value)
        {
            var s = (value ?? string.Empty).TrimEnd('\\');
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        }

        private static int OwnPid()
        {
            try { return Process.GetCurrentProcess().Id; }
            catch { return 0; }
        }

        private void NoteFailedStart(string why)
        {
            var rung = _attempts;
            _attempts++;

            if (_attempts >= Backoff.Length)
            {
                VoiceEngineGate.ReportDown(why);
                return;
            }

            var wait = Backoff[Math.Min(rung, Backoff.Length - 1)];
            _nextAttemptUtc = DateTime.UtcNow + wait;
            ModLog.Warn($"voice: host did not come up ({why}) — trying again in {wait.TotalSeconds:0}s.");
        }

        // ------------------------------------------------------------------
        // The wire
        // ------------------------------------------------------------------

        /// <summary>
        /// One utterance. Returns an honest failure rather than throwing — a voice that cannot be
        /// made is a line that is read instead of heard, and that is all it ever costs.
        /// </summary>
        public async Task<VoiceHostReply> SynthesizeAsync(
            string id,
            string text,
            string outPath,
            string voiceKind,
            string voicePath,
            string speaker,
            int languageId,
            VoiceEngineSetup setup,
            CancellationToken cancel = default,
            Action<int, string, long>? onChunk = null,
            bool whole = false,
            int maxTokens = 0,
            long expectedSamples = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) return VoiceHostReply.Failed("no id");
            if (string.IsNullOrWhiteSpace(text)) return VoiceHostReply.Failed("nothing to say");

            if (!await EnsureStartedAsync(setup).ConfigureAwait(false))
                return VoiceHostReply.Failed("the speech engine is not running");

            var waiter = new TaskCompletionSource<VoiceHostReply>();
            if (!_pending.TryAdd(id, waiter))
                return VoiceHostReply.Failed("that line is already being spoken");
            if (onChunk != null) _chunkListeners[id] = onChunk;

            try
            {
                var request = new VoiceSynthesizeRequest
                {
                    Id = id,
                    Text = text,
                    OutPath = outPath,
                    Voice = new VoiceSource
                    {
                        Kind = VoiceSource.ParseKind(voiceKind),
                        Path = voicePath ?? string.Empty,
                        Speaker = speaker ?? string.Empty,
                    },
                    LanguageId = languageId,
                    Whole = whole,
                    MaxTokens = maxTokens,
                    ExpectedSamples = expectedSamples,
                };

                // Caught here rather than at the far end of a pipe: the host would answer the same
                // refusal a round trip later, and this way the reason names our own bug.
                if (!request.Validate(out var wrong))
                    return VoiceHostReply.Failed(wrong);

                if (!Send(request.Serialize()))
                    return VoiceHostReply.Failed("the speech engine could not be reached");

                using (var timeout = new CancellationTokenSource(RequestTimeout))
                using (var either = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancel))
                {
                    var done = await Task.WhenAny(waiter.Task, Delay(either.Token)).ConfigureAwait(false);
                    if (done == waiter.Task) return waiter.Task.Result;

                    // Timed out, or the caller has moved on. Tell the host so a request still in its
                    // queue is dropped; one already inside the engine will finish and be cached.
                    Cancel(id);
                    return VoiceHostReply.Failed(cancel.IsCancellationRequested
                        ? "the line was let go"
                        : "the speech engine took too long");
                }
            }
            catch (Exception ex)
            {
                return VoiceHostReply.Failed(ex.Message);
            }
            finally
            {
                _pending.TryRemove(id, out _);
                _chunkListeners.TryRemove(id, out _);
            }
        }

        /// <summary>Drops a request the player has moved past. Best-effort by nature: a call already
        /// inside the engine cannot be interrupted, and its audio is kept rather than thrown away.</summary>
        public void Cancel(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !IsRunning) return;
            Send(new VoiceCancelRequest(id).Serialize());
        }

        /// <summary>Writes one already-serialized line. Serializing is <see cref="VoiceHostRequest"/>'s
        /// own business — it is what guarantees the line is compact, ASCII-escaped and newline-free.</summary>
        private bool Send(string line)
        {
            var writer = _stdin;
            if (writer == null || !IsRunning) return false;

            try
            {
                lock (_writeGate)
                {
                    writer.Write(line);
                    writer.Write('\n');
                    writer.Flush();
                }
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("voice: could not reach the speech host — " + ex.Message);
                return false;
            }
        }

        private void HandleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            // Read through Core's own protocol types rather than by hand. The field names then live
            // in ONE place for all three implementations, and the unit tests over them are actually
            // guarding this reader instead of a parallel copy of it that could quietly drift.
            var message = VoiceHostProtocol.ParseMessage(line);
            if (message == null)
            {
                // Never fatal. A host that prints a stray banner or a native library that writes to
                // stdout must cost us nothing but a log line — and only the first few of those, or a
                // chatty library would fill the log with the same complaint.
                if (Interlocked.Increment(ref _badLines) <= 5)
                    ModLog.Warn("voice: unreadable line from the speech host — " + Shorten(line, 200));
                return;
            }

            if (message is VoiceReadyEvent ready)
            {
                Backend = ready.Backend;
                Model = ready.Model;
                SampleRate = ready.Rate;
                _ready?.TrySetResult(string.Empty);
                return;
            }

            if (message is VoiceFailedEvent failed)
            {
                var why = failed.Error.Length > 0 ? failed.Error : "the speech engine could not start";
                _ready?.TrySetResult(why);
                return;
            }

            if (message is VoicePongEvent) return;

            if (message is VoiceChunkEvent piece)
            {
                // A piece of a streamed reply. It does NOT close the request — the result line does.
                if (piece.Id.Length == 0) return;
                if (_chunkListeners.TryGetValue(piece.Id, out var listener))
                {
                    try { listener(piece.Index, piece.Path, piece.Samples); }
                    catch (Exception ex) { ModLog.Error("voice: taking a spoken piece", ex); }
                }
                return;
            }

            if (!(message is VoiceSynthesisResult result)) return;
            if (result.Id.Length == 0)
            {
                // A refusal with no id to echo — a malformed request, or an op this host does not
                // know. Nobody is waiting on it, but it is exactly the line that explains a silence.
                if (!result.Ok && Interlocked.Increment(ref _badLines) <= 5)
                    ModLog.Warn("voice: the speech host refused a line — " + Shorten(result.Error, 200));
                return;
            }

            if (!_pending.TryRemove(result.Id, out var waiter)) return;   // cancelled, or already timed out

            waiter.TrySetResult(new VoiceHostReply
            {
                Ok = result.Ok,
                Path = result.Path,
                Error = result.Error,
                Ms = result.Ms,
                Samples = result.Samples,
                Rate = result.Rate,
                Derailed = result.Derailed,
            });
        }

        private void LogChildError(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            if (Interlocked.Increment(ref _childLines) <= 40) ModLog.Warn("voice host: " + Shorten(line, 400));
        }

        private void ReadLines(Process owner, StreamReader reader, Action<string> onLine)
        {
            try
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    try { onLine(line); }
                    catch (Exception ex) { ModLog.Error("voice: handling a line from the host", ex); }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("voice: the host's output ended — " + ex.Message);
            }
            finally
            {
                OnStreamEnded(owner);
            }
        }

        /// <summary>
        /// The host's stdout closed, which on this design means it is gone. Whoever was waiting is
        /// told so at once rather than sitting out the full request timeout.
        /// <para>
        /// Tied to the process it belongs to on purpose. A deliberate teardown also ends these
        /// streams, and without this check a host we put away ourselves would climb the restart
        /// ladder a second time for the same failure — burning through the session's patience in
        /// half the tries it was given.
        /// </para>
        /// </summary>
        private void OnStreamEnded(Process owner)
        {
            if (_disposed) return;
            if (!ReferenceEquals(_process, owner)) return;   // one we already put away

            try { if (!owner.HasExited) return; }            // stderr closing first is not a death
            catch { return; }

            // stdout and stderr both end when it dies, and both land here. One death, one rung.
            if (ReferenceEquals(Interlocked.Exchange(ref _mourned, owner), owner)) return;

            var code = ExitCodeOrNull();
            _ready?.TrySetResult("the speech engine stopped" + (code == null ? string.Empty : " (exit " + code + ")"));

            var stranded = 0;
            foreach (var key in new List<string>(_pending.Keys))
                if (_pending.TryRemove(key, out var waiter))
                {
                    waiter.TrySetResult(VoiceHostReply.Failed("the speech engine stopped"));
                    stranded++;
                }

            if (stranded > 0 || code != null)
                ModLog.Warn($"voice: the speech host exited{(code == null ? string.Empty : " with code " + code)}"
                            + (stranded > 0 ? $", {stranded} line(s) unspoken" : string.Empty) + ".");

            // The next line asks for it again; the ladder decides whether it is given. But a death
            // DURING bring-up already belongs to StartAsync, which is waiting on the very result we
            // just set — counting it here too would spend two rungs on one failure.
            if (!_startingUp) NoteFailedStart("the speech engine stopped on its own");
        }

        private int? ExitCodeOrNull()
        {
            try
            {
                var p = _process;
                return p != null && p.HasExited ? p.ExitCode : (int?)null;
            }
            catch { return null; }
        }

        private static void Pump(string name, Action work)
        {
            var thread = new Thread(() => { try { work(); } catch { } })
            {
                IsBackground = true,      // never keeps the game alive for a heartbeat
                Name = name,
            };
            thread.Start();
        }

        private static async Task Delay(CancellationToken token)
        {
            try { await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        // ------------------------------------------------------------------

        private void Teardown()
        {
            var process = _process;
            var stdin = _stdin;
            _process = null;
            _stdin = null;

            if (process == null) return;

            try
            {
                if (!process.HasExited && stdin != null)
                {
                    lock (_writeGate)
                    {
                        stdin.Write(new VoiceQuitRequest().Serialize());
                        stdin.Write('\n');
                        stdin.Flush();
                    }
                    process.WaitForExit(1000);
                }
            }
            catch { /* a host that will not be asked politely is killed below */ }

            try { if (!process.HasExited) process.Kill(); }
            catch { /* already gone, or beyond our reach */ }

            try { stdin?.Dispose(); } catch { }
            try { process.Dispose(); } catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var key in new List<string>(_pending.Keys))
                if (_pending.TryRemove(key, out var waiter))
                    waiter.TrySetResult(VoiceHostReply.Failed("the voice was put away"));

            Teardown();
            try { _startGate.Dispose(); } catch { }
        }

        private static string Shorten(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s ?? string.Empty : s.Substring(0, max) + "…";
    }
}
