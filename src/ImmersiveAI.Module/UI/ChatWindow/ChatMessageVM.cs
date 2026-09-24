using System;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.ChatWindow
{
    /// <summary>
    /// One rendered line of the chat window: a spoken message (player or NPC, with a small
    /// speaker-and-when header) or a narration line (inner-mind beats, and transient notes like
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

        // Whose voice, and what words — set only on rows that somebody actually SAID or WROTE.
        private Action<bool, string>? _speak;
        private bool _spokenByPlayer;
        private string _spokenText = string.Empty;

        public ChatMessageVM(string header, string body, bool isNarration, Color headerColor)
        {
            _header = header ?? string.Empty;
            _body = HighlightVoiceCues(body ?? string.Empty);
            _isNarration = isNarration;
            _headerColor = headerColor;
        }

        // The moods and sounds a soul writes for her voice — (tender), (laugh) — stay in her words, as
        // Anton asked, drawn in the orange the Abby app gives them so they read as delivery, not speech.
        // By name (or a near word for one), plus any one-word bracket OPENING a row — her stage direction,
        // which the voice never reads either; any other bracket is her own and stays plain. The style lives in the brush
        // ImmersiveAI.Thread.Text, which every thread body wears.
        private static readonly System.Text.RegularExpressions.Regex VoiceCue =
            new System.Text.RegularExpressions.Regex(
                @"^\(\s*[A-Za-z]+\s*\)|\((?:" + string.Join("|", Core.Voices.SpeakableText.Moods)
                         + "|" + string.Join("|", Core.Voices.SpeakableText.MoodAliases.Keys)
                         + @"|laugh|sigh|cough|clears throat)\)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private static string HighlightVoiceCues(string body)
            => body.IndexOf('(') < 0 ? body : VoiceCue.Replace(body, m => "<span style=\"VoiceCue\">" + m.Value + "</span>");

        /// <summary>
        /// Gives this row a ▶: these words, in this voice, on demand.
        /// <para>
        /// THE ROW HOLDS NO AUDIO STATE, and that is a rule rather than an omission. The thread is
        /// rebuilt into a fresh list on every change — a reply landing, a letter arriving, a day
        /// turning — so anything a row remembered would be thrown away moments later. What it holds
        /// is the WORDS, handed to claude-voice afresh each time the ▶ is pressed.
        /// </para>
        /// <para>
        /// <paramref name="spokenText"/> is the words THEMSELVES, not the row's decorated body: a
        /// letter speaks its letter and not "✉ so-and-so takes up the quill", and an inner beat
        /// speaks the thought and not the brackets around it.
        /// </para>
        /// </summary>
        public ChatMessageVM WithVoice(bool byPlayer, string? spokenText, Action<bool, string>? speak)
        {
            _spokenByPlayer = byPlayer;
            _spokenText = (spokenText ?? string.Empty).Trim();
            _speak = speak;
            return this;
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

        /// <summary>Whether this row shows a ▶ at all. False for everything nobody said — the
        /// scrollback of what they are about to be given, a courier's progress, an empty thread.</summary>
        [DataSourceProperty]
        public bool CanSpeak => _speak != null && _spokenText.Length > 0;

        [DataSourceProperty]
        public string SpeakHint => "Hear this";

        public void ExecuteSpeak()
        {
            try { _speak?.Invoke(_spokenByPlayer, _spokenText); }
            catch { /* a line that will not be spoken is a line that is read */ }
        }
    }
}
