using System;
using System.IO;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    /// <summary>
    /// The one kind popup a brand-new player needs: entered a campaign, no API key set anywhere —
    /// here is where keys come from, here is exactly where one goes, and nothing will speak until
    /// then. Shown ONCE per install (a marker file beside config.json; delete it to see the note
    /// again). The health check keeps firing its shorter reminder each session regardless.
    /// </summary>
    public static class FirstRunGuide
    {
        private static int _ran;

        /// <summary>Call when a campaign is entered and the configured backend has no key.</summary>
        public static void MaybeShow(ModConfig config)
        {
            if (Interlocked.Exchange(ref _ran, 1) != 0) return;

            try
            {
                var marker = Path.Combine(ModConfig.ConfigDirectory, "first_run_note_shown.txt");
                if (File.Exists(marker)) return;

                Directory.CreateDirectory(ModConfig.ConfigDirectory);
                File.WriteAllText(marker,
                    "Immersive AI showed its first-run key guide on " + DateTime.Now + ".\r\n" +
                    "Delete this file to see that popup once more.\r\n");

                var keyField = config?.Backend == "OpenAI" ? "OpenAIApiKey"
                    : config?.Backend == "Anthropic" ? "AnthropicApiKey"
                    : config?.Backend == "Gemini" ? "GeminiApiKey"
                    : config?.Backend == "DeepSeek" ? "DeepSeekApiKey"
                    : "OpenRouterApiKey";
                var body =
                    "Immersive AI gives every character a real, remembering mind — but the minds speak " +
                    "through an AI service, with YOUR OWN key, and no key is set yet. Until one is, the world stays silent.\n\n" +
                    "1. GET A KEY — openrouter.ai is recommended: one key, every model, and the mod comes set " +
                    "to openai/gpt-5.6-luna (openai/gpt-5.4-mini is the proven fallback). platform.openai.com " +
                    "with those same two works as well; console.anthropic.com (Claude) works but is less tested. " +
                    "All bill by use — an evening of conversation is typically well under a dollar, and the mod " +
                    "shows each exchange's cost as you play.\n\n" +
                    "FREE OF CHARGE — aistudio.google.com gives a Gemini key with a real free tier (no card, " +
                    "about 1,500 replies a day). Set Backend to \"Gemini\" and paste it into \"GeminiApiKey\". " +
                    "Know the trade: on Google's FREE tier what passes through is read to improve their products, " +
                    "so nothing said there is private. CHEAPEST PAID — platform.deepseek.com, roughly half the " +
                    "cost of the default for near-equal answers (Backend \"DeepSeek\"; their servers are in China). " +
                    "(Local models: tinkerers only, unsupported.)\n\n" +
                    "2. PUT IT HERE — open:\n" + ModConfig.ConfigFilePath + "\n" +
                    "and paste the key into \"" + keyField + "\"." +
                    " (With the Mod Configuration Menu installed, the key can also be set in-game under Mod Options.)\n\n" +
                    "3. RESTART THE GAME — a short \"connected\" notice will greet you when the world is listening.";

                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        InformationManager.ShowInquiry(new InquiryData(
                            "Immersive AI — one thing before the world can speak",
                            body,
                            isAffirmativeOptionShown: true,
                            isNegativeOptionShown: false,
                            affirmativeText: "Understood",
                            negativeText: null,
                            affirmativeAction: null,
                            negativeAction: null), pauseGameActiveState: true);
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("first-run guide popup", ex);
                    }
                });

                ModLog.Info("First-run key guide shown (no API key configured).");
            }
            catch (Exception ex)
            {
                ModLog.Error("first-run guide", ex);
            }
        }
    }
}
