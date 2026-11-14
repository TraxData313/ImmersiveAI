using ImmersiveAI.Core.Handmaids;

namespace ImmersiveAI.Core.Tests;

public class HandmaidTests
{
    [Theory]
    [InlineData("sea_raiders", "niya", "Neya", "sturgia", "spc_wanderer_sturgia_0", "Vladika moy")]
    [InlineData("desert_bandits", "yasmin", "Yasmin", "aserai", "spc_wanderer_aserai_8", "Zawji")]
    [InlineData("forest_bandits", "alys", "Alys", "vlandia", "spc_wanderer_vlandia_9", "Messire")]
    [InlineData("mountain_bandits", "nessa", "Nessa", "battania", "spc_wanderer_battania_0", "M'fhear")]
    [InlineData("steppe_bandits", "ayla", "Ayla", "khuzait", "spc_wanderer_khuzait_9", "Nökhör mini")]
    public void EachDenYieldsItsOwnGirl(string bandit, string id, string name, string culture, string template, string title)
    {
        var girl = HandmaidRoster.ForBanditCulture(bandit);
        Assert.Equal(id, girl.Id);
        Assert.Equal(name, girl.Name);
        Assert.Equal(culture, girl.Culture);
        Assert.Equal(template, girl.TemplateId);
        Assert.Equal(title, girl.NativeTitle);
        Assert.Same(girl, HandmaidRoster.ById(id));
        Assert.Same(girl, HandmaidRoster.ForBanditCulture("Culture." + bandit));
        Assert.Same(girl, HandmaidRoster.ForBanditCulture(bandit.ToUpperInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("looters")]
    [InlineData("southern_pirates")]
    public void AnUnknownDenFallsBackSafely(string? bandit)
    {
        Assert.Null(HandmaidRoster.OwnOf(bandit));
        Assert.Same(HandmaidRoster.Fallback, HandmaidRoster.ForBanditCulture(bandit));
        Assert.Equal(HandmaidRoster.All, HandmaidRoster.PickOrder(bandit));
    }

    [Fact]
    public void TheRosterIsFiveDistinctGirls()
    {
        Assert.Equal(5, HandmaidRoster.All.Count);
        Assert.Equal(5, HandmaidRoster.All.Select(g => g.Id).Distinct().Count());
        Assert.Equal(5, HandmaidRoster.All.Select(g => g.BanditCulture).Distinct().Count());
        Assert.All(HandmaidRoster.All, g => Assert.True(HandmaidText.LooksLikeFaceCode(g.FaceCode)));
        Assert.Equal(new[] { 19, 20, 18, 19, 21 }, HandmaidRoster.All.Select(HandmaidText.AgeOf).ToArray());
        Assert.Null(HandmaidRoster.ById("vesna"));
        Assert.Null(HandmaidRoster.ById(null));
    }

    [Fact]
    public void ThePickListPutsTheDensOwnFirst()
    {
        var order = HandmaidRoster.PickOrder("mountain_bandits");
        Assert.Equal(5, order.Count);
        Assert.Equal("nessa", order[0].Id);
        Assert.Equal(new[] { "niya", "yasmin", "alys", "ayla" }, order.Skip(1).Select(g => g.Id));
    }

    [Fact]
    public void PickLabelsReadLikeTheBrief()
    {
        Assert.Equal("Yasmin (Aserai)", HandmaidText.PickLabel(HandmaidRoster.Yasmin, false));
        Assert.Equal("Nessa (Battania) (this hideout's own)", HandmaidText.PickLabel(HandmaidRoster.Nessa, true));
    }

    [Fact]
    public void EveryShownLineHasItsTitleFilled()
    {
        foreach (var g in HandmaidRoster.All)
        {
            foreach (var line in new[] { HandmaidText.StoryLine(g), HandmaidText.SkillLine(g), HandmaidText.TeacherLine(g) })
            {
                Assert.False(string.IsNullOrWhiteSpace(line));
                Assert.DoesNotContain("{", line);
            }
        }
        Assert.Equal("*She kneels.* You have slain them, my lord.", HandmaidText.KneelLine());
        Assert.Contains("Take me with you, my lord.", HandmaidText.PleaLine());
        Assert.Equal("Then I am yours, my lord. Where you go, I will go.", HandmaidText.AcceptedLine());
        Assert.Equal("I can set a bone and cool a fever, my lord.", HandmaidText.SkillLine(HandmaidRoster.Nessa));
        Assert.Equal("I can set a bone and cool a fever, Messire.", HandmaidText.Fill(HandmaidRoster.Nessa.Skill, "Messire"));
    }

    [Fact]
    public void TheSoldierSpeaksTheBriefsWords()
    {
        Assert.Equal("Captain. Behind the stores, chained to a post, we found a girl. Alive. She will not speak to us.",
            HandmaidText.SoldierOpening(again: false));
        Assert.Equal("Captain?", HandmaidText.SoldierOpening(again: true));
        Assert.Equal("Bring her forward.", HandmaidText.PlayerBringHer);
        Assert.Equal("Give her bread and let her go.", HandmaidText.PlayerLetHerGo);
        Assert.Equal("[Immersive AI] What is this meeting?", HandmaidText.PlayerAskMeeting);
        Assert.Equal("As you say, captain.", HandmaidText.SoldierAsYouSay);
        Assert.Equal("\"" + HandmaidText.SoldierReport + "\"", HandmaidText.PopupReport);
    }

    [Fact]
    public void TheSoldiersNoteIsPlainlyANote()
    {
        var note = HandmaidText.SoldierNote;
        Assert.StartsWith("(Immersive AI: this meeting comes once in a campaign.", note);
        Assert.EndsWith(")", note);
        Assert.Contains("using no companion slot", note);
        Assert.Contains("let her go now, and she is gone for good", note);
        Assert.Contains("Dev mode", note);
    }

    [Fact]
    public void DevBringLabelsReadLikeTheBrief()
    {
        Assert.Equal("Bring Yasmin (Aserai) forward. (this hideout's own)",
            HandmaidText.DevBringLabel(HandmaidRoster.Yasmin, true));
        Assert.Equal("Bring Neya (Sturgia) forward.", HandmaidText.DevBringLabel(HandmaidRoster.Neya, false));
        Assert.Equal("[Immersive AI • test] Step back. Show me the others.", HandmaidText.DevStepBack);
        Assert.Equal("Call the captive (as if a hideout was just cleared)", HandmaidText.DevCallButton);
    }

    [Fact]
    public void ThePopupCarriesTheReportThenTheNote()
    {
        Assert.Equal("A captive in the den", HandmaidText.PopupTitle);
        var body = HandmaidText.PopupBody;
        Assert.StartsWith("\"Captain. Behind the stores", body);
        Assert.Contains("will not speak to us.\"\n\nImmersive AI: this meeting comes once", body);
        Assert.Equal("Bring her forward", HandmaidText.PopupBringHer);
        Assert.Equal("Give her bread and let her go", HandmaidText.PopupLetHerGo);
    }

    [Fact]
    public void HerMemoryNamesThePlayer()
    {
        var beat = HandmaidText.JoinMemoryFor("Toni");
        Assert.StartsWith("Toni and his men stormed the bandits' den where I was held captive", beat);
        Assert.Contains("They had taken me only days before", beat);
        Assert.EndsWith("I owe him my life and my freedom.", beat);
        Assert.DoesNotContain("{", HandmaidText.JoinMemoryFor(null));
        Assert.Equal("Ayla has joined your family.", HandmaidText.JoinedNoticeFor("Ayla"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("not a face", false)]
    [InlineData("<BodyProperties version='4' age='20' weight='0.5' build='0.5' key='zz' />", false)]
    [InlineData("<BodyProperties version='4' age='20.5' weight='0.5' build='0.5' key='000BAC088000100DB976648E6774B835537D86629511323BDCB177278A84F667017776140748B49500000000000000000000000000000000000000003A1A0040' />", true)]
    public void FaceCodesAreShapeChecked(string? code, bool ok)
    {
        Assert.Equal(ok, HandmaidText.LooksLikeFaceCode(code));
    }

    [Fact]
    public void TheLauncherNeutralizerTravelsAsOnePlainArgument()
    {
        var n = ImmersiveAI.Core.Llm.ClaudeCliShape.LauncherNeutralizer;
        Assert.DoesNotContain("'", n);
        Assert.DoesNotContain("\"", n);
        Assert.DoesNotContain("\n", n);
        Assert.Contains("email", n);
        Assert.Contains("date", n);
    }
}
