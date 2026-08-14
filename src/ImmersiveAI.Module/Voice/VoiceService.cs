using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Voices;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// The one door the rest of the mod knocks on to hear a line spoken. Two entry points, and
    /// everything else behind them.
    /// <para>
    /// <see cref="Prewarm"/> is the whole reason a voice feels instant: the moment a reply exists
    /// the words can be made into audio while the player is still reading the first sentence, so
    /// that when they ask to hear it there is nothing left to wait for. <see cref="Speak"/> plays
    /// what is there, making whatever is missing as it goes — the first bite goes into the air while
    /// the third is still being generated, which is the entire difference between "it speaks" and
    /// "it speaks quickly".
    /// </para>
    /// <para>
    /// THE GENERATION COUNTER is the small idea that keeps this honest. A synthesis started for one
    /// moment can easily outlive it — the player closes the window, walks off, talks to somebody
    /// else — and audio arriving late must be DISCARDED, never played over whatever is happening
    /// now. Every request carries the counter it was born under; a stale one is dropped on arrival,
    /// and a job nobody is waiting for any more stops rather than finishing at the engine's expense.
    /// </para>
    /// <para>
    /// NOTHING HERE MAY EVER COST A LINE. Every failure road ends in silence and a log line: no
    /// exception escapes, no call blocks the game thread, and with the engine absent, broken or
    /// simply turned off the mod behaves exactly as it did before it had a voice at all.
    /// </para>
    /// </summary>
    public static class VoiceService
    {
        /// <summary>Let the engine judge the tongue. The mod is played in many, and one line of
        /// Bulgarian inside an English conversation is a real case, not a corner one.</summary>
        private const int AutoLanguage = -1;

        private static readonly object ShelfGate = new object();
        private static readonly TimeSpan ShelfCheckRest = TimeSpan.FromSeconds(5);

        private static readonly ConcurrentDictionary<string, SynthJob> InFlight =
            new ConcurrentDictionary<string, SynthJob>();

        private static ModConfig? _config;
        private static VoiceHostClient? _client;
        private static long _generation;
        private static bool _hookedShutdown;
        private static bool _warnedRemote;

        private static IReadOnlyList<VoicePreset> _shelf = new List<VoicePreset>();
        private static VoiceAssignments _casting = new VoiceAssignments();
        private static DateTime _shelfCheckedUtc = DateTime.MinValue;
        private static DateTime _shelfStampUtc = DateTime.MinValue;
        private static bool _shelfLoaded;

        // ------------------------------------------------------------------

        /// <summary>Hands the service the one shared config, as every other manager here is handed it.</summary>
        public static void Configure(ModConfig config)
        {
            _config = config;
            if (!string.IsNullOrWhiteSpace(config?.VoiceSoundEvent))
                VoicePlayback.SoundEventName = config!.VoiceSoundEvent.Trim();
            HookShutdownOnce();
        }

        private static ModConfig? Config => _config ?? SubModule.Config;

        /// <summary>Where the player's voices and the casting sheet live.</summary>
        public static string VoicesRoot => VoiceCache.VoicesRoot;

        /// <summary>The casting sheet's own file. Deliberately NOT inside the campaign folder: the
        /// save-scoped memory snapshots photograph that folder, and rewinding a save must not
        /// silently recast anybody. Memory is a thing to rewind; a voice is not.</summary>
        public static string CastingFilePath => Path.Combine(VoicesRoot, "assignments.json");

        /// <summary>Whether a line could be spoken right now, engine and all.</summary>
        public static bool IsAvailable
        {
            get
            {
                try
                {
                    if (!EnabledQuick) return false;
                    return VoiceEngineDiscovery.Resolve(Config).IsComplete;
                }
                catch { return false; }
            }
        }

        /// <summary>Plainly why not, when <see cref="IsAvailable"/> is false. Empty when it is true.</summary>
        public static string UnavailableReason
        {
            get
            {
                try
                {
                    var config = Config;
                    if (config == null) return "the mod is still starting up";
                    if (!config.EnableVoice) return "voices are turned off in the settings";
                    if (VoiceEngineGate.DownForSession) return VoiceEngineGate.DownReason;
                    if (VoiceEngineGate.Quiet) return "the voices are resting after a run of failures";

                    var setup = VoiceEngineDiscovery.Resolve(config);
                    return setup.IsComplete ? string.Empty : setup.Missing;
                }
                catch (Exception ex) { return ex.Message; }
            }
        }

        /// <summary>Whether a reply should speak of its own accord, or wait to be asked. Read by
        /// whoever wires the conversation up; this service always speaks when told to.</summary>
        public static bool AutoSpeakEnabled => Config?.VoiceAutoSpeak ?? true;

        /// <summary>The sequence now being spoken — the counter a late arrival is judged against.</summary>
        public static long Generation => Interlocked.Read(ref _generation);

        // ------------------------------------------------------------------
        // The two doors
        // ------------------------------------------------------------------

        /// <summary>
        /// Makes this line's audio ahead of time and plays nothing. Returns at once; everything
        /// happens off the game thread. Cheap to call twice — a line already made, or already being
        /// made, costs nothing the second time.
        /// </summary>
        public static void Prewarm(Hero npc, string text)
        {
            try
            {
                if (!EnabledQuick) return;

                var who = Describe(npc);
                if (who == null || string.IsNullOrWhiteSpace(text)) return;

                Task.Run(() =>
                {
                    try
                    {
                        var plan = Plan(who, text);
                        if (plan == null) return;
                        if (VoiceCache.TryHit(plan.Voice.Id, plan.Key) != null) return;

                        // Pinned: made for the cache, so it is worth finishing even if nobody is
                        // listening by the time it lands. That is the whole point of a prewarm.
                        StartJob(plan, pinned: true, playbackGeneration: 0);
                    }
                    catch (Exception ex) { ModLog.Error("voice: preparing a line", ex); }
                });
            }
            catch (Exception ex) { ModLog.Error("voice: preparing a line", ex); }
        }

        /// <summary>
        /// Speaks this line: from the cache when it is there, making it as it goes when it is not.
        /// Call from the game thread. Whatever was being said is cut off — the newest words win.
        /// </summary>
        public static void Speak(Hero npc, string text)
        {
            try
            {
                if (!EnabledQuick) return;

                var who = Describe(npc);
                if (who == null || string.IsNullOrWhiteSpace(text)) return;

                // Claimed here, on the game thread, so two lines racing can never both believe they
                // are the newest one.
                var generation = Interlocked.Increment(ref _generation);
                VoicePlayback.Begin(generation);

                Task.Run(() =>
                {
                    var opened = true;
                    try
                    {
                        var plan = Plan(who, text);
                        if (plan == null) return;

                        var cached = VoiceCache.TryHit(plan.Voice.Id, plan.Key);
                        if (cached != null)
                        {
                            foreach (var file in cached) VoicePlayback.Enqueue(generation, file);
                            return;
                        }

                        var job = StartJob(plan, pinned: false, playbackGeneration: generation);
                        opened = false;    // the job's own finisher closes the sequence
                        job.Subscribe(
                            path => VoicePlayback.Enqueue(generation, path),
                            _ => VoicePlayback.Complete(generation));
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("voice: speaking a line", ex);
                    }
                    finally
                    {
                        // Every road out must close the sequence, or the playback chain waits for a
                        // chunk that is never coming and the driver spins for the rest of the session.
                        if (opened) VoicePlayback.Complete(generation);
                    }
                });
            }
            catch (Exception ex) { ModLog.Error("voice: speaking a line", ex); }
        }

        /// <summary>Silence, now. What was in the air stops and what was waiting is forgotten;
        /// anything still being made is dropped on arrival rather than played late.</summary>
        public static void Stop()
        {
            try
            {
                Interlocked.Increment(ref _generation);
                VoicePlayback.StopAll();
            }
            catch (Exception ex) { ModLog.Error("voice: stopping", ex); }
        }

        /// <summary>Puts the voice away: silence, and the engine's host with it. Called on the way
        /// out of the process, and safe to call more than once.</summary>
        public static void Shutdown()
        {
            try
            {
                Interlocked.Increment(ref _generation);
                VoicePlayback.StopAll();

                // The client is let go of HERE and put away THERE. Disposing it asks the host to quit
                // politely, and that road waits: on the write gate, which a background line's stuck
                // Flush can hold, and then on the child's own exit. This runs on the game thread when
                // a campaign ends — a wedged host would have frozen the game at the loading screen
                // for good. The child watches our pid once a second and leaves on its own, so the
                // worst this costs is a second of a host nobody is talking to any more.
                var client = Interlocked.Exchange(ref _client, null);
                if (client != null) Task.Run(() =>
                {
                    try { client.Dispose(); }
                    catch (Exception ex) { ModLog.Error("voice: putting the engine away", ex); }
                });
            }
            catch (Exception ex) { ModLog.Error("voice: shutting down", ex); }
        }

        /// <summary>Looks for the engine again and opens the gate — for the player who installs it
        /// while the game is running, and for a "try once more" lever.</summary>
        public static void Rediscover()
        {
            VoiceEngineDiscovery.Forget();
            VoiceEngineGate.Reopen();
            lock (ShelfGate) { _shelfLoaded = false; _shelfCheckedUtc = DateTime.MinValue; }
        }

        // ------------------------------------------------------------------
        // Working out what is to be made
        // ------------------------------------------------------------------

        private static bool EnabledQuick
        {
            get
            {
                var config = Config;
                return config != null && config.EnableVoice && VoiceEngineGate.IsOpen;
            }
        }

        /// <summary>The few facts about a soul that the rest of this needs — read on the caller's
        /// thread, because everything after it happens on another one and campaign objects are not
        /// ours to touch there.</summary>
        private sealed class Speaker
        {
            public string Id = string.Empty;
            public bool IsFemale;
            public bool IsPlayer;
        }

        /// <summary>
        /// Lifts the three facts a voice needs off a live Hero, and NOTHING else.
        /// <para>
        /// DELIBERATELY CALLABLE OFF THE GAME THREAD, which is why the three are named here rather
        /// than left to whoever edits this next. <c>StringId</c> and <c>IsFemale</c> never change
        /// once a hero exists, and <c>MainHero</c> does not change inside a conversation — so these
        /// reads are safe from the background thread a reply lands on, and prewarming can begin the
        /// moment the words exist instead of waiting a tick for the dispatcher.
        /// </para>
        /// <para>
        /// DO NOT WIDEN THIS off the game thread. Anything that can move while a talk is running —
        /// a party, a settlement, a clan, a relation, kin, equipment — is campaign state, and
        /// reading it here would put a race in the quietest possible place: a garnish feature
        /// touching live world data on a thread that has no business doing so. If a voice ever needs
        /// more than these three, capture it on the game thread at the call site and pass it in.
        /// </para>
        /// </summary>
        private static Speaker? Describe(Hero? npc)
        {
            try
            {
                if (npc == null) return null;
                return new Speaker
                {
                    Id = npc.StringId ?? string.Empty,
                    IsFemale = npc.IsFemale,
                    IsPlayer = npc == Hero.MainHero,
                };
            }
            catch { return null; }
        }

        private sealed class SpeechPlan
        {
            public VoicePreset Voice = new VoicePreset();
            public string Key = string.Empty;
            public string Kind = "default";
            public string VoicePath = string.Empty;
            public string SpeakerName = string.Empty;
            public IReadOnlyList<string> Bites = new List<string>();
            public string Text = string.Empty;
        }

        private static SpeechPlan? Plan(Speaker who, string text)
        {
            if (!IsAvailable) return null;

            var voice = VoiceFor(who);
            if (voice == null) return null;

            if (!TryVoiceArguments(voice, out var kind, out var voicePath, out var speakerName))
                return null;

            // The gestures are acted, not read aloud: a voice reciting *sets down her cup* is
            // instantly a machine reading stage directions.
            var spoken = SpeakableText.SpokenOnly(text);
            if (spoken.Length == 0) return null;

            var bites = SpeakableText.BitesFor(text);
            if (bites.Count == 0) return null;

            return new SpeechPlan
            {
                Voice = voice,
                Key = VoiceCacheKey.For(voice.Id, spoken, ModelName(), AutoLanguage),
                Kind = kind,
                VoicePath = voicePath,
                SpeakerName = speakerName,
                Bites = bites,
                Text = spoken,
            };
        }

        private static string ModelName()
        {
            try { return VoiceEngineDiscovery.Resolve(Config).ModelName; }
            catch { return string.Empty; }
        }

        private static VoicePreset? VoiceFor(Speaker who)
        {
            EnsureShelf();

            lock (ShelfGate)
            {
                // The player's own voice is its own slot and empty by default — most people do not
                // want to hear themselves, and that is the honest default rather than an oversight.
                var id = who.IsPlayer ? _casting.Player : _casting.VoiceFor(who.Id, who.IsFemale);
                if (string.IsNullOrWhiteSpace(id)) return null;

                return _shelf.FirstOrDefault(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static bool TryVoiceArguments(VoicePreset voice, out string kind, out string path, out string speaker)
        {
            kind = "default";
            path = string.Empty;
            speaker = string.Empty;

            if (voice.Backend == VoiceBackend.Remote)
            {
                // A hosted speech endpoint is a different road entirely and not one this local host
                // travels. Said once, in the log, rather than failing quietly for every line.
                if (!_warnedRemote)
                {
                    _warnedRemote = true;
                    ModLog.Warn($"voice: \"{voice.Name}\" is a hosted voice; the local engine cannot speak it.");
                }
                return false;
            }

            // THE EMBEDDING FIRST, and this order is measured rather than reasoned. Against the real
            // engine on the base talker model (2026.08.14), the embedding road speaks a five-second
            // line in 1.3 s and does it again and again; the ICL road on the same voice, the same
            // model and the same words answered ok:true with 1920 samples — eight hundredths of a
            // second — and then "No speech codes generated" for the very next bite. The likely
            // reason is that ICL wants its prompt encoder loaded separately
            // (qwen3_tts_load_icl_prompt_encoder_with_name) and a base model has none.
            //
            // This matters more than it looks: a voice imported from Studio carries BOTH files, so
            // preferring ICL — which on paper is the better clone — would have quietly sent EVERY
            // voice down the dead road and made the whole feature look broken. If the ICL road is
            // ever made to work, prove it live before turning this around.
            if (!string.IsNullOrWhiteSpace(voice.EmbeddingPath))
            {
                kind = "embedding";
                path = voice.EmbeddingPath;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(voice.IclPromptPath))
            {
                kind = "icl";
                path = voice.IclPromptPath;
                return true;
            }

            // "speaker" and "default" are part of the wire protocol and unreachable from here today:
            // a preset with neither file is not IsSpeakable, so the library never hands one over.
            return false;
        }

        /// <summary>
        /// Keeps the shelf and the casting sheet fresh without re-reading the disk for every line.
        /// A player editing a voice folder or the sheet while the game runs is a real thing to do,
        /// so the folder's own write stamp is checked — at most every few seconds.
        /// </summary>
        private static void EnsureShelf()
        {
            lock (ShelfGate)
            {
                var now = DateTime.UtcNow;
                if (_shelfLoaded && now - _shelfCheckedUtc < ShelfCheckRest) return;
                _shelfCheckedUtc = now;

                var stamp = ShelfStamp();
                if (_shelfLoaded && stamp == _shelfStampUtc) return;

                try
                {
                    var root = VoicesRoot;
                    Directory.CreateDirectory(root);

                    _shelf = VoiceLibrary.Load(root, out var skipped);
                    foreach (var problem in skipped) ModLog.Warn("voice: " + problem);

                    _casting = VoiceAssignments.Load(CastingFilePath);
                    var dropped = _casting.ForgetMissing(_shelf.Select(v => v.Id));
                    var filled = _casting.FillEmptyDefaults(_shelf);
                    if (dropped > 0 || filled)
                    {
                        _casting.Save(CastingFilePath);
                        ModLog.Info($"voice: casting sheet tidied — {dropped} stale, defaults {(filled ? "filled" : "kept")}.");
                    }

                    _shelfLoaded = true;
                    // Taken AFTER the save, or the sheet we just wrote reads as somebody else's edit.
                    _shelfStampUtc = ShelfStamp();
                }
                catch (Exception ex)
                {
                    ModLog.Error("voice: reading the shelf of voices", ex);
                    _shelf = new List<VoicePreset>();
                    _casting = new VoiceAssignments();
                    _shelfLoaded = true;      // do not retry on every single line
                    _shelfStampUtc = stamp;
                }
            }
        }

        private static DateTime ShelfStamp()
        {
            var newest = DateTime.MinValue;
            try
            {
                if (Directory.Exists(VoicesRoot))
                {
                    newest = Directory.GetLastWriteTimeUtc(VoicesRoot);
                    foreach (var folder in Directory.GetDirectories(VoicesRoot))
                    {
                        var stamp = Directory.GetLastWriteTimeUtc(folder);
                        if (stamp > newest) newest = stamp;
                    }
                }
                if (File.Exists(CastingFilePath))
                {
                    var stamp = File.GetLastWriteTimeUtc(CastingFilePath);
                    if (stamp > newest) newest = stamp;
                }
            }
            catch { /* an unreadable shelf is simply an unchanged one */ }
            return newest;
        }

        // ------------------------------------------------------------------
        // Making it
        // ------------------------------------------------------------------

        /// <summary>
        /// One utterance being made, and everyone waiting on it. A prewarm and a play of the same
        /// line share ONE job: the second caller subscribes to the first's chunks instead of asking
        /// the engine for the same words twice, and hears everything already made replayed in order
        /// the moment it attaches.
        /// </summary>
        private sealed class SynthJob
        {
            private readonly object _gate = new object();
            private readonly List<string> _ready = new List<string>();
            private readonly List<Action<string>> _listeners = new List<Action<string>>();
            private readonly List<Action<bool>> _finishers = new List<Action<bool>>();

            private bool _done;
            private bool _failed;
            private bool _started;

            public string Key = string.Empty;

            /// <summary>Worth finishing even with nobody listening: it was asked for as cache.</summary>
            public bool Pinned;

            /// <summary>The playback sequence this was started for, or 0 for a pure prewarm.</summary>
            public long PlaybackGeneration;

            /// <summary>
            /// Joins this job, and answers whether the caller is the one who must run it. The
            /// single-flight lives here rather than beside the dictionary because both the claim and
            /// what the newcomer wants of the job have to be settled under one lock — otherwise a
            /// prewarm arriving a hair after a play could fail to pin a job that is already deciding
            /// whether anyone still wants it.
            /// </summary>
            public bool Claim(bool pinned, long playbackGeneration)
            {
                lock (_gate)
                {
                    Pinned |= pinned;
                    if (playbackGeneration != 0) PlaybackGeneration = playbackGeneration;
                    if (_started) return false;
                    _started = true;
                    return true;
                }
            }

            public void Subscribe(Action<string> onChunk, Action<bool> onDone)
            {
                // The replay happens INSIDE the lock, on purpose: outside it a chunk published by
                // the worker could reach the new listener before the ones it is being caught up on,
                // and a reply would be spoken with its sentences out of order.
                lock (_gate)
                {
                    foreach (var path in _ready)
                        try { onChunk(path); } catch (Exception ex) { ModLog.Error("voice: handing over a chunk", ex); }

                    if (_done)
                    {
                        try { onDone(!_failed); } catch (Exception ex) { ModLog.Error("voice: closing a line", ex); }
                        return;
                    }

                    _listeners.Add(onChunk);
                    _finishers.Add(onDone);
                }
            }

            public void Publish(string path)
            {
                List<Action<string>> listeners;
                lock (_gate)
                {
                    _ready.Add(path);
                    listeners = new List<Action<string>>(_listeners);
                }
                foreach (var listener in listeners)
                    try { listener(path); } catch (Exception ex) { ModLog.Error("voice: handing over a chunk", ex); }
            }

            public void Finish(bool ok)
            {
                List<Action<bool>> finishers;
                lock (_gate)
                {
                    if (_done) return;
                    _done = true;
                    _failed = !ok;
                    finishers = new List<Action<bool>>(_finishers);
                    _listeners.Clear();
                    _finishers.Clear();
                }
                foreach (var finisher in finishers)
                    try { finisher(ok); } catch (Exception ex) { ModLog.Error("voice: closing a line", ex); }
            }

            /// <summary>True once nobody can still be waiting for this — the moment it was made for
            /// has passed, and it was not asked for as cache.</summary>
            public bool Abandoned =>
                !Pinned && PlaybackGeneration != 0 && PlaybackGeneration != Generation;
        }

        private static SynthJob StartJob(SpeechPlan plan, bool pinned, long playbackGeneration)
        {
            // GetOrAdd may hand back a job already under way — a prewarm the player has now asked to
            // hear. Only whoever claims it runs it; everyone else simply listens.
            var job = InFlight.GetOrAdd(plan.Key, key => new SynthJob { Key = key });
            if (job.Claim(pinned, playbackGeneration))
                Task.Run(() => RunJobAsync(job, plan));

            return job;
        }

        private static async Task RunJobAsync(SynthJob job, SpeechPlan plan)
        {
            var ok = false;
            var folder = string.Empty;
            try
            {
                folder = VoiceCache.PrepareFolder(plan.Voice.Id, plan.Key);
                if (folder.Length == 0) return;

                var client = EnsureClient();
                var setup = VoiceEngineDiscovery.Resolve(Config);
                if (!setup.IsComplete) { VoiceEngineGate.ReportDown(setup.Missing); return; }

                // ONE request for the whole reply, streamed back in pieces.
                //
                // It used to be one request per sentence, and that was wrong twice. The engine rolls
                // its prosody afresh for every generation, so the voice audibly became a different
                // person at each seam — "definitely an immersion breaker", and the right verdict. And
                // it re-paid the prefill and the speaker encode per sentence: measured on the same
                // card with the game running, 3.60x realtime streamed against 0.34x cut up. The
                // faithful road turned out to be the fast one.
                //
                // The pieces still land as 000.wav, 001.wav … in the same cache folder, so nothing
                // downstream changed: playback chains them, and a replay from cache is identical.
                var made = 0;
                var reply = await client.SynthesizeAsync(
                    id: plan.Key,
                    text: plan.Text,
                    outPath: VoiceCache.ChunkPath(folder, 0),
                    voiceKind: plan.Kind,
                    voicePath: plan.VoicePath,
                    speaker: plan.SpeakerName,
                    languageId: AutoLanguage,
                    setup: setup,
                    onChunk: (index, path, samples) =>
                    {
                        // Straight from the host's reader thread: hand it to playback the moment it
                        // exists. Abandoned means the player has moved on — stop publishing, but let
                        // the generation finish into the cache; it is paid for either way.
                        if (job.Abandoned) return;
                        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
                        made++;
                        job.Publish(path);
                    }).ConfigureAwait(false);

                if (!reply.Ok)
                {
                    VoiceEngineGate.ReportFailure(reply.Error);
                    return;
                }

                if (made == 0)
                {
                    VoiceEngineGate.ReportFailure("the engine reported a reply it did not write");
                    return;
                }

                // The same too-short guard as before, now against the WHOLE reply: on a voice road
                // the model cannot travel it answers ok:true having made a fraction of a second, and
                // sealed, that blip would be this line's voice for ever.
                if (IsTooShortToBeReal(reply, plan.Text))
                {
                    VoiceEngineGate.ReportFailure(
                        $"the engine answered with {reply.Samples} samples for {plan.Text.Length} characters");
                    return;
                }

                VoiceEngineGate.ReportSuccess();

                VoiceCache.Seal(folder, new VoiceCacheManifest
                {
                    Key = plan.Key,
                    VoiceId = plan.Voice.Id,
                    Model = ModelName(),
                    LanguageId = AutoLanguage,
                    Chunks = made,
                    Text = plan.Text,
                });

                ok = true;
                VoiceCache.PruneSoon(BudgetBytes());
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: making a line", ex);
                VoiceEngineGate.ReportFailure(ex.Message);
            }
            finally
            {
                // Order matters: let go of the single-flight claim only after the job is closed, so
                // a caller arriving this instant either joins a finished job or starts a clean one.
                job.Finish(ok);
                InFlight.TryRemove(job.Key, out _);
            }
        }

        /// <summary>
        /// Whether an "ok" is too short to be a real reading of these words. Deliberately a floor
        /// and not a ratio: real speech varies enormously with language and voice, and rejecting a
        /// merely brisk reading would be a worse bug than the one this catches. It only fires on
        /// audio so short that no reading of the words could fit inside it.
        /// </summary>
        private static bool IsTooShortToBeReal(VoiceHostReply reply, string bite)
        {
            if (reply.Rate <= 0 || reply.Samples <= 0) return false;   // nothing to judge it by
            if (bite == null || bite.Trim().Length < 20) return false; // short enough to be honest

            return reply.Samples / (double)reply.Rate < 0.25;
        }

        private static long BudgetBytes()
        {
            var mb = Config?.VoiceCacheBudgetMb ?? 0;
            return mb <= 0 ? 0 : (long)mb * 1024 * 1024;
        }

        private static VoiceHostClient EnsureClient()
        {
            var client = _client;
            if (client != null) return client;

            var fresh = new VoiceHostClient();
            var won = Interlocked.CompareExchange(ref _client, fresh, null);
            if (won == null) { HookShutdownOnce(); return fresh; }

            fresh.Dispose();
            return won;
        }

        /// <summary>The child must not outlive us. It watches our pid and leaves on its own, but
        /// asking it plainly on the way out is cheaper than making it notice.</summary>
        private static void HookShutdownOnce()
        {
            if (_hookedShutdown) return;
            _hookedShutdown = true;
            try
            {
                AppDomain.CurrentDomain.ProcessExit += (_, __) => { try { Shutdown(); } catch { } };
            }
            catch { /* no hook is not worth a failure; the watchdog still ends it */ }
        }
    }
}
