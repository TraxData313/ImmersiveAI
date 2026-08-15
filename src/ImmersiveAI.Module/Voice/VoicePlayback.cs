using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ImmersiveAI.Core.Voices;

// NOTE: no "using TaleWorlds.Engine;" in this file, and there must never be one — TaleWorlds.Engine
// carries its own Path type, which would shadow System.IO.Path in a file that reads WAV headers off
// the disk. SoundEvent is fully qualified below for exactly that reason.

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// Puts a made line into the air, piece after piece, through the game's own audio engine.
    /// <para>
    /// Through the ENGINE and not a player of our own, deliberately: an event on the game's
    /// voice-over bus obeys the player's own volume slider, mutes on alt-tab, and ducks under the
    /// music the way vanilla speech does. Everything that makes voices feel like part of the game
    /// rather than a program shouting over it comes free with that one decision.
    /// </para>
    /// <para>
    /// THE ENGINE IS TOUCHED FROM THE GAME THREAD AND NOWHERE ELSE. Anything may enqueue a piece
    /// from any thread — the queue is its own — but creating, playing and releasing a sound event
    /// happens only inside <see cref="Tick"/>, which runs on the game's own tick. This is why
    /// <see cref="StopAll"/> asks for silence rather than taking it: the stop lands on the very next
    /// drain, a frame away at worst, and never from a background thread.
    /// </para>
    /// <para>
    /// TWO TRAPS, both read out of the engine's own decompiled source. <c>Stop()</c> sets the sound
    /// id to -1, which makes a following <c>Release()</c> a silent no-op and leaks the event — so we
    /// only ever call <c>Release()</c>, which stops it itself. And a freshly created event reports
    /// <c>IsStopped()</c> true before it has begun, so "it is finished" is never believed until we
    /// have either seen it playing or given it a moment to start.
    /// </para>
    ///
    /// <para>
    /// ═══ THE SEAM, AND THE THREE THINGS THAT CLOSE IT (2026.08.15) ═══
    /// </para>
    /// <para>
    /// A streamed reply arrives as a second of audio at a time, and playing it as N separate sounds
    /// put a frame of silence inside every second of speech — heard as the voice breaking up. That
    /// was the whole reason Full read existed. Three changes together remove it, and none of them
    /// needs the engine to be touched off the game thread:
    /// </para>
    /// <list type="number">
    /// <item><b>Pieces are POURED TOGETHER before playing.</b> By the time the first second has been
    /// heard, several more have arrived — measured 2026.08.15, the engine makes audio about 2.5×
    /// faster than it plays, first piece in 427 ms — so whatever is waiting is joined into one file
    /// and played as one sound (<see cref="WavFiles.Join"/>). A fourteen-piece reply becomes three or
    /// four sounds. Most seams simply stop existing.</item>
    /// <item><b>The next sound is BUILT WHILE THE CURRENT ONE PLAYS.</b> Creating a sound event reads
    /// a file off the disk; doing that at the boundary is the boundary. Prepared a tick early, the
    /// handover is one <c>Play()</c> call.</item>
    /// <item><b>The handover is scheduled BY THE CLOCK, not by asking.</b> A WAV's own header says
    /// exactly how long it plays, so we know to the millisecond when it ends — where polling
    /// <c>IsPlaying()</c> only tells us afterwards, a frame or more late. The remaining error is one
    /// frame of quantisation, and a small lead swallows it.</item>
    /// </list>
    /// <para>
    /// What is left is a handover inside a single frame, which nobody can hear.
    /// </para>
    /// </summary>
    public static class VoicePlayback
    {
        /// <summary>How long a just-started event may claim to be stopped before we believe it.</summary>
        private static readonly TimeSpan StartGrace = TimeSpan.FromMilliseconds(500);

        /// <summary>Slack over a piece's own length before we stop waiting for it. A wedged event
        /// must never hold the rest of a reply silent forever.</summary>
        private static readonly TimeSpan LengthSlack = TimeSpan.FromSeconds(3);

        /// <summary>The ceiling for a piece whose length we could not read.</summary>
        private static readonly TimeSpan UnknownLengthCeiling = TimeSpan.FromSeconds(90);

        /// <summary>
        /// How early the next piece is started. One frame at sixty is 17 ms, and we are only looked
        /// at once a frame — so aiming a hair EARLY means the error lands as a few milliseconds of
        /// overlap rather than a few of silence. Overlap between two consecutive pieces of one
        /// reading is inaudible; silence in the middle of a word is the thing being fixed.
        /// </summary>
        private static readonly TimeSpan HandoverLead = TimeSpan.FromMilliseconds(12);

        /// <summary>Most pieces to pour into one sound. Only a bound against something pathological —
        /// a whole reply is a dozen or so.</summary>
        private const int MaxJoinPieces = 40;

        /// <summary>How often the chain is looked at while something is speaking. A safety net only:
        /// <c>SubModule.OnApplicationTick</c> already drains us every frame, which is what gives the
        /// handover its precision. This keeps the chain moving if frames ever stop coming.</summary>
        private const int DriverIntervalMs = 20;

        /// <summary>
        /// The FMOD programmer-event name our audio is handed to. Configurable ON PURPOSE, because
        /// which event a runtime WAV should ride is not something reading the game settles.
        /// <para>
        /// SETTLED 2026.08.15, and it corrects what was written here the day before. The game DOES
        /// define <c>event:/Extra/voiceover</c> — it is in its own event table
        /// (<c>Modules\Native\ModuleData\sound_event_data.gen.xml</c>, guid
        /// <c>{2a2e4e13-d391-41bd-bf9e-91891d2c63f4}</c>), sitting beside <c>event:/Extra/external</c>
        /// and <c>event:/Extra/voicechat</c>. It is a real programmer-sound event meant for exactly
        /// this: audio the game did not ship. So we are riding the game's own routing for it, and
        /// the earlier worry that FMOD was merely tolerating an invented name was wrong.
        /// </para>
        /// <para>
        /// Which means the first playtest's "veeeery quietly, can barely hear her" had ONE cause,
        /// not two: the engine's own output is 10-20 dB under a speaking level (peaks measured
        /// between 0.077 and 0.299). The host normalises that now, and that is the whole fix.
        /// </para>
        /// <para>
        /// If it ever does need moving, the honest alternatives in the same table are
        /// <c>event:/Extra/external</c> (the generic one, and it allows a longer sound) and
        /// <c>event:/Extra/voicechat</c>. Change <c>VoiceSoundEvent</c> in config.json and speak one
        /// line; no rebuild, and no restart of anything but the game.
        /// </para>
        /// </summary>
        public static string SoundEventName { get; set; } = "event:/Extra/voiceover";

        /// <summary>One thing to play, and every cache folder that must be held until it has been.</summary>
        private sealed class Sounding
        {
            public string Path = string.Empty;

            /// <summary>Folders whose in-use hold this piece carries — several when it was poured
            /// together out of several. Given back the moment it stops being needed.</summary>
            public readonly List<string> Holds = new List<string>();

            /// <summary>True when Path is a file WE made by pouring; it is ours to delete.</summary>
            public bool IsJoined;

            /// <summary>
            /// The sequence this piece belongs to. Carried on the PIECE and not only on the queue
            /// because pouring happens on a background thread: a pour started for one line can land
            /// after the player has said the next one, and without this it would be picked up and
            /// played into the new moment — the very thing the generation counter exists to stop.
            /// </summary>
            public long Generation;

            public TimeSpan Length;
            public TaleWorlds.Engine.SoundEvent? Sound;
        }

        private static readonly object Gate = new object();
        private static readonly Queue<Sounding> Queued = new Queue<Sounding>();

        private static Sounding? _current;
        private static Sounding? _next;               // event built and ready to Play()
        private static Sounding? _poured;             // pieces already joined, awaiting their event
        private static bool _pouring;                 // a background pour is under way
        private static DateTime _startedUtc;
        private static DateTime _endsUtc;             // when the current piece's own audio runs out
        private static DateTime _deadlineUtc;         // when we stop believing it is still playing
        private static bool _observedPlaying;

        private static long _generation;
        private static bool _active;
        private static bool _complete;
        private static bool _stopRequested;
        private static bool _dropCurrent;
        private static bool _driverRunning;
        private static Action? _onFinished;

        /// <summary>True while a line is being spoken or is waiting its turn.</summary>
        public static bool IsSpeaking
        {
            get { lock (Gate) return _active || _current != null; }
        }

        /// <summary>The sequence being spoken. A piece offered under an older one is discarded.</summary>
        public static long CurrentGeneration
        {
            get { lock (Gate) return _generation; }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Opens a new sequence, cutting off whatever was being said. <paramref name="generation"/>
        /// is the caller's own counter: every later call carries it, so audio made for a moment the
        /// player has already walked away from is dropped instead of played over the next line.
        /// </summary>
        public static void Begin(long generation, Action? onFinished = null)
        {
            lock (Gate)
            {
                DropQueuedInside();                 // the old sequence's waiting pieces
                // The one in the air AND anything already got ready go on the next tick, never from
                // here: this may be a background thread, and a sound event is the game thread's
                // alone. Tick's own `dropping` branch releases both.
                _dropCurrent = true;
                _stopRequested = false;
                _generation = generation;
                _active = true;
                _complete = false;
                // The old sequence was cut short, not finished — its callback is dropped unfired.
                _onFinished = onFinished;
            }

            MainThreadDispatcher.Enqueue(Tick);
            Kick();
        }

        /// <summary>Adds a made piece to the end of the current sequence. Safe from any thread.</summary>
        public static void Enqueue(long generation, string wavPath)
        {
            if (string.IsNullOrEmpty(wavPath)) return;

            lock (Gate)
            {
                if (generation != _generation || !_active)
                {
                    // THE SILENT DROP, and it is worth a line in the log. Audio arriving for a
                    // moment the player has walked away from is meant to be discarded — but when
                    // somebody reports "it made the sound and I heard nothing", this is the first
                    // thing that needs ruling in or out, and without a line here it is invisible.
                    ModLog.Info($"voice: a piece arrived for a moment that had passed "
                                + $"(made for {generation}, now {_generation}, speaking={_active}) — dropped.");
                    return;
                }

                var folder = FolderOf(wavPath);
                var piece = new Sounding { Path = wavPath, Generation = generation };
                piece.Holds.Add(folder);
                Queued.Enqueue(piece);

                // Held from the instant it is promised until the instant it has been heard, so the
                // pruner can never sweep the folder out from under a sentence in progress.
                VoiceCache.MarkInUse(folder);
            }
            Kick();
        }

        /// <summary>No more pieces are coming for this sequence. It ends when the last one has played.</summary>
        public static void Complete(long generation)
        {
            lock (Gate)
            {
                if (generation != _generation) return;
                _complete = true;
            }
            Kick();
        }

        /// <summary>Barge-in: silence, at once, and forget what was waiting.</summary>
        public static void StopAll()
        {
            lock (Gate)
            {
                if (!_active && _current == null && _next == null && Queued.Count == 0) return;
                _stopRequested = true;
                DropQueuedInside();
            }
            // Land it on the very next drain rather than touching the engine from here — the caller
            // may be a background thread, and this is the one thing that must not be.
            MainThreadDispatcher.Enqueue(Tick);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// The chain, one step. Runs on the game thread — from <c>SubModule.OnApplicationTick</c>
        /// every frame, and harmlessly again from the dispatcher's own drain: the work is idempotent
        /// and both callers are the same thread.
        /// </summary>
        public static void Tick()
        {
            try
            {
                bool stopping, dropping;
                lock (Gate)
                {
                    stopping = _stopRequested;
                    dropping = _dropCurrent;
                    _dropCurrent = false;
                }

                if (stopping) { Teardown(fireCallback: false); return; }
                if (dropping) { ReleaseCurrent(); ReleasePrepared(); }

                if (_current != null)
                {
                    // Keep the next one warming, always: pouring happens off this thread and making
                    // its sound event happens on it, so both want a head start.
                    PrepareNext();

                    // The handover, and it is by the CLOCK: the file's own header said how long it
                    // would play, so we know when it ends without asking the engine — which only
                    // ever answers a frame or more after the fact, and that answer is the seam.
                    if (DateTime.UtcNow + HandoverLead >= _endsUtc && _next != null)
                    {
                        HandOver();
                        return;
                    }

                    // Not due, or nothing ready to hand over TO. The engine's own judgement is the
                    // backstop here — for a piece whose length we could not read, and for an event
                    // that has wedged.
                    if (!HasFinished(_current)) return;
                    ReleaseCurrent();
                }

                if (_next != null) { HandOver(); return; }
                StartFromQueue();
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: playing a line", ex);
                try { Teardown(fireCallback: false); } catch { }
            }
        }

        /// <summary>
        /// Nothing is in the air: begin with whatever is there. The FIRST piece is deliberately
        /// played as it stands rather than waiting for anything to be poured — that first second is
        /// the whole latency story (measured 427 ms from asking to audio), and it would be a poor
        /// trade to spend it tidying up the pieces behind it.
        /// </summary>
        private static void StartFromQueue()
        {
            Sounding? piece = null;
            var finished = false;

            var stranded = false;
            lock (Gate)
            {
                if (!_active)
                {
                    // A pour that landed after the sequence ended: nobody will ever play it, so give
                    // its holds back and delete its file. Without this the cache pruner is quietly
                    // blocked on those folders for the rest of the session.
                    piece = _poured;
                    _poured = null;
                    stranded = true;
                }
            }

            // Outside the lock on purpose: discarding touches the cache's own bookkeeping and the
            // disk, and this file holds one lock only ever for the length of a field assignment.
            if (stranded) { Discard(piece); return; }

            lock (Gate)
            {
                if (_poured != null) { piece = _poured; _poured = null; }
                else if (Queued.Count > 0) piece = Queued.Dequeue();
                else if (_complete && !_pouring) finished = true;
                else return;                            // waiting on the engine to make more

                // Belt to the pour's braces: anything from a line the player has already moved past
                // is given back rather than played into this one.
                if (piece != null && piece.Generation != 0 && piece.Generation != _generation)
                {
                    stranded = true;
                }
            }

            if (stranded) { Discard(piece); return; }
            if (finished) { Teardown(fireCallback: true); return; }
            if (piece == null) return;

            if (piece.Length <= TimeSpan.Zero) piece.Length = LengthOf(piece.Path);
            StartPiece(piece);
        }

        /// <summary>The prepared piece becomes the one playing, in a single call — which is the whole
        /// point of having prepared it.</summary>
        private static void HandOver()
        {
            Sounding? next;
            lock (Gate) { next = _next; _next = null; }
            if (next?.Sound == null) { ReleaseCurrent(); return; }

            var started = false;
            try { started = next.Sound.Play(); }
            catch (Exception ex) { ModLog.Error("voice: starting the next piece", ex); }

            // The old one goes only AFTER the new one is in the air. Released before, there would be
            // a hole exactly where this whole arrangement exists to have none.
            ReleaseCurrent();

            if (!started)
            {
                ModLog.Warn("voice: the audio engine would not play " + Path.GetFileName(next.Path) + ".");
                Discard(next);
                return;
            }

            lock (Gate)
            {
                _current = next;
                _startedUtc = DateTime.UtcNow;
                _endsUtc = _startedUtc + next.Length;
                _deadlineUtc = _startedUtc + next.Length + LengthSlack;
                _observedPlaying = false;
            }

            // The seam, in the log. "handed over" means the next sound was ready and started in the
            // same breath the last one ended — which is the whole of the gapless story. A reply that
            // shows one "playing" and then several "handed over" lines is behaving; one that shows
            // several "playing" lines is starting each piece cold, and the pouring is not keeping up.
            ModLog.Info($"voice: handed over to {Path.GetFileName(next.Path)} ({next.Length.TotalSeconds:0.0}s)"
                        + (next.IsJoined ? " — poured from several pieces" : string.Empty));
            Kick();
        }

        /// <summary>
        /// Gets the next thing to play ready while the current one is still speaking. Two steps, and
        /// they are split because only one of them may happen on this thread: the POURING is file
        /// work and goes to a background task, the sound EVENT must be made here.
        /// </summary>
        private static void PrepareNext()
        {
            Sounding? ready = null;
            lock (Gate)
            {
                if (_next != null) return;
                if (_poured != null) { ready = _poured; _poured = null; }
            }

            if (ready == null) { BeginPour(); return; }

            if (ready.Length <= TimeSpan.Zero) ready.Length = LengthOf(ready.Path);

            var sound = Create(ready.Path);
            if (sound == null) { Discard(ready); return; }

            ready.Sound = sound;
            lock (Gate) _next = ready;
        }

        /// <summary>
        /// Takes everything currently waiting and pours it into ONE file, off the game thread. The
        /// pieces are consecutive audio from a single reading, so joining them removes their seams
        /// entirely rather than merely hiding them — and doing it here rather than in
        /// <see cref="Tick"/> keeps a megabyte of file copying out of a frame.
        /// </summary>
        private static void BeginPour()
        {
            var taken = new List<Sounding>();
            lock (Gate)
            {
                if (_pouring || _poured != null || _next != null) return;
                while (Queued.Count > 0 && taken.Count < MaxJoinPieces) taken.Add(Queued.Dequeue());
                if (taken.Count == 0) return;
                _pouring = true;
            }

            // One piece is already one sound; nothing to pour, and no reason to wait a tick for it.
            if (taken.Count == 1)
            {
                taken[0].Length = LengthOf(taken[0].Path);
                lock (Gate) { _poured = taken[0]; _pouring = false; }
                return;
            }

            Task.Run(() =>
            {
                Sounding? result = null;
                var leftover = new List<Sounding>();
                try
                {
                    var paths = new List<string>(taken.Count);
                    foreach (var piece in taken) paths.Add(piece.Path);

                    var joinedPath = JoinedPathFor();
                    if (joinedPath.Length > 0 && WavFiles.Join(paths, joinedPath))
                    {
                        var joined = new Sounding
                        {
                            Path = joinedPath,
                            IsJoined = true,
                            Generation = taken[0].Generation,
                        };
                        foreach (var piece in taken) joined.Holds.AddRange(piece.Holds);
                        joined.Length = LengthOf(joinedPath);
                        result = joined;
                    }
                    else
                    {
                        // Could not pour them together (a piece went missing, a format we do not
                        // understand): play the first and put the rest back at the FRONT, in order.
                        // A seam is a shrug; a reply with its middle missing is not.
                        result = taken[0];
                        result.Length = LengthOf(result.Path);
                        for (var i = 1; i < taken.Count; i++) leftover.Add(taken[i]);
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Error("voice: pouring the pieces together", ex);
                    result = taken[0];
                    for (var i = 1; i < taken.Count; i++) leftover.Add(taken[i]);
                }
                finally
                {
                    var stale = new List<Sounding>();
                    lock (Gate)
                    {
                        // THE MOMENT MAY HAVE PASSED while this was pouring — the player said the
                        // next thing, or walked away. Then none of it belongs to now: it must not be
                        // put back in a queue that is serving a different line, and it certainly
                        // must not be played.
                        var current = result != null && result.Generation == _generation && _active;
                        if (!current)
                        {
                            if (result != null) stale.Add(result);
                            stale.AddRange(leftover);
                        }
                        else if (leftover.Count > 0)
                        {
                            var behind = new List<Sounding>(Queued);
                            Queued.Clear();
                            foreach (var piece in leftover) Queued.Enqueue(piece);
                            foreach (var piece in behind) Queued.Enqueue(piece);
                        }

                        _poured = current ? result : null;
                        _pouring = false;
                    }

                    // Outside the lock: giving a piece back touches the cache's bookkeeping and the
                    // disk, and nothing here holds a lock across either.
                    foreach (var piece in stale) Discard(piece);
                    MainThreadDispatcher.Enqueue(Tick);
                }
            });
        }

        private static void StartPiece(Sounding piece)
        {
            var sound = Create(piece.Path);
            if (sound == null) { Discard(piece); return; }

            var started = false;
            try { started = sound.Play(); }
            catch (Exception ex) { ModLog.Error("voice: starting a spoken piece", ex); }

            if (!started)
            {
                ModLog.Warn("voice: the audio engine would not play " + Path.GetFileName(piece.Path) + ".");
                try { sound.Release(); } catch { }
                Discard(piece);
                return;
            }

            piece.Sound = sound;
            lock (Gate)
            {
                _current = piece;
                _startedUtc = DateTime.UtcNow;
                _endsUtc = _startedUtc + piece.Length;
                _deadlineUtc = _startedUtc + piece.Length + LengthSlack;
                _observedPlaying = false;
            }

            // THE ONE LINE THAT SETTLES "it made the sound and I heard nothing". If this appears,
            // the audio engine took the file and said it was playing, and the fault is past us —
            // the event, the bus, a volume. If it does NOT appear, nothing ever reached playback and
            // the fault is upstream. Once per piece, and pieces are poured together, so it is a
            // handful of lines per reply.
            ModLog.Info($"voice: playing {Path.GetFileName(piece.Path)} "
                        + $"({piece.Length.TotalSeconds:0.0}s, event {SoundEventName})");
            Kick();
        }

        private static TaleWorlds.Engine.SoundEvent? Create(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    ModLog.Warn("voice: a spoken piece went missing before it could be played.");
                    return null;
                }

                var sound = TaleWorlds.Engine.SoundEvent.CreateEventFromExternalFile(
                    SoundEventName, path, scene: null, is3d: false, isBlocking: false);

                if (sound == null || sound.IsNullSoundEvent())
                {
                    ModLog.Warn("voice: the audio engine would not take " + Path.GetFileName(path) + ".");
                    return null;
                }
                return sound;
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: making a sound event", ex);
                return null;
            }
        }

        private static TimeSpan LengthOf(string path)
        {
            try { return WavFiles.TryReadDuration(path) ?? UnknownLengthCeiling; }
            catch { return UnknownLengthCeiling; }
        }

        private static bool HasFinished(Sounding piece)
        {
            var sound = piece.Sound;
            if (sound == null) return true;
            try
            {
                if (!sound.IsValid) return true;
                if (DateTime.UtcNow > _deadlineUtc) return true;     // wedged; never hold the rest hostage

                if (sound.IsPlaying()) { _observedPlaying = true; return false; }
                if (sound.IsPaused()) return false;                  // the game is paused, not the line over

                if (!sound.IsStopped()) return false;

                // Stopped, but a brand-new event says that too before it has begun.
                return _observedPlaying || DateTime.UtcNow - _startedUtc > StartGrace;
            }
            catch
            {
                return true;   // an engine that will not answer about an event is done with it
            }
        }

        private static void ReleaseCurrent()
        {
            Sounding? piece;
            lock (Gate) { piece = _current; _current = null; }
            Discard(piece);
        }

        /// <summary>Lets go of everything being got ready — the built event and the poured file
        /// behind it. A pour still running in the background is left to finish and lands in
        /// <c>_poured</c>, where the next sequence's own Begin sweeps it.</summary>
        private static void ReleasePrepared()
        {
            Sounding? built;
            Sounding? poured;
            lock (Gate)
            {
                built = _next; _next = null;
                poured = _poured; _poured = null;
            }
            Discard(built);
            Discard(poured);
        }

        /// <summary>Lets go of one piece entirely: its sound event, its cache holds, and the poured
        /// file if we were the ones who made it.</summary>
        private static void Discard(Sounding? piece)
        {
            if (piece == null) return;

            if (piece.Sound != null)
            {
                // Release, never Stop-then-Release: Stop() clears the id and Release() would then do
                // nothing at all, leaking the event. Release stops it on its own.
                try { piece.Sound.Release(); } catch { /* already gone */ }
                piece.Sound = null;
            }

            foreach (var folder in piece.Holds)
                if (folder.Length > 0) VoiceCache.ReleaseInUse(folder);
            piece.Holds.Clear();

            if (piece.IsJoined && piece.Path.Length > 0)
                try { if (File.Exists(piece.Path)) File.Delete(piece.Path); } catch { /* swept later */ }
        }

        private static void Teardown(bool fireCallback)
        {
            ReleaseCurrent();
            ReleasePrepared();

            Action? finished;
            lock (Gate)
            {
                DropQueuedInside();
                _stopRequested = false;
                _dropCurrent = false;
                _active = false;
                _complete = false;
                finished = fireCallback ? _onFinished : null;
                _onFinished = null;
            }

            if (finished == null) return;
            try { finished(); } catch (Exception ex) { ModLog.Error("voice: after the last word", ex); }
        }

        /// <summary>Empties the queue, giving back one in-use hold per piece that will now never be
        /// played. Call inside the lock.</summary>
        private static void DropQueuedInside()
        {
            while (Queued.Count > 0)
            {
                var piece = Queued.Dequeue();
                foreach (var folder in piece.Holds)
                    if (folder.Length > 0) VoiceCache.ReleaseInUse(folder);
                if (piece.IsJoined && piece.Path.Length > 0)
                    try { if (File.Exists(piece.Path)) File.Delete(piece.Path); } catch { }
            }
        }

        // ------------------------------------------------------------------

        private static int _joinCounter;

        /// <summary>Where a poured-together piece is written: beside the cache, never inside an
        /// utterance's own folder — that folder is sealed by a manifest naming exactly its pieces,
        /// and a stranger in it would be a puzzle for whoever reads it next.</summary>
        private static string JoinedPathFor()
        {
            try
            {
                var folder = Path.Combine(VoiceCache.CacheRoot, "_joined");
                Directory.CreateDirectory(folder);
                var name = "j" + DateTime.UtcNow.Ticks.ToString("x") + "-" + (++_joinCounter).ToString() + ".wav";
                return Path.Combine(folder, name);
            }
            catch
            {
                return string.Empty;   // no scratch folder simply means seams, not silence
            }
        }

        /// <summary>Sweeps any poured pieces a crash left behind. Called once when the voice starts.</summary>
        public static void SweepJoined()
        {
            try
            {
                var folder = Path.Combine(VoiceCache.CacheRoot, "_joined");
                if (!Directory.Exists(folder)) return;
                foreach (var file in Directory.GetFiles(folder, "*.wav"))
                    try { File.Delete(file); } catch { /* in use by nothing we know of */ }
            }
            catch { /* best-effort housekeeping */ }
        }

        /// <summary>
        /// Keeps the chain moving while anything is speaking. A background loop that only ever
        /// enqueues onto the dispatcher — the work itself still happens on the game thread. It winds
        /// itself down the moment there is nothing left to say.
        /// </summary>
        private static void Kick()
        {
            lock (Gate)
            {
                if (_driverRunning) return;
                _driverRunning = true;
            }

            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        MainThreadDispatcher.Enqueue(Tick);
                        await Task.Delay(DriverIntervalMs).ConfigureAwait(false);

                        lock (Gate)
                        {
                            if (_active || _current != null || _next != null || _poured != null
                                || _pouring || _stopRequested) continue;
                            _driverRunning = false;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Error("voice: the playback driver", ex);
                    lock (Gate) _driverRunning = false;
                }
            });
        }

        private static string FolderOf(string path)
        {
            try { return Path.GetDirectoryName(path) ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
