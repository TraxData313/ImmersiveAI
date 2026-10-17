using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>The Python an installed claude-voice runs on, as its runtime.json pins it.</summary>
    public sealed class RuntimePin
    {
        public string Version = string.Empty;
        public string Url = string.Empty;
        public string Sha256 = string.Empty;
        public string Signer = string.Empty;
    }

    /// <summary>
    /// The decisions in installing claude-voice from the game, without the I/O (the Module downloads,
    /// unpacks and starts things). Since claude-voice 2.0 (2026.10.17) there is no setup .exe: Smart App
    /// Control refuses any executable without a signature it knows, and claude-voice has no certificate
    /// yet. What does run is python.org's own Python, signed by the Python Software Foundation — so the
    /// game puts that Python in the program folder, checked against the SHA-256 claude-voice pins in its
    /// runtime.json, and runs claude-voice's installer.py on it.
    /// </summary>
    public static class VoiceAppInstall
    {
        public const string SourceZip = "https://github.com/TraxData313/claude-voice/archive/refs/heads/main.zip";
        public const string Installer = "installer.py";
        public const string RuntimeMarker = "claude-voice-runtime.json";
        private const string PythonOrg = "https://www.python.org/ftp/python/";

        /// <summary>runtime.json, or null when it is not a pin this trusts: python.org only, a full SHA-256.</summary>
        public static RuntimePin? ParsePin(string? json)
        {
            try
            {
                var doc = JObject.Parse(json ?? string.Empty);
                var pin = new RuntimePin
                {
                    Version = (string?)doc["version"] ?? string.Empty,
                    Url = (string?)doc["url"] ?? string.Empty,
                    Sha256 = ((string?)doc["sha256"] ?? string.Empty).Trim().ToLowerInvariant(),
                    Signer = ((string?)doc["signer"] ?? string.Empty).Trim(),
                };
                // Blank would match every signer ("CN=" is in all of them).
                if (pin.Signer.Length == 0) pin.Signer = "Python Software Foundation";
                if (!pin.Url.StartsWith(PythonOrg, StringComparison.OrdinalIgnoreCase)) return null;
                if (!Regex.IsMatch(pin.Sha256, "^[0-9a-f]{64}$")) return null;
                return pin;
            }
            catch { return null; }
        }

        /// <summary>The runtime already in place is the pinned one (its marker names the same hash).</summary>
        public static bool RuntimeIsCurrent(string? markerJson, RuntimePin pin)
        {
            try
            {
                var sha = (string?)JObject.Parse(markerJson ?? string.Empty)["sha256"];
                return !string.IsNullOrEmpty(sha) && string.Equals(sha!.Trim(), pin.Sha256, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>
        /// The program folder: an installed copy's own (the registry names its <c>app</c> folder, whose
        /// parent holds install.json), else <c>%LOCALAPPDATA%\Programs\claude-voice</c>. Never a
        /// developer's checkout — the caller has already refused those.
        /// </summary>
        public static string ProgramRoot(VoiceAppRecord? found, string localAppData, Func<string, bool> fileExists)
        {
            var fallback = Path.Combine(localAppData, "Programs", "claude-voice");
            if (found == null || string.IsNullOrWhiteSpace(found.Root)) return fallback;
            try
            {
                var root = found.Root.TrimEnd('\\', '/');
                if (!string.Equals(Path.GetFileName(root), "app", StringComparison.OrdinalIgnoreCase)) return fallback;
                var parent = Path.GetDirectoryName(root);
                return !string.IsNullOrEmpty(parent) && fileExists(Path.Combine(parent!, "install.json")) ? parent! : fallback;
            }
            catch { return fallback; }
        }

        /// <summary>The command line for installer.py. A game install never touches Claude Code's settings.</summary>
        public static string InstallerArgs(string installerPy, string root, string source, string? engine, string? bigData, bool quiet)
        {
            var args = Quote(installerPy) + " install --root " + Quote(root) + " --source " + Quote(source) + " --no-claude";
            if (!string.IsNullOrWhiteSpace(engine)) args += " --engine " + engine!.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(bigData)) args += " --big-data " + Quote(bigData!);
            if (quiet) args += " --quiet";
            return args;
        }

        /// <summary>Unpacked from python.org's zip: everything but its 60 MB of HTML documentation.</summary>
        public static bool WantedFromRuntimeZip(string entryName)
        {
            var name = (entryName ?? string.Empty).Replace('\\', '/');
            return name.Length > 0 && !name.StartsWith("Doc/", StringComparison.OrdinalIgnoreCase)
                   && !name.StartsWith("/") && !name.Contains("../");
        }

        /// <summary>
        /// What to run to take claude-voice off quietly: its Settings → Apps QuietUninstallString, when that
        /// is claude-voice's own Python running installer.py (a 2.0 install). Null for an older install,
        /// whose entry runs PowerShell with its execution policy bypassed — the game does not start that;
        /// the player removes such a copy from Settings → Apps. Only when that Python is the one in
        /// <paramref name="programRoot"/>'s runtime: the entry must belong to the install the game found.
        /// </summary>
        public static (string Exe, string Args)? UninstallCommand(string? quietUninstall, string programRoot, Func<string, bool> fileExists)
        {
            var (exe, args) = VoiceAppLocator.SplitCommandLine(quietUninstall);
            if (exe.Length == 0 || string.IsNullOrWhiteSpace(programRoot)) return null;
            try
            {
                var runtime = Path.GetFullPath(Path.Combine(programRoot, "runtime")).TrimEnd('\\', '/');
                if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(exe)), runtime, StringComparison.OrdinalIgnoreCase)) return null;
            }
            catch { return null; }
            var leaf = Path.GetFileName(exe);
            if (!string.Equals(leaf, "pythonw.exe", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(leaf, "python.exe", StringComparison.OrdinalIgnoreCase)) return null;
            if (args.IndexOf(Installer, StringComparison.OrdinalIgnoreCase) < 0) return null;
            try { return fileExists(exe) ? (exe, args) : ((string, string)?)null; } catch { return null; }
        }

        /// <summary>A path as one argument: quoted, with no trailing backslash to escape the closing quote.
        /// A drive's root keeps its backslash, doubled before the quote: "D:" alone would mean that drive's
        /// current folder.</summary>
        public static string Quote(string path)
        {
            var trimmed = (path ?? string.Empty).TrimEnd('\\');
            return "\"" + trimmed + (trimmed.EndsWith(":") ? "\\\\" : string.Empty) + "\"";
        }
    }
}
