using System;
using ImmersiveAI.Core.Voices;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.TalkScreen
{
    /// <summary>
    /// One voice on the shelf, as the panel shows it: what it is called, where it came from, and who
    /// is speaking with it at the moment.
    /// <para>
    /// A pure view over <see cref="VoicePreset"/>. The panel rebuilds these whenever anything is
    /// cast, so — exactly like the thread's own rows — they hold no state worth keeping.
    /// </para>
    /// </summary>
    public class VoiceRowVM : ViewModel
    {
        // The marks under a name, in the same slate as every other piece of bookkeeping.
        private static readonly Color MarkColor = new Color(0.62f, 0.66f, 0.72f, 1f);
        private static readonly Color WantsSomethingColor = new Color(0.86f, 0.70f, 0.45f, 1f);

        private readonly Action<VoiceRowVM>? _select;
        private readonly Action<VoiceRowVM>? _hear;

        private bool _isSelected;

        public VoiceRowVM(VoicePreset voice, string marks, bool ready, string wants,
                          Action<VoiceRowVM>? select, Action<VoiceRowVM>? hear)
        {
            Voice = voice;
            Name = voice?.Name ?? string.Empty;
            Detail = DetailFor(voice);
            Marks = marks ?? string.Empty;
            Wants = wants ?? string.Empty;
            IsReady = ready;
            _select = select;
            _hear = hear;
        }

        public VoicePreset Voice { get; }

        public string Id => Voice?.Id ?? string.Empty;

        [DataSourceProperty]
        public string Name { get; }

        /// <summary>Where it came from and what it sounds like — the useful thing to read when
        /// deciding between five voices all named some variation of one person.</summary>
        [DataSourceProperty]
        public string Detail { get; }

        /// <summary>Who speaks with it: "· Sibylla", "· all women", "· you". Empty when nobody does.</summary>
        [DataSourceProperty]
        public string Marks { get; }

        [DataSourceProperty]
        public bool HasMarks => !string.IsNullOrEmpty(Marks);

        [DataSourceProperty]
        public Color MarksColor => MarkColor;

        /// <summary>What this voice still wants before it can speak — a key, mostly. Empty when it
        /// is ready. Said out loud on the row, because a voice that silently does nothing is the
        /// worst thing a list like this can contain.</summary>
        [DataSourceProperty]
        public string Wants { get; }

        [DataSourceProperty]
        public bool HasWants => !string.IsNullOrEmpty(Wants);

        [DataSourceProperty]
        public Color WantsColor => WantsSomethingColor;

        [DataSourceProperty]
        public bool IsReady { get; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value, "IsSelected"); } }
        }

        /// <summary>
        /// THE PLAY MARK — a musical note, and the block it comes from is the whole point.
        /// <para>
        /// The first cut used ▶ (U+25B6) and it drew as a question mark everywhere (Anton, first
        /// look, 2026.08.15 — "I've never seen [?] on a symbol before", which is exactly right: this
        /// mod already draws ❦ ❧ ☾ ✉ ✦ happily). Whatever the engine falls back to for glyphs the
        /// shipped .fnt files lack, it covers Miscellaneous Symbols (U+2600) and Dingbats (U+2700)
        /// and does NOT cover Geometric Shapes (U+25B6 ▶, and by implication ● ■ ▲ ▼).
        /// <b>So stay in the blocks this mod already proves work</b> — ☾ is U+263E, ♪ is U+266A,
        /// two doors down.
        /// </para>
        /// </summary>
        [DataSourceProperty]
        public string HearText => "♪";

        public void ExecuteSelect() => _select?.Invoke(this);
        public void ExecuteHear() => _hear?.Invoke(this);

        private static string DetailFor(VoicePreset? voice)
        {
            if (voice == null) return string.Empty;

            var kind = voice.Backend == VoiceBackend.Remote ? "hosted" : "made on this machine";
            var sex = voice.Gender == VoiceGender.Female ? "woman's"
                    : voice.Gender == VoiceGender.Male ? "man's"
                    : string.Empty;

            var said = (voice.ReferenceText ?? string.Empty).Trim();
            if (said.Length > 80) said = said.Substring(0, 80).TrimEnd() + "…";

            var parts = new System.Collections.Generic.List<string>(3);
            if (sex.Length > 0) parts.Add(sex);
            parts.Add(kind);
            if (said.Length > 0) parts.Add(said);
            return string.Join(" · ", parts);
        }
    }
}
