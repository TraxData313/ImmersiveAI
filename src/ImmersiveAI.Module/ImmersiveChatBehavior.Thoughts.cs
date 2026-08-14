using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImmersiveAI.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    /// <summary>
    /// "Let me think…" — the player's own next line, worked out for them (Anton's ask, 2026.08.10:
    /// "оставям мозъка на моя герой да помисли"). The one QoL lever that points INWARD: everything
    /// else in this mod gives the NPCs a mind of their own, this one lends the player theirs when
    /// the words will not come.
    ///
    /// It rides the very sheet the one before them would answer on — <see cref="BuildContext"/>,
    /// unchanged — and closes it with an aside asking for the player's voice instead of theirs
    /// (Core <see cref="PlayerThought"/>). Three rails, all deliberate:
    ///
    /// • PLAIN CALL, no tools. A thought must never move a heart, tend a courtship, or write a
    ///   file — it is not an exchange, and nobody is spoken to. That also keeps it cheap: one call.
    /// • NOTHING IS RECORDED. The words land in the player's writing box (and in the window's own
    ///   draft store, so a closed window loses none of it) and are theirs to keep, rewrite, or
    ///   throw away. The NPC learns of them only if they are actually sent.
    /// • ONE VOICE IN THE LOG. What the player sees is their own mind at work, first person —
    ///   "What should I say… let me think." then "I think this is what I should say." — and the
    ///   ledger's own ✒ line beneath it, which is where the cost of the call is told (Anton:
    ///   the price is the honest hint that a call was made, so the words themselves need not be).
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        // The player's own voice in the message log — the same warm parchment-gold the windows use
        // for their words, so a thought reads as theirs at a glance.
        private static readonly Color ThoughtColor = new Color(0.85f, 0.75f, 0.55f, 1f);

        // Thoughts in flight, keyed by soul AND shape ("id|say", "id|write"): the two windows are
        // never open at once, but a thought outlives the window it was asked from.
        private readonly HashSet<string> _thoughtBusy = new HashSet<string>(StringComparer.Ordinal);

        // Whose thought it is — the player's own name for the cost line.
        private static string PlayerNameForLedger()
        {
            try { return Hero.MainHero?.Name?.ToString() ?? "You"; }
            catch { return "You"; }
        }

        private static string ThoughtKey(Hero npc, bool asLetter) =>
            (npc?.StringId ?? "?") + (asLetter ? "|write" : "|say");

        /// <summary>Whether the player is presently thinking out what to say (or write) to this one —
        /// the windows gray their button while it lasts.</summary>
        internal static bool IsThinkingFor(Hero npc, bool asLetter) =>
            Current != null && npc != null && Current._thoughtBusy.Contains(ThoughtKey(npc, asLetter));

        /// <summary>Whether the feature is on at all (the windows hide the button when it is not).</summary>
        internal static bool ThinkingOffered => Current?._config.EnableThinkForMe ?? false;

        /// <summary>The standing presets for the menu, read from the player's own file.</summary>
        internal static List<ConversationPreset> ConversationPresetsForMenu()
        {
            try { return PromptFiles.LoadConversationPresets(); }
            catch { return new List<ConversationPreset>(); }
        }

        /// <summary>Writes the presets back after an add, a rewrite, or a strike-out in the menu.</summary>
        internal static void SaveConversationPresets(IEnumerable<ConversationPreset> presets)
        {
            try { PromptFiles.SaveConversationPresets(presets); }
            catch { /* the menu still holds them for this session */ }
        }

        /// <summary>Back to the three the player was given (Anton's ask, 2026.08.10). Destructive and
        /// unrecoverable — every preset of their own goes — so it asks first, plainly, and the popup
        /// rides the global inquiry layer safely above the window that called it.</summary>
        internal static void RestoreConversationPresets(Action? onDone)
        {
            try
            {
                var data = new InquiryData(
                    "Begin again with the presets you were given?",
                    "Every preset you have written, reworked, or struck out is undone, and the three you " +
                    "began with — starter, romantic, ender — stand alone in their place. Nothing of your " +
                    "own is kept anywhere: this cannot be taken back.",
                    true, true,
                    "Restore the first three", "Keep mine",
                    new Action(() =>
                    {
                        try { PromptFiles.SaveConversationPresets(ConversationPresets.Defaults); }
                        catch (Exception ex) { ModLog.Error("restoring the presets", ex); }
                        onDone?.Invoke();
                    }),
                    new Action(() => { }),
                    "", 0f, (Action?)null,
                    (Func<ValueTuple<bool, string>>?)null,
                    (Func<ValueTuple<bool, string>>?)null);
                InformationManager.ShowInquiry(data, pauseGameActiveState: false);
            }
            catch (Exception ex) { ModLog.Error("asking about restoring the presets", ex); }
        }

        /// <summary>Starts one thinking. Runs on the game thread (the situation is captured now,
        /// exactly as opening a chat does); the call itself runs in the background and lands back
        /// through the window managers. False when it cannot be started — the feature is off, one is
        /// already underway, or spoken words could not reach them anyway.</summary>
        internal static bool BeginPlayerThought(Hero npc, bool asLetter, string? wish)
        {
            var self = Current;
            if (self == null || npc == null) return false;
            if (!self._config.EnableThinkForMe) return false;

            var key = ThoughtKey(npc, asLetter);
            if (self._thoughtBusy.Contains(key)) return false;
            // Words need a shared roof; a letter needs only a road, which the letter window's own
            // CanWriteTo has already weighed by the time this is reached.
            if (!asLetter && !IsCoLocated(npc)) return false;

            self._thoughtBusy.Add(key);

            // The few plain facts, gathered HERE on the game thread — who they are to me, what they
            // are good at, how they stand toward me, where we both are. Deliberately NOT the persona
            // sheet: eleven thousand tokens of her own first person drowned every attempt to ask for
            // the player's voice (twice, 2026.08.10). See ThoughtFacts.
            var facts = SafeBuildThoughtFacts(npc, asLetter);

            InformationManager.DisplayMessage(new InformationMessage(
                asLetter ? "What should I write… let me think." : "What should I say… let me think.",
                ThoughtColor));

            _ = self.ThinkForPlayerAsync(npc, asLetter, wish ?? string.Empty, facts);
            return true;
        }

        private static string SafeBuildThoughtFacts(Hero npc, bool asLetter)
        {
            try { return Personas.ThoughtFacts.Build(npc, Hero.MainHero, apart: asLetter); }
            catch { return string.Empty; }
        }

        private async Task ThinkForPlayerAsync(Hero npc, bool asLetter, string wish, string facts)
        {
            // Billed like any other flow, so the ✒ line tells the player what the thinking cost —
            // and billed to the PLAYER, whose thought it is (Anton's screenshot, 2026.08.10: the
            // line read "Sibylla — thought", as though she had done the thinking).
            using var _cost = UsageLedger.BeginInteraction("thought", PlayerNameForLedger());
            var key = ThoughtKey(npc, asLetter);
            try
            {
                var memory = LoadMemory(npc);
                var playerName = PlayerNameForLedger();
                var npcName = npc.Name?.ToString() ?? "them";

                // The world prompt rides along because it is the PLAYER's world too; nothing else of
                // the sheet does. Her own voice appears in one place only — the transcript.
                var messages = _promptBuilder.BuildPlayerThought(
                    facts, memory, playerName, npcName, wish, asLetter,
                    _config.EnableActingOut, PromptFiles.LoadGlobalPrompt());

                // A spoken line lives inside the ordinary reply budget; a letter may run long, so it
                // borrows the roomier written budget the chronicler uses.
                var client = asLetter ? _storyClient : _client;
                var raw = await client.CompleteAsync(messages).ConfigureAwait(false);
                var words = PlayerThought.Tame(raw, playerName);

                MainThreadDispatcher.Enqueue(() =>
                {
                    _thoughtBusy.Remove(key);
                    if (words.Length == 0)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Nothing comes to me just now.", ThoughtColor));
                        NotifyThoughtFailed(npc, asLetter);
                        return;
                    }

                    InformationManager.DisplayMessage(new InformationMessage(
                        asLetter ? "I think this is what I should write." : "I think this is what I should say.",
                        ThoughtColor));
                    DeliverThought(npc, asLetter, words);
                });
            }
            catch (Exception ex)
            {
                ModLog.Error("thinking of what to say to " + (npc.Name?.ToString() ?? "?"), ex);
                var message = ex.Message;
                MainThreadDispatcher.Enqueue(() =>
                {
                    _thoughtBusy.Remove(key);
                    InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + message));
                    NotifyThoughtFailed(npc, asLetter);
                });
            }
        }

        // The words go to the window's draft store first and only then to the open view: a thought
        // asked for and then walked away from still waits in the writing box on the way back.
        private static void DeliverThought(Hero npc, bool asLetter, string words) =>
            UI.TalkUI.OnThoughtReady(npc, asLetter, words);

        private static void NotifyThoughtFailed(Hero npc, bool asLetter) =>
            UI.TalkUI.OnThoughtFailed(npc, asLetter);
    }
}
