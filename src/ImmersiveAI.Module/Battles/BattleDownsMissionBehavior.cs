using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ImmersiveAI.Battles
{
    /// <summary>
    /// The true tally of whose hand felled whom, kept only while a battle is actually fought on
    /// the field.
    ///
    /// Why it must exist: in a FOUGHT mission the campaign's own OnHeroCombatHitEvent carries an
    /// isFatal flag the game computes as (the victim's health AFTER the blow − that same blow's
    /// damage &lt; 1) — the damage subtracted twice — so every heavy hit that leaves a man with
    /// less life than it took is announced as a killing one. Four bandits could answer for six
    /// downs. The removal event below fires exactly once per soul who falls and names the hand
    /// that felled them, so on the field we count that instead. In a SIMULATED battle no mission
    /// is on stage and the campaign event's flag is the honest finishing strike — that path is
    /// untouched (see ImmersiveChatBehavior.OnHeroCombatHitForChronicle, which stands down
    /// whenever a mission is running).
    /// </summary>
    internal sealed class BattleDownsMissionBehavior : MissionLogic
    {
        private ImmersiveChatBehavior? _behavior;
        private bool _looked;

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try
            {
                base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);

                // Only a real battle of the player's is chronicled — never an arena bout, a
                // tournament, or a village practice ring.
                if (MapEvent.PlayerMapEvent == null) return;
                if (agentState != AgentState.Unconscious && agentState != AgentState.Killed) return;
                if (affectedAgent == null || !affectedAgent.IsHuman) return;
                if (affectorAgent == null) return;

                // A horse that tramples a man does it for its rider — the game reads it that way too.
                if (affectorAgent.IsMount && affectorAgent.RiderAgent != null)
                    affectorAgent = affectorAgent.RiderAgent;

                if (affectorAgent == affectedAgent) return;          // one's own end is nobody's deed
                if (!affectorAgent.IsEnemyOf(affectedAgent)) return; // never a comrade's fall

                // Only our own side's hands are chronicled — an enemy lord's work is his own story,
                // and counting it here would make the tally outrun our foes' true losses.
                var team = affectorAgent.Team;
                if (team == null || !team.IsValid || !(team.IsPlayerTeam || team.IsPlayerAlly)) return;

                var character = affectorAgent.Character as CharacterObject;
                var hero = character != null && character.IsHero ? character.HeroObject : null;
                if (hero == null) return;

                Behavior()?.TallyBattleDownOnTheField(hero.StringId);
            }
            catch { /* a lost mark is nothing; the battle goes on */ }
        }

        private ImmersiveChatBehavior? Behavior()
        {
            if (_looked) return _behavior;
            _looked = true;
            try { _behavior = Campaign.Current?.GetCampaignBehavior<ImmersiveChatBehavior>(); }
            catch { _behavior = null; }
            return _behavior;
        }
    }
}
