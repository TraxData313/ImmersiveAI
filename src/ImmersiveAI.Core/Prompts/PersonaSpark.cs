using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// The director's spark: a one-time, LLM-written private truth (1–3 first-person sentences)
    /// seeded into a soul's custom_instructions.txt at their FIRST interaction, so every character
    /// steps on stage already somebody — a wound, a habit, a vanity — instead of a blank sheet.
    /// This class is the pure half: the muse deck, the weighted intensity die, the director prompt
    /// (live-validated against gpt-5.6-terra on 2026.08.07), and the output clamp. The game layer
    /// owns when to call it and where the words land (PromptFiles' spark stamp).
    ///
    /// Design rails: the cards are an INVITATION, never an order ("take ONE — or discard both");
    /// the output must be concrete and first person; it must never repeat the sheet's facts. The
    /// randomness is real RNG, not the MoodTides determinism — the result is written to disk once,
    /// so a reroll is a feature (the DevMode lever), not a bug.
    /// </summary>
    public static class PersonaSpark
    {
        /// <summary>The muse deck — directions, not commands. Two are drawn per soul; the model may
        /// take one or throw both away. Kept broad so a thousand souls don't share ten shapes.</summary>
        public static readonly IReadOnlyList<string> Deck = new[]
        {
            "an old wound that never healed",
            "a secret shame",
            "a secret pride",
            "an odd habit, ritual, or superstition",
            "a fear they hide",
            "a joy that seems too small to matter",
            "a grudge held too long",
            "a debt of gratitude never repaid",
            "a strange way with words",
            "a thing they always carry",
            "a person they measure everyone against",
            "a dream they tell no one",
            "a rule they never break",
            "a lie they tell often",
            "what they do when no one watches",
            "a sound or smell that undoes them",
            "the thing that makes them laugh when nothing else does",
            "a quiet doubt of the heart they would never speak in company",
            "a love of a craft beyond all duty",
            "a soft spot for one kind of stranger",
            "a vanity",
            "a hunger or a thrift that rules their table",
            "how they hope to die, when they let themselves think of it",
            "the person they failed",

            // THE THREE CARDS OF THE ORDER OF THE WORLD (2026.08.15). The era holds one thing
            // about a woman's place — first and only is honor, second or hidden is shame — and
            // that holding is world-knowledge every soul carries. What is NOT world-knowledge is
            // her stance toward it, and these exist so that stance is drawn rather than assumed.
            // One norm, sixty women, no two of them saying the same thing about it: that is the
            // whole point, and if every woman sounds alike here the mod has failed its founding
            // rule. They are phrased for anyone, because a man has a stance on this too.
            "how they carry the world's rules about women, wives and honor — with pride, with a private ache, or as armor",
            "what they secretly think a person's worth is measured by, when the world says it is the house they keep",
            "what they would give up their place in the world's good opinion for, if anything",
        };

        /// <summary>One face of the intensity die: how bold the spark should be, with the exact
        /// clause the director prompt carries for it.</summary>
        public sealed class Intensity
        {
            public string Name { get; }
            public string Clause { get; }
            public double Weight { get; }

            public Intensity(string name, string clause, double weight)
            {
                Name = name;
                Clause = clause;
                Weight = weight;
            }
        }

        /// <summary>The intensity die: mostly marked, sometimes barely a shade, and one throw in
        /// five something the tavern would gossip about — so a town holds both quiet souls and
        /// memorable ones, the way real rooms do.</summary>
        public static readonly IReadOnlyList<Intensity> Intensities = new[]
        {
            new Intensity("SUBTLE", "SUBTLE — barely a shade, colour not drama.", 0.30),
            new Intensity("MARKED", "MARKED — plain to see by the second conversation.", 0.50),
            new Intensity("VIVID", "VIVID — bold, memorable, even a little mad; the kind of trait a tavern would gossip about.", 0.20),
        };

        /// <summary>Draws two distinct muse cards.</summary>
        public static (string First, string Second) DrawCards(Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            int a = rng.Next(Deck.Count);
            int b = rng.Next(Deck.Count - 1);
            if (b >= a) b++;
            return (Deck[a], Deck[b]);
        }

        /// <summary>Rolls the intensity die by weight.</summary>
        public static Intensity DrawIntensity(Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            double total = Intensities.Sum(i => i.Weight);
            double roll = rng.NextDouble() * total;
            foreach (var intensity in Intensities)
            {
                roll -= intensity.Weight;
                if (roll <= 0) return intensity;
            }
            return Intensities[Intensities.Count - 1];
        }

        /// <summary>Everything the director is told about the new soul. All strings are optional
        /// except the name — empty ones simply leave their line out of the prompt.</summary>
        public sealed class Facts
        {
            public string Name = string.Empty;
            /// <summary>"woman" / "man".</summary>
            public string GenderWord = string.Empty;
            public int Age;
            /// <summary>Their station in the world, e.g. "a Sturgian wanderer (a sellsword seeking work)".</summary>
            public string Station = string.Empty;
            /// <summary>Where they are met, e.g. "found in the tavern of Varcheg".</summary>
            public string WherePhrase = string.Empty;
            /// <summary>Their real traits, e.g. "honest, daring, somewhat merciful".</summary>
            public string Traits = string.Empty;
            /// <summary>The already-assigned speech style, so the spark grows WITH the voice, never against it.</summary>
            public string SpeechStyle = string.Empty;
            /// <summary>The story the world tells of them (the same text that seeds self.txt).</summary>
            public string Backstory = string.Empty;
            /// <summary>The player's global prompt (+ roleplay guidance), so the keeper's world colours the souls.</summary>
            public string WorldText = string.Empty;
        }

        /// <summary>
        /// Builds the director's prompt — a plain utility call (like the search refiner), NOT the
        /// NPC's own mind: the director is meta by nature, but the OUTPUT is in-world, first person,
        /// and lands in the soul's sheet forever. Validated live against terra; change wording only
        /// with fresh live samples in hand.
        /// </summary>
        public static string BuildPrompt(Facts facts, string cardOne, string cardTwo, Intensity intensity)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            if (intensity == null) throw new ArgumentNullException(nameof(intensity));
            var they = string.IsNullOrWhiteSpace(facts.GenderWord) ? "they" : (facts.GenderWord == "woman" ? "she" : "he");
            var their = they == "she" ? "her" : they == "he" ? "his" : "their";
            var livesWord = they == "they" ? "live" : "lives";
            var holdsWord = they == "they" ? "hold" : "holds";

            var sb = new StringBuilder();
            sb.AppendLine("You are the casting director of a living medieval world. A new soul steps on stage for the first time, and you write the one private truth that will make them nobody else — the seed of a character an audience would remember.");
            sb.AppendLine();
            sb.AppendLine("The raw facts of them:");

            var head = new StringBuilder();
            head.Append("- ").Append(facts.Name.Trim());
            if (!string.IsNullOrWhiteSpace(facts.GenderWord)) head.Append(" — ").Append(facts.GenderWord.Trim());
            if (facts.Age > 0) head.Append(", about ").Append(facts.Age);
            if (!string.IsNullOrWhiteSpace(facts.Station)) head.Append(", ").Append(facts.Station.Trim());
            if (!string.IsNullOrWhiteSpace(facts.WherePhrase)) head.Append(", ").Append(facts.WherePhrase.Trim());
            head.Append('.');
            sb.AppendLine(head.ToString());

            if (!string.IsNullOrWhiteSpace(facts.Traits))
                sb.AppendLine($"- {Cap(their)} cast of mind, from the world's reckoning: {facts.Traits.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.SpeechStyle))
                sb.AppendLine($"- {Cap(their)} way of speaking, already set: {facts.SpeechStyle.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.Backstory))
                sb.AppendLine($"- {Cap(their)} story, as the world tells it: \"{facts.Backstory.Trim()}\"");

            if (!string.IsNullOrWhiteSpace(facts.WorldText))
            {
                sb.AppendLine();
                sb.AppendLine($"The world {they} {livesWord} in, as its keeper wrote it: \"{facts.WorldText.Trim()}\"");
            }

            sb.AppendLine();
            sb.AppendLine("Muse cards drawn for this soul (take ONE as your seed — or discard both if a truer spark comes to you):");
            sb.AppendLine("- " + cardOne);
            sb.AppendLine("- " + cardTwo);
            sb.AppendLine("Intensity drawn: " + intensity.Clause);
            sb.AppendLine();
            sb.AppendLine($"Write 1 to 3 sentences in {their} own first-person voice, present tense — private truths {they} {holdsWord} about {(they == "they" ? "themselves" : they == "she" ? "herself" : "himself")}. Make them CONCRETE: name the person, the place, the habit, the sound, the smell. They must grow out of {their} story and traits, never contradict them, and never repeat what the facts above already say. No preamble, no quotes, no talk of cards or directors — output only the sentences themselves.");

            // THE TONGUE, riding last (2026.08.17, Steam/Moetel). Every instruction above is English,
            // so most models answered in English however the keeper wrote their world — and this text
            // is written ONCE, at a first meeting, and kept forever. That is the "it depends which
            // model I first met them with" effect exactly. The keeper's own world text is the only
            // sample of their tongue that exists this early: the soul has not yet spoken a word, and a
            // hand-written custom_instructions.txt cannot be evidence here because its presence blocks
            // the spark outright. So a keeper who wants their souls sparked in their own language must
            // WRITE global_prompt.txt in it — evidence beats instruction on the weaker models, which is
            // the whole reason this rule shows rather than tells.
            TongueRule.AppendQuoted(sb, facts.WorldText,
                "These are the words of the keeper who made this world:",
                "the sentences");

            return sb.ToString().TrimEnd();
        }

        private static string Cap(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>
        /// Tames the model's answer into the promised shape: wrapping quotes/backticks stripped,
        /// and no more than <paramref name="maxSentences"/> sentences kept (a chatty model that
        /// wrote a paragraph is cut at a sentence boundary, never mid-thought). Returns an empty
        /// string when nothing usable remains — the caller then simply seeds nothing this time.
        /// </summary>
        public static string ClampToSentences(string? raw, int maxSentences = 3)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var text = raw!.Trim();

            // Strip one layer of wrapping quotes or code fencing the model may have added.
            text = text.Trim('`').Trim();
            if (text.Length > 1)
            {
                var pairs = new (char Open, char Close)[] { ('"', '"'), ('“', '”'), ('\'', '\'') };
                foreach (var pair in pairs)
                    if (text[0] == pair.Open && text[text.Length - 1] == pair.Close)
                    {
                        text = text.Substring(1, text.Length - 2).Trim();
                        break;
                    }
            }
            if (text.Length == 0) return string.Empty;
            if (maxSentences < 1) maxSentences = 1;

            // Count sentence ends. A punctuation run followed by a lowercase continuation is a
            // pause (an ellipsis, "I wait... then I strike"), not a sentence's end.
            int sentences = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '.' && c != '!' && c != '?') continue;
                bool lastOfRun = i + 1 >= text.Length || (text[i + 1] != '.' && text[i + 1] != '!' && text[i + 1] != '?');
                if (!lastOfRun) continue;

                int next = i + 1;
                while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
                if (next < text.Length && char.IsLower(text[next])) continue;

                sentences++;
                if (sentences >= maxSentences)
                    return text.Substring(0, i + 1).Trim();
            }
            return text;
        }
    }
}
