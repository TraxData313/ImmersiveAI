using System;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    /// <summary>
    /// The circuit breaker for a dying key: when the AI service starts refusing calls (bad key,
    /// out of credit, rate-limited, or down), the autonomous flows — reach-outs and letters —
    /// go quiet for a while instead of hammering a dead door every hour and painting the log
    /// red. The player's own words still try at once (they are deliberate), and any success
    /// reopens everything. The player is told plainly ONCE per quieting, not once per failure.
    /// </summary>
    public static class LlmGate
    {
        private static readonly object Gate = new object();
        private static DateTime _quietUntil = DateTime.MinValue;
        private static bool _noticeShown;

        /// <summary>Whether the hourly autonomous flows should stay quiet right now.</summary>
        public static bool AutonomyQuiet
        {
            get { lock (Gate) return DateTime.UtcNow < _quietUntil; }
        }

        public static void ReportSuccess()
        {
            lock (Gate)
            {
                if (_quietUntil == DateTime.MinValue && !_noticeShown) return;
                _quietUntil = DateTime.MinValue;
                _noticeShown = false;
            }
            ModLog.Info("LLM call succeeded — the road is open again; reach-outs and letters resume.");
        }

        /// <summary>Called by the chat clients when the service answers with an error status
        /// (0 = the connection itself failed before any answer came).</summary>
        public static void ReportFailure(int statusCode, string backend, string detail)
        {
            TimeSpan quiet;
            string why;
            if (statusCode == 401 || statusCode == 403) { quiet = TimeSpan.FromMinutes(30); why = "the AI service rejected the API key"; }
            else if (statusCode == 429) { quiet = TimeSpan.FromMinutes(15); why = "the AI service is rate-limiting or out of credit (429)"; }
            else if (statusCode >= 500) { quiet = TimeSpan.FromMinutes(5); why = "the AI service is having trouble (" + statusCode + ")"; }
            else if (statusCode == 0) { quiet = TimeSpan.FromMinutes(5); why = "the AI service could not be reached"; }
            else return; // a 400 is our request's bug, not a dying key — never quiet the world for it

            bool tell;
            lock (Gate)
            {
                var until = DateTime.UtcNow + quiet;
                if (until > _quietUntil) _quietUntil = until;
                tell = !_noticeShown;
                _noticeShown = true;
            }

            ModLog.Warn($"{backend} call failed ({(statusCode == 0 ? "network" : statusCode.ToString())}): {Truncate(detail, 300)} — autonomous flows quiet for {quiet.TotalMinutes:0} min.");

            if (tell)
            {
                MainThreadDispatcher.Enqueue(() => InformationManager.DisplayMessage(new InformationMessage(
                    $"Immersive AI: {why} — visits and letters will stay quiet for a while. Speaking to someone yourself still tries at once, and one success reopens everything.",
                    new Color(0.98f, 0.78f, 0.55f, 1f))));
            }
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s ?? string.Empty : s.Substring(0, max) + "…";
    }
}
