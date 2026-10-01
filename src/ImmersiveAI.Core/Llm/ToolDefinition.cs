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

        /// <summary>
        /// A SILENT hand only sets something down beside the words — its answer never changes what
        /// is said (the heart's measure: "it is felt… now I give my answer"). Words that arrive WITH silent
        /// hands alone are therefore the finished reply: <see cref="ToolLoopRunner"/> resolves the
        /// hands and ends the turn, with no further round. A hand whose answer may change the words
        /// — every recall, and every hand that can be REFUSED (a laid bargain, a courtship step, a
        /// misgiving or a door reason that did not take, each telling her how to reach again) — is
        /// not silent: words beside it are only a draft, and the next round writes the reply whole.
        /// (2026.10.01, the coda bug: a round of words + move_heart was fed back and a second round
        /// wrote an epilogue after them — and only the epilogue was ever recorded.)
        /// </summary>
        public bool Silent { get; }

        /// <summary>
        /// When set, this hand is not a reach at all on a road that can shape its answer: its one
        /// parameter becomes a REQUIRED field of the answer itself, named this, set down beside the
        /// words in the same breath (2026.10.01 — the heart: "heart" rides every spoken answer, even
        /// when it is 0). It is never a mark inside the words — the two in-prose marks that failed
        /// (a ♥ tail, a &lt;relation&gt; tag) were narrated instead of emitted; a schema field cannot
        /// be narrated, only filled or left empty. Every client folds it into its own answer shape
        /// (see <see cref="AnswerShape"/>) and hands the value back as a call to this hand in
        /// <see cref="ChatResult.AnswerCalls"/>, so the resolver that applies it stays the same one.
        /// A client that knows nothing of answer fields simply offers it as an ordinary silent hand.
        /// Implies <see cref="Silent"/>; the hand must carry exactly one parameter.
        /// </summary>
        public string? AnswerField { get; }

        public bool IsAnswerField => AnswerField != null;

        public ToolDefinition(string name, string description, IReadOnlyList<ToolParameter>? parameters = null,
            bool silent = false, string? answerField = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            Parameters = parameters ?? Array.Empty<ToolParameter>();
            if (!string.IsNullOrWhiteSpace(answerField))
            {
                if (Parameters.Count != 1)
                    throw new ArgumentException("An answer-field hand carries exactly one parameter.", nameof(parameters));
                AnswerField = answerField;
                silent = true;
            }
            Silent = silent;
        }
    }

    /// <summary>A single parameter of a <see cref="ToolDefinition"/> — a string unless said otherwise.</summary>
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

        /// <summary>The JSON Schema type every client emits: "string" (the default — names, mostly)
        /// or "integer" (the heart's measure, so a schema can hold the model to a number).</summary>
        public string JsonType { get; }

        public ToolParameter(string name, string description, bool required = true,
            IReadOnlyList<string>? allowedValues = null, string jsonType = "string")
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            Required = required;
            AllowedValues = allowedValues != null && allowedValues.Count > 0 ? allowedValues : null;
            JsonType = string.IsNullOrWhiteSpace(jsonType) ? "string" : jsonType;
        }
    }
}
