using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// How an answer carries its answer fields (<see cref="ToolDefinition.AnswerField"/> — today the
    /// heart's measure) BESIDE the words, on every road, in one call (2026.10.01, Anton: "just output
    /// it with their answer always, even if it is 0 — not another go that takes time").
    /// <para>The flattened roads (Codex, Claude Code) already answer in a JSON envelope, so a field is
    /// one more required property next to "reply" (see <see cref="ClaudeCliShape.BuildSchema"/>).
    /// The native-tool roads (OpenAI-compatible, Anthropic) answer through ONE required hand,
    /// <see cref="SpeakTool"/>: <c>speak(words, heart)</c>, offered beside the recalls with the tool
    /// choice forced — so every round is either a reach or the finished answer with its measure,
    /// and the forced last round is "speak", never "none". Both shapes come back the same way: the
    /// words as <see cref="ChatResult.Text"/>, each field as a call to its own hand in
    /// <see cref="ChatResult.AnswerCalls"/>, so the resolver that applies a shift never changes.</para>
    /// <para>This is NOT the in-prose mark that failed twice (2026.07.09): a schema field is filled
    /// or left empty, it cannot be narrated. A leaked "heart: 2" line in the words is still removed
    /// (<see cref="StripLeakedMeasure"/>) — defence, not the channel.</para>
    /// </summary>
    public static class AnswerShape
    {
        /// <summary>The one hand the native-tool roads answer through when answer fields ride.</summary>
        public const string SpeakTool = "speak";

        /// <summary>Its parameter holding the spoken reply, whole.</summary>
        public const string WordsParameter = "words";

        /// <summary>
        /// Its optional parameter for the reply's <c>[voice: …]</c> direction (2026.10.01, probed on
        /// gpt-6-luna): the same model that closes plain content with the key, 3 of 3, dropped it
        /// 3 of 3 once the reply rode a JSON argument — a bracketed direction inside data reads as
        /// markup to clean away. Given a place of its own it is folded back as the reply's last
        /// line, so everything downstream (<see cref="Voices.VoiceLine"/>) sees the usual shape.
        /// It is offered — and REQUIRED — only when the sheet itself asks for the line (haiku left an
        /// optional one empty); with voices off, or on paper, the sheet asks for none and the hand
        /// carries no such parameter, so no direction is ever invented.
        /// </summary>
        public const string VoiceParameter = "voice";

        /// <summary>Whether the sheet asks every reply to close with a <c>[voice: …]</c> line
        /// (<see cref="Prompts.PromptBuilder.VoiceGuidance"/> — present only while voices are on and
        /// the reply is spoken). Read from the system messages; a past reply quoting the key in the
        /// history does not count.</summary>
        public static bool SheetAsksForVoiceLine(IReadOnlyList<ChatMessage>? messages) =>
            messages != null && messages.Any(m => m.Role == ChatRole.System
                && m.Content.IndexOf("[voice:", StringComparison.OrdinalIgnoreCase) >= 0);

        public static IReadOnlyList<ToolDefinition> FieldsOf(IReadOnlyList<ToolDefinition>? tools) =>
            tools == null ? Array.Empty<ToolDefinition>() : tools.Where(t => t.IsAnswerField).ToList();

        public static IReadOnlyList<ToolDefinition> HandsOf(IReadOnlyList<ToolDefinition>? tools) =>
            tools == null ? Array.Empty<ToolDefinition>() : tools.Where(t => !t.IsAnswerField).ToList();

        public static bool HasFields(IReadOnlyList<ToolDefinition>? tools) =>
            tools != null && tools.Any(t => t.IsAnswerField);

        /// <summary>The speak hand for these fields. Its words parameter comes first, then one
        /// parameter per field carrying that field's whole description — on the native roads the
        /// tool list is where a hand is explained, so this is the one copy of the calibration.</summary>
        public static ToolDefinition Speak(IReadOnlyList<ToolDefinition> fields, bool withVoice = false)
        {
            var parameters = new List<ToolParameter>
            {
                new ToolParameter(WordsParameter,
                    "My whole reply, exactly as my way of answering asks — but never the heart's measure."),
            };
            if (withVoice)
                parameters.Add(new ToolParameter(VoiceParameter,
                    "The few words my [voice: …] line would hold — set here instead of at the end of my words."));
            foreach (var f in fields)
                parameters.Add(new ToolParameter(f.AnswerField!, f.Description, true, null, f.Parameters[0].JsonType));

            var names = string.Join(" and ", fields.Select(f => "\"" + f.AnswerField + "\""));
            return new ToolDefinition(SpeakTool,
                "How I answer, every time: my reply in \"words\", with " + names + " set down beside it. " +
                "When I must look something up first, I reach for that hand instead and speak after.",
                parameters);
        }

        /// <summary>What a native-tool road offers: the hands as they are, the answer fields folded
        /// into one speak hand (with its voice parameter when the sheet asks for the line). Unchanged
        /// when no field rides.</summary>
        public static IReadOnlyList<ToolDefinition> NativeOffer(IReadOnlyList<ToolDefinition> tools,
            IReadOnlyList<ChatMessage>? messages = null)
        {
            var fields = FieldsOf(tools);
            if (fields.Count == 0) return tools;
            var offer = HandsOf(tools).ToList();
            offer.Add(Speak(fields, SheetAsksForVoiceLine(messages)));
            return offer;
        }

        /// <summary>
        /// A native-tool answer back into the mod's shape: the speak call's words become the text
        /// (the plain content stands in when speak carried none — a server that ignored the forced
        /// choice and simply talked), each field it carried becomes a call to that field's hand, and
        /// every other call stays a reach. Broken JSON in the speak call is salvaged by pattern —
        /// a long reply in a JSON string is exactly where a small model drops an escape.
        /// </summary>
        public static ChatResult FromNative(string text, IReadOnlyList<ToolCall> calls, IReadOnlyList<ToolDefinition>? tools)
        {
            var fields = FieldsOf(tools);
            calls = calls ?? Array.Empty<ToolCall>();
            if (fields.Count == 0) return new ChatResult(text ?? string.Empty, calls);

            var words = string.Empty;
            var answers = new List<ToolCall>();
            var reaches = new List<ToolCall>();
            foreach (var call in calls)
            {
                if (!string.Equals(call.Name, SpeakTool, StringComparison.Ordinal))
                {
                    reaches.Add(call);
                    continue;
                }
                var args = ParseArgs(call.ArgumentsJson);
                var spoken = args != null ? Str(args[WordsParameter]) : Salvage(call.ArgumentsJson, WordsParameter);
                var voice = args != null ? Str(args[VoiceParameter]) : Salvage(call.ArgumentsJson, VoiceParameter);
                if (!string.IsNullOrWhiteSpace(spoken)) words = WithVoice(spoken!, voice);

                var fromThisCall = new List<ToolCall>();
                foreach (var f in fields)
                {
                    var value = args != null ? args[f.AnswerField!] : SalvageToken(call.ArgumentsJson, f.AnswerField!);
                    if (value == null || value.Type == JTokenType.Null) continue;
                    fromThisCall.Add(AnswerCall(call.Id, f, value));
                }
                // The last speak that said something wins whole — its words and its measure together.
                if (!string.IsNullOrWhiteSpace(spoken) || answers.Count == 0)
                {
                    answers.Clear();
                    answers.AddRange(fromThisCall);
                }
            }

            // A signing backend (Gemini) signs only the FIRST call of a parallel set; when that was the
            // speak call, which is never replayed, the signature moves to the first reach that is.
            var signature = calls.FirstOrDefault(c => c.Name == SpeakTool && c.ProviderSignature != null)?.ProviderSignature;
            if (signature != null && reaches.Count > 0 && reaches.All(r => r.ProviderSignature == null))
                reaches[0] = new ToolCall(reaches[0].Id, reaches[0].Name, reaches[0].ArgumentsJson, signature);

            if (string.IsNullOrWhiteSpace(words)) words = text ?? string.Empty;
            return new ChatResult(StripLeakedMeasure(words, fields).Trim(), reaches, answers);
        }

        /// <summary>The direction back where every reader expects it — the last line — unless the
        /// words already carry their own key, which then stands.</summary>
        private static string WithVoice(string words, string? voice)
        {
            if (string.IsNullOrWhiteSpace(voice)) return words;
            Voices.VoiceLine.Split(words, out var own);
            if (own.Length > 0) return words;
            // A model may echo the brackets into the parameter; the key is the words inside them.
            var direction = voice!.Trim();
            Voices.VoiceLine.Split(direction, out var bracketed);
            if (bracketed.Length > 0) direction = bracketed;
            return words.TrimEnd() + "\n[voice: " + direction.Trim().Trim('[', ']').Trim() + "]";
        }

        /// <summary>The fields an envelope (the flattened roads' JSON answer) carried, as calls.</summary>
        public static IReadOnlyList<ToolCall> FromEnvelope(JObject envelope, IReadOnlyList<ToolDefinition> fields)
        {
            var answers = new List<ToolCall>();
            foreach (var f in fields)
            {
                var value = envelope[f.AnswerField!];
                if (value == null || value.Type == JTokenType.Null) continue;
                answers.Add(AnswerCall("answer", f, value));
            }
            return answers;
        }

        private static ToolCall AnswerCall(string baseId, ToolDefinition field, JToken value) =>
            new ToolCall((string.IsNullOrEmpty(baseId) ? "answer" : baseId) + "_" + field.AnswerField,
                field.Name,
                new JObject { [field.Parameters[0].Name] = value.DeepClone() }.ToString(Formatting.None));

        /// <summary>
        /// Removes a measure that leaked into the words anyway — a line that is nothing but
        /// "heart: +2", "[heart 0]", "(shift: -1)", "{"heart": 3}", "move_heart(2)", or such a
        /// bracketed tag closing the last sentence. Only whole lines and closing tags: a heart
        /// spoken of in a sentence ("my heart is yours") is speech and is never touched.
        /// </summary>
        public static string StripLeakedMeasure(string words, IReadOnlyList<ToolDefinition> fields)
        {
            if (string.IsNullOrEmpty(words) || fields == null || fields.Count == 0) return words ?? string.Empty;

            var names = new List<string>();
            foreach (var f in fields)
            {
                names.Add(f.AnswerField!);
                names.Add(f.Name);
                names.Add(f.Parameters[0].Name);
            }
            var alt = string.Join("|", names.Distinct(StringComparer.OrdinalIgnoreCase).Select(Regex.Escape));
            var tag = @"[\[\(\{<]?\s*[""'*_]*(?:" + alt + @")[""'*_]*\s*(?:[:=]\s*|\(\s*)?[""'*_]*\s*[+\-−]?\s*\d{1,3}\s*\)?\s*[\]\)\}>]?";

            // Whole lines that are only a measure.
            var cleaned = Regex.Replace(words, @"(?im)^[ \t]*" + tag + @"[ \t.]*(?:\r?\n|$)", string.Empty);
            // A bracketed tag closing a line ("…my love. [heart: +2]").
            cleaned = Regex.Replace(cleaned, @"(?im)[ \t]*[\[\(\{<]\s*[""'*_]*(?:" + alt + @")[""'*_]*\s*[:=]?\s*[+\-−]?\s*\d{1,3}\s*[\]\)\}>][ \t]*$", string.Empty);
            return cleaned.TrimEnd();
        }

        private static JObject? ParseArgs(string json)
        {
            try { return JObject.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json); }
            catch (JsonException) { return null; }
        }

        private static string? Str(JToken? token) =>
            token == null || token.Type == JTokenType.Null ? null
            : token.Type == JTokenType.String ? (string?)token : token.ToString(Formatting.None);

        /// <summary>A string field out of JSON that would not parse — the reply's words, mostly.</summary>
        private static string? Salvage(string json, string field)
        {
            var m = Regex.Match(json ?? string.Empty, "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)");
            if (!m.Success) return null;
            try { return JToken.Parse("\"" + m.Groups[1].Value + "\"").ToString(); }
            catch (JsonException) { return m.Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\""); }
        }

        /// <summary>A number field out of JSON that would not parse.</summary>
        private static JToken? SalvageToken(string json, string field)
        {
            var m = Regex.Match(json ?? string.Empty, "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"?([+\\-]?\\d{1,3})");
            return m.Success ? new JValue(m.Groups[1].Value) : null;
        }
    }
}
