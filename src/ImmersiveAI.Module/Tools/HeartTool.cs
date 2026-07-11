using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Prompts;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The heart's own hand: a tool the NPC may call mid-reply to let their regard for the one
    /// they speak with truly shift, in the same breath as the words that moved it — replacing the
    /// second, isolated "feeling" call on tool-capable backends (and letting greetings, letters,
    /// and reachings-out move the heart too, which the after-the-reply question never covered).
    ///
    /// This is NOT the in-message-mark pattern that failed twice on gpt-4o (a ♥ tail-mark, then a
    /// &lt;relation&gt; tag — both narrated in prose, never emitted): native tool calling is a
    /// first-class API channel on both backends, the same one the recalls ride reliably. If a
    /// backend proves shy of reaching for it, RelationshipChangesViaTool=false restores the
    /// separate feeling call without a redeploy.
    /// </summary>
    public static class HeartTool
    {
        public const string MoveHeart = "move_heart";

        /// <summary>Accumulates the shifts one spoken turn chose, so the caller can record the
        /// felt total on the conversation turn. Tool calls resolve one at a time inside the loop,
        /// so a plain int is safe.</summary>
        public sealed class Tally
        {
            public int Total;
        }

        public static readonly ToolDefinition Tool = new ToolDefinition(MoveHeart,
            "Weigh your heart each time before you answer, and set down here — every reply, without " +
            "exception — the honest measure of how what has just passed moved your regard for the one " +
            "you speak with. 0 is a full answer: the mark of a heart that truly held. A kind word, a " +
            "shared laugh, an open confidence, a small grace sways it a little (1 to 3) — and it " +
            "should; a slight or a coldness likewise away (-1 to -3); only what shakes the soul moves " +
            "it greatly. A heart already given wholly can still be warmed — the warmth counts even at " +
            "the summit. Never speak the measure aloud; let the feeling show only in your words and " +
            "bearing.",
            new[]
            {
                new ToolParameter("shift",
                    "The honest measure, a whole number: 0 when the heart held; positive toward them " +
                    "(+1 a small warmth, +3 a true kindness, more only for what shakes you), negative " +
                    "away from them (-1 to -100)."),
            });

        /// <summary>What the tool answers when the shift was felt — steering her back to words.</summary>
        public const string Felt =
            "It is felt, and it is yours — your heart has moved. Let it show only in your words and " +
            "bearing; speak no number aloud, and speak on.";

        /// <summary>What the tool answers when no readable number came — an honest stillness.</summary>
        public const string Held =
            "You look within, and your heart holds where it stood. Speak on.";

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
