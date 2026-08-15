using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Weddings;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The wedding's hand — recall_wedding: a soul who stood at one of the player's weddings may
    /// call that day back WHOLE, however loosely they name it ("our wedding", "that day in Onira",
    /// "the day you were wed"). The record answers with the day's own written account, and — for
    /// the one who was wed — with the night that followed.
    ///
    /// THE PRIVACY RULE IS ENFORCED HERE, not in the prompt: only the spouse is ever handed the
    /// second part. A witness who asks receives the day and is told plainly that the rest was never
    /// theirs to know. A soul who stood at no wedding never sees the tool at all.
    /// </summary>
    public static class NuptialTool
    {
        public const string RecallWedding = "recall_wedding";

        public static readonly ToolDefinition Tool = new ToolDefinition(RecallWedding,
            "Call back, whole, a wedding day I lived through — the place and the hour, who stood " +
            "there, what was given and said, and (if the wedding was my own) the night that " +
            "followed it, which is mine alone. Name it however it comes to me ('our wedding', " +
            "'that day in Onira'), or leave the name out for my own. I reach for this whenever " +
            "that day is spoken of and I would tell it truly rather than in a haze — some days " +
            "deserve to be remembered exactly as they were.",
            new[] { new ToolParameter("wedding",
                "The day I mean — 'ours', a place, a name. Leave it out for my own wedding day.",
                required: false) });

        /// <summary>Answers one recall against what this soul truly lived. The ledger lives on the
        /// game thread's side; the read is marshaled there like every other recall.</summary>
        public static async Task<string> ResolveAsync(ToolCall call, Hero asker, Func<WeddingLedger?> ledgerNow)
        {
            string query = string.Empty;
            try
            {
                var args = JObject.Parse(call.ArgumentsJson);
                query = ((string?)args["wedding"] ?? string.Empty).Trim();
            }
            catch { /* no name asked = their own day */ }

            var lookup = OnGameThread(() => Answer(query, asker, ledgerNow()));
            var finished = await Task.WhenAny(lookup, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false);
            if (finished != lookup) return ToolLoopRunner.NothingSurfaces;

            var answer = await lookup.ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(answer) ? ToolLoopRunner.NothingSurfaces : answer;
        }

        private static string Answer(string query, Hero asker, WeddingLedger? ledger)
        {
            if (ledger == null || asker == null) return string.Empty;
            var stood = ledger.SharedWith(asker.StringId);
            if (stood.Count == 0)
                return "I search my memory, but no wedding day of theirs is set down in it.";

            var record = ledger.Find(query, asker.StringId);
            if (record == null)
                return "No wedding by that name returns to me. " + Roll(stood);

            bool mine = record.IsSpouse(asker.StringId);
            var sb = new StringBuilder();
            sb.AppendLine(mine
                ? "It returns to me whole, as though I stood in it again — our own wedding day:"
                : "It returns to me whole, as the day itself:");
            sb.AppendLine(WeddingText.FullAccount(record, includeNight: mine, CalradiaYears.Since(record.GameDay)));

            if (stood.Count > 1)
            {
                sb.AppendLine();
                sb.AppendLine(Roll(stood));
            }
            return sb.ToString().TrimEnd();
        }

        private static string Roll(IReadOnlyList<WeddingRecord> stood)
        {
            var sb = new StringBuilder();
            sb.AppendLine("The wedding days I stood at, oldest to newest:");
            foreach (var record in stood)
                sb.AppendLine("- " + WeddingText.RollEntry(record));
            return sb.ToString().TrimEnd();
        }

        private static Task<string> OnGameThread(Func<string> read)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            MainThreadDispatcher.Enqueue(() =>
            {
                try { tcs.TrySetResult(read()); }
                catch { tcs.TrySetResult(string.Empty); }
            });
            return tcs.Task;
        }
    }
}
