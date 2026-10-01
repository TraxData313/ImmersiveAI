using System;
using System.Collections.Generic;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// What a tool-aware completion returned: spoken text, tool calls, or both (a model may
    /// think aloud before reaching for a recall). No tool calls means the turn is finished.
    /// </summary>
    public sealed class ChatResult
    {
        public string Text { get; }
        public IReadOnlyList<ToolCall> ToolCalls { get; }

        /// <summary>The answer-field hands that came INSIDE the answer (the heart's measure set down
        /// beside the words — see <see cref="ToolDefinition.AnswerField"/>). They are part of the
        /// answer, not reaches: never fed back, never a reason for another round. The loop resolves
        /// them with the words they came beside.</summary>
        public IReadOnlyList<ToolCall> AnswerCalls { get; }

        public bool WantsTools => ToolCalls.Count > 0;

        public ChatResult(string text, IReadOnlyList<ToolCall>? toolCalls = null, IReadOnlyList<ToolCall>? answerCalls = null)
        {
            Text = text ?? string.Empty;
            ToolCalls = toolCalls ?? Array.Empty<ToolCall>();
            AnswerCalls = answerCalls ?? Array.Empty<ToolCall>();
        }
    }
}
