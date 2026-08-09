using System;
using ImmersiveAI.Core.Prompts;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.ChatWindow
{
    /// <summary>
    /// One standing preset in the "Let me think…" menu — "romantic", "ender", "starter". The same
    /// row serves both surfaces: in the little menu a click CHOOSES it (its words drop into the
    /// writing box as a wish), and on the editing page the same row also wears a pen and a cross.
    /// Shared by both windows, exactly as ChatMessageVM is.
    /// </summary>
    public class ConversationPresetVM : ViewModel
    {
        private readonly Action<ConversationPresetVM>? _onChoose;
        private readonly Action<ConversationPresetVM>? _onEdit;
        private readonly Action<ConversationPresetVM>? _onRemove;

        public ConversationPresetVM(
            ConversationPreset preset,
            Action<ConversationPresetVM>? onChoose,
            Action<ConversationPresetVM>? onEdit = null,
            Action<ConversationPresetVM>? onRemove = null)
        {
            Name = preset?.Name ?? string.Empty;
            Wish = preset?.Text ?? string.Empty;
            _onChoose = onChoose;
            _onEdit = onEdit;
            _onRemove = onRemove;
        }

        public void ExecuteChoose() => _onChoose?.Invoke(this);
        public void ExecuteEdit() => _onEdit?.Invoke(this);
        public void ExecuteRemove() => _onRemove?.Invoke(this);

        /// <summary>The player's own short word for it — the menu's label.</summary>
        [DataSourceProperty]
        public string Name { get; }

        /// <summary>What they mean to get across — the wish itself, which is what the thinking reads.
        /// Deliberately not called "Text": a widget's own Text is what these rows bind INTO.</summary>
        [DataSourceProperty]
        public string Wish { get; }
    }
}
