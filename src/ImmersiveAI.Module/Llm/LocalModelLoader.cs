using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Llm
{
    /// <summary>
    /// At game start, on the Local backend: asks LM Studio to hold the chosen model at the mod's own
    /// "Local context length" (see <see cref="LocalModelLoad"/>). Any other server (Ollama), or an LM
    /// Studio that does not answer its /api/v1 door, is left alone: this only ever helps.
    /// </summary>
    public static class LocalModelLoader
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        /// <summary>A line for the log saying what was done, or null when nothing was.</summary>
        public static async Task<string?> EnsureAsync(ModConfig config, CancellationToken ct)
        {
            var root = LocalModelLoad.ApiRoot(config?.LocalEndpoint);
            var key = config?.LocalModel;
            var wanted = config?.LocalContextWindow ?? 0;
            if (root == null || string.IsNullOrWhiteSpace(key) || wanted <= 0) return null;

            JObject? models;
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(5));
                    var resp = await Http.GetAsync(root + "/api/v1/models", cts.Token).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) return null; // not LM Studio
                    models = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
            }
            catch (Exception) { return null; } // no server, or not LM Studio's shape: the ping reports it

            var plan = LocalModelLoad.Decide(models, key, wanted);
            switch (plan.Step)
            {
                case LocalModelLoad.Step.Nothing:
                case LocalModelLoad.Step.AlreadyRight:
                    return null;
                case LocalModelLoad.Step.Reload:
                    foreach (var id in plan.Unload)
                        await PostAsync(root + "/api/v1/models/unload", new JObject { ["instance_id"] = id }, 60, ct).ConfigureAwait(false);
                    break;
            }
            await PostAsync(root + "/api/v1/models/load",
                new JObject { ["model"] = key!.Trim(), ["context_length"] = wanted }, 600, ct).ConfigureAwait(false);
            return plan.Step == LocalModelLoad.Step.Reload
                ? $"LM Studio held {key} at {plan.LoadedContext} tokens; reloaded it at {wanted}, the mod's Local context length."
                : $"LM Studio loaded {key} at {wanted} tokens, the mod's Local context length.";
        }

        private static async Task PostAsync(string url, JObject body, int seconds, CancellationToken ct)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(seconds));
                var resp = await Http.PostAsync(url,
                    new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json"),
                    cts.Token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException("LM Studio " + url.Substring(url.LastIndexOf('/') + 1) + " failed ("
                        + (int)resp.StatusCode + "): " + await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
        }
    }
}
