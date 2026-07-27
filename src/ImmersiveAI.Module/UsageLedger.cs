using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    /// <summary>
    /// The cost ledger. Every LLM call reports the tokens the API itself measured (both clients
    /// parse the usage object from the response), and every player-visible interaction — a
    /// message, a greeting, a letter, a reach-out — closes with ONE soft notice of what it took:
    /// tokens in/out, how many calls it was (tool rounds, the feeling question, memory work all
    /// ride inside), and a price when the model's rates are known (ModelPrices in config.json,
    /// USD per million tokens). Attribution rides an AsyncLocal scope, so concurrent flows (a
    /// letter being written while the player chats) never mix their bills.
    /// Daily totals persist in usage.json beside the config, so MaxDailyRequests (0 = off) is a
    /// real safety valve across restarts. Best-effort everywhere.
    /// </summary>
    public static class UsageLedger
    {
        private sealed class Scope
        {
            public string Kind = "";
            public string Npc = "";
            public bool Quiet;
            public int Calls;
            public long TokensIn;
            public long TokensOut;
            public double CostUsd;
            public bool CostKnown = true;
        }

        private sealed class DailyRecord
        {
            public string Date = "";
            public int Requests;
            public long TokensIn;
            public long TokensOut;
            public double CostUsd;
        }

        private static readonly AsyncLocal<Scope?> Ambient = new AsyncLocal<Scope?>();
        private static readonly object Gate = new object();
        private static readonly Color NoticeColor = new Color(0.62f, 0.66f, 0.72f, 1f); // quiet slate — bookkeeping, not story

        private static ModConfig? _config;
        private static DailyRecord _today = new DailyRecord();
        private static int _sessionCalls;
        private static long _sessionIn, _sessionOut;
        private static double _sessionCost;

        public static string UsageFilePath => Path.Combine(ModConfig.ConfigDirectory, "usage.json");

        public static void Configure(ModConfig config)
        {
            lock (Gate)
            {
                _config = config;
                try
                {
                    if (File.Exists(UsageFilePath))
                        _today = JsonConvert.DeserializeObject<DailyRecord>(File.ReadAllText(UsageFilePath)) ?? new DailyRecord();
                }
                catch { _today = new DailyRecord(); }
                RollDate();
            }
        }

        // ------------------------------ the daily safety valve ------------------------------

        /// <summary>True when MaxDailyRequests is set and today's requests have reached it.</summary>
        public static bool DailyCapReached
        {
            get
            {
                lock (Gate)
                {
                    if (_config == null || _config.MaxDailyRequests <= 0) return false;
                    RollDate();
                    return _today.Requests >= _config.MaxDailyRequests;
                }
            }
        }

        /// <summary>Checked by the clients before every call; the reason is what the thrown error says.</summary>
        public static bool CanCall(out string reason)
        {
            if (DailyCapReached)
            {
                reason = $"Immersive AI's daily request cap is reached ({_config?.MaxDailyRequests}). Raise MaxDailyRequests in config.json (0 = no cap), or let the day turn.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        // ------------------------------ recording ------------------------------

        /// <summary>Called by the chat clients with the usage the API reported for one call.</summary>
        public static void RecordCall(string model, int tokensIn, int tokensOut)
        {
            try
            {
                bool known = TryPrice(model, tokensIn, tokensOut, out var cost);

                lock (Gate)
                {
                    RollDate();
                    _today.Requests++;
                    _today.TokensIn += tokensIn;
                    _today.TokensOut += tokensOut;
                    if (known) _today.CostUsd += cost;

                    _sessionCalls++;
                    _sessionIn += tokensIn;
                    _sessionOut += tokensOut;
                    if (known) _sessionCost += cost;

                    SaveDaily();
                }

                var scope = Ambient.Value;
                if (scope != null)
                {
                    scope.Calls++;
                    scope.TokensIn += tokensIn;
                    scope.TokensOut += tokensOut;
                    if (known) scope.CostUsd += cost; else scope.CostKnown = false;
                }
            }
            catch { /* bookkeeping only */ }
        }

        /// <summary>Opens one interaction's bill: everything the enclosed async flow spends —
        /// tool rounds, the feeling question, memory work — lands on it, and disposing it shows
        /// the one soft notice. Kind is a short player-facing word ("message", "letter"...).
        /// Quiet bills to log.txt and the totals only — for flows whose very existence is a
        /// surprise the player hasn't received yet (a private wish to reach out, a letter still
        /// sealed on the road).</summary>
        public static IDisposable BeginInteraction(string kind, string? npcName, bool quiet = false)
        {
            var scope = new Scope { Kind = kind ?? "", Npc = npcName ?? "", Quiet = quiet };
            Ambient.Value = scope;
            return new Ender(scope);
        }

        private sealed class Ender : IDisposable
        {
            private Scope? _scope;
            public Ender(Scope scope) { _scope = scope; }

            public void Dispose()
            {
                var scope = Interlocked.Exchange(ref _scope, null);
                if (scope == null) return;
                if (Ambient.Value == scope) Ambient.Value = null;
                if (scope.Calls == 0) return;

                var who = string.IsNullOrEmpty(scope.Npc) ? "" : scope.Npc + " — ";
                var calls = scope.Calls == 1 ? "1 call" : scope.Calls + " calls";
                var price = scope.CostKnown ? $", ~${scope.CostUsd.ToString("0.000", CultureInfo.InvariantCulture)}" : "";
                var line = $"✒ {who}{scope.Kind}: {scope.TokensIn:n0} → {scope.TokensOut:n0} tokens, {calls}{price}";

                ModLog.Info(line);

                if (!scope.Quiet && _config?.ShowCostNotices == true)
                    MainThreadDispatcher.Enqueue(() =>
                        InformationManager.DisplayMessage(new InformationMessage(line, NoticeColor)));
            }
        }

        /// <summary>The session so far, one line — for the odds view and curious players.</summary>
        public static string SessionSummary()
        {
            lock (Gate)
            {
                RollDate();
                return $"This session: {_sessionCalls} calls, {_sessionIn:n0} → {_sessionOut:n0} tokens, ~${_sessionCost.ToString("0.00", CultureInfo.InvariantCulture)}. " +
                       $"Today (all sessions): {_today.Requests} calls, ~${_today.CostUsd.ToString("0.00", CultureInfo.InvariantCulture)}" +
                       (_config != null && _config.MaxDailyRequests > 0 ? $" of a {_config.MaxDailyRequests}-call cap." : ".");
            }
        }

        // ------------------------------ prices ------------------------------

        /// <summary>Cost of one call, when the model's rates are known: the config's ModelPrices
        /// dict (USD per MILLION tokens), longest key contained in the model id wins.</summary>
        private static bool TryPrice(string model, int tokensIn, int tokensOut, out double cost)
        {
            cost = 0;
            try
            {
                var prices = _config?.ModelPrices;
                if (prices == null || string.IsNullOrWhiteSpace(model)) return false;

                ModConfig.ModelPrice? best = null;
                int bestLen = -1;
                foreach (var pair in prices)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null) continue;
                    if (model.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (pair.Key.Length > bestLen) { bestLen = pair.Key.Length; best = pair.Value; }
                }
                if (best == null) return false;

                cost = tokensIn / 1_000_000.0 * best.InputPerMTok + tokensOut / 1_000_000.0 * best.OutputPerMTok;
                return true;
            }
            catch { return false; }
        }

        // ------------------------------ the daily file ------------------------------

        private static void RollDate()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (_today.Date != today)
                _today = new DailyRecord { Date = today };
        }

        private static void SaveDaily()
        {
            try
            {
                Directory.CreateDirectory(ModConfig.ConfigDirectory);
                File.WriteAllText(UsageFilePath, JsonConvert.SerializeObject(_today, Formatting.Indented));
            }
            catch { /* the numbers still live for this session */ }
        }
    }
}
