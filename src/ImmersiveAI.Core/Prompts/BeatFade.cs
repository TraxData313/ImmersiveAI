using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Births;
using ImmersiveAI.Core.Weddings;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// HOW A GREAT DAY FADES (2026.08.11, Anton's design — "съобщенята за сватба и раждане са
    /// големи и искам когато бъдат подминати от 7 съобщения да фейднат... а над 14 вече само
    /// заглавието, а NPC-тата винаги могат да ползват тууловете да се сетят за тях").
    ///
    /// The wedding day and a child's feast land in memory WHOLE and stay whole while they are
    /// fresh — that was his call and it is the right one: the account is what a soul greets you
    /// with the next time you meet, and for a lord you have exchanged three words with it is the
    /// only thing he has of you. But an account of a thousand characters riding in the verbatim
    /// window forever, in up to sixty souls, is the single heaviest thing this mod puts in a
    /// prompt. So it thins with distance, exactly as a memory does:
    ///
    ///   • within <see cref="WholeWithin"/> turns — WHOLE, as it was written;
    ///   • out to <see cref="TitleBeyond"/> — the opening of it, and the honest note that the rest
    ///     has settled;
    ///   • beyond that — the day itself and nothing more: that it happened, where, and to whom.
    ///
    /// NOTHING IS REWRITTEN. This is a RENDER-time thinning of what the prompt carries; the turn in
    /// memories.json keeps every word it was born with, the ledgers keep the accounts forever, and
    /// `recall_wedding` / `recall_birth` read the LEDGER and not the memory — so a soul asked about
    /// that day still answers with the whole of it, at any distance. The faded lines say so in her
    /// own voice, which is also what makes her reach for the tool.
    /// </summary>
    public static class BeatFade
    {
        /// <summary>Turns back within which a great day is still carried whole.</summary>
        public const int WholeWithin = 7;

        /// <summary>Beyond this many turns back only the day itself remains in the prompt.</summary>
        public const int TitleBeyond = 14;

        /// <summary>How many sentences of the account survive in the middle distance.</summary>
        public const int FadedSentences = 2;

        internal const string SettledTail =
            "The rest of it has settled the way a day settles, though I can call the whole of it back to mind if it is spoken of.";

        internal const string KeptTail =
            "I keep the whole of that day in me, and can call it back to mind if it is spoken of.";

        /// <summary>
        /// The recorded line as the prompt should carry it, given how far back it now stands
        /// (0 = the most recent turn). Anything that is not one of the great accounts is returned
        /// untouched — this thins the two heaviest beats in the mod and nothing else.
        /// </summary>
        public static string Fade(string? line, int turnsBack)
        {
            if (string.IsNullOrWhiteSpace(line)) return line ?? string.Empty;
            if (turnsBack <= WholeWithin) return line!;
            if (!TrySplit(line!, out var frame, out var mark, out var account)) return line!;
            if (string.IsNullOrWhiteSpace(account)) return line!;

            var head = string.IsNullOrWhiteSpace(frame) ? string.Empty : frame.Trim() + " ";

            if (turnsBack > TitleBeyond)
                return (head + KeptTail).Trim();

            var opening = FirstSentences(account, FadedSentences);
            if (string.IsNullOrWhiteSpace(opening)) return (head + KeptTail).Trim();
            return (head + mark + " " + opening + " " + SettledTail).Trim();
        }

        /// <summary>Whether this recorded line is one of the great accounts that thins with time —
        /// public so a caller can skip the work entirely for the ordinary run of turns.</summary>
        public static bool IsGreatAccount(string? line) =>
            !string.IsNullOrWhiteSpace(line)
            && (WeddingText.IsDayBeat(line) || WeddingText.IsNightBeat(line)
                || BirthText.IsHourBeat(line) || BirthText.IsFeastBeat(line));

        // Both chronicles already know how to cut their own beats apart; this only has to work out
        // WHICH mark was used, so the faded middle form can keep it.
        private static bool TrySplit(string line, out string frame, out string mark, out string account)
        {
            frame = mark = account = string.Empty;

            if (WeddingText.TrySplitBeat(line, out var wFrame, out var wAccount))
            {
                frame = wFrame;
                account = wAccount;
                mark = WeddingText.IsNightBeat(line) ? WeddingText.NightAccountMark : WeddingText.DayAccountMark;
                return true;
            }
            if (BirthText.TrySplitBeat(line, out var bFrame, out var bAccount))
            {
                frame = bFrame;
                account = bAccount;
                mark = BirthText.IsHourBeat(line) ? BirthText.HourAccountMark : BirthText.FeastAccountMark;
                return true;
            }
            return false;
        }

        // The first whole sentences of an account, never a severed one — the same courtesy the
        // memory writer gets from TrimToLastCompleteSentence.
        private static string FirstSentences(string text, int count)
        {
            var trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0 || count <= 0) return string.Empty;

            int taken = 0;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c != '.' && c != '!' && c != '?' && c != '…') continue;
                // Walk past a run of closing punctuation and quotes so "…" or `.\"` counts once.
                int end = i;
                while (end + 1 < trimmed.Length &&
                       (trimmed[end + 1] == '.' || trimmed[end + 1] == '"' || trimmed[end + 1] == '”'
                        || trimmed[end + 1] == '»' || trimmed[end + 1] == '\'')) end++;
                taken++;
                i = end;
                if (taken >= count) return trimmed.Substring(0, end + 1).Trim();
            }
            return trimmed;   // fewer sentences than asked for: it is already short
        }
    }
}
