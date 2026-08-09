using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// The player's own next line, worked out for them ("Let me think…", 2026.08.10 — Anton's ask:
    /// "оставям мозъка на моя герой да помисли"). It rides the VERY sheet the one before them
    /// answers on — who they both are, the whole remembered story, the situation, the live thread —
    /// so the line that comes back is one a person in the player's place could truly say next.
    ///
    /// Two rails hold it apart from everything else the mod does:
    /// • It is a PLAIN call — no tools ride along, so nothing here can move a heart, tend a
    ///   courtship, or touch a single file. Nothing is recorded: the words land in the writing
    ///   line and are the player's to keep, rewrite, or throw away.
    /// • The instruction is an ASIDE, never spoken and never heard. No word of it breaks the
    ///   world (no talk of prompts or machines) — it simply asks for one voice instead of another,
    ///   the way an actor is handed the other half of a scene.
    /// </summary>
    public static class PlayerThought
    {
        /// <summary>
        /// The system message: the PLAYER's own mind, in their own first person — the only voice in
        /// the whole call. THIS IS THE FIX for the first playtest (2026.08.10): the first cut hung
        /// the aside off the end of the NPC's own live chat, where the sheet says "I am Sibylla" and
        /// the last spoken turn is hers, so the model simply carried on as her and handed the player
        /// HER line ("Обичам да чувам гласа ти") to send back to her. No wording beats a seating
        /// chart: the NPC's sheet and the transcript are now MATERIAL inside one user message
        /// (see <see cref="PromptBuilder.BuildPlayerThought"/>), there is no assistant turn of hers
        /// to continue, and the only "I" from the first line to the last is the player's.
        /// </summary>
        public static string MindFrame(string playerName, string npcName, bool asLetter = false)
        {
            var player = Name(playerName, "the traveler");
            var npc = Name(npcName, "them");
            return asLetter
                ? $"I am {player}. I am working out what to write to {npc} — the letter is not sealed yet, and " +
                  $"nothing here has reached them.\nI set down my own words only. {npc}'s mind and our own past " +
                  "words are before me to read, never to speak with: I do not answer in their voice, and I never " +
                  "write their side of it."
                : $"I am {player}. I am working out what to say next to {npc} — nothing here is spoken aloud yet.\n" +
                  $"I set down my own words only. {npc}'s mind and all that has passed between us are before me " +
                  "to read, never to speak with: I do not answer in their voice, and I never say their side of it.";
        }

        /// <summary>The block that closes the material when the player is thinking out what to SAY.
        ///
        /// Its shape is Anton's own (2026.08.10): <i>this is the story so far — now it is my turn —
        /// what turns in my mind is "…" — I will say:</i>. That one frame carries all three ways the
        /// box is used, with no branching prose to explain them: EMPTY, and the moment itself must
        /// find the words (a continuation, or an opening where there is nothing yet); a RANT
        /// half-typed, read as half-formed thought and handed back as words; a chosen PRESET, which
        /// slots into "what turns in my mind" as naturally as anything else.
        ///
        /// Deliberately SHORT (his ask, in as many words: no bedsheets of instruction). Everything
        /// it does not say, the material above it already does — the speech rules, the acting-out
        /// custom, the whole story. It adds only what none of that can know: whose turn it is.</summary>
        public static string SpokenLine(string playerName, string? wish, bool mayAct = false)
        {
            var player = Name(playerName, "the traveler");
            var sb = new StringBuilder();
            sb.AppendLine("[Now it is my turn to speak.]");
            AppendWish(sb, wish, "say");
            sb.AppendLine();
            sb.AppendLine("I carry the talk on in the same spirit, and in the same tongue we have been speaking " +
                          "in. As short as talk truly is, unless the thought above asks for more.");
            if (mayAct)
                sb.AppendLine("A small act of mine may ride between single *asterisks*, as our custom is — sparingly.");
            sb.AppendLine();
            sb.Append($"{player}:");
            return sb.ToString();
        }

        /// <summary>The same block for the letter window: what the player WRITES, not what they say.
        /// A letter runs as long as it runs, so nothing here asks for brevity.</summary>
        public static string LetterLine(string playerName, string? wish)
        {
            var player = Name(playerName, "the traveler");
            var sb = new StringBuilder();
            sb.AppendLine("[Now it is my turn to write.]");
            AppendWish(sb, wish, "write");
            sb.AppendLine();
            sb.AppendLine("I write in the same spirit, and in the same tongue we have been speaking in — the words " +
                          "that will stand on the page before them, not a telling about them.");
            sb.AppendLine();
            sb.Append($"{player} writes:");
            return sb.ToString();
        }

        // The one line that does the work of three: a wish read as half-formed THOUGHT, never as
        // wording to be handed back. Empty, it says so plainly and leaves the moment to find it —
        // which is the whole of what "think with nothing typed" means.
        private static void AppendWish(StringBuilder sb, string? wish, string verb)
        {
            var want = (wish ?? string.Empty).Trim();
            sb.AppendLine();
            sb.AppendLine(want.Length == 0
                ? $"Nothing is settled in my mind yet — the moment itself must find what to {verb}."
                : $"What turns in my mind: “{want}”\n— half-formed thought, not the words themselves.");
        }

        private static string Name(string? name, string fallback) =>
            string.IsNullOrWhiteSpace(name) ? fallback : name!.Trim();

        // ------------------------------ taming the answer ------------------------------

        /// <summary>Keeps only the words themselves. Models hand a line back wrapped in quotes, under
        /// a "Here is what you could say:" heading, behind an echoed aside, or labelled with the
        /// speaker's name — all of which would be typed straight into the writing line if left. Every
        /// cut is conservative: when in doubt the text stands as it came.</summary>
        public static string Tame(string? raw, string? playerName = null)
        {
            var text = (raw ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (text.Length == 0) return string.Empty;

            text = DropEchoedAside(text);
            text = DropLeadIn(text);
            text = DropSpeakerLabel(text, playerName);
            text = DropEchoedArrow(text);
            text = Unwrap(text);
            return text.Trim();
        }

        // An echoed "[An aside, …]" — or any wholly bracketed opening line — is stage furniture.
        private static string DropEchoedAside(string text)
        {
            while (true)
            {
                var lines = text.Split('\n');
                var first = lines[0].Trim();
                if (lines.Length < 2) return text;
                if (!(first.StartsWith("[", StringComparison.Ordinal) && first.EndsWith("]", StringComparison.Ordinal)))
                    return text;
                text = string.Join("\n", lines.Skip(1)).Trim();
                if (text.Length == 0) return text;
            }
        }

        // "Here is what you might say:" — a SHORT first line ending in a colon, with the real text
        // set off below it. Deliberately timid, and biased toward keeping: a lead-in left standing
        // is a nuisance the player can see and delete, while a genuine first line cut away
        // ("Listen, brother:" — which is why the word count is not enough on its own) is a loss they
        // never notice. So the heading must also be SET OFF from what follows — by a blank line, or
        // by the answer proper opening on a quotation mark.
        private static string DropLeadIn(string text)
        {
            var lines = text.Split('\n');
            if (lines.Length < 2) return text;

            var first = lines[0].Trim();
            if (!first.EndsWith(":", StringComparison.Ordinal)) return text;
            if (first.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 10) return text;

            var rest = string.Join("\n", lines.Skip(1)).Trim();
            if (rest.Length == 0) return text;

            bool setOff = lines[1].Trim().Length == 0
                          || Fences.Any(f => rest[0] == f.Open);
            return setOff ? rest : text;
        }

        // The frame's own arrow, handed back at the head of the answer. An em dash is deliberately
        // NOT stripped: in half the world's typography that is how a spoken line opens.
        private static string DropEchoedArrow(string text)
        {
            foreach (var arrow in new[] { "→", "->" })
                if (text.StartsWith(arrow, StringComparison.Ordinal))
                    return text.Substring(arrow.Length).TrimStart();
            return text;
        }

        // "Vulgrim: ..." — the speaker's own name typed in front of their words, or the frame's own
        // closing line ("Vulgrim says →") handed straight back. Only a real label is cut: a line
        // that merely OPENS on the name ("Vulgrim would never say that.") is left whole.
        private static string DropSpeakerLabel(string text, string? playerName)
        {
            var name = (playerName ?? string.Empty).Trim();
            if (name.Length == 0) return text;
            if (!text.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return text;

            var rest = text.Substring(name.Length).TrimStart();
            foreach (var verb in new[] { "says", "writes" })
                if (rest.StartsWith(verb, StringComparison.OrdinalIgnoreCase))
                {
                    rest = rest.Substring(verb.Length).TrimStart();
                    break;
                }

            foreach (var mark in new[] { "->", ":", "—", "→" })
                if (rest.StartsWith(mark, StringComparison.Ordinal))
                    return rest.Substring(mark.Length).TrimStart();
            return text;
        }

        // A whole answer fenced in quotation marks — but only when the fence truly wraps everything,
        // so a line that merely quotes someone inside itself keeps its marks.
        private static readonly (char Open, char Close)[] Fences =
        {
            ('"', '"'), ('“', '”'), ('«', '»'), ('„', '“'), ('„', '”'), ('\'', '\''),
        };

        private static string Unwrap(string text)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                if (text.Length < 2) return text;
                bool cut = false;
                foreach (var fence in Fences)
                {
                    if (text[0] != fence.Open || text[text.Length - 1] != fence.Close) continue;
                    var inner = text.Substring(1, text.Length - 2).Trim();
                    if (inner.Length == 0) return text;
                    // A closing mark in the middle means the fence was never the whole wrapper.
                    if (inner.IndexOf(fence.Close) >= 0) continue;
                    text = inner;
                    cut = true;
                    break;
                }
                if (!cut) return text;
            }
            return text;
        }
    }
}
