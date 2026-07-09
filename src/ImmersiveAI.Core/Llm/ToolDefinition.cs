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

        public ToolParameter(string name, string description, bool required = true)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            Required = required;
        }
    }
}
