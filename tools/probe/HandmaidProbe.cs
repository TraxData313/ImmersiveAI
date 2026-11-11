using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
