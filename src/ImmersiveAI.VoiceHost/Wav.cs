using System.Runtime.InteropServices;
using System.Text;

namespace ImmersiveAI.VoiceHost;

public static class Wav
{
    /// <summary>Copy the native float32 buffer out of the result.</summary>
    public static float[] Samples(in QwenResult r)
    {
        if (r.Audio == IntPtr.Zero || r.NumSamples <= 0) return Array.Empty<float>();
        var a = new float[r.NumSamples];
        Marshal.Copy(r.Audio, a, 0, r.NumSamples);
        return a;
    }

    /// <summary>
    /// 16-bit PCM mono WAV, written to a sibling .part file and moved into place,
    /// so the game can never open a half-written file. Returns the final path.
    /// </summary>
    /// <summary>
    /// Lifts a line to a normal speaking loudness before it is written.
    /// <para>
    /// The engine hands back very quiet audio — measured across a live playtest (2026.08.14) it
    /// peaked between 0.077 and 0.299 with an RMS around 0.02, which is roughly 10-20 dB under what
    /// a voice track should sit at. In a silent test that merely sounds soft; over a battle, a
    /// tavern and the game's own score it is inaudible, and the player reasonably concludes the
    /// feature is broken rather than quiet.
    /// </para>
    /// <para>
    /// Peak normalisation, not compression: the loudest sample is brought to <see cref="TargetPeak"/>
    /// and everything scales with it, so the performance is untouched — the shape of the sentence,
    /// its breaths and its emphasis all survive exactly. The ceiling is under 1.0 to leave room for
    /// the 16-bit rounding below, and a gain limit stops a near-silent clip from being amplified
    /// into a wall of hiss. Silence is left silent.
    /// </para>
    /// </summary>
    private const float TargetPeak = 0.89f;
    private const float MaxGain = 12f;
    private const float SilenceFloor = 1e-4f;

    internal static float Normalize(float[] s)
    {
        float gain = GainFor(s);
        ApplyGain(s, gain);
        return gain;
    }

    /// <summary>The gain this audio wants, without touching it. Split out from
    /// <see cref="Normalize"/> so a streamed reply can decide ONCE, on its first piece, and hold
    /// that figure for every piece after — normalising each piece to its own peak would make the
    /// loudness swell and fade sentence by sentence.</summary>
    internal static float GainFor(float[] s)
    {
        if (s == null || s.Length == 0) return 1f;

        float peak = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float a = Math.Abs(s[i]);
            if (float.IsNaN(a) || float.IsInfinity(a)) continue;
            if (a > peak) peak = a;
        }

        if (peak <= SilenceFloor) return 1f;            // nothing there to lift
        float gain = TargetPeak / peak;
        if (gain > MaxGain) gain = MaxGain;             // do not amplify a whisper into noise
        return gain;
    }

    /// <summary>Applies a gain, clamping to the rails. The clamp matters for a streamed reply: the
    /// gain was chosen from the first piece, and a later, louder piece would otherwise wrap around
    /// into a crack of noise instead of merely touching the ceiling.</summary>
    internal static void ApplyGain(float[] s, float gain)
    {
        if (s == null || s.Length == 0) return;
        if (gain > 0.999f && gain < 1.001f) return;     // already where it should be

        for (int i = 0; i < s.Length; i++)
        {
            float v = s[i] * gain;
            if (v > 1f) v = 1f;
            else if (v < -1f) v = -1f;
            s[i] = v;
        }
    }

    public static void WritePcm16Atomic(string path, float[] s, int rate)
    {
        string full = Path.GetFullPath(path);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);

        string tmp = full + "." + Environment.ProcessId + ".part";
        try
        {
            WritePcm16(tmp, s, rate);
            // Move(overwrite) is atomic enough on NTFS: the reader sees either the
            // old file or the whole new one, never a growing one.
            File.Move(tmp, full, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>16-bit PCM mono WAV - the most widely playable container.</summary>
    public static void WritePcm16(string path, float[] s, int rate)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs, Encoding.ASCII);
        int dataBytes = s.Length * 2;
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataBytes);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);      // PCM
        w.Write((short)1);      // mono
        w.Write(rate);
        w.Write(rate * 2);      // byte rate
        w.Write((short)2);      // block align
        w.Write((short)16);     // bits
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataBytes);
        foreach (var f in s)
        {
            float v = f;
            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
            if (v > 1f) v = 1f;
            if (v < -1f) v = -1f;
            w.Write((short)Math.Round(v * 32767f));
        }
    }

    /// <summary>Cheap sanity metrics for the log: is this speech, silence, or noise?</summary>
    public static string Describe(float[] s, int rate)
    {
        if (s.Length == 0) return "EMPTY";
        if (rate <= 0) rate = 24000;
        double peak = 0, sum = 0;
        int zc = 0, clipped = 0, nan = 0;
        float prev = 0;
        foreach (var f in s)
        {
            if (float.IsNaN(f) || float.IsInfinity(f)) { nan++; continue; }
            float a = Math.Abs(f);
            if (a > peak) peak = a;
            if (a >= 0.999f) clipped++;
            sum += (double)f * f;
            if ((f >= 0) != (prev >= 0)) zc++;
            prev = f;
        }
        double rms = Math.Sqrt(sum / s.Length);
        double zcr = (double)zc / s.Length * rate;
        string verdict = rms < 1e-4 ? "SILENT"
                       : zcr > rate * 0.30 ? "NOISE-LIKE"
                       : "speech-like";
        return $"peak={peak:F3} rms={rms:F4} zcr={zcr:F0}/s clipped={clipped} nan={nan} => {verdict}";
    }
}
