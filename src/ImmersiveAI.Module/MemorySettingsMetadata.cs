namespace ImmersiveAI
{
    public static class MemorySettingsMetadata
    {
        public const int MinMemoryPercent = 1;
        public const int MaxMemoryPercent = 30;
        public const int MemoryPercentStep = 1;

        public const string MaxMemoryPercentDisplayName = "Compress memory above";
        public const string MinMemoryPercentDisplayName = "Shrink memory to";

        public const string MaxMemoryPercentHint =
            "Percent of the selected model's context window allowed for verbatim recent NPC dialogue before compression starts.";

        public const string MinMemoryPercentHint =
            "Percent of the selected model's context window to keep verbatim after compression.";
    }
}
