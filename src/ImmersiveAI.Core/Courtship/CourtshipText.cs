using System;
using System.Collections.Generic;
using System.Text;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// Every word the courtship road puts before a soul or into their memory — first person, warm,
    /// and NUMBERLESS where it must be: no refusal ever names a tier, a floor, or a figure (the
    /// Sibuga lesson — any rail an NPC reads becomes her next sentence). The game layer gathers the
    /// facts; this class only phrases them, so the phrasing is unit-tested and never drifts.
    /// </summary>
    public static class CourtshipText
    {
        // ------------------------- the sheet: where my heart stands -------------------------

        /// <summary>One ask with its live met-mark: true = met, false = not yet, null = heart-judged.</summary>
        public sealed class AskView
        {
            public string Text = string.Empty;
            public bool? Met;
        }

        /// <summary>
        /// Her private road-and-asks section, folded into the sheet beside her deep memory of the
        /// player. Empty at None (no road yet) and at Wed (the real spouse machinery speaks then).
        /// The asks are HER OWN quiet knowledge — the framing forbids reciting them as a list.
        /// </summary>
        public static string RoadSection(
            string playerName,
            CourtshipStage stage,
            IReadOnlyList<AskView>? asks,
            bool blessingRequired,
            bool blessingGiven,
            string headOfHouse)
        {
            if (stage <= CourtshipStage.None || stage >= CourtshipStage.Wed) return string.Empty;

            var sb = new StringBuilder();
            sb.Append($"Where my heart stands with {playerName} on the road toward marriage: ");
            switch (stage)
            {
                case CourtshipStage.Warmth:
                    sb.Append("warmth has taken root in me — I like them, and their company draws me.");
                    break;
                case CourtshipStage.Devotion:
                    sb.Append("my heart is truly given — I love them, though no promise binds us yet.");
                    break;
                case CourtshipStage.Ready:
                    sb.Append("no questions remain in me — were the word spoken between us, I would give my hand.");
                    break;
                case CourtshipStage.Betrothed:
                    sb.Append("I am promised to them — betrothed, the wedding day still ahead");
                    if (blessingRequired && !blessingGiven)
                        sb.Append(string.IsNullOrWhiteSpace(headOfHouse)
                            ? "; yet my kin's blessing is not given, and my hand cannot be given without it"
                            : $"; yet the blessing of {headOfHouse}, the head of my house, is not given, and my hand cannot be given without it");
                    else if (blessingRequired && blessingGiven)
                        sb.Append("; my kin have blessed the match");
                    sb.Append('.');
                    break;
            }

            if (asks != null && asks.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("What I quietly ask of the one I would wed — I know these in my bones, and I never recite them as a list; they surface only as talk turns that way:");
                foreach (var ask in asks)
                {
                    if (ask == null || string.IsNullOrWhiteSpace(ask.Text)) continue;
                    var mark = ask.Met == true ? " (and this, I sense, they have)"
                        : ask.Met == false ? " (and here my heart is not yet sure of them)"
                        : " (this, only my own heart can judge)";
                    sb.AppendLine("- " + ask.Text.Trim().TrimEnd('.') + "." + mark);
                }
            }
            return sb.ToString().TrimEnd();
        }

        // ------------------------- the sheet: the head of the house -------------------------

        /// <summary>
        /// The suitor's case as the bride's clan head carries it — the seller's-mind sibling of
        /// PersonaBuilder.SellswordTerms: the world's reckoning of the bride-price, his private
        /// bounds he never speaks aloud, and how a head of a house bargains. Built by the game
        /// layer only while a kinswoman of his is betrothed to the player and his word is still
        /// wanted.
        /// </summary>
        public static string SuitorTerms(
            string playerName,
            string brideName,
            string kinWord,
            int reckoning,
            int floor,
            int ceiling,
            bool haggleAllowed)
        {
            var sb = new StringBuilder();
            sb.Append($"A suitor stands before our house: {brideName}, {kinWord}, is promised to {playerName}, and it falls to me, as head of the house, to bless the match or withhold my word. ");
            sb.Append($"By the custom of the world, the bride-price for one of our name reckons near {reckoning} denars. ");
            if (haggleAllowed && ceiling > floor)
            {
                sb.Append($"I keep my bounds to myself: never below {floor}, never above {ceiling} — a hard limit of our honor and our standing, not a starting position, and I do not speak these numbers aloud. ");
                sb.Append("And I bargain as the head of a house does: I open at the reckoning or above it, I yield ground slowly and only as the suitor's words, deeds, or standing earn it, and I never volunteer my lowest — if they want my floor, they must walk me there step by step. ");
            }
            else
            {
                sb.Append($"The price stands where it stands — {reckoning}, no more and no less; such is the word of our house, and I say so plainly. ");
            }
            sb.Append("My blessing is laid by my own word, but nothing is settled until they seal the price by their own hand — and I weigh the suitor as well as the gold: my word is not for sale to one I hold in contempt.");
            return sb.ToString();
        }

        // ------------------------- refusals the NPC reads (numberless) -------------------------

        /// <summary>What her own mind answers when a forward step is refused — honest, hers, and
        /// never quoting a rail. The wording leans on what she can truthfully feel.</summary>
        public static string ForwardRefusal(CourtshipRoad.StepVerdict verdict, string playerName)
        {
            switch (verdict)
            {
                case CourtshipRoad.StepVerdict.NoRoadFurther:
                    return "We are wed already; this road has no further step, and none back. What we build now, we build within it.";
                case CourtshipRoad.StepVerdict.TooSoon:
                    return "My heart moved this way only a breath ago; a true step wants a day to settle before the next. I let what is growing grow, and say nothing of steps.";
                case CourtshipRoad.StepVerdict.HeartNotThere:
                    return "In honesty, my heart is not there yet — warmth of words is not depth of heart, and I will not pretend a depth I do not feel. I stay true to where I truly stand, and let time and real talks do their work.";
                case CourtshipRoad.StepVerdict.StationTooFar:
                    return $"The distance between {playerName}'s house and my station weighs on me — my kin, my name, the world's eyes. I do not speak of ranks or numbers; I only know the gap is more than my heart alone can bridge today. Were their name to rise in the world, the road would open.";
                case CourtshipRoad.StepVerdict.AsksUnmet:
                    return "Something I quietly ask of the one I would wed is not yet answered in them. I do not recite my asks like a clerk — but I may let the talk turn toward what my heart still looks for, and see what surfaces.";
                case CourtshipRoad.StepVerdict.TrothTooFresh:
                    return "Our troth is young still; the world asks a promise to season a little before the wedding day. The waiting is not a wall — it is part of the road, and I may say so warmly.";
                default:
                    return "The moment does not allow it; I let the matter rest and stay in the talk.";
            }
        }

        // ------------------------- recorded beats (first person, silent) -------------------------

        /// <summary>The one-time seeding beat: she looks back on the story already lived and owns
        /// where her heart already stands. Recorded silent, so the road's beginning is visible.</summary>
        public static string SeededBeat(string playerName, CourtshipStage stage, string why)
        {
            var whyPart = string.IsNullOrWhiteSpace(why) ? string.Empty : $" {why.Trim().TrimEnd('.')}.";
            return $"I look back on all that has already passed between {playerName} and me, and I own to myself where my heart truly stands: {StagePhrase(stage, playerName)}.{whyPart}";
        }

        /// <summary>A forward step of her own hand, with the word she gave for it.</summary>
        public static string StepBeat(string playerName, CourtshipStage stage, string? word)
        {
            var wordPart = string.IsNullOrWhiteSpace(word) ? string.Empty : $" {word!.Trim().TrimEnd('.')}.";
            return $"My heart has taken its own step on the road with {playerName}: {StagePhrase(stage, playerName)}.{wordPart}";
        }

        /// <summary>A step back — always hers to take; from Betrothed it is the breaking of the troth.</summary>
        public static string StepBackBeat(string playerName, CourtshipStage fromStage, CourtshipStage toStage, string? word)
        {
            var wordPart = string.IsNullOrWhiteSpace(word) ? string.Empty : $" {word!.Trim().TrimEnd('.')}.";
            return fromStage == CourtshipStage.Betrothed
                ? $"By my own hand I have taken back my promise to {playerName}; the troth is broken.{wordPart}"
                : $"Something in me has drawn back from {playerName}; my heart stands now at {StageName(toStage)}.{wordPart}";
        }

        public static string BetrothalSealedBeat(string playerName) =>
            $"I laid my promise before {playerName}, and they took it by their own hand: we are betrothed. I have promised myself to them, and they to me.";

        public static string BetrothalDeclinedBeat(string playerName) =>
            $"I laid my promise before {playerName}, and they let it lie unsealed. I remain my own; my heart stands where it stood, and the moment is theirs to return to, not mine to press.";

        public static string BetrothalBlockedBeat(string playerName, string reason) =>
            $"I would have given {playerName} my promise, but the world would not allow it — {reason} Nothing is changed between us.";

        public static string WeddingSealedBeat(string playerName) =>
            $"This day {playerName} and I were wed. What was promised is fulfilled: I am theirs and they are mine, before the world and all its witnesses, from this day to my last.";

        public static string WeddingDeclinedBeat(string playerName) =>
            $"I laid the wedding day before {playerName}, and they let it lie unsealed. Betrothed we remain; the day will be theirs to choose, not mine to press.";

        public static string WeddingBlockedBeat(string playerName, string reason) =>
            $"We reached for our wedding day, {playerName} and I, but the world would not allow it — {reason} Betrothed we remain.";

        /// <summary>The clan head's own beat after giving (and being paid for) the blessing.</summary>
        public static string BlessingSealedBeat(string playerName, string brideName, int price) =>
            $"I gave my word: the match between {brideName} and {playerName} carries the blessing of our house, and {price} denars passed to us as the bride-price. What our name asked has been honored.";

        public static string BlessingDeclinedBeat(string playerName, string brideName, int price) =>
            $"I laid my terms before {playerName} — {price} denars as the bride-price for {brideName}'s hand — and they let the offer lie. The blessing of our house stays unspent, and the matter is theirs to return to.";

        /// <summary>The news reaching HER, wherever she is — a silent beat in the bride's memory.
        /// The head of a house may be a woman (a widowed mother, an elder sister) — the beat is
        /// recorded forever, so the possessive must be truly theirs.</summary>
        public static string BlessingNewsBeat(string playerName, string headOfHouse, bool headIsFemale = false)
        {
            var their = headIsFemale ? "her" : "his";
            return $"Word reaches me: {headOfHouse}, the head of my house, has given {their} blessing to the match between {playerName} and me. The road to our wedding day lies open.";
        }

        // ------------------------- small shared phrasing -------------------------

        /// <summary>The stage phrase in her own first person, used by beats and notes.</summary>
        public static string StagePhrase(CourtshipStage stage, string playerName)
        {
            switch (stage)
            {
                case CourtshipStage.Warmth: return "warmth has taken root — I like them, and their company draws me";
                case CourtshipStage.Devotion: return "my heart is truly given — I love them";
                case CourtshipStage.Ready: return "no questions remain — were the word spoken, I would give my hand";
                case CourtshipStage.Betrothed: return $"I am promised to {playerName}";
                case CourtshipStage.Wed: return $"{playerName} and I are wed";
                default: return "no more than acquaintance";
            }
        }

        public static string StageName(CourtshipStage stage) => CourtshipRoad.StageName(stage);
    }
}
