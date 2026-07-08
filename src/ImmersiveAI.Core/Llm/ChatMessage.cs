using System;

namespace ImmersiveAI.Core.Llm
{
    public enum ChatRole
    {
        System,
        User,
        Assistant
    }

    /// <summary>A single message in a multi-turn LLM conversation.</summary>
    public sealed class ChatMessage
    {
        public ChatRole Role { get; }
        public string Content { get; }

        public ChatMessage(ChatRole role, string content)
        {
            Role = role;
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public static ChatMessage System(string content) => new ChatMessage(ChatRole.System, content);
        public static ChatMessage User(string content) => new ChatMessage(ChatRole.User, content);
        public static ChatMessage Assistant(string content) => new ChatMessage(ChatRole.Assistant, content);
    }
}
