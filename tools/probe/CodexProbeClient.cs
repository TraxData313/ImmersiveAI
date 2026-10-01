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

namespace ImmersiveAI.Probe
{
    /// <summary>How one probe run talks to Codex. Defaults reproduce the mod exactly
    /// (src/ImmersiveAI.Module/Llm/CodexAppServerChatClient.cs as of 2026-10-01).</summary>
    public sealed class CodexOptions
    {
        public string Model = "gpt-6-sol";
        public string Effort = "low";
        public bool Ephemeral = true;
        /// <summary>Extra thread/start config overrides, merged over the mod's own.</summary>
        public JObject ExtraConfig = new JObject();
        /// <summary>CODEX_HOME for the spawned process; null = the real ~/.codex (as the mod).</summary>
        public string? CodexHome;
        /// <summary>Optional: replace the "developerInstructions" the mod sends ("").</summary>
        public string? DeveloperInstructions;
        public string? Label;
        /// <summary>Extra environment variables for the spawned codex (e.g. RUST_LOG).</summary>
        public Dictionary<string, string> Env = new Dictionary<string, string>();
        /// <summary>Keep the per-call scratch folder (its .codex-state holds Codex's own logs sqlite).</summary>
        public bool KeepScratch;
    }

    /// <summary>What one call measured.</summary>
    public sealed class CallRecord
    {
        public int Round;
        public string Model = "";
        public bool AllowToolUse;
        public int SystemChars;
        public int PromptChars;
        public int SchemaChars;
        public string System = "";
        public string Prompt = "";
        public string RawResult = "";
        public string Reply = "";
        public List<string> ToolCalls = new List<string>();
        public JObject? Usage;
        public int TokensIn, TokensCached, TokensOut, TokensReasoning;
        public Dictionary<string, double> StageMs = new Dictionary<string, double>();
        public double TotalMs;
        public string? ThreadPath;
        public string? Error;
        public List<string> NotificationMethods = new List<string>();
        public string EventsFile = "";
    }

    /// <summary>
    /// The mod's Codex client, copied and instrumented: stage timings, every JSONL line to a file,
    /// per-call usage, and switches for the variants. Wire behaviour is otherwise identical.
    /// </summary>
    public sealed class CodexProbeClient : IToolChatClient
    {
        private const int TimeoutSeconds = 300;
        private readonly CodexOptions _opt;
        private readonly int _maxTokens;
        private readonly string _logDir;
        public readonly List<CallRecord> Calls = new List<CallRecord>();
        private static int _seq;

        public CodexProbeClient(CodexOptions options, int maxTokens, string logDir)
        {
            _opt = options;
            _maxTokens = maxTokens;
            _logDir = logDir;
            Directory.CreateDirectory(_logDir);
        }

        public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default)
        {
            var rec = await RunAsync(messages, null, false, ct).ConfigureAwait(false);
            var result = CodexAppServerShape.ParseToolResult(rec.RawResult);
            return result.Text;
        }

        public async Task<ChatResult> CompleteWithToolsAsync(IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools, bool allowToolUse = true, CancellationToken ct = default)
        {
            var rec = await RunAsync(messages, tools, allowToolUse, ct).ConfigureAwait(false);
            var parsed = CodexAppServerShape.ParseToolResult(rec.RawResult, tools);
            rec.Reply = parsed.Text;
            rec.ToolCalls = parsed.ToolCalls.Select(c => c.Name + c.ArgumentsJson)
                .Concat(parsed.AnswerCalls.Select(c => "[answer]" + c.Name + c.ArgumentsJson)).ToList();
            return parsed;
        }

        private Task<CallRecord> RunAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition>? tools,
            bool allowToolUse, CancellationToken ct)
        {
            var exe = FindCodex() ?? throw new InvalidOperationException("codex.exe not found");
            var system = CodexAppServerShape.BuildSystem(messages, tools, allowToolUse, _maxTokens);
            var prompt = CodexAppServerShape.BuildTranscript(messages);
            var schema = CodexAppServerShape.BuildStrictSchema(tools, allowToolUse);
            var scratch = Path.Combine(Path.GetTempPath(), "immersive-ai-codex-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(scratch);
            var rec = new CallRecord
            {
                Round = Calls.Count + 1,
                Model = _opt.Model,
                AllowToolUse = allowToolUse,
                System = system,
                Prompt = prompt,
                SystemChars = system.Length,
                PromptChars = prompt.Length,
                SchemaChars = schema.Length,
            };
            Calls.Add(rec);
            return Task.Run(() =>
            {
                try { RunProcess(exe, scratch, system, prompt, schema, rec, ct); }
                catch (Exception ex) { rec.Error = ex.Message; throw; }
                finally
                {
                    if (_opt.KeepScratch) rec.ThreadPath = (rec.ThreadPath ?? "") + " scratch=" + scratch;
                    else try { Directory.Delete(scratch, true); } catch { }
                }
                return rec;
            }, ct);
        }

        private void RunProcess(string exe, string scratch, string system, string prompt, string schema,
            CallRecord rec, CancellationToken ct)
        {
            var codexHome = _opt.CodexHome ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            var sqliteHome = Path.Combine(scratch, ".codex-state");
            Directory.CreateDirectory(sqliteHome);
            var seq = Interlocked.Increment(ref _seq);
            rec.EventsFile = Path.Combine(_logDir, $"events_{DateTime.Now:HHmmss}_{seq:D3}.jsonl");
            var sw = Stopwatch.StartNew();
            void Stage(string name) => rec.StageMs[name] = Math.Round(sw.Elapsed.TotalMilliseconds);

            using (var events = new StreamWriter(rec.EventsFile, false, new UTF8Encoding(false)))
            using (var session = new AppServerSession(exe, scratch, codexHome, sqliteHome, TimeSpan.FromSeconds(TimeoutSeconds), ct, events, _opt.Env))
            {
                Stage("spawned");
                session.Initialize();
                Stage("initialized");
                var account = session.Request("account/read", new JObject { ["refreshToken"] = false });
                if (!string.Equals((string?)account.SelectToken("account.type"), "chatgpt", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("not a ChatGPT login");
                Stage("account_read");
                var config = LockedModelOnlyConfig(session);
                foreach (var p in _opt.ExtraConfig.Properties()) config[p.Name] = p.Value.DeepClone();
                Stage("config_read");
                var start = session.Request("thread/start", new JObject
                {
                    ["model"] = _opt.Model,
                    ["modelProvider"] = "openai",
                    ["baseInstructions"] = system,
                    ["developerInstructions"] = _opt.DeveloperInstructions ?? string.Empty,
                    ["ephemeral"] = _opt.Ephemeral,
                    ["cwd"] = scratch,
                    ["environments"] = new JArray(),
                    ["selectedCapabilityRoots"] = new JArray(),
                    ["dynamicTools"] = new JArray(),
                    ["approvalPolicy"] = "never",
                    ["sandbox"] = "read-only",
                    ["config"] = config,
                    ["personality"] = "none",
                    ["serviceName"] = "immersive_ai",
                });
                Stage("thread_started");
                var threadId = (string?)start.SelectToken("thread.id") ?? throw new InvalidOperationException("no thread");
                rec.ThreadPath = (string?)start.SelectToken("thread.path");

                var turnId = session.SendRequest("turn/start", new JObject
                {
                    ["threadId"] = threadId,
                    ["input"] = new JArray(new JObject { ["type"] = "text", ["text"] = prompt }),
                    ["effort"] = _opt.Effort,
                    ["outputSchema"] = JObject.Parse(schema),
                });
                var env = new CodexAppServerShape.TurnEnvelope();
                bool firstDelta = false, firstEvent = false;
                while (!env.Finished)
                {
                    var m = session.Next();
                    var method = (string?)m["method"];
                    if (method != null) rec.NotificationMethods.Add(method);
                    if (!firstEvent && method != null && method.StartsWith("item/")) { firstEvent = true; Stage("first_item"); }
                    if (!firstDelta && method == "item/agentMessage/delta") { firstDelta = true; Stage("first_delta"); }
                    if (method == "thread/tokenUsage/updated") rec.Usage = m["params"]?["tokenUsage"] as JObject;
                    if ((int?)m["id"] == turnId && m["error"] is JObject err)
                        throw new InvalidOperationException((string?)err["message"] ?? "turn refused");
                    CodexAppServerShape.FoldTurnEvent(env, m, threadId);
                }
                Stage("turn_completed");
                rec.RawResult = env.ResultText;
                rec.TokensIn = env.TokensIn;
                rec.TokensOut = env.TokensOut;
                rec.TokensReasoning = env.ThinkingTokens;
                rec.TokensCached = (int?)rec.Usage?.SelectToken("total.cachedInputTokens") ?? 0;
                var parsed = CodexAppServerShape.ParseToolResult(env.ResultText);
                rec.Reply = parsed.Text;
                rec.ToolCalls = parsed.ToolCalls.Select(c => c.Name + c.ArgumentsJson).ToList();
                if (!env.Ok) { rec.Error = env.ErrorText; throw new InvalidOperationException("Codex: " + env.ErrorText); }
            }
            rec.TotalMs = Math.Round(sw.Elapsed.TotalMilliseconds);
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
            var read = session.Request("config/read", new JObject { ["includeLayers"] = false });
            if (read.SelectToken("config.mcp_servers") is JObject servers)
                foreach (var server in servers.Properties())
                    disabled["mcp_servers." + server.Name + ".enabled"] = false;
            return disabled;
        }

        public static string? FindCodex()
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
            if (!Directory.Exists(root)) return null;
            return Directory.GetDirectories(root).Select(d => Path.Combine(d, "codex.exe")).Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }

        private sealed class AppServerSession : IDisposable
        {
            private readonly Process _process;
            private readonly StreamReader _reader;
            private readonly StreamWriter _writer;
            private readonly Task<string> _stderr;
            private readonly DateTime _deadlineUtc;
            private readonly CancellationToken _ct;
            private readonly StreamWriter _log;
            private int _counter;

            public AppServerSession(string exe, string wd, string codexHome, string sqliteHome, TimeSpan timeout,
                CancellationToken ct, StreamWriter log, Dictionary<string, string> env)
            {
                _deadlineUtc = DateTime.UtcNow + timeout;
                _ct = ct;
                _log = log;
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "app-server --stdio",
                    WorkingDirectory = wd,
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
                foreach (var kv in env) psi.EnvironmentVariables[kv.Key] = kv.Value;
                _process = Process.Start(psi)!;
                _reader = _process.StandardOutput;
                _writer = new StreamWriter(_process.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true };
                _stderr = _process.StandardError.ReadToEndAsync();
            }

            public void Initialize()
            {
                Request("initialize", new JObject
                {
                    ["clientInfo"] = new JObject { ["name"] = "immersive_ai", ["title"] = "Immersive AI", ["version"] = "1.0.0" },
                    ["capabilities"] = new JObject { ["experimentalApi"] = true },
                });
                Send(new JObject { ["method"] = "initialized", ["params"] = new JObject() });
            }

            public JObject Request(string method, JObject parameters)
            {
                var id = SendRequest(method, parameters);
                while (true)
                {
                    var m = Next();
                    if ((int?)m["id"] != id || m["method"] != null) continue;
                    if (m["error"] is JObject e) throw new InvalidOperationException("Codex refused " + method + ": " + (string?)e["message"]);
                    return m["result"] as JObject ?? new JObject();
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
                    _ct.ThrowIfCancellationRequested();
                    var remaining = _deadlineUtc - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero) throw new TimeoutException("Codex timeout");
                    var read = _reader.ReadLineAsync();
                    if (Task.WhenAny(read, Task.Delay(remaining, _ct)).GetAwaiter().GetResult() != read)
                        throw new TimeoutException("Codex timeout");
                    var line = read.GetAwaiter().GetResult();
                    if (line == null)
                        throw new InvalidOperationException("Codex closed: " + (_stderr.IsCompleted ? _stderr.Result : ""));
                    _log.WriteLine("<< " + Redact(line));
                    JObject m;
                    try { m = JObject.Parse(line); } catch (JsonException) { continue; }
                    if (m["method"] != null && m["id"] != null)
                    {
                        Send(new JObject
                        {
                            ["id"] = m["id"]!.DeepClone(),
                            ["error"] = new JObject { ["code"] = -32601, ["message"] = "Immersive AI grants no Codex tools or client permissions." },
                        });
                        continue;
                    }
                    return m;
                }
            }

            /// <summary>No account identity or credential ever reaches the probe's files.</summary>
            private static string Redact(string line) =>
                System.Text.RegularExpressions.Regex.Replace(line,
                    "\"(email|accessToken|refreshToken|idToken|access_token|refresh_token|id_token|apiKey|api_key|token)\"\\s*:\\s*\"[^\"]*\"",
                    "\"$1\":\"<redacted>\"");

            private void Send(JObject message)
            {
                var text = message.ToString(Formatting.None);
                _log.WriteLine(">> " + (text.Length > 4000 ? text.Substring(0, 4000) + "…[" + text.Length + " chars]" : text));
                _writer.WriteLine(text);
            }

            public void Dispose()
            {
                try { _writer.Close(); } catch { }
                try { if (!_process.HasExited && !_process.WaitForExit(500)) Process.Start(new ProcessStartInfo("taskkill", "/PID " + _process.Id + " /T /F") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit(5000); } catch { }
                try { if (!_process.HasExited) _process.WaitForExit(2000); } catch { }
                try
                {
                    var err = _stderr.IsCompleted ? _stderr.Result : "";
                    if (!string.IsNullOrWhiteSpace(err)) _log.WriteLine("!! STDERR\n" + Redact(err));
                }
                catch { }
                _process.Dispose();
            }
        }
    }
}
