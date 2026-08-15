using System;
using System.Collections.Generic;
using System.IO;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>What a WAV's own header says about it.</summary>
    public sealed class WavInfo
    {
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }

        /// <summary>Bytes of actual audio (the data chunk's length).</summary>
        public long DataBytes { get; set; }

        /// <summary>Where the data chunk's bytes begin in the file.</summary>
        public long DataOffset { get; set; }

        public int ByteRate { get; set; }

        /// <summary>What the data chunk's header CLAIMED, before it was clamped to the bytes that are
        /// really there. Kept because the difference is the whole diagnosis of a file still being
        /// written — see <see cref="WavFiles.Join"/>.</summary>
        public long DeclaredDataBytes { get; set; }

        /// <summary>Bytes one sample frame occupies. Audio may only ever be cut on a multiple of
        /// this: a cut anywhere else shifts every sample after it by a byte or three, which is heard
        /// not as a click but as a loud buzz over the rest of the take.</summary>
        public int BlockAlign => Math.Max(1, Channels * BitsPerSample / 8);

        /// <summary>True when the header promised more audio than the file holds. Either the write
        /// is still going on, or it stopped badly. Both mean the same thing to a joiner: this piece
        /// is not finished and its bytes may not be poured after anything else.
        /// <para>
        /// A hosted service that streams its answer writes <c>0xFFFFFFFF</c> as "I do not know yet",
        /// which is not a truncation and must not be read as one.
        /// </para>
        /// </summary>
        public bool LooksUnfinished =>
            DeclaredDataBytes > DataBytes && DeclaredDataBytes != uint.MaxValue;

        /// <summary>How long it plays, from its own numbers.</summary>
        public TimeSpan Duration =>
            ByteRate <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(DataBytes / (double)ByteRate);
    }

    /// <summary>
    /// Reading and JOINING the little WAV files a streamed reply arrives in.
    /// <para>
    /// The joining is what makes streaming gapless. The engine hands over about a second of audio at
    /// a time, and playing N files means starting each one after the last reports itself finished —
    /// which can only be noticed on a frame boundary, so there is a frame of silence inside every
    /// second of speech and the voice is heard breaking up. But by the time the first piece has
    /// finished playing, several more have arrived (measured 2026.08.15: the engine generates about
    /// 2.5x faster than the audio plays), so the pieces still waiting can simply be poured into ONE
    /// file and played as one sound. A fourteen-piece reply becomes three or four sounds, and the few
    /// seams left are the ones the double-buffer in <c>VoicePlayback</c> hides.
    /// </para>
    /// <para>
    /// Deliberately in Core, and deliberately dumb: it copies bytes and never resamples. Pieces of
    /// one reply always share a format because one generation made them — a mismatch means something
    /// is wrong, and the honest answer is to refuse the join and let them play one at a time.
    /// </para>
    /// </summary>
    public static class WavFiles
    {
        private const int HeaderBytes = 44;

        /// <summary>Reads a WAV's header. Null when the file is not a PCM WAV we understand — which
        /// costs the caller nothing but a fallback, never an exception.</summary>
        public static WavInfo? TryRead(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    return TryRead(stream);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Reads a WAV's header from an open stream, leaving the position wherever it ends up.</summary>
        public static WavInfo? TryRead(Stream stream)
        {
            try
            {
                if (stream == null || stream.Length < HeaderBytes) return null;
                stream.Seek(0, SeekOrigin.Begin);

                using (var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true))
                {
                    if (new string(reader.ReadChars(4)) != "RIFF") return null;
                    reader.ReadUInt32();                                   // riff size
                    if (new string(reader.ReadChars(4)) != "WAVE") return null;

                    var info = new WavInfo();
                    var haveFormat = false;

                    while (stream.Position + 8 <= stream.Length)
                    {
                        var id = new string(reader.ReadChars(4));
                        var size = reader.ReadUInt32();

                        if (id == "fmt ")
                        {
                            if (size < 16) return null;
                            reader.ReadUInt16();                           // format tag
                            info.Channels = reader.ReadUInt16();
                            info.SampleRate = (int)reader.ReadUInt32();
                            info.ByteRate = (int)reader.ReadUInt32();
                            reader.ReadUInt16();                           // block align
                            info.BitsPerSample = reader.ReadUInt16();
                            haveFormat = true;

                            var rest = (long)size - 16;
                            if (rest > 0) stream.Seek(rest, SeekOrigin.Current);
                        }
                        else if (id == "data")
                        {
                            if (!haveFormat) return null;
                            info.DataOffset = stream.Position;
                            // A size that runs past the end of the file is a truncated write, not a
                            // lie worth believing: take what is actually there. But REMEMBER that it
                            // happened — the clamp is what lets a half-written piece look perfectly
                            // healthy, and a joiner must be able to tell.
                            var available = stream.Length - stream.Position;
                            info.DeclaredDataBytes = size;
                            info.DataBytes = size > available ? available : size;
                            return info.DataBytes > 0 ? info : null;
                        }
                        else
                        {
                            stream.Seek(size + (size % 2), SeekOrigin.Current);   // chunks are word-aligned
                        }
                    }
                }
            }
            catch
            {
                // An unreadable header is simply an unknown file; every caller has a way to carry on.
            }
            return null;
        }

        /// <summary>How long a WAV plays, or null when it cannot be read.</summary>
        public static TimeSpan? TryReadDuration(string path) => TryRead(path)?.Duration;

        /// <summary>
        /// Makes a WAV's declared sizes agree with the bytes actually in the file. Answers true when
        /// it had to change something.
        /// <para>
        /// THIS EXISTS BECAUSE OF A REAL SILENCE (2026.08.15). A hosted speech service streams its
        /// answer, so it writes the header before it knows how long the audio will be and puts
        /// <c>0xFFFFFFFF</c> in the data chunk's size — the "I do not know yet" placeholder. Our own
        /// reader coped, clamping it to what was really there, so every number the mod reported was
        /// right: the clip was 7.1 s, it was cached, it was handed to the audio engine, and the
        /// engine said it was playing. And nothing came out of the speakers, because FMOD believes
        /// the header: it was told the audio was four gigabytes long.
        /// </para>
        /// <para>
        /// The lesson worth keeping: a lenient reader is a good thing, but it HIDES malformed input
        /// from everything downstream that is not lenient. Repair the file, do not just cope with it.
        /// </para>
        /// </summary>
        public static bool TryRepairSizes(string path)
        {
            try
            {
                var info = TryRead(path);
                if (info == null || info.DataOffset <= 0) return false;

                var length = new FileInfo(path).Length;
                var realData = length - info.DataOffset;
                if (realData <= 0 || realData > uint.MaxValue) return false;

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true))
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
                {
                    var changed = false;

                    // The RIFF size counts everything after itself: the whole file less "RIFF" and
                    // the four bytes of the size field.
                    stream.Seek(4, SeekOrigin.Begin);
                    var declaredRiff = reader.ReadUInt32();
                    var realRiff = (uint)(length - 8);
                    if (declaredRiff != realRiff)
                    {
                        stream.Seek(4, SeekOrigin.Begin);
                        writer.Write(realRiff);
                        changed = true;
                    }

                    // The data size sits in the four bytes just before the audio itself.
                    stream.Seek(info.DataOffset - 4, SeekOrigin.Begin);
                    var declaredData = reader.ReadUInt32();
                    if (declaredData != (uint)realData)
                    {
                        stream.Seek(info.DataOffset - 4, SeekOrigin.Begin);
                        writer.Write((uint)realData);
                        changed = true;
                    }

                    return changed;
                }
            }
            catch
            {
                return false;   // a file we cannot repair is one we hand over as it came
            }
        }

        /// <summary>
        /// Pours several WAVs into one, in the order given. Answers false — and writes nothing —
        /// unless there were at least two readable files that agree on their format.
        /// <para>
        /// The output is written to a sibling temporary file and moved into place, so a reader can
        /// never open a half-poured file. That matters more here than it looks: the thing that reads
        /// this file is the game's own audio engine, on the very next frame.
        /// </para>
        /// <para>
        /// TWO REFUSALS, AND THEY ARE THE FIX FOR THE BUZZ (Anton, 2026.08.16 — a long reply came out
        /// as "buuuuu", mostly at the paragraph breaks, and Full read of the same words was clean).
        /// Streaming publishes a piece the instant it EXISTS on disk, not the instant it is finished,
        /// and the reader above deliberately clamps an over-long declared size down to the bytes
        /// actually written — so a piece caught mid-write reads back as a perfectly healthy short
        /// clip. Pour that after another piece and everything downstream of it is byte-shifted:
        /// sixteen-bit samples read half a sample out are not a click, they are a loud drone for the
        /// rest of the take. Full read never showed it because the host joins its own pieces and only
        /// announces a file that is closed.
        /// <list type="number">
        /// <item>A piece whose header promised more than it holds is UNFINISHED — refuse the whole
        /// join and let the pieces play one at a time. A seam is a far smaller wound than a buzz.</item>
        /// <item>Every piece is poured on a whole number of sample FRAMES. Even a well-formed file
        /// can end on a half sample if something upstream miscounted, and one stray byte is enough
        /// to shift all of it.</item>
        /// </list>
        /// </para>
        /// </summary>
        public static bool Join(IReadOnlyList<string> inputs, string outputPath)
        {
            if (inputs == null || inputs.Count < 2) return false;
            if (string.IsNullOrWhiteSpace(outputPath)) return false;

            var pieces = new List<KeyValuePair<string, WavInfo>>(inputs.Count);
            WavInfo? first = null;

            foreach (var path in inputs)
            {
                var info = TryRead(path);
                if (info == null) return false;                       // a hole would be worse than a seam
                if (info.LooksUnfinished) return false;               // still being written — see above
                if (first == null) first = info;
                else if (info.SampleRate != first.SampleRate
                         || info.Channels != first.Channels
                         || info.BitsPerSample != first.BitsPerSample)
                    return false;                                     // never resample; just do not join

                // Only whole sample frames may be poured. The remainder is at most three bytes —
                // inaudible — where keeping it would shift every sample that follows.
                var aligned = info.DataBytes - info.DataBytes % info.BlockAlign;
                if (aligned <= 0) return false;
                info.DataBytes = aligned;

                pieces.Add(new KeyValuePair<string, WavInfo>(path, info));
            }

            if (first == null || pieces.Count < 2) return false;

            long total = 0;
            foreach (var piece in pieces) total += piece.Value.DataBytes;
            if (total <= 0 || total > int.MaxValue - HeaderBytes) return false;

            var temp = outputPath + ".joining";
            try
            {
                var folder = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                using (var outStream = new FileStream(temp, FileMode.Create, FileAccess.Write))
                {
                    WriteHeader(outStream, first, total);

                    var buffer = new byte[64 * 1024];
                    foreach (var piece in pieces)
                    {
                        using (var inStream = new FileStream(piece.Key, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            inStream.Seek(piece.Value.DataOffset, SeekOrigin.Begin);
                            var left = piece.Value.DataBytes;
                            while (left > 0)
                            {
                                var want = (int)Math.Min(buffer.Length, left);
                                var got = inStream.Read(buffer, 0, want);
                                if (got <= 0) break;
                                outStream.Write(buffer, 0, got);
                                left -= got;
                            }
                        }
                    }
                }

                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(temp, outputPath);
                return true;
            }
            catch
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { /* best effort */ }
                return false;
            }
        }

        private static void WriteHeader(Stream stream, WavInfo format, long dataBytes)
        {
            using (var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
            {
                var blockAlign = (short)(format.Channels * format.BitsPerSample / 8);
                var byteRate = format.SampleRate * blockAlign;

                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write((uint)(36 + dataBytes));
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16u);
                w.Write((short)1);                    // PCM
                w.Write((short)format.Channels);
                w.Write(format.SampleRate);
                w.Write(byteRate);
                w.Write(blockAlign);
                w.Write((short)format.BitsPerSample);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write((uint)dataBytes);
            }
        }
    }
}
