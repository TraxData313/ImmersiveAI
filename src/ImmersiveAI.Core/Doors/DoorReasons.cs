using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Courtship;

namespace ImmersiveAI.Core.Doors
{
    /// <summary>Why the night is not open to him tonight — and there are only three answers.</summary>
    public enum DoorStanding
    {
        /// <summary>Open.</summary>
        Open = 0,
        /// <summary>Her body's own season — the one refusal that was always here, and the only one
        /// the nights ever had before this. Not a wound and never treated as one.</summary>
        HerSeason = 1,
        /// <summary>She has written down why, and it stands unanswered.</summary>
        HerWord = 2,
        /// <summary>Nothing is written; the feeling is simply gone. Told plainly rather than dressed
        /// as a grievance she never actually made — a fabricated reason would be the mod putting
        /// words in her mouth, which is the one thing it does not do.</summary>
        Coldness = 3,
    }

    /// <summary>
    /// THE DOORS (2026.08.15, Anton's design — and this section of the design record deliberately
    /// SUPERSEDES a settled rule). The nights record says refusals were cut and the only refusal is
    /// the days of her custom: "do not reintroduce her saying no without asking." He has now asked.
    ///
    /// From here a wife's or a lover's door may be CLOSED, and the closure always carries HER OWN
    /// WRITTEN REASONS. That last clause is the whole feature. A door that closes for reasons the
    /// player cannot read is a random number generator wearing a woman's face; a door that closes
    /// with "you gave the jewel to Ira in front of the whole house, and I heard about it before you
    /// told me" is a marriage.
    ///
    /// The ops below are the misgivings' own, verb for verb — set down, settle, release, revise,
    /// reopen — because the design record says to apply that model rather than invent a second
    /// shape, and because a player who has learned one of these has learned both.
    /// </summary>
    public static class DoorReasons
    {
        /// <summary>How many of her reasons may stand OPEN at once. It bounds what is unanswered,
        /// never what she is allowed to feel: settled ones never keep her from writing a fresh one,
        /// exactly as the misgivings' own cap works.</summary>
        public const int MaxStandingOpen = 5;

        /// <summary>Past this many kept in all, the oldest SETTLED ones fade. What still stands is
        /// never dropped — a wound nobody answered does not go away because time passed.</summary>
        public const int MaxKept = 12;

        public static IEnumerable<DoorReason> Standing(IEnumerable<DoorReason>? reasons) =>
            (reasons ?? Enumerable.Empty<DoorReason>()).Where(r => r != null && r.Stands);

        public static int OpenCount(IEnumerable<DoorReason>? reasons) => Standing(reasons).Count();

        public static int TotalCount(IEnumerable<DoorReason>? reasons) =>
            (reasons ?? Enumerable.Empty<DoorReason>()).Count(r => r != null && !string.IsNullOrWhiteSpace(r.Text));

        /// <summary>Whether anything of hers stands unanswered.</summary>
        public static bool AnyStanding(IEnumerable<DoorReason>? reasons) => Standing(reasons).Any();

        /// <summary>
        /// Where the door stands, all told. The season comes first because it is not a wound and
        /// must never be read as one — a wife in her custom days whose heart is perfectly whole
        /// would otherwise be reported as cold, which is both false and cruel.
        /// </summary>
        public static DoorStanding StandingOf(
            IEnumerable<DoorReason>? reasons, bool herSeasonIsUpon, HeartBand band, BondKind bond)
        {
            if (herSeasonIsUpon) return DoorStanding.HerSeason;
            if (AnyStanding(reasons)) return DoorStanding.HerWord;
            if (!HeartBands.OpensTo(band, bond)) return DoorStanding.Coldness;
            return DoorStanding.Open;
        }

        // ------------------------- her own hand -------------------------

        /// <summary>
        /// She writes one down. Refused when her list is already as full of unanswered things as it
        /// gets, and when the same thing is already standing — a doubled grievance is the model
        /// losing its grip, not a woman feeling twice as much.
        /// </summary>
        public static bool SetDown(List<DoorReason> reasons, string text, string opens, double day,
            out string trouble, bool byTheWorld = false)
        {
            trouble = string.Empty;
            if (reasons == null) { trouble = "there is nowhere to set it down"; return false; }
            var body = Tidy(text);
            if (body.Length == 0) { trouble = "no words were given for it"; return false; }

            // THE STRICT MATCHER HERE, NEVER THE LENIENT ONE. Settling asks "which of these did she
            // mean?" and must be generous; setting down asks "is this the same thing again?" and
            // must not be — two truly different wounds in the same register share plenty of words,
            // and swallowing a real new one is much the worse mistake. Caught by its own test: five
            // separate grievances phrased alike collapsed into one.
            if (Text.LooseMatch.Restates(Standing(reasons).Select(r => r.Text), body))
            {
                trouble = "that one already stands";
                return false;
            }
            if (OpenCount(reasons) >= MaxStandingOpen)
            {
                trouble = "too much already stands unanswered between them";
                return false;
            }

            reasons.Add(new DoorReason
            {
                Text = body,
                Opens = Tidy(opens),
                SetDownDay = day,
                LaidByTheWorld = byTheWorld,
            });
            Prune(reasons);
            return true;
        }

        /// <summary>She lays one to rest, with her light word on what answered it. HER judgment and
        /// nothing else — there is no gold price, no timer, and no way for the player to do this.</summary>
        public static bool Settle(List<DoorReason> reasons, string which, string note, double day,
            out string trouble)
        {
            trouble = string.Empty;
            var found = Find(reasons, which, standingOnly: true);
            if (found == null)
            {
                trouble = OpenCount(reasons) == 0
                    ? "nothing of hers stands open just now"
                    : "no standing reason of hers matches those words";
                return false;
            }
            found.Settled = true;
            found.SettledNote = Tidy(note);
            found.SettledDay = day;
            Prune(reasons);
            return true;
        }

        /// <summary>She strikes one out — it was never really the thing, or it has stopped being
        /// true. No note: releasing is not forgiving, it is withdrawing.</summary>
        public static bool Release(List<DoorReason> reasons, string which, out string trouble)
        {
            trouble = string.Empty;
            if (reasons == null) { trouble = "there is nothing of hers to strike out"; return false; }
            var found = Find(reasons, which, standingOnly: false);
            if (found == null) { trouble = "no reason of hers matches those words"; return false; }
            reasons.Remove(found);
            return true;
        }

        /// <summary>
        /// She rewords one — the thing under it has changed shape, which is what happens to a
        /// grievance that gets talked about without being resolved.
        ///
        /// <paramref name="which"/> names the one she means and <paramref name="text"/> is its NEW
        /// wording, and they are two different things: handed the same string for both (as the
        /// resolver did until 2026.08.16) this can only ever rewrite something to itself and then
        /// report success, which is the one shape of failure this whole system cannot afford — a
        /// silent lie about what she just decided. It also refuses a revise that would change
        /// NOTHING, for the same reason the misgivings' own revise never blanks: "I have reworded
        /// it" over an untouched line is that same lie wearing a politer face.
        /// </summary>
        public static bool Revise(List<DoorReason> reasons, string which, string text, string opens,
            out string trouble)
        {
            trouble = string.Empty;
            var body = Tidy(text);
            var road = Tidy(opens);
            if (body.Length == 0 && road.Length == 0)
            {
                trouble = "no new words were given for it";
                return false;
            }
            var found = Find(reasons, which, standingOnly: false);
            if (found == null) { trouble = "no reason of hers matches those words"; return false; }
            if (body.Length > 0) found.Text = body;
            if (road.Length > 0) found.Opens = road;
            return true;
        }

        /// <summary>She takes one back up. Whatever settled it did not settle it after all, which is
        /// a real thing that happens and the reason the list is not a one-way ratchet.</summary>
        public static bool Reopen(List<DoorReason> reasons, string which, double day, out string trouble)
        {
            trouble = string.Empty;
            var found = Find(reasons, which, standingOnly: false);
            if (found == null) { trouble = "no reason of hers matches those words"; return false; }
            if (found.Stands) { trouble = "that one already stands"; return false; }
            if (OpenCount(reasons) >= MaxStandingOpen)
            {
                trouble = "too much already stands unanswered between them";
                return false;
            }
            found.Settled = false;
            found.SettledNote = string.Empty;
            found.SettledDay = -1;
            found.SetDownDay = day;
            return true;
        }

        // ------------------------- the world's own hand -------------------------

        /// <summary>
        /// THE SPIRAL, and it is code and not prose (2026.08.15). A duty night lays one of these
        /// down whether or not she is in a talk, whether or not a tool slot is free, and whether or
        /// not the model felt like reaching for one.
        ///
        /// This is not the mod telling her how to feel — it is the mod recording what happened, in
        /// the flattest words it has, exactly as the duty night's own beat does. What she makes of
        /// it is hers, and she may settle it by her own judgment like anything else. But it will be
        /// THERE, and each one makes the road back longer, which is the entire moral of the design:
        /// a duty night that costs nothing turns the whole thing into a farm.
        ///
        /// When her list is already as full as it gets, nothing new is added — the closure is
        /// already as deep as this mechanism goes, and piling on would only be noise.
        /// </summary>
        public static bool LayDownByTheWorld(List<DoorReason> reasons, string text, string opens, double day)
        {
            return SetDown(reasons, text, opens, day, out _, byTheWorld: true);
        }

        /// <summary>How many times the world has laid something down here — what the player is shown
        /// as the plain count of a thing that keeps happening.</summary>
        public static int LaidByTheWorldCount(IEnumerable<DoorReason>? reasons) =>
            (reasons ?? Enumerable.Empty<DoorReason>()).Count(r => r != null && r.LaidByTheWorld);

        // ------------------------- matching, lenient on purpose -------------------------

        /// <summary>
        /// Finds the reason she means — through <see cref="Text.LooseMatch"/>, which the misgivings
        /// also use and which carries three lessons paid for by live probes, the most important
        /// being that an INFLECTED tongue must match itself. Anton plays in Bulgarian; a matcher
        /// that compared words letter for letter would make the settle verb silently do nothing
        /// there, which is this system's worst possible failure — a woman who forgave and was not
        /// recorded as forgiving.
        /// </summary>
        public static DoorReason? Find(IEnumerable<DoorReason>? reasons, string? which, bool standingOnly)
        {
            var pool = (reasons ?? Enumerable.Empty<DoorReason>())
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Text))
                .Where(r => !standingOnly || r.Stands)
                .ToList();
            if (pool.Count == 0) return null;

            // No words at all, and only one thing it could be: take it. A model that says "settle
            // it" with a single thing standing has been perfectly clear.
            if (string.IsNullOrWhiteSpace(which)) return pool.Count == 1 ? pool[0] : null;

            return Text.LooseMatch.Best(pool, r => r.Text, which);
        }

        /// <summary>
        /// One of the five deeds, from whatever the model actually said; empty when nothing close
        /// came. The schema enum is the first defence and this is the second — the misgivings'
        /// exact lesson, where a soul laid three doubts to rest under the word "resolve" and every
        /// one fell into a do-nothing branch.
        ///
        /// IT IS NOT <see cref="Courtship.CourtshipMisgivings.CanonicalAction"/>, and must never be
        /// replaced by it, because two of its words mean the OPPOSITE thing here. For a misgiving,
        /// "close" and "closed" mean laying the matter to rest. For a DOOR they plainly mean
        /// shutting it. Sharing that table would have made a woman forgive at the exact moment she
        /// meant to take offence — the worst single bug this feature could carry, and silent.
        /// </summary>
        public static string CanonicalDeed(string? raw)
        {
            var a = (raw ?? string.Empty).Trim().ToLowerInvariant()
                .Replace('-', '_').Replace(' ', '_').Trim('_', '.', '"', '\'');
            if (a.Length == 0) return string.Empty;
            switch (a)
            {
                case ActSetDown:
                case "set":
                case "setdown":
                case "write":
                case "write_down":
                case "record":
                case "note":
                case "add":
                case "new":
                case "raise":
                // The collision, resolved the way a reader of THIS system would expect.
                case "close":
                case "closed":
                case "shut":
                case "shut_door":
                case "close_door":
                    return ActSetDown;

                case ActSettle:
                case "settled":
                case "resolve":
                case "resolved":
                case "answer":
                case "answered":
                case "lay_to_rest":
                case "laytorest":
                case "rest":
                case "forgive":
                case "forgiven":
                case "satisfy":
                case "satisfied":
                case "open":
                case "open_door":
                    return ActSettle;

                case ActRelease:
                case "released":
                case "strike":
                case "strike_out":
                case "strikeout":
                case "remove":
                case "delete":
                case "drop":
                case "discard":
                case "withdraw":
                case "retract":
                    return ActRelease;

                case ActRevise:
                case "revised":
                case "reword":
                case "rewrite":
                case "edit":
                case "update":
                case "amend":
                case "change":
                    return ActRevise;

                case ActReopen:
                case "reopened":
                case "unsettle":
                case "restore":
                case "revive":
                case "take_up":
                case "takeup":
                    return ActReopen;

                default:
                    return string.Empty;
            }
        }

        public const string ActSetDown = "set_down";
        public const string ActSettle = "settle";
        public const string ActRelease = "release";
        public const string ActRevise = "revise";
        public const string ActReopen = "reopen";

        /// <summary>
        /// Whether the two hands came in swapped — the matter written into her light word and the
        /// word into the matter's place. Caught live on the misgivings' own settle verb, on every
        /// single call a model made: it put the ANSWER in the matter's field and the matter in the
        /// note, so nothing matched and nothing was ever laid to rest. Named fields are the first
        /// fix; this is the one that holds when a model reads them its own way anyway. Deliberately
        /// narrow — it swaps ONLY when what came as the matter matches nothing and what came as the
        /// note truly names one.
        /// </summary>
        public static bool HandsCameSwapped(IEnumerable<DoorReason>? reasons, string? matter, string? note,
            bool standingOnly = true) =>
            !string.IsNullOrWhiteSpace(note)
            && Find(reasons, matter, standingOnly) == null
            && Find(reasons, note, standingOnly) != null;

        private static string Tidy(string? text)
        {
            var t = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t.Length <= 400 ? t : t.Substring(0, 400).TrimEnd();
        }

        /// <summary>Past the ceiling the oldest SETTLED ones fade. What still stands never does.</summary>
        public static void Prune(List<DoorReason> reasons)
        {
            if (reasons == null) return;
            while (reasons.Count > MaxKept)
            {
                var oldest = reasons.Where(r => r != null && r.Settled)
                    .OrderBy(r => r.SettledDay).ThenBy(r => r.SetDownDay).FirstOrDefault();
                if (oldest == null) return;   // nothing but standing wounds; they all stay
                reasons.Remove(oldest);
            }
        }
    }
}
