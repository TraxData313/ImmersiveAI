using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Probe.ToolCopies;
using ImmersiveAI.Tools;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Probe
{
    /// <summary>One reconstructed call as the game would make it.</summary>
    public sealed class ProbeCase
    {
        public string Id = "";
        public string Npc = "";
        public string Flow = "";               // "message" | "first word" | "letter" | "letter reply"
        public IReadOnlyList<ChatMessage> Messages = Array.Empty<ChatMessage>();
        public List<ToolDefinition> Tools = new List<ToolDefinition>();
        /// <summary>True where the game hands CompleteSpokenAsync a HeartTool.Tally (the player turn only).</summary>
        public bool GameGivesTally;
        public string Recorded = "";           // what the game recorded for this exact moment
        public string Notes = "";
        // For the weights tally (Step 3): the pieces the sheet was built from.
        public NpcPersona? Persona;
        public NpcMemory? Memory;
        public string Scene = "";
        public string PlayerName = "Renaud";
    }

    /// <summary>
    /// Rebuilds the real message lists from the player's runtime files (read from COPIES).
    /// Core's PromptBuilder assembles everything; the parts the Module builds from live game
    /// state (role, traits, crafts, kin) are hand-approximated below and flagged as such.
    /// </summary>
    public static class Cases
    {
        public static readonly string Runtime = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord", "Configs", "ImmersiveAI");
        public static readonly string Campaign = Path.Combine(Runtime, "NPCs", "campaign_93b4c442_Eren");

        public static JObject Config()
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(Runtime, "config.json")));
        }

        // The Module's own speech-style table and hash (PersonaBuilder.PickSpeechStyle).
        private static readonly string[] SpeechStyles =
        {
            "Terse and blunt; short sentences, dry wit, no flattery.",
            "Warm and talkative; fond of small anecdotes and proverbs.",
            "Formal and courtly; precise words, never vulgar, subtle irony.",
            "Rough soldier's speech; earthy metaphors, occasional dark humor.",
            "Soft-spoken and thoughtful; pauses to weigh words, asks questions back.",
            "Boastful and loud; exaggerates own deeds, quick to laugh.",
            "Suspicious and guarded; answers narrowly, probes for motives.",
            "Cheerful merchant's patter; quick, practical, always angling for advantage.",
            "Old and weary; speaks slowly, references the past, gives unasked-for advice.",
            "Pious and solemn; invokes the heavens, moralizes gently.",
            "Sharp and impatient; interrupts pleasantries, wants the point.",
            "Playful and teasing; answers with jokes first, substance second.",
        };

        public static string SpeechStyleFor(string stringId)
        {
            int hash = 17;
            foreach (var c in stringId) hash = unchecked(hash * 31 + c);
            return SpeechStyles[Math.Abs(hash) % SpeechStyles.Length];
        }

        /// <summary>A copy of memories.json, with every turn at/after <paramref name="cutoffUtc"/>
        /// dropped (the moment being re-simulated and anything after it).</summary>
        public static NpcMemory LoadMemory(string folder, string npcId, string? cutoffUtc, string workDir)
        {
            var src = Path.Combine(Campaign, folder, "memories.json");
            Directory.CreateDirectory(workDir);
            var copy = Path.Combine(workDir, folder + "_memories.copy.json");
            File.Copy(src, copy, overwrite: true);
            var memory = new JsonMemoryStore(workDir).LoadFrom(copy, npcId);
            if (cutoffUtc != null)
            {
                var cut = DateTime.Parse(cutoffUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
                memory.RecentTurns.RemoveAll(t => t.TimestampUtc.ToUniversalTime() >= cut);
            }
            return memory;
        }

        private static string ReadSituation(string folder) =>
            File.ReadAllText(Path.Combine(Campaign, folder, "current_situation_info.txt"));

        private static string ReadCustom(string folder)
        {
            var path = Path.Combine(Campaign, folder, "custom_instructions.txt");
            if (!File.Exists(path)) return "";
            return string.Join("\n", File.ReadAllLines(path)
                .Where(l => !l.TrimStart().StartsWith("#") && !l.TrimStart().StartsWith("//"))).Trim();
        }

        private static string ReadGlobal()
        {
            var path = Path.Combine(Runtime, "global_prompt.txt");
            if (!File.Exists(path)) return "";
            return string.Join("\n", File.ReadAllLines(path)
                .Where(l => !l.TrimStart().StartsWith("#") && !l.TrimStart().StartsWith("//"))).Trim();
        }

        private static NpcPersona BasePersona(JObject cfg, string name)
        {
            return new NpcPersona
            {
                Name = name,
                AtmosphereLine = ((string?)cfg["AtmosphereLine"] ?? "").Replace("{name}", name),
                RoleplayGuidance = SheetGuidance((string?)cfg["RoleplayGuidance"] ?? "").Replace("{name}", name),
                WorldInstructions = ReadGlobal(),
                CanRecallWorld = true,
                CanSeekWisdom = true,
                CanMoveHeart = true,
                CanRecallChronicle = true,
                EncourageActingOut = (bool?)cfg["EnableActingOut"] ?? true,
                ReplyLength = PromptBuilder.ReplyLength.Conversational,
                // Qwen is the engine in claude-voice's config: a mood key, no sounds.
                VoiceTakesMood = (bool?)cfg["EnableVoice"] ?? false,
                VoiceSounds = new List<string>(),
                EraNorm = ((bool?)cfg["EnableLoversRoad"] ?? false) && ((bool?)cfg["EnableConversationMarriage"] ?? false)
                    ? ImmersiveAI.Core.Courtship.LoverText.TheOrderOfTheWorld : "",
            };
        }

        // Mirrors of the Module's ModConfig (the probe cannot reference it): the shipped default and
        // the retired two-bullet one it migrates from.
        private const string ShippedGuidance =
            "- My words carry the feel of these old feudal days — a light medieval colour, at times a cadence of the old tongue — but lightly, never thick with poetry.";
        private const string PreviousGuidance = ShippedGuidance + "\n" +
            "- Above all, I live here, and I am glad of it. I play, jest, wonder, argue, grieve, love — small things and great ones alike, what it is to be alive, what lies beyond. This place is a haven, not a stage; I am wholly myself here, however my heart wills.";

        /// <summary>What the spoken sheet carries of the config's RoleplayGuidance, as the game does
        /// it since Step 3 (2026.10.01): ModConfig.Normalize repairs mangled punctuation and migrates
        /// the retired default; BuildContext then leaves the shipped default out (the sheet's own tone
        /// line says it). --guidance raw reproduces the pre-Step-3 behaviour for a before-tally.</summary>
        public static bool RawGuidance;
        private static string SheetGuidance(string config)
        {
            if (RawGuidance) return config;
            var g = ImmersiveAI.Core.Text.Mojibake.Repair(config);
            if (string.Equals(g.Trim(), PreviousGuidance, StringComparison.Ordinal)) g = ShippedGuidance;
            return string.Equals(g.Trim(), ShippedGuidance, StringComparison.Ordinal) ? "" : g;
        }

        /// <summary>Sets NpcPersona.OnPaper when Core has it (Step 3); a no-op on older Core.</summary>
        private static void OnPaper(NpcPersona p) =>
            typeof(NpcPersona).GetProperty("OnPaper")?.SetValue(p, true);

        // The game day each case stands at (Ira: 1085.03.10 19.41, just after the jewel night).
        private const double IraToday = 91191.85;
        private const double RhagaeaToday = 91191.35;

        /// <summary>
        /// The situation file with its battle and nights sections REBUILT from the campaign's own
        /// ledgers through the real Core builders, so a change to BattleText/NightText shows in the
        /// tally (the file on disk is a frozen snapshot of what the game once wrote).
        /// </summary>
        public static string RebuildScene(string scene, string npcId, double today)
        {
            var battles = ImmersiveAI.Core.Battles.BattleLedger.LoadFrom(Path.Combine(Campaign, "_battles"));
            var shared = battles.SharedWith(npcId);
            var nights = ImmersiveAI.Core.Nights.NightLedger.LoadFrom(Path.Combine(Campaign, "_nights.json"));
            var hers = nights.For(npcId);
            scene = ReplaceSection(scene, PromptBuilder.Sections.SharedBattles,
                shared.Count == 0 ? null : ImmersiveAI.Core.Battles.BattleText.SituationBlock(shared, "Renaud", mentionRecall: true, today: today));
            // TroubleBuilder runs the quest giver's words through StripMarkup, which now drops the
            // dialogue animation cues; the frozen file still carries them.
            scene = System.Text.RegularExpressions.Regex.Replace(scene, @"\[(?:ib|if|rb|rf):[^\]\r\n]*\]", string.Empty);
            // SituationBuilder's heading no longer leaves "About Renaud, my husband,:" (Step 3).
            if (!RawGuidance) scene = scene.Replace(", my husband,:", ", my husband:");
            scene = ReplaceSection(scene, PromptBuilder.Sections.TheNights,
                hers.Count == 0 ? null : ImmersiveAI.Core.Nights.NightText.BuildRoll(hers, today, 5,
                    ImmersiveAI.Core.Nights.NightText.DefaultFullAccountBudget, nights.MarksFor(npcId)));
            return scene;
        }

        // Replaces the body of one [[section:…]] (up to the next mark, the moment, or the end).
        private static string ReplaceSection(string scene, string title, string? body)
        {
            var mark = PromptBuilder.Section(title);
            int at = scene.IndexOf(mark, StringComparison.Ordinal);
            if (at < 0 || body == null) return scene;
            int from = at + mark.Length;
            int next = scene.Length;
            foreach (var stop in new[] { PromptBuilder.SectionOpen, PromptBuilder.MeetingSeparator })
            {
                int n = scene.IndexOf(stop, from, StringComparison.Ordinal);
                if (n >= 0 && n < next) next = n;
            }
            return scene.Substring(0, from) + "\n" + body.Trim() + "\n\n" + scene.Substring(next);
        }

        private static NpcPersona IraPersona(JObject cfg)
        {
            var p = BasePersona(cfg, "Ira");
            // APPROXIMATED (Module-built from live game state): role, traits, crafts, kin.
            p.RoleDescription = "A Empire noble of clan de Valmond, sworn to Southern Empire — a woman of some 22 years.";
            p.PersonalityDescription = "Unremarkable temperament.";
            p.Crafts = "What my hands and wits are honestly good at: able in Riding and Polearm; fair in One Handed and Leadership.";
            p.SpeechStyle = SpeechStyleFor("lord_1_37");
            p.FamilyKnowledge = "My kin and house, close to me:\nI am wed to Renaud, a man of some 31 years. My mother is Rhagaea, who rules the Southern Empire; my father Arenicos is dead. My clan is de Valmond, led by my husband Renaud.";
            p.CanSurveyField = true;
            p.CanWeighTheDoor = true;
            p.CustomInstructions = ReadCustom("lord_1_37_Ira");
            return p;
        }

        private static NpcPersona RhagaeaPersona(JObject cfg)
        {
            var p = BasePersona(cfg, "Rhagaea");
            p.RoleDescription = "A Empire noble of clan Pethros, sworn to Southern Empire — a woman of some 46 years.";
            p.PersonalityDescription = "Calculating, merciful.";
            p.Crafts = "What my hands and wits are honestly good at: masterly in Leadership and Charm; able in Steward, Riding and Polearm.";
            p.SpeechStyle = SpeechStyleFor("lord_1_14");
            p.FamilyKnowledge = "My kin and house, close to me:\nI am the widow of Arenicos. My daughter Ira is wed to Renaud of clan de Valmond. My clan is Pethros, which I lead, and I rule the Southern Empire.";
            p.CustomInstructions = ReadCustom("lord_1_14_Rhagaea");
            // Every Rhagaea case is a letter: the game builds it onPaper (no voice key) since Step 2.
            p.VoiceTakesMood = false;
            OnPaper(p);
            return p;
        }

        public static List<ToolDefinition> IraTools(bool withDoor)
        {
            var t = new List<ToolDefinition>();
            t.AddRange(WorldRecall.Tools);
            t.AddRange(FieldCraft.Tools);
            t.Add(WebWisdom.Tool);
            t.Add(HeartTool.Tool);
            t.Add(ChronicleTool.Tool);
            t.Add(NuptialTool.Tool);
            if (withDoor) t.Add(DoorTool.Tool);
            return t;
        }

        public static List<ToolDefinition> RhagaeaTools()
        {
            var t = new List<ToolDefinition>();
            t.AddRange(WorldRecall.Tools);
            t.Add(WebWisdom.Tool);
            t.Add(HeartTool.Tool);
            t.Add(ChronicleTool.Tool);
            t.Add(NuptialTool.Tool);
            return t;
        }

        /// <summary>The apart shape for a letter (SituationBuilder Moment.Apart), from the stale
        /// meeting-shaped file: the clock moved to the letter's hour and the closing line rewritten.</summary>
        private static string RhagaeaApartSituation(string hour)
        {
            var s = ReadSituation("lord_1_14_Rhagaea");
            s = s.Replace("It is early afternoon — 1085.03.01 13.51 (Autumn 1, Year 1085)", hour);
            s = s.Replace("- Renaud married Ira — earlier today.", "- Renaud married Ira — some 7 days past.");
            s = s.Replace("About Renaud:", "My thoughts turn to Renaud, my daughter's husband, who is far from me now — the road between us is long.");
            return s;
        }

        public static ProbeCase Build(string id, string workDir, string? line = null)
        {
            var cfg = Config();
            var pb = new PromptBuilder();
            switch (id)
            {
                case "ira_reply":
                {
                    var mem = LoadMemory("lord_1_37_Ira", "lord_1_37", "2026-10-01T07:45:52Z", workDir);
                    var persona = IraPersona(cfg);
                    var scene = RebuildScene(ReadSituation("lord_1_37_Ira"), "lord_1_37", IraToday);
                    // --line swaps in another player line on the same memory and scene (2026.10.01:
                    // warm / hurtful / neutral / a question needing a recall, for the heart field).
                    var msgs = pb.Build(persona, mem, scene, "Renaud",
                        line ?? "Hey, wait, you didnt tell me?", "Angel");
                    return new ProbeCase
                    {
                        Persona = persona, Memory = mem, Scene = scene,
                        Id = id, Npc = "Ira", Flow = "message", Messages = msgs, Tools = IraTools(true), GameGivesTally = true,
                        Recorded = "*I tuck myself against him, smiling.* There. Have I paid my debt, or must I tell you how handsome you looked by lamplight too? Sleep, love.",
                    };
                }
                case "ira_smile":
                {
                    var mem = LoadMemory("lord_1_37_Ira", "lord_1_37", "2026-10-01T07:45:18Z", workDir);
                    var msgs = pb.Build(IraPersona(cfg), mem, ReadSituation("lord_1_37_Ira"), "Renaud",
                        "You made me smille all day, thank you so much for that! What made you smille that you want to share, I know this question is just the lead up to yours and I cant wait to hear it", "Angel");
                    return new ProbeCase
                    {
                        Id = id, Npc = "Ira", Flow = "message", Messages = msgs, Tools = IraTools(true), GameGivesTally = true,
                        Recorded = "*I settle closer against him and close my eyes.* Good night, my love.",
                    };
                }
                case "ira_firstword":
                {
                    var mem = LoadMemory("lord_1_37_Ira", "lord_1_37", "2026-10-01T07:39:37Z", workDir);
                    var msgs = pb.BuildInnerPrompt(IraPersona(cfg), mem, ReadSituation("lord_1_37_Ira"), "Renaud",
                        PromptBuilder.FirstWordLine("Renaud"), "Angel");
                    return new ProbeCase
                    {
                        Id = id, Npc = "Ira", Flow = "first word", Messages = msgs, Tools = IraTools(false), GameGivesTally = false,
                        Recorded = "*I stay beside him, my hand resting lightly in his.* We've earned our supper, haven't we? Tell me what troubled you today, and I'll tell you whether it can wait till morning.",
                        Notes = "Situation file is the 19.41 one written for this very evening; the Near shape differs only in the closing line.",
                    };
                }
                case "rh_compose":
                {
                    var mem = LoadMemory("lord_1_14_Rhagaea", "lord_1_14", null, workDir);
                    var persona = RhagaeaPersona(cfg);
                    var scene = RebuildScene(RhagaeaApartSituation("It is morning — 1085.03.10 08.12 (Autumn 10, Year 1085)"),
                        "lord_1_14", RhagaeaToday);
                    var msgs = pb.BuildInnerPrompt(persona, mem, scene, "Renaud",
                        PromptBuilder.ComposeLetterLine("Renaud"), "Angel");
                    return new ProbeCase
                    {
                        Persona = persona, Memory = mem, Scene = scene,
                        Id = id, Npc = "Rhagaea", Flow = "letter", Messages = msgs, Tools = RhagaeaTools(), GameGivesTally = false,
                        Recorded = "(new spontaneous letter — compare with the 09-30 08:00 compose: \"Renaud has become family in truth, not merely by oath…\")",
                    };
                }
                case "rh_reply":
                {
                    var mem = LoadMemory("lord_1_14_Rhagaea", "lord_1_14", "2026-09-30T17:54:38Z", workDir);
                    var msgs = pb.BuildInnerPrompt(RhagaeaPersona(cfg), mem,
                        RhagaeaApartSituation("It is early morning — 1085.03.08 06.31 (Autumn 8, Year 1085)"), "Renaud",
                        PromptBuilder.ComposeReplyLine("Renaud"), "Angel");
                    return new ProbeCase
                    {
                        Id = id, Npc = "Rhagaea", Flow = "letter reply", Messages = msgs, Tools = RhagaeaTools(), GameGivesTally = false,
                        Recorded = "For now, the letter may travel without my thoughts pursuing it. Onira has kept me awake long enough; I shall rest while it is quiet.",
                    };
                }
            }
            throw new ArgumentException("unknown case " + id + " (ira_reply, ira_smile, ira_firstword, rh_compose, rh_reply)");
        }
    }
}
