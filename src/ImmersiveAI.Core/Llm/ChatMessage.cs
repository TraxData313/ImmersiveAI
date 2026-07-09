using System;
using System.Collections.Generic;

namespace ImmersiveAI.Core.Llm
{
    public enum ChatRole
    {
        System,
        User,
        Assistant,

        /// <summary>A tool result being handed back to the model — the world's memory answering
        /// an NPC's recall. Carries <see cref="ChatMessage.ToolCallId"/> so the backend can match
        /// it to the call that asked.</summary>
        Tool
    }

    /// <summary>A single message in a multi-turn LLM conversation.</summary>
    public sealed class ChatMessage
    {
        public ChatRole Role { get; }
        public string Content { get; }

        /// <summary>The tool calls this assistant message asked for (empty on every other role,
        /// and on ordinary spoken assistant messages).</summary>
        public IReadOnlyList<ToolCall> ToolCalls { get; }

        /// <summary>On a <see cref="ChatRole.Tool"/> message, the id of the call this result answers.</summary>
        public string? ToolCallId { get; }

        public ChatMessage(ChatRole role, string content)
            : this(role, content, null, null)
        {
        }

        private ChatMessage(ChatRole role, string content, IReadOnlyList<ToolCall>? toolCalls, string? toolCallId)
        {
            Role = role;
            Content = content ?? throw new ArgumentNullException(nameof(content));
            ToolCalls = toolCalls ?? Array.Empty<ToolCall>();
            ToolCallId = toolCallId;
        }

        public static ChatMessage System(string content) => new ChatMessage(ChatRole.System, content);
        public static ChatMessage User(string content) => new ChatMessage(ChatRole.User, content);
        public static ChatMessage Assistant(string content) => new ChatMessage(ChatRole.Assistant, content);

        /// <summary>An assistant turn that reached for one or more tools (its text, if any, is the
        /// thinking-aloud that came with the reach).</summary>
        public static ChatMessage AssistantToolCalls(string content, IReadOnlyList<ToolCall> toolCalls) =>
            new ChatMessage(ChatRole.Assistant, content ?? string.Empty, toolCalls, null);

        /// <summary>The answer to one tool call, matched back by its id.</summary>
        public static ChatMessage ToolResult(string toolCallId, string content) =>
            new ChatMessage(ChatRole.Tool, content ?? string.Empty, null, toolCallId ?? string.Empty);
    }
}
