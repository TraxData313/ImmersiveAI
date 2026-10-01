using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Llm
{
    /// <summary>
    /// The ChatGPT-subscription road: one short-lived, model-only Codex app-server session per LLM
    /// call. Authentication belongs to the installed Codex CLI (<c>codex login</c>); this client
    /// lets Codex own and persist its normal credential store, rejects API-key sessions, isolates
    /// all thread/SQLite state, disables Codex's own tools/plugins/MCP/skills, and carries the mod's
    /// hands through strict structured output. No OpenAI API key is read and there is deliberately
    /// no pay-as-you-go fallback.
    /// </summary>
    public sealed class CodexAppServerChatClient : IToolChatClient, IToolOfferPolicy
    {
        private const int TimeoutSeconds = 300;
        private const string ScratchPrefix = "immersive-ai-codex-";
        private static int _sweptStaleScratch;

        /// <summary>The whole history is flattened and the hands ride a schema rebuilt every call,
        /// so a silent hand already used this turn may leave the offer.</summary>
        public bool OfferMayNarrowMidTurn => true;

        private readonly string _model;
        private readonly string _configuredPath;
        private readonly int _maxTokens;

        public CodexAppServerChatClient(string model, string configuredPath, int maxTokens = 0)
        {
            _model = string.IsNullOrWhiteSpace(model) ? "gpt-6-sol" : model.Trim();
            _configuredPath = (configuredPath ?? string.Empty).Trim();
            _maxTokens = maxTokens;
        }

        public async Task<string> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            var envelope = await RunAsync(messages, tools: null, allowToolUse: false, cancellationToken)
                .ConfigureAwait(false);
            var result = CodexAppServerShape.ParseToolResult(envelope.ResultText);
            if (string.IsNullOrWhiteSpace(result.Text))
                throw new InvalidOperationException("Codex returned an empty response.");
            return result.Text;
        }

        public async Task<ChatResult> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            bool allowToolUse = true,
            CancellationToken cancellationToken = default)
        {
            var envelope = await RunAsync(messages, tools, allowToolUse, cancellationToken)
                .ConfigureAwait(false);
            return CodexAppServerShape.ParseToolResult(envelope.ResultText, tools);
        }

        private async Task<CodexAppServerShape.TurnEnvelope> RunAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition>? tools,
            bool allowToolUse,
            CancellationToken cancellationToken)
        {
            if (!UsageLedger.CanCall(out var capReason))
                throw new InvalidOperationException(capReason);

            var exe = FindCodex(_configuredPath);
            if (exe == null)
                throw new InvalidOperationException(
                    "Codex was not found on this machine. Install the Codex app or CLI, run 'codex login' "
                    + "once with your ChatGPT subscription, or set CodexPath in " + ModConfig.ConfigFilePath);

            var system = CodexAppServerShape.BuildSystem(messages, tools, allowToolUse, _maxTokens);
            var prompt = CodexAppServerShape.BuildTranscript(messages);
            var schema = CodexAppServerShape.BuildStrictSchema(tools, allowToolUse);
            SweepStaleScratchOnce();
            var scratch = Path.Combine(Path.GetTempPath(),
                ScratchPrefix + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(scratch);

            try
            {
                var envelope = await Task.Run(
                    () => RunProcess(exe, scratch, system, prompt, schema, cancellationToken),
                    cancellationToken).ConfigureAwait(false);

                if (!envelope.Ok)
                {
                    var reason = string.IsNullOrWhiteSpace(envelope.ErrorText)
                        ? "Codex did not complete the turn."
                        : envelope.ErrorText;
                    throw new InvalidOperationException("Codex request failed: " + Truncate(reason, 400));
                }

                // Subscription usage has no API dollar bill. Record the measured tokens and calls,
                // but explicitly suppress the API price-table estimate.
                UsageLedger.RecordCall(_model, envelope.TokensIn, envelope.TokensOut,
                    exactCostUsd: null, estimateCost: false);
                LlmGate.ReportSuccess();
                return envelope;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LlmGate.ReportFailure(LooksRateLimited(ex.Message) ? 429 : 0, "Codex", ex.Message);
                throw;
            }
            finally
            {
                DeleteScratch(scratch);
            }
        }

        // ONE FOLDER PER CALL USED TO STAY BEHIND (48 of them by 2026.10.01): the delete raced
        // codex.exe's exit — the process (or a hook it ran) still held a file for a moment, the
        // single attempt threw, and the catch swallowed it. A few short retries let the handles go.
        private static void DeleteScratch(string scratch)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (!Directory.Exists(scratch)) return;
                    Directory.Delete(scratch, recursive: true);
                    return;
                }
                catch
                {
                    if (attempt < 4) Thread.Sleep(150 * (attempt + 1));
                }
            }
        }

        /// <summary>Once per game session, off-thread: whatever an earlier crash or a stubborn
        /// handle left in %TEMP% older than a day is cleared. Best-effort, silent.</summary>
        private static void SweepStaleScratchOnce()
        {
            if (Interlocked.Exchange(ref _sweptStaleScratch, 1) != 0) return;
            Task.Run(() =>
            {
                try
                {
                    var cutoff = DateTime.UtcNow.AddDays(-1);
                    foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), ScratchPrefix + "*"))
                    {
                        try
                        {
                            if (Directory.GetLastWriteTimeUtc(dir) < cutoff)
                                Directory.Delete(dir, recursive: true);
                        }
                        catch { /* in use or locked: next session */ }
                    }
                }
                catch { /* temp hygiene only */ }
            });
        }

        private CodexAppServerShape.TurnEnvelope RunProcess(
            string exe,
            string scratch,
            string system,
            string prompt,
            string schema,
            CancellationToken cancellationToken)
        {
            // Authentication must remain in Codex's normal persistent home. A copied auth.json in
            // the per-call scratch looks safer but is subtly one-shot: when Codex refreshes its
            // managed ChatGPT token, the replacement is written into that copy and then deleted,
            // leaving the next call with stale credentials. Only transient thread/SQLite state is
            // redirected into scratch; the official Codex process alone reads or writes the login.
            var codexHome = ResolveCodexHome();
            var sqliteHome = Path.Combine(scratch, ".codex-state");
            Directory.CreateDirectory(sqliteHome);

            using (var session = new AppServerSession(exe, scratch, codexHome, sqliteHome,
                TimeSpan.FromSeconds(TimeoutSeconds), cancellationToken))
            {
                session.Initialize();
                RequireChatGptLogin(session);

                var disabled = LockedModelOnlyConfig(session);
                var start = session.Request("thread/start", new JObject
                {
                    ["model"] = _model,
                    ["modelProvider"] = "openai",
                    ["baseInstructions"] = system,
                    // The player's global AGENTS.md rides every thread with no switch to skip it.
                    ["developerInstructions"] = CodexAppServerShape.ForeignInstructionsNeutralizer,
                    ["ephemeral"] = true,
                    ["cwd"] = scratch,
                    ["environments"] = new JArray(),
                    ["selectedCapabilityRoots"] = new JArray(),
                    ["dynamicTools"] = new JArray(),
                    ["approvalPolicy"] = "never",
                    ["sandbox"] = "read-only",
                    ["config"] = disabled,
                    ["personality"] = "none",
                    ["serviceName"] = "immersive_ai",
                });
                var threadId = (string?)start.SelectToken("thread.id");
                if (string.IsNullOrWhiteSpace(threadId))
                    throw new InvalidOperationException("Codex started no thread.");

                var turnRequestId = session.SendRequest("turn/start", new JObject
                {
                    ["threadId"] = threadId,
                    ["input"] = new JArray(new JObject { ["type"] = "text", ["text"] = prompt }),
                    ["effort"] = "low",
                    ["outputSchema"] = JObject.Parse(schema),
                });

                var envelope = new CodexAppServerShape.TurnEnvelope();
                while (!envelope.Finished)
                {
                    var message = session.Next();
                    if ((int?)message["id"] == turnRequestId && message["error"] is JObject error)
                        throw new InvalidOperationException((string?)error["message"] ?? "Codex refused the turn.");
                    CodexAppServerShape.FoldTurnEvent(envelope, message, threadId!);
                }
                cancellationToken.ThrowIfCancellationRequested();

                if (CodexPlanGauge.NeedsRefresh)
                {
                    try
                    {
                        var limits = session.Request("account/rateLimits/read", new JObject());
                        CodexPlanGauge.Note(limits);
                    }
                    catch { /* a gauge must never cost the answer */ }
                }
                return envelope;
            }
        }

        private static void RequireChatGptLogin(AppServerSession session)
        {
            // False means "read the managed session". True FORCES a refresh, and must never be
            // paired with disposable credentials (the original first-call-only bug).
            var accountResult = session.Request("account/read", new JObject { ["refreshToken"] = false });
            var type = (string?)accountResult.SelectToken("account.type");
            if (!string.Equals(type, "chatgpt", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Codex needs a ChatGPT subscription sign-in. Run 'codex login' on this computer and "
                    + "choose ChatGPT, then try again. An API-key login is deliberately not used by this backend.");
        }

        private static JObject LockedModelOnlyConfig(AppServerSession session)
        {
            var disabled = new JObject
            {
                ["features.apps"] = false,
                ["features.plugins"] = false,
                ["features.browser_use"] = false,
                ["features.shell_tool"] = false,
                ["features.multi_agent"] = false,
                ["features.multi_agent_v2"] = false,
                ["agents.enabled"] = false,
                ["features.memories"] = false,
                ["web_search"] = "disabled",
                ["project_doc_max_bytes"] = 0,
                ["skills.include_instructions"] = false,
                ["orchestrator.mcp.enabled"] = false,
                ["orchestrator.skills.enabled"] = false,
            };
            foreach (var quiet in CodexAppServerShape.QuietContextOverrides)
                disabled[quiet.Key] = quiet.Value;

            // Sharing Codex's credential home also makes its config visible. Enumerate every MCP
            // entry and explicitly switch it off for this thread; the process additionally refuses
            // every server-initiated tool/permission request in Next().
            var read = session.Request("config/read", new JObject { ["includeLayers"] = false });
            if (read.SelectToken("config.mcp_servers") is JObject servers)
            {
                foreach (var server in servers.Properties())
                {
                    if (!IsSimpleConfigSegment(server.Name))
                        throw new InvalidOperationException(
                            "Codex has an MCP server name that cannot be safely disabled ('"
                            + server.Name + "'), so no model turn was started.");
                    disabled["mcp_servers." + server.Name + ".enabled"] = false;
                }
            }
            return disabled;
        }

        private static bool IsSimpleConfigSegment(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && value.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');

        private static string ResolveCodexHome()
        {
            var configured = (Environment.GetEnvironmentVariable("CODEX_HOME") ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        }

        /// <summary>The explicit override, then PATH, then the desktop app's versioned bin folders.
        /// The desktop install does not always refresh an already-running game's PATH.</summary>
        internal static string? FindCodex(string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                if (File.Exists(configuredPath)) return configuredPath;
                try
                {
                    var nested = Path.Combine(configuredPath, "codex.exe");
                    if (File.Exists(nested)) return nested;
                }
                catch { }
                return null;
            }

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    var candidate = Path.Combine(dir.Trim(), "codex.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }

            try
            {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OpenAI", "Codex", "bin");
                if (Directory.Exists(root))
                    return Directory.GetDirectories(root)
                        .Select(dir => Path.Combine(dir, "codex.exe"))
                        .Where(File.Exists)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();
            }
            catch { }
            return null;
        }

        private static bool LooksRateLimited(string text) =>
            (text ?? string.Empty).IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0
            || (text ?? string.Empty).IndexOf("usage limit", StringComparison.OrdinalIgnoreCase) >= 0
            || (text ?? string.Empty).IndexOf("429", StringComparison.Ordinal) >= 0;

        private static string Truncate(string text, int max) =>
            text.Length <= max ? text : text.Substring(0, max) + "…";

        /// <summary>A small synchronous JSONL client. The whole owner runs on Task.Run, while stderr
        /// drains asynchronously so neither pipe can fill and deadlock the game.</summary>
        private sealed class AppServerSession : IDisposable
        {
            private readonly Process _process;
            private readonly StreamReader _reader;
            private readonly StreamWriter _writer;
            private readonly Task<string> _stderr;
            private readonly DateTime _deadlineUtc;
            private readonly CancellationToken _cancellationToken;
            private readonly CancellationTokenRegistration _cancelRegistration;
            private int _counter;
            private bool _disposed;

            public AppServerSession(
                string exe,
                string workingDirectory,
                string codexHome,
                string sqliteHome,
                TimeSpan timeout,
                CancellationToken cancellationToken)
            {
                _deadlineUtc = DateTime.UtcNow + timeout;
                _cancellationToken = cancellationToken;
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "app-server --stdio",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                psi.EnvironmentVariables.Remove("OPENAI_API_KEY");
                psi.EnvironmentVariables.Remove("CODEX_API_KEY");
                psi.EnvironmentVariables["CODEX_HOME"] = codexHome;
                psi.EnvironmentVariables["CODEX_SQLITE_HOME"] = sqliteHome;

                try { _process = Process.Start(psi); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Codex could not be started (" + ex.Message
                        + "). Check CodexPath in " + ModConfig.ConfigFilePath, ex);
                }
                _reader = _process.StandardOutput;
                _writer = new StreamWriter(_process.StandardInput.BaseStream, new UTF8Encoding(false))
                {
                    AutoFlush = true,
                };
                _stderr = _process.StandardError.ReadToEndAsync();
                _cancelRegistration = cancellationToken.Register(Stop);
            }

            public void Initialize()
            {
                Request("initialize", new JObject
                {
                    ["clientInfo"] = new JObject
                    {
                        ["name"] = "immersive_ai",
                        ["title"] = "Immersive AI",
                        ["version"] = "1.0.0",
                    },
                    ["capabilities"] = new JObject { ["experimentalApi"] = true },
                });
                Send(new JObject { ["method"] = "initialized", ["params"] = new JObject() });
            }

            public JObject Request(string method, JObject parameters)
            {
                var id = SendRequest(method, parameters);
                while (true)
                {
                    var message = Next();
                    if ((int?)message["id"] != id || message["method"] != null) continue;
                    if (message["error"] is JObject error)
                        throw new InvalidOperationException("Codex refused " + method + ": "
                            + ((string?)error["message"] ?? "unknown error"));
                    return message["result"] as JObject ?? new JObject();
                }
            }

            public int SendRequest(string method, JObject parameters)
            {
                var id = ++_counter;
                Send(new JObject { ["id"] = id, ["method"] = method, ["params"] = parameters });
                return id;
            }

            public JObject Next()
            {
                while (true)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    var remaining = _deadlineUtc - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                        throw new TimeoutException("Codex was still working after " + TimeoutSeconds
                            + " seconds, so it was stopped.");

                    var read = _reader.ReadLineAsync();
                    var finished = Task.WhenAny(read, Task.Delay(remaining, _cancellationToken))
                        .GetAwaiter().GetResult();
                    if (finished != read)
                    {
                        _cancellationToken.ThrowIfCancellationRequested();
                        throw new TimeoutException("Codex was still working after " + TimeoutSeconds
                            + " seconds, so it was stopped.");
                    }
                    var line = read.GetAwaiter().GetResult();
                    if (line == null)
                        throw new InvalidOperationException("Codex closed before completing the answer. "
                            + Tail(SafeResult(_stderr), 300));

                    JObject message;
                    try { message = JObject.Parse(line); }
                    catch (JsonException) { continue; }

                    // This model-only client grants no client-side permissions or tool execution.
                    if (message["method"] != null && message["id"] != null)
                    {
                        Send(new JObject
                        {
                            ["id"] = message["id"]!.DeepClone(),
                            ["error"] = new JObject
                            {
                                ["code"] = -32601,
                                ["message"] = "Immersive AI grants no Codex tools or client permissions.",
                            },
                        });
                        continue;
                    }
                    return message;
                }
            }

            private void Send(JObject message)
            {
                _writer.WriteLine(message.ToString(Formatting.None));
            }

            private void Stop()
            {
                try
                {
                    if (!_process.HasExited) _process.Kill();
                }
                catch { }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _cancelRegistration.Dispose();
                try { _writer.Close(); } catch { }
                try
                {
                    if (!_process.HasExited && !_process.WaitForExit(500)) KillTree(_process.Id);
                }
                catch { Stop(); }
                try { if (!_process.HasExited) _process.WaitForExit(2000); } catch { }
                try { _reader.Close(); } catch { }
                _process.Dispose();
            }

            private static void KillTree(int pid)
            {
                try
                {
                    var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    var killer = Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(system, "taskkill.exe"),
                        Arguments = "/PID " + pid + " /T /F",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    });
                    if (killer == null) return;
                    using (killer) killer.WaitForExit(5000);
                }
                catch { }
            }

            private static string SafeResult(Task<string> task)
            {
                try { return task.IsCompleted ? (task.GetAwaiter().GetResult() ?? string.Empty) : string.Empty; }
                catch { return string.Empty; }
            }

            private static string Tail(string text, int max) =>
                text.Length <= max ? text : text.Substring(text.Length - max);
        }
    }
}
