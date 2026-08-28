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

        /// <summary>What this NPC's hands and wits are honestly good at, in their own words — their
        /// real skills weighed into craft-words ("masterly in leechcraft, able in scouting…"). Built
        /// by the game layer (CraftsBuilder) from live skill values, so a wanderer asked what they
        /// would be good at, or a captain judging his own scouting, answers from truth.</summary>
        public string Crafts { get; set; } = string.Empty;

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

        /// <summary>Optional world-wide, user-authored instructions shared by every NPC
        /// (the global prompt file). Shown near the top as "Of this world, this I know:".</summary>
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

        /// <summary>True when this NPC is invited to act out small gestures between *asterisks* —
        /// *I smile and meet their eyes* — as the one exception to the plain-speech rule (see
        /// <see cref="PromptBuilder.ActingOutGuidance"/>). The chat window draws such spans as soft
        /// narration; kept sparing by the guidance itself. Set from the game layer's EnableActingOut.</summary>
        public bool EncourageActingOut { get; set; }

        /// <summary>How long they speak — the player's own dial (2026.08.28). Defaults to the
        /// long-standing Conversational, so nothing changes for anyone who never touches it.</summary>
        public PromptBuilder.ReplyLength ReplyLength { get; set; } = PromptBuilder.ReplyLength.Conversational;

        /// <summary>True when this NPC rides with a company on the map and may cast their eyes over
        /// the country and weigh a battle (the field-craft tools ride along — see the game layer's
        /// FieldCraft: survey_surroundings and weigh_battle). Adds a whisper to look before judging
        /// pace, pursuit, escape, or odds aloud.</summary>
        public bool CanSurveyField { get; set; }

        /// <summary>True when this NPC has stood in at least one battle beside the player and the
        /// recall_battle tool rides along (see the game layer's ChronicleTool). Adds a whisper that
        /// their shared battles are set down by name and can be called back whole — spoken from the
        /// record, never from fog.</summary>
        public bool CanRecallChronicle { get; set; }

        /// <summary>True when this NPC is an unhired sellsword speaking with the one who could take
        /// them on, and the strike_bargain tool rides along (see the game layer's BargainTool). Adds
        /// a whisper that the bargain is theirs to strike — but only once service and price were
        /// plainly agreed in words, and the seal always belongs to the other side.</summary>
        public bool CanStrikeBargain { get; set; }

        /// <summary>True when this NPC may tend their own courtship road with the player mid-reply
        /// (the tend_courtship tool rides along — see the game layer's TrothTool). Adds a whisper
        /// that the road is theirs to walk one honest step at a time, forward only when real talks
        /// have earned it — and that laying the betrothal or the wedding settles nothing until the
        /// other side seals it by their own hand.</summary>
        public bool CanTendTroth { get; set; }

        /// <summary>True when this NPC heads a house whose kinswoman (or kinsman) is betrothed to
        /// the player and the bless_marriage tool rides along (see the game layer's TrothTool). Adds
        /// a whisper that the blessing and its bride-price are theirs to lay — never their lowest
        /// price — and that the gold and the choice remain wholly the suitor's.</summary>
        public bool CanBlessTroth { get; set; }

        /// <summary>Her private courtship stance — where her heart stands on the road and the
        /// misgivings she set down by her own hand (standing or laid to rest), built by the game
        /// layer from persisted memory via Core CourtshipText.RoadSection. Folded into the sheet
        /// beside her deep memory of the player; empty when no road is walked.</summary>
        public string CourtshipTerms { get; set; } = string.Empty;

        /// <summary>The suitor's case as a clan head carries it — the bride-price reckoning, his
        /// private bounds, the head-of-house bargaining mind (Core CourtshipText.SuitorTerms).
        /// Empty unless a kinswoman of his is betrothed to the player and his word is wanted.</summary>
        public string SuitorTerms { get; set; } = string.Empty;

        // ------------------------- the lover's fork (2026.08.15) -------------------------

        /// <summary>True when this NPC may offer herself to the player mid-reply — the courtship
        /// road's other branch, where nothing is promised and nothing is sealed before the world
        /// (the offer_myself tool rides along; see the game layer's LoverTool). Adds a whisper that
        /// the offering is hers alone to make, from a love gone past all sense, and that it settles
        /// nothing until the other side takes it by their own hand.</summary>
        public bool CanOfferSelf { get; set; }

        /// <summary>True when this NPC heads a house a woman of his blood is leaving for the player
        /// with no wedding in it, and the name_her_price tool rides along. Adds a whisper that what
        /// her going costs is his to name — never his lowest — and that the gold settles what is
        /// owed to his house and nothing else besides.</summary>
        public bool CanNamePrice { get; set; }

        /// <summary>What she is to the player outside the world's ceremonies, and what the world
        /// holds about women in that place — built by the game layer via Core LoverText.BondSection.
        /// Rides every sheet once the bond stands or once stood, tool or no tool; empty otherwise.
        /// It states the world's position and never her feeling about it: her stance is her own,
        /// and if every woman sounds the same about this the mod has failed its founding rule.</summary>
        public string LoverTerms { get; set; } = string.Empty;

        // ------------------------- the door (2026.08.15) -------------------------

        /// <summary>True when her own hand on the door rides this turn (the weigh_what_stands tool;
        /// see the game layer's DoorTool). Adds a whisper that the list is hers alone, that no warm
        /// hour opens a door while something of hers stands unanswered on it, and equally that she
        /// does not invent a grievance to keep it shut.</summary>
        public bool CanWeighTheDoor { get; set; }

        /// <summary>Why her door is shut, in her own written words, and what she said would answer
        /// each — built by the game layer via Core DoorText. Rides every sheet where a bed exists,
        /// tool or no tool. Empty when there is nothing standing and nothing ever was.</summary>
        public string DoorTerms { get; set; } = string.Empty;

        /// <summary>
        /// THE ORDER OF THE WORLD (2026.08.15) — one short passage of what the ERA holds about a
        /// woman's place, carried by every soul who lives in it. See Core LoverText.TheOrderOfTheWorld.
        ///
        /// Its whole grammar is "so it is held", never "so I feel", and that is the difference
        /// between a world and a personality mandate. Her stance toward this air is her own — her
        /// traits, her spark, her lived story — and the spark deck carries cards for exactly that.
        /// If every woman in a player's game sounds the same about this, the mod has failed its
        /// founding rule, and this field is where that failure would begin.
        /// </summary>
        public string EraNorm { get; set; } = string.Empty;

        /// <summary>
        /// How the player's house is SPOKEN OF — which children he has owned before the world and
        /// which he has not (Core BirthText.HouseLine). Carried only by the women of his hearth,
        /// who would all know it and all have a view, and empty for a house with nothing to explain,
        /// which is most houses. Blood is the game's and untouched; this is the layer of words over
        /// it, and the layer of words is where this mod lives.
        /// </summary>
        public string PlayerHouseLine { get; set; } = string.Empty;
    }
}
