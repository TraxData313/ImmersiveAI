using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.Core;

namespace ImmersiveAI.UI
{
    /// <summary>
    /// The wedding, played again (2026.08.09, Anton's ask — "като го натисна да се пусне ванила
    /// клипчето със женитбата, и после като кликна... да отваря попъпа със сватбената история").
    /// The keepsake button no longer opens a wall of text: it plays the game's OWN wedding scene
    /// first, and the written account follows the moment the player clicks through it.
    ///
    /// Both halves are the game's own public API, verified against the real DLLs (2026.08.09):
    /// <see cref="MarriageSceneNotificationItem"/> has a public constructor taking the two heroes
    /// and a time, and <see cref="SceneNotificationData.OnCloseAction"/> is virtual — which is the
    /// whole trick, since it is exactly the "click to continue" the player already sees. We subclass
    /// only to hang the account off that close; every scene, banner, character and title stays
    /// vanilla's.
    ///
    /// <para>THE COUPLE ARE DRAWN AT THE AGE THEY WERE (2026.08.15, Anton: "if I remember it when
    /// I'm 50 I don't see two old ppl merrying"). The scene builds its people from the LIVE heroes,
    /// so a wedding replayed twenty years on showed two twenty-years-older faces standing at their
    /// own altar. Vanilla already solved this for itself and we borrow its exact move
    /// (HeirComingOfAgeSceneNotificationItem draws one child at six and again at fourteen): hand
    /// <see cref="CampaignSceneNotificationHelper.CreateNotificationCharacterFromHero"/> an
    /// overridden <see cref="BodyProperties"/> whose <see cref="DynamicBodyProperties"/> carries the
    /// age we want, over the hero's own static face. We do it by REPLACING the two principals in
    /// whatever the base returned, never by reimplementing the method — SceneNotificationCharacter
    /// is a readonly struct with public fields, so the equipment, colours and flags vanilla computed
    /// (the bride's culture wedding dress among them) ride across untouched. The audience is left
    /// alone deliberately: it is whoever is alive and friendly TODAY, so there is no "then" for it
    /// to be drawn at.</para>
    ///
    /// Unlike our map notices, a SceneNotificationData is never written into a save file (it is a
    /// transient popup, not an InformationData), so this type needs no save definer and carries no
    /// migration debt.
    /// </summary>
    public sealed class WeddingSceneReplay : MarriageSceneNotificationItem
    {
        private readonly Action _afterwards;
        private readonly float _groomAge;
        private readonly float _brideAge;

        private WeddingSceneReplay(Hero groom, Hero bride, CampaignTime when, Action afterwards,
            float groomAge, float brideAge)
            : base(groom, bride, when)
        {
            _afterwards = afterwards;
            _groomAge = groomAge;
            _brideAge = brideAge;
        }

        public override void OnCloseAction()
        {
            base.OnCloseAction();
            // Next tick, never inside the closing scene's own callback — the account opens a
            // pausing popup of its own, and the two must not fight over the screen.
            try { MainThreadDispatcher.Enqueue(() => { try { _afterwards?.Invoke(); } catch { } }); }
            catch { }
        }

        /// <summary>Vanilla's own order, decompile-verified: groom, bride, the monk, then six
        /// audience slots. Only the first two are ours to correct.</summary>
        public override SceneNotificationData.SceneNotificationCharacter[] GetSceneNotificationCharacters()
        {
            var people = base.GetSceneNotificationCharacters();
            try
            {
                DrawAtAge(people, 0, GroomHero, _groomAge);
                DrawAtAge(people, 1, BrideHero, _brideAge);
            }
            catch (Exception ex) { ModLog.Error("drawing the wedding at the age it happened", ex); }
            return people;
        }

        private static void DrawAtAge(SceneNotificationData.SceneNotificationCharacter[] people,
            int index, Hero hero, float age)
        {
            if (people == null || index < 0 || index >= people.Length) return;
            if (hero == null || age <= 0f) return;
            // Within a year of today there is nothing to roll back, and rebuilding for nothing
            // would only be one more chance to get vanilla's own composition wrong.
            if (Math.Abs(hero.Age - age) < 1f) return;

            var was = people[index];
            if (was.Character == null) return;

            var body = new BodyProperties(
                new DynamicBodyProperties(age, hero.Weight, hero.Build),
                hero.StaticBodyProperties);

            people[index] = new SceneNotificationData.SceneNotificationCharacter(
                was.Character, was.OverriddenEquipment, body,
                was.UseCivilianEquipment, was.CustomColor1, was.CustomColor2, was.UseHorse);
        }

        /// <summary>Plays the couple's wedding once more and runs <paramref name="afterwards"/> when
        /// the player clicks through it. Answers false when the scene cannot be played at all (an
        /// old save, a missing hero, a game that has moved the API) — the caller then simply opens
        /// the account directly, which is what the button did before this existed.</summary>
        /// <param name="spouseAgeThen">The spouse's age on the day, from the record; 0 when the
        /// record predates our keeping it, and then the age is worked back from the calendar.</param>
        /// <param name="playerAgeThen">The same for the player.</param>
        public static bool TryPlay(Hero spouse, double weddingGameDay, Action afterwards,
            int spouseAgeThen = 0, int playerAgeThen = 0)
        {
            try
            {
                var player = Hero.MainHero;
                if (player == null || spouse == null || !spouse.IsAlive) return false;

                // Vanilla's own reading of the pair: the man is the groom, the woman the bride.
                var groom = spouse.IsFemale ? player : spouse;
                var bride = spouse.IsFemale ? spouse : player;
                if (groom == null || bride == null || groom == bride) return false;
                // The scene dresses the bride from her culture's marriage roster and the groom from
                // his civilian kit; a hero with neither would render as nothing at all.
                if (groom.CivilianEquipment == null || bride.CivilianEquipment == null) return false;

                float spouseAge = AgeOnThatDay(spouse, spouseAgeThen, weddingGameDay);
                float playerAge = AgeOnThatDay(player, playerAgeThen, weddingGameDay);

                var when = weddingGameDay > 0 ? CampaignTime.Days((float)weddingGameDay) : CampaignTime.Now;
                MBInformationManager.ShowSceneNotification(new WeddingSceneReplay(groom, bride, when, afterwards,
                    spouse.IsFemale ? playerAge : spouseAge,
                    spouse.IsFemale ? spouseAge : playerAge));
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("playing the wedding scene again", ex);
                return false;
            }
        }

        /// <summary>The recorded age when we have one, else the calendar's own answer: what they are
        /// now, less the years since. The fallback is what lets every wedding already in a player's
        /// save be drawn honestly, with nothing to migrate.</summary>
        internal static float AgeOnThatDay(Hero hero, int recorded, double gameDay)
        {
            try
            {
                if (recorded > 0) return recorded;
                if (hero == null || gameDay <= 0) return 0f;
                int daysInYear = CampaignTime.DaysInYear;
                if (daysInYear <= 0) return 0f;
                float years = (float)((CampaignTime.Now.ToDays - gameDay) / daysInYear);
                if (years < 1f) return 0f;           // it was this year; nothing to roll back
                float then = hero.Age - years;
                return then < 1f ? 0f : then;        // nonsense (a bad record) draws them as they are
            }
            catch { return 0f; }
        }
    }
}
