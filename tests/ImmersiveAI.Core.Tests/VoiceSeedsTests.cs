using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Core.Tests;

public class VoiceSeedsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "immersiveai-seeds-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* a locked temp folder is not a failed test */ }
    }

    private string Shipped => Path.Combine(_root, "module", "Voices");
    private string Shelf => Path.Combine(_root, "Configs", "Voices");

    /// <summary>A voice as it travels with the mod: under a group folder, or loose when
    /// <paramref name="group"/> is empty.</summary>
    private string Ship(string folderName, string group = "female", string? json = null, bool withEmbedding = true)
    {
        var folder = group.Length == 0
            ? Path.Combine(Shipped, folderName)
            : Path.Combine(Shipped, group, folderName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, VoiceLibrary.MetadataFileName),
            json ?? "{ \"Name\": \"" + folderName + "\" }");
        if (withEmbedding)
            File.WriteAllText(Path.Combine(folder, VoiceLibrary.EmbeddingFileName), "[0.1,0.2]");
        return folder;
    }

    private VoicePreset? OnShelf(string id) =>
        VoiceLibrary.Load(Shelf).FirstOrDefault(v => v.Id == id);

    // ---------------- the ordinary road ----------------

    [Fact]
    public void Seed_LaysShippedVoicesOntoTheShelf()
    {
        Ship("ana");
        Ship("boris", "male");

        var report = VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Equal(2, report.Added.Count);
        Assert.Equal(2, VoiceLibrary.Load(Shelf).Count);
        Assert.True(File.Exists(Path.Combine(Shelf, "ana", VoiceLibrary.EmbeddingFileName)));
    }

    [Fact]
    public void Seed_TakesTheGenderFromTheFolderItWasFiledUnder()
    {
        Ship("ana");
        Ship("boris", "male");
        Ship("nameless", "elderly");

        VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Equal(VoiceGender.Female, OnShelf("ana")!.Gender);
        Assert.Equal(VoiceGender.Male, OnShelf("boris")!.Gender);
        // An unrecognised group folder still seeds — it simply lends no hint.
        Assert.Equal(VoiceGender.Unknown, OnShelf("nameless")!.Gender);
    }

    [Fact]
    public void Seed_AVoiceStatingItsOwnGenderKeepsIt()
    {
        Ship("ana", "male", "{ \"Name\": \"Ana\", \"Gender\": 1 }");

        VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Equal(VoiceGender.Female, OnShelf("ana")!.Gender);
    }

    [Fact]
    public void Seed_TheFolderNamesTheVoice_NotWhateverStudioCalledIt()
    {
        // Studio exports carry their own preset id ("sibylla-3"), which is not the name we filed
        // the voice under. The folder is the thing visible in the repo, so the folder wins - but
        // the player-facing name is left exactly as written.
        Ship("max", "male", "{ \"Id\": \"sibylla-3\", \"Name\": \"Achilles\" }");

        VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Null(OnShelf("sibylla-3"));
        Assert.Equal("Achilles", OnShelf("max")!.Name);
        Assert.Equal(VoiceGender.Male, OnShelf("max")!.Gender);
    }

    [Fact]
    public void Seed_ALooseVoiceFolderIsSeededToo()
    {
        Ship("ana", group: "");

        Assert.Single(VoiceSeeds.Seed(Shipped, Shelf).Added);
        Assert.NotNull(OnShelf("ana"));
    }

    // ---------------- never overruling the player ----------------

    [Fact]
    public void Seed_NeverWritesOverAVoiceAlreadyOnTheShelf()
    {
        Ship("ana");
        Directory.CreateDirectory(Path.Combine(Shelf, "ana"));
        File.WriteAllText(Path.Combine(Shelf, "ana", VoiceLibrary.MetadataFileName),
            "{ \"Id\": \"ana\", \"Name\": \"Ana, as I rewrote her\" }");
        File.WriteAllText(Path.Combine(Shelf, "ana", VoiceLibrary.EmbeddingFileName), "[9]");

        var report = VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Empty(report.Added);
        Assert.Contains("ana", report.Kept);
        Assert.Equal("Ana, as I rewrote her", OnShelf("ana")!.Name);
    }

    [Fact]
    public void Seed_AVoiceTheyThrewAwayStaysThrownAway()
    {
        Ship("ana");
        VoiceSeeds.Seed(Shipped, Shelf);
        Directory.Delete(Path.Combine(Shelf, "ana"), recursive: true);

        var report = VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Empty(report.Added);
        Assert.Empty(VoiceLibrary.Load(Shelf));
        // ...and it says so, because otherwise this is indistinguishable from the feature being
        // broken: the voice is plainly shipped and plainly not on the shelf.
        Assert.Contains("ana", report.AlreadyOffered);
    }

    [Fact]
    public void Seed_TheLedgerAloneIsEnoughToHoldAVoiceBack()
    {
        // Deleting the voice folders but leaving _seeded.json behind - the exact shape of a
        // half-cleared shelf during development.
        Ship("ana");
        Directory.CreateDirectory(Shelf);
        File.WriteAllText(Path.Combine(Shelf, VoiceSeeds.LedgerFileName), "{ \"Seeded\": [ \"ana\" ] }");

        Assert.Empty(VoiceSeeds.Seed(Shipped, Shelf).Added);

        File.Delete(Path.Combine(Shelf, VoiceSeeds.LedgerFileName));

        Assert.Single(VoiceSeeds.Seed(Shipped, Shelf).Added);
    }

    [Fact]
    public void Seed_RunTwice_AddsNothingTheSecondTime()
    {
        Ship("ana");

        Assert.Single(VoiceSeeds.Seed(Shipped, Shelf).Added);
        Assert.Empty(VoiceSeeds.Seed(Shipped, Shelf).Added);
        Assert.Single(VoiceLibrary.Load(Shelf));
    }

    [Fact]
    public void Seed_AVoiceAddedToALaterVersionArrivesOnItsOwn()
    {
        Ship("ana");
        VoiceSeeds.Seed(Shipped, Shelf);

        Ship("boris", "male");
        var report = VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Single(report.Added);
        Assert.NotNull(OnShelf("boris"));
    }

    // ---------------- a broken voice costs only itself ----------------

    [Fact]
    public void Seed_ABrokenVoiceIsSkippedAndTheRestArrive()
    {
        Ship("mangled", json: "{ not json at all");
        Ship("ana");

        var report = VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Single(report.Added);
        Assert.Single(report.Skipped);
        Assert.NotNull(OnShelf("ana"));
    }

    [Fact]
    public void Seed_AVoiceWithNoVoiceDataIsSkipped()
    {
        Ship("hollow", withEmbedding: false);

        var report = VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Empty(report.Added);
        Assert.Single(report.Skipped);
    }

    [Fact]
    public void Seed_AMendedVoiceArrivesOnTheNextRun()
    {
        var folder = Ship("hollow", withEmbedding: false);
        Assert.Empty(VoiceSeeds.Seed(Shipped, Shelf).Added);

        File.WriteAllText(Path.Combine(folder, VoiceLibrary.EmbeddingFileName), "[0.1]");

        Assert.Single(VoiceSeeds.Seed(Shipped, Shelf).Added);
    }

    [Fact]
    public void Seed_NoShippedFolderIsSimplyAModCarryingNoVoices()
    {
        var report = VoiceSeeds.Seed(Path.Combine(_root, "nope"), Shelf);

        Assert.Empty(report.Added);
        Assert.Empty(report.Skipped);
    }

    // ---- sex, then people, then the voice ----

    [Fact]
    public void Seed_TakesGenderAndCultureFromTheFoldersItSatIn()
    {
        Ship("gwen", group: Path.Combine("female", "battania"));

        VoiceSeeds.Seed(Shipped, Shelf);

        var voice = OnShelf("gwen");
        Assert.NotNull(voice);
        Assert.Equal(VoiceGender.Female, voice!.Gender);
        Assert.Equal("battania", voice.Culture);
    }

    [Fact]
    public void Seed_OtherMeansBelongingToNoPeople()
    {
        Ship("sibylla", group: Path.Combine("female", "other"));

        VoiceSeeds.Seed(Shipped, Shelf);

        var voice = OnShelf("sibylla");
        Assert.NotNull(voice);
        Assert.Equal(VoiceGender.Female, voice!.Gender);
        Assert.Equal(string.Empty, voice.Culture);
    }

    [Fact]
    public void Seed_TheOlderShallowShapesStillWork()
    {
        Ship("plain", group: "female");
        Ship("loose", group: "");

        VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Equal(VoiceGender.Female, OnShelf("plain")?.Gender);
        Assert.Equal(string.Empty, OnShelf("plain")?.Culture);
        Assert.Equal(VoiceGender.Unknown, OnShelf("loose")?.Gender);
    }

    /// <summary>Two peoples may both name a voice Gwen; neither may be silently lost.</summary>
    [Fact]
    public void Seed_TwoPeoplesMayBothHaveAGwen()
    {
        Ship("gwen", group: Path.Combine("female", "battania"));
        Ship("gwen", group: Path.Combine("female", "vlandia"));

        var report = VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Equal(2, report.Added.Count);
        Assert.Equal("battania", OnShelf("gwen")?.Culture);
        Assert.Equal("vlandia", OnShelf("gwen-vlandia")?.Culture);
    }

    [Fact]
    public void Seed_AVoiceStatingItsOwnCultureKeepsIt()
    {
        Ship("gwen", group: Path.Combine("female", "battania"),
             json: "{ \"Name\": \"Gwen\", \"Culture\": \"empire\" }");

        VoiceSeeds.Seed(Shipped, Shelf);

        Assert.Equal("empire", OnShelf("gwen")?.Culture);
    }
}
