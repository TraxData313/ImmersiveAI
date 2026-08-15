using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>
    /// The player's own shelf of voices, on disk under <c>Configs\ImmersiveAI\Voices\</c>.
    /// <para>
    /// One folder per voice, every file inside it named the same way in every voice, so nothing
    /// depends on where the folder sits: zip it, hand it over, drop it in — it speaks. The law
    /// here is the one <see cref="Prompts.PromptPack"/> keeps: a broken voice never costs more
    /// than itself. A folder with a mangled voice.json, a missing embedding, or somebody's
    /// holiday photos in it is skipped with a reason, and the rest of the shelf loads.
    /// </para>
    /// </summary>
    public static class VoiceLibrary
    {
        public const string MetadataFileName = "voice.json";
        public const string EmbeddingFileName = "embedding.json";
        public const string IclPromptFileName = "icl-prompt.json";
        public const string ReferenceWavFileName = "reference.wav";

        /// <summary>Loads every readable voice under <paramref name="rootFolder"/>, by name.
        /// A missing folder is simply an empty shelf, never an error.</summary>
        public static IReadOnlyList<VoicePreset> Load(string rootFolder)
        {
            return Load(rootFolder, out _);
        }

        /// <summary>Loads the shelf and reports what it had to skip, so the caller can tell the
        /// player plainly instead of leaving them wondering where a voice went.</summary>
        public static IReadOnlyList<VoicePreset> Load(string rootFolder, out IReadOnlyList<string> skipped)
        {
            var problems = new List<string>();
            var voices = new List<VoicePreset>();
            skipped = problems;

            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
                return voices;

            foreach (var folder in SafeDirectories(rootFolder))
            {
                var voice = LoadFolder(folder, out var problem);
                if (voice != null) voices.Add(voice);
                else if (problem != null) problems.Add(problem);
            }

            // Ordered the way the panel SAYS it — women, then their peoples, then names. With five
            // voices alphabetical was fine; with a shelf carrying a dozen peoples the player is
            // scanning for a group, not a name.
            return voices
                .OrderBy(v => v.Gender == VoiceGender.Female ? 0 : v.Gender == VoiceGender.Male ? 1 : 2)
                .ThenBy(v => VoiceCasting.Normalize(v.Culture), StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Reads one voice folder. Returns null (with a reason) when it is not one.</summary>
        public static VoicePreset? LoadFolder(string folder, out string? problem)
        {
            problem = null;
            try
            {
                var metaPath = Path.Combine(folder, MetadataFileName);
                if (!File.Exists(metaPath)) return null;   // not a voice folder at all — silent

                var preset = JsonConvert.DeserializeObject<VoicePreset>(File.ReadAllText(metaPath));
                if (preset == null)
                {
                    problem = $"{Path.GetFileName(folder)}: its {MetadataFileName} could not be read.";
                    return null;
                }

                preset.FolderPath = folder;
                if (string.IsNullOrWhiteSpace(preset.Id)) preset.Id = Path.GetFileName(folder);
                if (string.IsNullOrWhiteSpace(preset.Name)) preset.Name = preset.Id;

                preset.EmbeddingPath = PathIfExists(folder, EmbeddingFileName);
                preset.IclPromptPath = PathIfExists(folder, IclPromptFileName);
                preset.ReferenceWavPath = PathIfExists(folder, ReferenceWavFileName);

                if (!preset.IsSpeakable)
                {
                    problem = $"{preset.Name}: no voice data in the folder — it cannot speak.";
                    return null;
                }
                return preset;
            }
            catch (Exception ex)
            {
                problem = $"{Path.GetFileName(folder)}: {ex.Message}";
                return null;
            }
        }

        /// <summary>Writes a voice's metadata into its own folder, creating it if need be, and
        /// fills in <see cref="VoicePreset.FolderPath"/>. The voice data files are the caller's
        /// to place (the engine writes them where we tell it).</summary>
        public static void Save(string rootFolder, VoicePreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (string.IsNullOrWhiteSpace(rootFolder))
                throw new ArgumentException("Voices folder required.", nameof(rootFolder));

            if (string.IsNullOrWhiteSpace(preset.Id)) preset.Id = SlugFor(preset.Name);
            var folder = string.IsNullOrWhiteSpace(preset.FolderPath)
                ? Path.Combine(rootFolder, preset.Id)
                : preset.FolderPath;

            Directory.CreateDirectory(folder);
            preset.FolderPath = folder;

            var json = JsonConvert.SerializeObject(preset, Formatting.Indented);
            var path = Path.Combine(folder, MetadataFileName);
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(path)) File.Replace(temp, path, destinationBackupFileName: null);
            else File.Move(temp, path);

            preset.EmbeddingPath = PathIfExists(folder, EmbeddingFileName);
            preset.IclPromptPath = PathIfExists(folder, IclPromptFileName);
            preset.ReferenceWavPath = PathIfExists(folder, ReferenceWavFileName);
        }

        /// <summary>A folder name that is safe, stable and still recognisably the voice's name.
        /// Never empty, never a reserved device name, never a collision with an existing
        /// folder under <paramref name="rootFolder"/> (a "-2" is appended instead).</summary>
        public static string SlugFor(string? name, string? rootFolder = null)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (var c in (name ?? string.Empty).Trim())
            {
                if (invalid.Contains(c) || c == ' ') sb.Append('-');
                else sb.Append(char.ToLowerInvariant(c));
            }

            var slug = sb.ToString().Trim('-', '.');
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            if (slug.Length == 0) slug = "voice";
            if (IsReservedName(slug)) slug = "_" + slug;
            if (slug.Length > 48) slug = slug.Substring(0, 48).Trim('-');

            if (string.IsNullOrWhiteSpace(rootFolder)) return slug;

            var candidate = slug;
            var n = 2;
            while (Directory.Exists(Path.Combine(rootFolder, candidate)))
                candidate = slug + "-" + n++.ToString(CultureInfo.InvariantCulture);
            return candidate;
        }

        // ------------------------------------------------------------------
        // Bringing over what the player already made in Qwen-TTS Studio
        // ------------------------------------------------------------------

        /// <summary>One voice found in Studio's own store, before we decide to take it.</summary>
        public sealed class StudioVoice
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string ReferenceText { get; set; } = string.Empty;
            public string ReferenceWavPath { get; set; } = string.Empty;
            public int Dimension { get; set; } = 2048;

            /// <summary>Embedding json by width (1024 / 2048), as absolute paths.</summary>
            public Dictionary<int, string> EmbeddingPaths { get; } = new Dictionary<int, string>();

            /// <summary>ICL-prompt json by width, as absolute paths.</summary>
            public Dictionary<int, string> IclPromptPaths { get; } = new Dictionary<int, string>();
        }

        /// <summary>
        /// Reads Qwen-TTS Studio's <c>voice-presets.tsv</c>. The columns, in order, are:
        /// id, display name, the default embedding path, the reference clip, the default width,
        /// <c>"1024:&lt;b64 path&gt;;2048:&lt;b64 path&gt;"</c> for the embeddings, the base64
        /// reference transcript, then the same width map for the ICL prompts. The base64 is
        /// unpadded, so it is repaired before decoding. Every field is optional in practice —
        /// a row we cannot read is skipped, never fatal.
        /// </summary>
        public static IReadOnlyList<StudioVoice> ReadStudioPresets(string tsvPath)
        {
            var found = new List<StudioVoice>();
            if (string.IsNullOrWhiteSpace(tsvPath) || !File.Exists(tsvPath)) return found;

            string[] lines;
            try { lines = File.ReadAllLines(tsvPath); }
            catch { return found; }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var col = line.Split('\t');
                if (col.Length < 2) continue;

                var voice = new StudioVoice
                {
                    Id = col[0].Trim(),
                    Name = col.Length > 1 ? col[1].Trim() : col[0].Trim(),
                };
                if (voice.Id.Length == 0) continue;
                if (voice.Name.Length == 0) voice.Name = voice.Id;

                if (col.Length > 3) voice.ReferenceWavPath = col[3].Trim();
                if (col.Length > 4 && int.TryParse(col[4].Trim(), out var dim) && dim > 0)
                    voice.Dimension = dim;
                if (col.Length > 5) ReadWidthMap(col[5], voice.EmbeddingPaths);
                if (col.Length > 6) voice.ReferenceText = DecodeBase64(col[6]) ?? string.Empty;
                if (col.Length > 7) ReadWidthMap(col[7], voice.IclPromptPaths);

                // The plain third column is the default-width embedding; keep it as a fallback.
                if (voice.EmbeddingPaths.Count == 0 && col.Length > 2 && col[2].Trim().Length > 0)
                    voice.EmbeddingPaths[voice.Dimension] = col[2].Trim();

                found.Add(voice);
            }
            return found;
        }

        /// <summary>
        /// Drops the numbered try-again voices, keeping the one that earned the bare name.
        /// <para>
        /// Cloning a voice is cheap and nobody nails it first go, so a real Studio shelf ends up
        /// holding "Sibylla", "Sibylla 2", "Sibylla 3" … — five attempts at one person. The keeper
        /// is the unnumbered one, and a numbered voice is dropped ONLY when its base name is
        /// actually present: a shelf whose sole take is "Sibylla 3" keeps it rather than importing
        /// nothing. Names that merely end in a digit ("Cunbert II", "Voice 7") are untouched unless
        /// something else claims their stem.
        /// </para>
        /// </summary>
        public static IReadOnlyList<StudioVoice> DropNumberedRetakes(IEnumerable<StudioVoice> voices)
        {
            var all = voices?.ToList() ?? new List<StudioVoice>();
            var bare = new HashSet<string>(
                all.Select(v => v.Name?.Trim() ?? string.Empty),
                StringComparer.CurrentCultureIgnoreCase);

            return all.Where(v =>
            {
                var stem = RetakeStem(v.Name);
                return stem == null || !bare.Contains(stem);
            }).ToList();
        }

        /// <summary>"Sibylla 3" → "Sibylla". Null when the name is not a numbered retake.</summary>
        public static string? RetakeStem(string? name)
        {
            var s = (name ?? string.Empty).TrimEnd();
            var i = s.Length;
            while (i > 0 && char.IsDigit(s[i - 1])) i--;
            if (i == s.Length || i == 0) return null;         // no trailing digits, or all digits

            var stem = s.Substring(0, i).TrimEnd();
            return stem.Length == 0 ? null : stem;
        }

        /// <summary>
        /// Copies a Studio voice onto our own shelf — metadata, the embedding at the best available
        /// width, its ICL prompt, and the reference clip when it can still be found. Copies, never
        /// moves: Studio keeps working exactly as it did, and the player can go on making voices
        /// there and bringing them over.
        /// </summary>
        public static VoicePreset ImportFromStudio(
            StudioVoice studio,
            string rootFolder,
            int preferredDimension = 2048,
            VoiceGender gender = VoiceGender.Unknown)
        {
            if (studio == null) throw new ArgumentNullException(nameof(studio));
            if (string.IsNullOrWhiteSpace(rootFolder))
                throw new ArgumentException("Voices folder required.", nameof(rootFolder));

            var dim = PickWidth(studio.EmbeddingPaths, preferredDimension, studio.Dimension);
            var preset = new VoicePreset
            {
                Id = SlugFor(studio.Name, rootFolder),
                Name = studio.Name,
                Backend = VoiceBackend.QwenLocal,
                Dimension = dim,
                ReferenceText = studio.ReferenceText,
                Gender = gender,
                Source = "Qwen-TTS Studio",
            };

            var folder = Path.Combine(rootFolder, preset.Id);
            Directory.CreateDirectory(folder);
            preset.FolderPath = folder;

            CopyIfPresent(Lookup(studio.EmbeddingPaths, dim), Path.Combine(folder, EmbeddingFileName));
            CopyIfPresent(Lookup(studio.IclPromptPaths, dim), Path.Combine(folder, IclPromptFileName));
            CopyIfPresent(studio.ReferenceWavPath, Path.Combine(folder, ReferenceWavFileName));

            Save(rootFolder, preset);
            return preset;
        }

        /// <summary>Studio's default data folder, where its presets and models live.</summary>
        public static string DefaultStudioDataFolder()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(home) ? string.Empty : Path.Combine(home, ".qwen-tts-studio");
        }

        // ------------------------------------------------------------------

        private static int PickWidth(Dictionary<int, string> paths, int preferred, int fallback)
        {
            if (paths.Count == 0) return fallback > 0 ? fallback : preferred;
            if (paths.ContainsKey(preferred)) return preferred;
            if (paths.ContainsKey(fallback)) return fallback;
            return paths.Keys.Max();
        }

        private static string Lookup(Dictionary<int, string> paths, int width)
            => paths.TryGetValue(width, out var p) ? p : string.Empty;

        private static void ReadWidthMap(string field, Dictionary<int, string> into)
        {
            if (string.IsNullOrWhiteSpace(field)) return;
            foreach (var part in field.Split(';'))
            {
                var idx = part.IndexOf(':');
                if (idx <= 0) continue;
                if (!int.TryParse(part.Substring(0, idx).Trim(), out var width)) continue;
                var path = DecodeBase64(part.Substring(idx + 1));
                if (!string.IsNullOrWhiteSpace(path)) into[width] = path!;
            }
        }

        /// <summary>Studio writes base64 without padding; restore it before decoding.</summary>
        private static string? DecodeBase64(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw!.Trim();
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
                case 1: return null;      // never valid base64
            }
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
            catch { return null; }
        }

        private static void CopyIfPresent(string? source, string destination)
        {
            if (string.IsNullOrWhiteSpace(source)) return;
            try
            {
                if (File.Exists(source)) File.Copy(source, destination, overwrite: true);
            }
            catch
            {
                // A clip that has since been deleted or is locked must not cost us the voice —
                // the embedding is what speaks, the rest is convenience.
            }
        }

        private static string PathIfExists(string folder, string fileName)
        {
            var path = Path.Combine(folder, fileName);
            return File.Exists(path) ? path : string.Empty;
        }

        private static IEnumerable<string> SafeDirectories(string root)
        {
            try { return Directory.GetDirectories(root); }
            catch { return Array.Empty<string>(); }
        }

        private static bool IsReservedName(string slug)
        {
            switch (slug.ToUpperInvariant())
            {
                case "CON": case "PRN": case "AUX": case "NUL":
                case "COM1": case "COM2": case "COM3": case "COM4": case "COM5":
                case "COM6": case "COM7": case "COM8": case "COM9":
                case "LPT1": case "LPT2": case "LPT3": case "LPT4": case "LPT5":
                case "LPT6": case "LPT7": case "LPT8": case "LPT9":
                    return true;
                default:
                    return false;
            }
        }
    }
}
