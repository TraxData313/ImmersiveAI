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
    /// Unlike our map notices, a SceneNotificationData is never written into a save file (it is a
    /// transient popup, not an InformationData), so this type needs no save definer and carries no
    /// migration debt.
    /// </summary>
    public sealed class WeddingSceneReplay : MarriageSceneNotificationItem
    {
        private readonly Action _afterwards;

        private WeddingSceneReplay(Hero groom, Hero bride, CampaignTime when, Action afterwards)
            : base(groom, bride, when)
        {
            _afterwards = afterwards;
        }

        public override void OnCloseAction()
        {
            base.OnCloseAction();
            // Next tick, never inside the closing scene's own callback — the account opens a
            // pausing popup of its own, and the two must not fight over the screen.
            try { MainThreadDispatcher.Enqueue(() => { try { _afterwards?.Invoke(); } catch { } }); }
            catch { }
        }

        /// <summary>Plays the couple's wedding once more and runs <paramref name="afterwards"/> when
        /// the player clicks through it. Answers false when the scene cannot be played at all (an
        /// old save, a missing hero, a game that has moved the API) — the caller then simply opens
        /// the account directly, which is what the button did before this existed.</summary>
        public static bool TryPlay(Hero spouse, double weddingGameDay, Action afterwards)
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

                var when = weddingGameDay > 0 ? CampaignTime.Days((float)weddingGameDay) : CampaignTime.Now;
                MBInformationManager.ShowSceneNotification(new WeddingSceneReplay(groom, bride, when, afterwards));
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("playing the wedding scene again", ex);
                return false;
            }
        }
    }
}
