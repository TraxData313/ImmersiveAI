using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Prompts;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The heart's measure — set down beside EVERY spoken answer, 0 included (2026.10.01, Anton:
    /// "output it with their answer always … not another go that takes time"). It is an ANSWER
    /// FIELD (<see cref="ToolDefinition.AnswerField"/> "heart"): on the flattened roads (Codex,
    /// Claude Code) a required property of the JSON answer beside "reply"; on the native-tool roads
    /// (OpenAI-compatible, Anthropic) a required parameter of the one "speak" hand the reply is
    /// given through. Either way it arrives with the words in the same call and is applied by
    /// <c>ResolveHeartShift</c> exactly as the old reach was.
    ///
    /// The history it ends: an in-prose mark failed twice on gpt-4o (a ♥ tail, then a
    /// &lt;relation&gt; tag — the number was narrated, never emitted); then an OPTIONAL tool that
    /// models went shy of, then the always-weigh ritual whose extra tool round cost seconds on every
    /// reply. A required schema field is neither prose nor optional. If a backend cannot shape its
    /// answer this way, the field is missing, the turn counts as unweighed, and the player turn
    /// falls back to the separate feeling call (RelationshipChangesViaTool=false forces that road).
    /// </summary>
    public static class HeartTool
    {
        public const string MoveHeart = "move_heart";

        /// <summary>Accumulates the shifts one spoken turn chose, so the caller can record the
        /// felt total on the conversation turn — and whether the heart was truly WEIGHED at all:
        /// a model that never reached for the tool leaves Weighed false, and the caller falls back
        /// to the separate feeling question (gpt-4o goes shy of volunteering the call — observed
        /// again 2026.07.11 with eleven tools riding). Tool calls resolve one at a time inside the
        /// loop, so plain fields are safe.</summary>
        public sealed class Tally
        {
            public int Total;
            public bool Weighed;
        }

        /// <summary>The answer field's name — what the model fills beside "reply" / "words".</summary>
        public const string Field = "heart";

        // The calibration is said ONCE per road (token diet): the description below rides the sheet
        // on the flattened roads and the speak hand's "heart" parameter on the native ones; the
        // parameter's own short line is only the schema's reminder of the range.
        public static readonly ToolDefinition Tool = new ToolDefinition(MoveHeart,
            "how what just passed moved my regard for the one I speak with — a whole number, set every " +
            "reply. 0 is a full answer: my heart held. A kind word warms it a little (+1 to +3), a slight " +
            "cools it likewise (-1 to -3); only what shakes the soul moves it far (up to ±100). It " +
            "measures the moment, not the room left on a scale — a heart given wholly can still be " +
            "warmed. When I speak first and nothing new has passed between us, it is 0. It agrees with my " +
            "words, and it is never spoken aloud.",
            new[]
            {
                new ToolParameter("shift",
                    "A whole number from -100 to 100: 0 when my heart held, positive toward them, negative away.",
                    jsonType: "integer"),
            },
            silent: true,
            answerField: Field);

        /// <summary>What the tool answers when the shift was felt — steering her back to words.
        /// It is only ever read when the measure came WITHOUT words (words beside it end the turn),
        /// so it asks for the reply itself, never for "more" after one (the coda, 2026.10.01).</summary>
        public const string Felt =
            "It is felt, and it is mine — my heart has moved. I let it show only in my words and " +
            "bearing; I speak no number aloud. Now I give my answer.";

        /// <summary>What the tool answers when no readable number came — an honest stillness.</summary>
        public const string Held =
            "I look within, and my heart holds where it stood. Now I give my answer.";

        /// <summary>
        /// What a SECOND weighing in the same exchange is answered with (2026.08.28, Anton's
        /// playtest: "+3, +1, +1" in one message). "Every reply" means the one answer she gives
        /// the person in front of her — but a reply reaching for recalls is built over several
        /// rounds, and each round looks like a reply from the inside, so a dutiful soul weighed
        /// again every round and each shift was applied for real. The measure of an exchange is
        /// taken ONCE; the rounds after are the same breath, not new ones.
        /// </summary>
        public const string AlreadyWeighed =
            "I have already weighed my heart in this exchange, and what I set down stands — this is " +
            "the same breath, not a new one. I do not weigh it twice; now I give my answer.";

        /// <summary>The shift the NPC chose, clamped to -100..100, or null when none can be read.
        /// Lenient like the feeling call's parser: a bare number, "+2", or a number wrapped in a
        /// word or two all count.</summary>
        public static int? ParseShift(ToolCall call)
        {
            try
            {
                var args = JObject.Parse(call.ArgumentsJson);
                return FeelingParser.ParseShift(args["shift"]?.ToString());
            }
            catch { return null; }
        }
    }
}
