using System.Runtime.InteropServices;
using System.Text;

namespace ImmersiveAI.VoiceHost;

// ---------------------------------------------------------------------------
// The C ABI of qwen3_tts.dll. Derived from the disassembly of the DLL's own JNI
// wrappers and then PROVEN by the M0 harness (200 consecutive syntheses, zero
// failures, no drift). Signatures and struct layouts below are load-bearing:
// copy them, never "tidy" them. A wrong field offset does not throw, it
// silently reads the wrong memory.
// ---------------------------------------------------------------------------

/// <summary>Return value of every synthesize_* call. 40 bytes (0x28).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct QwenResult
{
    public IntPtr Audio;       // 0x00  float*  (mono float32)
    public int NumSamples;     // 0x08  int32
    public int SampleRate;     // 0x0C  int32
    public int Success;        // 0x10  int32 (nonzero == ok)
    public int Reserved14;     // 0x14  (padding / unread by the JNI shim)
    public IntPtr Error;       // 0x18  const char* (UTF-8, owned by the result)
    public long TimeMs;        // 0x20  int64
}

/// <summary>
/// The params block. The non-streaming calls read the first 64 bytes (0x40);
/// the streaming calls read 80 (0x50) - the extra 16 carry the chunking knobs.
/// We always pass 80 zeroed bytes, which is safe for both.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct QwenParams
{
    public int MaxAudioTokens;        // 0x00  Studio default 4096
    public float Temperature;         // 0x04  Studio hardcodes 0.9f
    public float TopP;                // 0x08  Studio hardcodes 1.0f
    public int TopK;                  // 0x0C  Studio hardcodes 50
    public int Threads;               // 0x10  Studio hardcodes 4
    public int Unknown14;             // 0x14  Studio writes 0
    public int Unknown18;             // 0x18  Studio writes 1
    public float Unknown1C;           // 0x1C  Studio writes 1.05f (repetition penalty?)
    public int LanguageId;            // 0x20  -1 = auto; en=2050 ru=2069 ...
    public int Unknown24;             // 0x24  Studio leaves UNINITIALISED; we zero it
    public IntPtr Instruction;        // 0x28  const char* or null
    public IntPtr Speaker;            // 0x30  const char* or null
    public float Unknown38;           // 0x38  Studio writes 2.0f
    public int Unknown3C;             // 0x3C  Studio leaves UNINITIALISED; we zero it
    public float ChunkSeconds;        // 0x40  streaming only
    public float LeftContextSeconds;  // 0x44  streaming only
    public int CollectAudio;          // 0x48  streaming only
    public int Pad4C;                 // 0x4C

    public static QwenParams StudioDefaults(int languageId = -1, int maxAudioTokens = 4096)
        => new QwenParams
        {
            MaxAudioTokens = maxAudioTokens,
            Temperature = 0.9f,
            TopP = 1.0f,
            TopK = 50,
            Threads = 4,
            Unknown14 = 0,
            Unknown18 = 1,
            Unknown1C = 1.05f,
            LanguageId = languageId,
            Unknown24 = 0,
            Instruction = IntPtr.Zero,
            Speaker = IntPtr.Zero,
            Unknown38 = 2.0f,
            Unknown3C = 0,
            ChunkSeconds = 0f,
            LeftContextSeconds = 0f,
            CollectAudio = 0,
            Pad4C = 0,
        };
}

/// <summary>28 bytes, returned through a hidden pointer in RCX.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct QwenCaps
{
    public int Loaded;                 // 0x00
    public int SupportsCloning;        // 0x04
    public int SupportsNamedSpeakers;  // 0x08
    public int SupportsInstruction;    // 0x0C
    public int SpeakerEmbeddingDim;    // 0x10
    public int SpeakerCount;           // 0x14
    public int ModelKind;              // 0x18  0 unknown 1 base 2 customvoice 3 voicedesign
}

public static unsafe class Native
{
    public const string DLL = "qwen3_tts";

    public const int BACKEND_AUTO = 0;
    public const int BACKEND_CPU = 1;
    public const int BACKEND_CUDA = 2;

    // ---- lifecycle -------------------------------------------------------
    [DllImport(DLL, EntryPoint = "qwen3_tts_init", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Init();

    [DllImport(DLL, EntryPoint = "qwen3_tts_free", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Free(IntPtr ctx);

    [DllImport(DLL, EntryPoint = "qwen3_tts_load_models_with_name", CallingConvention = CallingConvention.Cdecl)]
    public static extern int LoadModelsWithName(IntPtr ctx, byte[]? modelDir, byte[]? modelName);

    [DllImport(DLL, EntryPoint = "qwen3_tts_load_models", CallingConvention = CallingConvention.Cdecl)]
    public static extern int LoadModels(IntPtr ctx, byte[]? modelDir);

    // ---- backend / threads ----------------------------------------------
    [DllImport(DLL, EntryPoint = "qwen3_tts_set_backend_preference", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetBackendPreference(int pref);

    [DllImport(DLL, EntryPoint = "qwen3_tts_get_compiled_backend_mask", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetCompiledBackendMask();

    [DllImport(DLL, EntryPoint = "qwen3_tts_get_active_backend_name", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetActiveBackendNameRaw();

    [DllImport(DLL, EntryPoint = "qwen3_tts_set_cpu_threads", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetCpuThreads(int threads);

    [DllImport(DLL, EntryPoint = "qwen3_tts_get_cpu_threads", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetCpuThreads();

    // ---- introspection ---------------------------------------------------
    [DllImport(DLL, EntryPoint = "qwen3_tts_get_last_error", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetLastErrorRaw(IntPtr ctx);

    [DllImport(DLL, EntryPoint = "qwen3_tts_get_available_speakers", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetAvailableSpeakersRaw(IntPtr ctx);

    [DllImport(DLL, EntryPoint = "qwen3_tts_get_model_capabilities", CallingConvention = CallingConvention.Cdecl)]
    public static extern QwenCaps GetModelCapabilities(IntPtr ctx);

    // ---- freeing ---------------------------------------------------------
    [DllImport(DLL, EntryPoint = "qwen3_tts_free_string", CallingConvention = CallingConvention.Cdecl)]
    public static extern void FreeString(IntPtr s);

    [DllImport(DLL, EntryPoint = "qwen3_tts_free_result", CallingConvention = CallingConvention.Cdecl)]
    public static extern void FreeResult(ref QwenResult r);

    // ---- synthesis (natural declaration: CLR emits the hidden RCX itself) -
    [DllImport(DLL, EntryPoint = "qwen3_tts_synthesize_with_icl_prompt", CallingConvention = CallingConvention.Cdecl)]
    public static extern QwenResult SynthIcl(IntPtr ctx, byte[]? text, byte[]? iclPath, ref QwenParams p);

    [DllImport(DLL, EntryPoint = "qwen3_tts_synthesize_with_speaker_embedding", CallingConvention = CallingConvention.Cdecl)]
    public static extern QwenResult SynthEmbedding(IntPtr ctx, byte[]? text, byte[]? embeddingPath, ref QwenParams p);

    // ---- streaming: ONE generation that reports its audio as it is made -------------------
    //
    // This is the road the mod actually takes for a spoken reply, and the reason is not only
    // latency. Synthesizing a reply sentence by sentence means a separate generation per
    // sentence, and the model rolls its own prosody afresh each time - so the same voice came
    // back as a recognisably different person at every seam (playtest, 2026.08.14). Streaming
    // is a SINGLE generation whose audio is handed over in pieces, so the timbre is continuous
    // by construction. It is also far faster: the prefill and the speaker encode happen once
    // instead of once per sentence, measured at 3.60x realtime against 0.34x for the same words
    // cut into sentences, on the same card with the game running.
    //
    // Callback shape is decompile-confirmed, not guessed: the DLL's JNI trampoline at rva
    // 0x28f30 receives exactly (chunk*, user_data); the chunk offsets come from sub_29600.
    // Return nonzero to continue, zero to ask the engine to stop early.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int QwenChunkCallback(IntPtr chunk, IntPtr userData);

    [StructLayout(LayoutKind.Sequential)]
    public struct QwenChunk
    {
        public IntPtr Audio;          // 0x00  float*
        public int NumSamples;        // 0x08
        public int SampleRate;        // 0x0C
        public long StartSample;      // 0x10
        public long EndSample;        // 0x18
        public int StartFrame;        // 0x20
        public int EndFrame;          // 0x24
        public int StartTextByte;     // 0x28
        public int EndTextByte;       // 0x2C
        public int TextAlignmentKind; // 0x30
        public float Confidence;      // 0x34
    }

    [DllImport(DLL, EntryPoint = "qwen3_tts_synthesize_with_speaker_embedding_streaming", CallingConvention = CallingConvention.Cdecl)]
    public static extern QwenResult SynthEmbeddingStreaming(IntPtr ctx, byte[]? text, byte[]? embeddingPath,
                                                            ref QwenParams p, QwenChunkCallback cb, IntPtr userData);

    [DllImport(DLL, EntryPoint = "qwen3_tts_synthesize_with_icl_prompt_streaming", CallingConvention = CallingConvention.Cdecl)]
    public static extern QwenResult SynthIclStreaming(IntPtr ctx, byte[]? text, byte[]? iclPath,
                                                      ref QwenParams p, QwenChunkCallback cb, IntPtr userData);

    [DllImport(DLL, EntryPoint = "qwen3_tts_synthesize_streaming", CallingConvention = CallingConvention.Cdecl)]
    public static extern QwenResult SynthStreaming(IntPtr ctx, byte[]? text, ref QwenParams p,
                                                   QwenChunkCallback cb, IntPtr userData);

    [DllImport(DLL, EntryPoint = "qwen3_tts_synthesize", CallingConvention = CallingConvention.Cdecl)]
    public static extern QwenResult Synth(IntPtr ctx, byte[]? text, ref QwenParams p);

    // NOTE: FOUR args, not five. An earlier five-arg guess shifted the params
    // pointer onto the wrong stack slot and produced an AccessViolation.
    [DllImport(DLL, EntryPoint = "qwen3_tts_synthesize_with_voice", CallingConvention = CallingConvention.Cdecl)]
    public static extern QwenResult SynthWithVoice(IntPtr ctx, byte[]? text, byte[]? voiceOrRefWav, ref QwenParams p);

    // ---- helpers ---------------------------------------------------------
    public static byte[]? Utf8(string? s) =>
        s == null ? null : Encoding.UTF8.GetBytes(s + "\0");

    public static string? PtrToUtf8(IntPtr p)
    {
        if (p == IntPtr.Zero) return null;
        int len = 0;
        byte* b = (byte*)p;
        while (b[len] != 0 && len < 1 << 20) len++;
        return Encoding.UTF8.GetString(b, len);
    }

    public static string LastError(IntPtr ctx)
    {
        IntPtr p = GetLastErrorRaw(ctx);
        if (p == IntPtr.Zero) return "(no error string)";
        string s = PtrToUtf8(p) ?? "(no error string)";
        FreeString(p);
        return s;
    }

    public static string ActiveBackend()
    {
        IntPtr p = GetActiveBackendNameRaw();
        if (p == IntPtr.Zero) return "(null)";
        // NOTE: the JNI shim does NOT free this one, so neither do we.
        return PtrToUtf8(p) ?? "(null)";
    }

    public static string[] Speakers(IntPtr ctx)
    {
        IntPtr p = GetAvailableSpeakersRaw(ctx);
        if (p == IntPtr.Zero) return Array.Empty<string>();
        string? s = PtrToUtf8(p);
        FreeString(p);
        if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();
        return s!.Split(new[] { '\n', ',', ';', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
    }
}
