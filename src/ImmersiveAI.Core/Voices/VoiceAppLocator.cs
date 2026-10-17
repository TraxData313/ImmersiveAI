using System;
using System.Collections.Generic;
using System.IO;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>One place claude-voice might be, as one source describes it.</summary>
    public sealed class VoiceAppRecord
    {
        /// <summary>"registry", "where.json", "apps" or "folder" — said in the log, so a wrong find can be traced.</summary>
        public string Source = string.Empty;
        public string Root = string.Empty;
        /// <summary>"installed" or "dev" (the registry's Kind); empty when the source does not say.</summary>
        public string Kind = string.Empty;
        /// <summary>The exact command line that starts it, executable first and quoted. Registry only.</summary>
        public string Launcher = string.Empty;
        /// <summary>The Python its last start ran on (where.json's "pythonw", else "python").</summary>
        public string Python = string.Empty;
        /// <summary>The port it actually listens on (0 = not said).</summary>
        public int Port;
        public string Version = string.Empty;
    }

    /// <summary>
    /// How the game finds claude-voice and starts it — the decisions only, so they can be tested
    /// without a registry, a disk or a game (the Module gathers the records and does the I/O).
    /// <para>
    /// THE ORDER IS THE CONTRACT (claude-voice's docs/discovery.md, 2026.10.17): its registry key,
    /// which the installer writes and a developer's checkout opts into; then where.json, which every
    /// running engine leaves; then the places its setup and README put it. The first that still holds
    /// the app wins. Before this the game read where.json alone, which only a running engine writes —
    /// a fresh install, a moved folder or an old copy was "not installed" until started by hand.
    /// </para>
    /// <para>
    /// NEVER BARE <c>python</c>. A guess at a folder carries no Python of its own, and the PATH a game
    /// inherited from Steam predates any Python installed since; "python" then meets the Microsoft
    /// Store stub or nothing. A start with no known interpreter says so instead.
    /// </para>
    /// </summary>
    public static class VoiceAppLocator
    {
        public const string Marker = "voice_cli.py";

        /// <summary>The first record that still holds the app: its folder has voice_cli.py, or (a packaged
        /// build) its launcher's executable is there. Null when none does.</summary>
        public static VoiceAppRecord? Choose(IEnumerable<VoiceAppRecord?> candidates, Func<string, bool> fileExists)
        {
            foreach (var r in candidates)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.Root)) continue;
                if (Exists(fileExists, SafeCombine(r.Root, Marker))) return r;
                var exe = SplitCommandLine(r.Launcher).Exe;
                if (exe.Length > 0 && Exists(fileExists, exe)) return r;
                var program = ProgramFolder(r, fileExists);
                if (program != null) return program;
            }
            return null;
        }

        /// <summary>
        /// A 2.0 install named by its PROGRAM folder (the Settings → Apps entry's InstallLocation, or the
        /// folder guess): the code is in <c>app\</c> beside install.json, and its own signed Python in
        /// <c>runtime\</c>. Returned as a copy that names <c>app\</c> — the folder the registry key and
        /// where.json name for the same install — with that Python, so it starts like any other find.
        /// Null when the folder is not one.
        /// </summary>
        private static VoiceAppRecord? ProgramFolder(VoiceAppRecord r, Func<string, bool> fileExists)
        {
            var app = SafeCombine(r.Root, "app");
            if (app.Length == 0 || !Exists(fileExists, SafeCombine(app, Marker))
                || !Exists(fileExists, SafeCombine(r.Root, "install.json"))) return null;
            var python = r.Python;
            if (string.IsNullOrWhiteSpace(python))
            {
                python = string.Empty;
                foreach (var name in new[] { "pythonw.exe", "python.exe" })
                {
                    var candidate = SafeCombine(SafeCombine(r.Root, "runtime"), name);
                    if (Exists(fileExists, candidate)) { python = candidate; break; }
                }
            }
            return new VoiceAppRecord
            {
                Source = r.Source, Root = app, Kind = r.Kind, Launcher = r.Launcher,
                Python = python, Port = r.Port, Version = r.Version,
            };
        }

        /// <summary>
        /// What to run to start it: the recorded launcher; else the Python its last start used with
        /// <c>voice_cli.py start</c>; else <paramref name="fallbackPython"/> (an interpreter the
        /// Module found registered with Windows). Null when none of them exists — never bare python.
        /// </summary>
        public static (string Exe, string Args)? LaunchCommand(VoiceAppRecord app, Func<string, bool> fileExists, string? fallbackPython = null)
        {
            var launcher = SplitCommandLine(app.Launcher);
            if (launcher.Exe.Length > 0 && Exists(fileExists, launcher.Exe)) return (launcher.Exe, launcher.Args);

            var script = "\"" + SafeCombine(app.Root, Marker) + "\" start";
            foreach (var python in new[] { app.Python, fallbackPython })
                if (!string.IsNullOrWhiteSpace(python) && Path.IsPathRooted(python) && Exists(fileExists, python!))
                    return (python!, script);
            return null;
        }

        /// <summary>The port to knock on: the chosen copy's own word, then any other source's, then the
        /// game's setting, then the app's default.</summary>
        public static int ChoosePort(VoiceAppRecord? chosen, IEnumerable<VoiceAppRecord?> others, int configured, int fallback)
        {
            if (Valid(chosen?.Port ?? 0)) return chosen!.Port;
            foreach (var r in others)
                if (r != null && Valid(r.Port) && (chosen == null || SamePath(r.Root, chosen.Root))) return r.Port;
            return Valid(configured) ? configured : fallback;
        }

        /// <summary>Somebody's own copy — registered as "dev", or a git checkout. The game never installs
        /// into it, rewrites it or uninstalls it (2026.10.16: a game's quiet install ran in Anton's clone).</summary>
        public static bool IsDevCopy(VoiceAppRecord? app, Func<string, bool> dirExists)
        {
            if (app == null) return false;
            if (string.Equals(app.Kind, "dev", StringComparison.OrdinalIgnoreCase)) return true;
            return !string.IsNullOrWhiteSpace(app.Root) && dirExists(SafeCombine(app.Root, ".git"));
        }

        /// <summary>Splits a command line into its executable and the rest. The executable is either
        /// quoted (<c>"C:\a b\x.exe" args</c>) or runs to the first space.</summary>
        public static (string Exe, string Args) SplitCommandLine(string? line)
        {
            var s = (line ?? string.Empty).Trim();
            if (s.Length == 0) return (string.Empty, string.Empty);
            if (s[0] == '"')
            {
                var close = s.IndexOf('"', 1);
                if (close < 0) return (s.Substring(1), string.Empty);
                return (s.Substring(1, close - 1), s.Substring(close + 1).Trim());
            }
            var space = s.IndexOf(' ');
            return space < 0 ? (s, string.Empty) : (s.Substring(0, space), s.Substring(space + 1).Trim());
        }

        public static bool SamePath(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return string.Equals(Normal(a!), Normal(b!), StringComparison.OrdinalIgnoreCase);

            static string Normal(string p)
            {
                try { p = Path.GetFullPath(p); } catch { }
                return p.Replace('/', '\\').TrimEnd('\\');
            }
        }

        private static bool Valid(int port) => port > 0 && port < 65536;

        private static bool Exists(Func<string, bool> fileExists, string path)
        {
            try { return path.Length > 0 && fileExists(path); } catch { return false; }
        }

        private static string SafeCombine(string a, string b)
        {
            try { return Path.Combine(a, b); } catch { return string.Empty; }
        }
    }
}
