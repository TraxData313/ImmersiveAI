using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Voices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// The one door the rest of the mod knocks on to hear a line spoken.
    /// <para>
    /// SINCE 2026.09.24 THE SPEAKING IS CLAUDE-VOICE'S. This service no longer makes a sound: it
    /// decides WHO a soul sounds like (the casting — by hand, or a voice of their own people chosen
    /// from their name), turns their words into what an actor would say (a laugh where they laugh,
    /// a whisper where they whisper, when the engine can), and asks the voice app to say it. The app
    /// owns the engines, the models, the audio and the speaker. See <see cref="ClaudeVoiceClient"/>
    /// for why, and <see cref="ClaudeVoiceApp"/> for how the game knows whether it is there.
    /// </para>
    /// <para>
    /// NOTHING HERE MAY EVER COST A LINE. Every failure road ends in silence, a log line and at most
    /// one plain notice: no exception escapes, no call blocks the game thread, and with the app
    /// absent or voices off the mod behaves exactly as it did before it had a voice at all.
    /// </para>
    /// </summary>
    public static class VoiceService
    {
        private static readonly object ShelfGate = new object();
        private static ModConfig? _config;
        private static VoiceAssignments _casting = new VoiceAssignments();
        private static DateTime _castingStampUtc = DateTime.MinValue;
        private static DateTime _castingCheckedUtc = DateTime.MinValue;
        private static bool _castingLoaded;
        private static DateTime _speakingUntilUtc = DateTime.MinValue;
        private static bool _warnedUnreadable;

        public static void Configure(ModConfig config)
        {
            _config = config;
            SweepOldCache();
            SweepOldEngine();
            if (!_hookedExit)
            {
                _hookedExit = true;
                try { AppDomain.CurrentDomain.ProcessExit += (_, __) => Shutdown(); } catch { }
            }
        }

        private static bool _hookedExit;

        internal static ModConfig? Config => _config ?? SubModule.Config;

        /// <summary>Where the casting sheet lives, and the player's own voices from the days the mod
        /// carried its own engine (still read — claude-voice is told about this folder).</summary>
        public static string VoicesRoot => Path.Combine(ModConfig.ConfigDirectory, "Voices");

        /// <summary>The casting sheet's own file. Deliberately NOT inside the campaign folder: the
        /// save-scoped memory snapshots photograph that folder, and rewinding a save must not
        /// silently recast anybody. Memory is a thing to rewind; a voice is not.</summary>
        public static string CastingFilePath => Path.Combine(VoicesRoot, "assignments.json");

        private static bool Enabled => Config?.EnableVoice ?? false;

        /// <summary>Whether a line could be spoken right now: voices on, and the app answering.</summary>
        public static bool IsAvailable => Enabled && ClaudeVoiceApp.Now.Running;

        /// <summary>Plainly why not, when <see cref="IsAvailable"/> is false. Empty when it is true.</summary>
        public static string UnavailableReason
        {
            get
            {
                if (Config == null) return "the mod is still starting up";
                if (!Enabled) return "voices are turned off (Voices → Turn voices on)";
                switch (ClaudeVoiceApp.Now.State)
                {
                    case ClaudeVoiceApp.AppState.Running: return string.Empty;
                    case ClaudeVoiceApp.AppState.Starting: return "the voice app is still waking up — give it a minute";
                    case ClaudeVoiceApp.AppState.Installing: return "the voice app is being installed";
                    case ClaudeVoiceApp.AppState.Installed: return "the voice app isn't running (Voices → Start)";
                    case ClaudeVoiceApp.AppState.NotInstalled: return "the voice app isn't installed yet (Voices → Install)";
                    default: return "looking for the voice app";
                }
            }
        }

        /// <summary>Whether a reply should speak of its own accord, or wait to be asked.</summary>
        public static bool AutoSpeakEnabled => Config?.VoiceAutoSpeak ?? true;

        /// <summary>The sounds and whether a mood is followed, by the engine speaking now — nothing
        /// while voices are off, the app is down, or the player turned the acting off. The one
        /// answer the sheet, the speaking and the thread all share.</summary>
        public static (IList<string>? Sounds, bool TakesMood) WhatTheVoiceCanDo()
        {
            if (!Enabled || !(Config?.VoicePerformSounds ?? true)) return (null, false);
            var now = ClaudeVoiceApp.Now;
            if (!now.Running || now.Health == null) return (null, false);
            return (now.Health.Sounds, now.Health.TakesMood);
        }

        /// <summary>Roughly whether something of ours is still in the air — for the Stop button and
        /// the panic key, which should only take a key while there is something to stop. Worked out
        /// from the line's length, generously: a Stop pressed after the line ended costs nothing.</summary>
        public static bool IsSpeaking => DateTime.UtcNow < _speakingUntilUtc;

        // ------------------------------------------------------------------ the doors

        /// <summary>Speaks this line in this soul's voice. Call from the game thread; returns at once.
        /// The newest words win — whatever the mod was saying is cut off.</summary>
        public static void Speak(Hero npc, string text) => SpeakAs(Describe(npc), null, text);

        /// <summary>Speaks a sample in one voice, whoever it belongs to.</summary>
        public static void Preview(VoicePreset? voice, string? line = null)
        {
            if (voice == null) return;
            SpeakAs(null, voice.Id, string.IsNullOrWhiteSpace(line) ? PreviewLine : line!.Trim());
        }

        /// <summary>What a voice says when the player asks to hear it. In the world's own words and
        /// not "testing, one two three": the point is to judge whether this is the person.</summary>
        public const string PreviewLine =
            "We have ridden a long way together, you and I. Whatever comes of tomorrow, I am glad it is you beside me.";

        private static void SpeakAs(Speaker? who, string? voiceId, string text)
        {
            try
            {
                if (!Enabled || string.IsNullOrWhiteSpace(text)) return;
                if (who == null && string.IsNullOrWhiteSpace(voiceId)) return;

                var snapshot = ClaudeVoiceApp.Now;
                if (!snapshot.Running)
                {
                    ClaudeVoiceApp.Poll(force: true);
                    ModLog.Info("voice: nothing spoken — " + UnavailableReason + ".");
                    return;
                }

                var id = voiceId ?? (who == null ? string.Empty : ResolveVoiceId(who, snapshot.Voices));
                if (string.IsNullOrWhiteSpace(id))
                {
                    ModLog.Info($"voice: {who?.Id} has no voice on {snapshot.Engine} — nothing spoken.");
                    return;
                }

                var health = snapshot.Health;
                var acted = Config?.VoiceSpeakActedParts ?? true;
                var perform = Config?.VoicePerformSounds ?? true;
                var line = SpeakableText.Performed(text, acted,
                    perform ? health?.Sounds : null,
                    takesMood: perform && (health?.TakesMood ?? false));
                if (line.Text.Length == 0) return;

                // A generous guess at how long it runs: speech is 13-17 characters a second, a mood
                // slows it, and the app may still be loading.
                _speakingUntilUtc = DateTime.UtcNow + TimeSpan.FromSeconds(4 + line.Text.Length / 11.0);

                var engine = snapshot.Engine;
                Task.Run(async () =>
                {
                    var outcome = await ClaudeVoiceClient.SpeakAsync(line.Text, id, line.Mood, line.Instruction).ConfigureAwait(false);
                    switch (outcome)
                    {
                        case ClaudeVoiceClient.SpeakOutcome.Spoken:
                            break;
                        case ClaudeVoiceClient.SpeakOutcome.Unreadable:
                            _speakingUntilUtc = DateTime.MinValue;
                            if (!_warnedUnreadable)
                            {
                                _warnedUnreadable = true;
                                MainThreadDispatcher.Enqueue(() => InformationManager.DisplayMessage(new InformationMessage(
                                    $"{ClaudeVoiceApp.EngineName(engine)} can't read this language, so those lines stay silent. "
                                    + "Qwen reads every language — switch to it on the Voices page.",
                                    new Color(0.85f, 0.72f, 0.45f))));
                            }
                            break;
                        case ClaudeVoiceClient.SpeakOutcome.UnknownVoice:
                            // The catalogue moved under us (a voice deleted, the engine switched):
                            // look again, so the next line is cast from what is really there.
                            _speakingUntilUtc = DateTime.MinValue;
                            ModLog.Info($"voice: claude-voice has no voice '{id}' on {engine} — reading its list again.");
                            ClaudeVoiceApp.Poll(force: true);
                            break;
                        default:
                            _speakingUntilUtc = DateTime.MinValue;
                            ClaudeVoiceApp.Poll(force: true);
                            break;
                    }
                });
            }
            catch (Exception ex) { ModLog.Error("voice: speaking a line", ex); }
        }

        /// <summary>Silence, now — ours and whatever else is waiting in the app.</summary>
        // ------------------------------------------------------------------ hearing an engine first

        private static System.Media.SoundPlayer? _sample;

        /// <summary>The short recording of one engine that ships with the mod (Voices\_samples), so an
        /// engine can be HEARD before a gigabyte of it is downloaded — the same line, in the same voice,
        /// on each of the three, so what differs is the engine and nothing else.</summary>
        public static string SamplePath(string engine)
        {
            var root = ClaudeVoiceApp.ShippedVoicesFolder();
            return root.Length == 0 ? string.Empty : Path.Combine(root, "_samples", engine + ".wav");
        }

        public static bool HasSample(string engine)
        {
            var path = SamplePath(engine);
            return path.Length > 0 && File.Exists(path);
        }

        /// <summary>Plays it through Windows' own player — no voice app needed, which is the point.</summary>
        public static bool PlaySample(string engine)
        {
            try
            {
                StopSample();
                if (!HasSample(engine)) return false;
                _sample = new System.Media.SoundPlayer(SamplePath(engine));
                _sample.Play();
                _speakingUntilUtc = DateTime.UtcNow + TimeSpan.FromSeconds(11);
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: playing the " + engine + " sample", ex);
                return false;
            }
        }

        public static void StopSample()
        {
            try { _sample?.Stop(); _sample?.Dispose(); } catch { }
            _sample = null;
        }

        public static void Stop()
        {
            StopSample();
            _speakingUntilUtc = DateTime.MinValue;
            if (!ClaudeVoiceApp.Now.Running) return;
            Task.Run(() => ClaudeVoiceClient.StopAsync());
        }

        /// <summary>On the way out: close the app if the game opened it.</summary>
        public static void Shutdown()
        {
            try { ClaudeVoiceApp.CloseIfWeStartedIt(); }
            catch (Exception ex) { ModLog.Error("voice: shutting down", ex); }
        }

        /// <summary>Looks again, now — for the "turn voices on" moment and anything like it.</summary>
        public static void Rediscover()
        {
            lock (ShelfGate) _castingLoaded = false;
            ClaudeVoiceApp.Poll(force: true);
        }

        // ------------------------------------------------------------------ who sounds like whom

        /// <summary>The few facts about a soul that casting needs, read on the caller's thread.
        /// <c>StringId</c>, <c>IsFemale</c> and <c>Culture</c> are fixed for a hero's life, which is
        /// why these alone may be carried off the game thread. Do not widen it.</summary>
        private sealed class Speaker
        {
            public string Id = string.Empty;
            public bool IsFemale;
            public bool IsPlayer;
            public string Culture = string.Empty;
        }

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

        private static bool AutoCastOn => Config?.VoiceAutoCast ?? true;

        /// <summary>The one place the tiers are applied — by hand, their own people, no people,
        /// anyone of their sex — so the panel, the badge and the speaking never disagree.</summary>
        private static string ResolveVoiceId(Speaker who, IReadOnlyList<VoicePreset> shelf)
        {
            EnsureCasting();
            lock (ShelfGate)
            {
                if (who.IsPlayer && !string.IsNullOrWhiteSpace(_casting.Player)
                    && shelf.Any(v => string.Equals(v.Id, _casting.Player, StringComparison.OrdinalIgnoreCase)))
                    return _casting.Player;

                // A hand-cast voice that the engine speaking now does not have (cast on Qwen, and the
                // player moved to Pocket) falls through to their people's rather than to silence. The
                // casting itself is kept: switching back brings it back.
                var sheet = who.IsPlayer ? null : _casting;
                if (sheet != null && sheet.IsCast(who.Id)
                    && !shelf.Any(v => string.Equals(v.Id, sheet.ByNpc[who.Id], StringComparison.OrdinalIgnoreCase)))
                    sheet = null;
                return VoiceCasting.Pick(sheet, shelf, who.Id, who.IsFemale, who.Culture, AutoCastOn);
            }
        }

        /// <summary>Every voice the engine speaking now can speak in — claude-voice's own list,
        /// the last one seen when it is not running.</summary>
        public static IReadOnlyList<VoicePreset> Shelf() => ClaudeVoiceApp.Now.Voices;

        public static string VoiceIdFor(Hero? npc)
        {
            var who = Describe(npc);
            return who == null ? string.Empty : ResolveVoiceId(who, Shelf());
        }

        public enum VoiceOrigin { None, Cast, TheirPeople }

        /// <summary>WHY this soul speaks with the voice they do — the panel says "chosen" apart from
        /// "of their people", because a label that guesses at its own reasoning is worse than none.</summary>
        public static VoiceOrigin OriginFor(Hero? npc)
        {
            var who = Describe(npc);
            if (who == null) return VoiceOrigin.None;
            var id = ResolveVoiceId(who, Shelf());
            if (string.IsNullOrWhiteSpace(id)) return VoiceOrigin.None;
            EnsureCasting();
            lock (ShelfGate)
            {
                var chosen = who.IsPlayer ? _casting.Player : (_casting.IsCast(who.Id) ? _casting.ByNpc[who.Id] : string.Empty);
                return string.Equals(chosen, id, StringComparison.OrdinalIgnoreCase) ? VoiceOrigin.Cast : VoiceOrigin.TheirPeople;
            }
        }

        public static bool IsCastByHand(Hero? npc)
        {
            if (npc == null) return false;
            EnsureCasting();
            lock (ShelfGate) return _casting.IsCast(npc.StringId);
        }

        public static string PlayerVoiceId { get { EnsureCasting(); lock (ShelfGate) return _casting.Player ?? string.Empty; } }

        /// <summary>Casts a soul. An empty id clears it, and they fall back to their people's voice.</summary>
        public static void Cast(Hero? npc, string? voiceId)
        {
            if (npc == null) return;
            Change(sheet => sheet.Cast(npc.StringId, voiceId));
        }

        public static void SetPlayerVoice(string? voiceId) => Change(sheet => sheet.Player = (voiceId ?? string.Empty).Trim());

        private static void Change(Action<VoiceAssignments> edit)
        {
            try
            {
                EnsureCasting();
                lock (ShelfGate)
                {
                    edit(_casting);
                    Directory.CreateDirectory(VoicesRoot);
                    _casting.Save(CastingFilePath);
                    _castingStampUtc = Stamp();
                }
            }
            catch (Exception ex) { ModLog.Error("voice: writing the casting sheet", ex); }
        }

        /// <summary>The casting sheet, re-read when the file changes under us (a player editing it
        /// by hand while the game runs is a real thing to do) — checked at most every few seconds.</summary>
        private static void EnsureCasting()
        {
            lock (ShelfGate)
            {
                var now = DateTime.UtcNow;
                if (_castingLoaded && now - _castingCheckedUtc < TimeSpan.FromSeconds(5)) return;
                _castingCheckedUtc = now;
                var stamp = Stamp();
                if (_castingLoaded && stamp == _castingStampUtc) return;
                try
                {
                    _casting = VoiceAssignments.Load(CastingFilePath);
                    if (_casting.ClearDeadDefaults()) _casting.Save(CastingFilePath);
                }
                catch (Exception ex)
                {
                    ModLog.Error("voice: reading the casting sheet", ex);
                    _casting = new VoiceAssignments();
                }
                _castingLoaded = true;
                _castingStampUtc = Stamp();
            }
        }

        private static DateTime Stamp()
        {
            try { return File.Exists(CastingFilePath) ? File.GetLastWriteTimeUtc(CastingFilePath) : DateTime.MinValue; }
            catch { return DateTime.MinValue; }
        }

        /// <summary>
        /// The spoken-audio cache from the days the mod made its own sound — a folder of WAVs that
        /// nothing reads any more and that could have grown to two gigabytes. Swept once, quietly.
        /// </summary>
        private static void SweepOldCache()
        {
            Task.Run(() =>
            {
                try
                {
                    var cache = Path.Combine(VoicesRoot, "_cache");
                    if (Directory.Exists(cache))
                    {
                        Directory.Delete(cache, true);
                        ModLog.Info("voice: swept the old spoken-audio cache (the voice app keeps its own now).");
                    }
                }
                catch (Exception ex) { ModLog.Warn("voice: could not sweep the old audio cache — " + ex.Message); }
            });
        }

        /// <summary>
        /// The speech engine an earlier version of the mod fetched for itself: eight DLLs, about 0.66 GB,
        /// unpacked flat into %LOCALAPPDATA%\Programs\qwen-tts-studio. Nothing reads them any more —
        /// claude-voice runs Qwen through the whole of Studio, a different download — so they are swept
        /// once. Only that exact shape is touched, a folder of DLLs and nothing else: a real Studio there
        /// has folders of its own (its Java runtime) and is somebody's working install. The model files
        /// are NOT touched: they live elsewhere, and claude-voice uses them where they lie.
        /// </summary>
        private static void SweepOldEngine()
        {
            Task.Run(() =>
            {
                try
                {
                    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "qwen-tts-studio");
                    if (!Directory.Exists(dir) || !File.Exists(Path.Combine(dir, "qwen3_tts.dll"))) return;
                    if (Directory.EnumerateDirectories(dir).Any()) return;
                    var files = Directory.GetFiles(dir);
                    if (files.Any(f => !f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                                       && !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase))) return;
                    // A setup somebody is running may be about to move a whole Studio in here.
                    if (System.Diagnostics.Process.GetProcessesByName("ClaudeVoiceSetup").Length > 0) return;

                    var bytes = files.Sum(f => new FileInfo(f).Length);
                    Directory.Delete(dir, true);
                    ModLog.Info("voice: swept the old built-in engine's DLLs (" + VoiceMachine.Bytes(bytes)
                                + ") — the voice app brings its own; the Qwen model is kept for it.");
                }
                catch (Exception ex) { ModLog.Warn("voice: could not sweep the old engine's DLLs — " + ex.Message); }
            });
        }
    }
}
