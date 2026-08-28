using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI
{
    /// <summary>
    /// What is left on the player's Claude plan, read from where the numbers actually live
    /// (2026.08.28, Anton's ask — mirroring living-abby's limits.py, which he approved on
    /// 2026.08.23). The CLI's own stream names a window and when it resets and stops there — it
    /// carries no percentage at all; the figures Claude Code shows in its own footer come from a
    /// small endpoint on the player's account, so ours come from the same one.
    /// <para>
    /// THE ONE RULE, held as code: the login token is read from Claude Code's own credentials file
    /// at the moment of asking, sent to api.anthropic.com and NOWHERE else, and never written
    /// down — not into the log, not into config.json, and never within a mile of an NPC's sheet.
    /// The player is shown the percentages. Nothing is shown the key.
    /// </para>
    /// Only the ClaudeCode backend ever consults this; a failed read shows nothing rather than
    /// guessing, and the last good reading stands while the network sulks.
    /// </summary>
    internal static class PlanGauge
    {
        private const string Url = "https://api.anthropic.com/api/oauth/usage";

        // A minute is finer than the thing being measured — a five-hour window moves about a third
        // of a percent a minute at play pace — and coarse enough that per-exchange notices never
        // queue up network reads.
        private const int TtlSeconds = 60;

        private static readonly object Gate = new object();
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        private static string? _label;
        private static DateTime _readAtUtc = DateTime.MinValue;
        private static bool _asking;

        /// <summary>The gauge as one short clause — "5h at 9%, weekly at 1%" — or null while it has
        /// never been readable. Never blocks: a stale reading goes back at once and the refresh
        /// happens on its own thread.</summary>
        public static string? Label()
        {
            RefreshIfStale();
            lock (Gate) return _label;
        }

        /// <summary>A call just finished — the moment the gauge is most worth being fresh for.
        /// The stream's own windows carry no figures, so they only prompt the real read.</summary>
        public static void NoteWindows(List<ClaudeCliShape.RateWindow> windows) => RefreshIfStale();

        private static void RefreshIfStale()
        {
            lock (Gate)
            {
                if (_asking || (DateTime.UtcNow - _readAtUtc).TotalSeconds < TtlSeconds) return;
                _asking = true;
            }
            Task.Run((Func<Task>)RefreshAsync);
        }

        private static async Task RefreshAsync()
        {
            string? found = null;
            try
            {
                var token = ReadToken();
                if (token != null)
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, Url))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
                        request.Headers.TryAddWithoutValidation("Accept", "application/json");
                        using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                                found = Compose(JObject.Parse(body));
                            }
                        }
                    }
                }
            }
            catch
            {
                // A bad minute must not blank the gauge — and must never surface the why, because
                // the why could carry request details. The last real reading stands.
            }
            finally
            {
                lock (Gate)
                {
                    _asking = false;
                    _readAtUtc = DateTime.UtcNow;
                    if (found != null) _label = found;
                }
            }
        }

        /// <summary>The display list Claude Code itself draws from, folded to Anton's own shape:
        /// "5h at 9%, weekly at 1%", a named weekly window appended only while it registers.</summary>
        internal static string? Compose(JObject body)
        {
            if (!(body["limits"] is JArray rows)) return null;

            string? fiveHour = null, weekly = null;
            var scoped = new List<string>();
            foreach (var row in rows)
            {
                var pctToken = row["percent"];
                if (pctToken == null || pctToken.Type == JTokenType.Null) continue;
                var pct = Math.Round((double)pctToken).ToString(CultureInfo.InvariantCulture) + "%";
                switch ((string?)row["kind"])
                {
                    case "session": fiveHour = "5h at " + pct; break;
                    case "weekly_all": weekly = "weekly at " + pct; break;
                    case "weekly_scoped":
                        var name = (string?)row.SelectToken("scope.model.display_name");
                        if (!string.IsNullOrWhiteSpace(name) && (double)pctToken >= 0.5)
                            scoped.Add(name + " at " + pct);
                        break;
                }
            }

            var parts = new List<string>(3);
            if (fiveHour != null) parts.Add(fiveHour);
            if (weekly != null) parts.Add(weekly);
            parts.AddRange(scoped);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private static string? ReadToken()
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");
                if (!File.Exists(path)) return null;
                var cred = JObject.Parse(File.ReadAllText(path));
                var token = (string?)cred.SelectToken("claudeAiOauth.accessToken");
                return string.IsNullOrWhiteSpace(token) ? null : token;
            }
            catch
            {
                return null;
            }
        }
    }
}
