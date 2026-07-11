namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// Everything that makes one NPC sound different from another.
    /// Built by the game layer from Hero data; consumed by PromptBuilder.
    /// </summary>
    public sealed class NpcPersona
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>The opening identity/atmosphere line, e.g. "You are Aurelia, a living soul in the world
        /// of Calradia in feudal times." Player-configurable (name already substituted by the game layer);
        /// when empty <see cref="PromptBuilder"/> falls back to its own default. Lets the storyteller set the
        /// whole atmosphere from the config file.</summary>
        public string AtmosphereLine { get; set; } = string.Empty;

        /// <summary>Optional, player-authored guidance on tone and spirit — how the world feels, an invitation
        /// to roleplay and enjoy it — offered gently as freedom, never a command. Folded into the closing
        /// "whisper of guidance". Empty by default (the game layer fills it from config).</summary>
        public string RoleplayGuidance { get; set; } = string.Empty;

        /// <summary>The NPC's kin and house, in their own second-person recollection (parents, spouse,
        /// children with ages, clan and its people). Durable identity, folded in on every chat so they feel
        /// part of a family in this world. Built by the game layer from live Hero data.</summary>
        public string FamilyKnowledge { get; set; } = string.Empty;

        /// <summary>Role and standing, e.g. "Vlandian lord, ruler of Sargot, at war with the Empire".</summary>
        public string RoleDescription { get; set; } = string.Empty;

        /// <summary>Prose rendering of game personality traits (honor, valor, mercy, ...).</summary>
        public string PersonalityDescription { get; set; } = string.Empty;

        /// <summary>
        /// A distinct voice assigned to this NPC (vocabulary, sentence rhythm, verbal tics).
        /// Giving every NPC its own speech style is a primary anti-repetition lever.
        /// </summary>
        public string SpeechStyle { get; set; } = string.Empty;

        /// <summary>The NPC's own evolving sense of who they are, authored by them (not the player)
        /// during reflection. Distinct from the game-given traits above and from the user-authored
        /// instructions below; it is the self they have grown into. Kept in its own file. See
        /// <see cref="ImmersiveAI.Core.Memory.NpcSelf"/>.</summary>
        public string SelfConcept { get; set; } = string.Empty;

        /// <summary>The personal aims this NPC carries, of their own will — held apart from the self
        /// because they are what the NPC strives *toward*, not who they are. Authored by them (the
        /// tend_goals tool mid-conversation, wholesale in reflection), kept in their own file. Folded
        /// into the prompt as "What you strive for". See <see cref="ImmersiveAI.Core.Memory.NpcGoals"/>.</summary>
        public System.Collections.Generic.List<string> Goals { get; set; } = new System.Collections.Generic.List<string>();

        /// <summary>Optional world-wide, user-authored instructions shared by every NPC
        /// (the global prompt file). Shown near the top as "About Calradia:".</summary>
        public string WorldInstructions { get; set; } = string.Empty;

        /// <summary>Optional user-authored extra instructions for THIS NPC (per-NPC prompt file).
        /// Shown near the top as "About you:".</summary>
        public string CustomInstructions { get; set; } = string.Empty;

        /// <summary>True when this NPC can reach into the world's memory mid-thought (the recall
        /// tools are on offer — see the game layer's WorldRecall). Adds a whisper telling them to
        /// trust what surfaces over invention.</summary>
        public bool CanRecallWorld { get; set; }

        /// <summary>True when this NPC can also seek "the counsel of the far-seeing sages" — a web
        /// search, framed in-world — when asked how something in the world is done. Adds a whisper
        /// offering the counsel and reminding them to answer in their own world's words.</summary>
        public bool CanSeekWisdom { get; set; }

        /// <summary>True when this NPC may move their own regard for the one they speak with
        /// mid-reply (the move_heart tool rides along — see the game layer's HeartTool). Adds a
        /// whisper that their heart is theirs to move — and that most words leave it where it
        /// stood. When false the game layer asks the feeling in a separate call instead.</summary>
        public bool CanMoveHeart { get; set; }

        /// <summary>True when this NPC may tend their own aims mid-conversation (the tend_goals tool
        /// rides along — see the game layer's GoalTool). Adds a whisper that their aims are theirs to
        /// keep, take up, or let go — sparingly, only when something truly shifts what they strive for.
        /// Even when false, reflection still lets them rework their aims.</summary>
        public bool CanTendGoals { get; set; }

        /// <summary>True when this NPC may set down a lasting truth mid-conversation (the hold_truth
        /// tool rides along — see the game layer's TruthTool). Adds a whisper that what deserves to
        /// stay may be quietly kept. Even when false, reflection still rewrites their truths whole.</summary>
        public bool CanHoldTruths { get; set; }
    }
}
