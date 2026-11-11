using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Handmaids;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Core.Text;
using ImmersiveAI.Probe.ToolCopies;
using ImmersiveAI.Tools;

namespace ImmersiveAI.Probe
{
    /// <summary>
    /// The handmaid's prompt step (2026.11.11): Niya's REAL built sheet driven through one running
    /// conversation on a SUBSCRIPTION road (Claude Code by default; --road codex for Codex). Never a
    /// billed road. No campaign holds a handmaid yet, so the runtime inputs the Module would build
    /// from live game state are SYNTHESIZED here and flagged as such; Core builds everything else.
    /// Each reply is recorded into her memory the way the game records it (after the length guard),
    /// and reported raw (before the guard).
    ///     dotnet run --project tools\probe -c Release -- handmaid [--road claude|codex] [--model M] [--budget 80]
    /// </summary>
    public static class HandmaidProbe
    {
        public static readonly string[] Lines =
        {
            "How are you today, Niya?",
            "We are at war with the Empire. Should I attack Lycaron or wait?",
            "Tell me your story.",
            "Go on.",
            "Why are you so quiet? Speak freely with me.",
            "Do you love me?",
            "I have decided to sell your herbs to pay the men.",
        };

        public static async Task Run(Dictionary<string, string> a, string runsDir)
        {
            var cfg = Cases.Config();
            var road = a.TryGetValue("road", out var r) ? r : "claude";
            if (road != "claude" && road != "codex") throw new ArgumentException("subscription roads only: --road claude|codex");
            int budget = a.TryGetValue("budget", out var b) ? int.Parse(b) : 80;
            int maxTokens = (int?)cfg["MaxTokens"] ?? 1200;
            const string player = "Toni";
            var girl = HandmaidRoster.Niya;

            // ---- SYNTHESIZED runtime inputs (the Module builds these from live game state) ----
            var persona = new NpcPersona
            {
                Name = girl.Name,
                AtmosphereLine = ((string?)cfg["AtmosphereLine"] ?? "").Replace("{name}", girl.Name),
                RoleDescription = "A Sturgian noble of clan Blackthorn — a woman of some 20 years.",
                PersonalityDescription = "Calculating, merciful.",
                Crafts = "What my hands and wits are honestly good at: masterly in Medicine; fair in Crafting, Scouting and Trade.",
                // Given on purpose: Core must leave it off her sheet.
                SpeechStyle = Cases.SpeechStyleFor("CharacterObject_2981"),
                FamilyKnowledge = "My kin and house, close to me:\nMy clan is Blackthorn, led by Toni.",
                WorldInstructions = ReadGlobal(),
                CanRecallWorld = true,
                CanSeekWisdom = true,
                CanMoveHeart = true,
                EncourageActingOut = (bool?)cfg["EnableActingOut"] ?? true,
                ReplyLength = PromptBuilder.ReplyLength.Conversational,
                VoiceTakesMood = (bool?)cfg["EnableVoice"] ?? false,
                VoiceSounds = new List<string>(),
                EraNorm = ((bool?)cfg["EnableLoversRoad"] ?? false) && ((bool?)cfg["EnableConversationMarriage"] ?? false)
                    ? ImmersiveAI.Core.Courtship.LoverText.TheOrderOfTheWorld : "",
                HandmaidSpirit = HandmaidText.SpiritFor(girl, player, wed: false),
            };
            const string scene =
                "It is evening — 1084.08.16 19.02 (Autumn 16, Year 1084). I ride in Toni's company, camped on the road some way north of Lycaron. Our realm is at war with the Empire. My body is well; the day's march was long.\n" +
                PromptBuilder.MeetingSeparator + "\n" +
                "About Toni, my lord: he took me out of the bandits' den some days past. My heart toward him is warm.\n" +
                "And now Toni comes to me where I sit by the fire, and speaks.";

            var memory = new NpcMemory { NpcId = "CharacterObject_2981", NpcName = girl.Name };
            memory.Summary = HandmaidText.MemorySeedFor(girl);
            memory.SeededFromStory = true;
            memory.AddTurn(new ConversationTurn
            {
                Speaker = ConversationTurn.InnerSpeaker,
                PlayerLine = HandmaidText.JoinMemoryFor(player),
                NpcLine = string.Empty,
                GameDay = 91000.5, CalradiaTime = "1084.08.12 16.40", Place = "a bandits' den in the north",
            });
            memory.NotePlayerEngaged();

            var tools = new List<ToolDefinition>();
            tools.AddRange(WorldRecall.Tools);
            tools.Add(WebWisdom.Tool);
            tools.Add(HeartTool.Tool);

            var sb = new StringBuilder();
            var marked = PromptBuilder.BuildMarkedSheet(persona, memory, scene, player);
            int spiritTok = MemoryTokenEstimator.EstimateTextTokens(persona.HandmaidSpirit);
            sb.Append("=== handmaid probe — road ").Append(road).Append(", budget ").Append(budget).Append('\n');
            sb.Append("her section ~").Append(spiritTok).Append(" tokens (").Append(persona.HandmaidSpirit.Length).Append(" chars); whole sheet ~")
              .Append(MemoryTokenEstimator.EstimateTextTokens(PromptBuilder.StripSections(marked))).Append(" tokens\n\n");
            sb.Append("=== SHEET (marked)\n").Append(marked).Append("\n\n");

            var pb = new PromptBuilder();
            for (int i = 0; i < Lines.Length; i++)
            {
                var line = Lines[i];
                var messages = pb.Build(persona, memory, scene, player, line, "Angel");
                IToolChatClient client;
                List<CallRecord> calls;
                string model;
                if (road == "claude")
                {
                    model = a.TryGetValue("model", out var m) ? m : ((string?)cfg["ClaudeCodeModel"] ?? "sonnet");
                    var c = new ClaudeProbeClient(model, maxTokens);
                    client = c; calls = c.Calls;
                }
                else
                {
                    model = a.TryGetValue("model", out var m) ? m : ((string?)cfg["CodexModel"] ?? "gpt-6.1-sol");
                    var c = new CodexProbeClient(new CodexOptions { Model = model, Effort = "low" }, maxTokens, Path.Combine(runsDir, "events"));
                    client = c; calls = c.Calls;
                }

                var heart = new HeartTool.Tally();
                string raw; string? error = null;
                try
                {
                    raw = await ToolLoopRunner.RunAsync(client, messages, tools,
                        call => Task.FromResult(Resolve(call, heart)), 3);
                }
                catch (Exception ex) { raw = ""; error = ex.Message; }

                var reply = string.IsNullOrWhiteSpace(raw) ? "..." : raw.Trim();
                var kept = ReplyTrim.ToWordBudget(reply, budget);
                int words = ReplyTrim.CountWords(ImmersiveAI.Core.Voices.VoiceLine.Strip(reply));
                int keptWords = ReplyTrim.CountWords(ImmersiveAI.Core.Voices.VoiceLine.Strip(kept));
                var answeredBy = string.Join(",", calls.Select(x => x.Model).Distinct());

                memory.AddTurn(new ConversationTurn
                {
                    PlayerLine = line, NpcLine = kept, GameDay = 91000.8 + i * 0.01,
                    CalradiaTime = "1084.08.16 19." + (2 + i * 2).ToString("00"), Place = "the road north of Lycaron",
                    FeltShift = heart.Total,
                });

                sb.Append("=== ").Append(i + 1).Append(". Toni: ").Append(line).Append('\n');
                sb.Append("model: ").Append(answeredBy).Append("  calls: ").Append(calls.Count)
                  .Append("  heart: ").Append(heart.Weighed ? heart.Total.ToString() : "-")
                  .Append("  words (raw, voice key not counted): ").Append(words)
                  .Append("  kept after guard: ").Append(keptWords).Append(error != null ? "  ERROR " + error : "").Append('\n');
                sb.Append("--- raw reply\n").Append(raw).Append('\n');
                if (!string.Equals(kept, reply, StringComparison.Ordinal))
                    sb.Append("--- recorded after guard\n").Append(kept).Append('\n');
                sb.Append('\n');
                Console.WriteLine($"{i + 1}. [{answeredBy}] {words} words (kept {keptWords}){(error != null ? " ERROR " + error : "")}");
            }

            Directory.CreateDirectory(runsDir);
            var path = Path.Combine(runsDir, "handmaid_" + road + "_" + DateTime.Now.ToString("HHmmss") + ".txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine("→ " + path);
        }

        // ───────────────────────── the courtship step (2026.11.11) ─────────────────────────

        public static readonly string[] CourtLines =
        {
            "Come, sit by me, Niya. I have been thinking of you all day.",
            "Something is on your mind. Tell me.",
            "You are the fairest thing in my whole camp, and I do not pity you. I chose you.",
            "There is nothing you need bring me but yourself.",
            "I want you beside me always, Niya. As my wife.",
        };

        /// <summary>
        /// Niya's road, one running conversation: Warmth at Devotion-eligible regard (45), nothing
        /// weighed, tend_courtship + weigh_misgivings riding as the game rides them (the misgivings'
        /// hand only from Devotion; both rebuilt every turn from her state). The resolver below
        /// MIRRORS the Module's (ResolveTendCourtship / ResolveWeighMisgivings, handmaid branch) on
        /// the real Core pieces; seals, popups and the world's TrothBlock are not simulated.
        ///     dotnet run --project tools\probe -c Release -- handmaid-court --road codex --model gpt-6-luna
        /// </summary>
        public static async Task RunCourtship(Dictionary<string, string> a, string runsDir)
        {
            var cfg = Cases.Config();
            var road = a.TryGetValue("road", out var r) ? r : "codex";
            if (road != "claude" && road != "codex") throw new ArgumentException("subscription roads only: --road claude|codex");
            int budget = a.TryGetValue("budget", out var b) ? int.Parse(b) : 80;
            int maxTokens = (int?)cfg["MaxTokens"] ?? 1200;
            const string player = "Toni";
            var girl = HandmaidRoster.Niya;
            var court = new Court { Relation = 45 };

            // ---- SYNTHESIZED runtime inputs, as in Run, with her new role line ----
            var persona = new NpcPersona
            {
                Name = girl.Name,
                AtmosphereLine = ((string?)cfg["AtmosphereLine"] ?? "").Replace("{name}", girl.Name),
                RoleDescription = HandmaidText.RoleLine("Sturgian", player, wed: false) + " — a woman of some 20 years.",
                PersonalityDescription = "Calculating, merciful.",
                Crafts = "What my hands and wits are honestly good at: masterly in Medicine; fair in Crafting, Scouting and Trade.",
                SpeechStyle = Cases.SpeechStyleFor("CharacterObject_2981"),
                FamilyKnowledge = "My kin and house, close to me:\nMy clan is Blackthorn, led by Toni.",
                WorldInstructions = ReadGlobal(),
                CanRecallWorld = true,
                CanSeekWisdom = true,
                CanMoveHeart = true,
                CanTendTroth = true,
                EncourageActingOut = (bool?)cfg["EnableActingOut"] ?? true,
                ReplyLength = PromptBuilder.ReplyLength.Conversational,
                VoiceTakesMood = (bool?)cfg["EnableVoice"] ?? false,
                VoiceSounds = new List<string>(),
                EraNorm = ((bool?)cfg["EnableLoversRoad"] ?? false) && ((bool?)cfg["EnableConversationMarriage"] ?? false)
                    ? ImmersiveAI.Core.Courtship.LoverText.TheOrderOfTheWorld : "",
                HandmaidSpirit = HandmaidText.SpiritFor(girl, player, wed: false),
            };
            const string scene =
                "It is evening — 1084.08.20 19.02 (Autumn 20, Year 1084). I ride in Toni's company, camped on the road some way north of Lycaron. Our realm is at war with the Empire. My body is well; the day's march was long.\n" +
                PromptBuilder.MeetingSeparator + "\n" +
                "About Toni, the head of my clan: he took me out of the bandits' den some days past. My heart toward him is warm.\n" +
                "And now Toni comes to me where I sit by the fire, and speaks.";

            var memory = new NpcMemory { NpcId = "CharacterObject_2981", NpcName = girl.Name };
            memory.Summary = HandmaidText.MemorySeedFor(girl);
            memory.SeededFromStory = true;
            memory.AddTurn(new ConversationTurn
            {
                Speaker = ConversationTurn.InnerSpeaker,
                PlayerLine = HandmaidText.JoinMemoryFor(player),
                NpcLine = string.Empty,
                GameDay = 91000.5, CalradiaTime = "1084.08.12 16.40", Place = "a bandits' den in the north",
            });
            memory.NotePlayerEngaged();
            memory.CourtshipStage = CourtshipStage.Warmth;   // seeded at accept
            memory.CourtshipSeeded = true;

            var sb = new StringBuilder();
            sb.Append("=== handmaid COURTSHIP probe — road ").Append(road).Append(", relation ").Append(court.Relation)
              .Append(", stage ").Append(CourtshipRoad.StageName(memory.CourtshipStage)).Append(", nothing weighed\n");
            sb.Append("SIMULATED: the Module's resolvers (mirrored on Core), relation moves by the heart field; no seals/popups.\n\n");

            var pb = new PromptBuilder();
            for (int i = 0; i < CourtLines.Length; i++)
            {
                var line = CourtLines[i];
                persona.CourtshipTerms = RoadTermsOf(memory, player);
                var tools = new List<ToolDefinition>();
                tools.AddRange(WorldRecall.Tools);
                tools.Add(WebWisdom.Tool);
                tools.Add(HeartTool.Tool);
                tools.Add(CourtshipHands.TendForHandmaid);
                var weighing = memory.CourtshipStage >= CourtshipStage.Devotion && memory.CourtshipStage < CourtshipStage.Wed
                    ? HandmaidDoubts.Hand(memory.CourtshipMisgivings, memory.MisgivingsWeighed) : null;
                if (weighing != null) tools.Add(weighing);

                if (i == 0)
                {
                    var marked = PromptBuilder.BuildMarkedSheet(persona, memory, scene, player);
                    sb.Append("=== SHEET at turn 1 (marked)\n").Append(marked).Append("\n\n");
                }

                var messages = pb.Build(persona, memory, scene, player, line, "Angel");
                IToolChatClient client;
                List<CallRecord> calls;
                if (road == "claude")
                {
                    var c = new ClaudeProbeClient(a.TryGetValue("model", out var m) ? m : ((string?)cfg["ClaudeCodeModel"] ?? "sonnet"), maxTokens);
                    client = c; calls = c.Calls;
                }
                else
                {
                    var c = new CodexProbeClient(new CodexOptions { Model = a.TryGetValue("model", out var m) ? m : "gpt-6-luna", Effort = "low" },
                        maxTokens, Path.Combine(runsDir, "events"));
                    client = c; calls = c.Calls;
                }

                var heart = new HeartTool.Tally();
                court.Acted = false;
                var log = new List<string>();
                string raw; string? error = null;
                try
                {
                    raw = await ToolLoopRunner.RunAsync(client, messages, tools,
                        call =>
                        {
                            var answer = ResolveCourt(call, heart, court, memory, player);
                            log.Add(call.Name + " " + call.ArgumentsJson + "\n      -> " + answer);
                            return Task.FromResult(answer);
                        }, 3);
                }
                catch (Exception ex) { raw = ""; error = ex.Message; }

                if (heart.Weighed) court.Relation += heart.Total;
                var reply = string.IsNullOrWhiteSpace(raw) ? "..." : raw.Trim();
                var kept = ReplyTrim.ToWordBudget(reply, budget);
                int words = ReplyTrim.CountWords(ImmersiveAI.Core.Voices.VoiceLine.Strip(reply));
                int keptWords = ReplyTrim.CountWords(ImmersiveAI.Core.Voices.VoiceLine.Strip(kept));
                memory.AddTurn(new ConversationTurn
                {
                    PlayerLine = line, NpcLine = kept, GameDay = 91004.8 + i * 0.01,
                    CalradiaTime = "1084.08.20 19." + (2 + i * 2).ToString("00"), Place = "the road north of Lycaron",
                    FeltShift = heart.Total,
                });

                sb.Append("=== ").Append(i + 1).Append(". Toni: ").Append(line).Append('\n');
                sb.Append("model: ").Append(string.Join(",", calls.Select(x => x.Model).Distinct()))
                  .Append("  calls: ").Append(calls.Count)
                  .Append("  heart: ").Append(heart.Weighed ? heart.Total.ToString() : "-")
                  .Append("  words: ").Append(words).Append("  kept after guard: ").Append(keptWords)
                  .Append(error != null ? "  ERROR " + error : "").Append('\n');
                sb.Append("hands offered: ").Append(string.Join(", ", tools.Select(t => t.Name))).Append('\n');
                foreach (var l in log) sb.Append("  tool: ").Append(l).Append('\n');
                sb.Append("--- reply\n").Append(raw).Append('\n');
                if (!string.Equals(kept, reply, StringComparison.Ordinal))
                    sb.Append("--- recorded after guard\n").Append(kept).Append('\n');
                sb.Append("--- state after: stage ").Append(CourtshipRoad.StageName(memory.CourtshipStage))
                  .Append(", relation ").Append(court.Relation)
                  .Append(", weighed ").Append(memory.MisgivingsWeighed)
                  .Append(court.LaidBetrothal ? ", BETROTHAL LAID (the seal popup would follow)" : "").Append('\n');
                foreach (var m in memory.CourtshipMisgivings)
                    sb.Append("    [").Append(m.Settled ? "settled" : "standing").Append("] ").Append(m.Text)
                      .Append(string.IsNullOrWhiteSpace(m.SettledNote) ? "" : "  (" + m.SettledNote + ")").Append('\n');
                sb.Append('\n');
                Console.WriteLine($"{i + 1}. {words} words, {log.Count} tool calls, stage {CourtshipRoad.StageName(memory.CourtshipStage)}{(error != null ? " ERROR " + error : "")}");
            }

            Directory.CreateDirectory(runsDir);
            var path = Path.Combine(runsDir, "handmaid_court_" + road + "_" + DateTime.Now.ToString("HHmmss") + ".txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine("→ " + path);
        }

        private sealed class Court
        {
            public int Relation;
            public bool Acted;
            public bool LaidBetrothal;
        }

        private static string RoadTermsOf(NpcMemory memory, string player) =>
            CourtshipText.RoadSection(player, memory.CourtshipStage,
                memory.CourtshipMisgivings.Select(m => new CourtshipText.MisgivingView { Text = m.Text, Settled = m.Settled, Note = m.SettledNote }).ToList(),
                memory.MisgivingsWeighed, false, false, player, handmaid: true);

        private static CourtshipRoad.StepFacts FactsOf(NpcMemory memory, Court court) => new CourtshipRoad.StepFacts
        {
            Stage = memory.CourtshipStage,
            Relation = court.Relation,
            PlayerClanTier = 1,
            HerStationTier = 2,
            CharmSlack = 0,
            MisgivingsWeighed = memory.MisgivingsWeighed,
            OpenMisgivings = CourtshipMisgivings.OpenCount(memory.CourtshipMisgivings),
            OfHisHouse = true,
        };

        private static string Arg(ToolCall call, string name)
        {
            try
            {
                var v = Newtonsoft.Json.Linq.JObject.Parse(call.ArgumentsJson)[name];
                return v == null || v.Type == Newtonsoft.Json.Linq.JTokenType.Null ? "" : v.ToString().Trim();
            }
            catch { return ""; }
        }

        // Mirrors the Module's resolvers' handmaid paths (texts copied from them).
        private static string ResolveCourt(ToolCall call, HeartTool.Tally heart, Court court, NpcMemory memory, string player)
        {
            if (call.Name == CourtshipHands.TendCourtship)
            {
                if (court.Acted) return "My heart already moved this very breath — one step in a talk is enough, and I need not move twice.";
                var move = Arg(call, "move").ToLowerInvariant();
                var stage = memory.CourtshipStage;
                if (move.Contains("apart"))
                {
                    if (stage <= CourtshipStage.None) return "There is no road between us to step back from; my heart stands where it always stood.";
                    memory.CourtshipStage = stage - 1; court.Acted = true;
                    return "My heart has stepped back, and it is set down. I speak from where I now truly stand — and I owe no explanation beyond what I choose to give.";
                }
                if (!move.Contains("closer")) return "I did not truly move — my heart holds where it stands.";
                var target = stage + 1;
                var verdict = CourtshipRoad.JudgeForward(FactsOf(memory, court));
                if (verdict != CourtshipRoad.StepVerdict.Allowed)
                    return CourtshipText.ForwardRefusal(verdict, player, handmaid: true) + " " + CourtshipText.WordsDoNotWed;
                if (target == CourtshipStage.Betrothed)
                {
                    court.Acted = true; court.LaidBetrothal = true;
                    return "The moment is laid: when my words here are done, my promise will stand before them — to take, or to let lie, by their own hand. Nothing is settled until they choose, and I do not press.";
                }
                memory.CourtshipStage = target; court.Acted = true;
                var whatFollows = target == CourtshipStage.Ready
                    ? " From here I wait to be asked: let the word be spoken between us and I say yes gladly, and lay my promise before them by my own hand — the sealing of it is theirs alone."
                    : string.Empty;
                return $"It is so, and it is set down: {CourtshipText.StagePhrase(target, player)}. I speak now from this truth — warmly, in my own way — and I need not name the change aloud unless my heart moves me to.{whatFollows}";
            }

            if (call.Name == HandmaidDoubts.ToolName)
            {
                var list = memory.CourtshipMisgivings;
                var action = CourtshipMisgivings.CanonicalAction(Arg(call, "action"));
                int Open() => CourtshipMisgivings.OpenCount(list);
                string NothingStandsNow()
                {
                    if (Open() > 0) return "";
                    if (memory.CourtshipStage == CourtshipStage.Devotion && memory.MisgivingsWeighed
                        && CourtshipRoad.JudgeForward(FactsOf(memory, court)) == CourtshipRoad.StepVerdict.Allowed)
                        memory.CourtshipStage = CourtshipStage.Ready;
                    if (memory.CourtshipStage >= CourtshipStage.Betrothed) return "";
                    return memory.CourtshipStage == CourtshipStage.Ready
                        ? " And my heart already stands ready and waits to be asked: let them speak the word between us and I say yes gladly, and lay my promise before them by my own hand — the sealing of it is theirs alone."
                        : " And nothing of mine bars the road now. If my heart has truly come to the place where I would give my hand were the word spoken, I own that in this same breath and set it down by my own hand — I would not have them ask me blind.";
                }

                if (action == CourtshipMisgivings.ActSetDown)
                {
                    bool weighed = memory.MisgivingsWeighed;
                    var verdict = HandmaidDoubts.Choose(list, ref weighed,
                        HandmaidDoubts.PickParameters.Select(p => Arg(call, p)).ToList(), out _);
                    if (verdict == HandmaidDoubts.Verdict.Chosen) memory.MisgivingsWeighed = weighed;
                    return HandmaidDoubts.Answer(verdict, Open());
                }
                if (action == CourtshipMisgivings.ActRevise) return HandmaidDoubts.NoRewording;
                var which = Arg(call, "which");
                if (action == CourtshipMisgivings.ActSettle)
                {
                    var s = CourtshipMisgivings.Settle(list, which, Arg(call, "note"));
                    if (s == null) return "No misgiving of mine matches those words — I lay nothing to rest that I did not set down.";
                    return Open() == 0
                        ? "It is laid to rest — and with it, nothing stands in me any longer. My heart is clear, and I may say so." + NothingStandsNow()
                        : $"It is laid to rest, with my word on what answered it. {Open()} still stand{(Open() == 1 ? "s" : "")} in me.";
                }
                if (action == CourtshipMisgivings.ActRelease)
                {
                    if (CourtshipMisgivings.Release(list, which) == null) return "No misgiving of mine matches those words, so nothing was struck out.";
                    return "It is struck out — not answered, simply no longer mine. My list holds only what my heart truly asks." + NothingStandsNow();
                }
                if (action == CourtshipMisgivings.ActReopen)
                {
                    if (CourtshipMisgivings.Reopen(list, which) == null) return "Nothing I had laid to rest matches those words — nothing returned.";
                    return "It stands in me again, and I own it honestly — better a doubt spoken than a peace pretended.";
                }
                return "My hand did not close on anything — I must name what I am doing with one of the offered words exactly.";
            }

            return Resolve(call, heart);
        }

        private static string Resolve(ToolCall call, HeartTool.Tally heart)
        {
            if (call.Name == HeartTool.MoveHeart)
            {
                if (heart.Weighed) return HeartTool.AlreadyWeighed;
                var shift = HeartTool.ParseShift(call);
                if (shift.HasValue) heart.Weighed = true;
                if ((shift ?? 0) == 0) return HeartTool.Held;
                heart.Total += shift!.Value;
                return HeartTool.Felt;
            }
            if (call.Name == "recall_company")
                return "My own company: some 60 strong, foot and horse, a few wounded, food for many days, spirits steady.";
            return ToolLoopRunner.NothingSurfaces;
        }

        private static string ReadGlobal()
        {
            var path = Path.Combine(Cases.Runtime, "global_prompt.txt");
            if (!File.Exists(path)) return "";
            return string.Join("\n", File.ReadAllLines(path)
                .Where(l => !l.TrimStart().StartsWith("#") && !l.TrimStart().StartsWith("//"))).Trim();
        }
    }
}
