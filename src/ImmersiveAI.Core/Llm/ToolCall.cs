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

        /// <summary>An opaque token the backend attached to this call and demands back, unchanged,
        /// when the call is replayed in the next request. Core neither reads nor understands it —
        /// the client that received it is the only thing that knows what it means; everything here
        /// just carries it along so the reach and its replay stay the same call in the model's eyes.
        /// Null on backends that issue none (most of them). Gemini's thinking models sign every
        /// function call this way and answer 400 if the signature does not ride back.</summary>
        public string? ProviderSignature { get; }

        public ToolCall(string id, string name, string argumentsJson, string? providerSignature = null)
        {
            Id = id ?? string.Empty;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ArgumentsJson = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson;
            ProviderSignature = string.IsNullOrWhiteSpace(providerSignature) ? null : providerSignature;
        }
    }
}
