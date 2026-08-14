using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Core.Tests;

public class VoiceAssignmentsTests
{
    private static VoicePreset Voice(string id, VoiceGender gender)
        => new VoicePreset { Id = id, Name = id, Gender = gender, RemoteVoiceId = "x", Backend = VoiceBackend.Remote };

    private static VoiceAssignments Cast()
        => new VoiceAssignments { DefaultFemale = "sibylla", DefaultMale = "achilles" };

    // ---------------- the three tiers ----------------

    [Fact]
    public void VoiceFor_FallsToTheGenderDefault()
    {
        var cast = Cast();
        Assert.Equal("sibylla", cast.VoiceFor("lord_1", isFemale: true));
        Assert.Equal("achilles", cast.VoiceFor("lord_1", isFemale: false));
    }

    [Fact]
    public void VoiceFor_OwnCastingBeatsTheDefault()
    {
        var cast = Cast();
        cast.Cast("lord_1", "briseis");
        Assert.Equal("briseis", cast.VoiceFor("lord_1", isFemale: true));
        Assert.Equal("sibylla", cast.VoiceFor("lord_2", isFemale: true));
    }

    [Fact]
    public void VoiceFor_NoDefault_IsSilence()
    {
        Assert.Equal(string.Empty, new VoiceAssignments().VoiceFor("lord_1", isFemale: true));
    }

    [Fact]
    public void VoiceFor_UnknownSoul_StillGetsTheDefault()
    {
        Assert.Equal("achilles", Cast().VoiceFor(null, isFemale: false));
    }

    [Fact]
    public void Cast_EmptyClearsBackToTheDefault()
    {
        var cast = Cast();
        cast.Cast("lord_1", "briseis");
        Assert.True(cast.IsCast("lord_1"));

        cast.Cast("lord_1", "");
        Assert.False(cast.IsCast("lord_1"));
        Assert.Equal("sibylla", cast.VoiceFor("lord_1", isFemale: true));
    }

    [Fact]
    public void IsCast_TellsChosenFromDefaulted()
    {
        var cast = Cast();
        cast.Cast("lord_1", "briseis");
        Assert.True(cast.IsCast("lord_1"));
        Assert.False(cast.IsCast("lord_2"));    // speaks, but only because she is a woman
    }

    // ---------------- a voice that left the shelf ----------------

    [Fact]
    public void ForgetMissing_DropsStaleCastingsAndDefaults()
    {
        var cast = Cast();
        cast.Cast("lord_1", "briseis");
        cast.Cast("lord_2", "achilles");
        cast.Player = "maximus";

        // Only achilles survives on the shelf.
        var dropped = cast.ForgetMissing(new[] { "achilles" });

        Assert.Equal(3, dropped);                       // briseis casting, sibylla default, maximus player
        Assert.False(cast.IsCast("lord_1"));
        Assert.True(cast.IsCast("lord_2"));
        Assert.Equal(string.Empty, cast.DefaultFemale);
        Assert.Equal("achilles", cast.DefaultMale);
        Assert.Equal(string.Empty, cast.Player);
    }

    [Fact]
    public void ForgetMissing_IsCaseInsensitive()
    {
        var cast = new VoiceAssignments { DefaultFemale = "Sibylla" };
        Assert.Equal(0, cast.ForgetMissing(new[] { "sibylla" }));
        Assert.Equal("Sibylla", cast.DefaultFemale);
    }

    [Fact]
    public void ForgetMissing_EmptyShelfSilencesEveryone_ButDoesNotThrow()
    {
        var cast = Cast();
        cast.Cast("lord_1", "briseis");
        cast.ForgetMissing(Array.Empty<string>());
        Assert.Equal(string.Empty, cast.VoiceFor("lord_1", isFemale: true));
    }

    // ---------------- speaking on a fresh install ----------------

    [Fact]
    public void FillEmptyDefaults_CastsFromTheShelfByGender()
    {
        var cast = new VoiceAssignments();
        var changed = cast.FillEmptyDefaults(new[]
        {
            Voice("achilles", VoiceGender.Male),
            Voice("sibylla", VoiceGender.Female),
        });

        Assert.True(changed);
        Assert.Equal("sibylla", cast.DefaultFemale);
        Assert.Equal("achilles", cast.DefaultMale);
    }

    [Fact]
    public void FillEmptyDefaults_NeverOverwritesAChoice()
    {
        // Including the deliberate choice of a woman's voice for the men.
        var cast = new VoiceAssignments { DefaultMale = "sibylla" };
        cast.FillEmptyDefaults(new[] { Voice("achilles", VoiceGender.Male) });
        Assert.Equal("sibylla", cast.DefaultMale);
    }

    [Fact]
    public void FillEmptyDefaults_SkipsAVoiceThatCannotSpeak()
    {
        var mute = new VoicePreset { Id = "mute", Name = "Mute", Gender = VoiceGender.Female };
        Assert.False(mute.IsSpeakable);

        var cast = new VoiceAssignments();
        cast.FillEmptyDefaults(new[] { mute });
        Assert.Equal(string.Empty, cast.DefaultFemale);
    }

    [Fact]
    public void FillEmptyDefaults_NoShelf_ChangesNothing()
    {
        var cast = new VoiceAssignments();
        Assert.False(cast.FillEmptyDefaults(Array.Empty<VoicePreset>()));
        Assert.False(cast.FillEmptyDefaults(null!));
    }

    // ---------------- persistence ----------------

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "immersiveai-cast-" + Guid.NewGuid().ToString("N"), "voice-assignments.json");
        try
        {
            var cast = Cast();
            cast.Cast("lord_1", "briseis");
            cast.Player = "maximus";
            cast.Save(path);

            var back = VoiceAssignments.Load(path);
            Assert.Equal("briseis", back.VoiceFor("lord_1", isFemale: true));
            Assert.Equal("sibylla", back.DefaultFemale);
            Assert.Equal("achilles", back.DefaultMale);
            Assert.Equal("maximus", back.Player);
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            try { if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void Load_MissingOrMangled_IsAnEmptySheetNotAThrow()
    {
        Assert.Empty(VoiceAssignments.Load(Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid().ToString("N"))).ByNpc);

        var bad = Path.Combine(Path.GetTempPath(), "immersiveai-cast-bad-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(bad, "{ not json ");
        try
        {
            var sheet = VoiceAssignments.Load(bad);
            Assert.Empty(sheet.ByNpc);
            Assert.Equal(string.Empty, sheet.DefaultFemale);
        }
        finally { try { File.Delete(bad); } catch { } }
    }
}
