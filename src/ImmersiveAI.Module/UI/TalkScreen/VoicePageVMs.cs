using System;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.TalkScreen
{
    // The small pieces of the Voices page. Every glyph here is from a block the shipped fonts carry —
    // Dingbats (✓ ✗ ✦), General Punctuation (• –), Miscellaneous Symbols (♪) — never Geometric Shapes,
    // which draw as "?" (see VoiceRowVM).

    /// <summary>The page's colours, in one place so the chip, the steps and the verdicts agree.</summary>
    internal static class VoicePalette
    {
        public static readonly Color Good = new Color(0.55f, 0.86f, 0.50f, 1f);
        public static readonly Color Busy = new Color(0.95f, 0.76f, 0.36f, 1f);
        public static readonly Color Bad = new Color(0.93f, 0.42f, 0.36f, 1f);
        public static readonly Color Muted = new Color(0.56f, 0.54f, 0.50f, 1f);
        public static readonly Color Text = new Color(0.85f, 0.80f, 0.70f, 1f);
        public static readonly Color Gold = new Color(0.85f, 0.80f, 0.66f, 1f);
    }

    /// <summary>
    /// One engine, as a card: what it is, what it speaks, what it needs, how big it is, how much of
    /// the graphics card it holds — and whether THIS computer runs it. Chosen by clicking the card
    /// before an install; after one, the card's own button switches to it or adds it.
    /// </summary>
    public class VoiceEngineCardVM : ViewModel
    {
        private readonly Action<VoiceEngineCardVM> _select, _hear, _act;
        private bool _isSelected;

        public VoiceEngineCardVM(string id, Action<VoiceEngineCardVM> select, Action<VoiceEngineCardVM> hear, Action<VoiceEngineCardVM> act)
        {
            Id = id;
            _select = select;
            _hear = hear;
            _act = act;
        }

        public string Id { get; }

        [DataSourceProperty] public string NameText { get; set; } = string.Empty;
        [DataSourceProperty] public string RoleText { get; set; } = string.Empty;
        [DataSourceProperty] public string TaglineText { get; set; } = string.Empty;
        [DataSourceProperty] public string SpeaksText { get; set; } = string.Empty;
        [DataSourceProperty] public string NeedsText { get; set; } = string.Empty;
        [DataSourceProperty] public string SizeText { get; set; } = string.Empty;
        [DataSourceProperty] public string GpuText { get; set; } = string.Empty;
        [DataSourceProperty] public string VoicesText { get; set; } = string.Empty;
        [DataSourceProperty] public string VerdictText { get; set; } = string.Empty;
        [DataSourceProperty] public Color VerdictColor { get; set; } = VoicePalette.Muted;
        [DataSourceProperty] public bool CanPick { get; set; } = true;
        [DataSourceProperty] public bool ShowAction { get; set; }
        [DataSourceProperty] public string ActionText { get; set; } = string.Empty;
        [DataSourceProperty] public bool CanAct { get; set; }
        [DataSourceProperty] public bool CanHear { get; set; }
        [DataSourceProperty] public string HearText => "♪  Hear it";

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value, "IsSelected"); } }
        }

        public void ExecuteSelect() { if (CanPick) _select(this); }
        public void ExecuteHear() => _hear(this);
        public void ExecuteAction() { if (CanAct) _act(this); }
    }

    /// <summary>A drive the voice files could live on: "✦ D:   700 GB free".</summary>
    public class VoiceDriveVM : ViewModel
    {
        private readonly Action<VoiceDriveVM> _select;

        public VoiceDriveVM(Voice.VoiceMachine.Drive drive, bool selected, bool roomy, Action<VoiceDriveVM> select)
        {
            Drive = drive;
            _select = select;
            CanSelect = roomy;
            Text = (selected ? "✦  " : string.Empty) + drive.Label + "   " + Voice.VoiceMachine.Gb(drive.FreeGb) + " free";
        }

        public Voice.VoiceMachine.Drive Drive { get; }
        [DataSourceProperty] public string Text { get; }
        [DataSourceProperty] public bool CanSelect { get; }
        public void ExecuteSelect() { if (CanSelect) _select(this); }
    }

    /// <summary>One of the install's four steps: done ✓, happening now •, or still to come –.</summary>
    public class VoiceStepVM : ViewModel
    {
        public VoiceStepVM(string label, string detail, int state)
        {
            LabelText = label;
            DetailText = detail;
            MarkText = state > 0 ? "✓" : state == 0 ? "•" : "–";
            MarkColor = state > 0 ? VoicePalette.Good : state == 0 ? VoicePalette.Busy : VoicePalette.Muted;
            LabelColor = state < 0 ? VoicePalette.Muted : state == 0 ? VoicePalette.Gold : VoicePalette.Text;
        }

        [DataSourceProperty] public string MarkText { get; }
        [DataSourceProperty] public Color MarkColor { get; }
        [DataSourceProperty] public string LabelText { get; }
        [DataSourceProperty] public Color LabelColor { get; }
        [DataSourceProperty] public string DetailText { get; }
    }

    /// <summary>Where one part lives on disk, how big it is, and a button to open the folder.</summary>
    public class VoiceStorageRowVM : ViewModel
    {
        private readonly string _path;

        public VoiceStorageRowVM(string name, string size, string shown, string path)
        {
            NameText = name;
            SizeText = size;
            PathText = shown;
            _path = path;
        }

        [DataSourceProperty] public string NameText { get; }
        [DataSourceProperty] public string SizeText { get; }
        [DataSourceProperty] public string PathText { get; }
        [DataSourceProperty] public bool CanOpen => !string.IsNullOrEmpty(_path);
        [DataSourceProperty] public string OpenText => "Open";

        public void ExecuteOpen() => Voice.ClaudeVoiceApp.OpenPath(_path);
    }
}
