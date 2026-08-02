using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Llm
{
    /// <summary>Which house dialect of the shared chat-completions shape a service speaks. They all
    /// take the same messages and tools; they differ ONLY in how one tells them not to think — and
    /// getting that wrong is the "..." bug, where an NPC spends every token in silence and speaks
    /// nothing (2026.07.13). Each is a hard 400 on the others' field, so it can never be guessed at
    /// runtime; it is decided by which backend the player chose.</summary>
    public enum OpenAiDialect
    {
        /// <summary>OpenAI and the routers: reasoning_effort "none", or the routers' reasoning:{enabled}.</summary>
        Standard,
        /// <summary>Google: reasoning_effort, but "none" only on the 2.5 models — the 3.x line cannot
        /// be silenced at all, only turned down to "minimal".</summary>
        Gemini,
        /// <summary>DeepSeek: thinking:{"type":"disabled"}, and it thinks by DEFAULT when unsaid.</summary>
        DeepSeek,
    }

    /// <summary>OpenAI chat-completions client (raw HTTP, .NET Framework compatible). Supports
    /// native function calling, which carries the NPCs' "recall the world" ability. The endpoint
    /// is configurable (<see cref="ModConfig.OpenAIBaseUrl"/>), so the same client speaks to any
    /// OpenAI-compatible service — OpenRouter, Gemini, DeepSeek, NanoGPT, a local server.</summary>
    public sealed class OpenAIChatClient : IToolChatClient
    {
        // No timeout on the shared client itself — each request carries its own (see PostOnceAsync):
        // 90s for cloud services, minutes for a local server, where a big model's first request
        // (loading, prompt-crunching on home hardware) honestly takes that long (2026.07.17,
        // Anton's Qwen-35B test dying at the old flat 90s).
        private static readonly HttpClient Http = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

        private static readonly TimeSpan CloudRequestTimeout = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan LocalRequestTimeout = TimeSpan.FromMinutes(5);

        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;
        private readonly string _endpoint;
        private readonly string _label;
        private readonly bool _isLocal;
        private readonly OpenAiDialect _dialect;

        static OpenAIChatClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public OpenAIChatClient(string apiKey, string model, int maxTokens, string? endpoint = null, string? providerLabel = null, bool isLocal = false,
            OpenAiDialect dialect = OpenAiDialect.Standard)
        {
            _apiKey = apiKey ?? "";
            _model = string.IsNullOrWhiteSpace(model) ? "gpt-5.4-mini" : model;
            _dialect = dialect;
            // Gemini 3.x cannot be told to stop thinking, and its budget counts thought and speech
            // against one ceiling — so a spoken budget alone would be eaten in silence. Raising a
            // CEILING costs nothing on a reply that simply speaks.
            _maxTokens = maxTokens > 0 ? maxTokens : 400;
            if (_dialect == OpenAiDialect.Gemini && _maxTokens < ModConfig.GeminiThinkingFloor)
                _maxTokens = ModConfig.GeminiThinkingFloor;
            _endpoint = string.IsNullOrWhiteSpace(endpoint) ? ModConfig.DefaultOpenAIEndpoint : endpoint;
            // Errors and log lines name the true provider ("OpenRouter request failed…"), so a
            // router problem never sends the player checking their OpenAI account.
            _label = string.IsNullOrWhiteSpace(providerLabel) ? "OpenAI" : providerLabel;
            // A local server (LM Studio, Ollama) legitimately runs keyless, and speaks no router
            // dialect — the two graces below key off this.
            _isLocal = isLocal;
        }

        private bool IsOpenRouter =>
            _endpoint.IndexOf("openrouter.ai", StringComparison.OrdinalIgnoreCase) >= 0;

        // A provider-prefixed id ("openai/gpt-5.4-mini") is the OpenRouter/NanoGPT convention: the
        // request rides through a router that translates parameters per provider. Those get the
        // universal shape (classic max_tokens), never the OpenAI-only fields below.
        private bool IsRoutedModel => _model.IndexOf('/') >= 0;

        // Only Google's 2.5 line can be told to stop thinking outright; 2.5 Pro and the whole 3.x
        // line refuse "none". Matched on the id so a router-prefixed spelling counts too.
        private bool IsGemini25 =>
            _model.IndexOf("gemini-2.5", StringComparison.OrdinalIgnoreCase) >= 0
            && _model.IndexOf("2.5-pro", StringComparison.OrdinalIgnoreCase) < 0;

        // The gpt-5.x family and the o-series reject the classic max_tokens in favor of
        // max_completion_tokens, and carry the reasoning_effort dial; older models keep the
        // classic shape. Getting this wrong is a hard 400, so it keys off the model id.
        private bool IsReasoningFamily =>
            _model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase)
            || _model.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
            || _model.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
            || _model.StartsWith("o4", StringComparison.OrdinalIgnoreCase);

        public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            var result = await SendAsync(messages, null, allowToolUse: false, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.Text))
                throw new InvalidOperationException("OpenAI returned an empty response.");
            return result.Text;
        }

        public Task<ChatResult> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            bool allowToolUse = true,
            CancellationToken cancellationToken = default)
        {
            return SendAsync(messages, tools, allowToolUse, cancellationToken);
        }

        private async Task<ChatResult> SendAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition>? tools,
            bool allowToolUse,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) && !_isLocal)
                throw new InvalidOperationException(_label + " API key is not set. Add it to " + ModConfig.ConfigFilePath);
            if (!UsageLedger.CanCall(out var capReason))
                throw new InvalidOperationException(capReason);

            // Reasoning is OFF for good (2026.07.13, Anton's call): silent thinking spends billed
            // tokens against the reply's budget and slows every answer — an NPC that "thinks" too
            // long answers with silence. "none" is sent explicitly because omitting the dial lets
            // the API default to reasoning. (It also sidesteps gpt-5.6's refusal to combine
            // function tools with reasoning on chat completions, hit live 2026.07.12.)
            var reasoningFamily = _dialect == OpenAiDialect.Standard && !IsRoutedModel && IsReasoningFamily;
            var payload = new JObject
            {
                ["model"] = _model,
                [reasoningFamily ? "max_completion_tokens" : "max_tokens"] = _maxTokens,
                ["messages"] = BuildTurns(messages),
            };
            if (_dialect == OpenAiDialect.Gemini)
                // Google's OpenAI door takes reasoning_effort, but "none" is honored only by the 2.5
                // models; asking it of a 3.x is refused, and leaving the field off entirely lets them
                // default to HIGH — the costliest, slowest, most silent answer of all. "minimal" is
                // as close to off as Google allows.
                payload["reasoning_effort"] = IsGemini25 ? "none" : "minimal";
            else if (_dialect == OpenAiDialect.DeepSeek)
                // DeepSeek thinks unless explicitly told not to — the same trap Anthropic sets, and
                // the same shape of answer.
                payload["thinking"] = new JObject { ["type"] = "disabled" };
            else if (reasoningFamily)
                payload["reasoning_effort"] = "none";
            else if (IsRoutedModel && !_isLocal)
                // The routers' own unified reasoning switch (documented as ignored by models that
                // cannot reason) — without it, a routed gpt-5.x thinks at its default effort and
                // spends the spoken budget on silence, the very "..." bug of 2026.07.13. Local
                // servers (whose LM Studio ids are slashed too) know no such field and the strict
                // ones 400 on it — there, thinking is governed by which model the user loads.
                payload["reasoning"] = new JObject { ["enabled"] = false };

            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = BuildTools(tools);
                // Definitions always ride along (a history holding tool calls needs them to validate);
                // "none" is how a final, spoken-answer-only round is enforced.
                if (!allowToolUse) payload["tool_choice"] = "none";
            }

            var payloadText = payload.ToString(Formatting.None);

            var (status, body) = await PostOnceAsync(payloadText, cancellationToken).ConfigureAwait(false);

            // Some routed models cannot have their thinking turned off — fable, grok-4.5 and
            // gemini-3.5 all answer 400 "Reasoning is mandatory for this endpoint" (verified live
            // 2026.07.16). Drop the reasoning field and let such a model think as it must: better
            // a thoughtful reply than a refused one, and no per-model list to keep.
            if (status == 400 && payload["reasoning"] != null
                && body.IndexOf("Reasoning is mandatory", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                payload.Remove("reasoning");
                payloadText = payload.ToString(Formatting.None);
                ModLog.Warn($"{_label}: '{_model}' cannot run with reasoning disabled — retrying with its own thinking allowed.");
                (status, body) = await PostOnceAsync(payloadText, cancellationToken).ConfigureAwait(false);
            }

            // The same grace for the two house dialects. Providers rename and retire these switches
            // (Google has already moved once, from a thinking budget to a level), and a model that
            // must think is still worth hearing — so a 400 that names our quieting field drops it and
            // asks again, rather than leaving the NPC mute over a parameter.
            if (status == 400 && MentionsThinkingField(body))
            {
                var dropped = _dialect == OpenAiDialect.Gemini ? "reasoning_effort"
                    : _dialect == OpenAiDialect.DeepSeek ? "thinking" : null;
                if (dropped != null && payload[dropped] != null)
                {
                    payload.Remove(dropped);
                    payloadText = payload.ToString(Formatting.None);
                    ModLog.Warn($"{_label}: '{_model}' refused '{dropped}' — retrying and letting it think as it must.");
                    (status, body) = await PostOnceAsync(payloadText, cancellationToken).ConfigureAwait(false);
                }
            }

            // OpenAI's "insufficient permissions" 401 shows up INTERMITTENTLY for a while after
            // the account's model access is changed (their ACL propagating — observed live
            // 2026.07.12: the same request shape succeeding and failing seconds apart, and
            // one 1.5s retry still losing whole replies HOURS after the grant). Three retries
            // with growing pauses ride out a stale server; a truly bad key fails them all and
            // is reported as before.
            for (int attempt = 1; attempt <= 3 && status == 401
                 && body.IndexOf("insufficient permissions", StringComparison.OrdinalIgnoreCase) >= 0; attempt++)
            {
                ModLog.Warn($"{_label} answered 401 'insufficient permissions' — retry {attempt} of 3 (fresh access changes propagate slowly on their side).");
                await Task.Delay(1500 * attempt, cancellationToken).ConfigureAwait(false);
                (status, body) = await PostOnceAsync(payloadText, cancellationToken).ConfigureAwait(false);
            }

            if (status < 200 || status >= 300)
            {
                LlmGate.ReportFailure(status, _label, body);
                throw new InvalidOperationException($"{_label} request failed ({status}): {Truncate(body, 400)}");
            }

            var json = JObject.Parse(body);

            // The API measures its own tokens — hand them to the ledger, and tell the
            // gate the road is open again.
            var completionTokens = (int?)json.SelectToken("usage.completion_tokens") ?? 0;
            UsageLedger.RecordCall(_model,
                (int?)json.SelectToken("usage.prompt_tokens") ?? 0,
                completionTokens);
            LlmGate.ReportSuccess();

            // The thinking tax, made visible: some backends cannot have thinking switched off
            // (Gemini 3.x), and any backend could quietly ignore the off switch — when the API
            // reports reasoning tokens, the log says so, and "why is it slow" is answered by
            // log.txt instead of guesswork. Silent when the count is zero, which is the norm.
            var thinkingTokens = (int?)json.SelectToken("usage.completion_tokens_details.reasoning_tokens") ?? 0;
            if (thinkingTokens > 0)
                ModLog.Info($"{_label} ({_model}): {thinkingTokens} of {completionTokens} output tokens were silent thinking.");

            var message = json.SelectToken("choices[0].message");

            var text = ((string?)message?["content"] ?? string.Empty).Trim();

            var calls = (message?["tool_calls"] as JArray ?? new JArray())
                .Where(c => (string?)c["type"] == "function")
                .Select(c => new ToolCall(
                    (string?)c["id"] ?? "",
                    PlainToolName((string?)c.SelectToken("function.name") ?? ""),
                    (string?)c.SelectToken("function.arguments") ?? "{}",
                    // Gemini's thinking models cryptographically SIGN each function call and refuse
                    // the next request if the signature does not come back with it ("Function call
                    // is missing a thought_signature", a hard 400 — hit live 2026.08.02 on the very
                    // first Gemini reply, since move_heart rides every one). Carried opaquely; no
                    // other backend sends the field, so nothing else is affected.
                    (string?)c.SelectToken("extra_content.google.thought_signature")))
                .ToList();

            // Local hybrid thinkers (Qwen3.x and kin) cannot be told reasoning-off through this
            // API — some leak their thinking into content as <think> blocks, others speak ONLY
            // into a separate reasoning channel and leave content empty (Anton's live find,
            // 2026.07.17: 2,912 billed tokens arriving as "..."). Strip the leaked blocks, and
            // when a reply thought without ever speaking, say so in the log in words the tester
            // can act on. Cloud backends are untouched.
            if (_isLocal)
            {
                var beforeStrip = text;
                text = StripThinkBlocks(text).Trim();
                if (text.Length == 0 && calls.Count == 0)
                {
                    var reasoning = ((string?)message?["reasoning_content"] ?? (string?)message?["reasoning"] ?? "").Trim();
                    if (reasoning.Length > 0 || beforeStrip.Length > 0)
                        ModLog.Warn("Local AI: the model spent its whole answer thinking and never spoke. " +
                            "Disable the model's thinking in your server (LM Studio has a per-model toggle) " +
                            "or load a non-thinking 'instruct' build.");
                }
            }
            else if (_dialect == OpenAiDialect.Gemini && text.Length == 0 && calls.Count == 0)
            {
                // Gemini 3.x cannot be silenced, only turned down — so an empty answer here means the
                // thinking ate the ceiling before a single word. The floor in the constructor should
                // prevent it; say the remedy plainly if it ever slips through anyway.
                ModLog.Warn("Gemini: the model spent its whole budget thinking and never spoke. Raise the reply length "
                    + "(MaxTokens in " + ModConfig.ConfigFilePath + "), or pick a gemini-2.5 model, whose thinking CAN be switched off.");
            }

            return new ChatResult(text, calls);
        }

        /// <summary>One POST to the chat endpoint: status + body, never throwing on an API error
        /// status (the caller decides about retries). A failed CONNECTION still throws, after
        /// telling the gate.</summary>
        private async Task<(int Status, string Body)> PostOnceAsync(string payloadText, CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
            {
                timeout.CancelAfter(_isLocal ? LocalRequestTimeout : CloudRequestTimeout);
                // A keyless local server gets no Authorization header at all (some reject "Bearer ").
                if (!string.IsNullOrWhiteSpace(_apiKey))
                    request.Headers.Add("Authorization", "Bearer " + _apiKey);
                if (IsOpenRouter)
                {
                    // OpenRouter's optional app attribution — shows the mod by name in the user's
                    // own activity view instead of an anonymous key.
                    request.Headers.Add("HTTP-Referer", "https://www.nexusmods.com/mountandblade2bannerlord/mods/12119");
                    request.Headers.Add("X-Title", "Immersive AI (Bannerlord)");
                }
                request.Content = new StringContent(payloadText, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await Http.SendAsync(request, timeout.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The connection itself failed (or timed out) — quiet the autonomous flows.
                    LlmGate.ReportFailure(0, _label, ex.Message);
                    throw;
                }
                using (response)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return ((int)response.StatusCode, body);
                }
            }
        }

        private static JArray BuildTurns(IReadOnlyList<ChatMessage> messages)
        {
            var turns = new JArray();
            foreach (var m in messages)
            {
                if (m.Role == ChatRole.Tool)
                {
                    turns.Add(new JObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = m.ToolCallId ?? "",
                        ["content"] = m.Content,
                    });
                    continue;
                }

                if (m.Role == ChatRole.Assistant && m.ToolCalls.Count > 0)
                {
                    var toolCalls = new JArray();
                    foreach (var call in m.ToolCalls)
                    {
                        var entry = new JObject
                        {
                            ["id"] = call.Id,
                            ["type"] = "function",
                            ["function"] = new JObject
                            {
                                ["name"] = call.Name,
                                ["arguments"] = call.ArgumentsJson,
                            },
                        };
                        // Whatever the backend signed the call with rides back exactly as it came.
                        // Keyed off the signature's presence, not the dialect: only Gemini issues
                        // one, so no other service ever sees a field it does not know.
                        if (call.ProviderSignature != null)
                            entry["extra_content"] = new JObject
                            {
                                ["google"] = new JObject { ["thought_signature"] = call.ProviderSignature },
                            };
                        toolCalls.Add(entry);
                    }

                    var turn = new JObject { ["role"] = "assistant", ["tool_calls"] = toolCalls };
                    // OpenAI wants content null (not "") when the turn is purely a tool reach.
                    turn["content"] = string.IsNullOrWhiteSpace(m.Content) ? JValue.CreateNull() : (JToken)m.Content;
                    turns.Add(turn);
                    continue;
                }

                turns.Add(new JObject
                {
                    ["role"] = m.Role == ChatRole.System ? "system" : m.Role == ChatRole.User ? "user" : "assistant",
                    ["content"] = m.Content,
                });
            }
            return turns;
        }

        private static JArray BuildTools(IReadOnlyList<ToolDefinition> tools)
        {
            var arr = new JArray();
            foreach (var tool in tools)
            {
                var properties = new JObject();
                var required = new JArray();
                foreach (var p in tool.Parameters)
                {
                    properties[p.Name] = new JObject { ["type"] = "string", ["description"] = p.Description };
                    if (p.Required) required.Add(p.Name);
                }

                arr.Add(new JObject
                {
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = properties,
                            ["required"] = required,
                        },
                    },
                });
            }
            return arr;
        }

        /// <summary>Removes &lt;think&gt;…&lt;/think&gt; spans a local hybrid model leaked into its
        /// spoken content. An UNCLOSED &lt;think&gt; means the token budget died mid-thought —
        /// nothing after it was ever speech, so the tail goes too.</summary>
        private static string StripThinkBlocks(string text)
        {
            if (text.IndexOf("<think", StringComparison.OrdinalIgnoreCase) < 0) return text;
            var stripped = System.Text.RegularExpressions.Regex.Replace(
                text, "<think>[\\s\\S]*?</think>", string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var open = stripped.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            if (open >= 0) stripped = stripped.Substring(0, open);
            return stripped;
        }

        /// <summary>The tool name as WE named it. Gemini namespaces functions internally
        /// ("default_api:move_heart" — seen in its own 400 text, 2026.08.02); should that spelling
        /// ever reach the reply, every lookup would miss and each tool would answer its honest
        /// "nothing surfaces", which is silent and miserable to diagnose. Our tool names never carry
        /// a colon, so dropping a namespace prefix is a no-op when there is none — and it says so in
        /// the log the one time it fires, since that would be worth knowing.</summary>
        private static string PlainToolName(string name)
        {
            var colon = name.LastIndexOf(':');
            if (colon < 0 || colon == name.Length - 1) return name;
            var plain = name.Substring(colon + 1);
            if (_namespaceNoted) return plain;
            _namespaceNoted = true;
            ModLog.Info($"Tool names arrive namespaced ('{name}') — using '{plain}'.");
            return plain;
        }

        private static bool _namespaceNoted;

        /// <summary>Whether a 400 body is complaining about the very field we use to quiet the model
        /// (unknown, unsupported, or given a value it will not take) rather than about our prompt.</summary>
        private static bool MentionsThinkingField(string body) =>
            body.IndexOf("reasoning_effort", StringComparison.OrdinalIgnoreCase) >= 0
            || body.IndexOf("thinking", StringComparison.OrdinalIgnoreCase) >= 0
            || body.IndexOf("thinking_level", StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}
