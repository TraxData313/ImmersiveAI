namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// Whether a backend lets the hands on offer CHANGE between the rounds of one turn. The API
    /// backends replay real tool_use blocks and want every definition to keep riding (see
    /// <see cref="IToolChatClient"/>), so their offer never narrows. The subscription roads (Claude
    /// Code, Codex) flatten the whole history into one script and enforce the hands through a
    /// schema rebuilt every call — there a silent hand already used this turn (the heart, weighed
    /// once) is simply taken off the table, so she cannot spend another round reaching for it.
    /// A client that does not implement this is treated as "never narrows".
    /// </summary>
    public interface IToolOfferPolicy
    {
        bool OfferMayNarrowMidTurn { get; }
    }
}
