using ImmersiveAI.Core.Memory;

namespace ImmersiveAI.Core.Tests;

public class JsonMemoryStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ImmersiveAITests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_WhenNoFile_ReturnsFreshMemoryWithId()
    {
        var store = new JsonMemoryStore(_tempDir);

        var memory = store.Load("lord_7_18");

        Assert.Equal("lord_7_18", memory.NpcId);
        Assert.Empty(memory.RecentTurns);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var store = new JsonMemoryStore(_tempDir);
        var memory = new NpcMemory { NpcId = "lord_1", NpcName = "Gafnir", Summary = "old friends" };
        memory.KnownFacts.Add("fact one");
        memory.AddTurn(new ConversationTurn { PlayerLine = "hi", NpcLine = "ho", GameDay = 42 });

        store.Save(memory);
        var loaded = store.Load("lord_1");

        Assert.Equal("Gafnir", loaded.NpcName);
        Assert.Equal("old friends", loaded.Summary);
        Assert.Single(loaded.KnownFacts);
        Assert.Single(loaded.RecentTurns);
        Assert.Equal(42, loaded.RecentTurns[0].GameDay);
        Assert.Equal(42, loaded.LastConversationGameDay);
    }

    [Fact]
    public void Save_OverwritesExistingFileAtomically()
    {
        var store = new JsonMemoryStore(_tempDir);
        var memory = new NpcMemory { NpcId = "lord_1", Summary = "v1" };
        store.Save(memory);
        memory.Summary = "v2";

        store.Save(memory);

        Assert.Equal("v2", store.Load("lord_1").Summary);
        Assert.False(File.Exists(store.GetMemoryFilePath("lord_1") + ".tmp"));
    }

    [Fact]
    public void SaveToThenLoadFrom_UsesExplicitPathAndCreatesFolder()
    {
        var store = new JsonMemoryStore(_tempDir);
        var path = Path.Combine(_tempDir, "NPCs", "lord_7_13_1_Gunjadrid", "memories.json");
        var memory = new NpcMemory { NpcId = "lord_7_13_1", NpcName = "Gunjadrid", Summary = "kept" };

        store.SaveTo(path, memory);

        Assert.True(File.Exists(path));
        var loaded = store.LoadFrom(path, "lord_7_13_1");
        Assert.Equal("kept", loaded.Summary);
        Assert.Equal("Gunjadrid", loaded.NpcName);
    }

    [Fact]
    public void LoadFrom_WhenNoFile_ReturnsFreshMemoryWithId()
    {
        var store = new JsonMemoryStore(_tempDir);
        var path = Path.Combine(_tempDir, "NPCs", "lord_1_Absent", "memories.json");

        var memory = store.LoadFrom(path, "lord_1");

        Assert.Equal("lord_1", memory.NpcId);
        Assert.Empty(memory.RecentTurns);
    }

    [Fact]
    public void NpcIdsWithInvalidPathChars_AreSanitizedToValidFileNames()
    {
        var store = new JsonMemoryStore(_tempDir);
        var memory = new NpcMemory { NpcId = "lord<1>:\"weird\"", Summary = "ok" };

        store.Save(memory);

        Assert.Equal("ok", store.Load("lord<1>:\"weird\"").Summary);
    }
}
