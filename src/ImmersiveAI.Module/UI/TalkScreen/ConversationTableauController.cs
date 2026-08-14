using System;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.UI.TalkScreen
{
    /// <summary>
    /// The one before you, standing in their own place (2026.08.14).
    ///
    /// Vanilla's map Talk is not a mission — it is a render-to-texture TABLEAU: a small cached scene
    /// with the partner's visuals built from plain character data, drawn into a texture widget, with
    /// an atmosphere chosen from terrain, culture, weather and hour. All of it is public, so we can
    /// host the same picture inside our own screen and show ANY soul in it — including one a kingdom
    /// away, rendered against THEIR sky rather than ours, which is the whole reason the talk screen
    /// can list the far-off at all.
    ///
    /// Three disciplines, learned from reading how the game drives it:
    /// • The scene is SHARED. Never hold it while the game's own map conversation is running, and
    ///   always strike it on the way out (<see cref="Clear"/> from the manager's teardown).
    /// • It is a VERSION FACT. Everything here is wrapped: if a game patch moves the ground, the
    ///   face quietly does not appear and the conversation goes on without it.
    /// • It is a GRACE, never the point. No failure in this file may cost a single word.
    /// </summary>
    internal static class ConversationTableauController
    {
        /// <summary>Who is on stage right now — so re-choosing the same soul costs nothing.</summary>
        private static Hero? _shown;

        /// <summary>Set once the visuals prove unbuildable in this game build: after that the screen
        /// simply shows no face, and stops paying for the attempt on every click.</summary>
        private static bool _unavailable;

        /// <summary>Whether a face can be shown at all right now — bound by the screen so the middle
        /// column falls back to its hint text when the answer is no.</summary>
        internal static bool CanShowFace => !_unavailable;

        /// <summary>The data the tableau widget draws. Held as a plain object so this file is the
        /// ONLY place that names the game's conversation types.</summary>
        internal static object? TableauData { get; private set; }

        /// <summary>Puts a soul before the player. Null simply empties the stage — someone dead has
        /// no face to show — WITHOUT striking it: see <see cref="Hide"/>.</summary>
        internal static void Show(Hero? hero)
        {
            if (hero == null) { Hide(); return; }
            if (_unavailable) return;
            if (ReferenceEquals(_shown, hero)) return;

            try
            {
                var data = ConversationSceneBuilder.BuildFor(hero);
                TableauData = data;
                _shown = data == null ? null : hero;
                // A null here is NOT a verdict on this game build — the map may simply not be up
                // yet, or the game's own Talk may hold the one shared scene this instant. Latching
                // the face off forever on a passing "not now" would cost it for the whole process.
            }
            catch (Exception ex)
            {
                MarkUnavailable(ex);
            }
        }

        /// <summary>A small shift of weight as your words reach them — the one movement there is,
        /// and only for the soul actually standing there. Always a grace: a stage that cannot carry
        /// it simply does not move, and nothing is lost.</summary>
        internal static bool ShiftStance(Hero? hero)
        {
            if (hero == null || _unavailable) return false;
            if (!ReferenceEquals(_shown, hero)) return false;   // not the one being looked at
            try { return ConversationSceneBuilder.ShiftStance(hero); }
            catch (Exception ex)
            {
                // Never worth a crash, and never worth losing the face over either — so this does
                // NOT latch the whole feature off the way a failed build does.
                ModLog.Error("moving " + (hero.Name?.ToString() ?? "them") + " on the talk screen", ex);
                return false;
            }
        }

        /// <summary>Empties the stage but LEAVES IT STANDING — the data goes, the planted mission
        /// stays. This is the difference between choosing someone with no face to show (a dead
        /// correspondent) and closing the screen altogether.
        ///
        /// It matters because the widget is still alive here: handing it a null makes it reach for
        /// the conversation mission to let go of its scene, and if we had already unplanted that
        /// mission the reach lands on nothing — a crash thrown deep inside the engine's own property
        /// propagation, where none of our try/catch can see it.</summary>
        internal static void Hide()
        {
            _shown = null;
            TableauData = null;
        }

        /// <summary>Strikes the stage — called when the screen closes, so the shared scene is never
        /// left held against the game's own conversation.
        ///
        /// ORDER MATTERS: the data goes first and the stub last. The widget reads its scene through
        /// the data, and unplanting the mission out from under a widget that has not finished with it
        /// is one of the two known teardown crashes. The manager helps by taking the layer down
        /// before it calls this, so the widget is already finalized by now.</summary>
        internal static void Clear()
        {
            _shown = null;
            TableauData = null;
            try { ConversationSceneBuilder.Release(); }
            catch { /* best-effort */ }
        }

        // Latched ONLY by a real throw — this game build cannot raise these visuals at all, and
        // trying again every click would just pay the same price for the same answer. A merely
        // unbuildable moment (no map yet, the game's own Talk holding the scene) is not this.
        private static void MarkUnavailable(Exception ex)
        {
            _unavailable = true;
            TableauData = null;
            _shown = null;
            ModLog.Error("showing a face on the talk screen (the words carry on without it)", ex);
        }
    }
}
