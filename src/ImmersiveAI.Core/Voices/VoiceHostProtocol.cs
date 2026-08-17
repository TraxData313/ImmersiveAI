using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>
    /// The words the game and the voice host say to each other — one compact JSON object per line,
    /// over the host's stdin and stdout.
    /// <para>
    /// The engine is hosted OUT of process on purpose: ggml answers bad input with
    /// <c>GGML_ASSERT -&gt; abort()</c>, which on the game's runtime is fail-fast and uncatchable. Out
    /// there, a crash costs the voices for a session; in here it would cost the campaign. Everything
    /// in this file exists to make that boundary survivable — which is why <c>TryParse</c> never
    /// throws for any input at all, and why nothing is ever "impossible enough" to skip a check.
    /// </para>
    /// <para>
    /// Two directions, parsed by two doors. <see cref="ParseRequest"/> is what the HOST reads
    /// (<c>synthesize</c> / <c>cancel</c> / <c>ping</c> / <c>quit</c>); <see cref="ParseMessage"/> is
    /// what the GAME reads (the <c>ready</c> / <c>failed</c> / <c>pong</c> events, and the per-request
    /// results). Each door refuses the other's lines, so a confused stream is silence rather than
    /// nonsense.
    /// </para>
    /// <para>
    /// THE PARSE IS LENIENT WHERE LOSING A LINE WOULD COST MORE THAN READING A BAD ONE. A malformed
    /// line is skipped, but an understood-yet-wrong request is NOT: the game side has an id waiting on
    /// an answer, and a silently dropped request is a reply that never comes. So a known op with a
    /// missing field still parses, and <see cref="VoiceSynthesizeRequest.Validate"/> names what is
    /// wrong so the host can answer honestly with the id in hand. Unknown ops parse too, as
    /// <see cref="VoiceUnknownRequest"/> — the protocol's own answer to them is an error line, not a
    /// crash and not a shrug.
    /// </para>
    /// <para>
    /// Serialized lines are pure ASCII (non-ASCII is written as <c>\uXXXX</c>). The author plays in
    /// Bulgarian and this is a Windows console pipe, where the encoding on either end is a classic
    /// place to lose a language; escaping sidesteps the question entirely and costs a few bytes on a
    /// local pipe. Parsing accepts raw UTF-8 all the same, so another implementation may write it
    /// either way.
    /// </para>
    /// </summary>
    public static class VoiceHostProtocol
    {
        /// <summary>What the engine actually produces: 16-bit mono at 24 kHz. Used as the fallback
        /// when a line omits <c>rate</c> — a missing rate must never become a zero someone divides by.</summary>
        public const int DefaultSampleRate = 24000;

        /// <summary>No language forced; the engine decides from the text itself.</summary>
        public const int NoLanguage = -1;

        // ---- the wire's own vocabulary, in one place, so three implementations cannot drift ----

        public const string EventReady = "ready";
        public const string EventFailed = "failed";
        public const string EventPong = "pong";

        /// <summary>The host running as an ERRAND rather than as an engine: fetching the speech
        /// engine and its models so the player never has to. Same executable, same channel, its own
        /// event — and it is only ever sent by a host started with <c>--fetch</c>, which loads no
        /// native library at all.</summary>
        public const string EventFetch = "fetch";

        /// <summary>What the errand is doing right now — one of the <c>Stage*</c> names below.</summary>
        public const string FieldStage = "stage";

        /// <summary>Bytes done and bytes expected, for the one file in hand. Not for the whole
        /// errand: a total that jumps when the next file starts is honest and cheap, where one
        /// pretending to know the size of everything up front is neither.</summary>
        public const string FieldDone = "done";
        public const string FieldTotal = "total";

        /// <summary>The stage in the player's own words ("the speech engine"), written by the host
        /// because the host is what knows which file it is on.</summary>
        public const string FieldNote = "note";

        public const string StageChecking = "checking";
        public const string StageEngine = "engine";
        public const string StageUnpacking = "unpacking";
        public const string StageModel = "model";

        /// <summary>The last fetch line, and the only one carrying a verdict.</summary>
        public const string StageDone = "done";

        /// <summary>Marks a line as ONE PIECE of a streamed reply rather than the reply's verdict.
        /// It carries an id like a result does, so it must be recognised BEFORE the result shape or
        /// a piece would be read as the whole thing finishing — and the reply would be cut off after
        /// its first second.</summary>
        public const string FieldChunk = "chunk";

        /// <summary>Asks the host to write a reply as one file instead of as pieces.</summary>
        public const string FieldWhole = "whole";

        /// <summary>The audio-token ceiling for ONE line, worked out from that line's own length —
        /// see <see cref="VoiceBudget"/>. The anti-derail rail, and the reason it lives on the
        /// request rather than on the host's command line: a ceiling that fits a whole session fits
        /// no single sentence in it. Absent or 0 means "whatever the host was started with".</summary>
        public const string FieldMaxTokens = "maxTokens";

        /// <summary>Set on a result the host does not trust: far more audio came back than the words
        /// could justify, so the generation was cut short. The words already spoken are good — every
        /// derail measured begins correctly and runs away later — but the clip must never be CACHED,
        /// or one bad synthesis is replayed for the rest of the campaign.</summary>
        public const string FieldDerailed = "derailed";

        public const string OpSynthesize = "synthesize";
        public const string OpCancel = "cancel";
        public const string OpPing = "ping";
        public const string OpQuit = "quit";

        public const string KindDefault = "default";
        public const string KindIcl = "icl";
        public const string KindEmbedding = "embedding";
        public const string KindSpeaker = "speaker";

        /// <summary>The error text the host answers an op it does not know with.</summary>
        public const string UnknownOpError = "unknown op";

        internal const string FieldEvent = "event";
        internal const string FieldOp = "op";
        internal const string FieldId = "id";
        internal const string FieldText = "text";
        internal const string FieldOut = "out";
        internal const string FieldVoice = "voice";
        internal const string FieldLanguageId = "languageId";
        internal const string FieldKind = "kind";
        internal const string FieldPath = "path";
        internal const string FieldSpeaker = "speaker";
        internal const string FieldOk = "ok";
        internal const string FieldMs = "ms";
        internal const string FieldSamples = "samples";
        internal const string FieldRate = "rate";
        internal const string FieldError = "error";
        internal const string FieldBackend = "backend";
        internal const string FieldModel = "model";

        // ------------------------------------------------------------------
        // game -> host
        // ------------------------------------------------------------------

        /// <summary>Reads one line the GAME sent. Null when the line is not a request at all —
        /// garbage, an array, an event line, an object with no <c>op</c>. Never throws.</summary>
        public static VoiceHostRequest? ParseRequest(string? line)
        {
            var o = AsObject(line);
            if (o == null) return null;

            var op = Str(o, FieldOp);
            if (op.Length == 0) return null;

            var id = Str(o, FieldId);

            switch (op.ToLowerInvariant())
            {
                case OpSynthesize:
                    return new VoiceSynthesizeRequest
                    {
                        Id = id,
                        Text = Str(o, FieldText),
                        OutPath = Str(o, FieldOut),
                        Voice = VoiceSource.FromToken(o[FieldVoice]),
                        LanguageId = (int)Num(o, FieldLanguageId, NoLanguage),
                        Whole = Bool(o, FieldWhole, false),
                        MaxTokens = (int)Num(o, FieldMaxTokens, 0),
                    };

                case OpCancel:
                    return new VoiceCancelRequest { Id = id };

                case OpPing:
                    return new VoicePingRequest { Id = id };

                case OpQuit:
                    return new VoiceQuitRequest { Id = id };

                default:
                    // Understood as a request, just not one we know: the host owes it an error line
                    // carrying whatever id it came with, which it cannot send if we drop it here.
                    return new VoiceUnknownRequest(op) { Id = id };
            }
        }

        /// <summary>Try-shape of <see cref="ParseRequest"/>. False for every unreadable line —
        /// garbage, truncated JSON, an empty line, an array, an object of the wrong shape.</summary>
        public static bool TryParseRequest(string? line, out VoiceHostRequest? request)
        {
            request = ParseRequest(line);
            return request != null;
        }

        // ------------------------------------------------------------------
        // host -> game
        // ------------------------------------------------------------------

        /// <summary>Reads one line the HOST sent — an event or a per-request result. Null when the
        /// line is neither. Never throws.</summary>
        public static VoiceHostMessage? ParseMessage(string? line)
        {
            var o = AsObject(line);
            if (o == null) return null;

            // An EVENT names itself; a RESULT is known by carrying an id (or at least a verdict).
            // Checked in this order so a line wearing both is read as the event it declares itself to be.
            var ev = Str(o, FieldEvent);
            if (ev.Length > 0)
            {
                switch (ev.ToLowerInvariant())
                {
                    case EventReady:
                        return new VoiceReadyEvent
                        {
                            Backend = Str(o, FieldBackend),
                            Model = Str(o, FieldModel),
                            Rate = (int)Num(o, FieldRate, DefaultSampleRate),
                        };
                    case EventFailed:
                        return new VoiceFailedEvent { Error = Str(o, FieldError) };
                    case EventPong:
                        return new VoicePongEvent();
                    case EventFetch:
                        return new VoiceFetchEvent
                        {
                            Stage = Str(o, FieldStage),
                            Note = Str(o, FieldNote),
                            Done = (long)Num(o, FieldDone, 0),
                            Total = (long)Num(o, FieldTotal, 0),
                            // Only the closing line carries a verdict, and an absent one is not a
                            // failure — a progress line that parsed as "ok:false" would end the
                            // errand at its first megabyte.
                            Ok = Bool(o, FieldOk, true),
                            Error = Str(o, FieldError),
                        };
                    default:
                        return null;   // an event from a newer host: not ours to guess at
                }
            }

            // A streamed piece before the result shape: it wears an id too, and read as a result it
            // would look like the reply finishing early.
            if (o[FieldChunk] != null)
            {
                return new VoiceChunkEvent
                {
                    Id = Str(o, FieldId),
                    Index = (int)Num(o, FieldChunk, 0),
                    Path = Str(o, FieldPath),
                    Samples = Num(o, FieldSamples, 0),
                    Rate = (int)Num(o, FieldRate, DefaultSampleRate),
                };
            }

            var hasVerdict = o[FieldOk] != null;
            var id = Str(o, FieldId);
            if (!hasVerdict && id.Length == 0) return null;

            return new VoiceSynthesisResult
            {
                Id = id,
                Ok = Bool(o, FieldOk, false),
                Path = Str(o, FieldPath),
                Ms = (int)Num(o, FieldMs, 0),
                Samples = Num(o, FieldSamples, 0),
                Rate = (int)Num(o, FieldRate, DefaultSampleRate),
                Error = Str(o, FieldError),
                Derailed = Bool(o, FieldDerailed, false),
            };
        }

        /// <summary>Try-shape of <see cref="ParseMessage"/>.</summary>
        public static bool TryParseMessage(string? line, out VoiceHostMessage? message)
        {
            message = ParseMessage(line);
            return message != null;
        }

        /// <summary>The line a host answers an op it does not know with, carrying the id back so the
        /// waiting side can stop waiting.</summary>
        public static string UnknownOpLine(string? id)
            => VoiceSynthesisResult.Failure(id ?? string.Empty, UnknownOpError).Serialize();

        // ------------------------------------------------------------------
        // the machinery underneath
        // ------------------------------------------------------------------

        /// <summary>One object as one line: compact, ASCII, and guaranteed newline-free — the frame
        /// is the newline the transport adds, so a line that held one would swallow the next message.</summary>
        internal static string Write(JObject o)
        {
            var sb = new StringBuilder(256);
            using (var sw = new StringWriter(sb, CultureInfo.InvariantCulture))
            using (var jw = new JsonTextWriter(sw))
            {
                jw.Formatting = Formatting.None;
                jw.StringEscapeHandling = StringEscapeHandling.EscapeNonAscii;
                o.WriteTo(jw);
            }

            var line = sb.ToString();
            // Cannot happen with a conformant writer (JSON escapes every control character), and it
            // is one scan of a short string to make sure it stays that way.
            if (line.IndexOf('\n') >= 0 || line.IndexOf('\r') >= 0)
                line = line.Replace("\r", string.Empty).Replace("\n", string.Empty);
            return line;
        }

        /// <summary>Writes a string field, skipping it when empty — an absent field and an empty one
        /// mean the same thing everywhere in this protocol, and the short line is the readable one.</summary>
        internal static void Put(JObject o, string name, string? value)
        {
            if (!string.IsNullOrEmpty(value)) o[name] = value;
        }

        private static JObject? AsObject(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            try
            {
                var token = JToken.Parse(line);
                return token as JObject;   // an array, a bare string or a number is not a message
            }
            catch
            {
                return null;               // garbage, truncated, trailing junk — all the same answer
            }
        }

        internal static string Str(JObject o, string name)
        {
            try
            {
                var t = o[name];
                if (t == null) return string.Empty;
                switch (t.Type)
                {
                    case JTokenType.String:
                    case JTokenType.Integer:
                    case JTokenType.Float:
                    case JTokenType.Boolean:
                    case JTokenType.Date:
                    case JTokenType.Guid:
                    case JTokenType.Uri:
                        return t.Value<string>() ?? string.Empty;
                    default:
                        return string.Empty;   // null, an object, an array: nothing we can honestly read
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static long Num(JObject o, string name, long fallback)
        {
            try
            {
                var t = o[name];
                if (t == null) return fallback;
                switch (t.Type)
                {
                    case JTokenType.Integer:
                        return t.Value<long>();
                    case JTokenType.Float:
                        return (long)t.Value<double>();
                    case JTokenType.String:
                        return long.TryParse(t.Value<string>(), NumberStyles.Integer,
                                             CultureInfo.InvariantCulture, out var v) ? v : fallback;
                    default:
                        return fallback;
                }
            }
            catch
            {
                return fallback;   // a number too large for a long, most likely; the line still stands
            }
        }

        internal static bool Bool(JObject o, string name, bool fallback)
        {
            try
            {
                var t = o[name];
                if (t == null) return fallback;
                switch (t.Type)
                {
                    case JTokenType.Boolean:
                        return t.Value<bool>();
                    case JTokenType.Integer:
                        return t.Value<long>() != 0;
                    case JTokenType.String:
                        return bool.TryParse(t.Value<string>(), out var v) ? v : fallback;
                    default:
                        return fallback;
                }
            }
            catch
            {
                return fallback;
            }
        }
    }

    // ======================================================================
    // the voice a line is to be spoken in
    // ======================================================================

    /// <summary>How the host is told which voice to speak with. Four roads because the engine has
    /// four: a cloned voice's ICL prompt, a bare speaker embedding, one of the model's own named
    /// speakers, or nothing at all — whatever the engine does unaided.</summary>
    public enum VoiceSourceKind
    {
        Default = 0,
        Icl = 1,
        Embedding = 2,
        Speaker = 3,
    }

    /// <summary>The <c>voice</c> object of a synthesize request.</summary>
    public sealed class VoiceSource
    {
        public VoiceSourceKind Kind { get; set; } = VoiceSourceKind.Default;

        /// <summary>Absolute path to the icl-prompt.json or embedding.json this voice lives in.
        /// Empty for the other two kinds.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>One of the model's own speaker names. Empty for the other three kinds.</summary>
        public string Speaker { get; set; } = string.Empty;

        public static VoiceSource FromIcl(string path)
            => new VoiceSource { Kind = VoiceSourceKind.Icl, Path = path ?? string.Empty };

        public static VoiceSource FromEmbedding(string path)
            => new VoiceSource { Kind = VoiceSourceKind.Embedding, Path = path ?? string.Empty };

        public static VoiceSource FromSpeaker(string speaker)
            => new VoiceSource { Kind = VoiceSourceKind.Speaker, Speaker = speaker ?? string.Empty };

        /// <summary>Whatever the engine sounds like unaided — the honest answer when a soul has no
        /// voice cast and the player still wants to hear something.</summary>
        public static VoiceSource Unvoiced() => new VoiceSource();

        /// <summary>The wire spelling of a kind. An enum value from some later version writes
        /// "default", which is the safe thing for a host to receive.</summary>
        public static string NameOf(VoiceSourceKind kind)
        {
            switch (kind)
            {
                case VoiceSourceKind.Icl: return VoiceHostProtocol.KindIcl;
                case VoiceSourceKind.Embedding: return VoiceHostProtocol.KindEmbedding;
                case VoiceSourceKind.Speaker: return VoiceHostProtocol.KindSpeaker;
                default: return VoiceHostProtocol.KindDefault;
            }
        }

        /// <summary>Reads a kind, case-insensitively. Anything unknown is Default rather than an
        /// error — a newer game asking a older host for a kind it lacks should still get words.</summary>
        public static VoiceSourceKind ParseKind(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return VoiceSourceKind.Default;
            switch (name!.Trim().ToLowerInvariant())
            {
                case VoiceHostProtocol.KindIcl: return VoiceSourceKind.Icl;
                case VoiceHostProtocol.KindEmbedding: return VoiceSourceKind.Embedding;
                case VoiceHostProtocol.KindSpeaker: return VoiceSourceKind.Speaker;
                default: return VoiceSourceKind.Default;
            }
        }

        internal static VoiceSource FromToken(JToken? token)
        {
            if (!(token is JObject o)) return new VoiceSource();
            return new VoiceSource
            {
                Kind = ParseKind(VoiceHostProtocol.Str(o, VoiceHostProtocol.FieldKind)),
                Path = VoiceHostProtocol.Str(o, VoiceHostProtocol.FieldPath),
                Speaker = VoiceHostProtocol.Str(o, VoiceHostProtocol.FieldSpeaker),
            };
        }

        internal JObject ToJson()
        {
            var o = new JObject { [VoiceHostProtocol.FieldKind] = NameOf(Kind) };
            VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldPath, Path);
            VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldSpeaker, Speaker);
            return o;
        }

        /// <summary>True when this actually names a voice the host can find. Never a file check —
        /// Core does no IO, and the host is the one standing next to the disk.</summary>
        public bool IsNamed
        {
            get
            {
                switch (Kind)
                {
                    case VoiceSourceKind.Icl:
                    case VoiceSourceKind.Embedding:
                        return !string.IsNullOrWhiteSpace(Path);
                    case VoiceSourceKind.Speaker:
                        return !string.IsNullOrWhiteSpace(Speaker);
                    default:
                        return true;
                }
            }
        }
    }

    // ======================================================================
    // game -> host
    // ======================================================================

    /// <summary>One thing the game asks of the host.</summary>
    public abstract class VoiceHostRequest
    {
        /// <summary>The wire's own name for this op.</summary>
        public abstract string Op { get; }

        /// <summary>The cache key the answer will be filed under. Empty for ops that answer nothing
        /// in particular (<c>ping</c>, <c>quit</c>) — but carried anyway when one is sent, so a reply
        /// can always be addressed.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>This request as one line, without its trailing newline — the transport frames it.</summary>
        public string Serialize() => VoiceHostProtocol.Write(ToJson());

        internal virtual JObject ToJson()
        {
            var o = new JObject { [VoiceHostProtocol.FieldOp] = Op };
            VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldId, Id);
            return o;
        }
    }

    /// <summary>Speak these words into this file.</summary>
    public sealed class VoiceSynthesizeRequest : VoiceHostRequest
    {
        public override string Op => VoiceHostProtocol.OpSynthesize;

        /// <summary>The words themselves, gestures already stripped — see <see cref="SpeakableText"/>.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Absolute path of the WAV to write. Named for the wire's <c>out</c>, which is a
        /// C# keyword and so cannot be the property's own name.</summary>
        public string OutPath { get; set; } = string.Empty;

        public VoiceSource Voice { get; set; } = new VoiceSource();

        public int LanguageId { get; set; } = VoiceHostProtocol.NoLanguage;

        /// <summary>Ask for the reply as ONE file rather than as pieces. The host still streams the
        /// generation — one reading, one voice, and fast — but writes it once at the end. Worth the
        /// wait when a seam would be worse: playing N files means chaining them on the game's tick,
        /// and a frame of silence between every second of speech is heard as the voice breaking up.</summary>
        public bool Whole { get; set; }

        /// <summary>The audio-token ceiling for this line — <see cref="VoiceBudget.MaxTokensFor"/>.
        /// 0 leaves it to the host's own setting. One token is 80 ms of audio, measured, so this is
        /// how many seconds a runaway is allowed before the engine stops it.</summary>
        public int MaxTokens { get; set; }

        /// <summary>
        /// Whether this request can actually be served, and if not, in what words to say so.
        /// <para>
        /// Deliberately NOT part of parsing. A request that arrived wrong still arrived, and the id
        /// on it is someone's waiting line: the host answers it with an honest error rather than
        /// dropping it into a silence the game must eventually time out of.
        /// </para>
        /// </summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Id)) { error = "missing id"; return false; }
            if (string.IsNullOrWhiteSpace(Text)) { error = "empty text"; return false; }
            if (string.IsNullOrWhiteSpace(OutPath)) { error = "missing out path"; return false; }

            var voice = Voice ?? new VoiceSource();
            if (!voice.IsNamed)
            {
                error = voice.Kind == VoiceSourceKind.Speaker
                    ? "voice kind 'speaker' with no speaker name"
                    : "voice kind '" + VoiceSource.NameOf(voice.Kind) + "' with no path";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal override JObject ToJson()
        {
            var o = base.ToJson();
            o[VoiceHostProtocol.FieldText] = Text ?? string.Empty;
            o[VoiceHostProtocol.FieldOut] = OutPath ?? string.Empty;
            o[VoiceHostProtocol.FieldVoice] = (Voice ?? new VoiceSource()).ToJson();
            o[VoiceHostProtocol.FieldLanguageId] = LanguageId;
            o[VoiceHostProtocol.FieldWhole] = Whole;
            if (MaxTokens > 0) o[VoiceHostProtocol.FieldMaxTokens] = MaxTokens;
            return o;
        }
    }

    /// <summary>Never mind that one — if it has not started. A call already inside the DLL cannot be
    /// aborted, so the host finishes it and keeps the file; the words will simply be there next time.</summary>
    public sealed class VoiceCancelRequest : VoiceHostRequest
    {
        public override string Op => VoiceHostProtocol.OpCancel;

        public VoiceCancelRequest() { }
        public VoiceCancelRequest(string id) { Id = id ?? string.Empty; }
    }

    /// <summary>Are you still there? Answered with <see cref="VoicePongEvent"/>.</summary>
    public sealed class VoicePingRequest : VoiceHostRequest
    {
        public override string Op => VoiceHostProtocol.OpPing;
    }

    /// <summary>Stand down.</summary>
    public sealed class VoiceQuitRequest : VoiceHostRequest
    {
        public override string Op => VoiceHostProtocol.OpQuit;
    }

    /// <summary>A well-formed request naming an op this version does not know. It parses precisely
    /// so it can be REFUSED in words — <see cref="VoiceHostProtocol.UnknownOpLine"/> — instead of
    /// vanishing.</summary>
    public sealed class VoiceUnknownRequest : VoiceHostRequest
    {
        private readonly string _op;

        public VoiceUnknownRequest(string op) { _op = op ?? string.Empty; }

        public override string Op => _op;
    }

    // ======================================================================
    // host -> game
    // ======================================================================

    /// <summary>One thing the host says back.</summary>
    public abstract class VoiceHostMessage
    {
        /// <summary>This message as one line, without its trailing newline.</summary>
        public string Serialize() => VoiceHostProtocol.Write(ToJson());

        internal abstract JObject ToJson();
    }

    /// <summary>The models are up and the host can speak. Sent once, at startup.</summary>
    public sealed class VoiceReadyEvent : VoiceHostMessage
    {
        /// <summary>Which device carried it — "CUDA0", "CPU". Shown to the player, never to an NPC.</summary>
        public string Backend { get; set; } = string.Empty;

        /// <summary>The gguf actually loaded. Folded into the cache key, so a model swap never
        /// serves stale audio.</summary>
        public string Model { get; set; } = string.Empty;

        public int Rate { get; set; } = VoiceHostProtocol.DefaultSampleRate;

        internal override JObject ToJson()
        {
            var o = new JObject { [VoiceHostProtocol.FieldEvent] = VoiceHostProtocol.EventReady };
            VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldBackend, Backend);
            VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldModel, Model);
            o[VoiceHostProtocol.FieldRate] = Rate;
            return o;
        }
    }

    /// <summary>Bring-up failed; the host exits after saying so. The player is told plainly and the
    /// game goes on without voices — never the other way round.</summary>
    public sealed class VoiceFailedEvent : VoiceHostMessage
    {
        public string Error { get; set; } = string.Empty;

        public VoiceFailedEvent() { }
        public VoiceFailedEvent(string error) { Error = error ?? string.Empty; }

        internal override JObject ToJson()
        {
            var o = new JObject { [VoiceHostProtocol.FieldEvent] = VoiceHostProtocol.EventFailed };
            VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldError, Error);
            return o;
        }
    }

    /// <summary>Still here.</summary>
    /// <summary>
    /// One piece of a streamed reply is written and can be played now. Several arrive per request,
    /// in order, and the usual result line still closes the request afterwards.
    /// <para>
    /// This is what lets a long reply begin speaking in under a second: the engine makes the whole
    /// reply in ONE generation — which is also what keeps the voice from changing person between
    /// sentences — and hands over each second of it as it is finished.
    /// </para>
    /// </summary>
    public sealed class VoiceChunkEvent : VoiceHostMessage
    {
        public string Id { get; set; } = string.Empty;
        public int Index { get; set; }
        public string Path { get; set; } = string.Empty;
        public long Samples { get; set; }
        public int Rate { get; set; } = VoiceHostProtocol.DefaultSampleRate;

        internal override JObject ToJson()
            => new JObject
            {
                [VoiceHostProtocol.FieldId] = Id,
                [VoiceHostProtocol.FieldChunk] = Index,
                [VoiceHostProtocol.FieldPath] = Path,
                [VoiceHostProtocol.FieldSamples] = Samples,
                [VoiceHostProtocol.FieldRate] = Rate,
            };
    }

    public sealed class VoicePongEvent : VoiceHostMessage
    {
        internal override JObject ToJson()
            => new JObject { [VoiceHostProtocol.FieldEvent] = VoiceHostProtocol.EventPong };
    }

    /// <summary>
    /// How the errand is going: the host fetching the speech engine and its models on the player's
    /// behalf, so that setting voices up is a button rather than a page of instructions.
    /// <para>
    /// Many of these arrive, then exactly one carrying a verdict (<see cref="IsFinished"/>). The
    /// byte counts are for the FILE IN HAND, not the whole errand — see
    /// <see cref="VoiceHostProtocol.FieldTotal"/> for why.
    /// </para>
    /// </summary>
    public sealed class VoiceFetchEvent : VoiceHostMessage
    {
        /// <summary>One of <see cref="VoiceHostProtocol.StageEngine"/> and its siblings. Never
        /// matched on to build a sentence — <see cref="Note"/> is what the player reads — only to
        /// tell the closing line from the rest.</summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>What is happening, in the player's own words.</summary>
        public string Note { get; set; } = string.Empty;

        public long Done { get; set; }
        public long Total { get; set; }

        /// <summary>True only on the closing line, which is the one that carries the verdict.</summary>
        public bool IsFinished => string.Equals(Stage, VoiceHostProtocol.StageDone, StringComparison.OrdinalIgnoreCase);

        public bool Ok { get; set; } = true;
        public string Error { get; set; } = string.Empty;

        /// <summary>How far through the file in hand, 0..1. Zero when the size is not yet known —
        /// a server that sends no length gives an honest bar of nothing rather than a made-up one.</summary>
        public double Fraction
        {
            get
            {
                if (Total <= 0 || Done <= 0) return 0;
                if (Done >= Total) return 1;
                return (double)Done / Total;
            }
        }

        /// <summary>The one line a player sees while this runs. Lives here rather than in the game
        /// so the shape of it is unit-tested: it is on screen for twenty minutes and is the only
        /// thing saying the download has not stalled.</summary>
        public string Describe()
        {
            if (IsFinished) return Ok ? "Done." : (Error.Length > 0 ? Error : "It did not finish.");

            var what = Note.Length > 0 ? Note : Stage;
            if (Total <= 0) return what;

            var percent = (int)Math.Round(Fraction * 100);
            return what + " — " + Megabytes(Done) + " of " + Megabytes(Total) + " (" + percent + "%)";
        }

        private static string Megabytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
                return (bytes / (1024.0 * 1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
            return Math.Round(bytes / (1024.0 * 1024)).ToString("0", CultureInfo.InvariantCulture) + " MB";
        }

        internal override JObject ToJson()
        {
            var o = new JObject { [VoiceHostProtocol.FieldEvent] = VoiceHostProtocol.EventFetch };
            VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldStage, Stage);
            VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldNote, Note);
            if (Done > 0) o[VoiceHostProtocol.FieldDone] = Done;
            if (Total > 0) o[VoiceHostProtocol.FieldTotal] = Total;
            if (IsFinished)
            {
                o[VoiceHostProtocol.FieldOk] = Ok;
                VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldError, Error);
            }
            return o;
        }
    }

    /// <summary>The answer to one synthesize request, well or badly.</summary>
    public sealed class VoiceSynthesisResult : VoiceHostMessage
    {
        /// <summary>The key it was asked under. Can be empty for a refusal that had no id to echo
        /// (a malformed request, an unknown op) — then it is nobody's waiting line, only a log entry.</summary>
        public string Id { get; set; } = string.Empty;

        public bool Ok { get; set; }

        /// <summary>Where the WAV was written. Empty on failure.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>How long the synthesis took, in milliseconds.</summary>
        public int Ms { get; set; }

        /// <summary>How many samples were written. A long because it costs nothing and an hour of
        /// audio is nobody's business to overflow on.</summary>
        public long Samples { get; set; }

        public int Rate { get; set; } = VoiceHostProtocol.DefaultSampleRate;

        /// <summary>Why not, in plain words. Empty on success.</summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// True when the host cut this generation short because it was producing far more audio than
        /// the words could justify. It is NOT a failure: what was made before the runaway is good
        /// speech and worth playing — every derail measured begins correctly and drifts later. It is
        /// a "do not keep this", and the cache is where that must be obeyed.
        /// </summary>
        public bool Derailed { get; set; }

        public static VoiceSynthesisResult Success(string id, string path, int ms, long samples,
                                                   int rate = VoiceHostProtocol.DefaultSampleRate,
                                                   bool derailed = false)
            => new VoiceSynthesisResult
            {
                Id = id ?? string.Empty,
                Ok = true,
                Path = path ?? string.Empty,
                Ms = ms,
                Samples = samples,
                Rate = rate,
                Derailed = derailed,
            };

        public static VoiceSynthesisResult Failure(string id, string error)
            => new VoiceSynthesisResult
            {
                Id = id ?? string.Empty,
                Ok = false,
                Error = error ?? string.Empty,
            };

        internal override JObject ToJson()
        {
            var o = new JObject();
            VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldId, Id);
            o[VoiceHostProtocol.FieldOk] = Ok;

            if (Ok)
            {
                VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldPath, Path);
                o[VoiceHostProtocol.FieldMs] = Ms;
                o[VoiceHostProtocol.FieldSamples] = Samples;
                o[VoiceHostProtocol.FieldRate] = Rate;
                if (Derailed) o[VoiceHostProtocol.FieldDerailed] = true;
            }
            else
            {
                VoiceHostProtocol.Put(o, VoiceHostProtocol.FieldError, Error);
            }

            return o;
        }
    }
}
