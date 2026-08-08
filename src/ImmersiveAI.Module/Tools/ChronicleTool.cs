using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Battles;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The chronicle's hand — recall_battle: an NPC who stood in battles beside the player may call
    /// any of them back whole, by the loose way people name them ("the storming of Varcheg", just
    /// "Ortysia", "our last battle"), or run through the roll of what they shared. The answer is the
    /// record's own account plus their personal line in it (their hand's work, how they came out),
    /// phrased as remembrance — so old battles are discussed from the record, never from fog. Only
    /// offered to souls who truly have a shared battle to recall (a lean tool list keeps tools used).
    /// </summary>
    public static class ChronicleTool
    {
        public const string RecallBattle = "recall_battle";

        public static readonly ToolDefinition Tool = new ToolDefinition(RecallBattle,
            "Call back, whole, a battle I lived through at their side — the field and the hour, the " +
            "musters and the odds, the cost, prisoners and spoils, and whose hand did what — by the " +
            "name the chronicle keeps or any word of it ('the storming of Varcheg', 'Ortysia', 'our " +
            "last battle'). Leave the name out to run through the roll of every battle we have " +
            "shared. Reach for this whenever a battle we fought is spoken of and my memory of its " +
            "numbers is dim — the record is truer than fog.",
            new[] { new ToolParameter("battle",
                "The battle's name, or any word of it — the place, the foe, 'last'. Leave it out for the whole roll.",
                required: false) });

        /// <summary>Answers one recall against the asker's shared battles. The ledger lives on the
        /// game thread's side; reads are marshaled there like every other recall.</summary>
        public static async Task<string> ResolveAsync(ToolCall call, Hero asker, Func<BattleLedger?> ledgerNow)
        {
            string query = string.Empty;
            try
            {
                var args = JObject.Parse(call.ArgumentsJson);
                query = ((string?)args["battle"] ?? string.Empty).Trim();
            }
            catch { /* no name asked = the roll */ }

            var lookup = OnGameThread(() => Answer(query, asker, ledgerNow()));
            var finished = await Task.WhenAny(lookup, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false);
            if (finished != lookup) return ToolLoopRunner.NothingSurfaces;

            var answer = await lookup.ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(answer) ? ToolLoopRunner.NothingSurfaces : answer;
        }

        private static string Answer(string query, Hero asker, BattleLedger? ledger)
        {
            if (ledger == null || asker == null) return string.Empty;
            var shared = ledger.SharedWith(asker.StringId);
            if (shared.Count == 0)
                return "I search my memory, but no battle at their side is set down in it.";

            if (string.IsNullOrWhiteSpace(query))
                return Roll(shared);

            var record = ledger.Find(query, asker.StringId);
            if (record == null)
                return "No battle by that name returns to me. " + Roll(shared);

            var sb = new StringBuilder();
            sb.AppendLine("It returns to me whole, as the day itself:");
            sb.AppendLine(record.FullTale?.Trim().Length > 0 ? record.FullTale : BattleText.FullAccount(record));

            var mine = record.ParticipantById(asker.StringId);
            if (mine != null)
            {
                var my = new StringBuilder("And my own part in it: ");
                if (mine.Downs > 0) my.Append($"{mine.Downs} fell to my hand");
                else if (mine.Downs == 0) my.Append("none fell to my hand — my part lay elsewhere");
                else my.Append("of my own hand no tally was kept");
                switch (mine.State)
                {
                    case BattleParticipant.States.Wounded: my.Append("; I came out of it wounded."); break;
                    case BattleParticipant.States.Captured: my.Append("; the day ended with me a captive."); break;
                    default: my.Append("; I came through unhurt."); break;
                }
                sb.AppendLine(my.ToString());
            }
            return sb.ToString().TrimEnd();
        }

        // The roll of shared battles, newest last — titles, days, and the short tales.
        private static string Roll(System.Collections.Generic.IReadOnlyList<BattleRecord> shared)
        {
            var sb = new StringBuilder();
            sb.AppendLine("The roll of battles we have shared, oldest to newest:");
            foreach (var r in shared.Skip(Math.Max(0, shared.Count - 12)))
                sb.AppendLine("- " + BattleText.RollEntry(r));
            if (shared.Count > 12) sb.AppendLine($"(and {shared.Count - 12} older still, gone dim but kept in the chronicle)");
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
