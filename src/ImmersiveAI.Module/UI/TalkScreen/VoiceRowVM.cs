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
            Name = NameFor(voice);
            Detail = DetailFor(voice);
            Marks = marks ?? string.Empty;
            Wants = wants ?? string.Empty;
            IsReady = ready;
            _select = select;
            _hear = hear;
        }

        /// <summary>
        /// A FOLDER rather than a voice — one line the player can fold shut.
        /// <para>
        /// Headers ride the same list as the voices rather than living in a nested control because
        /// Gauntlet gives one ItemTemplate per list: two kinds of row in one flat list, each drawn
        /// by the half of the template that its own flag makes visible. It is also what keeps
        /// scrolling, selection and the scrollbar working exactly as they already did.
        /// </para>
        /// </summary>
        public VoiceRowVM(string groupKey, string headerText, bool collapsed, Action<VoiceRowVM>? toggle)
        {
            Voice = null!;
            IsHeader = true;
            GroupKey = groupKey ?? string.Empty;
            IsCollapsed = collapsed;
            // ASCII, and it has to stay that way. The first cut used ▸ / ▾ and both drew as "?" —
            // they are Geometric Shapes (U+25B8, U+25BE), the one block the shipped fonts do not
            // cover. It is the SAME trap already written up on HearText below, and I walked into it
            // anyway; a plus and a minus cannot be got wrong by any font.
            HeaderText = (collapsed ? "+  " : "-  ") + (headerText ?? string.Empty);
            Name = string.Empty;
            Detail = string.Empty;
            Marks = string.Empty;
            Wants = string.Empty;
            IsReady = true;
            _select = toggle;
        }

        public VoicePreset Voice { get; }

        /// <summary>True for a folder line, false for a voice. The template draws one or the other.</summary>
        [DataSourceProperty]
        public bool IsHeader { get; }

        [DataSourceProperty]
        public bool IsVoiceRow => !IsHeader;

        /// <summary>Which folder this row is, or belongs to. Empty for a loose voice.</summary>
        public string GroupKey { get; } = string.Empty;

        public bool IsCollapsed { get; }

        /// <summary>The folder's own line, arrow and all.</summary>
        [DataSourceProperty]
        public string HeaderText { get; } = string.Empty;

        public string Id => Voice?.Id ?? string.Empty;

        /// <summary>The folder colour — dimmer than a voice's name, so the eye reads structure
        /// before it reads contents.</summary>
        [DataSourceProperty]
        public Color HeaderColor => new Color(0.78f, 0.71f, 0.55f, 1f);

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

        /// <summary>
        /// The name with WHO MAKES IT after it — "Sibylla (Qwen TTS Studio)", "Alloy (OpenAI)".
        /// <para>
        /// Said on every row rather than only on the hosted ones (Anton, 2026.08.15: the hosted
        /// voices wore a mark and the local ones wore nothing, which reads as though only one kind
        /// came from anywhere). The tag lives HERE and not in the preset's own Name so it stays a
        /// piece of rendering: nothing on disk, nothing in the casting sheet, and a voice shared
        /// with a friend carries only its real name.
        /// </para>
        /// </summary>
        private static string NameFor(VoicePreset? voice)
        {
            if (voice == null) return string.Empty;
            var name = (voice.Name ?? string.Empty).Trim();
            var tag = TagFor(voice);
            return tag.Length == 0 ? name : (name.Length == 0 ? tag : name + " " + tag);
        }

        private static string TagFor(VoicePreset voice)
        {
            if (voice.Backend == VoiceBackend.Remote) return "(OpenAI)";
            // The model's own speakers are Qwen too, but they were never cloned in Studio — saying
            // so would be a small lie, and which shelf a voice came off is the useful half anyway.
            if (!string.IsNullOrWhiteSpace(voice.SpeakerName)) return "(Qwen TTS, built-in)";
            return "(Qwen TTS Studio)";
        }

        /// <summary>
        /// Where the voice sits, said the way the folders read: <c>female/battania/Gwen</c>.
        /// <para>
        /// It replaces the old "woman's · made on this machine" opening because with a shelf of a
        /// hundred voices the useful question stopped being what KIND of voice it is and became
        /// which people it is for — that is what a player scanning the list is hunting. The maker
        /// still rides on the name itself, so nothing was actually lost.
        /// </para>
        /// </summary>
        private static string DetailFor(VoicePreset? voice)
        {
            if (voice == null) return string.Empty;

            var parts = new System.Collections.Generic.List<string>(3);

            var where = VoiceCasting.Label(voice);
            if (where.Length > 0) parts.Add(where);

            var said = (voice.ReferenceText ?? string.Empty).Trim();
            if (said.Length > 80) said = said.Substring(0, 80).TrimEnd() + "…";
            if (said.Length > 0) parts.Add(said);

            return string.Join(" · ", parts);
        }
    }
}
