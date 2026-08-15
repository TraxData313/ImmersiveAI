using System.Buffers;
using System.Text;
using System.Text.Json;

namespace ImmersiveAI.VoiceHost;

public enum VoiceKind { Default, Icl, Embedding, Speaker }

/// <summary>One "synthesize" line, already parsed and sanity-checked.</summary>
public sealed class SynthRequest
{
    public string Id = "";
    public string Text = "";
    public string OutPath = "";
    public VoiceKind Kind = VoiceKind.Default;
    public string? VoicePath;
    public string? Speaker;
    public int LanguageId = -1;

    /// <summary>True: hand the reply back as ONE file, not as pieces. The generation is streamed
    /// either way — that is what keeps the voice one person and makes it fast — but the audio is
    /// accumulated and written once at the end. The game asks for this when it would rather wait
    /// than risk a seam: playing N files means chaining them on the game's tick, and a frame of
    /// silence between every second of speech is heard as the voice breaking up.</summary>
    public bool Whole;

    /// <summary>
    /// The audio-token ceiling for THIS line, worked out by the game from the line's own length
    /// (Core's VoiceBudget). 0 = whatever the host was started with.
    /// <para>
    /// This is the anti-derail rail, and it is per-request because a ceiling that suits a session
    /// suits no sentence in it. Measured 2026.08.15 on qwen-talker-1.7b-base: one audio token is
    /// exactly 1920 samples at 24 kHz — 80 ms — and the ceiling is honoured to the sample (256 tokens
    /// came back as 20.48 s twice, from different texts). The engine's own default of 4096 is
    /// therefore 327.68 s, which is precisely where a real derail stopped that same evening: 202
    /// characters of Bulgarian became five and a half minutes of noise.
    /// </para>
    /// </summary>
    public int MaxTokens;

    public override string ToString() =>
        $"id={Id} kind={Kind} voice={(VoicePath is null ? Speaker ?? "-" : Path.GetFileName(VoicePath))} " +
        $"lang={LanguageId} whole={Whole} maxTokens={(MaxTokens > 0 ? MaxTokens.ToString() : "host")} " +
        $"chars={Text.Length} out={OutPath}";
}

/// <summary>
/// The wire: newline-delimited JSON, one compact object per line, UTF-8.
/// Writing is serialised by a lock - the reader thread answers pings while the
/// worker thread answers syntheses, and two half-lines would desynchronise the
/// game for the rest of the session.
/// </summary>
public static class Wire
{
    private static Stream _out = Stream.Null;
    private static readonly object WriteGate = new();

    public static void Init(Stream stdout) => _out = stdout;

    // ------------------------------------------------------------- outgoing

    public static void Ready(string backend, string model, int rate) =>
        Send(w =>
        {
            w.WriteString("event", "ready");
            w.WriteString("backend", backend);
            w.WriteString("model", model);
            w.WriteNumber("rate", rate);
        });

    public static void Failed(string error) =>
        Send(w =>
        {
            w.WriteString("event", "failed");
            w.WriteString("error", Short(error));
        });

    public static void Pong() => Send(w => w.WriteString("event", "pong"));

    /// <summary>One piece of a streamed reply is on disk and can be played NOW. Several of these
    /// arrive per request, in order, each followed eventually by the usual Ok that closes it out.
    /// The game plays them as they land; that is what makes a long reply start speaking in under a
    /// second instead of after the whole thing has been generated.</summary>
    public static void Chunk(string id, int index, string path, int samples, int rate) =>
        Send(w =>
        {
            w.WriteString("id", id);
            w.WriteNumber("chunk", index);
            w.WriteString("path", path);
            w.WriteNumber("samples", samples);
            w.WriteNumber("rate", rate);
        });

    /// <summary><paramref name="derailed"/> says "play this if you like, but do not KEEP it": the
    /// generation was cut short for running away, so the words are good up to the cut and the clip
    /// must never reach the cache — one bad synthesis replayed forever is far worse than one bad
    /// synthesis.</summary>
    public static void Ok(string id, string path, long ms, int samples, int rate, bool derailed = false) =>
        Send(w =>
        {
            w.WriteString("id", id);
            w.WriteBoolean("ok", true);
            w.WriteString("path", path);
            w.WriteNumber("ms", ms);
            w.WriteNumber("samples", samples);
            w.WriteNumber("rate", rate);
            if (derailed) w.WriteBoolean("derailed", true);
        });

    public static void Fail(string? id, string error) =>
        Send(w =>
        {
            if (!string.IsNullOrEmpty(id)) w.WriteString("id", id!);
            w.WriteBoolean("ok", false);
            w.WriteString("error", Short(error));
        });

    private static string Short(string s)
    {
        s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length <= 500 ? s : s.Substring(0, 500) + "...";
    }

    private static void Send(Action<Utf8JsonWriter> body)
    {
        try
        {
            var buffer = new ArrayBufferWriter<byte>(256);
            using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
            {
                w.WriteStartObject();
                body(w);
                w.WriteEndObject();
            }
            lock (WriteGate)
            {
                _out.Write(buffer.WrittenSpan);
                _out.WriteByte((byte)'\n');
                _out.Flush();
            }
        }
        catch (Exception ex)
        {
            // If we cannot answer, the game's own timeout takes over; dying here
            // would strand the engine.
            HostLog.Error("could not write to the protocol channel", ex);
        }
    }

    // ------------------------------------------------------------- incoming

    /// <summary>
    /// Parses one line. Returns null for anything unusable, having already logged
    /// and (where an answer is owed) replied. A malformed line is never fatal.
    /// </summary>
    public static JsonDocument? TryParse(string line)
    {
        try { return JsonDocument.Parse(line); }
        catch (Exception ex)
        {
            HostLog.Warn($"malformed line skipped ({ex.Message}): {Preview(line)}");
            return null;
        }
    }

    public static string Preview(string s)
    {
        s = s.Replace("\r", "").Replace("\n", " ");
        return s.Length <= 200 ? s : s.Substring(0, 200) + "...";
    }

    public static string? Str(JsonElement o, string name)
    {
        if (o.ValueKind != JsonValueKind.Object) return null;
        if (!o.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.ToString(),
            _ => null,
        };
    }

    public static bool Bool(JsonElement o, string name, bool fallback)
    {
        if (!o.TryGetProperty(name, out var v)) return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => v.TryGetInt32(out var n) && n != 0,
            JsonValueKind.String => bool.TryParse(v.GetString(), out var b) ? b : fallback,
            _ => fallback,
        };
    }

    public static int Int(JsonElement o, string name, int fallback)
    {
        if (o.ValueKind != JsonValueKind.Object) return fallback;
        if (!o.TryGetProperty(name, out var v)) return fallback;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int m)) return m;
        return fallback;
    }

    public static string Utf8Line(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
