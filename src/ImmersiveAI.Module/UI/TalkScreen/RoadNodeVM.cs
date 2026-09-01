using TaleWorlds.Library;

namespace ImmersiveAI.UI.TalkScreen
{
    /// <summary>
    /// One rung of the marriage road as a chip under a soul's name (2026.08.31, Anton's design:
    /// "maybe at the top have the stages … indicating where we are in this path and what the path is
    /// at all").
    ///
    /// <para>A row of these is the whole point: a path can only be read as a path if you can see the
    /// rungs behind you and the rungs ahead at the same time. One highlighted word in a sentence
    /// could never do it, which is why "heart's road: warmth" told Anton nothing for a month.</para>
    ///
    /// <para>The colour lives on the row rather than in the prefab because a Gauntlet TextWidget
    /// takes one brush colour for its whole string — so the three states (behind us, here, ahead)
    /// have to be three widgets, and each has to carry its own.</para>
    /// </summary>
    public sealed class RoadNodeVM : ViewModel
    {
        private readonly bool _last;

        public RoadNodeVM(Core.Courtship.CourtshipRail.Node node, bool last)
        {
            _last = last;
            Name = node?.Name ?? string.Empty;
            IsCurrent = node?.Current ?? false;
            IsDone = node?.Done ?? false;
            OfTheWorld = node?.OfTheWorld ?? false;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public bool IsCurrent { get; }
        [DataSourceProperty] public bool IsDone { get; }

        /// <summary>A rung of the world (her kin's word, the seasoning days) rather than of her
        /// heart — drawn only when it truly applies to this pair, and softened when it is.</summary>
        [DataSourceProperty] public bool OfTheWorld { get; }

        /// <summary>The chip's own text, the lit rung wearing its brackets so the path still reads
        /// as a path where colour cannot be trusted (a colour-blind eye, a dimmed screen).</summary>
        [DataSourceProperty]
        public string Text => IsCurrent ? "[ " + Name + " ]" : Name;

        /// <summary>The separator after this chip — nothing after the last one.</summary>
        [DataSourceProperty]
        public string Tail => _last ? string.Empty : "  ›  ";

        [DataSourceProperty]
        public Color TextColor =>
            IsCurrent ? new Color(0.94f, 0.80f, 0.42f, 1f)     // where we stand: the road's own gold
            : IsDone ? new Color(0.62f, 0.66f, 0.58f, 1f)      // behind us: quiet green-grey
            : OfTheWorld ? new Color(0.48f, 0.46f, 0.42f, 1f)  // a rung of the world still ahead
            : new Color(0.55f, 0.53f, 0.49f, 1f);              // ahead

        [DataSourceProperty] public Color TailColor => new Color(0.40f, 0.38f, 0.35f, 1f);
    }
}
