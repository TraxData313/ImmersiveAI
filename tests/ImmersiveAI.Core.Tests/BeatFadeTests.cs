using ImmersiveAI.Core.Births;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Core.Weddings;

namespace ImmersiveAI.Core.Tests;

public class BeatFadeTests
{
    private const string Account =
        "И се събраха в залата, и хлябът беше разчупен над люлката. "
        + "Мата напълни чашите и рече груба шега. "
        + "Тогава Сибила влезе с детето на ръце, а Мизам застана до нея. "
        + "Пред всички те нарекоха сина си Ибран, и името му бе чуто от двата рода.";

    private static string WitnessBeat() =>
        BirthText.WitnessFeastBeat("Мизам", "Ибран", "the town of Akkalat", Account);

    [Fact]
    public void AGreatDayIsCarriedWhole_WhileItIsStillFresh()
    {
        var beat = WitnessBeat();
        Assert.True(BeatFade.IsGreatAccount(beat));

        // The account is what a soul greets you with the next time you meet — Anton's call, and
        // for a lord you have exchanged three words with it is the only thing he has of you.
        Assert.Equal(beat, BeatFade.Fade(beat, turnsBack: 0));
        Assert.Equal(beat, BeatFade.Fade(beat, BeatFade.WholeWithin));
    }

    [Fact]
    public void PastSeven_ItThins_AndPastFourteen_OnlyTheDayItselfRemains()
    {
        var beat = WitnessBeat();

        var faded = BeatFade.Fade(beat, BeatFade.WholeWithin + 1);
        Assert.NotEqual(beat, faded);
        Assert.True(faded.Length < beat.Length);
        // What it WAS survives, and so does the opening of it — cut at a whole sentence.
        Assert.Contains("I stood at the feast", faded);
        Assert.Contains("хлябът беше разчупен", faded);
        Assert.DoesNotContain("нарекоха сина си", faded);
        Assert.Contains("call the whole of it back to mind", faded);

        var titled = BeatFade.Fade(beat, BeatFade.TitleBeyond + 1);
        Assert.True(titled.Length < faded.Length);
        Assert.Contains("I stood at the feast", titled);
        Assert.Contains("Ибран", titled);
        Assert.DoesNotContain("хлябът беше разчупен", titled);
        // And she is told, in her own voice, that she can still call it back — which is what makes
        // her reach for recall_birth instead of inventing the day.
        Assert.Contains("call it back to mind", titled);
    }

    [Fact]
    public void EveryGreatAccountFades_AndNothingElseIsTouched()
    {
        var day = WeddingText.WitnessDayBeat("Мизам", "Сибила", "the town of Baltakhand", Account);
        var night = WeddingText.NightBeat(Account);
        var hour = BirthText.MotherHourBeat("Мизам", "the town of Akkalat", "a son", "Ибран", Account);

        foreach (var beat in new[] { day, night, hour, WitnessBeat() })
        {
            Assert.True(BeatFade.IsGreatAccount(beat));
            Assert.True(BeatFade.Fade(beat, 20).Length < beat.Length, beat.Substring(0, 40));
        }

        // Ordinary turns, inner reckonings, night marks and a father's plain mark all ride
        // untouched: they are already short, and thinning them would only lose meaning.
        foreach (var ordinary in new[]
        {
            "Ела, нека отидем в твоята стая и там ще останем до сутринта.",
            Core.Nights.NightText.NamedBeat("Мизам", "the town of Onira", "Хлябът до лампата"),
            BirthText.FatherBeat("Сибила", "the town of Akkalat", "a son", "Ибран", wasThere: false),
            BirthText.GriefBeat("Мизам", "the town of Akkalat", twinLived: false),
        })
        {
            Assert.False(BeatFade.IsGreatAccount(ordinary));
            Assert.Equal(ordinary, BeatFade.Fade(ordinary, 99));
        }

        Assert.Equal(string.Empty, BeatFade.Fade(null, 99));
    }

    [Fact]
    public void TheRecordItselfIsNeverRewritten()
    {
        // The fade is what the PROMPT carries. The turn keeps every word it was born with, so a
        // window that scrolls back does not erase what she actually lived — and the ledgers, which
        // the recall tools read, are untouched by any of this.
        var memory = new NpcMemory();
        var beat = WitnessBeat();
        memory.AddTurn(new ConversationTurn { PlayerLine = beat, NpcLine = string.Empty, Speaker = ConversationTurn.InnerSpeaker });
        for (int i = 0; i < 20; i++)
            memory.AddTurn(new ConversationTurn { PlayerLine = "Здравей.", NpcLine = "Здравей и на теб." });

        Assert.Equal(beat, memory.RecentTurns[0].PlayerLine);
    }

    [Fact]
    public void TheBookkeepingDoesNotGetToHoldTheVerbatimWindow()
    {
        // Anton, 2026.08.11: beats may not take more than a third of what she has word for word.
        var memory = new NpcMemory();
        for (int i = 0; i < 9; i++)
            memory.AddTurn(new ConversationTurn
            {
                PlayerLine = "Battle is behind us, and I set it down in my mind: near Kiraz.",
                NpcLine = string.Empty,
                Speaker = ConversationTurn.InnerSpeaker,
                GameDay = 100 + i,
            });
        for (int i = 0; i < 3; i++)
            memory.AddTurn(new ConversationTurn
            {
                PlayerLine = "Как си днес?", NpcLine = "Добре съм, благодаря.", GameDay = 109 + i,
            });

        // Asked to keep ten, it keeps far fewer — the excess bookkeeping settles deeper instead,
        // which is where it was always headed.
        int keep = memory.GetKeepMostRecentForCompression(
            keepRecentTurns: 10, currentGameDay: 112, keepRecentDays: 0,
            minRecentMemoryTokensAfterCompression: 0);
        Assert.True(keep < 10);

        var kept = memory.RecentTurns.Skip(memory.RecentTurns.Count - keep).ToList();
        int beats = kept.Count(t => string.IsNullOrWhiteSpace(t.NpcLine));
        Assert.True(beats <= System.Math.Max(1, keep / 3), $"{beats} beats in a window of {keep}");
        // And the words actually exchanged all survive.
        Assert.Equal(3, kept.Count(t => !string.IsNullOrWhiteSpace(t.NpcLine)));

        // Turning the rule off restores exactly the old behaviour.
        Assert.Equal(10, memory.GetKeepMostRecentForCompression(10, 112, 0, 0, maxBeatShare: 0));
    }
}
