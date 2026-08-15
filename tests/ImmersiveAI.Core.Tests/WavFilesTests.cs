using System.Text;
using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// The joiner that makes streamed speech gapless: several one-second pieces poured into one sound,
/// so there is no seam to hear between them.
/// </summary>
public class WavFilesTests : IDisposable
{
    private readonly string _folder;

    public WavFilesTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "immersiveai-wav-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* a temp folder is not worth a failure */ }
    }

    // ------------------------------------------------------------------

    private string WriteWav(string name, int sampleRate, int channels, int bits, int sampleFrames)
    {
        var path = Path.Combine(_folder, name);
        var blockAlign = channels * bits / 8;
        var dataBytes = sampleFrames * blockAlign;

        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var w = new BinaryWriter(fs, Encoding.ASCII))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(sampleRate);
            w.Write(sampleRate * blockAlign);
            w.Write((short)blockAlign);
            w.Write((short)bits);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            for (var i = 0; i < dataBytes; i++) w.Write((byte)(i % 251));
        }
        return path;
    }

    // ------------------------------------------------------------------

    [Fact]
    public void TryRead_ReadsWhatTheHeaderSays()
    {
        var path = WriteWav("a.wav", 24000, 1, 16, 24000);       // exactly one second
        var info = WavFiles.TryRead(path);

        Assert.NotNull(info);
        Assert.Equal(24000, info!.SampleRate);
        Assert.Equal(1, info.Channels);
        Assert.Equal(16, info.BitsPerSample);
        Assert.Equal(48000, info.DataBytes);
        Assert.Equal(1.0, info.Duration.TotalSeconds, 3);
    }

    [Fact]
    public void TryRead_AnswersNullForWhatIsNotAWav()
    {
        var path = Path.Combine(_folder, "not-a-wav.wav");
        File.WriteAllText(path, "these are not the bytes you are looking for");

        Assert.Null(WavFiles.TryRead(path));
        Assert.Null(WavFiles.TryRead(Path.Combine(_folder, "nothing-here.wav")));
        Assert.Null(WavFiles.TryReadDuration(Path.Combine(_folder, "nothing-here.wav")));
    }

    [Fact]
    public void TryRead_SurvivesAFileTruncatedMidWrite()
    {
        // The host writes atomically, but a crashed or half-copied file must not become an exception.
        var path = WriteWav("cut.wav", 24000, 1, 16, 24000);
        var bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes.Take(bytes.Length / 3).ToArray());

        var info = WavFiles.TryRead(path);
        // Either it reads only what is really there, or it refuses. Never a lie, never a throw.
        if (info != null) Assert.True(info.DataBytes <= bytes.Length);
    }

    // ---- the repair, and the silence that bought it -------------------------------------------

    /// <summary>Rewrites a size field in place, the way a streaming writer leaves it.</summary>
    private static void PokeUInt32(string path, int offset, uint value)
    {
        var bytes = File.ReadAllBytes(path);
        BitConverter.GetBytes(value).CopyTo(bytes, offset);
        File.WriteAllBytes(path, bytes);
    }

    private static uint ReadUInt32(string path, int offset)
        => BitConverter.ToUInt32(File.ReadAllBytes(path), offset);

    [Fact]
    public void TryRepairSizes_FixesTheStreamedPlaceholder()
    {
        // EXACTLY what a hosted speech service sends: a header written before the length was known,
        // carrying 0xFFFFFFFF. Our reader coped with it; the game's audio engine did not, and the
        // clip played as silence while every number we logged about it was correct.
        var path = WriteWav("streamed.wav", 24000, 1, 16, 24000);
        PokeUInt32(path, 40, 0xFFFFFFFF);            // data chunk size
        PokeUInt32(path, 4, 0xFFFFFFFF);             // riff size

        Assert.True(WavFiles.TryRepairSizes(path));

        var length = new FileInfo(path).Length;
        Assert.Equal((uint)(length - 8), ReadUInt32(path, 4));
        Assert.Equal((uint)(length - 44), ReadUInt32(path, 40));

        // And it still reads as the same second of audio it always was.
        var info = WavFiles.TryRead(path);
        Assert.NotNull(info);
        Assert.Equal(1.0, info!.Duration.TotalSeconds, 3);
    }

    [Fact]
    public void TryRepairSizes_LeavesAnHonestFileAlone()
    {
        var path = WriteWav("fine.wav", 24000, 1, 16, 12000);
        var before = File.ReadAllBytes(path);

        Assert.False(WavFiles.TryRepairSizes(path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void TryRepairSizes_RefusesWhatItCannotUnderstand()
    {
        var path = Path.Combine(_folder, "rubbish.wav");
        File.WriteAllText(path, "not a wav at all");

        Assert.False(WavFiles.TryRepairSizes(path));
        Assert.False(WavFiles.TryRepairSizes(Path.Combine(_folder, "absent.wav")));
    }

    [Fact]
    public void RepairedFilesStillJoin()
    {
        var a = WriteWav("000.wav", 24000, 1, 16, 24000);
        var b = WriteWav("001.wav", 24000, 1, 16, 24000);
        PokeUInt32(a, 40, 0xFFFFFFFF);
        WavFiles.TryRepairSizes(a);

        var joined = Path.Combine(_folder, "joined.wav");
        Assert.True(WavFiles.Join(new[] { a, b }, joined));
        Assert.Equal(2.0, WavFiles.TryRead(joined)!.Duration.TotalSeconds, 3);
    }

    [Fact]
    public void Join_PoursThePiecesIntoOne()
    {
        var a = WriteWav("000.wav", 24000, 1, 16, 24000);        // 1.0 s
        var b = WriteWav("001.wav", 24000, 1, 16, 12000);        // 0.5 s
        var c = WriteWav("002.wav", 24000, 1, 16, 6000);         // 0.25 s
        var joined = Path.Combine(_folder, "joined.wav");

        Assert.True(WavFiles.Join(new[] { a, b, c }, joined));

        var info = WavFiles.TryRead(joined);
        Assert.NotNull(info);
        Assert.Equal(24000, info!.SampleRate);
        Assert.Equal(1.75, info.Duration.TotalSeconds, 3);
        Assert.Equal(48000 + 24000 + 12000, info.DataBytes);
    }

    [Fact]
    public void Join_KeepsTheAudioItselfIntact()
    {
        var a = WriteWav("000.wav", 24000, 1, 16, 100);
        var b = WriteWav("001.wav", 24000, 1, 16, 100);
        var joined = Path.Combine(_folder, "joined.wav");

        Assert.True(WavFiles.Join(new[] { a, b }, joined));

        var first = File.ReadAllBytes(a).Skip(44).ToArray();
        var second = File.ReadAllBytes(b).Skip(44).ToArray();
        var all = File.ReadAllBytes(joined).Skip(44).ToArray();

        Assert.Equal(first.Concat(second).ToArray(), all);
    }

    [Fact]
    public void Join_RefusesRatherThanResample()
    {
        var a = WriteWav("000.wav", 24000, 1, 16, 24000);
        var b = WriteWav("001.wav", 16000, 1, 16, 24000);       // a different rate
        var joined = Path.Combine(_folder, "joined.wav");

        Assert.False(WavFiles.Join(new[] { a, b }, joined));
        Assert.False(File.Exists(joined));                       // and writes nothing at all
    }

    [Fact]
    public void Join_RefusesWhenAPieceIsMissing()
    {
        // A hole in the middle of a sentence would be far worse than a seam between two.
        var a = WriteWav("000.wav", 24000, 1, 16, 24000);
        var joined = Path.Combine(_folder, "joined.wav");

        Assert.False(WavFiles.Join(new[] { a, Path.Combine(_folder, "gone.wav") }, joined));
        Assert.False(File.Exists(joined));
    }

    [Fact]
    public void Join_RefusesTrivialAndRubbishInput()
    {
        var a = WriteWav("000.wav", 24000, 1, 16, 24000);
        var joined = Path.Combine(_folder, "joined.wav");

        Assert.False(WavFiles.Join(new[] { a }, joined));         // one piece is already one sound
        Assert.False(WavFiles.Join(Array.Empty<string>(), joined));
        Assert.False(WavFiles.Join(null!, joined));
        Assert.False(WavFiles.Join(new[] { a, a }, ""));
    }

    [Fact]
    public void Join_LeavesNoTemporaryFileBehind()
    {
        var a = WriteWav("000.wav", 24000, 1, 16, 2400);
        var b = WriteWav("001.wav", 24000, 1, 16, 2400);
        var joined = Path.Combine(_folder, "joined.wav");

        Assert.True(WavFiles.Join(new[] { a, b }, joined));
        Assert.Empty(Directory.GetFiles(_folder, "*.joining"));
    }

    [Fact]
    public void Join_OverwritesAnEarlierJoin()
    {
        var a = WriteWav("000.wav", 24000, 1, 16, 2400);
        var b = WriteWav("001.wav", 24000, 1, 16, 2400);
        var c = WriteWav("002.wav", 24000, 1, 16, 2400);
        var joined = Path.Combine(_folder, "joined.wav");

        Assert.True(WavFiles.Join(new[] { a, b }, joined));
        Assert.True(WavFiles.Join(new[] { a, b, c }, joined));

        Assert.Equal(0.3, WavFiles.TryRead(joined)!.Duration.TotalSeconds, 3);
    }

    // ---- the buzz: a piece poured before it was finished ---------------------------------------

    [Fact]
    public void Join_RefusesAPieceStillBeingWritten()
    {
        // THE BUG ANTON HEARD (2026.08.16). Streaming publishes a piece the moment it EXISTS, and
        // TryRead clamps an over-long declared size down to what is really there — so a half-written
        // piece reads back as a healthy short clip. Poured after another piece it byte-shifts
        // everything downstream, which is not a click but a drone for the rest of the take.
        var whole = WriteWav("000.wav", 24000, 1, 16, 24000);
        var half = WriteWav("001.wav", 24000, 1, 16, 24000);

        // Cut the bytes off the end WITHOUT touching the header — exactly a write in progress.
        var bytes = File.ReadAllBytes(half);
        File.WriteAllBytes(half, bytes.Take(bytes.Length / 2).ToArray());

        var info = WavFiles.TryRead(half);
        Assert.NotNull(info);
        Assert.True(info!.LooksUnfinished);

        var joined = Path.Combine(_folder, "joined.wav");
        Assert.False(WavFiles.Join(new[] { whole, half }, joined));
        Assert.False(File.Exists(joined));      // a seam is a smaller wound than a buzz
    }

    [Fact]
    public void Join_IsNotFooledByTheStreamingPlaceholder()
    {
        // 0xFFFFFFFF means "I do not know yet", not "I was cut short". A hosted service writes it
        // on purpose, and reading it as a truncation would refuse a perfectly good file.
        var a = WriteWav("000.wav", 24000, 1, 16, 24000);
        var b = WriteWav("001.wav", 24000, 1, 16, 12000);
        PokeUInt32(b, 40, uint.MaxValue);

        var info = WavFiles.TryRead(b);
        Assert.NotNull(info);
        Assert.False(info!.LooksUnfinished);

        var joined = Path.Combine(_folder, "joined.wav");
        Assert.True(WavFiles.Join(new[] { a, b }, joined));
    }

    [Fact]
    public void Join_PoursOnlyWholeSampleFrames()
    {
        // Even a well-formed file can end on half a sample if something upstream miscounted. One
        // stray byte shifts all of it, so the remainder is dropped instead.
        var a = WriteWav("000.wav", 24000, 2, 16, 1000);      // 4 bytes a frame
        var b = WriteWav("001.wav", 24000, 2, 16, 1000);

        var joined = Path.Combine(_folder, "joined.wav");
        Assert.True(WavFiles.Join(new[] { a, b }, joined));

        var info = WavFiles.TryRead(joined);
        Assert.NotNull(info);
        Assert.Equal(0, info!.DataBytes % info.BlockAlign);
    }
}
