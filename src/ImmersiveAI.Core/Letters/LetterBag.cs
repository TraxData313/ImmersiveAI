using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Letters
{
    /// <summary>
    /// Every letter currently on the road for one campaign, with its JSON persistence (same
    /// atomic-write pattern as <see cref="Memory.JsonMemoryStore"/>). Letters leave the bag the
    /// moment they are delivered; what was said then lives on in the NPC's memory and the
    /// correspondence logs, not here.
    /// </summary>
    public sealed class LetterBag
    {
        public List<Letter> Letters { get; set; } = new List<Letter>();

        /// <summary>Letters whose road has run out, oldest arrival first.</summary>
        public IReadOnlyList<Letter> Due(double nowGameDay) =>
            Letters.Where(l => l != null && l.ArriveGameDay <= nowGameDay)
                   .OrderBy(l => l.ArriveGameDay)
                   .ToList();

        /// <summary>How many letters are riding TOWARD the player right now, across every writer.
        /// Caps the world's spontaneous letter-writing (a morning of feeling social must not become
        /// an evening buried in arrivals): when this reaches the configured ceiling, no new NPC
        /// letter sets out until one lands. Replies the player invited, and the player's own
        /// outgoing letters, are not counted.</summary>
        public int InFlightToPlayerCount =>
            Letters.Count(l => l != null && l.ToPlayer);

        /// <summary>True while any letter is on the road between the player and this NPC — one
        /// courier per bond keeps correspondence a conversation, not a flood.</summary>
        public bool HasInFlightWith(string npcId) =>
            !string.IsNullOrEmpty(npcId)
            && Letters.Any(l => l != null && string.Equals(l.NpcId, npcId, StringComparison.Ordinal));

        public void Add(Letter letter)
        {
            if (letter != null) Letters.Add(letter);
        }

        public void Remove(string letterId) =>
            Letters.RemoveAll(l => l == null || string.Equals(l.Id, letterId, StringComparison.Ordinal));

        /// <summary>Loads a bag from disk; a missing or unreadable file is an empty bag, never an error.</summary>
        public static LetterBag LoadFrom(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return new LetterBag();
                var bag = JsonConvert.DeserializeObject<LetterBag>(File.ReadAllText(filePath));
                if (bag == null) return new LetterBag();
                bag.Letters = bag.Letters?.Where(l => l != null).ToList() ?? new List<Letter>();
                return bag;
            }
            catch { return new LetterBag(); }
        }

        /// <summary>Saves atomically (temp + replace), so a crash mid-write never loses the road.</summary>
        public void SaveTo(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path required.", nameof(filePath));

            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(filePath))
                File.Replace(tempPath, filePath, destinationBackupFileName: null);
            else
                File.Move(tempPath, filePath);
        }
    }
}
