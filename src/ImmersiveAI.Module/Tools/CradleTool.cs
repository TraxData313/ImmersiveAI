using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Births;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The cradle's hand — recall_birth: a soul who lived one of these days may call it back whole,
    /// however loosely they name it ("when our son was born", "Ira", "that day in Akkalat").
    ///
    /// THE PRIVACY RULE IS ENFORCED HERE, not in the prompt, and it is the wedding tool's rule moved
    /// one bond over: the HOUR of a birth belongs to the two who made the child. A witness who asks
    /// is given the feast and told plainly that the rest was never theirs. And the two parents are
    /// not given it the same way either — the mother reads her own memory back, while the father is
    /// given it as what she told him of it afterwards, which is the honest way a man comes to know
    /// an hour he may not have been in the room for.
    /// </summary>
    public static class CradleTool
    {
        public const string RecallBirth = "recall_birth";

        public static readonly ToolDefinition Tool = new ToolDefinition(RecallBirth,
            "Call back, whole, the day a child of ours came into the world — the place and the " +
            "hour, who stood at the feast, the name and who said it first, and (if the child is " +
            "my own) the hour of the birth itself, which belongs to its two parents. Name it " +
            "however it comes to me ('when our son was born', a child's name, a place), or leave " +
            "the name out for my youngest. I reach for this whenever that day is spoken of and I " +
            "would tell it truly rather than in a haze.",
            new[] { new ToolParameter("birth",
                "The day I mean — a child's name, a place, 'our first'. Leave it out for my youngest.",
                required: false) });

        /// <summary>Answers one recall against what this soul truly lived. The ledger lives on the
        /// game thread's side; the read is marshaled there like every other recall.</summary>
        public static async Task<string> ResolveAsync(ToolCall call, Hero asker, Func<BirthLedger?> ledgerNow)
        {
            string query = string.Empty;
            try
            {
                var args = JObject.Parse(call.ArgumentsJson);
                query = ((string?)args["birth"] ?? string.Empty).Trim();
            }
            catch { /* no name asked = their youngest */ }

            var lookup = OnGameThread(() => Answer(query, asker, ledgerNow()));
            var finished = await Task.WhenAny(lookup, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false);
            if (finished != lookup) return ToolLoopRunner.NothingSurfaces;

            var answer = await lookup.ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(answer) ? ToolLoopRunner.NothingSurfaces : answer;
        }

        private static string Answer(string query, Hero asker, BirthLedger? ledger)
        {
            if (ledger == null || asker == null) return string.Empty;

            // CONTENT, never existence: a child whose day the chronicler has not written yet is
            // not a day to be told. Without this filter the loose finder's own fallback ("my
            // youngest", when nothing matches) would happily hand back a bare header for a birth
            // that happened four minutes ago and has no words yet (2026.08.10 review).
            var lived = ledger.SharedWith(asker.StringId).Where(IsWritten).ToList();
            if (lived.Count == 0)
                return "I search my memory, but no such day is set down in it.";

            var record = ledger.Find(query, asker.StringId);
            if (record != null && !IsWritten(record)) record = lived[lived.Count - 1];
            if (record == null)
                return "No such day returns to me. " + Roll(lived);

            bool mine = record.IsParent(asker.StringId);
            bool hers = string.Equals(record.MotherId, asker.StringId, StringComparison.Ordinal);

            var sb = new StringBuilder();
            sb.AppendLine(mine
                ? "It returns to me whole, as though it were happening again — my own child:"
                : "It returns to me whole, as the day itself:");
            sb.AppendLine(BirthText.FullAccount(record, includeHour: mine, asMother: hers));

            if (lived.Count > 1)
            {
                sb.AppendLine();
                sb.AppendLine(Roll(lived));
            }
            return sb.ToString().TrimEnd();
        }

        private static bool IsWritten(BirthRecord? record) =>
            record != null && (!string.IsNullOrWhiteSpace(record.BirthAccount)
                            || !string.IsNullOrWhiteSpace(record.FeastAccount));

        private static string Roll(IReadOnlyList<BirthRecord> lived)
        {
            var sb = new StringBuilder();
            sb.AppendLine("The children whose coming I saw, oldest to youngest:");
            foreach (var record in lived)
                sb.AppendLine("- " + BirthText.RollEntry(record));
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
