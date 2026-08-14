namespace ImmersiveAI.VoiceHost;

/// <summary>
/// Where the engine and its models actually live on THIS machine. Nothing is
/// hardcoded in shipped code: the studio writes a settings file in the user's
/// profile, and beyond that we look in the handful of places such an app is
/// normally unpacked. When none of it is found we say so plainly and the host
/// reports {"event":"failed"} - the game simply goes on without voices.
/// </summary>
public sealed class EngineLocation
{
    public required string EngineDir { get; init; }   // holds qwen3_tts.dll + ggml siblings
    public required string ModelDir { get; init; }    // holds the *.gguf files
    public required string ModelName { get; init; }   // the talker gguf to load
    public string? AppDir { get; init; }              // the studio's data folder, if we found one
}

public static class EngineDiscovery
{
    public static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     ".qwen-tts-studio", "settings.properties");

    public static EngineLocation Locate(HostOptions o)
    {
        var settings = ReadSettings(SettingsPath);
        string? appDir = Clean(settings.GetValueOrDefault("appdir"));
        if (appDir is not null) HostLog.Info($"settings.properties appDir={appDir}");

        string engineDir = FindEngineDir(o.EngineDir, appDir)
            ?? throw new FileNotFoundException(
                "the Qwen TTS engine was not found (" + Loader.MainLibrary + "). " +
                "Point the host at it with --engine-dir or IMMERSIVEAI_TTS_ENGINE_DIR.");

        string modelDir = FindModelDir(o.ModelDir, settings, appDir, engineDir)
            ?? throw new DirectoryNotFoundException(
                "no folder of *.gguf voice models was found. " +
                "Point the host at it with --model-dir or IMMERSIVEAI_TTS_MODEL_DIR.");

        string modelName = FindModel(o.ModelName, settings, modelDir)
            ?? throw new FileNotFoundException("no talker model (*.gguf) in " + modelDir);

        return new EngineLocation
        {
            EngineDir = engineDir,
            ModelDir = modelDir,
            ModelName = modelName,
            AppDir = appDir,
        };
    }

    // ---------------------------------------------------------------- engine

    private static string? FindEngineDir(string? explicitDir, string? appDir)
    {
        foreach (var c in EngineCandidates(explicitDir, appDir))
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            try
            {
                if (File.Exists(Path.Combine(c, Loader.MainLibrary)))
                {
                    HostLog.Info("engine folder: " + c);
                    return Path.GetFullPath(c);
                }
            }
            catch { /* an unreadable candidate is simply not the one */ }
        }
        return null;
    }

    private static IEnumerable<string> EngineCandidates(string? explicitDir, string? appDir)
    {
        if (!string.IsNullOrWhiteSpace(explicitDir)) yield return explicitDir!;

        // Beside our own exe, and the "engine" folder a deploy script would use.
        string? here = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(here))
        {
            yield return here!;
            yield return Path.Combine(here!, "engine");
            yield return Path.Combine(here!, "qwen-tts-studio");
        }

        // The studio's own data folder, and the places it keeps binaries.
        if (!string.IsNullOrWhiteSpace(appDir))
        {
            yield return appDir!;
            yield return Path.Combine(appDir!, "app");
            yield return Path.Combine(appDir!, "bin");
            yield return Path.Combine(appDir!, "runtime");
        }

        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var root in new[]
                 {
                     Path.Combine(profile, "Downloads"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Path.Combine(profile, "Desktop"),
                 })
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            yield return Path.Combine(root, "qwen-tts-studio");
            // one shallow sweep for a folder whose name mentions qwen
            string[] subs;
            try { subs = Directory.Exists(root) ? Directory.GetDirectories(root, "*qwen*") : Array.Empty<string>(); }
            catch { subs = Array.Empty<string>(); }
            foreach (var s in subs) yield return s;
        }

        // Finally, anything already on PATH.
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var part in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
            yield return part.Trim().Trim('"');
    }

    // ---------------------------------------------------------------- models

    private static string? FindModelDir(string? explicitDir, Dictionary<string, string> settings,
                                        string? appDir, string engineDir)
    {
        foreach (var c in new[]
                 {
                     explicitDir,
                     Clean(settings.GetValueOrDefault("modeldir")),
                     appDir is null ? null : Path.Combine(appDir, "models"),
                     Path.Combine(engineDir, "models"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                  ".qwen-tts-studio", "models"),
                 })
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            try
            {
                if (Directory.Exists(c) && Directory.GetFiles(c!, "*.gguf").Length > 0)
                {
                    HostLog.Info("model folder: " + c);
                    return Path.GetFullPath(c!);
                }
            }
            catch { /* not this one */ }
        }
        return null;
    }

    private static string? FindModel(string? explicitName, Dictionary<string, string> settings, string modelDir)
    {
        foreach (var wanted in new[] { explicitName, Clean(settings.GetValueOrDefault("modelname")) })
        {
            if (string.IsNullOrWhiteSpace(wanted)) continue;
            string name = Path.GetFileName(wanted!);
            if (File.Exists(Path.Combine(modelDir, name)))
            {
                HostLog.Info("model: " + name);
                return name;
            }
            HostLog.Warn($"model '{name}' is not in {modelDir}; picking the best one present");
        }

        string[] all;
        try { all = Directory.GetFiles(modelDir, "*.gguf"); }
        catch { return null; }

        // The talker speaks; the tokenizer is its companion file, never the model to load.
        var talkers = all.Select(Path.GetFileName)
                         .Where(n => n is not null)
                         .Select(n => n!)
                         .Where(n => n.IndexOf("tokenizer", StringComparison.OrdinalIgnoreCase) < 0)
                         .OrderByDescending(n => n.IndexOf("1.7b", StringComparison.OrdinalIgnoreCase) >= 0)
                         .ThenByDescending(n => n.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0)
                         .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
                         .ToList();
        if (talkers.Count == 0) return null;
        HostLog.Info("model (chosen): " + talkers[0]);
        return talkers[0];
    }

    // ------------------------------------------------------------- settings

    /// <summary>
    /// java.util.Properties, as the studio writes it: '#' comments, key=value,
    /// and backslash escapes on the value side (C\:\\Users\\... is C:\Users\...).
    /// Keys come back lowercased. Never throws.
    /// </summary>
    public static Dictionary<string, string> ReadSettings(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(path)) { HostLog.Info("no settings.properties at " + path); return map; }
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == '!') continue;
                int eq = IndexOfSeparator(line);
                if (eq <= 0) continue;
                string key = Unescape(line.Substring(0, eq)).Trim().ToLowerInvariant();
                string value = Unescape(line.Substring(eq + 1)).Trim();
                if (key.Length > 0) map[key] = value;
            }
        }
        catch (Exception ex) { HostLog.Warn("settings.properties unreadable: " + ex.Message); }
        return map;
    }

    private static int IndexOfSeparator(string line)
    {
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '\\') { i++; continue; }          // escaped, skip the next char
            if (line[i] == '=' || line[i] == ':') return i;
        }
        return -1;
    }

    private static string Unescape(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '\\') { sb.Append(c); continue; }
            if (++i >= s.Length) break;
            char n = s[i];
            sb.Append(n switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => n });
        }
        return sb.ToString();
    }

    private static string? Clean(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s!.Trim().Trim('"');
}
