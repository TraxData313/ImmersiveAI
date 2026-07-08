using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace LivingCalradia.Core.Memory
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
            var path = GetMemoryFilePath(npcId);
            if (!File.Exists(path))
                return new NpcMemory { NpcId = npcId };

            var json = File.ReadAllText(path);
            var memory = JsonConvert.DeserializeObject<NpcMemory>(json);
            return memory ?? new NpcMemory { NpcId = npcId };
        }

        public void Save(NpcMemory memory)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            if (string.IsNullOrWhiteSpace(memory.NpcId)) throw new ArgumentException("NpcId required.", nameof(memory));

            Directory.CreateDirectory(_rootFolder);
            var path = GetMemoryFilePath(memory.NpcId);
            var json = JsonConvert.SerializeObject(memory, Formatting.Indented);

            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
                File.Replace(tempPath, path, destinationBackupFileName: null);
            else
                File.Move(tempPath, path);
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
