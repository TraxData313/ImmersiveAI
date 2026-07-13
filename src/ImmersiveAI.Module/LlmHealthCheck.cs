using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Llm;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    /// <summary>
    /// A quiet "are you there?" to the LLM when a game is entered, so a player with a missing key,
    /// a wrong key, or no connection learns it at once — from a plain message telling them exactly
    /// what to fix and to restart — instead of discovering a silent, mute mod only when they try to
    /// speak with someone. Runs once per process (the remedy for any failure is to fix the config
    /// and restart, which re-runs the check); costs a single tiny round-trip on success.
    /// </summary>
    public static class LlmHealthCheck
    {
        private static readonly Color OkColor = new Color(0.74f, 0.90f, 0.86f, 1f);   // soft sea-glass, like the activity notes
        private static readonly Color WarnColor = new Color(0.98f, 0.78f, 0.55f, 1f); // warm amber — a thing to see to

        private static int _ran; // 0 = not yet; guards against a second campaign load in one session

        /// <summary>Fire the check off in the background, at most once per process. Safe to call from
        /// any game-start hook; it marshals its own result to the game thread for display.</summary>
        public static void RunOnce(ModConfig config)
        {
            if (Interlocked.Exchange(ref _ran, 1) != 0) return;
            _ = CheckAsync(config);
        }

        private static async Task CheckAsync(ModConfig config)
        {
            try
            {
                var openAi = config != null && config.Backend == "OpenAI";
                var backend = openAi ? "OpenAI" : "Anthropic";
                var apiKey = openAi ? config?.OpenAIApiKey : config?.AnthropicApiKey;
                var model = openAi ? config?.OpenAIModel : config?.AnthropicModel;

                // The commonest case, and the one an API call can't diagnose kindly: no key at all.
                // A brand-new install also gets the one-time first-run guide popup — the "here is
                // where keys come from, here is where one goes" version of this message.
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Report($"Immersive AI: no {backend} API key is set — the NPCs cannot speak. Add your key to "
                        + ModConfig.ConfigFilePath + " and restart the game.", WarnColor);
                    if (config != null) FirstRunGuide.MaybeShow(config);
                    return;
                }

                var client = ChatClientFactory.Create(config, maxTokensOverride: 16);
                var messages = new[]
                {
                    ChatMessage.System("Reply with the single word: OK"),
                    ChatMessage.User("ping"),
                };

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    await client.CompleteAsync(messages, cts.Token).ConfigureAwait(false);
                }

                var quiet = string.IsNullOrWhiteSpace(model) ? backend : $"{backend} · {model}";
                ModLog.Info($"Health check OK — connected to {quiet}.");
                Report($"Immersive AI: connected to {quiet}. The world is listening.", OkColor);
            }
            catch (Exception ex)
            {
                ModLog.Error("health check", ex);
                Report("Immersive AI: " + Diagnose(ex, config), WarnColor);
            }
        }

        // Turn whatever went wrong into one sentence a player can act on. The clients raise
        // InvalidOperationException carrying the HTTP status ("... failed (401): ...") for API-side
        // refusals; the underlying HttpClient raises HttpRequestException / a cancellation on a dead
        // connection or a timeout. Everything ends with "restart the game", the task's asked-for remedy.
        private static string Diagnose(Exception ex, ModConfig config)
        {
            var configPath = ModConfig.ConfigFilePath;

            if (IsNetworkFailure(ex))
                return "could not reach the AI service — check your internet connection, then restart the game.";

            var msg = ex.Message ?? string.Empty;

            // A model that spent its whole token budget before it could speak (reasoning is sent
            // OFF everywhere since 2026.07.13, so seeing this means MaxTokens is set very low).
            if (Mentions(msg, "max_tokens or model output limit"))
                return "the model ran out of token budget before it could answer. Raise MaxTokens in " + configPath + ", then restart the game.";

            // OpenAI's "insufficient permissions" is a VALID key that may not use the asked-for
            // model (project model-access list, restricted key scopes, or an unverified org) —
            // pointing at the key would send the player hunting the wrong problem.
            if (Mentions(msg, "insufficient permissions") || Mentions(msg, "must be verified") || Mentions(msg, "does not have access to model"))
            {
                var model = config?.Backend == "OpenAI" ? config?.OpenAIModel : config?.AnthropicModel;
                return $"your API key is valid but not allowed to use the model '{model}'. In your provider's console, check the project's model access and the key's permissions (or verify the organization), or pick another model — then restart the game.";
            }

            if (Mentions(msg, "401") || Mentions(msg, "403") || msg.IndexOf("api key", StringComparison.OrdinalIgnoreCase) >= 0)
                return "the AI service rejected your API key. Check the key in " + configPath + " and restart the game.";

            if (Mentions(msg, "429"))
                return "the AI service is rate-limiting or your plan is out of credit (429). Check your account, then restart the game.";

            if (Mentions(msg, "404") || msg.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0)
                return "the AI service did not recognize the configured model. Check the model name in " + configPath + " and restart the game.";

            if (Mentions(msg, "500") || Mentions(msg, "502") || Mentions(msg, "503") || Mentions(msg, "529"))
                return "the AI service is having trouble right now. Wait a little and restart the game.";

            // Anything unclassified: surface the raw message so a report can name it, but still guide.
            return "the AI could not be reached — " + Truncate(msg, 200) + " Fix it in " + configPath + " and restart the game.";
        }

        private static bool IsNetworkFailure(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is HttpRequestException || e is WebException ||
                    e is OperationCanceledException || e is TimeoutException ||
                    e is System.Net.Sockets.SocketException)
                    return true;
            }
            return false;
        }

        private static bool Mentions(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max) + "…");

        private static void Report(string message, Color color)
        {
            MainThreadDispatcher.Enqueue(() =>
                InformationManager.DisplayMessage(new InformationMessage(message, color)));
        }
    }
}
