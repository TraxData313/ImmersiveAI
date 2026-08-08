namespace ImmersiveAI
{
    /// <summary>
    /// The one place the memory-consolidation dials are described: the rails <see cref="ModConfig.Normalize"/>
    /// clamps to, the names the in-game menu shows, and the hints that explain each one (defaults named
    /// in the hint itself, so the menu never leaves a player guessing what "normal" was). MCM attributes
    /// take compile-time constants, so the menu and the clamps can share these literally — change a rail
    /// here and both move together.
    /// </summary>
    public static class MemorySettingsMetadata
    {
        // ── the share of the model's context window kept verbatim ──
        public const int MinMemoryPercent = 1;
        public const int MaxMemoryPercent = 30;
        public const int MemoryPercentStep = 1;

        public const string MaxMemoryPercentDisplayName = "Compress memory above (% of context)";
        public const string MinMemoryPercentDisplayName = "Shrink memory back to (% of context)";

        public const string MaxMemoryPercentHint =
            "How much of the model's context window one NPC's word-for-word memory of you may fill before it is folded into their rolling summary. Higher = they recall more exactly, at a higher cost per reply. Default 10%.";

        public const string MinMemoryPercentHint =
            "What that word-for-word memory is trimmed back to once a compression runs. Must stay below the ceiling above (it is halved automatically if it does not). Default 5%.";

        // ── the turn count ──
        public const int MinRecentTurns = 2;
        public const int MaxRecentTurnsCeiling = 200;

        public const string MaxRecentTurnsDisplayName = "Compress memory above (exchanges)";
        public const string KeepRecentTurnsDisplayName = "Keep after compression (exchanges)";

        public const string MaxRecentTurnsHint =
            "The other ceiling: how many exchanges an NPC keeps word for word before older ones are folded into the summary. Whichever ceiling is reached first — this, the percent, or the days — starts the compression. Default 30.";

        public const string KeepRecentTurnsHint =
            "How many of the newest exchanges stay word for word after a compression. Must stay below the ceiling above. Default 15.";

        // ── the age of what is kept ──
        public const int MinRecentDays = 1;
        public const int MaxRecentDaysCeiling = 365;

        public const string MaxRecentDaysDisplayName = "Compress memory older than (days)";
        public const string KeepRecentDaysDisplayName = "Keep after compression (days)";

        public const string MaxRecentDaysHint =
            "The third ceiling: an exchange older than this many in-game days is folded into the summary even if nothing else has filled up — so an NPC's verbatim memory is of the recent road, not of a year ago. Default 30.";

        public const string KeepRecentDaysHint =
            "After a compression, only exchanges from the last this-many in-game days stay word for word. Must stay below the ceiling above. Default 15.";

        // ── the room the memory-writing calls get ──
        public const int MinMemoryWriteTokens = 100;
        public const int MaxMemoryWriteTokensCeiling = 8000;

        public const string MemoryWriteTokensDisplayName = "Room for memory writing (tokens)";

        public const string MemoryWriteTokensHint =
            "The output budget for the calls where an NPC reworks their memory — the rolling summary, the truths they hold, who they have become, what they strive for. Kept apart from the reply length so reflection is never squeezed. Never falls below the reply length. Default 1500.";

        public const string NotifyOnMemoryRefactorDisplayName = "Notice when they turn memories over";

        public const string NotifyOnMemoryRefactorHint =
            "A soft grey line the moment an NPC's memory is compressed — 'turns over old memories of you, and settles them deeper'. Default on.";
    }
}
