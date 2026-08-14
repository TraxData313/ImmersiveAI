using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Core.Tests;

public class VoiceLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "immersiveai-voices-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* a locked temp folder is not a failed test */ }
    }

    private string Voices => Path.Combine(_root, "Voices");

    private string MakeVoiceFolder(string folderName, string json, bool withEmbedding = true)
    {
        var folder = Path.Combine(Voices, folderName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, VoiceLibrary.MetadataFileName), json);
        if (withEmbedding)
            File.WriteAllText(Path.Combine(folder, VoiceLibrary.EmbeddingFileName), "[0.1,0.2,0.3]");
        return folder;
    }

    // ---------------- the shelf ----------------

    [Fact]
    public void Load_MissingFolder_IsAnEmptyShelfNotAnError()
    {
        Assert.Empty(VoiceLibrary.Load(Path.Combine(_root, "nope")));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var preset = new VoicePreset
        {
            Name = "Sibylla",
            Gender = VoiceGender.Female,
            ReferenceText = "American voice of a young women, emotional, about 20 years old.",
            Dimension = 2048,
            Source = "Qwen-TTS Studio",
        };
        Directory.CreateDirectory(Voices);
        VoiceLibrary.Save(Voices, preset);
        File.WriteAllText(Path.Combine(preset.FolderPath, VoiceLibrary.EmbeddingFileName), "[0.1]");

        var loaded = VoiceLibrary.Load(Voices);
        var one = Assert.Single(loaded);
        Assert.Equal("Sibylla", one.Name);
        Assert.Equal("sibylla", one.Id);
        Assert.Equal(VoiceGender.Female, one.Gender);
        Assert.Equal(2048, one.Dimension);
        Assert.True(one.IsSpeakable);
        Assert.NotEqual(string.Empty, one.EmbeddingPath);
    }

    [Fact]
    public void Load_BrokenVoice_IsSkippedAndTheRestSurvive()
    {
        // The PromptPack law: a broken one never costs more than itself.
        MakeVoiceFolder("good", "{\"Id\":\"good\",\"Name\":\"Achilles\"}");
        MakeVoiceFolder("mangled", "{ this is not json at all ");
        MakeVoiceFolder("empty", "{\"Id\":\"empty\",\"Name\":\"Silent\"}", withEmbedding: false);
        Directory.CreateDirectory(Path.Combine(Voices, "not-a-voice-at-all"));

        var loaded = VoiceLibrary.Load(Voices, out var skipped);

        var one = Assert.Single(loaded);
        Assert.Equal("Achilles", one.Name);
        Assert.Equal(2, skipped.Count);                     // mangled + unspeakable
        Assert.Contains(skipped, s => s.Contains("mangled"));
        Assert.Contains(skipped, s => s.Contains("Silent"));
    }

    [Fact]
    public void Load_FolderNameStandsInForAMissingId()
    {
        MakeVoiceFolder("briseis", "{\"Name\":\"Briseis\"}");
        var one = Assert.Single(VoiceLibrary.Load(Voices));
        Assert.Equal("briseis", one.Id);
    }

    [Fact]
    public void Load_SortsByName()
    {
        MakeVoiceFolder("c", "{\"Name\":\"Cunbert\"}");
        MakeVoiceFolder("a", "{\"Name\":\"Achilles\"}");
        MakeVoiceFolder("b", "{\"Name\":\"Briseis\"}");
        var names = VoiceLibrary.Load(Voices).Select(v => v.Name).ToArray();
        Assert.Equal(new[] { "Achilles", "Briseis", "Cunbert" }, names);
    }

    [Fact]
    public void RemoteVoice_SpeaksWithoutAnyLocalFiles()
    {
        MakeVoiceFolder("kokoro-bella",
            "{\"Name\":\"Bella\",\"Backend\":1,\"RemoteVoiceId\":\"kokoro:af_bella\"}",
            withEmbedding: false);
        var one = Assert.Single(VoiceLibrary.Load(Voices));
        Assert.Equal(VoiceBackend.Remote, one.Backend);
        Assert.True(one.IsSpeakable);
    }

    // ---------------- folder names ----------------

    [Theory]
    [InlineData("Sibylla", "sibylla")]
    [InlineData("Sibylla 3", "sibylla-3")]
    [InlineData("  Achilles  ", "achilles")]
    [InlineData("A/B\\C:D", "a-b-c-d")]
    [InlineData("", "voice")]
    [InlineData("   ", "voice")]
    [InlineData("...", "voice")]
    public void SlugFor_IsSafeAndRecognisable(string name, string expected)
    {
        Assert.Equal(expected, VoiceLibrary.SlugFor(name));
    }

    [Fact]
    public void SlugFor_AvoidsReservedDeviceNames()
    {
        Assert.Equal("_con", VoiceLibrary.SlugFor("CON"));
    }

    [Fact]
    public void SlugFor_AvoidsCollidingWithAnExistingVoice()
    {
        Directory.CreateDirectory(Path.Combine(Voices, "sibylla"));
        Assert.Equal("sibylla-2", VoiceLibrary.SlugFor("Sibylla", Voices));
    }

    // ---------------- bringing over what Studio made ----------------

    // A real row from the author's own voice-presets.tsv, paths repointed at the test folder.
    private string WriteStudioStore()
    {
        var studio = Path.Combine(_root, "studio");
        var embeddings = Path.Combine(studio, "embeddings");
        var icl = Path.Combine(studio, "icl-prompts");
        Directory.CreateDirectory(embeddings);
        Directory.CreateDirectory(icl);

        var wav = Path.Combine(studio, "Sibylla.wav");
        File.WriteAllText(wav, "RIFF....");
        var e1024 = Path.Combine(embeddings, "voice-1-d1024.json");
        var e2048 = Path.Combine(embeddings, "voice-1-d2048.json");
        var i2048 = Path.Combine(icl, "voice-1-d2048.json");
        File.WriteAllText(e1024, "[1.0]");
        File.WriteAllText(e2048, "[2.0]");
        File.WriteAllText(i2048, "{\"format\":\"qwen3_tts_icl_prompt_v1\"}");

        static string B64(string s) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)).TrimEnd('=');

        var row = string.Join("\t",
            "voice-1",
            "Sibylla",
            e1024,
            wav,
            "1024",
            $"1024:{B64(e1024)};2048:{B64(e2048)}",
            B64("American voice of a young women, emotional, about 20 years old."),
            $"2048:{B64(i2048)}");

        var tsv = Path.Combine(studio, "voice-presets.tsv");
        File.WriteAllLines(tsv, new[] { row });
        return tsv;
    }

    [Fact]
    public void ReadStudioPresets_DecodesTheRow()
    {
        var found = VoiceLibrary.ReadStudioPresets(WriteStudioStore());
        var one = Assert.Single(found);

        Assert.Equal("voice-1", one.Id);
        Assert.Equal("Sibylla", one.Name);
        Assert.Equal("American voice of a young women, emotional, about 20 years old.", one.ReferenceText);
        Assert.Equal(1024, one.Dimension);
        Assert.Equal(2, one.EmbeddingPaths.Count);
        Assert.True(File.Exists(one.EmbeddingPaths[2048]));
        Assert.True(File.Exists(one.IclPromptPaths[2048]));
        Assert.True(File.Exists(one.ReferenceWavPath));
    }

    [Fact]
    public void ReadStudioPresets_MissingFile_IsEmptyNotAThrow()
    {
        Assert.Empty(VoiceLibrary.ReadStudioPresets(Path.Combine(_root, "nothing.tsv")));
    }

    [Fact]
    public void ReadStudioPresets_JunkRowsAreSkipped()
    {
        var path = Path.Combine(_root, "junk.tsv");
        Directory.CreateDirectory(_root);
        File.WriteAllLines(path, new[]
        {
            "",
            "onlyonecolumn",
            "\tno-id\t\t",
            "id-ok\tName Ok",
        });
        var found = VoiceLibrary.ReadStudioPresets(path);
        var one = Assert.Single(found);
        Assert.Equal("id-ok", one.Id);
    }

    [Fact]
    public void ImportFromStudio_CopiesTheVoiceOntoOurOwnShelf()
    {
        var studio = VoiceLibrary.ReadStudioPresets(WriteStudioStore()).Single();
        Directory.CreateDirectory(Voices);

        var preset = VoiceLibrary.ImportFromStudio(studio, Voices, preferredDimension: 2048,
                                                   gender: VoiceGender.Female);

        Assert.Equal("sibylla", preset.Id);
        Assert.Equal(2048, preset.Dimension);
        Assert.Equal(VoiceGender.Female, preset.Gender);
        Assert.Equal("Qwen-TTS Studio", preset.Source);

        // Canonical names inside the folder — that is what makes it portable.
        Assert.True(File.Exists(Path.Combine(preset.FolderPath, VoiceLibrary.MetadataFileName)));
        Assert.True(File.Exists(Path.Combine(preset.FolderPath, VoiceLibrary.EmbeddingFileName)));
        Assert.True(File.Exists(Path.Combine(preset.FolderPath, VoiceLibrary.IclPromptFileName)));
        Assert.True(File.Exists(Path.Combine(preset.FolderPath, VoiceLibrary.ReferenceWavFileName)));

        // The 2048 embedding, not the 1024 one the row named as default.
        Assert.Equal("[2.0]", File.ReadAllText(Path.Combine(preset.FolderPath, VoiceLibrary.EmbeddingFileName)));

        // And it loads back as a speakable voice.
        var one = Assert.Single(VoiceLibrary.Load(Voices));
        Assert.True(one.IsSpeakable);
        Assert.Equal("Sibylla", one.Name);
    }

    [Fact]
    public void ImportFromStudio_LeavesStudioUntouched()
    {
        var tsv = WriteStudioStore();
        var studio = VoiceLibrary.ReadStudioPresets(tsv).Single();
        Directory.CreateDirectory(Voices);

        VoiceLibrary.ImportFromStudio(studio, Voices);

        Assert.True(File.Exists(studio.EmbeddingPaths[2048]));
        Assert.True(File.Exists(studio.ReferenceWavPath));
        Assert.True(File.Exists(tsv));
    }

    [Fact]
    public void ImportFromStudio_TwiceGivesTwoFoldersNotAClobber()
    {
        var studio = VoiceLibrary.ReadStudioPresets(WriteStudioStore()).Single();
        Directory.CreateDirectory(Voices);

        var first = VoiceLibrary.ImportFromStudio(studio, Voices);
        var second = VoiceLibrary.ImportFromStudio(studio, Voices);

        Assert.NotEqual(first.FolderPath, second.FolderPath);
        Assert.Equal(2, VoiceLibrary.Load(Voices).Count);
    }

    // ---------------- the retakes ----------------

    private static VoiceLibrary.StudioVoice Named(string name)
        => new VoiceLibrary.StudioVoice { Id = name.ToLowerInvariant(), Name = name };

    [Fact]
    public void DropNumberedRetakes_KeepsTheOneThatEarnedTheBareName()
    {
        // The author's own shelf: five goes at Sibylla, two at Achilles.
        var kept = VoiceLibrary.DropNumberedRetakes(new[]
        {
            Named("Sibylla"), Named("Sybylla 2"), Named("Sibylla 3"),
            Named("Sibylla 4"), Named("Sibylla 5"),
            Named("Maximus"), Named("Achilles"), Named("Achilles 2"), Named("Briseis"),
        }).Select(v => v.Name).ToArray();

        Assert.Equal(new[] { "Sibylla", "Sybylla 2", "Maximus", "Achilles", "Briseis" }, kept);
    }

    [Fact]
    public void DropNumberedRetakes_LoneRetakeSurvives()
    {
        // No bare "Sibylla" on the shelf — dropping the only take would import nothing.
        var kept = VoiceLibrary.DropNumberedRetakes(new[] { Named("Sibylla 3"), Named("Achilles") })
            .Select(v => v.Name).ToArray();
        Assert.Equal(new[] { "Sibylla 3", "Achilles" }, kept);
    }

    [Fact]
    public void DropNumberedRetakes_LeavesNamesThatMerelyEndInADigit()
    {
        var kept = VoiceLibrary.DropNumberedRetakes(new[] { Named("Voice 7"), Named("R2") })
            .Select(v => v.Name).ToArray();
        Assert.Equal(new[] { "Voice 7", "R2" }, kept);
    }

    [Fact]
    public void DropNumberedRetakes_EmptyIsEmpty()
    {
        Assert.Empty(VoiceLibrary.DropNumberedRetakes(Array.Empty<VoiceLibrary.StudioVoice>()));
    }

    [Theory]
    [InlineData("Sibylla 3", "Sibylla")]
    [InlineData("Sibylla3", "Sibylla")]
    [InlineData("Sibylla  12", "Sibylla")]
    [InlineData("Sibylla", null)]
    [InlineData("7", null)]
    [InlineData("", null)]
    public void RetakeStem_ReadsTheNameBeneathTheNumber(string name, string? expected)
    {
        Assert.Equal(expected, VoiceLibrary.RetakeStem(name));
    }

    [Fact]
    public void ImportFromStudio_MissingReferenceClip_StillYieldsASpeakingVoice()
    {
        var studio = VoiceLibrary.ReadStudioPresets(WriteStudioStore()).Single();
        File.Delete(studio.ReferenceWavPath);
        Directory.CreateDirectory(Voices);

        var preset = VoiceLibrary.ImportFromStudio(studio, Voices);

        Assert.Equal(string.Empty, preset.ReferenceWavPath);
        Assert.True(preset.IsSpeakable);
    }
}
