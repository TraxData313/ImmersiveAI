using System;
using System.Collections.Generic;
using ImmersiveAI.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Narrates the trouble the speaker themselves carries — the issue the game has laid on them
    /// (the very matter a player is sent to resolve) and any quest they have already given — so a
    /// villager asked "what ails you?" truly knows his own problem instead of inventing one.
    /// Rendered in the same gentle second person as the rest of the situation, using the issue's
    /// own words (the brief and the asked-for remedy are written first person by the giver, so
    /// they quote naturally as "this is how you tell it").
    ///
    /// Everything is best-effort: a missing or throwing game datum costs only its own sentence,
    /// and a hero with no trouble simply contributes nothing.
    /// </summary>
    public static class TroubleBuilder
    {
        /// <summary>The speaker's own trouble and given quests as a flowing paragraph, or empty
        /// when nothing weighs on them. <paramref name="partner"/> only shapes the phrasing (the
        /// taker of a quest is always the player, named outright even when speaking to another).</summary>
        public static string Build(Hero speaker, Hero partner)
        {
            try { return BuildInner(speaker); }
            catch { return string.Empty; }
        }

        private static string BuildInner(Hero speaker)
        {
            if (speaker == null || Campaign.Current == null) return string.Empty;

            var sentences = new List<string>();
            IssueBase issue = null;
            Try(() =>
            {
                var issues = Campaign.Current.IssueManager?.Issues;
                if (issues != null) issues.TryGetValue(speaker, out issue);
            });

            if (issue != null)
                DescribeOwnIssue(issue, sentences);

            // Quests they gave that ride on without an issue behind them (a lord's charge, a story
            // quest) — the issue's own quest is already told above, so it is not repeated here.
            Try(() => DescribeGivenQuests(speaker, issue, sentences));

            return sentences.Count == 0 ? string.Empty : string.Join(" ", sentences);
        }

        // The trouble itself, in the giver's own words, and where its resolving presently stands.
        private static void DescribeOwnIssue(IssueBase issue, List<string> sentences)
        {
            string title = null, brief = null, ask = null;
            Try(() => title = TidingsFormatter.StripMarkup(issue.Title?.ToString()));
            Try(() => brief = TidingsFormatter.StripMarkup(issue.IssueBriefByIssueGiver?.ToString()));
            Try(() => ask = TidingsFormatter.StripMarkup(issue.IssueQuestSolutionExplanationByIssueGiver?.ToString()));

            sentences.Add(string.IsNullOrWhiteSpace(title)
                ? "A trouble weighs on you in these days."
                : $"A trouble weighs on you in these days — the matter of “{title.TrimEnd('.')}”.");

            if (!string.IsNullOrWhiteSpace(brief))
                sentences.Add($"When any ask after it, this is how you tell it: “{brief}”");

            var player = Hero.MainHero?.Name?.ToString() ?? "someone";

            if (issue.IsSolvingWithQuest)
            {
                sentences.Add($"{player} has taken this burden up at your asking.");
                if (!string.IsNullOrWhiteSpace(ask))
                    sentences.Add($"What you asked of them, in your own words: “{ask}”");
                Try(() => DescribeQuestProgress(issue.IssueQuest, sentences));
            }
            else if (issue.IsSolvingWithAlternative)
            {
                sentences.Add($"{player} has sent trusted people with a company of men to see it done for you; you await word of how they fare.");
            }
            else if (issue.IsSolvingWithLordSolution)
            {
                sentences.Add("The matter has been laid in a lord's hands to resolve, and you await their justice.");
            }
            else
            {
                sentences.Add("No one has yet taken this burden from you.");
                if (!string.IsNullOrWhiteSpace(ask))
                    sentences.Add($"Were one willing and able to see it done, this is what you would ask of them: “{ask}”");
            }
        }

        // How the taken-up quest fares: the last words of its journal, and the time it has left.
        private static void DescribeQuestProgress(QuestBase quest, List<string> sentences)
        {
            if (quest == null || !quest.IsOngoing) return;

            Try(() =>
            {
                var latest = LatestJournalLine(quest);
                if (latest.Length > 0)
                    sentences.Add($"The last word of how it fares: {latest}");
            });

            Try(() =>
            {
                if (quest.IsRemainingTimeHidden) return;
                var remaining = quest.QuestDueTime - CampaignTime.Now;
                double days = remaining.ToDays;
                if (days <= 0 || days > 500) return; // lapsed, or so distant it does not press
                sentences.Add(days < 1.5
                    ? "The time for it is nearly spent."
                    : $"Some {(int)Math.Round(days)} days remain before the chance is lost.");
            });
        }

        // Quests this hero gave that are not the issue's own — each named with its latest word.
        private static void DescribeGivenQuests(Hero speaker, IssueBase ownIssue, List<string> sentences)
        {
            var quests = Campaign.Current.QuestManager?.Quests;
            if (quests == null) return;

            var player = Hero.MainHero?.Name?.ToString() ?? "someone";
            int told = 0;
            foreach (var quest in quests)
            {
                if (quest == null || !quest.IsOngoing || quest.QuestGiver != speaker) continue;
                if (ownIssue?.IssueQuest == quest) continue;
                if (told >= 2) break; // more than a couple and the trouble drowns the person
                told++;

                string title = null;
                Try(() => title = TidingsFormatter.StripMarkup(quest.Title?.ToString()));
                if (string.IsNullOrWhiteSpace(title)) continue;

                sentences.Add($"And there is the matter of “{title.TrimEnd('.')}”, which {player} took up at your asking.");
                var latest = LatestJournalLine(quest);
                if (latest.Length > 0)
                    sentences.Add($"The last word of it: {latest}");
            }
        }

        // The most recent journal entry that carries words, with its task's count when one is kept
        // ("Delivered hardwood: 4 of 10") — the same journal the player's quest log shows.
        private static string LatestJournalLine(QuestBase quest)
        {
            var entries = quest?.JournalEntries;
            if (entries == null) return string.Empty;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var log = entries[i];
                if (log == null) continue;
                var text = TidingsFormatter.StripMarkup(log.LogText?.ToString());
                if (text.Length == 0) continue;

                Try(() =>
                {
                    var task = TidingsFormatter.StripMarkup(log.TaskName?.ToString());
                    if (task.Length > 0 && log.Range > 0)
                        text = $"{text} ({task}: {log.CurrentProgress} of {log.Range})";
                });
                return text;
            }
            return string.Empty;
        }

        // A missing fact should never sink the whole trouble, so each is attempted independently.
        private static void Try(Action a) { try { a(); } catch { /* skip this fact */ } }
    }
}
