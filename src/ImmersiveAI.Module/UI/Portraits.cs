using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ImmersiveAI.UI
{
    /// <summary>
    /// Shared portrait plumbing for every place the mod draws a character's face (the map notice,
    /// the chat window's contact list). The tableau tints the portrait backdrop with the character
    /// code's Color1/Color2 (multiplayer uses team colors there); the plain CreateFrom leaves them
    /// white, which renders as the raw purple gradient — so a muted near-black keeps the face lit
    /// and the circle quiet. Falls back to the plain code if any piece cannot be read.
    /// </summary>
    internal static class Portraits
    {
        internal static CharacterCode DarkCode(CharacterObject character)
        {
            try
            {
                const uint backdrop = 0xFF17120DU; // ARGB: near-black with a warm cast
                return CharacterCode.CreateFrom(
                    character.Equipment?.CalculateEquipmentCode(),
                    character.GetBodyProperties(character.Equipment),
                    character.IsFemale,
                    character.IsHero,
                    backdrop,
                    backdrop,
                    character.DefaultFormationClass,
                    character.Race);
            }
            catch
            {
                return CharacterCode.CreateFrom(character);
            }
        }
    }
}
