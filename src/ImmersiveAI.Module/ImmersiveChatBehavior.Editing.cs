using System;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI
{
    /// <summary>
    /// The talk screen's editor reaching past the two prompt files (2026.10.14, Anton: "make Who
    /// they are and their deep memory freely editable from the game"). "Their prompt" already
    /// edited custom_instructions.txt; this adds the two things a player most wants to correct and
    /// could not: who the NPC is (built live from the game, so the player's words are kept apart
    /// and stand in its place) and their deep memory of the player (the one rolling summary in
    /// memories.json, written straight back).
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        /// <summary>The "Who they are" block the game builds for this NPC, exactly as the sheet
        /// would (same persona, same atmosphere line, same crafts).</summary>
        internal static string BuiltWhoTheyAre(Hero npc)
        {
            var self = Current;
            if (self == null || npc == null) return string.Empty;
            try
            {
                var persona = PersonaBuilder.Build(npc, self._config);
                persona.AtmosphereLine = self.ApplyTokens(self._config.AtmosphereLine, persona.Name);
                persona.Crafts = CraftsBuilder.Build(npc);
                return PromptBuilder.BuiltWhoTheyAre(persona);
            }
            catch (Exception ex)
            {
                ModLog.Error("building who " + (npc.Name?.ToString() ?? "?") + " is, for the editor", ex);
                return string.Empty;
            }
        }

        /// <summary>What the editor opens with: the player's own words when they have written some,
        /// else the block the game builds.</summary>
        internal static string WhoTheyAreForEdit(Hero npc)
        {
            var mine = PromptFiles.LoadWhoTheyAre(NpcPaths.WhoTheyAreFile(npc));
            return EditLine.ToLine(mine.Length > 0 ? mine : BuiltWhoTheyAre(npc));
        }

        /// <summary>Their deep memory of the player, as the editor's one line. Empty when they hold
        /// none yet (a soul never met carries their backstory there, and it is shown).</summary>
        internal static string DeepMemoryForEdit(Hero npc)
        {
            var self = Current;
            if (self == null || npc == null) return string.Empty;
            try { return EditLine.ToLine(self.LoadMemory(npc).Summary); }
            catch (Exception ex)
            {
                ModLog.Error("reading the deep memory of " + (npc.Name?.ToString() ?? "?") + " for the editor", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Writes an in-game edit of the deep memory back, and says how it went in a line for the
        /// player. Refused while anything is still working with this NPC's memory — a reply on its
        /// way, a memory being gathered, a letter being written — because that work holds a copy
        /// loaded before the edit and would save it over the player's words when it lands. An empty
        /// edit is refused too: wiping a memory is deleting memories.json, not a slip of the keys.
        /// </summary>
        internal static bool SaveDeepMemoryFromGame(Hero npc, string line, out string message)
        {
            var self = Current;
            var name = npc?.Name?.ToString() ?? "They";
            if (self == null || npc == null) { message = "Immersive AI: no campaign is running."; return false; }

            var text = EditLine.FromLine(line);
            if (text.Length == 0)
            {
                message = $"Nothing saved: an empty memory would wipe what {name} remembers of you. "
                          + "To make them forget you entirely, delete their memories.json.";
                return false;
            }
            if (self.IsExchangeInFlight(npc) || self._letterWorkInFlight)
            {
                message = $"{name} is still in the middle of something with their memory. Save again in a moment.";
                return false;
            }

            try
            {
                var memory = self.LoadMemory(npc);
                memory.Summary = text;
                self.SaveMemory(npc, memory);
                message = $"{name}'s deep memory is set — it speaks from their next reply.";
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("saving an in-game edit of the deep memory of " + name, ex);
                message = "Immersive AI: " + ex.Message;
                return false;
            }
        }
    }
}
