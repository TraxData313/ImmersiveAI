using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI
{
    /// <summary>
    /// The talk screen's own bridges (2026.08.14) — the one place the chat window's "those near you"
    /// and the letter window's "everyone the roads can reach" are gathered into a single circle.
    ///
    /// The screen makes no distinction between a friend in the room and a friend a kingdom away:
    /// both are simply people you know, and the only difference is whether your words go by voice
    /// or by courier. That decision lives here (<see cref="ReachOf"/>), so the view never has to
    /// reason about presence, couriers, or the dead.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        /// <summary>One row of the talk screen's list: a soul near, a soul far, or a soul gone whose
        /// letters remain. Gathered on the game thread.</summary>
        internal sealed class TalkContactInfo
        {
            public TalkContactInfo(Hero? hero, string name, string folder, bool isHere, bool isGone,
                                   bool hasHistory, bool hasLetters, double lastSpokenGameDay, string detail)
            {
                Hero = hero; Name = name; Folder = folder; IsHere = isHere; IsGone = isGone;
                HasHistory = hasHistory; HasLetters = hasLetters;
                LastSpokenGameDay = lastSpokenGameDay; Detail = detail;
            }

            public Hero? Hero { get; }
            public string Name { get; }
            public string Folder { get; }
            public bool IsHere { get; }
            public bool IsGone { get; }
            public bool HasHistory { get; }
            public bool HasLetters { get; }
            public double LastSpokenGameDay { get; }
            public string Detail { get; }
        }

        /// <summary>How the chosen one can be reached at all — the whole of what the screen needs to
        /// know to label its button and gray it honestly.</summary>
        internal enum TalkReach
        {
            /// <summary>They stand with you: words carry.</summary>
            Spoken,
            /// <summary>They are away: only a letter reaches them.</summary>
            Letter,
            /// <summary>Nothing can be sent — they are gone, or the road is already taken.</summary>
            Closed
        }

        /// <summary>What the screen may do with this one right now. <paramref name="reason"/> speaks
        /// whenever the sending is shut, and is shown under their name rather than swallowed: a dead
        /// button is indistinguishable from a broken one.
        ///
        /// One letter at a time between any two people, in EITHER direction (Anton, 2026.08.14) —
        /// that rail is <see cref="CanWriteTo"/>'s, and it is direction-blind by design: while her
        /// letter rides toward you, your seal waits too. Correspondence stays a turn-taking thing.</summary>
        internal static TalkReach ReachOf(Hero? hero, out string reason)
        {
            reason = string.Empty;
            if (hero == null || !hero.IsAlive)
            {
                reason = "The hand that wrote these is gone from this world — what came before stays readable.";
                return TalkReach.Closed;
            }

            if (IsCoLocated(hero)) return TalkReach.Spoken;

            // Away: a letter is the only road, and it may itself be closed (a courier already out,
            // or the couriers not riding at all).
            return CanWriteTo(hero, out reason) ? TalkReach.Letter : TalkReach.Closed;
        }

        /// <summary>Everyone the talk screen lists: everyone co-located, every remembered bond
        /// wherever they stand, every correspondent whose letters exist (the dead included), and the
        /// betrothal kin the letter list unlocks. One circle, gathered once.
        ///
        /// Both source lists walk the campaign's folders, so this is the same disk work the two old
        /// windows each did on opening — merged by folder, which is the identity that survives a
        /// rename and a death alike.</summary>
        internal static List<TalkContactInfo> ContactsForTalk()
        {
            var byFolder = new Dictionary<string, TalkContactInfo>(StringComparer.OrdinalIgnoreCase);
            var self = Current;

            // 1) Those who can be SPOKEN with, and the remembered bonds away across the map. This
            //    list is the richer one for anyone present (it knows their duty in your company).
            try
            {
                foreach (var info in NearbyHeroesForChat())
                {
                    var hero = info.Hero;
                    if (hero == null) continue;
                    var folder = FolderOf(hero);
                    if (folder.Length == 0) continue;
                    byFolder[folder] = new TalkContactInfo(
                        hero, hero.Name?.ToString() ?? "Unknown", folder,
                        isHere: info.IsHere, isGone: false,
                        hasHistory: info.HasHistory, hasLetters: false,
                        lastSpokenGameDay: info.LastSpokenGameDay, detail: info.Detail);
                }
            }
            catch { /* a half-gathered circle still beats none */ }

            // 2) The correspondents: everyone with letters (even the dead), plus the betrothal kin
            //    the letters list unlocks. Where someone is already listed above, only their letter
            //    facts are folded in — their presence and duty line are already the truer ones.
            try
            {
                foreach (var info in CorrespondentsForLetters())
                {
                    var folder = info.Folder ?? string.Empty;
                    if (folder.Length == 0) continue;

                    if (byFolder.TryGetValue(folder, out var already))
                    {
                        // Away, a courier already on the road outranks a bare whereabouts — it is the
                        // very thing that decides whether the player may write at all. Asked of the
                        // letter bag directly rather than sniffed out of the wording: a caravan
                        // master "upon the roads" is not a courier, and any test on the words would
                        // have said he was.
                        var detail = already.Detail;
                        if (!already.IsHere && already.Hero != null)
                        {
                            var riding = self?.CourierStatusFor(already.Hero.StringId) ?? string.Empty;
                            if (riding.Length > 0) detail = riding;
                        }

                        byFolder[folder] = new TalkContactInfo(
                            already.Hero, already.Name, folder, already.IsHere, already.IsGone,
                            already.HasHistory, info.HasLetters,
                            Math.Max(already.LastSpokenGameDay, info.LastSpokenGameDay), detail);
                        continue;
                    }

                    bool gone = info.Hero == null || !info.Hero.IsAlive;
                    bool here = !gone && IsCoLocated(info.Hero!);
                    byFolder[folder] = new TalkContactInfo(
                        info.Hero, info.Name, folder, isHere: here, isGone: gone,
                        hasHistory: info.LastSpokenGameDay >= 0, hasLetters: info.HasLetters,
                        lastSpokenGameDay: info.LastSpokenGameDay, detail: info.Detail);
                }
            }
            catch { /* the near circle still stands without the far one */ }

            return byFolder.Values.ToList();
        }

        /// <summary>A soul's memory folder, healed for identity first — empty when it cannot be
        /// resolved at all (then they simply do not join the list).</summary>
        private static string FolderOf(Hero hero)
        {
            try
            {
                NpcPaths.EnsureMigrated(hero);
                return NpcPaths.NpcFolder(hero);
            }
            catch { return string.Empty; }
        }

        // ============================ what they are about to be given ============================
        //
        // THE SCROLLBACK (Anton, 2026.08.14: "as I scroll above I see everything the NPC sees in
        // their prompt… in the order the NPC sees them"). Not a button, not a developer's popup, not
        // a wall of JSON — the thread simply keeps going upward past its oldest word into the whole
        // of what this soul carries into the next thing you say. Every player sees it.
        //
        // Two rules keep it honest. It is built from the SAME calls the real exchange uses — the
        // same context, the same sheet, the same tool list (GatherSpokenTools) — so it cannot drift
        // into a comfortable fiction. And it costs nothing: no LLM call, no writing, no recording.

        /// <summary>One hand the soul may reach for, as the model is really told about it.</summary>
        internal sealed class PreviewTool
        {
            public PreviewTool(string name, string description, List<string> parameters)
            {
                Name = name; Description = description; Parameters = parameters;
            }
            public string Name { get; }
            public string Description { get; }
            public List<string> Parameters { get; }
        }

        /// <summary>Everything given to a soul before your next words: their own mind, first person,
        /// and the hands they may reach for while answering. The recorded turns are deliberately NOT
        /// here — the thread below is already showing them, and showing them twice would be a lie
        /// about what the model receives.</summary>
        internal sealed class PromptPreview
        {
            public PromptPreview(string sheet, List<PreviewTool> tools, string weights)
            {
                Sheet = sheet; Tools = tools; Weights = weights;
            }
            public string Sheet { get; }
            public List<PreviewTool> Tools { get; }
            /// <summary>The tally of what the next message weighs, piece by piece — the audit the
            /// scrollback opens with (Anton, 2026.08.27). Empty when it could not be reckoned.</summary>
            public string Weights { get; }
        }

        /// <summary>Builds what the chosen one would be given right now. Null when it cannot be
        /// built — the thread then simply begins at the oldest word, as it always did.</summary>
        internal static PromptPreview? PromptPreviewFor(Hero npc, bool asLetter)
        {
            var self = Current;
            if (self == null || npc == null) return null;
            try { return self.BuildPromptPreview(npc, asLetter); }
            catch (Exception ex)
            {
                ModLog.Error("laying out what " + (npc.Name?.ToString() ?? "?") + " is about to be given", ex);
                return null;
            }
        }

        private PromptPreview BuildPromptPreview(Hero npc, bool asLetter)
        {
            // The situation is built FRESH and explicitly, exactly as the real send builds it — the
            // spoken shape for someone in the room, the apart shape for someone across the map (a
            // letter is read by a soul who is not looking at you, and her sheet must say so).
            // Passing null here instead would let a situation captured during some earlier
            // conversation stand in, and the player would be shown a moment that is no longer true.
            var scene = asLetter ? SafeBuildApartSituation(npc) : SafeBuildSituation(npc);

            var ctx = BuildContext(npc, scene);

            // The sheet WITH its section marks — the one reader in the mod that wants them, so the
            // screen can draw a named, coloured header per block (2026.08.27). It is the very same
            // builder the real send uses; Build() simply strips the marks on its way out, so the
            // preview cannot drift from the truth by being assembled a second way.
            var sheet = Core.Prompts.PromptBuilder.BuildMarkedSheet(
                ctx.Persona, ctx.Memory, ctx.Scene, ctx.PlayerName);

            var tools = new List<PreviewTool>();
            foreach (var tool in GatherSpokenTools(npc))
            {
                var parameters = new List<string>();
                if (tool.Parameters != null)
                {
                    foreach (var p in tool.Parameters)
                    {
                        var line = p.Name + (p.Required ? "" : " (optional)") + " — " + p.Description;
                        if (p.AllowedValues != null && p.AllowedValues.Count > 0)
                            line += "  [one of: " + string.Join(", ", p.AllowedValues) + "]";
                        parameters.Add(line);
                    }
                }
                tools.Add(new PreviewTool(tool.Name, tool.Description, parameters));
            }

            return new PromptPreview(sheet, tools, BuildPromptWeights(sheet, ctx, tools));
        }

        /// <summary>The tally the scrollback opens with: what the next message truly weighs, piece
        /// by piece, reckoned with the same estimator the memory gauge uses so the two never
        /// disagree (Anton, 2026.08.27 — the token audit, made a fixture). Empty on any stumble;
        /// the preview stands whole without it.</summary>
        private static string BuildPromptWeights(string sheet, ChatContext ctx, List<PreviewTool> tools)
        {
            try
            {
                int sheetTok = Core.Memory.MemoryTokenEstimator.EstimateTextTokens(sheet);
                int memoryTok = Core.Memory.MemoryTokenEstimator.EstimateTextTokens(
                    ctx.Memory == null ? string.Empty : ctx.Memory.DeepMemoryText());
                int sceneTok = Core.Memory.MemoryTokenEstimator.EstimateTextTokens(ctx.Scene);
                int personaTok = Math.Max(0, sheetTok - memoryTok - sceneTok);
                // Only what actually RIDES is weighed: the oldest silent marks stop being sent once
                // the bookkeeping would hold more than a third of the window (BeatsThatStillRide).
                var riding = new List<Core.Memory.ConversationTurn>();
                int settled = 0;
                if (ctx.Memory != null)
                {
                    var carries = Core.Prompts.PromptBuilder.BeatsThatStillRide(ctx.Memory);
                    for (int i = 0; i < ctx.Memory.RecentTurns.Count; i++)
                    {
                        if (carries[i]) riding.Add(ctx.Memory.RecentTurns[i]);
                        else settled++;
                    }
                }
                int turnsTok = Core.Memory.MemoryTokenEstimator.EstimateRecentTurnsTokens(riding);
                int turnCount = riding.Count;

                // ~30 tokens per tool is the schema's own scaffolding (name, types, required) that
                // rides beside the visible words.
                int toolsTok = 0;
                foreach (var t in tools)
                {
                    toolsTok += 30 + Core.Memory.MemoryTokenEstimator.EstimateTextTokens(t.Description);
                    foreach (var p in t.Parameters)
                        toolsTok += Core.Memory.MemoryTokenEstimator.EstimateTextTokens(p);
                }

                int total = sheetTok + turnsTok + toolsTok;
                string K(int n) => n >= 10_000 ? (n / 1000) + "k"
                    : n >= 1_000 ? (n / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "k"
                    : n.ToString();

                var sb = new StringBuilder();
                sb.Append("About ").Append(K(total)).Append(" tokens ride with your next message:");
                sb.Append("\n   · their mind's sheet — ").Append(K(sheetTok))
                  .Append("  (deep memory ").Append(K(memoryTok))
                  .Append(" · this moment about them ").Append(K(sceneTok))
                  .Append(" · who they are ").Append(K(personaTok)).Append(')');
                sb.Append("\n   · the hands they may reach for — ").Append(K(toolsTok))
                  .Append(" across ").Append(tools.Count).Append(" tools");
                sb.Append("\n   · the words already between you — ").Append(K(turnsTok))
                  .Append(" in ").Append(turnCount).Append(" remembered turns");
                if (settled > 0)
                    sb.Append("\n   · ").Append(settled)
                      .Append(settled == 1 ? " older mark has settled" : " older marks have settled")
                      .Append(" — kept in memory and in the chronicles, but no longer carried into every reply");
                sb.Append("\nEach time they reach for a hand mid-reply, the whole of it is sent again.");
                return sb.ToString();
            }
            catch { return string.Empty; }
        }

        /// <summary>The correspondence a folder holds, whether or not its writer still lives — the
        /// letter window's own view, kept for the talk screen's thread.</summary>
        internal static bool FolderHasLetters(string folder)
        {
            try
            {
                return !string.IsNullOrEmpty(folder)
                       && File.Exists(Path.Combine(folder, NpcPaths.CorrespondenceFileName));
            }
            catch { return false; }
        }
    }
}
