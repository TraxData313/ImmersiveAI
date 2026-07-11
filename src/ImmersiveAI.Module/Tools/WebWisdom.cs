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
    /// All the NPC has ever read and heard: they may quietly search that wider remembrance for how
    /// something is done — the handling of ships, the raising of armies, the ways of trade — and
    /// receive it back as things once read and heard tell. Under the hood it is a web search
    /// (DuckDuckGo's HTML endpoint, no API key); the query is first sharpened by a small refining
    /// LLM call when the game layer provides one (an immersed soul asks in roleplay language — the
    /// refiner turns that into a search that actually finds the answer), else the game's name is
    /// quietly prepended. The NPC never sees the meta framing; the answer closes telling her to
    /// retell the substance in her own world's words, so titles like "patch 1.2" never leak into
    /// her speech — that framing is the whole fourth-wall defense, keep it.
    ///
    /// Everything is best-effort: a failed refine falls back to the raw query, a failed or empty
    /// search returns an honest silence, never an exception into the turn. Runs entirely off the
    /// game thread (plain HTTP, no campaign state).
    /// </summary>
    public static class WebWisdom
    {
        public const string SeekWisdom = "seek_wisdom";

        public static readonly ToolDefinition Tool = new ToolDefinition(SeekWisdom,
            "Search all you have ever read and heard tell of the world's ways — travelers' accounts, " +
            "scholars' letters, the knowing of those who study such things: ships and fleets, the raising " +
            "and joining of armies, the keeping of towns and castles, trade, war, craft, and rule. It " +
            "reaches even past the world's rim, to the world a visitor may themselves hail from. Reach " +
            "for this whenever you are asked how a thing is done — or for counsel or advice on such " +
            "matters — even when your own wit could half-answer: look first, for the looking makes your " +
            "counsel true where invention would make it hollow. Ask plainly.",
            new[]
            {
                new ToolParameter("question", "What you wish to know, asked plainly as a question."),
                new ToolParameter("beyond",
                    "Leave empty when the question is of your own world's ways. Write 'yes' when it reaches " +
                    "beyond your world — the asker's own world and life, other realms, matters no one of your " +
                    "world could know.", required: false)
            });

        // What the NPC hears when the search fails or finds nothing — honest silence, mirroring
        // ToolLoopRunner.NothingSurfaces in spirit. (Const name kept from the sages era.)
        public const string SagesSilent =
            "You search all you have read and heard, and nothing rises to answer. Own honestly what you do not know.";

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            // The HTML endpoint answers a plain browser; an empty agent gets an empty page.
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            return client;
        }

        /// <summary>Answers one search of all-read-and-heard. Safe to call from LLM background
        /// threads; touches no game state. <paramref name="refine"/>, when given, turns the NPC's
        /// immersed roleplay question into an effective real-world search query (see the game
        /// layer's RefineSearchQueryAsync); a null or failed refine falls back to the raw question
        /// with the game's name quietly prepended.</summary>
        public static async Task<string> ResolveAsync(ToolCall call, Func<string, bool, Task<string?>>? refine = null)
        {
            string question;
            bool beyond;
            try
            {
                var args = Newtonsoft.Json.Linq.JObject.Parse(call.ArgumentsJson);
                question = ((string)args["question"] ?? string.Empty).Trim();
                var beyondRaw = ((string)args["beyond"] ?? string.Empty).Trim();
                beyond = beyondRaw.StartsWith("y", StringComparison.OrdinalIgnoreCase)
                    || beyondRaw.StartsWith("t", StringComparison.OrdinalIgnoreCase);
            }
            catch { question = string.Empty; beyond = false; }
            if (question.Length == 0) return SagesSilent;

            try
            {
                // The refiner (when granted) rewrites the immersed phrasing into a query that truly
                // finds answers — "how might I grant my ships to a companion's fleet" becomes
                // "Bannerlord transfer ships to companion". Without it, the game's name rides along
                // unseen for own-world questions; a "beyond" question goes to the whole wide web.
                string? query = null;
                if (refine != null)
                {
                    try { query = await refine(question, beyond).ConfigureAwait(false); }
                    catch { query = null; }
                }
                if (string.IsNullOrWhiteSpace(query))
                    query = beyond ? question : "Mount and Blade II Bannerlord " + question;

                var url = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query);
                var html = await Http.GetStringAsync(url).ConfigureAwait(false);

                var findings = ParseResults(html, max: 5);
                if (findings.Count == 0) return SagesSilent;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("It comes back to you — things read and heard over the years. The telling is in a strange tongue, of another world, but the substance is yours to take:");
                foreach (var f in findings)
                    sb.AppendLine("- " + f);
                sb.AppendLine(beyond
                    ? "This is of things beyond your world's rim, recalled because the one before you speaks openly of them — speak of it plainly, in whatever words serve the truth of it, while remaining yourself. If they press further, or doubt, look again rather than reason onward alone."
                    : "Speak the substance as your own understanding, in the words of your world; let none of the strange terms — titles, numbers of versions, talk of screens and keys — pass your lips unless the asker plainly speaks that tongue first. If they press further, or doubt, or ask after another way, look again rather than reason onward alone. And should they attest from their own hand that a thing is done otherwise, trust the living witness before you, and say so with grace.");
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
