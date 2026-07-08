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
    /// <summary>
    /// Anthropic Messages API client (raw HTTP â€” the official SDK requires modern .NET,
    /// and Bannerlord runs mods on .NET Framework 4.7.2).
    /// </summary>
    public sealed class AnthropicChatClient : IChatClient
    {
        private const string Endpoint = "https://api.anthropic.com/v1/messages";

        private static readonly HttpClient Http = CreateHttpClient();

        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;

        static AnthropicChatClient()
        {
            // .NET Framework needs an explicit opt-in to TLS 1.2 on some systems
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            return client;
        }

        public AnthropicChatClient(string apiKey, string model, int maxTokens)
        {
            _apiKey = apiKey ?? "";
            _model = string.IsNullOrWhiteSpace(model) ? "claude-opus-4-8" : model;
            _maxTokens = maxTokens > 0 ? maxTokens : 400;
        }

        public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("Anthropic API key is not set. Add it to " + ModConfig.ConfigFilePath);

            var system = string.Join("\n\n", messages.Where(m => m.Role == ChatRole.System).Select(m => m.Content));
            var turns = messages
                .Where(m => m.Role != ChatRole.System)
                .Select(m => new { role = m.Role == ChatRole.User ? "user" : "assistant", content = m.Content })
                .ToList();

            var payload = new Dictionary<string, object>
            {
                ["model"] = _model,
                ["max_tokens"] = _maxTokens,
                ["messages"] = turns,
            };
            if (system.Length > 0) payload["system"] = system;

            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                using (var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"Anthropic request failed ({(int)response.StatusCode}): {Truncate(body, 400)}");

                    var json = JObject.Parse(body);
                    var stopReason = (string?)json["stop_reason"];
                    if (stopReason == "refusal")
                        throw new InvalidOperationException("The model declined to answer this request.");

                    var text = string.Concat(
                        (json["content"] as JArray ?? new JArray())
                            .Where(b => (string?)b["type"] == "text")
                            .Select(b => (string?)b["text"] ?? ""));

                    if (string.IsNullOrWhiteSpace(text))
                        throw new InvalidOperationException("Anthropic returned an empty response.");
                    return text.Trim();
                }
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}
