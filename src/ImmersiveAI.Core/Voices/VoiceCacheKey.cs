using System;
using System.Globalization;
using System.Text;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>
    /// The name a spoken line's audio is filed under — derived from the line itself, never stored.
    /// <para>
    /// One value doing five jobs, which is why it is worth its own file. It is the cache's identity,
    /// so the same words in the same voice are synthesized once. It is how a ▶ on a three-year-old
    /// memory finds its audio, or discovers there is none and makes it. It is how recasting a soul
    /// invalidates nothing — the key simply changes, and the old audio ages out on its own. And it
    /// survives the chat thread being rebuilt: <c>RefreshThread()</c> allocates a fresh
    /// <c>MBBindingList</c> every time anything changes, so a row cannot carry state — but it can
    /// re-derive this from the words it already holds.
    /// </para>
    /// <para>
    /// 64-bit FNV-1a, not 32. A long campaign holds many thousands of lines, and a 32-bit collision
    /// is not a slow cache — it is the wrong person's voice on someone else's words, once, with no
    /// way to reproduce it.
    /// </para>
    /// </summary>
    public static class VoiceCacheKey
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>A unit separator between the folded fields, so ("ab","c") can never hash the
        /// same as ("a","bc"). No voice id, model name or spoken line can contain it.</summary>
        private const string Separator = "\u001f";

        /// <summary>
        /// The key for one utterance. Everything that changes how it SOUNDS is folded in — the voice,
        /// the model, the language — so a model swap or a recast never serves stale audio. Nothing
        /// that changes only how it LOOKS is folded in: the text is normalized first, so re-rendering
        /// the same line with different spacing is still a cache hit.
        /// </summary>
        public static string For(string? voiceId, string? text, string? model = null, int languageId = -1)
        {
            var hash = FnvOffset;
            Fold(ref hash, voiceId ?? string.Empty);
            Fold(ref hash, Separator);
            Fold(ref hash, model ?? string.Empty);
            Fold(ref hash, Separator);
            Fold(ref hash, languageId.ToString(CultureInfo.InvariantCulture));
            Fold(ref hash, Separator);
            Fold(ref hash, Normalize(text));

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The spelling of a line that decides cache identity: trimmed, with every run of whitespace
        /// (including the new lines the plain-speech rule allows) collapsed to one space. Case and
        /// punctuation are kept — "Go." and "Go?" are different performances, and a voice engine
        /// hears the difference even where a reader would not.
        /// </summary>
        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var sb = new StringBuilder(text!.Length);
            var pendingSpace = false;
            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c)) { pendingSpace = sb.Length > 0; continue; }
                if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>The folder one utterance's chunks live in, beneath a voice's own cache folder:
        /// <c>_cache\&lt;voiceId&gt;\&lt;key&gt;\000.wav</c>. A directory rather than a file because a
        /// chunked reply must be evicted whole — pruning one chunk of five leaves a reply with a
        /// hole in the middle of it.</summary>
        public static string FolderNameFor(string key) => key;

        /// <summary>The name of one chunk within an utterance, zero-padded so a plain lexical sort
        /// is playback order.</summary>
        public static string ChunkFileName(int index)
            => index.ToString("000", CultureInfo.InvariantCulture) + ".wav";

        private static void Fold(ref ulong hash, string s)
        {
            // Hash the UTF-8 bytes, not the chars: a Cyrillic line and its transliteration must not
            // depend on the runtime's char width, and this mod is played in Bulgarian.
            var bytes = Encoding.UTF8.GetBytes(s);
            foreach (var b in bytes)
            {
                hash ^= b;
                hash *= FnvPrime;
            }
        }
    }
}
