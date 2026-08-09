using System;
using System.Collections.Generic;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// One ability an NPC may quietly call upon mid-thought — in play, "reaching into the world's
    /// memory" for what is truly known of a person or place, rather than guessing. Parameters are
    /// deliberately all strings (names, mostly): it keeps the JSON schema the backends need trivial
    /// to build, and every recall we offer is a lookup by name.
    /// </summary>
    public sealed class ToolDefinition
    {
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<ToolParameter> Parameters { get; }

        public ToolDefinition(string name, string description, IReadOnlyList<ToolParameter>? parameters = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            Parameters = parameters ?? Array.Empty<ToolParameter>();
        }
    }

    /// <summary>A single string parameter of a <see cref="ToolDefinition"/>.</summary>
    public sealed class ToolParameter
    {
        public string Name { get; }
        public string Description { get; }
        public bool Required { get; }
        /// <summary>The closed set of words this parameter accepts, or null when it takes free
        /// text. Emitted as the schema's own "enum" by every client — WITHOUT it a model reads the
        /// allowed words out of the description's prose and then answers with a synonym of its own
        /// ("resolve" for "settle"), which lands in the resolver's default branch and silently does
        /// nothing (live-caught on gpt-5.6-terra, 2026.08.09: Sibylla laid all three of her
        /// marriage misgivings to rest three times over, and not one of them moved).</summary>
        public IReadOnlyList<string>? AllowedValues { get; }

        public ToolParameter(string name, string description, bool required = true,
            IReadOnlyList<string>? allowedValues = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            Required = required;
            AllowedValues = allowedValues != null && allowedValues.Count > 0 ? allowedValues : null;
        }
    }
}
