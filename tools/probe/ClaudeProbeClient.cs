using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Probe
{
    /// <summary>The Claude Code road, as src/ImmersiveAI.Module/Llm/ClaudeCodeChatClient.cs runs it:
    /// headless claude -p, the sheet in a file, the schema on the line, thinking off by env — the
    /// same Core shapes (ClaudeCliShape) both sides use. Instrumented with per-call records.</summary>
    public sealed class ClaudeProbeClient : IToolChatClient
    {
        private readonly string _model;
        private readonly int _maxTokens;
        public readonly List<CallRecord> Calls = new List<CallRecord>();

        public ClaudeProbeClient(string model, int maxTokens)
        {
            _model = model;
            _maxTokens = maxTokens;
        }

        public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default) =>
            (await RunAsync(messages, null, false, ct)).ResultText;

        public async Task<ChatResult> CompleteWithToolsAsync(IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools, bool allowToolUse = true, CancellationToken ct = default)
        {
            var env = await RunAsync(messages, tools, allowToolUse, ct);
            return ClaudeCliShape.ParseToolResult(env.ResultText ?? "", tools);
        }

        private Task<ClaudeCliShape.Envelope> RunAsync(IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition>? tools, bool allowToolUse, CancellationToken ct)
        {
            var system = ClaudeCliShape.BuildSystem(messages, tools, allowToolUse, _maxTokens);
            var prompt = ClaudeCliShape.BuildTranscript(messages);
            var schema = tools != null && tools.Count > 0 ? ClaudeCliShape.BuildSchema(tools, allowToolUse) : null;
            var rec = new CallRecord
            {
                Round = Calls.Count + 1, Model = "claude:" + _model, AllowToolUse = allowToolUse,
                System = system, Prompt = prompt, SystemChars = system.Length, PromptChars = prompt.Length,
                SchemaChars = schema?.Length ?? 0,
            };
            Calls.Add(rec);
            return Task.Run(() =>
            {
                var dir = Path.Combine(Path.GetTempPath(), "immersive-ai-cli-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(dir);
                try
                {
                    var sysFile = Path.Combine(dir, "system.txt");
                    File.WriteAllText(sysFile, system, new UTF8Encoding(false));
                    var args = new List<string>
                    {
                        "-p", "--model", _model, "--output-format", "stream-json", "--verbose",
                        "--system-prompt-file", sysFile, "--tools", "", "--no-session-persistence",
                        "--strict-mcp-config", "--disable-slash-commands", "--safe-mode",
                    };
                    if (schema != null) { args.Add("--json-schema"); args.Add(schema); }
                    var psi = new ProcessStartInfo
                    {
                        FileName = FindClaude(),
                        Arguments = string.Join(" ", args.Select(ClaudeCliShape.EscapeWindowsArg)),
                        UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
                        RedirectStandardError = true, CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                        WorkingDirectory = dir,
                    };
                    psi.EnvironmentVariables["MAX_THINKING_TOKENS"] = "0";
                    var sw = Stopwatch.StartNew();
                    using var proc = Process.Start(psi)!;
                    var stdout = proc.StandardOutput.ReadToEndAsync();
                    var stderr = proc.StandardError.ReadToEndAsync();
                    var bytes = Encoding.UTF8.GetBytes(prompt);
                    proc.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                    proc.StandardInput.Close();
                    if (!proc.WaitForExit(300_000)) { try { proc.Kill(); } catch { } throw new TimeoutException("claude timed out"); }
                    rec.TotalMs = Math.Round(sw.Elapsed.TotalMilliseconds);
                    var env = ClaudeCliShape.ParseStream(stdout.Result);
                    rec.RawResult = env.ResultText;
                    rec.TokensIn = env.TokensIn;
                    rec.TokensOut = env.TokensOut;
                    if (!env.Ok || proc.ExitCode != 0)
                    {
                        rec.Error = (env.ErrorText.Length > 0 ? env.ErrorText : stderr.Result);
                        throw new InvalidOperationException("claude: " + rec.Error);
                    }
                    var parsed = ClaudeCliShape.ParseToolResult(env.ResultText, tools);
                    rec.Reply = parsed.Text;
                    rec.ToolCalls = parsed.ToolCalls.Select(c => c.Name + c.ArgumentsJson)
                        .Concat(parsed.AnswerCalls.Select(c => "[answer]" + c.Name + c.ArgumentsJson)).ToList();
                    return env;
                }
                finally { try { Directory.Delete(dir, true); } catch { } }
            }, ct);
        }

        private static string FindClaude()
        {
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(d)) continue;
                var p = Path.Combine(d.Trim(), "claude.exe");
                if (File.Exists(p)) return p;
            }
            throw new InvalidOperationException("claude.exe not found on PATH");
        }
    }
}
