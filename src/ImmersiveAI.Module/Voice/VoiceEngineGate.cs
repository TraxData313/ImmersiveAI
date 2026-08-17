using System;
using TaleWorlds.Library;

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// The circuit breaker for the voice, in <see cref="LlmGate"/>'s image and with its restraint.
    /// <para>
    /// The difference from the LLM's gate is what silence COSTS. A quiet LLM is a mute NPC and the
    /// feature is gone; a quiet voice is the mod exactly as it was for its whole life before this —
    /// the words still arrive, they simply are not spoken aloud. So this gate is quicker to give up
    /// and far quieter about it: a few failures in a row and the voice rests for a while, a host
    /// that will not stay up at all and it rests for the session. ONE plain notice per quieting,
    /// never one per failure, and any success reopens everything.
    /// </para>
    /// </summary>
    public static class VoiceEngineGate
    {
        /// <summary>Failures in a row before the voice rests. Two is noise; three is a pattern.</summary>
        private const int FailuresBeforeQuiet = 3;

        private static readonly TimeSpan QuietFor = TimeSpan.FromMinutes(5);

        /// <summary>The same slate the cost ledger speaks in: this is bookkeeping, not story.</summary>
        private static readonly Color NoticeColor = new Color(0.62f, 0.66f, 0.72f, 1f);

        private static readonly object Gate = new object();
        private static DateTime _quietUntil = DateTime.MinValue;
        private static bool _quietNoticeShown;
        private static bool _down;
        private static string _downReason = string.Empty;
        private static int _consecutive;

        /// <summary>Whether anything should try to speak right now.</summary>
        public static bool IsOpen
        {
            get { lock (Gate) return !_down && DateTime.UtcNow >= _quietUntil; }
        }

        /// <summary>Resting for a while after a run of failures — it will come back on its own.</summary>
        public static bool Quiet
        {
            get { lock (Gate) return !_down && DateTime.UtcNow < _quietUntil; }
        }

        /// <summary>Given up for this session. Nothing but a restart (or <see cref="Reopen"/>)
        /// brings it back, and the player has been told once, plainly.</summary>
        public static bool DownForSession
        {
            get { lock (Gate) return _down; }
        }

        /// <summary>Why it is down, in the words the player was shown. Empty when it is not.</summary>
        public static string DownReason
        {
            get { lock (Gate) return _downReason; }
        }

        /// <summary>A voice was made. Everything that was holding back may go again.</summary>
        public static void ReportSuccess()
        {
            bool wasResting;
            lock (Gate)
            {
                wasResting = _quietUntil != DateTime.MinValue || _consecutive > 0;
                _consecutive = 0;
                _quietUntil = DateTime.MinValue;
                _quietNoticeShown = false;
            }
            if (wasResting) ModLog.Info("voice: a line was spoken — the voice is open again.");
        }

        /// <summary>
        /// One synthesis (or one bring-up) failed. Counted, not announced: the run of them is what
        /// means something. <paramref name="detail"/> goes to the log, never to the player.
        /// </summary>
        public static void ReportFailure(string detail)
        {
            bool tell;
            lock (Gate)
            {
                if (_down) return;

                _consecutive++;
                ModLog.Warn($"voice: failed ({_consecutive}/{FailuresBeforeQuiet}) — {Truncate(detail, 300)}");
                if (_consecutive < FailuresBeforeQuiet) return;

                var until = DateTime.UtcNow + QuietFor;
                if (until > _quietUntil) _quietUntil = until;
                tell = !_quietNoticeShown;
                _quietNoticeShown = true;
            }

            ModLog.Warn($"voice: quiet for {QuietFor.TotalMinutes:0} min after {FailuresBeforeQuiet} failures in a row.");

            if (tell) Say("Immersive AI: the voices have gone quiet for a few minutes — the speech engine is not answering. Everything else carries on as usual.");
        }

        /// <summary>
        /// The voice cannot serve this session at all: the engine is not installed, or its host will
        /// not stay up. Said once, kindly, and never mentioned again — the mod is perfectly whole
        /// without it, and the player did not ask to be nagged.
        /// </summary>
        public static void ReportDown(string reason)
        {
            lock (Gate)
            {
                if (_down) return;
                _down = true;
                _downReason = reason ?? string.Empty;
            }

            ModLog.Warn("voice: down for the session — " + Truncate(reason, 400));
            Say("Immersive AI: the voices are off for this session — " + Truncate(reason, 200) + ". Nothing else is affected.");
        }

        /// <summary>Opens everything again, for a "try once more" lever and for the player who
        /// installs the engine while the game is running.</summary>
        public static void Reopen()
        {
            lock (Gate)
            {
                _down = false;
                _downReason = string.Empty;
                _consecutive = 0;
                _quietUntil = DateTime.MinValue;
                _quietNoticeShown = false;
            }
            ModLog.Info("voice: the gate was opened by hand.");
        }

        /// <summary>
        /// A reading ran away and was cut. Says so once, plainly, and then holds its tongue for a
        /// while.
        /// <para>
        /// Worth a word to the player where most of this file's troubles are not, because of what it
        /// SOUNDS like: a derail is a held, wordless note in a voice they know, and the playtest that
        /// prompted this guard (2026.08.17) described it as scary. Told that the mod noticed and
        /// stopped it, it is a stumble; untold, it is the game doing something inexplicable. It says
        /// nothing about why, since there is no why the player can act on — the line is simply spoken
        /// afresh the next time it is asked for, the clip never having been kept.
        /// </para>
        /// </summary>
        public static void NoteStumble()
        {
            lock (Gate)
            {
                if (DateTime.UtcNow < _stumbleQuietUntil) return;
                _stumbleQuietUntil = DateTime.UtcNow + StumbleQuietFor;
            }
            Say("The voice stumbled over that line and was cut short.");
        }

        /// <summary>One derail is a dice roll and worth mentioning; six in a row is a broken engine
        /// shouting, and the gate's own quieting is the answer to that.</summary>
        private static readonly TimeSpan StumbleQuietFor = TimeSpan.FromMinutes(3);

        private static DateTime _stumbleQuietUntil = DateTime.MinValue;

        private static void Say(string message)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                try { InformationManager.DisplayMessage(new InformationMessage(message, NoticeColor)); }
                catch { /* a notice that cannot be shown is not worth a second failure */ }
            });
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s ?? string.Empty : s.Substring(0, max) + "…";
    }
}
