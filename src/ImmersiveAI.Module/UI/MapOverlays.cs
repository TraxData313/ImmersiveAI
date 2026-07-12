using System;
using System.Reflection;

namespace ImmersiveAI.UI
{
    /// <summary>
    /// Soft eyes on map overlays the GameState cannot see. The encyclopedia lives as a layer OVER
    /// MapState — the state never changes while it is up — so typing into its search box would trip
    /// the windows' raw hotkey polls ("O"/"U" opening a window mid-word). Vanilla gates its escape
    /// menu on the same flag (MapScreen.EncyclopediaScreenManager.IsEncyclopediaOpen); we read it by
    /// cached reflection because SandBox.View is deliberately not referenced — if a game patch ever
    /// reshapes MapScreen, this answers false and the hotkeys merely lose the encyclopedia guard
    /// instead of the mod failing to load.
    /// </summary>
    internal static class MapOverlays
    {
        private static bool _resolved;
        private static PropertyInfo _instance;   // static MapScreen MapScreen.Instance
        private static PropertyInfo _manager;    // MapEncyclopediaView MapScreen.EncyclopediaScreenManager
        private static PropertyInfo _isOpen;     // bool MapEncyclopediaView.IsEncyclopediaOpen

        internal static bool IsEncyclopediaOpen
        {
            get
            {
                try
                {
                    if (!_resolved)
                    {
                        _resolved = true;
                        var mapScreen = Type.GetType("SandBox.View.Map.MapScreen, SandBox.View");
                        _instance = mapScreen?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        _manager = mapScreen?.GetProperty("EncyclopediaScreenManager", BindingFlags.Public | BindingFlags.Instance);
                        _isOpen = _manager?.PropertyType.GetProperty("IsEncyclopediaOpen", BindingFlags.Public | BindingFlags.Instance);
                    }
                    if (_instance == null || _manager == null || _isOpen == null) return false;
                    var screen = _instance.GetValue(null);
                    if (screen == null) return false;
                    var manager = _manager.GetValue(screen);
                    if (manager == null) return false;
                    return _isOpen.GetValue(manager) is bool open && open;
                }
                catch { return false; }
            }
        }
    }
}
