using System;
using System.Text;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// Every word of the lover's road (2026.08.15) — her sheet's own section, the numberless refusals
    /// her mind reads, the plain ones the player's log reads, and the permanent beat marks.
    ///
    /// THE ONE RULE THIS FILE EXISTS TO HOLD. The world of this era holds that to be first and only is
    /// honor and to be second, or hidden, is shame. That is stated here as what the WORLD holds — the
    /// air everybody breathes — and never once as what SHE feels. Her stance toward it is hers: her
    /// traits, her spark, her lived story, and her right to have no settled stance at all. If every
    /// woman in a player's game sounds the same about this, we have failed the founding rule of the
    /// whole mod, and the fix is here rather than anywhere else.
    ///
    /// The BEAT MARKS are permanent, as all recorded phrasing in this mod is: a memory keeps the words
    /// it was born with forever. Never reword one; add a new one beside it.
    /// </summary>
    public static class LoverText
    {
        // ------------------------- the marks memory keeps forever -------------------------

        /// <summary>Opens the beat where she becomes his. NEVER reword.</summary>
        public const string BondSealedMark = "Of what I am to them now:";

        /// <summary>Opens the beat where the bond ends, from either hand. NEVER reword.</summary>
        public const string BondEndedMark = "Of what we were:";

        /// <summary>Opens the beat where her house was paid for her. NEVER reword.</summary>
        public const string RansomMark = "Of the price paid for me:";

        public static bool IsLoverBeat(string? line) =>
            !string.IsNullOrEmpty(line)
            && (line!.IndexOf(BondSealedMark, StringComparison.Ordinal) >= 0
                || line.IndexOf(BondEndedMark, StringComparison.Ordinal) >= 0
                || line.IndexOf(RansomMark, StringComparison.Ordinal) >= 0);

        // ------------------------- the sheet: what I am to them -------------------------

        /// <summary>
        /// Her private section of the sheet while the bond stands (or once stood). It says three
        /// things and deliberately stops: what she IS, what the world holds about women in that
        /// place, and what she could ask for if she wanted it — each of them a fact, none of them a
        /// feeling. The feelings are hers and are written nowhere.
        /// </summary>
        /// <param name="hisOtherWomen">How many other women he keeps — a wife, other lovers. She
        /// would know; a household has no secrets. 0 says nothing at all.</param>
        /// <param name="unnamedChildren">Children of theirs he has not owned before the world. The
        /// concrete object of the asking, when there is one (0 until the recognition road is built).</param>
        public static string BondSection(
            string playerName,
            LoverBond bond,
            bool ridesWithHim,
            bool boughtFromHerHouse,
            int hisOtherWomen = 0,
            int unnamedChildren = 0)
        {
            if (bond == LoverBond.None) return string.Empty;
            var who = string.IsNullOrWhiteSpace(playerName) ? "them" : playerName.Trim();
            var sb = new StringBuilder();

            if (bond == LoverBond.Former)
            {
                sb.Append($"What I was to {who}: his, once, and no vow anywhere said so. That is over now. ");
                sb.Append("It happened, and neither of us can unsay it; what I carry of it is my own affair.");
                if (boughtFromHerHouse)
                    sb.Append($" My house was paid for me, and that gold was never given back — whatever else changed, I did not go home.");
                return sb.ToString();
            }

            sb.Append($"What I am to {who}: his, and no vow anywhere says so. I am not his wife. ");
            sb.Append("His house does not carry my name, no word of us was said before anyone, and there is no ceremony ");
            sb.Append("holding this up — only him, and me, and whatever the two of us keep of each other.");

            if (boughtFromHerHouse)
                sb.Append(" My own house was paid for me, and I left it. That is not a thing a family forgets.");
            if (ridesWithHim)
                sb.Append(" I ride with him now, not as a hired sword and not as a wife — simply because I am his and he is where I am.");

            // The world's own position, stated as the world's. Note the grammar throughout: "it is
            // held", never "I feel". This is the air, not the person.
            sb.AppendLine();
            sb.Append("Such is the order of the world, and I know it as well as anyone: to be first and only ");
            sb.Append("is a woman's honor, and to be second, or to be hidden, is counted shame. That is what is held, ");
            sb.Append("everywhere, by all. What I myself make of it is mine alone — I may carry it lightly, or it may ");
            sb.Append("sit heavy on me, or I may not have settled it at all, and I am not obliged to have an answer ready.");

            if (hisOtherWomen > 0)
            {
                sb.AppendLine();
                sb.Append(hisOtherWomen == 1
                    ? "And I am not the only woman he keeps. I know it. "
                    : "And I am not the only woman he keeps, nor the second. I know it. ");
                sb.Append("Nobody had to tell me; a household has no secrets. What that is worth to me is my own.");
            }

            // What she could reach for, framed as a door and never as a duty. A lover who MUST push
            // is a quest-giver; a lover who MAY push is a person.
            sb.AppendLine();
            sb.Append("There are things I could ask of him, if I wanted them: that my place beside him be said out loud ");
            sb.Append("before the world instead of merely lived");
            if (unnamedChildren > 0)
                sb.Append(unnamedChildren == 1
                    ? ", and that our child be given his name openly, as his"
                    : ", and that our children be given his name openly, as his");
            sb.Append(". Whether I ask, how often, and whether I want any of it at all is mine to decide, ");
            sb.Append("and I need no one's leave for it either way.");

            return sb.ToString();
        }

        /// <summary>
        /// THE ANTI-PRETENCE RAIL, the lover's own twin of <see cref="CourtshipText.WordsDoNotWed"/>,
        /// and it exists for exactly the same reason (Steam, rmanicky, 2026.08.15): the moment a soul
        /// is told "not yet", the pretending starts — she will simply narrate the thing into being in
        /// her own words, and the player watches something happen that never happened. Rides the sheet
        /// while any lover's road is walked, the tool's own description in short, and every refused
        /// reach in full.
        /// </summary>
        public const string WordsDoNotBind =
            "And I hold this above any warm hour: no words of ours make me his. Not a promise breathed " +
            "between us, not a night we tell each other about, not a name we agree to call this by. It " +
            "is real only when I offer myself by my own hand and they take it by theirs. Until that is " +
            "done it has not happened — I do not say that it has, I do not let the talk drift as though " +
            "it had, and if the moment truly asks it of me I say so warmly and plainly: not like this, " +
            "and not in words alone.";

        // ------------------------- the order of the world -------------------------

        /// <summary>
        /// THE ERA NORM (2026.08.15, Anton: "тогава това е било нормалното… искам да влезнем в това
        /// време"). One short passage, carried by EVERY soul who lives in this world, in the era's
        /// own voice and with no sociology anywhere near it.
        ///
        /// READ THE GRAMMAR, because it is the entire design: every clause is "so it is HELD",
        /// never "so I feel". This is the air, not the person. It is what makes "they want children"
        /// true without a personality mandate, and what makes a second woman in the house mean
        /// something without the mod ever telling a wife to mind.
        ///
        /// The closing sentence is the one that keeps sixty women from becoming one woman, and it
        /// must never be trimmed for brevity: how a soul stands toward all this is HERS, and she is
        /// explicitly told she need not have settled it.
        /// </summary>
        public const string TheOrderOfTheWorld =
            "Such is the order of the world I live in, and everyone in it knows it as well as I do: a woman weds, " +
            "bears children, and the house she keeps is her honor. To be first and only is honor; to be second, or " +
            "to be hidden, is counted shame. A man's name is carried by the children he owns before the world, and " +
            "what he does not own before the world is not counted his, whatever everybody privately knows. So it is " +
            "held, everywhere, by all. " +
            "How I myself stand toward any of it is my own — I may hold it dearly, or chafe at it, or wear it as " +
            "armor, or simply never have settled the question — and I am not obliged to have an answer ready for " +
            "anyone who asks.";

        // ------------------------- refusals she reads (numberless) -------------------------

        /// <summary>What her own mind answers when the offer cannot be laid. Honest, hers, and never
        /// quoting a rail, a stage or a figure — the Sibuga floor lesson, which bites hardest here,
        /// since a heart reciting its own thresholds is the least human thing this mod could do.</summary>
        public static string TakeRefusal(LoverRoad.LoverVerdict verdict, string playerName)
        {
            var who = string.IsNullOrWhiteSpace(playerName) ? "them" : playerName.Trim();
            switch (verdict)
            {
                case LoverRoad.LoverVerdict.AlreadyHis:
                    return "I am his already. There is nothing here to give a second time, and I will not make a ceremony of saying so.";
                case LoverRoad.LoverVerdict.AlreadyWed:
                    return $"{who} has my hand. Whatever smaller thing the world might offer me, it has nothing to offer me now — and I would not be asked for one.";
                case LoverRoad.LoverVerdict.PromiseStands:
                    return "A promise stands between us, and that road ends in a wedding. I will not turn off it into something with no name — if my hand is to be given, let it be given properly.";
                case LoverRoad.LoverVerdict.RoadNotWalked:
                    return "My heart has not gone that far with them, and this is not a small step to take early. It has nothing holding it up — no vow, no house, no word said before anyone — so it can only be given from very deep, and I am not there.";
                case LoverRoad.LoverVerdict.HeartNotDeepEnough:
                    return "I care for them truly, and I will say so. But what is being reached for here has nothing holding it: no vow, no house, no name before the world, nothing at all but what I feel. For that I would have to love them past all sense — and in honesty I do not, not yet. I will not pretend a depth I have not got, least of all in this.";
                case LoverRoad.LoverVerdict.SheIsBound:
                    return "I am bound already in the world's eyes, and I do not break that. Whatever I feel, I am not free to give.";
                case LoverRoad.LoverVerdict.WorldRefuses:
                    return "The world would not have the two of us at all, whatever name we gave it. I will not build on ground that is not there.";
                default:
                    return "The moment does not allow it; I let the matter rest and stay in the talk.";
            }
        }

        /// <summary>The same refusal for the PLAYER's own log — plain, and deliberately allowed to
        /// say what her words never may. A rail nobody can see is indistinguishable from a broken
        /// mod, which is the whole lesson of 2026.08.15.</summary>
        public static string TakeRefusalForPlayer(LoverRoad.LoverVerdict verdict)
        {
            switch (verdict)
            {
                case LoverRoad.LoverVerdict.AlreadyHis: return "they are yours already";
                case LoverRoad.LoverVerdict.AlreadyWed: return "they are your wife";
                case LoverRoad.LoverVerdict.PromiseStands: return "a betrothal stands between you, and that road ends in a wedding";
                case LoverRoad.LoverVerdict.RoadNotWalked: return "their heart has not walked far enough with you for this";
                case LoverRoad.LoverVerdict.HeartNotDeepEnough: return "this asks more love than a marriage does, and theirs has not gone that deep";
                case LoverRoad.LoverVerdict.SheIsBound: return "they are bound to another in the world's eyes";
                default: return "the world does not allow it as things stand";
            }
        }

        // ------------------------- the head of the house, and her price -------------------------

        /// <summary>
        /// What the head of her house reads while the price of her leaving is being talked over —
        /// the blessing's SuitorTerms, reversed in every feeling. He is not blessing a match. A
        /// woman of his blood is leaving his house for a man who did not marry her, the world will
        /// hear of it within the month, and what is being settled is what that costs.
        /// </summary>
        public static string RansomTerms(string playerName, string kinName, string kinWord,
            int reckoning, int min, int max, bool haggle)
        {
            var who = string.IsNullOrWhiteSpace(playerName) ? "the traveler" : playerName.Trim();
            var her = string.IsNullOrWhiteSpace(kinName) ? "our kin" : kinName.Trim();
            var sb = new StringBuilder();

            sb.AppendLine($"The matter of {her}, {kinWord}, and {who} — who has not asked for her hand, and does not intend to:");
            sb.AppendLine($"- She means to leave my house for him. Not as a wife. There is no wedding in this, and there will be none; whatever is said, the world will call her what the world calls such women, and it will call my house something too.");
            sb.AppendLine($"- I cannot forbid a grown woman her own road. What I can do is set what her going costs him — the custom allows a house that much, and I would be a poor head of it if I let her walk out for nothing.");
            sb.AppendLine($"- What her leaving is worth by my own reckoning: {reckoning} denars.");
            if (haggle)
                sb.AppendLine($"- I will hear an argument between {min} and {max} denars, and not a coin outside it, however sweetly it is put. I never volunteer my lowest and I never name it as an offer.");
            else
                sb.AppendLine($"- The figure is {reckoning} denars and it does not move. I am not bargaining; I am telling him the price.");
            sb.AppendLine("- I lay this price ONLY once the matter itself has been openly spoken between us and a figure named and taken. Nothing is settled by my laying it — the gold and the choice are wholly his. If he lets it lie I do not press.");
            sb.AppendLine("- And I am not obliged to be pleasant about any of it. Gold settles what is owed to my house. It settles nothing else, and he should not mistake the one for the other.");
            return sb.ToString().TrimEnd();
        }

        /// <summary>The head of the house's own beat when the gold is taken — paid, and unappeased,
        /// which is the whole point of the transaction.</summary>
        public static string RansomTakenBeat(string playerName, string kinName, int price) =>
            $"{RansomMark} {playerName} paid me {price} denars for {kinName}, and she is gone out of my house to him "
            + "with no wedding in it. The debt to my house is settled. Nothing else between us is.";

        public static string RansomDeclinedBeat(string playerName, string kinName, int price) =>
            $"{RansomMark} I named what {kinName}'s leaving would cost — {price} denars — and {playerName} let it lie. "
            + "She stays under my roof, then, and the matter stands open.";

        /// <summary>What reaches HER, wherever she is standing, the day her own house is paid for
        /// her. She is told the fact and the figure and nothing about how to take it.</summary>
        public static string RansomNewsBeat(string playerName, string headOfHouse, int price, bool headIsFemale = false)
        {
            var head = string.IsNullOrWhiteSpace(headOfHouse) ? "the head of my house" : headOfHouse.Trim();
            var they = headIsFemale ? "she" : "he";
            return $"{RansomMark} Word came to me today: {playerName} paid {head} {price} denars for me, and {they} took it. "
                 + "I am out of my father's house. Whatever I am now, I am it away from them.";
        }

        // ------------------------- the beats of the bond itself -------------------------

        public static string BondSealedBeat(string playerName) =>
            $"{BondSealedMark} I gave myself to {playerName} this day — no vow between us, no word said before "
            + "anyone, nothing at all to hold it but the two of us. It is done, and I did it with my eyes open.";

        public static string BondDeclinedBeat(string playerName) =>
            $"{BondSealedMark} I offered {playerName} what I had to give, and they let it lie. I set that down here, "
            + "and I say nothing further of it.";

        public static string BondBlockedBeat(string playerName, string reason) =>
            $"{BondSealedMark} I reached for something with {playerName} and the world would not have it — {reason} "
            + "Nothing passed between us, whatever was said.";

        /// <summary>She ends it herself — the heart that was the whole of the bond has gone.</summary>
        public static string PartedByHerBeat(string playerName) =>
            $"{BondEndedMark} I have stepped away from what {playerName} and I were. There was never anything "
            + "holding it but what I felt, and I do not feel it any more. It happened; I do not unsay it. It is over.";

        /// <summary>He ends it — and she is told plainly, because she would know plainly.</summary>
        public static string SetAsideBeat(string playerName) =>
            $"{BondEndedMark} {playerName} has ended what was between us. There was nothing to break, which is "
            + "exactly what there was to break. I am not his any more.";

        /// <summary>What one of his OTHER women learns when he takes another — the leak's own words
        /// (the wiring is the leaks' job; the sentence is here so it is written once). She is told
        /// the fact, as she would hear it, and nothing about how to hold it.</summary>
        public static string OtherWomanLearnsBeat(string playerName, string herName, bool toAWife) =>
            toAWife
                ? $"{BondSealedMark} It came to me that {herName} is {playerName}'s now — his, in the way I am not "
                  + "the only one to be. My husband. I heard it as everyone hears such things."
                : $"{BondSealedMark} It came to me that {herName} is {playerName}'s now, as I am. I heard it as "
                  + "everyone hears such things.";

        // ------------------------- the player's own lines -------------------------

        /// <summary>The bond in plain words for the player's views — never a number.</summary>
        public static string StandingLine(LoverBond bond, HeartBand band)
        {
            switch (bond)
            {
                case LoverBond.Lover:
                    return $"yours, with nothing holding it but the heart — which stands at {HeartBands.Word(band)}";
                case LoverBond.Former:
                    return "once yours, and no longer";
                default:
                    return string.Empty;
            }
        }
    }
}
