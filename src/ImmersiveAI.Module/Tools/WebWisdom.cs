using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The counsel of the far-seeing sages: an NPC may quietly ask the wider world how something is
    /// done — the handling of ships, the raising of armies, the ways of trade — and receive, as
    /// gentle remembrance, what "voices from beyond the horizon" answer. Under the hood it is a web
    /// search (DuckDuckGo's HTML endpoint, no API key) with the game's name quietly prepended, so
    /// the NPC never sees or speaks the meta framing: to her it is sages; to the player it is her
    /// companions replacing the wiki tab. The answer closes with an instruction to translate the
    /// sages' "strange tongue" into her own world's words, so titles like "patch 1.2" never leak
    /// into her speech.
    ///
    /// Everything is best-effort: a failed or empty search returns an honest silence, never an
    /// exception into the turn. Runs entirely off the game thread (plain HTTP, no campaign state).
    /// </summary>
    public static class WebWisdom
    {
        public const string SeekWisdom = "seek_wisdom";

        public static readonly ToolDefinition Tool = new ToolDefinition(SeekWisdom,
            "Seek the counsel of the far-seeing sages — voices from beyond the horizon whose knowing of " +
            "the world's ways is wide and strangely exact: the handling of ships and fleets, the raising " +
            "and joining of armies, the arts of trade, war, and rule. Reach for this when someone asks " +
            "you HOW a thing in the world is done and your own knowledge falls short. Ask plainly.",
            new[] { new ToolParameter("question", "What you wish to know, asked plainly as a question about the ways of the world.") });

        // What the NPC hears when the search fails or finds nothing — honest silence, mirroring
        // ToolLoopRunner.NothingSurfaces in spirit but in the sages' register.
        public const string SagesSilent =
            "You quiet your mind and ask, but the sages are silent — no counsel comes. Own honestly what you do not know.";

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            // The HTML endpoint answers a plain browser; an empty agent gets an empty page.
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            return client;
        }

        /// <summary>Answers one counsel-seeking. Safe to call from LLM background threads; touches
        /// no game state.</summary>
        public static async Task<string> ResolveAsync(ToolCall call)
        {
            string question;
            try
            {
                question = ((string)Newtonsoft.Json.Linq.JObject.Parse(call.ArgumentsJson)["question"] ?? string.Empty).Trim();
            }
            catch { question = string.Empty; }
            if (question.Length == 0) return SagesSilent;

            try
            {
                // The game's name rides along unseen, so "how do I transfer ships to a companion's
                // army" finds the right world's answers without the NPC ever naming a game.
                var query = "Mount and Blade II Bannerlord " + question;
                var url = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query);
                var html = await Http.GetStringAsync(url).ConfigureAwait(false);

                var findings = ParseResults(html, max: 5);
                if (findings.Count == 0) return SagesSilent;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("You quiet your mind and ask, and the far-seeing sages answer. Their tongue is strange — of another world — but the truth in it is yours to take:");
                foreach (var f in findings)
                    sb.AppendLine("- " + f);
                sb.AppendLine("Take from this the substance of the matter and speak it as your own understanding, in the words of your world; let none of the sages' strange terms — their titles, their numbers of versions, their talk of screens and keys — pass your lips unless the asker plainly speaks that tongue first. And if the asker presses further, or doubts, or asks after another way — do not reason onward from this counsel alone; ask the sages anew, for they know more than any one answer carries. Above all: should the one before you attest from their own hand that a thing is done otherwise — that they have themselves done what the counsel calls impossible — trust the living witness before you over the sages' distant word, and say so with grace.");
                return sb.ToString().TrimEnd();
            }
            catch
            {
                return SagesSilent;
            }
        }

        // Pulls "title: snippet" lines out of DuckDuckGo's HTML results page. Regex over markup is
        // deliberate here: no HTML parser ships with net472, the page is flat and stable, and any
        // drift in its shape only degrades to the sages falling silent.
        private static List<string> ParseResults(string html, int max)
        {
            var findings = new List<string>();
            if (string.IsNullOrEmpty(html)) return findings;

            var titles = Regex.Matches(html, "<a[^>]*class=\"result__a\"[^>]*>(.*?)</a>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var snippets = Regex.Matches(html, "class=\"result__snippet\"[^>]*>(.*?)</a>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            for (int i = 0; i < titles.Count && findings.Count < max; i++)
            {
                var title = CleanFragment(titles[i].Groups[1].Value);
                var snippet = i < snippets.Count ? CleanFragment(snippets[i].Groups[1].Value) : string.Empty;
                if (title.Length == 0 && snippet.Length == 0) continue;
                findings.Add(snippet.Length > 0 ? $"{title}: {snippet}" : title);
            }
            return findings;
        }

        private static string CleanFragment(string fragment)
        {
            var text = Regex.Replace(fragment ?? string.Empty, "<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);
            return Regex.Replace(text, @"\s+", " ").Trim();
        }
    }
}
