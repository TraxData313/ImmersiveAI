using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ImmersiveAI.VoiceHost;

/// <summary>
/// The errand: fetch the speech engine and its models so the player never has to.
/// <para>
/// This used to be a page of instructions — install Qwen-TTS Studio, open it, find the model list,
/// download the right one, come back. Every step of that is a step somebody stops at, and none of
/// it was ever load-bearing: the mod has never needed Studio INSTALLED, never launches it, and
/// never reads its window. It needs eight native DLLs out of that package and two .gguf files from
/// Hugging Face. So we take exactly those, and the player presses one button.
/// </para>
/// <para>
/// It runs in the HOST rather than in the game for the same reason the engine does: 2.8 GB of
/// streaming download and a 632 MB unzip are not things to do inside a process holding somebody's
/// campaign. The host loads no native library in this mode — <see cref="Loader"/> is never called —
/// so the errand cannot fall over the way the engine can.
/// </para>
/// <para>
/// Everything resumes. A download that dies at 2 GB leaves a <c>.part</c> file and picks up from
/// there, and only a file whose bytes are all present is ever given its real name — which is what
/// makes "does it exist?" a sufficient test for "is it whole?" on the next run.
/// </para>
/// </summary>
public static class Fetcher
{
    /// <summary>The Studio release the engine DLLs are taken out of. A version, not "latest": a
    /// release whose layout changed under us should fail on a version we can look at, rather than
    /// silently on whatever shipped this morning.</summary>
    public const string StudioVersion = "0.2.9";

    /// <summary>The bundled build, always. The smaller <c>cuda-system</c> package expects NVIDIA's
    /// CUDA toolkit to be on the machine already, and installing THAT wants administrator rights —
    /// which is the whole thing this errand exists to avoid.</summary>
    private const string StudioBuild = "cuda-bundled";

    private const string ModelBase = "https://huggingface.co/Serveurperso/Qwen3-TTS-GGUF/resolve/main";

    /// <summary>Both are needed: the talker turns text into audio tokens, the tokenizer turns those
    /// back into sound. It must be the <b>1.7b</b> talker — model size fixes the embedding dimension
    /// (1.7b gives d2048, 0.6b gives d1024), and every voice the mod ships is d2048. The smaller
    /// model is not a lighter fallback; it is a different shelf that none of our voices load on.</summary>
    private static readonly (string File, string Note)[] Models =
    {
        ("qwen-talker-1.7b-base-Q8_0.gguf", "the voice model"),
        ("qwen-tokenizer-12hz-Q8_0.gguf", "the sound model"),
    };

    /// <summary>Roughly what lands on disk, for the free-space check. Deliberately generous.</summary>
    private const long EngineBytes = 700L * 1024 * 1024;
    private const long ModelBytes = 2300L * 1024 * 1024;
    private const long ZipBytes = 700L * 1024 * 1024;

    /// <summary>No bytes for this long and the connection is dead rather than slow. Long enough to
    /// survive a Hugging Face hiccup, short enough that a stalled errand does not look like a
    /// working one until the player gives up on it.</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(90);

    private const int Attempts = 3;

    public static int Run(HostOptions opts)
    {
        string engineDir = string.IsNullOrWhiteSpace(opts.EngineDir) ? DefaultEngineDir() : opts.EngineDir!;
        string modelDir = string.IsNullOrWhiteSpace(opts.ModelDir) ? DefaultModelDir() : opts.ModelDir!;

        HostLog.Info($"fetch: engine -> {engineDir}, models -> {modelDir}");

        try
        {
            Wire.Fetch(Protocol.StageChecking, "Looking at what is already here", 0, 0);

            bool needEngine = !File.Exists(Path.Combine(engineDir, Loader.MainLibrary));
            var needModels = Models.Where(m => !File.Exists(Path.Combine(modelDir, m.File))).ToArray();

            if (!needEngine && needModels.Length == 0)
            {
                HostLog.Info("fetch: nothing to do, it is all here");
                Wire.FetchDone(true, null);
                return 0;
            }

            CheckRoom(engineDir, modelDir, needEngine, needModels.Length);

            if (needEngine) FetchEngine(engineDir);
            else HostLog.Info("fetch: the engine is already here, skipped");

            foreach (var model in needModels)
                FetchModel(modelDir, model.File, model.Note);

            // Say plainly that it worked only if it actually did. Everything above throws on
            // failure, but a missing file after a "successful" run would send the player back to
            // the setup page with no idea why.
            string mainLib = Path.Combine(engineDir, Loader.MainLibrary);
            if (!File.Exists(mainLib))
                throw new FileNotFoundException("the engine is still not in " + engineDir);
            foreach (var model in Models)
                if (!File.Exists(Path.Combine(modelDir, model.File)))
                    throw new FileNotFoundException("the model " + model.File + " is still not in " + modelDir);

            HostLog.Info("fetch: done");
            Wire.FetchDone(true, null);
            return 0;
        }
        catch (Exception ex)
        {
            HostLog.Error("fetch failed", ex);
            Wire.FetchDone(false, Explain(ex));
            return 4;
        }
    }

    // ---------------------------------------------------------------- the engine

    private static void FetchEngine(string engineDir)
    {
        string url = $"https://github.com/Danmoreng/qwen-tts-studio/releases/download/v{StudioVersion}/" +
                     $"qwen-tts-studio-{StudioVersion}-windows-{StudioBuild}.zip";

        string zip = Path.Combine(Path.GetTempPath(), $"immersiveai-qwen-tts-studio-{StudioVersion}-{StudioBuild}.zip");
        Download(url, zip, Protocol.StageEngine, "The speech engine");
        Unpack(zip, engineDir);

        // 632 MB of temp that will never be wanted again: the DLLs are out and named.
        try { File.Delete(zip); } catch { /* a leftover in TEMP is Windows' problem, not the player's */ }
    }

    /// <summary>
    /// Takes the native libraries out of the package and leaves the rest of it behind.
    /// <para>
    /// The package is a whole Java application — its own JRE, its jars, its launcher — of which we
    /// want the eight DLLs sitting beside the exe. Measured on 0.2.9: 662 MB of engine inside an
    /// 833 MB folder, so skipping the app spares the player 171 MB and, far more usefully, means
    /// nothing that could be mistaken for an installed program lands on their disk.
    /// </para>
    /// <para>
    /// Which entries those are is worked out from <c>qwen3_tts.dll</c> rather than from a list of
    /// names: whatever folder inside the zip holds the main library IS the engine folder — the same
    /// rule the game's own discovery uses — and its siblings at that level come with it. A package
    /// that one day wraps everything one directory deeper therefore still unpacks correctly.
    /// </para>
    /// </summary>
    private static void Unpack(string zipPath, string engineDir)
    {
        Wire.Fetch(Protocol.StageUnpacking, "Unpacking the speech engine", 0, 0);
        Directory.CreateDirectory(engineDir);

        using var zip = ZipFile.OpenRead(zipPath);

        var main = zip.Entries.FirstOrDefault(e =>
            string.Equals(e.Name, Loader.MainLibrary, StringComparison.OrdinalIgnoreCase));
        if (main is null)
            throw new InvalidDataException("this package holds no " + Loader.MainLibrary +
                                           " — it is not the build the voices are made with");

        // "qwen-tts-studio/qwen3_tts.dll" -> "qwen-tts-studio/", or "" when it sits at the root.
        string prefix = main.FullName.Substring(0, main.FullName.Length - main.Name.Length);

        var wanted = zip.Entries.Where(e =>
            e.Name.Length > 0
            && e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            && e.FullName.Length == prefix.Length + e.Name.Length
            && e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

        long total = wanted.Sum(e => e.Length);
        long done = 0;
        HostLog.Info($"fetch: unpacking {wanted.Count} libraries ({total / (1024 * 1024)} MB) into {engineDir}");

        foreach (var entry in wanted)
        {
            string dest = Path.Combine(engineDir, entry.Name);
            // Written beside and renamed, so an interrupted unpack cannot leave a half-DLL behind
            // wearing the name of a whole one — the loader would fail on it with nothing to say.
            string part = dest + ".part";
            entry.ExtractToFile(part, overwrite: true);
            File.Move(part, dest, overwrite: true);

            done += entry.Length;
            Wire.Fetch(Protocol.StageUnpacking, "Unpacking the speech engine", done, total);
            HostLog.Info($"  [unpack] {entry.Name}  ({entry.Length / (1024 * 1024)} MB)");
        }
    }

    // ---------------------------------------------------------------- the models

    private static void FetchModel(string modelDir, string file, string note)
        => Download(ModelBase + "/" + file, Path.Combine(modelDir, file), Protocol.StageModel, note);

    // ---------------------------------------------------------------- the wire itself

    /// <summary>
    /// Streams one file to disk, resuming a part-finished one rather than starting again.
    /// <para>
    /// Two and a half gigabytes does not always arrive on the first attempt, and beginning again
    /// from zero is a poor answer to that — so the bytes go to <c>&lt;dest&gt;.part</c> with a Range
    /// header naming what is already there, and only a complete file is given its real name.
    /// </para>
    /// </summary>
    private static void Download(string url, string dest, string stage, string note)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        string part = dest + ".part";

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                DownloadOnce(url, dest, part, stage, note);
                return;
            }
            catch (Exception ex) when (attempt < Attempts && IsWorthRetrying(ex))
            {
                // The part-file is deliberately left where it is: the next attempt is a resume,
                // which is the entire reason this is written to a part-file at all.
                HostLog.Warn($"fetch: {note} attempt {attempt} failed ({ex.Message}); resuming");
                Thread.Sleep(TimeSpan.FromSeconds(2 * attempt));
            }
        }
    }

    private static void DownloadOnce(string url, string dest, string part, string stage, string note)
    {
        long from = File.Exists(part) ? new FileInfo(part).Length : 0;

        using var http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.None,   // a .gguf is not to be re-encoded
            AllowAutoRedirect = true,                             // GitHub and HF both redirect to a CDN
        })
        {
            // The default 100 seconds covers the WHOLE response, body included, which for a 2 GB
            // file is a guaranteed failure. The stall watchdog below is the real timeout.
            Timeout = Timeout.InfiniteTimeSpan,
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ImmersiveAI-VoiceHost/1.0");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (from > 0) request.Headers.Range = new RangeHeaderValue(from, null);

        using var response = http.Send(request, HttpCompletionOption.ResponseHeadersRead);

        // 416 means the part-file already holds the whole thing: the server has nothing past that
        // offset to send. That is a finished download wearing the wrong name.
        if (from > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Move(part, dest, overwrite: true);
            HostLog.Info($"fetch: {note} was already complete");
            return;
        }

        // A server that ignores the Range header answers 200 and starts from the beginning; keeping
        // our offset then would splice the file's head into its middle.
        if (from > 0 && response.StatusCode == HttpStatusCode.OK)
        {
            HostLog.Warn($"fetch: {note} cannot resume here, starting again");
            from = 0;
            try { File.Delete(part); } catch { }
        }

        response.EnsureSuccessStatusCode();

        long total = (response.Content.Headers.ContentLength ?? 0) + from;
        HostLog.Info($"fetch: {note} — {(from > 0 ? "resuming at " + from / (1024 * 1024) + " MB of " : "")}{total / (1024 * 1024)} MB");

        using (var input = response.Content.ReadAsStream())
        using (var output = new FileStream(part, FileMode.Append, FileAccess.Write, FileShare.None, 1 << 20))
        {
            var buffer = new byte[1 << 20];
            long done = from;
            var lastSaid = DateTime.UtcNow;
            Wire.Fetch(stage, note, done, total);

            while (true)
            {
                int read;
                using (var stall = new CancellationTokenSource(StallTimeout))
                {
                    try { read = input.ReadAsync(buffer, stall.Token).AsTask().GetAwaiter().GetResult(); }
                    catch (OperationCanceledException)
                    {
                        throw new IOException("the download stopped sending for " +
                                              (int)StallTimeout.TotalSeconds + " seconds");
                    }
                }
                if (read <= 0) break;

                output.Write(buffer, 0, read);
                done += read;

                // Two a second at most: this line crosses a pipe and is drawn on a live UI, and a
                // progress report per megabyte of a 2 GB file is eight thousand of them.
                if ((DateTime.UtcNow - lastSaid).TotalMilliseconds >= 500)
                {
                    lastSaid = DateTime.UtcNow;
                    Wire.Fetch(stage, note, done, total);
                }
            }

            output.Flush(true);

            if (total > 0 && done != total)
                throw new IOException($"{note}: got {done} bytes of {total}");
        }

        File.Move(part, dest, overwrite: true);
        HostLog.Info($"fetch: {note} done");
        Wire.Fetch(stage, note, total, total);
    }

    /// <summary>A dropped connection is worth another go; a 404 or a full disk is not.</summary>
    private static bool IsWorthRetrying(Exception ex)
        => ex is IOException or HttpRequestException or TaskCanceledException or OperationCanceledException
           && ex is not { InnerException: IOException { HResult: -2147024784 } };   // ERROR_DISK_FULL

    // ---------------------------------------------------------------- housekeeping

    /// <summary>
    /// Refuses before it starts rather than at 90%. A download that fills the disk is worse than one
    /// that never began — it takes the rest of the machine down with it, and Bannerlord is running.
    /// </summary>
    private static void CheckRoom(string engineDir, string modelDir, bool needEngine, int modelsWanted)
    {
        var wanted = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        void Want(string path, long bytes)
        {
            string root = RootOf(path);
            if (root.Length == 0 || bytes <= 0) return;
            wanted[root] = (wanted.TryGetValue(root, out var already) ? already : 0) + bytes;
        }

        if (needEngine)
        {
            Want(engineDir, EngineBytes);
            Want(Path.GetTempPath(), ZipBytes);
        }
        if (modelsWanted > 0) Want(modelDir, ModelBytes * modelsWanted / Models.Length);

        foreach (var pair in wanted)
        {
            long free;
            try { free = new DriveInfo(pair.Key).AvailableFreeSpace; }
            catch { continue; }   // a drive that will not answer is not one to refuse over

            if (free >= pair.Value) continue;

            throw new IOException(
                $"there is not enough room on {pair.Key.TrimEnd('\\')} — " +
                $"{pair.Value / (1024 * 1024 * 1024.0):0.0} GB is needed and {free / (1024 * 1024 * 1024.0):0.0} GB is free");
        }
    }

    private static string RootOf(string path)
    {
        try { return Path.GetPathRoot(Path.GetFullPath(path)) ?? string.Empty; }
        catch { return string.Empty; }
    }

    /// <summary>Where the engine goes when nobody says otherwise: the same folder Qwen-TTS Studio's
    /// own installer uses, which is already a first-class place the game looks — and is shared with
    /// anyone who fetched it for some other tool, so this errand skips it entirely.</summary>
    public static string DefaultEngineDir()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Programs", "qwen-tts-studio");

    /// <summary>And Studio's own model folder, for the same reason.</summary>
    public static string DefaultModelDir()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".qwen-tts-studio", "models");

    /// <summary>What went wrong, in words a player can act on. The .NET text for a dead network is
    /// two sentences of type names, and this line is the only thing they will see.</summary>
    private static string Explain(Exception ex)
    {
        return ex switch
        {
            HttpRequestException http when http.StatusCode == HttpStatusCode.NotFound
                => "the download is no longer where it was — the mod's setup page has the manual road",
            HttpRequestException
                => "the download could not be reached. Check the connection and try again — it carries on from where it stopped",
            IOException io when io.Message.Contains("room", StringComparison.OrdinalIgnoreCase)
                => io.Message,
            IOException io
                => io.Message + ". Trying again carries on from where it stopped",
            UnauthorizedAccessException
                => "Windows would not let the files be written. Try a folder inside your own user folder",
            _ => ex.Message,
        };
    }
}
