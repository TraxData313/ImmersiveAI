using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// What the voice engines ask of a computer, and what THIS computer has — so the Voices page can
    /// say "this one is right for your PC" before anything is downloaded.
    /// <para>
    /// A mirror of claude-voice's own setup (setup-app\Machine.cs): the same thresholds, measured on
    /// real cards, so the game and the installer can never disagree about a machine. The graphics
    /// card is read once per session, off the game thread, through nvidia-smi — half a second the
    /// page must never wait for.
    /// </para>
    /// </summary>
    public static class VoiceMachine
    {
        public sealed class Engine
        {
            public string Id = "";
            public string Name = "";
            public string Role = "";
            public string Tagline = "";
            public string Languages = "";
            public string Needs = "";
            public string VoicesNote = "";
            public double DownloadGb;
            public double DiskGb;
            /// <summary>Graphics memory held while it runs; 0 = the processor does the work.</summary>
            public double GpuGb;
            public bool EnglishOnly;
            public bool NeedsNvidia;
        }

        public static readonly Engine Breeze = new Engine
        {
            Id = "breeze", Name = "Breeze", Role = "the actor",
            Tagline = "Laughs, sighs and whispers where the words call for it. The most alive of the three.",
            Languages = "English only",
            Needs = "NVIDIA card with 16 GB (RTX 30-series or newer)",
            VoicesNote = "All the voices of Calradia",
            DownloadGb = 11, DiskGb = 14, GpuGb = 13, EnglishOnly = true, NeedsNvidia = true,
        };

        public static readonly Engine Qwen = new Engine
        {
            Id = "qwen", Name = "Qwen", Role = "the storyteller",
            Tagline = "Warm, natural voices that read any language — Bulgarian, Russian, Chinese and the rest.",
            Languages = "Every language",
            Needs = "NVIDIA card with 4 GB or more",
            VoicesNote = "All the voices of Calradia",
            DownloadGb = 3, DiskGb = 3.5, GpuGb = 3.5, NeedsNvidia = true,
        };

        public static readonly Engine Pocket = new Engine
        {
            Id = "pocket", Name = "Pocket", Role = "the light one",
            Tagline = "Runs on any computer, no graphics card at all — and leaves the graphics card to the game.",
            Languages = "English, or one of French, German, Italian, Portuguese, Spanish",
            Needs = "Any Windows PC",
            VoicesNote = "Its own English voices (the Calradian ones need Qwen or Breeze)",
            DownloadGb = 1, DiskGb = 1.5, GpuGb = 0,
        };

        public static readonly Engine[] All = { Breeze, Qwen, Pocket };

        public static Engine? ById(string? id)
            => All.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

        // ------------------------------------------------------------------ the graphics card

        public sealed class Gpu
        {
            public string Name = "";
            public int VramMiB;
            public double Compute;
            public double Cuda;
            public double VramGb => VramMiB / 1024.0;
        }

        private static Gpu? _gpu;
        private static int _probe;           // 0 not asked, 1 asking, 2 answered
        public static bool GpuKnown => _probe == 2;
        public static Gpu? Card => _gpu;

        /// <summary>Starts the one look at the card; the page redraws when it lands.</summary>
        public static void Probe(Action? whenKnown = null)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _probe, 1, 0) != 0) return;
            Task.Run(() =>
            {
                try { _gpu = DetectNvidia(); }
                catch { _gpu = null; }
                _probe = 2;
                if (whenKnown != null) MainThreadDispatcher.Enqueue(whenKnown);
            });
        }

        public static bool HasNvidiaDriver =>
            File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvcuda.dll"));

        /// <summary>"NVIDIA GeForce RTX 4080 (16 GB)", or a plain sentence when there is none.</summary>
        public static string Describe()
        {
            if (!GpuKnown) return "Looking at your graphics card…";
            if (_gpu != null) return $"{_gpu.Name} ({Math.Round(_gpu.VramGb)} GB)";
            return HasNvidiaDriver ? "an NVIDIA card whose details could not be read" : "no NVIDIA graphics card";
        }

        private static Gpu? DetectNvidia()
        {
            var smi = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
            }.FirstOrDefault(File.Exists);
            if (smi == null) return null;

            Gpu? best = null;
            foreach (var line in Run(smi, "--query-gpu=name,memory.total,compute_cap --format=csv,noheader,nounits")
                         .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;
                var gpu = new Gpu { Name = parts[0].Trim() };
                int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out gpu.VramMiB);
                if (parts.Length > 2)
                    double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out gpu.Compute);
                if (best == null || gpu.VramMiB > best.VramMiB) best = gpu;
            }
            if (best == null) return null;
            var m = Regex.Match(Run(smi, ""), @"CUDA Version:\s*([0-9]+\.[0-9]+)");
            if (m.Success) double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out best.Cuda);
            return best;
        }

        private static string Run(string exe, string args)
        {
            using (var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            }))
            {
                if (p == null) return string.Empty;
                var text = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10_000);
                return text;
            }
        }

        // ------------------------------------------------------------------ does it fit

        public enum Fit { Great, Slow, No }

        public sealed class Verdict
        {
            public Fit Fit;
            public string Why = "";
            /// <summary>Refused for want of room on the drive, not for want of hardware: the page says which drive.</summary>
            public bool NoRoom;
        }

        // setup-app\Machine.cs's own numbers — keep the two together.
        private const int BreezeNeedMiB = 11_500;
        private const int BreezeFullSpeedMiB = 15_500;
        private const double BreezeNeedCompute = 8.0;
        private const double BreezeNeedCuda = 12.8;
        private const int QwenNeedMiB = 3_500;

        public static Verdict Judge(Engine engine, double freeGb)
        {
            var disk = DiskGbFor(engine);
            if (freeGb > 0 && freeGb < disk + 1)
                return new Verdict { Fit = Fit.No, NoRoom = true, Why = $"Needs about {Gb(disk)} free on the drive you pick." };
            if (engine == Pocket) return new Verdict { Fit = Fit.Great };

            var nvidia = _gpu != null || HasNvidiaDriver;
            if (!nvidia) return new Verdict { Fit = Fit.No, Why = "Needs an NVIDIA graphics card — none was found." };

            if (engine == Qwen)
            {
                if (_gpu != null && _gpu.VramMiB > 0 && _gpu.VramMiB < QwenNeedMiB)
                    return new Verdict { Fit = Fit.Slow, Why = "Your card is small for it — it may not fit beside the game." };
                return new Verdict { Fit = Fit.Great };
            }

            if (_gpu == null) return new Verdict { Fit = Fit.No, Why = "Could not read your card's memory — Breeze needs 12 GB or more." };
            if (_gpu.Compute > 0 && _gpu.Compute < BreezeNeedCompute) return new Verdict { Fit = Fit.No, Why = "Needs an RTX 30-series card or newer." };
            if (_gpu.VramMiB < BreezeNeedMiB) return new Verdict { Fit = Fit.No, Why = $"Needs 12 GB of graphics memory — yours has {Math.Round(_gpu.VramGb)} GB." };
            if (_gpu.Cuda > 0 && _gpu.Cuda < BreezeNeedCuda) return new Verdict { Fit = Fit.No, Why = "Your graphics driver is too old for it — update it from nvidia.com first." };
            if (_gpu.VramMiB < BreezeFullSpeedMiB) return new Verdict { Fit = Fit.Slow, Why = "Fits, but on a card under 16 GB it speaks a little slower." };
            return new Verdict { Fit = Fit.Great };
        }

        /// <summary>The best engine this computer runs well, for the language the characters will speak.</summary>
        public static Engine Recommend(double freeGb, bool english)
        {
            if (english && Judge(Breeze, freeGb).Fit == Fit.Great) return Breeze;
            if (Judge(Qwen, freeGb).Fit == Fit.Great) return Qwen;
            return Pocket;
        }

        // ------------------------------------------------------------------ what is here already

        /// <summary>
        /// Qwen's model, if it is already where Qwen-TTS Studio keeps it. An earlier version of this
        /// mod fetched exactly these two files into exactly this folder, and claude-voice's setup uses
        /// them where they lie rather than downloading 2.2 GB a second time — so a player coming from
        /// the old built-in voices is shown the download they will really make.
        /// </summary>
        public static readonly string[] QwenModelFiles = { "qwen-talker-1.7b-base-Q8_0.gguf", "qwen-tokenizer-12hz-Q8_0.gguf" };
        public const double QwenModelGb = 2.2;

        public static string UsualQwenModels =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qwen-tts-studio", "models");

        public static bool QwenModelHere
        {
            get
            {
                try { return QwenModelFiles.All(f => File.Exists(Path.Combine(UsualQwenModels, f))); }
                catch { return false; }
            }
        }

        /// <summary>What installing it would really fetch, with what is already here taken off.</summary>
        public static double DownloadGbFor(Engine e) => e == Qwen && QwenModelHere ? e.DownloadGb - QwenModelGb : e.DownloadGb;

        /// <summary>And what it would put on the drive the player picks.</summary>
        public static double DiskGbFor(Engine e) => e == Qwen && QwenModelHere ? e.DiskGb - QwenModelGb : e.DiskGb;

        // ------------------------------------------------------------------ where the files go

        public sealed class Drive
        {
            public string Root = "";       // "C:\"
            public string Label = "";      // "C:"
            public double FreeGb;
            public string DataDir = "";    // where the voice files would go on it
        }

        /// <summary>The fixed drives with room on them, the system drive first. On the system drive the
        /// files go under the user's own AppData, beside claude-voice's other notes; on any other drive
        /// into a claude-voice folder at its root, where people look for things.</summary>
        public static List<Drive> Drives()
        {
            var list = new List<Drive>();
            var system = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\";
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                        var free = d.AvailableFreeSpace / 1_000_000_000.0;
                        var isSystem = string.Equals(d.RootDirectory.FullName, system, StringComparison.OrdinalIgnoreCase);
                        list.Add(new Drive
                        {
                            Root = d.RootDirectory.FullName,
                            Label = d.RootDirectory.FullName.TrimEnd('\\'),
                            FreeGb = free,
                            DataDir = isSystem
                                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "claude-voice")
                                : Path.Combine(d.RootDirectory.FullName, "claude-voice"),
                        });
                    }
                    catch { /* a drive that will not answer is not offered */ }
                }
            }
            catch { }
            return list.OrderByDescending(d => string.Equals(d.Root, system, StringComparison.OrdinalIgnoreCase))
                       .ThenBy(d => d.Root, StringComparer.OrdinalIgnoreCase).Take(5).ToList();
        }

        public static string Gb(double gb) =>
            gb >= 10 ? string.Format(CultureInfo.InvariantCulture, "{0:0} GB", gb)
                     : string.Format(CultureInfo.InvariantCulture, "{0:0.#} GB", gb);

        public static string Bytes(long bytes) =>
            bytes >= 1_000_000_000 ? string.Format(CultureInfo.InvariantCulture, "{0:0.0} GB", bytes / 1_000_000_000.0)
                                   : string.Format(CultureInfo.InvariantCulture, "{0:0} MB", bytes / 1_000_000.0);
    }
}
