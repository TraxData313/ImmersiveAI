using TaleWorlds.Library;

namespace ImmersiveAI.UI.ChatWindow
{
    /// <summary>
    /// One rendered line of the chat window: a spoken message (player or NPC, with a small
    /// speaker-and-when header) or a narration line (the Angel's beats, and transient notes like
    /// "considers your words…" — shown softly, without a header, because they are the story's
    /// stage directions rather than anyone's spoken words). Nothing the NPC remembers is hidden:
    /// the window shows the same recorded stream her prompt replays.
    /// </summary>
    public class ChatMessageVM : ViewModel
    {
        private string _header = string.Empty;
        private string _body = string.Empty;
        private bool _isNarration;
        private Color _headerColor = Colors.White;

        public ChatMessageVM(string header, string body, bool isNarration, Color headerColor)
        {
            _header = header ?? string.Empty;
            _body = body ?? string.Empty;
            _isNarration = isNarration;
            _headerColor = headerColor;
        }

        [DataSourceProperty]
        public string Header
        {
            get => _header;
            set { if (value != _header) { _header = value; OnPropertyChangedWithValue(value, "Header"); OnPropertyChanged("HasHeader"); } }
        }

        [DataSourceProperty]
        public bool HasHeader => !string.IsNullOrWhiteSpace(_header);

        [DataSourceProperty]
        public string Body
        {
            get => _body;
            set { if (value != _body) { _body = value; OnPropertyChangedWithValue(value, "Body"); } }
        }

        [DataSourceProperty]
        public bool IsNarration
        {
            get => _isNarration;
            set { if (value != _isNarration) { _isNarration = value; OnPropertyChangedWithValue(value, "IsNarration"); } }
        }

        [DataSourceProperty]
        public Color HeaderColor
        {
            get => _headerColor;
            set { if (value != _headerColor) { _headerColor = value; OnPropertyChangedWithValue(value, "HeaderColor"); } }
        }
    }
}
