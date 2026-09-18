using System;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI
{
    /// <summary>The ChatGPT subscription windows app-server reports. Unlike the Claude gauge this
    /// never reads a credential or calls an account endpoint itself: the already-authenticated,
    /// isolated Codex process asks, and hands only the percentages back.</summary>
    internal static class CodexPlanGauge
    {
        private const int TtlSeconds = 60;
        private static readonly object Gate = new object();
        private static string? _label;
        private static DateTime _readAtUtc = DateTime.MinValue;

        public static bool NeedsRefresh
        {
            get
            {
                lock (Gate) return (DateTime.UtcNow - _readAtUtc).TotalSeconds >= TtlSeconds;
            }
        }

        public static void Note(JObject result)
        {
            var label = CodexAppServerShape.ComposeRateLimitLabel(result);
            lock (Gate)
            {
                _readAtUtc = DateTime.UtcNow;
                if (label != null) _label = label;
            }
        }

        public static string? Label()
        {
            lock (Gate) return _label;
        }
    }
}
