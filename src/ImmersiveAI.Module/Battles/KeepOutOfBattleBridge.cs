using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace ImmersiveAI.Battles
{
    /// <summary>
    /// Asks the sibling mod Keep Out of Battle whether a hero of the player's party was held back from a
    /// battle (a surgeon, a steward spouse), so the chronicle tells them they WATCHED the day rather than
    /// handing them a "none fell to my hand" beat for a fight they were never in.
    ///
    /// Soft reflection, same stance as the MCM bridge: no reference either way, bound once by type name,
    /// and every failure answers "no" so the chronicle stays exactly as it was without that mod.
    /// CONTRACT with the sibling repo (keep-out-of-battle): type "KeepOutOfBattle.KeepOutOfBattleApi",
    /// static bool WasKeptOut(MapEvent, string heroId). Its source carries the same note.
    /// </summary>
    internal static class KeepOutOfBattleBridge
    {
        private const string ApiTypeName = "KeepOutOfBattle.KeepOutOfBattleApi";

        private static bool _looked;
        private static Func<MapEvent, string, bool>? _wasKeptOut;

        public static bool WasKeptOut(MapEvent? mapEvent, string? heroId)
        {
            if (mapEvent == null || string.IsNullOrEmpty(heroId)) return false;
            var wasKeptOut = Bind();
            if (wasKeptOut == null) return false;
            try { return wasKeptOut(mapEvent, heroId!); }
            catch { return false; }
        }

        // Module DLLs all load before any campaign, so one look at the first battle is final.
        private static Func<MapEvent, string, bool>? Bind()
        {
            if (_looked) return _wasKeptOut;
            _looked = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = asm.GetType(ApiTypeName, throwOnError: false);
                    var method = type?.GetMethod("WasKeptOut", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(MapEvent), typeof(string) }, null);
                    if (method == null || method.ReturnType != typeof(bool)) continue;
                    _wasKeptOut = (Func<MapEvent, string, bool>)Delegate.CreateDelegate(typeof(Func<MapEvent, string, bool>), method);
                    ModLog.Info("Keep Out of Battle found: the chronicle tells kept-back heroes they watched.");
                    break;
                }
            }
            catch { _wasKeptOut = null; }
            return _wasKeptOut;
        }
    }
}
