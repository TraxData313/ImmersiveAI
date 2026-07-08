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
    /// <summary>OpenAI chat-completions client (raw HTTP, .NET Framework compatible).</summary>
    public sealed class OpenAIChatClient : IChatClient
    {
        private const string Endpoint = "https://api.openai.com/v1/chat/completions";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };

        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;

        static OpenAIChatClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public OpenAIChatClient(string apiKey, string model, int maxTokens)
        {
            _apiKey = apiKey ?? "";
            _model = string.IsNullOrWhiteSpace(model) ? "gpt-4o" : model;
            _maxTokens = maxTokens > 0 ? maxTokens : 400;
        }

        public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("OpenAI API key is not set. Add it to " + ModConfig.ConfigFilePath);

            var payload = new
            {
                model = _model,
                max_tokens = _maxTokens,
                messages = messages.Select(m => new
                {
                    role = m.Role == ChatRole.System ? "system" : m.Role == ChatRole.User ? "user" : "assistant",
                    content = m.Content,
                }).ToList(),
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Add("Authorization", "Bearer " + _apiKey);
                request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                using (var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"OpenAI request failed ({(int)response.StatusCode}): {Truncate(body, 400)}");

                    var json = JObject.Parse(body);
                    var text = (string?)json.SelectToken("choices[0].message.content");
                    if (string.IsNullOrWhiteSpace(text))
                        throw new InvalidOperationException("OpenAI returned an empty response.");
                    return text!.Trim();
                }
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}
