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

            // Poured-together pieces are deleted as they finish playing; a crash mid-sentence would
            // leave one behind. Swept once, at the start, rather than tracked forever.
            try { VoicePlayback.SweepJoined(); } catch { /* housekeeping */ }
        }

        private static ModConfig? Config => _config ?? SubModule.Config;

        /// <summary>Where the player's voices and the casting sheet live.</summary>
        public static string VoicesRoot => VoiceCache.VoicesRoot;

        /// <summary>The casting sheet's own file. Deliberately NOT inside the campaign folder: the
        /// save-scoped memory snapshots photograph that folder, and rewinding a save must not
        /// silently recast anybody. Memory is a thing to rewind; a voice is not.</summary>
        public static string CastingFilePath => Path.Combine(VoicesRoot, "assignments.json");

        /// <summary>Whether a line could be spoken right now — by EITHER road. The two are equals
        /// here: an engine on the player's own machine, or a hosted service on a key they already
        /// have. Everything above this point is indifferent to which.</summary>
        public static bool IsAvailable
        {
            get
            {
                try
                {
                    if (!EnabledQuick) return false;
                    return LocalReady || CloudVoiceClient.IsConfigured(Config);
                }
                catch { return false; }
            }
        }

        /// <summary>Whether the player's own machine can speak: the engine is installed and found.</summary>
        public static bool LocalReady
        {
            get
            {
                try { return VoiceEngineDiscovery.Resolve(Config).IsComplete; }
                catch { return false; }
            }
        }

        /// <summary>Whether the hosted road is open: a key is set.</summary>
        public static bool CloudReady => CloudVoiceClient.IsConfigured(Config);

        /// <summary>
        /// THE NEXT THING TO ACTUALLY DO to get voices on this machine. Empty when the local road is
        /// already open.
        /// <para>
        /// Separate from <see cref="UnavailableReason"/> on purpose: that one says what is wrong
        /// ("the speech engine (qwen3_tts.dll) was not found"), which is a diagnosis, not an
        /// instruction. A player who has not read the setup page cannot act on a missing DLL's name.
        /// So this names the program, where to get it, and which model to pull — the three facts the
        /// diagnosis leaves out. The once-per-install nudge deliberately says none of this and points
        /// at the panel instead, so the panel has to be the place that explains itself.
        /// </para>
        /// </summary>
        public static string SetupAdvice
        {
            get
            {
                try
                {
                    var config = Config;
                    if (config == null) return string.Empty;

                    var setup = VoiceEngineDiscovery.Resolve(config);
                    if (setup.IsComplete) return string.Empty;

                    // Our own file, not theirs — a different problem with a different remedy, and
                    // telling someone to fetch an engine over it would send them the wrong way.
                    if (setup.HostExePath.Length == 0)
                        return "The mod's own voice program is missing from its folder — reinstalling Immersive AI puts it back.";

                    // Said at the door rather than twenty minutes into a download: the speech engine
                    // is CUDA-only, so on any other card the whole 2.8 GB would be spent to fail at
                    // model load. The hosted road asks nothing of the machine at all.
                    if (!VoiceFetcher.HasNvidiaCard)
                        return "Voices made on this machine need an NVIDIA graphics card — the speech engine has no other build. "
                             + "The hosted road works on any machine: put a key in the settings under Voices → Hosted voices.";

                    return "Press \"Download the voices\" below and it fetches everything itself — about "
                         + VoiceFetcher.DownloadSize + ", nothing to install, no administrator rights. "
                         + "You can carry on playing while it runs.";
                }
                catch { return string.Empty; }
            }
        }

        /// <summary>True when the speech engine is installed but has no model to speak with — the
        /// one-download-from-done state. A flag rather than a caller matching on the wording of
        /// <see cref="SetupAdvice"/>, which would quietly change meaning the next time that text is
        /// reworded.</summary>
        public static bool EngineWaitsOnAModel
        {
            get
            {
                try
                {
                    var config = Config;
                    if (config == null) return false;
                    var setup = VoiceEngineDiscovery.Resolve(config);
                    return !setup.IsComplete && setup.HostExePath.Length > 0 && setup.EnginePath.Length > 0;
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
                    if (setup.IsComplete) return string.Empty;
                    if (CloudVoiceClient.IsConfigured(config)) return string.Empty;

                    // Neither road. Name BOTH ways out, because "install a speech engine" is a poor
                    // answer for somebody whose machine will never run one.
                    return setup.Missing + " — or use a hosted voice instead (a key in the Voices panel)";
                }
                catch (Exception ex) { return ex.Message; }
            }
        }

        /// <summary>The three ways a reply can be made and handed over. See ModConfig.VoiceDelivery.</summary>
        internal enum Delivery { FullRead, Streaming, ByLine }

        /// <summary>Read fresh every time, never cached, so the setting takes hold on the very next
        /// line without a restart — which is the point of it being a dial rather than a constant.</summary>
        internal static Delivery DeliveryRoad()
        {
            var road = Config?.VoiceDelivery;
            if (string.Equals(road, "Streaming", StringComparison.OrdinalIgnoreCase)) return Delivery.Streaming;
            if (string.Equals(road, "ByLine", StringComparison.OrdinalIgnoreCase)) return Delivery.ByLine;
            return Delivery.FullRead;
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

                        // A HOSTED voice is never made ahead of time, and this is the whole of the
                        // cost argument: making audio on the player's own machine costs a second of
                        // a graphics card nobody was using, but making it out there costs MONEY —
                        // and a line prepared for a ▶ that is never pressed would be money spent on
                        // silence. The local road stays eager, so a cast local voice is instant.
                        if (plan.Voice.Backend == VoiceBackend.Remote) return;

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
                        if (plan == null)
                        {
                            // The commonest reason for "I pressed it and nothing happened", and it
                            // was silent until now: no voice cast for this soul, or the road that
                            // voice needs is not open.
                            ModLog.Info($"voice: nothing to speak with for {who.Id} — "
                                        + (UnavailableReason.Length > 0 ? UnavailableReason : "no voice is cast for them")
                                        + ".");
                            return;
                        }

                        var cached = VoiceCache.TryHit(plan.Voice.Id, plan.Key);
                        if (cached != null)
                        {
                            ModLog.Info($"voice: {plan.Voice.Name} speaks {cached.Count} piece(s) from the cache.");
                            foreach (var file in cached) VoicePlayback.Enqueue(generation, file);
                            return;
                        }

                        ModLog.Info($"voice: making a line for {plan.Voice.Name} "
                                    + $"({(plan.Voice.Backend == VoiceBackend.Remote ? "hosted" : "this machine")}, "
                                    + $"{plan.Text.Length} chars, moment {generation}).");

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

        /// <summary>Whether souls nobody has cast are given a voice of their own people. Read live,
        /// like every other voice setting, so turning it off takes hold on the next line.</summary>
        private static bool AutoCastOn
        {
            get
            {
                var config = Config;
                return config == null || config.VoiceAutoCast;
            }
        }

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

            /// <summary>Their people, as the game's own culture id. Used only to choose a first
            /// voice; empty is perfectly fine and simply widens the choice.</summary>
            public string Culture = string.Empty;
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
        /// <para>
        /// Culture joined them (2026.08.15) under exactly that test and no other: a hero's culture
        /// is set when they are made and does not move while a talk is running, so reading its
        /// <c>StringId</c> here is the same kind of read as <c>IsFemale</c>. It is what lets a soul
        /// be given a voice of their own people. Anything less fixed than this still belongs on the
        /// game thread.
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
                    Culture = npc.Culture?.StringId ?? string.Empty,
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
            if (!EnabledQuick) return null;

            var voice = VoiceFor(who);
            if (voice == null) return null;
            return PlanFor(voice, text);
        }

        /// <summary>The same plan, for a voice chosen directly rather than by whose it is — what the
        /// panel's "hear it" button needs.</summary>
        private static SpeechPlan? PlanFor(VoicePreset voice, string text)
        {
            if (voice == null) return null;

            // WHICH ROAD is decided by the voice itself, not by a setting: a cloned voice can only be
            // spoken by the engine that holds its embedding, and a hosted voice only by the service
            // that owns it. So a player may have both and cast either on anybody.
            if (voice.Backend == VoiceBackend.Remote)
            {
                if (!CloudVoiceClient.IsConfigured(Config))
                {
                    if (!_warnedRemote)
                    {
                        _warnedRemote = true;
                        ModLog.Warn($"voice: \"{voice.Name}\" is a hosted voice, but no key is set for one.");
                    }
                    return null;
                }
            }
            else if (!LocalReady)
            {
                return null;
            }

            if (!TryVoiceArguments(voice, out var kind, out var voicePath, out var speakerName))
                return null;

            // Whether the acted parts are read aloud is the player's call (Anton, 2026.08.15), and it
            // is ON by default: a reply that answers only with *turns away without a word* was
            // otherwise silent, which is the opposite of what a voice is for. Off, the old rule
            // stands — a voice reciting *sets down her cup* is a machine reading stage directions.
            //
            // Nothing else has to know: the cache key is built from the SPOKEN text below, so the two
            // settings key differently and flipping the toggle can never replay a clip made under the
            // other one.
            var acted = Config?.VoiceSpeakActedParts ?? true;
            var spoken = acted ? SpeakableText.SpokenWithGestures(text) : SpeakableText.SpokenOnly(text);
            if (spoken.Length == 0) return null;

            var bites = SpeakableText.BitesFor(text, includeGestures: acted);
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

        /// <summary>
        /// The one place the four tiers are applied, so the panel, the badge and the actual speaking
        /// can never disagree about who sounds like what. Call under <see cref="ShelfGate"/>.
        /// <para>
        /// The player's own slot is separate but is filled the same way when empty (Anton,
        /// 2026.08.15): their own people, their own sex, chosen from their name. The old behaviour
        /// was silence on the grounds that "most people do not want to hear themselves" — but with
        /// a ▶ on every line that is the player's own choice to make each time, and an unassigned
        /// slot just made their half of the conversation dead.
        /// </para>
        /// </summary>
        private static string ResolveVoiceId(Speaker who)
        {
            if (who.IsPlayer && !string.IsNullOrWhiteSpace(_casting.Player)) return _casting.Player;

            // Their own casting is checked inside Pick; the player has no per-soul casting, only
            // the slot above, so the sheet is not consulted for them.
            return VoiceCasting.Pick(who.IsPlayer ? null : _casting, _shelf,
                                     who.Id, who.IsFemale, who.Culture, AutoCastOn);
        }

        private static VoicePreset? VoiceFor(Speaker who)
        {
            EnsureShelf();

            lock (ShelfGate)
            {
                var id = ResolveVoiceId(who);
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
                // A hosted voice is named, not made: it carries no embedding of its own, only the
                // service's own name for it. The local engine never sees this road at all.
                kind = "remote";
                speaker = voice.RemoteVoiceId;
                return !string.IsNullOrWhiteSpace(speaker);
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

            // One of the model's OWN built-in speakers — nothing on disk, just a name the model
            // knows. Only the customvoice model carries any (nine of them); the base model that
            // does the cloning reports none, which is why these are offered by model name.
            if (!string.IsNullOrWhiteSpace(voice.SpeakerName))
            {
                kind = "speaker";
                speaker = voice.SpeakerName;
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
                    SeedShippedVoices(root);

                    var made = VoiceLibrary.Load(root, out var skipped);
                    foreach (var problem in skipped) ModLog.Warn("voice: " + problem);

                    // The player's own voices FIRST, then the hosted shelf. Order matters twice: a
                    // default is filled from the first voice of the right kind, and a player who has
                    // made a voice should never be handed a stranger's instead.
                    //
                    // The hosted ones are listed even with no key set, deliberately. They cost
                    // nothing to hold (they are made in memory, not on disk), the panel can then
                    // explain what they want, and — the real reason — a casting is never quietly
                    // forgotten because a key was removed for an evening.
                    var shelf = new List<VoicePreset>(made);
                    shelf.AddRange(BuiltInShelf());
                    shelf.AddRange(CloudVoiceClient.Shelf());
                    _shelf = shelf;

                    _casting = VoiceAssignments.Load(CastingFilePath);
                    var dropped = _casting.ForgetMissing(_shelf.Select(v => v.Id));

                    // The retired all-women / all-men slots, emptied once. An old sheet carries them
                    // filled — often from the automatic fill that used to run on a fresh shelf — and
                    // while nothing reads them any more, leaving them written is a lie about what
                    // the shelf is doing.
                    var cleared = _casting.ClearDeadDefaults();
                    if (dropped > 0 || cleared)
                    {
                        _casting.Save(CastingFilePath);
                        ModLog.Info($"voice: casting sheet tidied — {dropped} stale"
                                    + (cleared ? ", the retired all-women/all-men slots emptied" : "") + ".");
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

        /// <summary>
        /// Lays the voices that ship WITH the mod onto the player's shelf, once each.
        /// <para>
        /// Done here rather than at startup because this is the one place that is guaranteed to run
        /// before anybody reads the shelf, and it costs nothing on the many passes where there is
        /// nothing to do. Once per session is plenty: the ledger makes it idempotent anyway, but a
        /// player is not installing a mod update mid-campaign.
        /// </para>
        /// </summary>
        private static bool _seededThisSession;

        private static void SeedShippedVoices(string root)
        {
            if (_seededThisSession) return;
            _seededThisSession = true;      // set first: a throw must not make us try every few seconds

            try
            {
                var shipped = ShippedVoicesFolder();
                if (shipped.Length == 0) return;

                var report = VoiceSeeds.Seed(shipped, root);
                foreach (var problem in report.Skipped) ModLog.Warn("voice: shipped voice — " + problem);
                if (report.DidAnything)
                    ModLog.Info($"voice: {report.Added.Count} voice(s) came with the mod — {string.Join(", ", report.Added)}.");

                // Said out loud, because from the outside it looks exactly like a broken feature:
                // the voices are plainly in the module folder and plainly not on the shelf.
                if (report.AlreadyOffered.Count > 0)
                    ModLog.Info($"voice: {report.AlreadyOffered.Count} shipped voice(s) were offered once before and not laid out again "
                                + $"({string.Join(", ", report.AlreadyOffered)}) — delete {Path.Combine(root, VoiceSeeds.LedgerFileName)} to be offered them afresh.");
            }
            catch (Exception ex) { ModLog.Error("voice: laying out the voices that ship with the mod", ex); }
        }

        /// <summary>
        /// <c>…\Modules\ImmersiveAI\Voices</c> — beside GUI and ModuleData, where the packager put
        /// it. Found by walking up from our own DLL, exactly as the engine discovery does, because
        /// the folder this was built in is not the folder it runs in.
        /// </summary>
        private static string ShippedVoicesFolder()
        {
            try
            {
                var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(location)) return string.Empty;

                // …\Modules\ImmersiveAI\bin\Win64_Shipping_Client\ImmersiveAI.dll → …\ImmersiveAI
                var moduleRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(location)));
                if (string.IsNullOrEmpty(moduleRoot)) return string.Empty;

                var voices = Path.Combine(moduleRoot, "Voices");
                return Directory.Exists(voices) ? voices : string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>
        /// The speech model's OWN voices — the ones that need no download beyond the model itself.
        /// <para>
        /// THE WRINKLE THIS EXISTS FOR: they live on <c>qwen-talker-1.7b-customvoice</c> and NOT on
        /// <c>qwen-talker-1.7b-base</c>, which is the model that does the cloning and the one the
        /// setup page tells people to fetch. So a player who followed the instructions, made no
        /// voice of their own and turned voices on would find a shelf with nothing on it — a feature
        /// that appears simply not to work. Offering these when (and only when) the loaded model is
        /// one that carries them makes the shelf tell the truth about what the player has.
        /// </para>
        /// <para>
        /// The nine names are the model's own, read from it on 2026.08.14 (<c>caps.speakers == 9</c>).
        /// They were auditioned as shippable defaults and Anton's verdict was "they are very stupid
        /// all of them" — which is a fair reason not to CAST one by default, and no reason at all to
        /// hide them from somebody who has nothing else.
        /// </para>
        /// </summary>
        private static IReadOnlyList<VoicePreset> BuiltInShelf()
        {
            var shelf = new List<VoicePreset>();
            try
            {
                var model = ModelName();
                if (model.IndexOf("customvoice", StringComparison.OrdinalIgnoreCase) < 0) return shelf;

                foreach (var pair in BuiltInSpeakers)
                    shelf.Add(new VoicePreset
                    {
                        Id = "builtin-" + pair.Key,
                        Name = Pretty(pair.Key),      // the panel marks the shelf — see VoiceRowVM.NameFor
                        Backend = VoiceBackend.QwenLocal,
                        SpeakerName = pair.Key,
                        Gender = pair.Value,
                        Source = "the speech model's own voices",
                    });
            }
            catch (Exception ex) { ModLog.Error("voice: reading the model's own voices", ex); }
            return shelf;
        }

        /// <summary>The nine the customvoice model carries, with only a hint at which kind of soul
        /// they suit — a hint is all a gender ever is here.</summary>
        private static readonly List<KeyValuePair<string, VoiceGender>> BuiltInSpeakers =
            new List<KeyValuePair<string, VoiceGender>>
            {
                new KeyValuePair<string, VoiceGender>("serena", VoiceGender.Female),
                new KeyValuePair<string, VoiceGender>("vivian", VoiceGender.Female),
                new KeyValuePair<string, VoiceGender>("sohee", VoiceGender.Female),
                new KeyValuePair<string, VoiceGender>("ono_anna", VoiceGender.Female),
                new KeyValuePair<string, VoiceGender>("aiden", VoiceGender.Male),
                new KeyValuePair<string, VoiceGender>("dylan", VoiceGender.Male),
                new KeyValuePair<string, VoiceGender>("eric", VoiceGender.Male),
                new KeyValuePair<string, VoiceGender>("ryan", VoiceGender.Male),
                new KeyValuePair<string, VoiceGender>("uncle_fu", VoiceGender.Male),
            };

        private static string Pretty(string speaker)
        {
            var s = (speaker ?? string.Empty).Replace('_', ' ').Trim();
            if (s.Length == 0) return string.Empty;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
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

                // THE HOSTED ROAD, and it is short: one request, one WAV, done. No child process, no
                // model, no chunking — the whole reply arrives at once because that is what a
                // hosted service hands over. Everything downstream (the cache, the playback chain,
                // the ▶ marks) cannot tell which road made the file.
                if (plan.Voice.Backend == VoiceBackend.Remote)
                {
                    var config = Config;
                    if (config == null) return;

                    var path = VoiceCache.ChunkPath(folder, 0);
                    var spoken = await CloudVoiceClient
                        .SynthesizeAsync(config, plan.SpeakerName, plan.Text, path)
                        .ConfigureAwait(false);

                    if (!spoken.Ok) { VoiceEngineGate.ReportFailure(spoken.Error); return; }
                    if (VoiceBudget.LooksTruncated(plan.Text, spoken.Samples, spoken.Rate))
                    {
                        VoiceEngineGate.ReportFailure(
                            $"the hosted voice answered with {spoken.Samples} samples for {plan.Text.Length} characters");
                        return;
                    }

                    VoiceEngineGate.ReportSuccess();
                    if (job.Abandoned)
                        ModLog.Info("voice: the hosted line was made, but the moment had passed before it arrived.");
                    else
                        job.Publish(path);

                    VoiceCache.Seal(folder, new VoiceCacheManifest
                    {
                        Key = plan.Key,
                        VoiceId = plan.Voice.Id,
                        Model = config.CloudVoiceModel,
                        LanguageId = AutoLanguage,
                        Chunks = 1,
                        Text = plan.Text,
                    });

                    ok = true;
                    VoiceCache.PruneSoon(BudgetBytes());
                    return;
                }

                var client = EnsureClient();
                var setup = VoiceEngineDiscovery.Resolve(Config);
                if (!setup.IsComplete) { VoiceEngineGate.ReportDown(setup.Missing); return; }

                // THREE ROADS, and which is best depends on how busy the graphics card is. All
                // three end with the same thing on disk — 000.wav, 001.wav … in this folder — so a
                // replay from cache is identical whichever made it.
                var road = DeliveryRoad();
                var made = 0;

                // Set true when the host tells us it cut a reading short for running away. The clip
                // is still PLAYED — everything before the runaway is good speech — but it must never
                // be sealed into the cache, or one bad synthesis is replayed every time that line is
                // scrolled back to, for the rest of the campaign.
                var derailed = false;

                if (road == Delivery.ByLine)
                {
                    // The oldest road, kept for comparison. A separate generation per sentence: it
                    // begins quickly even on a loaded card, but the model rolls its prosody afresh
                    // each time, so the voice changes person at every seam. The host continues its
                    // numbering from the file we name, which is what keeps the sentences from
                    // overwriting one another.
                    foreach (var bite in plan.Bites)
                    {
                        if (job.Abandoned) return;

                        var atIndex = made;
                        var reply = await client.SynthesizeAsync(
                            id: plan.Key + "#" + atIndex.ToString(),
                            text: bite,
                            outPath: VoiceCache.ChunkPath(folder, atIndex),
                            voiceKind: plan.Kind,
                            voicePath: plan.VoicePath,
                            speaker: plan.SpeakerName,
                            languageId: AutoLanguage,
                            setup: setup,
                            maxTokens: VoiceBudget.MaxTokensFor(bite),
                            onChunk: (_, path, __) =>
                            {
                                if (job.Abandoned) return;
                                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
                                made++;
                                job.Publish(path);
                            }).ConfigureAwait(false);

                        if (!reply.Ok) { VoiceEngineGate.ReportFailure(reply.Error); return; }
                        if (reply.Derailed) derailed = true;
                        if (VoiceBudget.LooksTruncated(bite, reply.Samples, reply.Rate))
                        {
                            VoiceEngineGate.ReportFailure(
                                $"the engine answered with {reply.Samples} samples for {bite.Length} characters");
                            return;
                        }
                        VoiceEngineGate.ReportSuccess();
                    }
                }
                else
                {
                    // ONE generation for the whole reply. This is what keeps the voice the same
                    // person from first word to last — the model's own state carries across the
                    // sentences instead of being thrown away between them — and it is far faster
                    // besides, because the prefill and the speaker encode are paid once rather than
                    // per sentence (3.60x realtime against 0.34x, same card, game running).
                    //
                    // The two differ only in WHEN the pieces are handed to playback:
                    //   Streaming — as each lands, so she begins speaking in under a second, and a
                    //               card too busy to keep ahead leaves audible gaps.
                    //   FullRead  — not until every piece exists, so there can be no gap at all,
                    //               paid for by a longer wait before the first word.
                    // FullRead asks the host to JOIN it: one file, one sound, no chaining.
                    //
                    // Holding the pieces back and releasing them together was not enough, and this
                    // is the correction (playtest, 2026.08.14 — "she is still streaming"). Playing N
                    // files means starting each one from the game's tick after the last reports it
                    // has finished, so there is a frame of silence inside every second of speech —
                    // heard as the voice breaking up, identically whether the pieces arrived early
                    // or late. The Qwen GUI does not do this because its non-streaming mode hands
                    // over a single file, which is exactly what we now ask for.
                    var joined = road == Delivery.FullRead;

                    var reply = await client.SynthesizeAsync(
                        id: plan.Key,
                        text: plan.Text,
                        outPath: VoiceCache.ChunkPath(folder, 0),
                        voiceKind: plan.Kind,
                        voicePath: plan.VoicePath,
                        speaker: plan.SpeakerName,
                        languageId: AutoLanguage,
                        setup: setup,
                        whole: joined,
                        maxTokens: VoiceBudget.MaxTokensFor(plan.Text),
                        onChunk: (index, path, samples) =>
                        {
                            // Streaming: a piece per second. Joined: this fires ONCE, at the end,
                            // naming the single finished file.
                            if (job.Abandoned) return;
                            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
                            made++;
                            job.Publish(path);
                        }).ConfigureAwait(false);

                    if (!reply.Ok) { VoiceEngineGate.ReportFailure(reply.Error); return; }
                    if (reply.Derailed) derailed = true;
                    if (made == 0)
                    {
                        VoiceEngineGate.ReportFailure("the engine reported a reply it did not write");
                        return;
                    }
                    if (VoiceBudget.LooksTruncated(plan.Text, reply.Samples, reply.Rate))
                    {
                        VoiceEngineGate.ReportFailure(
                            $"the engine answered with {reply.Samples} samples for {plan.Text.Length} characters");
                        return;
                    }
                    VoiceEngineGate.ReportSuccess();

                }

                if (made == 0) return;

                // A reading that ran away is heard once and never again. ok stays false, so the
                // folder is left unsealed and the next attempt at these words starts clean.
                if (derailed)
                {
                    ModLog.Warn("voice: a reading ran away and was cut short — it will not be kept.");
                    return;
                }

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

        // ------------------------------------------------------------------
        // What the voice panel needs — casting, hearing, and bringing voices over
        // ------------------------------------------------------------------

        /// <summary>Every voice that could speak: the player's own folders first, then the hosted
        /// shelf. Fresh from disk when the folder has changed since it was last read.</summary>
        public static IReadOnlyList<VoicePreset> Shelf()
        {
            EnsureShelf();
            lock (ShelfGate) return _shelf;
        }

        /// <summary>The voice a soul actually speaks with right now, or empty for silence.</summary>
        public static string VoiceIdFor(Hero? npc)
        {
            var who = Describe(npc);
            if (who == null) return string.Empty;

            EnsureShelf();
            lock (ShelfGate) return ResolveVoiceId(who);
        }

        /// <summary>Which of the four tiers actually answered for a soul. See <see cref="OriginFor"/>.</summary>
        public enum VoiceOrigin { None, Cast, TheirPeople }

        /// <summary>
        /// WHY this soul speaks with the voice they do — not merely which voice it is.
        /// <para>
        /// Worth its own call because the panel was saying "of their people" for a voice that came
        /// out of the all-men slot, and that one wrong word hid a real problem for a whole session
        /// (Anton, 2026.08.15: every Khuzait speaking with Max, while the label cheerfully claimed
        /// they had been given a voice of their own people). A label that guesses at its own
        /// reasoning is worse than no label at all.
        /// </para>
        /// </summary>
        public static VoiceOrigin OriginFor(Hero? npc)
        {
            var who = Describe(npc);
            if (who == null) return VoiceOrigin.None;

            EnsureShelf();
            lock (ShelfGate)
            {
                if (who.IsPlayer)
                    return !string.IsNullOrWhiteSpace(_casting.Player) ? VoiceOrigin.Cast
                         : string.IsNullOrWhiteSpace(ResolveVoiceId(who)) ? VoiceOrigin.None
                         : VoiceOrigin.TheirPeople;

                if (_casting.IsCast(who.Id)) return VoiceOrigin.Cast;
                return string.IsNullOrWhiteSpace(ResolveVoiceId(who)) ? VoiceOrigin.None : VoiceOrigin.TheirPeople;
            }
        }

        /// <summary>Whether this soul was cast BY HAND, rather than falling to their kind's default —
        /// the panel says which, so "chosen" can be told from "whatever women get".</summary>
        public static bool IsCastByHand(Hero? npc)
        {
            if (npc == null) return false;
            EnsureShelf();
            lock (ShelfGate) return _casting.IsCast(npc.StringId);
        }

        /// <summary>The two defaults and the player's own slot, for the panel and for MCM.</summary>
        public static string PlayerVoiceId { get { EnsureShelf(); lock (ShelfGate) return _casting.Player ?? string.Empty; } }

        /// <summary>Casts a soul. An empty voice id clears the casting and drops them back to their
        /// kind's default — that is how "use the usual voice again" is said.</summary>
        public static void Cast(Hero? npc, string? voiceId)
        {
            if (npc == null) return;
            Change(sheet => sheet.Cast(npc.StringId, voiceId));
        }


        /// <summary>The player's own voice for their own lines. Empty is the honest default: most
        /// people do not want to hear themselves read their own words back.</summary>
        public static void SetPlayerVoice(string? voiceId) => Change(sheet => sheet.Player = (voiceId ?? string.Empty).Trim());

        private static void Change(Action<VoiceAssignments> edit)
        {
            try
            {
                EnsureShelf();
                lock (ShelfGate)
                {
                    edit(_casting);
                    _casting.Save(CastingFilePath);
                    // Taken after the save, or the sheet we just wrote reads as somebody else's edit
                    // and the shelf is needlessly re-read on the next line.
                    _shelfStampUtc = ShelfStamp();
                }
            }
            catch (Exception ex) { ModLog.Error("voice: writing the casting sheet", ex); }
        }

        /// <summary>What a voice says when the player asks to hear it. Deliberately in the world's
        /// own words and not "testing, one two three" — the point is to judge whether this is the
        /// person, and a line of Calradia is the only fair test of that.</summary>
        public const string PreviewLine =
            "We have ridden a long way together, you and I. Whatever comes of tomorrow, I am glad it is you beside me.";

        /// <summary>Speaks a sample in one voice, whoever it belongs to. Cuts off whatever was being
        /// said, exactly as a new line does.</summary>
        public static void Preview(VoicePreset? voice, string? line = null)
        {
            try
            {
                if (voice == null || !EnabledQuick) return;
                var text = string.IsNullOrWhiteSpace(line) ? PreviewLine : line!.Trim();

                var generation = Interlocked.Increment(ref _generation);
                VoicePlayback.Begin(generation);

                Task.Run(() =>
                {
                    var opened = true;
                    try
                    {
                        var plan = PlanFor(voice, text);
                        if (plan == null)
                        {
                            ModLog.Info($"voice: {voice.Name} cannot speak — "
                                        + (UnavailableReason.Length > 0 ? UnavailableReason : "its road is not open") + ".");
                            return;
                        }

                        var cached = VoiceCache.TryHit(plan.Voice.Id, plan.Key);
                        if (cached != null)
                        {
                            ModLog.Info($"voice: hearing {plan.Voice.Name} from the cache.");
                            foreach (var file in cached) VoicePlayback.Enqueue(generation, file);
                            return;
                        }

                        ModLog.Info($"voice: hearing {plan.Voice.Name} "
                                    + $"({(plan.Voice.Backend == VoiceBackend.Remote ? "hosted" : "this machine")}, moment {generation}).");

                        var job = StartJob(plan, pinned: false, playbackGeneration: generation);
                        opened = false;
                        job.Subscribe(
                            path => VoicePlayback.Enqueue(generation, path),
                            _ => VoicePlayback.Complete(generation));
                    }
                    catch (Exception ex) { ModLog.Error("voice: hearing a voice", ex); }
                    finally { if (opened) VoicePlayback.Complete(generation); }
                });
            }
            catch (Exception ex) { ModLog.Error("voice: hearing a voice", ex); }
        }

        /// <summary>
        /// Brings over every voice made in Qwen-TTS Studio that is not on the shelf already, matched
        /// by NAME — so the button can be pressed again after making another one and only the new
        /// ones arrive. Copies, never moves: Studio goes on working exactly as it did.
        /// <para>
        /// Numbered retakes are kept rather than dropped ("Sibylla", "Sibylla 2" … "Sibylla 5" are
        /// five attempts at one person and the whole point is to hear them against each other). The
        /// panel is where an unwanted one is deleted.
        /// </para>
        /// </summary>
        public static int ImportFromStudio(out string trouble)
        {
            trouble = string.Empty;
            try
            {
                var studioFolder = VoiceLibrary.DefaultStudioDataFolder();
                var tsv = Path.Combine(studioFolder, "voice-presets.tsv");
                if (!File.Exists(tsv))
                {
                    trouble = "no voices found — Qwen-TTS Studio keeps them in " + studioFolder;
                    return 0;
                }

                var found = VoiceLibrary.ReadStudioPresets(tsv);
                if (found.Count == 0) { trouble = "Studio's list of voices is empty."; return 0; }

                var root = VoicesRoot;
                Directory.CreateDirectory(root);

                var already = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                foreach (var voice in VoiceLibrary.Load(root)) already.Add(voice.Name.Trim());

                var brought = 0;
                foreach (var studio in found)
                {
                    if (already.Contains(studio.Name.Trim())) continue;
                    try
                    {
                        VoiceLibrary.ImportFromStudio(studio, root);
                        brought++;
                    }
                    catch (Exception ex)
                    {
                        ModLog.Warn($"voice: could not bring over \"{studio.Name}\" — {ex.Message}");
                    }
                }

                if (brought > 0) Rediscover();
                return brought;
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: bringing voices over from Studio", ex);
                trouble = ex.Message;
                return 0;
            }
        }

        /// <summary>Opens the voices folder in Explorer — the one place a voice can be dropped in,
        /// zipped up, or handed to a friend.</summary>
        public static void OpenVoicesFolder()
        {
            try
            {
                Directory.CreateDirectory(VoicesRoot);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = VoicesRoot,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex) { ModLog.Error("voice: opening the voices folder", ex); }
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
