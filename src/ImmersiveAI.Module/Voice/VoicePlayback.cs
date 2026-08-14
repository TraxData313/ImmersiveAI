using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

// NOTE: no "using TaleWorlds.Engine;" in this file, and there must never be one — TaleWorlds.Engine
// carries its own Path type, which would shadow System.IO.Path in a file that reads WAV headers off
// the disk. SoundEvent is fully qualified below for exactly that reason.

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// Puts a made line into the air, chunk after chunk, through the game's own audio engine.
    /// <para>
    /// Through the ENGINE and not a player of our own, deliberately: an event on the game's
    /// voice-over bus obeys the player's own volume slider, mutes on alt-tab, and ducks under the
    /// music the way vanilla speech does. Everything that makes voices feel like part of the game
    /// rather than a program shouting over it comes free with that one decision.
    /// </para>
    /// <para>
    /// THE ENGINE IS TOUCHED FROM THE GAME THREAD AND NOWHERE ELSE. Anything may enqueue a chunk
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
    /// </summary>
    public static class VoicePlayback
    {
        /// <summary>How long a just-started event may claim to be stopped before we believe it.</summary>
        private static readonly TimeSpan StartGrace = TimeSpan.FromMilliseconds(500);

        /// <summary>Slack over a chunk's own length before we stop waiting for it. A wedged event
        /// must never hold the rest of a reply silent forever.</summary>
        private static readonly TimeSpan LengthSlack = TimeSpan.FromSeconds(3);

        /// <summary>The ceiling for a chunk whose length we could not read.</summary>
        private static readonly TimeSpan UnknownLengthCeiling = TimeSpan.FromSeconds(90);

        /// <summary>How often the chain is looked at while something is speaking.</summary>
        private const int DriverIntervalMs = 40;

        /// <summary>
        /// The FMOD programmer-event name our audio is handed to. Configurable ON PURPOSE, because
        /// which event a runtime WAV should ride is not something reading the game settles.
        /// <para>
        /// What is known (2026.08.14): the game defines no <c>event:/Extra/voiceover</c> at all —
        /// that name is ours, and FMOD accepts it as a programmer sound and plays it, which is why
        /// the first test worked. What it does NOT obviously do is route through the game's own
        /// <c>vca:/Voiceover</c> fader, and the first playtest came back "veeeery quietly, can
        /// barely hear her". The engine's own output is also 10-20 dB low at source (the host
        /// normalises that now), so there were two quiet problems stacked, and only one of them was
        /// ours to fix in the WAV.
        /// </para>
        /// <para>
        /// Real events the game does define, if this needs to be tried against a tuned bus:
        /// <c>event:/mod/mission/voice</c>, <c>event:/mod/mission/voice_shout</c>,
        /// <c>event:/mod/mission/voice_trivial</c>. Untested — they are agent voices from missions
        /// and may want a scene or a switch. Change <c>VoiceSoundEvent</c> in config.json and speak
        /// one line; no rebuild, no restart of anything but the game.
        /// </para>
        /// </summary>
        public static string SoundEventName { get; set; } = "event:/Extra/voiceover";

        private static readonly object Gate = new object();
        private static readonly Queue<string> Queued = new Queue<string>();

        private static TaleWorlds.Engine.SoundEvent? _current;
        private static string _currentFolder = string.Empty;
        private static DateTime _startedUtc;
        private static DateTime _deadlineUtc;
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

        /// <summary>The sequence being spoken. A chunk offered under an older one is discarded.</summary>
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
                DropQueuedInside();                 // the old sequence's waiting chunks
                _dropCurrent = true;                // and the one in the air, on the next tick
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

        /// <summary>Adds a made chunk to the end of the current sequence. Safe from any thread.</summary>
        public static void Enqueue(long generation, string wavPath)
        {
            if (string.IsNullOrEmpty(wavPath)) return;

            lock (Gate)
            {
                if (generation != _generation || !_active) return;    // the moment has passed

                Queued.Enqueue(wavPath);
                // Held from the instant it is promised until the instant it has been heard, so the
                // pruner can never sweep the folder out from under a sentence in progress.
                VoiceCache.MarkInUse(FolderOf(wavPath));
            }
            Kick();
        }

        /// <summary>No more chunks are coming for this sequence. It ends when the last one has played.</summary>
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
                if (!_active && _current == null && Queued.Count == 0) return;
                _stopRequested = true;
                DropQueuedInside();
            }
            // Land it on the very next drain rather than touching the engine from here — the caller
            // may be a background thread, and this is the one thing that must not be.
            MainThreadDispatcher.Enqueue(Tick);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// The chain, one step. Runs on the game thread — from the dispatcher's own drain today, and
        /// harmlessly again from <c>OnApplicationTick</c> whenever a later milestone wires it there:
        /// the work is idempotent and both callers are the same thread.
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
                if (dropping) ReleaseCurrent();

                if (_current != null)
                {
                    if (!HasFinished(_current)) return;
                    ReleaseCurrent();
                }

                string? next;
                lock (Gate)
                {
                    if (!_active) return;
                    if (Queued.Count > 0) next = Queued.Dequeue();
                    else if (_complete) next = null;                  // nothing left and nothing coming
                    else return;                                     // waiting on the engine to make more
                }

                if (next == null) { Teardown(fireCallback: true); return; }
                StartChunk(next);
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: playing a line", ex);
                try { Teardown(fireCallback: false); } catch { }
            }
        }

        private static void StartChunk(string path)
        {
            var folder = FolderOf(path);
            try
            {
                if (!File.Exists(path))
                {
                    ModLog.Warn("voice: a spoken chunk went missing before it could be played.");
                    VoiceCache.ReleaseInUse(folder);
                    return;                                          // the next tick takes the one after it
                }

                var sound = TaleWorlds.Engine.SoundEvent.CreateEventFromExternalFile(
                    SoundEventName, path, scene: null, is3d: false, isBlocking: false);

                if (sound == null || sound.IsNullSoundEvent())
                {
                    ModLog.Warn("voice: the audio engine would not take " + Path.GetFileName(path) + ".");
                    VoiceCache.ReleaseInUse(folder);
                    return;
                }

                if (!sound.Play())
                {
                    ModLog.Warn("voice: the audio engine would not play " + Path.GetFileName(path) + ".");
                    try { sound.Release(); } catch { }
                    VoiceCache.ReleaseInUse(folder);
                    return;
                }

                var length = TryReadWavLength(path) ?? UnknownLengthCeiling;
                lock (Gate)
                {
                    _current = sound;
                    _currentFolder = folder;                         // its in-use mark travels with it
                    _startedUtc = DateTime.UtcNow;
                    _deadlineUtc = _startedUtc + length + LengthSlack;
                    _observedPlaying = false;
                }
                Kick();
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: starting a spoken chunk", ex);
                VoiceCache.ReleaseInUse(folder);
            }
        }

        private static bool HasFinished(TaleWorlds.Engine.SoundEvent sound)
        {
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
            TaleWorlds.Engine.SoundEvent? sound;
            string folder;
            lock (Gate)
            {
                sound = _current;
                folder = _currentFolder;
                _current = null;
                _currentFolder = string.Empty;
            }

            if (sound != null)
            {
                // Release, never Stop-then-Release: Stop() clears the id and Release() would then do
                // nothing at all, leaking the event. Release stops it on its own.
                try { sound.Release(); } catch { /* already gone */ }
            }
            if (folder.Length > 0) VoiceCache.ReleaseInUse(folder);
        }

        private static void Teardown(bool fireCallback)
        {
            ReleaseCurrent();

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

        /// <summary>Empties the queue, giving back one in-use hold per chunk that will now never be
        /// played. Call inside the lock.</summary>
        private static void DropQueuedInside()
        {
            while (Queued.Count > 0)
                VoiceCache.ReleaseInUse(FolderOf(Queued.Dequeue()));
        }

        /// <summary>
        /// Keeps the chain moving while anything is speaking. A background loop that only ever
        /// enqueues onto the dispatcher — the work itself still happens on the game thread, on the
        /// drain that <c>SubModule.OnApplicationTick</c> already performs every frame. It winds
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
                            if (_active || _current != null || _stopRequested) continue;
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

        // ------------------------------------------------------------------

        /// <summary>
        /// How long a WAV runs, read from its own header — the honest ceiling for "this chunk should
        /// be over by now". Null when the file is not a WAV we understand, which costs nothing but a
        /// more generous ceiling.
        /// </summary>
        private static TimeSpan? TryReadWavLength(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(stream))
                {
                    if (stream.Length < 44) return null;
                    if (new string(reader.ReadChars(4)) != "RIFF") return null;
                    reader.ReadUInt32();                                  // riff size
                    if (new string(reader.ReadChars(4)) != "WAVE") return null;

                    uint byteRate = 0;
                    while (stream.Position + 8 <= stream.Length)
                    {
                        var id = new string(reader.ReadChars(4));
                        var size = reader.ReadUInt32();

                        if (id == "fmt ")
                        {
                            if (size < 16) return null;
                            reader.ReadUInt16();                          // format tag
                            reader.ReadUInt16();                          // channels
                            reader.ReadUInt32();                          // sample rate
                            byteRate = reader.ReadUInt32();
                            var rest = (long)size - 12;
                            if (rest > 0) stream.Seek(rest, SeekOrigin.Current);
                        }
                        else if (id == "data")
                        {
                            if (byteRate == 0) return null;
                            return TimeSpan.FromSeconds(size / (double)byteRate);
                        }
                        else
                        {
                            stream.Seek(size + (size % 2), SeekOrigin.Current);   // chunks are word-aligned
                        }
                    }
                }
            }
            catch { /* an unreadable header is simply an unknown length */ }
            return null;
        }

        private static string FolderOf(string path)
        {
            try { return Path.GetDirectoryName(path) ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
