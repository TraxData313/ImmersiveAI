using System.Runtime.InteropServices;

namespace ImmersiveAI.VoiceHost;

/// <summary>
/// Reproduces the load order that Qwen TTS Studio's own ensureNativeLoaded uses:
/// CUDA runtime first (cudart / cublasLt / cublas), then ggml-base, then a ggml
/// backend, then ggml, then the main library. Getting this wrong shows up as a
/// bare DllNotFoundException with no useful text.
/// </summary>
public static class Loader
{
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string path);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string path);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool AddDllDirectory(string path);

    /// <summary>The main library; its presence is what marks an engine folder.</summary>
    public const string MainLibrary = "qwen3_tts.dll";

    // Order matters. cudart before cublas*; ggml-base before the backends.
    // The CUDA trio is version-stamped (cudart64_13.dll today), so those three
    // are matched by pattern rather than by exact name - a CUDA 12 or 14 build
    // of the same engine must still come up.
    private static readonly string[] CudaPatterns = { "cudart64_*.dll", "cublasLt64_*.dll", "cublas64_*.dll" };
    private static readonly string[] Order = { "ggml-base.dll", "ggml-cpu.dll", "ggml-cuda.dll", "ggml.dll", MainLibrary };

    private static bool _done;

    /// <summary>
    /// Puts <paramref name="engineDir"/> on the loader's search path and preloads the
    /// siblings in dependency order. Missing optional siblings (no CUDA on this box)
    /// are logged and skipped; only a missing qwen3_tts.dll is fatal.
    /// </summary>
    public static void Preload(string engineDir)
    {
        if (_done) return;

        if (!Directory.Exists(engineDir))
            throw new DirectoryNotFoundException("engine folder not found: " + engineDir);
        if (!File.Exists(Path.Combine(engineDir, MainLibrary)))
            throw new FileNotFoundException("no " + MainLibrary + " in " + engineDir);

        if (!SetDllDirectory(engineDir))
            HostLog.Warn($"SetDllDirectory failed (win32 {Marshal.GetLastWin32Error()}) for {engineDir}");
        AddDllDirectory(engineDir);
        Environment.SetEnvironmentVariable("PATH",
            engineDir + ";" + Environment.GetEnvironmentVariable("PATH"));

        foreach (var name in Enumerate(engineDir))
        {
            string full = Path.Combine(engineDir, name);
            if (!File.Exists(full)) { HostLog.Info($"  [load] missing  {name}"); continue; }
            IntPtr h = LoadLibraryW(full);
            if (h == IntPtr.Zero)
                HostLog.Warn($"  [load] FAILED   {name}  (win32 {Marshal.GetLastWin32Error()})");
            else
                HostLog.Info($"  [load] ok       {name}");
        }

        _done = true;
    }

    private static IEnumerable<string> Enumerate(string engineDir)
    {
        foreach (var pattern in CudaPatterns)
        {
            string[] hits;
            try { hits = Directory.GetFiles(engineDir, pattern); }
            catch (Exception ex) { HostLog.Warn($"  [load] scan {pattern}: {ex.Message}"); continue; }
            Array.Sort(hits, StringComparer.OrdinalIgnoreCase);
            foreach (var h in hits) yield return Path.GetFileName(h);
        }
        foreach (var name in Order) yield return name;
    }
}
