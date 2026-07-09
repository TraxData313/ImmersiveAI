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

        public bool WantsTools => ToolCalls.Count > 0;

        public ChatResult(string text, IReadOnlyList<ToolCall>? toolCalls = null)
        {
            Text = text ?? string.Empty;
            ToolCalls = toolCalls ?? Array.Empty<ToolCall>();
        }
    }
}
