using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>
    /// Who speaks with which voice — the casting sheet.
    /// <para>
    /// Deliberately a THIN map of id → voice id, and deliberately NOT stored under
    /// <c>NPCs\campaign_&lt;id&gt;\</c>. The save-scoped memory snapshots photograph that folder and
    /// restore it wholesale, which is exactly right for what a soul remembers and exactly wrong for
    /// what they sound like: you would cast a voice, reload an older save, and find it silently
    /// recast. The test to apply to any new file this mod writes is "would rewinding this with the
    /// save be a feature or a defect?" — for memory it is a feature, for a voice it is a bug.
    /// </para>
    /// <para>
    /// Three tiers answer "who speaks for this soul": their own casting if they have one, else the
    /// default for their gender, else silence. Nothing here ever guesses from a name.
    /// </para>
    /// </summary>
    public sealed class VoiceAssignments
    {
        /// <summary>Hero string id → voice id. Only souls deliberately cast appear here.</summary>
        public Dictionary<string, string> ByNpc { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// DEAD as of 2026.08.15 — kept only so an old casting sheet still loads and saves without
        /// losing anything else in it. Nothing reads these to choose a voice any more.
        /// <para>
        /// They held "the voice every woman/man speaks with", and they sat ABOVE the per-people
        /// casting: one press, or the automatic fill that used to happen on a fresh shelf, and every
        /// man in the world shared a voice while ninety-three culture-matched ones sat unused, with
        /// nothing in the panel able to undo it. <see cref="ClearDeadDefaults"/> empties them once.
        /// Do not bring them back — see the note on <see cref="VoiceCasting"/>.
        /// </para>
        /// </summary>
        public string DefaultFemale { get; set; } = string.Empty;

        /// <inheritdoc cref="DefaultFemale"/>
        public string DefaultMale { get; set; } = string.Empty;

        /// <summary>Empties the retired gender slots. Returns true when there was something to
        /// clear, so the caller knows to save. Per-soul castings are never touched.</summary>
        public bool ClearDeadDefaults()
        {
            if (string.IsNullOrWhiteSpace(DefaultFemale) && string.IsNullOrWhiteSpace(DefaultMale))
                return false;
            DefaultFemale = string.Empty;
            DefaultMale = string.Empty;
            return true;
        }

        /// <summary>The player's own voice, for their own lines. Empty = the player is not voiced,
        /// which is the honest default: most people do not want to hear themselves.</summary>
        public string Player { get; set; } = string.Empty;

        /// <summary>The voice a soul speaks with, or empty for silence. <paramref name="isFemale"/>
        /// comes from the game layer (Hero.IsFemale); this stays pure and never guesses.</summary>
        public string VoiceFor(string? npcId, bool isFemale)
        {
            if (!string.IsNullOrWhiteSpace(npcId)
                && ByNpc.TryGetValue(npcId!, out var own)
                && !string.IsNullOrWhiteSpace(own))
                return own;

            return (isFemale ? DefaultFemale : DefaultMale) ?? string.Empty;
        }

        /// <summary>True when this soul has been cast by hand, rather than falling to a default.
        /// The panel shows the difference, so a player can tell "chosen" from "whatever women get".</summary>
        public bool IsCast(string? npcId)
            => !string.IsNullOrWhiteSpace(npcId)
               && ByNpc.TryGetValue(npcId!, out var v)
               && !string.IsNullOrWhiteSpace(v);

        /// <summary>Casts a soul. An empty voice id clears the casting, dropping them back to the
        /// default for their gender — that is how "use the usual voice again" is expressed.</summary>
        public void Cast(string npcId, string? voiceId)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return;
            if (string.IsNullOrWhiteSpace(voiceId)) ByNpc.Remove(npcId);
            else ByNpc[npcId] = voiceId!.Trim();
        }

        /// <summary>
        /// Forgets every reference to a voice that is no longer on the shelf, falling those souls
        /// back to their gender's default. Called after the library loads: a deleted or renamed
        /// folder must never leave a soul pointing at nothing, and a stale id is worse than no id
        /// because it silently produces silence with no way to see why.
        /// </summary>
        public int ForgetMissing(IEnumerable<string> knownVoiceIds)
        {
            var known = new HashSet<string>(knownVoiceIds ?? Array.Empty<string>(),
                                            StringComparer.OrdinalIgnoreCase);
            var dropped = 0;

            var stale = new List<string>();
            foreach (var pair in ByNpc)
                if (!known.Contains(pair.Value)) stale.Add(pair.Key);
            foreach (var id in stale) { ByNpc.Remove(id); dropped++; }

            if (!string.IsNullOrWhiteSpace(DefaultFemale) && !known.Contains(DefaultFemale))
            { DefaultFemale = string.Empty; dropped++; }
            if (!string.IsNullOrWhiteSpace(DefaultMale) && !known.Contains(DefaultMale))
            { DefaultMale = string.Empty; dropped++; }
            if (!string.IsNullOrWhiteSpace(Player) && !known.Contains(Player))
            { Player = string.Empty; dropped++; }

            return dropped;
        }

        /// <summary>
        /// Fills any empty default slot from what is actually on the shelf, so a fresh install
        /// speaks without the player casting anything. Picks by the voice's own gender hint, and
        /// only ever fills a slot that is EMPTY — a player's choice is never overwritten, including
        /// their choice of a woman's voice for a man.
        /// </summary>
        public bool FillEmptyDefaults(IReadOnlyList<VoicePreset> shelf)
        {
            if (shelf == null || shelf.Count == 0) return false;
            var changed = false;

            if (string.IsNullOrWhiteSpace(DefaultFemale))
            {
                var pick = FirstWith(shelf, VoiceGender.Female);
                if (pick != null) { DefaultFemale = pick.Id; changed = true; }
            }
            if (string.IsNullOrWhiteSpace(DefaultMale))
            {
                var pick = FirstWith(shelf, VoiceGender.Male);
                if (pick != null) { DefaultMale = pick.Id; changed = true; }
            }
            return changed;
        }

        private static VoicePreset? FirstWith(IReadOnlyList<VoicePreset> shelf, VoiceGender gender)
        {
            foreach (var v in shelf)
                if (v.Gender == gender && v.IsSpeakable) return v;
            return null;
        }

        // ------------------------------------------------------------------

        /// <summary>Reads the casting sheet. A missing or unreadable file is an empty sheet, never
        /// an error — nobody should lose a conversation because a voice map went bad.</summary>
        public static VoiceAssignments Load(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return new VoiceAssignments();
                var loaded = JsonConvert.DeserializeObject<VoiceAssignments>(File.ReadAllText(filePath));
                if (loaded == null) return new VoiceAssignments();
                loaded.ByNpc ??= new Dictionary<string, string>();
                return loaded;
            }
            catch
            {
                return new VoiceAssignments();
            }
        }

        /// <summary>Writes the casting sheet atomically, the way every other file here is written.</summary>
        public void Save(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path required.", nameof(filePath));

            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            var temp = filePath + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(filePath)) File.Replace(temp, filePath, destinationBackupFileName: null);
            else File.Move(temp, filePath);
        }
    }
}
