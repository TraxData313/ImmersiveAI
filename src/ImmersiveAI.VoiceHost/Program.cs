using System.Text;
using System.Text.Json;

namespace ImmersiveAI.VoiceHost;

/// <summary>
/// The voice sidecar. It exists for one reason: ggml's failure mode is
/// GGML_ASSERT -> abort(), which on the game's runtime is uncatchable and
/// fail-fast. Out here a crash costs the voices for a session; in the game's own
/// process it would cost the player's campaign.
///
/// It speaks newline-delimited JSON on stdin/stdout - one object per line, UTF-8,
/// never a newline inside a message - and serves requests strictly one at a time,
/// because the engine is not re-entrant.
/// </summary>
public static class Program
{
    private const int MaxQueued = 64;

    private static readonly object Gate = new();
    private static readonly LinkedList<SynthRequest> Queue = new();
    private static bool _stopping;
    private static string? _running;          // the id currently inside the DLL
    private static TtsEngine? _engine;

    public static int Main(string[] argv)
    {
        var opts = HostOptions.Parse(argv);
        HostLog.Init(opts.LogPath);
        HostLog.Info("options: " + opts);

        // Before ANY native library is loaded: take stdout for the protocol and
        // point everyone else's stdout at NUL.
        Wire.Init(StdoutGuard.Install(opts.KeepStderr));

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            HostLog.Error("UNHANDLED: " + (e.ExceptionObject?.ToString() ?? "(null)"));

        // If the game goes, we go. This is the first thing armed and the last
        // thing that can be argued with.
        Watchdog.WatchParent(opts.ParentPid);

        var engine = new TtsEngine();
        try
        {
            var loc = EngineDiscovery.Locate(opts);
            engine.Bring(loc, opts);
        }
        catch (Exception ex)
        {
            HostLog.Error("bring-up failed", ex);
            Wire.Failed(ex.Message);
            try { engine.Dispose(); } catch { /* nothing to save */ }
            return 3;
        }

        _engine = engine;
        Wire.Ready(engine.BackendName, engine.ModelName, engine.Rate);
        HostLog.Info($"ready: backend={engine.BackendName} model={engine.ModelName} rate={engine.Rate}");

        var worker = new Thread(WorkerLoop) { IsBackground = true, Name = "synth" };
        worker.Start();

        bool quitAsked = ReadLoop();

        lock (Gate)
        {
            _stopping = true;
            if (quitAsked)
            {
                // "quit" is an order: finish what is inside the DLL, drop the rest -
                // but answer each dropped one, so nothing is left waiting on a
                // voice that will never come.
                foreach (var dropped in Queue)
                {
                    HostLog.Info("dropped on quit: " + dropped.Id);
                    Wire.Fail(dropped.Id, "the host is shutting down");
                }
                Queue.Clear();
            }
            // stdin EOF is not an order, only a closed door: let the queue drain,
            // bounded by the join below, so a request sent just before the pipe
            // closed is not silently lost.
            Monitor.PulseAll(Gate);
        }

        if (!worker.Join(TimeSpan.FromSeconds(60)))
            HostLog.Warn("the engine was still speaking at shutdown; leaving it behind");

        engine.Dispose();
        HostLog.Info("clean exit");
        return 0;
    }

    // ------------------------------------------------------------- the loop

    /// <summary>Reads stdin until EOF or "quit". Returns true if "quit" was asked.</summary>
    private static bool ReadLoop()
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false),
                                            detectEncodingFromByteOrderMarks: false, bufferSize: 8192);
        while (true)
        {
            string? line;
            try { line = reader.ReadLine(); }
            catch (Exception ex) { HostLog.Error("stdin read failed", ex); return false; }

            if (line is null) { HostLog.Info("stdin closed"); return false; }

            // A UTF-8 BOM on the first line is what almost every default text
            // writer does, and it would otherwise cost the game its very first
            // message with no way to see why. Eat it, and any zero-width kin.
            line = line.Trim(' ', '\t', '\r', '\n', '\uFEFF', '\u200B');
            if (line.Length == 0) continue;

            try
            {
                if (Handle(line)) return true;
            }
            catch (Exception ex)
            {
                // A bad line is never fatal. Ever.
                HostLog.Error("line could not be served: " + Wire.Preview(line), ex);
            }
        }
    }

    /// <summary>Serves one line. Returns true when the line was "quit".</summary>
    private static bool Handle(string line)
    {
        using var doc = Wire.TryParse(line);
        if (doc is null) return false;

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            HostLog.Warn("line is not an object, skipped: " + Wire.Preview(line));
            return false;
        }

        string? id = Wire.Str(root, "id");
        string? op = Wire.Str(root, "op")?.Trim().ToLowerInvariant();

        switch (op)
        {
            case "synthesize":
                Enqueue(root, id);
                return false;

            case "cancel":
                Cancel(id);
                return false;

            case "ping":
                Wire.Pong();
                return false;

            case "quit":
                HostLog.Info("quit asked");
                return true;

            case null:
                HostLog.Warn("line carries no op, skipped: " + Wire.Preview(line));
                Wire.Fail(id, "unknown op");
                return false;

            default:
                HostLog.Warn($"unknown op '{op}'");
                Wire.Fail(id, "unknown op");
                return false;
        }
    }

    private static void Enqueue(JsonElement root, string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) { Wire.Fail(null, "the request carries no id"); return; }

        string? text = Wire.Str(root, "text");
        string? outPath = Wire.Str(root, "out");
        if (string.IsNullOrWhiteSpace(text)) { Wire.Fail(id, "there is nothing to say (empty text)"); return; }
        if (string.IsNullOrWhiteSpace(outPath)) { Wire.Fail(id, "the request carries no output path"); return; }

        var req = new SynthRequest
        {
            Id = id!,
            Text = text!,
            OutPath = outPath!,
            LanguageId = Wire.Int(root, "languageId", -1),
            Whole = Wire.Bool(root, "whole", false),
            MaxTokens = Wire.Int(root, "maxTokens", 0),
        };

        if (root.TryGetProperty("voice", out var voice) && voice.ValueKind == JsonValueKind.Object)
        {
            req.VoicePath = Wire.Str(voice, "path");
            req.Speaker = Wire.Str(voice, "speaker");
            req.Kind = Wire.Str(voice, "kind")?.Trim().ToLowerInvariant() switch
            {
                "icl" => VoiceKind.Icl,
                "embedding" => VoiceKind.Embedding,
                "speaker" => VoiceKind.Speaker,
                "default" => VoiceKind.Default,
                _ => Infer(req),
            };
        }

        lock (Gate)
        {
            if (_stopping) { Wire.Fail(id, "the host is shutting down"); return; }
            if (Queue.Count >= MaxQueued)
            {
                Wire.Fail(id, "too many voices are waiting already");
                HostLog.Warn($"queue full ({Queue.Count}), refused {id}");
                return;
            }
            Queue.AddLast(req);
            HostLog.Info($"queued ({Queue.Count} waiting): {req}");
            Monitor.PulseAll(Gate);
        }
    }

    /// <summary>A voice that named no kind is read by what it actually carries.</summary>
    private static VoiceKind Infer(SynthRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.VoicePath))
        {
            string n = Path.GetFileName(req.VoicePath!).ToLowerInvariant();
            return n.Contains("embedding") ? VoiceKind.Embedding : VoiceKind.Icl;
        }
        return string.IsNullOrWhiteSpace(req.Speaker) ? VoiceKind.Default : VoiceKind.Speaker;
    }

    private static void Cancel(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) { HostLog.Warn("cancel with no id"); return; }

        lock (Gate)
        {
            for (var node = Queue.First; node is not null; node = node.Next)
            {
                if (!string.Equals(node.Value.Id, id, StringComparison.Ordinal)) continue;
                Queue.Remove(node);
                HostLog.Info("cancelled while waiting: " + id);
                // Answer it, so the game never waits on a voice that will not come.
                Wire.Fail(id, "cancelled");
                return;
            }

            if (string.Equals(_running, id, StringComparison.Ordinal))
            {
                // A call already inside the DLL cannot be aborted. It finishes,
                // the file is kept, and the ordinary reply follows.
                HostLog.Info("cancel came too late, already speaking: " + id);
                return;
            }
        }
        HostLog.Info("cancel for something not waiting: " + id);
    }

    // ----------------------------------------------------------- the engine

    private static void WorkerLoop()
    {
        while (true)
        {
            SynthRequest req;
            lock (Gate)
            {
                while (Queue.Count == 0 && !_stopping) Monitor.Wait(Gate);
                if (Queue.Count == 0) { HostLog.Info("worker done"); return; }
                req = Queue.First!.Value;
                Queue.RemoveFirst();
                _running = req.Id;
            }

            SynthOutcome outcome;
            try
            {
                // Streamed, always: one generation for the whole reply. Sentence-by-sentence
                // synthesis made the voice change person at every seam and cost ten times the
                // time, because it re-paid the prefill and the speaker encode for each sentence.
                // Each piece is announced the moment it is written, so the game can begin speaking
                // while the rest is still being made.
                void Announce(int index, string path, int samples)
                {
                    try { Wire.Chunk(req.Id, index, path, samples, _engine!.Rate); }
                    catch (Exception ex) { HostLog.Error("could not announce a piece of " + req.Id, ex); }
                }

                // A WHOLE reply has not been heard by anybody until it is written, so a runaway on
                // the first go costs nothing to throw away and try again: at ~3x realtime a second
                // attempt is a second or two, and the player never learns the first one happened.
                // A STREAMED reply cannot do this - its pieces are already in the air - so it keeps
                // what it has and simply stops. Either way the clip is marked so the game does not
                // cache it: one bad synthesis replayed forever is the bug worth avoiding.
                outcome = _engine!.SynthesizeStreaming(req, Announce, keepDerailed: !req.Whole);
                if (outcome.Derailed && !outcome.Ok && req.Whole)
                {
                    HostLog.Warn("the reading ran away, trying once more: " + req.Id);
                    outcome = _engine!.SynthesizeStreaming(req, Announce, keepDerailed: true);
                }
            }
            catch (Exception ex)
            {
                // TtsEngine already swallows its own; this is the belt to that
                // pair of braces.
                HostLog.Error("the worker caught what the engine let through", ex);
                outcome = new SynthOutcome { Ok = false, Error = ex.Message };
            }
            finally
            {
                lock (Gate) _running = null;
            }

            if (outcome.Ok) Wire.Ok(req.Id, outcome.Path, outcome.Ms, outcome.Samples, outcome.Rate, outcome.Derailed);
            else
            {
                HostLog.Warn($"failed {req.Id}: {outcome.Error}");
                Wire.Fail(req.Id, outcome.Error);
            }
        }
    }
}
