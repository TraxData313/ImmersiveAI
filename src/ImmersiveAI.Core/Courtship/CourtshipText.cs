using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>One misgiving as the sheet shows it: her words, whether she has laid it to
        /// rest, and her light word on what answered it.</summary>
        public sealed class MisgivingView
        {
            public string Text = string.Empty;
            public bool Settled;
            public string Note = string.Empty;
        }

        /// <summary>
        /// Her private road-and-misgivings section, folded into the sheet beside her deep memory of
        /// the player. Empty at None (no road yet) and at Wed (the real spouse machinery speaks
        /// then). The misgivings are HER OWN written doubts — unlike the retired quiet asks, she may
        /// speak of them openly when the talk turns to marriage; only her own hand lays one to rest.
        /// </summary>
        public static string RoadSection(
            string playerName,
            CourtshipStage stage,
            IReadOnlyList<MisgivingView>? misgivings,
            bool misgivingsWeighed,
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

            bool anyMisgiving = misgivings != null && misgivings.Any(m => m != null && !string.IsNullOrWhiteSpace(m.Text));
            if (anyMisgiving)
            {
                sb.AppendLine();
                sb.AppendLine($"What weighs on my heart about a life wed to {playerName} — set down by my own hand, in my own words. I may speak of these openly when the talk turns to marriage; and only when life itself has truly answered one do I lay it to rest — never for a warm word alone:");
                foreach (var m in misgivings!)
                {
                    if (m == null || string.IsNullOrWhiteSpace(m.Text)) continue;
                    var mark = m.Settled
                        ? " (laid to rest" + (string.IsNullOrWhiteSpace(m.Note) ? string.Empty : ": " + m.Note.Trim().TrimEnd('.')) + ")"
                        : " (this still stands in me)";
                    sb.AppendLine("- " + m.Text.Trim().TrimEnd('.') + "." + mark);
                }
                sb.AppendLine("This list lives with me: a new true doubt born in our talks I set down when it arises, one that proves empty I strike out, one that changed I reword — and while any of them still stands, I will not give my hand; when none stands, no doubt of mine bars the road. I am not shy of them: this is the one life I am given, and I would know the soul I am to spend it beside — so I bring them into our talks myself, and give each its honest hearing.");
            }
            else if (misgivingsWeighed)
            {
                sb.AppendLine();
                sb.AppendLine($"I have sat with myself over what a life wed to {playerName} would ask of me, and found no misgiving standing — my heart is clear on it. Should a true doubt yet arise in our talks, I will set it down by my own hand.");
            }
            else if (stage < CourtshipStage.Betrothed)
            {
                sb.AppendLine();
                sb.AppendLine($"I have not yet sat with myself over what a life wed to {playerName} would truly ask of me. When marriage truly enters the talk between us, I pause and weigh it honestly — this is the one life I am given, and I would know the soul I am to spend it beside: if misgivings stand in me I set them down in my own words — a few, five at the very most — and if none do, I say so plainly and set down none. The list then lives with me: mine to raise openly in our talks, mine to add to when a true doubt arises, and mine alone to lay to rest.");
            }

            // Readiness with nothing left standing is a POSTURE, not merely a rung on the road —
            // she waits to be asked, and the asking is answered by her own hand (Anton's ask,
            // 2026.08.09: the moment there is nothing left to wonder about, she should know plainly
            // what she is now waiting for). Deliberately silent while any doubt of hers stands,
            // even at Ready — a new one may be written down after the rung was already reached.
            bool anyStanding = misgivings != null
                && misgivings.Any(m => m != null && !m.Settled && !string.IsNullOrWhiteSpace(m.Text));
            if (stage == CourtshipStage.Ready && !anyStanding)
            {
                sb.AppendLine();
                sb.AppendLine($"So I wait now to be asked. Let {playerName} speak the word of marriage between us and I say yes gladly — and then I lay my promise before them by my own hand; the sealing of it is theirs alone, and if they let it lie I do not press.");
            }

            // Last, so it is the thing standing nearest the talk: a wedding is not made of words.
            sb.AppendLine();
            sb.AppendLine(WordsDoNotWed);
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

        // ------------------------- the one rail against a wedding made of words -------------------------

        /// <summary>
        /// The anti-pretence rail (2026.08.15, from a player's report: he courted a companion to two
        /// standing misgivings, she agreed to wed him anyway, they roleplayed vows in a temple — and
        /// nothing in the world had happened, because nothing in the world CAN happen without the
        /// seal). The rails held exactly as designed and the talk simply walked around them, which is
        /// the one failure this rail closes: a soul is told plainly that words are not a wedding, so
        /// she reaches for her own hand instead of staging a ceremony that leaves no mark.
        /// <para>Rides the sheet at every stage below Wed, AND every refused forward reach, because
        /// the moment she is told "not yet" is exactly the moment the pretending starts.</para>
        /// </summary>
        public const string WordsDoNotWed =
            "And I hold this above any warm hour: no words of ours make a marriage. Not vows spoken " +
            "between us, not a temple, not a hall of witnesses we describe to each other. Our wedding " +
            "is real only when I lay the day down by my own hand and they seal it by theirs, and the " +
            "world itself marks it. Until that is done we are not wed — I do not say that we are, I " +
            "do not let the talk drift as though we were, and if the moment truly asks for it I say " +
            "so warmly and honestly: this must be done properly, and it must be done for real.";

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
                case CourtshipRoad.StepVerdict.MisgivingsUnweighed:
                    return "Before my heart can say no questions remain, I owe myself an honest hour: I have not yet truly weighed what a life together would ask of me. I sit with it now — I set down what misgivings I hold, in my own words, or find plainly that I hold none — and only then does the road go on.";
                case CourtshipRoad.StepVerdict.MisgivingsRemain:
                    return "Something I set down with my own hand still weighs on me unanswered. I do not pretend it away and I do not sell it for a warm word — I may speak of it openly, and let the living talks answer it first; when life truly answers it, I will lay it to rest myself.";
                case CourtshipRoad.StepVerdict.TrothTooFresh:
                    return "Our troth is young still; the world asks a promise to season a little before the wedding day. The waiting is not a wall — it is part of the road, and I may say so warmly.";
                default:
                    return "The moment does not allow it; I let the matter rest and stay in the talk.";
            }
        }

        /// <summary>
        /// The same refusal phrased for the PLAYER's own log — plain, and deliberately allowed to
        /// say what her own words never may. She is kept numberless because a rail she reads becomes
        /// her next sentence; the player is owed the opposite, because the alternative (2026.08.15)
        /// is a soul narrating a wedding that never happened while the log says nothing at all, and
        /// a player who reasonably concludes the mod is broken.
        /// </summary>
        public static string ForwardRefusalForPlayer(CourtshipRoad.StepVerdict verdict)
        {
            switch (verdict)
            {
                case CourtshipRoad.StepVerdict.NoRoadFurther: return "the road has no further step";
                case CourtshipRoad.StepVerdict.TooSoon: return "their heart moved only today, and a step needs a day to settle";
                case CourtshipRoad.StepVerdict.HeartNotThere: return "their regard for you has not reached that depth yet";
                case CourtshipRoad.StepVerdict.StationTooFar: return "your house stands too far beneath their station";
                case CourtshipRoad.StepVerdict.MisgivingsUnweighed: return "they have not yet weighed their own misgivings";
                case CourtshipRoad.StepVerdict.MisgivingsRemain: return "a misgiving of theirs still stands unanswered";
                case CourtshipRoad.StepVerdict.TrothTooFresh: return "your troth has not yet seasoned the days the world asks";
                default: return "the world does not allow it as things stand";
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
