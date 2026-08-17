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

    /// <summary>
    /// How far the sampler may wander. STUDIO'S OWN VALUES, restored 2026.08.17.
    /// <para>
    /// These were cooled to 0.55 / 0.85 on 2026.08.14 for a real defect — the same embedding came
    /// back as a recognisably different person from one sentence to the next — but that defect was
    /// drift BETWEEN separate generations, and streaming made a whole reply ONE generation the very
    /// next day. The reason expired; the setting stayed.
    /// </para>
    /// <para>
    /// What put it back is a comparison nobody had to run, because Anton had already run it: the
    /// sister project (claude-voice) drives this same DLL on this same card at Studio's 0.9 / 1.0
    /// and derailed ONCE in about a thousand generations, where this mod derailed TWELVE times in
    /// 196 — six per cent against a tenth of one. Cooled sampling collapsing onto a repeated token
    /// is the textbook failure of an autoregressive decoder, and a repeated audio token is exactly
    /// what a held vowel sounds like.
    /// </para>
    /// <para>
    /// The ONE road that still wants cooling is <c>Delivery.ByLine</c>, which really is several
    /// generations and so really does have seams — and it is the legacy road, kept for comparison
    /// and not the default. Both numbers are settable (config.json, the command line, or the
    /// environment) so a player whose ear disagrees can put them back without a rebuild.
    /// </para>
    /// </summary>
    public float Temperature = 0.9f;   // --temperature    IMMERSIVEAI_TTS_TEMPERATURE
    public float TopP = 1.0f;          // --top-p          IMMERSIVEAI_TTS_TOP_P

    /// <summary>Run the errand instead of the engine: fetch the speech engine and its models into
    /// --engine-dir and --model-dir, report progress, and exit. No native library is loaded in this
    /// mode, so it cannot fail the way bringing the engine up can.</summary>
    public bool Fetch;                 // --fetch

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
            Temperature = Flt(Env("IMMERSIVEAI_TTS_TEMPERATURE"), 0.9f),
            TopP = Flt(Env("IMMERSIVEAI_TTS_TOP_P"), 1.0f),
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
                case "temperature" or "temp": o.Temperature = Flt(Take(ref i, next), 0.9f); break;
                case "top-p" or "topp": o.TopP = Flt(Take(ref i, next), 1.0f); break;
                case "keep-stderr": o.KeepStderr = true; break;
                case "fetch": o.Fetch = true; break;
                default: break;   // unknown switches are ignored, never fatal
            }
        }

        if (o.MaxAudioTokens < 256 || o.MaxAudioTokens > 65536) o.MaxAudioTokens = 4096;
        if (o.Threads < 0 || o.Threads > 256) o.Threads = 0;

        // Rails, not opinions: a temperature of zero is greedy decoding, which is the one setting
        // guaranteed to loop, and a top-p of zero would leave the sampler nothing to choose from.
        if (o.Temperature < 0.05f || o.Temperature > 2f) o.Temperature = 0.9f;
        if (o.TopP < 0.05f || o.TopP > 1f) o.TopP = 1.0f;
        return o;
    }

    private static float Flt(string? s, float fallback) =>
        float.TryParse(s, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;

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
        $"backend={Backend} threads={Threads} parentPid={ParentPid} maxAudioTokens={MaxAudioTokens} " +
        $"temperature={Temperature} topP={TopP} keepStderr={KeepStderr} fetch={Fetch}";
}
