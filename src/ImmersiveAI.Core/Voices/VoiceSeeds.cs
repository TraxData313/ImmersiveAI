using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>What one seeding pass did, so the caller can say it plainly in the log.</summary>
    public sealed class VoiceSeedReport
    {
        /// <summary>Voices copied onto the shelf just now.</summary>
        public List<string> Added { get; } = new List<string>();

        /// <summary>Voices that were already there under the same name — the player's own wins,
        /// always, and is never written over.</summary>
        public List<string> Kept { get; } = new List<string>();

        /// <summary>Shipped folders that are not usable voices, with the reason. Each is skipped
        /// WITHOUT being marked done, so mending one makes it arrive on the next start.</summary>
        public List<string> Skipped { get; } = new List<string>();

        /// <summary>
        /// Voices the ledger says have been offered before and are therefore not offered again —
        /// the player threw them away, and that has to mean something.
        /// <para>
        /// Reported rather than passed over in silence because from the outside this is
        /// indistinguishable from the feature being broken: the voices are plainly there in the
        /// module folder and plainly not on the shelf. It cost an hour once. Saying so in the log
        /// also names the one file to delete to start over.
        /// </para>
        /// </summary>
        public List<string> AlreadyOffered { get; } = new List<string>();

        public bool DidAnything => Added.Count > 0;
    }

    /// <summary>
    /// The voices that travel WITH the mod, laid onto the player's shelf the first time they run it.
    /// <para>
    /// Without this, a player who turns voices on before making a clone of their own finds an empty
    /// list and a feature that appears not to work. So the module carries a <c>Voices\</c> folder —
    /// <c>female\</c> and <c>male\</c> inside it, purely for our own tidiness — and each voice in
    /// there is copied into <c>Configs\ImmersiveAI\Voices\</c> once. From that moment it is the
    /// PLAYER'S voice: theirs to rename, recast, or throw away.
    /// </para>
    /// <para>
    /// TWO RULES, and both are about never overruling the player. A name already on the shelf is
    /// never written over — an edited voice.json survives every update. And a voice already seeded
    /// is never seeded again, because deleting one has to MEAN something; that is what the little
    /// <c>_seeded.json</c> ledger is for, and it is why the ledger records a name the moment it is
    /// offered rather than the moment it is copied.
    /// </para>
    /// <para>
    /// The gender folders are a convenience, not a schema: any other name (<c>elderly\</c>,
    /// <c>battanian\</c>) works just as well and simply seeds its voices with no gender hint. A
    /// voice folder may also sit loose at the top. And the same law <see cref="VoiceLibrary"/>
    /// keeps holds here — one malformed shipped voice costs nothing but itself.
    /// </para>
    /// </summary>
    public static class VoiceSeeds
    {
        /// <summary>The names of voices already offered. Sits on the shelf, beside the voices.</summary>
        public const string LedgerFileName = "_seeded.json";

        /// <summary>Folder names that carry a gender hint to the voices inside them.</summary>
        private static readonly Dictionary<string, VoiceGender> GenderFolders =
            new Dictionary<string, VoiceGender>(StringComparer.OrdinalIgnoreCase)
            {
                { "female", VoiceGender.Female },
                { "women", VoiceGender.Female },
                { "male", VoiceGender.Male },
                { "men", VoiceGender.Male },
            };

        /// <summary>
        /// Lays every shipped voice not yet offered onto the shelf. Safe to call as often as you
        /// like: a missing <paramref name="shippedRoot"/> is simply a mod carrying no voices, which
        /// is a perfectly ordinary thing for it to be.
        /// </summary>
        public static VoiceSeedReport Seed(string shippedRoot, string shelfRoot)
        {
            var report = new VoiceSeedReport();
            if (string.IsNullOrWhiteSpace(shelfRoot)) return report;
            if (string.IsNullOrWhiteSpace(shippedRoot) || !SafeExists(shippedRoot)) return report;

            var ledgerPath = Path.Combine(shelfRoot, LedgerFileName);
            var offered = LoadLedger(ledgerPath);
            var changed = false;

            // Ids are worked out for the whole batch at once because two peoples may well name a
            // voice the same thing — Voices\female\battania\gwen and Voices\female\vlandia\gwen are
            // different women. Resolved one at a time, the second would find the first's folder
            // already on the shelf and be quietly reported as "kept", i.e. lost.
            foreach (var candidate in WithUniqueIds(Candidates(shippedRoot)))
            {
                var id = candidate.Id;
                if (id.Length == 0) continue;
                if (offered.Contains(id)) { report.AlreadyOffered.Add(id); continue; }

                // Read it before trusting it — a shipped folder is still just a folder.
                var preset = SafeLoad(candidate.Candidate.Folder, out var problem);
                if (preset == null)
                {
                    // NOT ledgered: a voice we shipped broken should arrive once it is mended.
                    report.Skipped.Add(problem ?? (id + ": not a voice."));
                    continue;
                }

                var destination = Path.Combine(shelfRoot, id);
                if (SafeExists(destination))
                {
                    // Already theirs. Mark it done so we stop looking, and leave it entirely alone.
                    offered.Add(id);
                    changed = true;
                    report.Kept.Add(id);
                    continue;
                }

                try
                {
                    CopyVoiceFolder(candidate.Candidate.Folder, destination);
                    StampArrival(destination, id, candidate.Candidate.Gender, candidate.Candidate.Culture);
                    offered.Add(id);
                    changed = true;
                    report.Added.Add(preset.Name.Length > 0 ? preset.Name : id);
                }
                catch (Exception ex)
                {
                    // A half-written folder would read as a broken voice forever, so take it back.
                    TryRemove(destination);
                    report.Skipped.Add(id + ": " + ex.Message);
                }
            }

            if (changed) SaveLedger(ledgerPath, offered);
            return report;
        }

        // ------------------------------------------------------------------

        private struct Candidate
        {
            public string Folder;
            public VoiceGender Gender;
            public string Culture;
        }

        /// <summary>Folder names that mean "these belong to nobody in particular" rather than
        /// naming a people. Kept as words a person would actually type.</summary>
        private static readonly HashSet<string> NoCultureFolders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "other", "others", "misc", "any" };

        /// <summary>
        /// Every shipped voice folder, at whichever depth it sits.
        /// <para>
        /// The shape the mod ships is <c>Voices\female\battania\gwen\</c> — sex, then people, then
        /// the voice — because that is the order a person casting a hundred voices thinks in. Both
        /// shallower forms still work and always will: <c>Voices\female\sibylla\</c> is a woman
        /// belonging to no people, and a voice folder sitting loose at the top belongs to nobody at
        /// all. An <c>other\</c> rung says the same thing out loud.
        /// </para>
        /// </summary>
        private static IEnumerable<Candidate> Candidates(string shippedRoot)
        {
            foreach (var child in SafeDirectories(shippedRoot))
            {
                if (HoldsVoice(child))
                {
                    yield return new Candidate { Folder = child, Gender = VoiceGender.Unknown, Culture = string.Empty };
                    continue;
                }

                var name = Path.GetFileName(child) ?? string.Empty;
                if (name.StartsWith("_", StringComparison.Ordinal)) continue;   // bookkeeping, never a voice

                GenderFolders.TryGetValue(name, out var gender);

                foreach (var grandchild in SafeDirectories(child))
                {
                    if (HoldsVoice(grandchild))
                    {
                        // <gender>\<voice> — a voice of that sex and no particular people.
                        yield return new Candidate { Folder = grandchild, Gender = gender, Culture = string.Empty };
                        continue;
                    }

                    var cultureName = Path.GetFileName(grandchild) ?? string.Empty;
                    if (cultureName.StartsWith("_", StringComparison.Ordinal)) continue;

                    var culture = NoCultureFolders.Contains(cultureName)
                        ? string.Empty
                        : VoiceCasting.Normalize(cultureName);

                    foreach (var voiceFolder in SafeDirectories(grandchild))
                        if (HoldsVoice(voiceFolder))
                            yield return new Candidate { Folder = voiceFolder, Gender = gender, Culture = culture };
                }
            }
        }

        /// <summary>A candidate with the name it will wear on the shelf already settled.</summary>
        private struct Named
        {
            public Candidate Candidate;
            public string Id;
        }

        /// <summary>
        /// Settles every shipped voice's shelf name in one pass, so two peoples may both have a
        /// Gwen. The first keeps the bare name; the next takes its people's name after it
        /// (<c>gwen-vlandia</c>), and only if that is taken too does it start counting. Ordered by
        /// folder path so the same shelf comes out of the same module folder every time — a name
        /// that moved between updates would orphan every casting that used it.
        /// </summary>
        private static IEnumerable<Named> WithUniqueIds(IEnumerable<Candidate> candidates)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = candidates.OrderBy(c => c.Folder, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var candidate in ordered)
            {
                var id = IdOf(candidate);
                if (id.Length == 0) continue;

                if (used.Contains(id) && candidate.Culture.Length > 0)
                {
                    var byPeople = VoiceLibrary.SlugFor(id + "-" + candidate.Culture);
                    if (!used.Contains(byPeople)) id = byPeople;
                }
                var unique = id;
                var n = 2;
                while (used.Contains(unique))
                    unique = id + "-" + n++.ToString(System.Globalization.CultureInfo.InvariantCulture);

                used.Add(unique);
                yield return new Named { Candidate = candidate, Id = unique };
            }
        }

        /// <summary>
        /// The name this voice will wear on the shelf: THE FOLDER'S, not whatever <c>voice.json</c>
        /// happens to state.
        /// <para>
        /// The folder is the thing we can see in the repository and the thing an update diffs, so it
        /// has to be the identity — otherwise <c>male\max\</c> could quietly arrive as "sibylla-3"
        /// because that is what Studio called the preset it was exported from, and the ledger that
        /// remembers a deleted voice would be keyed on a name nobody ever chose. Renaming the
        /// folder is how a shipped voice is renamed. (The player-facing NAME is a separate field
        /// and is left exactly as written.)
        /// </para>
        /// </summary>
        private static string IdOf(Candidate candidate)
        {
            var folderName = Path.GetFileName(candidate.Folder) ?? string.Empty;
            return string.IsNullOrWhiteSpace(folderName) ? string.Empty : VoiceLibrary.SlugFor(folderName);
        }

        /// <summary>Writes down what the folder it landed in already implied — its name, the gender
        /// its group folder lent it, and the people whose folder it sat in. Only ever FILLS what is
        /// missing: a voice that states its own gender or culture keeps it, whichever folder we
        /// happened to file it under.</summary>
        private static void StampArrival(string folder, string id, VoiceGender gender, string culture)
        {
            var preset = VoiceLibrary.LoadFolder(folder, out _);
            if (preset == null) return;

            var wants = false;
            if (!string.Equals(preset.Id, id, StringComparison.Ordinal)) { preset.Id = id; wants = true; }
            if (preset.Gender == VoiceGender.Unknown && gender != VoiceGender.Unknown)
            {
                preset.Gender = gender;
                wants = true;
            }
            if (string.IsNullOrWhiteSpace(preset.Culture) && !string.IsNullOrWhiteSpace(culture))
            {
                preset.Culture = culture;
                wants = true;
            }
            if (preset.Source.Length == 0) { preset.Source = "shipped with the mod"; wants = true; }

            if (wants) VoiceLibrary.Save(Path.GetDirectoryName(folder) ?? folder, preset);
        }

        /// <summary>Copies a voice folder's own files. Deliberately not recursive: a voice is
        /// flat by definition, and anything nested is somebody's leftovers.</summary>
        private static void CopyVoiceFolder(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }

        private static VoicePreset? SafeLoad(string folder, out string? problem)
        {
            try { return VoiceLibrary.LoadFolder(folder, out problem); }
            catch (Exception ex) { problem = Path.GetFileName(folder) + ": " + ex.Message; return null; }
        }

        // ---- the ledger ----

        private sealed class Ledger
        {
            public List<string> Seeded { get; set; } = new List<string>();
        }

        private static HashSet<string> LoadLedger(string path)
        {
            var offered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(path)) return offered;
                var ledger = JsonConvert.DeserializeObject<Ledger>(File.ReadAllText(path));
                if (ledger?.Seeded == null) return offered;
                foreach (var id in ledger.Seeded)
                    if (!string.IsNullOrWhiteSpace(id)) offered.Add(id.Trim());
            }
            catch { /* an unreadable ledger seeds afresh — noisier than losing a voice, and safer */ }
            return offered;
        }

        private static void SaveLedger(string path, IEnumerable<string> offered)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                var json = JsonConvert.SerializeObject(
                    new Ledger { Seeded = offered.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList() },
                    Formatting.Indented);

                var temp = path + ".tmp";
                File.WriteAllText(temp, json);
                if (File.Exists(path)) File.Replace(temp, path, destinationBackupFileName: null);
                else File.Move(temp, path);
            }
            catch { /* an unwritten ledger costs a re-offer, never a voice */ }
        }

        // ---- small safe wrappers ----

        private static bool HoldsVoice(string folder)
        {
            try { return File.Exists(Path.Combine(folder, VoiceLibrary.MetadataFileName)); }
            catch { return false; }
        }

        private static bool SafeExists(string folder)
        {
            try { return Directory.Exists(folder); }
            catch { return false; }
        }

        private static IEnumerable<string> SafeDirectories(string root)
        {
            try { return Directory.GetDirectories(root); }
            catch { return Enumerable.Empty<string>(); }
        }

        private static void TryRemove(string folder)
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
            catch { /* nothing more to be done about it */ }
        }
    }
}
