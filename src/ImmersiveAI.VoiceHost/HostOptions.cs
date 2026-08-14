namespace ImmersiveAI.VoiceHost;

/// <summary>
/// Everything the host can be told, from argv or from the environment. argv wins.
/// Nothing here is required: with no arguments at all the host discovers the
/// engine, picks a model and talks the protocol - which is what makes it easy to
/// drive by hand when something needs proving.
/// </summary>
public sealed class HostOptions
{
    public string? EngineDir;          // --engine-dir     IMMERSIVEAI_TTS_ENGINE_DIR
    public string? ModelDir;           // --model-dir      IMMERSIVEAI_TTS_MODEL_DIR
    public string? ModelName;          // --model          IMMERSIVEAI_TTS_MODEL
    public string Backend = "auto";    // --backend        IMMERSIVEAI_TTS_BACKEND   auto|cpu|cuda
    public int ParentPid;              // --parent         IMMERSIVEAI_TTS_PARENT_PID
    public int Threads;                // --threads        IMMERSIVEAI_TTS_THREADS   0 = engine default
    public string? LogPath;            // --log            IMMERSIVEAI_TTS_LOG
    public int MaxAudioTokens = 4096;  // --max-audio-tokens
    public bool KeepStderr;            // --keep-stderr    (default: stderr -> NUL, see StdoutGuard)

    public int BackendCode => Backend?.Trim().ToLowerInvariant() switch
    {
        "cpu" => Native.BACKEND_CPU,
        "cuda" or "gpu" => Native.BACKEND_CUDA,
        _ => Native.BACKEND_AUTO,
    };

    public static HostOptions Parse(string[] argv)
    {
        var o = new HostOptions
        {
            EngineDir = Env("IMMERSIVEAI_TTS_ENGINE_DIR"),
            ModelDir = Env("IMMERSIVEAI_TTS_MODEL_DIR"),
            ModelName = Env("IMMERSIVEAI_TTS_MODEL"),
            Backend = Env("IMMERSIVEAI_TTS_BACKEND") ?? "auto",
            LogPath = Env("IMMERSIVEAI_TTS_LOG"),
            ParentPid = Int(Env("IMMERSIVEAI_TTS_PARENT_PID"), 0),
            Threads = Int(Env("IMMERSIVEAI_TTS_THREADS"), 0),
        };

        for (int i = 0; i < argv.Length; i++)
        {
            string a = argv[i];
            string? next = i + 1 < argv.Length ? argv[i + 1] : null;
            switch (a.TrimStart('-').ToLowerInvariant())
            {
                case "engine-dir" or "enginedir": o.EngineDir = Take(ref i, next); break;
                case "model-dir" or "modeldir": o.ModelDir = Take(ref i, next); break;
                case "model" or "model-name": o.ModelName = Take(ref i, next); break;
                case "backend": o.Backend = Take(ref i, next) ?? "auto"; break;
                case "parent" or "parent-pid" or "ppid": o.ParentPid = Int(Take(ref i, next), 0); break;
                case "threads": o.Threads = Int(Take(ref i, next), 0); break;
                case "log" or "log-path": o.LogPath = Take(ref i, next); break;
                case "max-audio-tokens": o.MaxAudioTokens = Int(Take(ref i, next), 4096); break;
                case "keep-stderr": o.KeepStderr = true; break;
                default: break;   // unknown switches are ignored, never fatal
            }
        }

        if (o.MaxAudioTokens < 256 || o.MaxAudioTokens > 65536) o.MaxAudioTokens = 4096;
        if (o.Threads < 0 || o.Threads > 256) o.Threads = 0;
        return o;
    }

    private static string? Take(ref int i, string? next)
    {
        if (next is null || next.StartsWith("--", StringComparison.Ordinal)) return null;
        i++;
        return next;
    }

    private static string? Env(string name)
    {
        string? v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static int Int(string? s, int fallback) =>
        int.TryParse(s, out int v) ? v : fallback;

    public override string ToString() =>
        $"engineDir={EngineDir ?? "(discover)"} modelDir={ModelDir ?? "(discover)"} model={ModelName ?? "(discover)"} " +
        $"backend={Backend} threads={Threads} parentPid={ParentPid} maxAudioTokens={MaxAudioTokens} keepStderr={KeepStderr}";
}
