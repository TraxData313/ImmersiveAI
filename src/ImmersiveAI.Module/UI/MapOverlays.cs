using System;
using System.Reflection;
using TaleWorlds.ScreenSystem;

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
        private static bool _resolved;           // only once everything was FOUND — a miss retries
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
                        // NOT Type.GetType("…, SandBox.View"): the game loads module assemblies from
                        // their own folders (LoadFrom context), where a plain Assembly.Load by name
                        // does not reach — that call answered null and silently disarmed this whole
                        // guard (the 2026.07.12 "O in the encyclopedia search box" regression). The
                        // already-loaded assembly always knows its own types.
                        var mapScreen = FindLoadedType("SandBox.View", "SandBox.View.Map.MapScreen");
                        _instance = mapScreen?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        _manager = mapScreen?.GetProperty("EncyclopediaScreenManager", BindingFlags.Public | BindingFlags.Instance);
                        _isOpen = _manager?.PropertyType.GetProperty("IsEncyclopediaOpen", BindingFlags.Public | BindingFlags.Instance);
                        _resolved = _instance != null && _manager != null && _isOpen != null;
                    }
                    if (!_resolved) return false;
                    var screen = _instance.GetValue(null);
                    if (screen == null) return false;
                    var manager = _manager.GetValue(screen);
                    if (manager == null) return false;
                    return _isOpen.GetValue(manager) is bool open && open;
                }
                catch { return false; }
            }
        }

        /// <summary>True while any focused layer is swallowing the keyboard into a text field —
        /// the encyclopedia's search box, a save-name line, any overlay's input. This is the
        /// engine's own signal (GauntletLayer.IsFocusedOnInput: an EditableTextWidget holds
        /// focus), so it guards the hotkeys without knowing WHICH overlay is up — the belt to
        /// the encyclopedia check's braces.</summary>
        internal static bool IsTypingSomewhere
        {
            get
            {
                try { return ScreenManager.FocusedLayer?.IsFocusedOnInput() == true; }
                catch { return false; }
            }
        }

        private static Type FindLoadedType(string assemblyName, string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (string.Equals(asm.GetName().Name, assemblyName, StringComparison.Ordinal))
                        return asm.GetType(typeName);
                }
                catch { /* a hostile assembly's name is not worth the guard */ }
            }
            return null;
        }
    }
}
