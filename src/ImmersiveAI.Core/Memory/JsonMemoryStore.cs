using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>
    /// Persists one NpcMemory per NPC as JSON under a root folder
    /// (in-game: a per-save folder, mirroring how ChatAi organized its save_data).
    /// Writes are atomic (temp file + replace) so a crash mid-write never corrupts memory.
    /// </summary>
    public sealed class JsonMemoryStore
    {
        private readonly string _rootFolder;

        public JsonMemoryStore(string rootFolder)
        {
            if (string.IsNullOrWhiteSpace(rootFolder)) throw new ArgumentException("Root folder required.", nameof(rootFolder));
            _rootFolder = rootFolder;
        }

        public string GetMemoryFilePath(string npcId)
        {
            return Path.Combine(_rootFolder, SanitizeFileName(npcId) + ".json");
        }

        public NpcMemory Load(string npcId)
        {
            return LoadFrom(GetMemoryFilePath(npcId), npcId);
        }

        public void Save(NpcMemory memory)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            if (string.IsNullOrWhiteSpace(memory.NpcId)) throw new ArgumentException("NpcId required.", nameof(memory));
            SaveTo(GetMemoryFilePath(memory.NpcId), memory);
        }

        /// <summary>
        /// Loads a memory from an explicit file path, so callers that organize files in a
        /// custom layout (e.g. one folder per NPC) control the path instead of the id-derived
        /// default. Returns a fresh memory (tagged with <paramref name="npcId"/>) if absent.
        /// </summary>
        public NpcMemory LoadFrom(string filePath, string npcId)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path required.", nameof(filePath));
            if (!File.Exists(filePath))
                return new NpcMemory { NpcId = npcId };

            var json = File.ReadAllText(filePath);
            var memory = JsonConvert.DeserializeObject<NpcMemory>(json);
            return memory ?? new NpcMemory { NpcId = npcId };
        }

        /// <summary>Saves a memory to an explicit file path, creating the folder and writing atomically.</summary>
        public void SaveTo(string filePath, NpcMemory memory)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path required.", nameof(filePath));
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            if (string.IsNullOrWhiteSpace(memory.NpcId)) throw new ArgumentException("NpcId required.", nameof(memory));

            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var json = JsonConvert.SerializeObject(memory, Formatting.Indented);

            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(filePath))
                File.Replace(tempPath, filePath, destinationBackupFileName: null);
            else
                File.Move(tempPath, filePath);
        }

        internal static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "_";
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return cleaned.Length == 0 ? "_" : cleaned;
        }
    }
}
