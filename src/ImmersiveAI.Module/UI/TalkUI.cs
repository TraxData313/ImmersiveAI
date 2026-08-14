using System;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.UI
{
    /// <summary>
    /// The one door the rest of the mod knocks on when it has something to show the player
    /// (2026.08.14). Behind it stand the TALK SCREEN — the full-stage place where everyone you know
    /// is one list and letters read as part of the same story — and, as insurance, the two small
    /// windows it replaced.
    ///
    /// Why a façade at all: forty call sites across the behavior say "a thread changed", "a message
    /// failed", "open on this soul". None of them should know or care which shape is on stage, and
    /// when the old windows are finally retired this file is the only one that has to notice.
    ///
    /// The rule is simple — NOTICES go to everyone (each manager is a no-op when it is not open, so
    /// fanning out is free and nothing can be missed), while OPENING goes to whichever shape the
    /// player has chosen.
    /// </summary>
    internal static class TalkUI
    {
        private static ModConfig? _config;

        internal static void Configure(ModConfig config)
        {
            _config = config;
            TalkScreen.TalkScreenManager.Configure(config);
            ChatWindow.ChatWindowManager.Configure(config);
            LetterWindow.LetterWindowManager.Configure(config);
        }

        internal static void Tick()
        {
            TalkScreen.TalkScreenManager.Tick();

            // The old windows only LISTEN while they are the shape in use. They poll the very same
            // keys from the very same global input, so ticking them beside the talk screen meant one
            // press of "O" opened the screen AND the window it replaced, stacked at the same layer
            // order. An already-open one is still ticked, so a mid-session change of shape can close
            // what it left standing.
            if (!UsesTalkScreen || ChatWindow.ChatWindowManager.IsOpen) ChatWindow.ChatWindowManager.Tick();
            if (!UsesTalkScreen || LetterWindow.LetterWindowManager.IsOpen) LetterWindow.LetterWindowManager.Tick();
        }

        /// <summary>Whether the talk screen is the shape in use — false when the player asked for the
        /// old windows, or when raising the screen has already failed this session.</summary>
        internal static bool UsesTalkScreen => TalkScreen.TalkScreenManager.IsPreferred;

        /// <summary>Whether ANY of the conversation surfaces is on stage right now.</summary>
        internal static bool IsOpen =>
            TalkScreen.TalkScreenManager.IsOpen
            || ChatWindow.ChatWindowManager.IsOpen
            || LetterWindow.LetterWindowManager.IsOpen;

        /// <summary>Whether the player is looking at this very thread right now (so a "has answered"
        /// toast would only state the obvious).</summary>
        internal static bool IsViewing(Hero npc) =>
            TalkScreen.TalkScreenManager.IsViewing(npc) || ChatWindow.ChatWindowManager.IsViewing(npc);

        // ------------------------------ notices (fan out to all) ------------------------------

        /// <summary>A thread changed underneath whatever is on stage — a reply landed, a letter came,
        /// a beat was recorded. Must run on the game thread.</summary>
        internal static void OnThreadChanged(Hero npc, bool markUnread)
        {
            TalkScreen.TalkScreenManager.OnThreadChanged(npc, markUnread);
            ChatWindow.ChatWindowManager.OnThreadChanged(npc, markUnread);
        }

        /// <summary>A message could not be sent after all — give the words back to the player.</summary>
        internal static void OnSendFailed(Hero npc, string text)
        {
            TalkScreen.TalkScreenManager.OnSendFailed(npc, text);
            ChatWindow.ChatWindowManager.OnSendFailed(npc, text);
        }

        /// <summary>The player's own thought came back. Goes to every shape: the words land in each
        /// draft store, so whichever one they open next is holding them.</summary>
        internal static void OnThoughtReady(Hero npc, bool asLetter, string words)
        {
            TalkScreen.TalkScreenManager.OnThoughtReady(npc, words);
            if (asLetter) LetterWindow.LetterWindowManager.OnThoughtReady(npc, words);
            else ChatWindow.ChatWindowManager.OnThoughtReady(npc, words);
        }

        internal static void OnThoughtFailed(Hero npc, bool asLetter)
        {
            TalkScreen.TalkScreenManager.OnThoughtFailed(npc);
            if (asLetter) LetterWindow.LetterWindowManager.OnThoughtFailed(npc);
            else ChatWindow.ChatWindowManager.OnThoughtFailed(npc);
        }

        // ------------------------------ opening (the chosen shape) ------------------------------

        /// <summary>Opens the conversation surface, optionally on one soul's thread. With the talk
        /// screen in use this is the ONE door for both speaking and letters.</summary>
        internal static void Open(Hero? preselect = null)
        {
            if (UsesTalkScreen)
            {
                TalkScreen.TalkScreenManager.Open(preselect);
                // The screen may have bowed out as it tried to rise (a game patch under it); fall
                // through to the old window rather than leaving the player with nothing.
                if (TalkScreen.TalkScreenManager.IsOpen || TalkScreen.TalkScreenManager.IsPreferred) return;
            }
            ChatWindow.ChatWindowManager.Open(preselect);
        }

        /// <summary>The letters door specifically — the courier menu, and "write to someone far".
        /// With the talk screen it is the same place; with the old windows it is the letter one.
        /// Returns false when nothing could be raised (the caller falls back to its popups).</summary>
        internal static bool OpenForLetters(Hero? preselect = null)
        {
            if (UsesTalkScreen)
            {
                TalkScreen.TalkScreenManager.Open(preselect);
                if (TalkScreen.TalkScreenManager.IsOpen) return true;
            }
            if (_config?.EnableLetterWindow != true) return false;
            if (preselect != null) { LetterWindow.LetterWindowManager.OpenWhenClear(preselect, null); return true; }
            return LetterWindow.LetterWindowManager.Open();
        }

        /// <summary>Opens on a soul's thread the moment the map is clear of popups — used when
        /// something closing (an inquiry, a notice) would otherwise race the opening. The fallback
        /// runs only if the wait ran out.</summary>
        internal static void OpenWhenClear(Hero npc, Action<Hero>? fallback = null)
        {
            if (UsesTalkScreen) { TalkScreen.TalkScreenManager.OpenWhenClear(npc, fallback); return; }
            LetterWindow.LetterWindowManager.OpenWhenClear(npc, fallback);
        }

        /// <summary>Closes whatever is on stage (used when the world moves somewhere none of them
        /// can follow).</summary>
        internal static void Close()
        {
            TalkScreen.TalkScreenManager.Close();
            ChatWindow.ChatWindowManager.Close();
            LetterWindow.LetterWindowManager.Close();
        }
    }
}
