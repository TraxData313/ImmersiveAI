using System;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// One tool invocation the model asked for: the backend-issued id (echoed back with the
    /// result so the model can match them up), the tool name, and the arguments as the raw
    /// JSON object string the backend produced. Core never parses the arguments itself —
    /// the resolver owning the tool does, with whatever JSON library its side has.
    /// </summary>
    public sealed class ToolCall
    {
        public string Id { get; }
        public string Name { get; }
        public string ArgumentsJson { get; }

        public ToolCall(string id, string name, string argumentsJson)
        {
            Id = id ?? string.Empty;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ArgumentsJson = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson;
        }
    }
}
