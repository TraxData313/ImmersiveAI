using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Llm
{
    /// <summary>
    /// The subscription road (2026.08.28, Anton's ask — his living-abby room proved it): the NPCs
    /// speak through the player's own installed Claude Code, so a claude.ai Pro/Max plan carries
    /// them with NO API key at all. Each call is one short-lived headless process — prompt down
    /// stdin, answer read from the stream — hardened the way living-abby learned to hold it: the
    /// system sheet goes in a FILE (Windows takes 32,767 characters for the whole command line,
    /// counted escaped, and the day a sheet outgrew it the only word said was WinError 206), the
    /// process is killed rather than waited on forever, and the CLI's own measured tokens and cost
    /// figure feed the ledger, so the ✒ notice tells the truth. Tool calling rides the CLI's
    /// structured output (see Core's ClaudeCliShape) — probed live before any of this was written:
    /// recall, and move_heart beside the spoken words, all first try.
    /// </summary>
    public sealed class ClaudeCodeChatClient : IToolChatClient
    {
        // Long enough for a slow opus turn with reaches, short enough that a hung process is not
        // mistaken for a patient one. (living-abby gives her 600s; NPC replies are far shorter.)
        private const int TimeoutSeconds = 300;

        private readonly string _model;
        private readonly string _configuredPath;

        public ClaudeCodeChatClient(string model, string configuredPath)
        {
            _model = string.IsNullOrWhiteSpace(model) ? "claude-haiku-4-5" : model.Trim();
            _configuredPath = (configuredPath ?? "").Trim();
        }

        public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            var env = await RunAsync(messages, tools: null, allowToolUse: false, cancellationToken).ConfigureAwait(false);
            var text = env.ResultText?.Trim() ?? "";
            if (text.Length == 0)
                throw new InvalidOperationException("Claude Code returned an empty response.");
            return text;
        }

        public async Task<ChatResult> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            bool allowToolUse = true,
            CancellationToken cancellationToken = default)
        {
            var env = await RunAsync(messages, tools, allowToolUse, cancellationToken).ConfigureAwait(false);
            return ClaudeCliShape.ParseToolResult(env.ResultText ?? "");
        }

        private async Task<ClaudeCliShape.Envelope> RunAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition>? tools,
            bool allowToolUse,
            CancellationToken cancellationToken)
        {
            if (!UsageLedger.CanCall(out var capReason))
                throw new InvalidOperationException(capReason);

            var exe = FindClaude(_configuredPath);
            if (exe == null)
                throw new InvalidOperationException(
                    "Claude Code was not found on this machine. The ClaudeCode backend speaks through the "
                    + "installed Claude Code app (claude.ai Pro/Max plan) — install it from claude.com/code, sign in "
                    + "once by running it, and the NPCs can speak. Or set ClaudeCodePath in " + ModConfig.ConfigFilePath);

            var system = ClaudeCliShape.BuildSystem(messages, tools, allowToolUse);
            var prompt = ClaudeCliShape.BuildTranscript(messages);
            var withSchema = tools != null && tools.Count > 0;

            // The sheet in a file, never on the line (the WinError 206 lesson). One folder per
            // call, removed whole afterwards.
            var sysDir = Path.Combine(Path.GetTempPath(), "immersive-ai-cli-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(sysDir);
            var sysFile = Path.Combine(sysDir, "system.txt");
            File.WriteAllText(sysFile, system, new UTF8Encoding(false));

            var args = new List<string>
            {
                "-p",
                "--model", _model,
                "--output-format", "stream-json",
                "--verbose",
                "--system-prompt-file", sysFile,
                // The CLI's own workshop stays shut: no local tools, no sessions on disk, no MCP,
                // no skills, no player customizations — one clean model call, nothing else.
                "--tools", "",
                "--no-session-persistence",
                "--strict-mcp-config",
                "--disable-slash-commands",
                "--safe-mode",
            };
            if (withSchema)
            {
                args.Add("--json-schema");
                args.Add(ClaudeCliShape.BuildSchema(tools, allowToolUse));
            }

            try
            {
                var env = await Task.Run(() => RunProcess(exe, args, prompt, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);

                if (!env.Ok)
                {
                    var reason = string.IsNullOrWhiteSpace(env.ErrorText) ? "Claude Code reported an error." : env.ErrorText;
                    LlmGate.ReportFailure(LooksRateLimited(reason) ? 429 : 0, "Claude Code", reason);
                    throw new InvalidOperationException("Claude Code request failed: " + Truncate(reason, 400));
                }

                // The CLI measures its own run — tokens AND the money figure — so the ledger gets
                // the exact numbers instead of a price-table guess. On a subscription the dollars
                // are what the plan absorbed, which is exactly what the player wants to see.
                UsageLedger.RecordCall(string.IsNullOrEmpty(env.Model) ? _model : env.Model,
                    env.TokensIn, env.TokensOut, env.CostUsd);
                PlanGauge.NoteWindows(env.Limits);
                LlmGate.ReportSuccess();
                return env;
            }
            finally
            {
                try { Directory.Delete(sysDir, recursive: true); } catch { /* temp hygiene only */ }
            }
        }

        private static ClaudeCliShape.Envelope RunProcess(string exe, List<string> args, string prompt, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = string.Join(" ", args.Select(ClaudeCliShape.EscapeWindowsArg)),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                // A neutral home: never a repo, so nothing local can be picked up even in theory.
                WorkingDirectory = Path.GetDirectoryName(ModConfig.ConfigFilePath) ?? Path.GetTempPath(),
            };
            // Thinking OFF, the same law every other backend is held to (2026.07.13) — here it is
            // an env var, because the CLI has no flag for it: --effort's floor is "low", which on
            // opus still spent 222 thinking tokens and half again the wall-clock. MEASURED on opus
            // with the mod's own flags (2026.08.28): baseline 222 thinking / 6.2s, --effort low
            // 14 / 3.3s, this 0 / 3.4s. Silent thought buys an NPC nothing the player can hear and
            // spends the plan's windows to do it.
            psi.EnvironmentVariables["MAX_THINKING_TOKENS"] = "0";

            Process proc;
            try { proc = Process.Start(psi); }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Claude Code could not be started (" + ex.Message + "). Check ClaudeCodePath in "
                    + ModConfig.ConfigFilePath, ex);
            }

            using (proc)
            using (ct.Register(() => { try { proc.Kill(); } catch { } }))
            {
                // stdout/stderr drain on their own threads so a full pipe can never deadlock the
                // stdin write (living-abby found that hang the hard way).
                var stdout = proc.StandardOutput.ReadToEndAsync();
                var stderr = proc.StandardError.ReadToEndAsync();

                // .NET Framework has no StandardInputEncoding — write UTF-8 bytes to the raw pipe,
                // or every Cyrillic word arrives as the system code page's guess.
                var bytes = Encoding.UTF8.GetBytes(prompt ?? "");
                try
                {
                    proc.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                    proc.StandardInput.BaseStream.Flush();
                }
                catch (IOException) { /* the process died early; its output says why */ }
                try { proc.StandardInput.Close(); } catch { }

                if (!proc.WaitForExit(TimeoutSeconds * 1000))
                {
                    try { proc.Kill(); } catch { }
                    proc.WaitForExit(5000);
                    throw new InvalidOperationException(
                        "Claude Code was still working after " + TimeoutSeconds + " seconds, so it was stopped.");
                }
                ct.ThrowIfCancellationRequested();

                var outText = SafeResult(stdout);
                var errText = SafeResult(stderr);

                if (proc.ExitCode != 0)
                {
                    var env = ClaudeCliShape.ParseStream(outText);
                    var detail = !string.IsNullOrWhiteSpace(env.ErrorText) && env.ErrorText != "no result arrived from Claude Code"
                        ? env.ErrorText
                        : Tail(errText.Length > 0 ? errText : outText, 400);
                    if (detail.IndexOf("log in", StringComparison.OrdinalIgnoreCase) >= 0
                        || detail.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0
                        || detail.IndexOf("OAuth", StringComparison.OrdinalIgnoreCase) >= 0)
                        detail = "Claude Code is installed but not signed in — run it once and log in with your claude.ai account. (" + detail + ")";
                    throw new InvalidOperationException("Claude Code exited " + proc.ExitCode + ": " + detail);
                }

                return ClaudeCliShape.ParseStream(outText);
            }
        }

        private static string SafeResult(Task<string> read)
        {
            try { return read.GetAwaiter().GetResult() ?? ""; }
            catch { return ""; }
        }

        /// <summary>A standalone install first (PATH — the winget/native installs live there);
        /// then the copies bundled under the Claude apps' own folders, newest version winning.
        /// The player's explicit path overrides everything.</summary>
        internal static string? FindClaude(string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
                return File.Exists(configuredPath) ? configuredPath : null;

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    var p = Path.Combine(dir.Trim(), "claude.exe");
                    if (File.Exists(p)) return p;
                }
                catch { /* a malformed PATH entry is somebody else's problem */ }
            }

            string? best = null;
            var bestKey = new Version(0, 0);
            foreach (var root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            })
            {
                if (string.IsNullOrEmpty(root)) continue;
                var home = Path.Combine(root, "Claude", "claude-code");
                if (!Directory.Exists(home)) continue;
                foreach (var ver in Directory.GetDirectories(home))
                {
                    var exe = Path.Combine(ver, "claude.exe");
                    if (!File.Exists(exe)) continue;
                    var digits = System.Text.RegularExpressions.Regex.Matches(Path.GetFileName(ver), "\\d+");
                    var parts = digits.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).Take(4).ToArray();
                    Version key;
                    if (!Version.TryParse(string.Join(".", parts.Length >= 2 ? parts : new[] { "0", "0" }), out key))
                        key = new Version(0, 1);
                    if (best == null || key > bestKey) { best = exe; bestKey = key; }
                }
            }
            return best;
        }

        private static bool LooksRateLimited(string text) =>
            text.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("429", StringComparison.Ordinal) >= 0
            || text.IndexOf("usage limit", StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";
        private static string Tail(string s, int max) => s.Length <= max ? s : s.Substring(s.Length - max);
    }
}
