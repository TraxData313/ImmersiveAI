using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Voice
{
    /// <summary>
    /// Installs claude-voice from the game the way its own install.ps1 does (claude-voice 2.0,
    /// 2026.10.17), because the setup .exe the game used to fetch is unsigned and Smart App Control —
    /// on by default on a clean Windows 11 — refuses to run it:
    /// <list type="number">
    /// <item>claude-voice's code: GitHub's zip of main (or the folder <c>ClaudeVoiceSetupSource</c> names);</item>
    /// <item>python.org's own Python, checked against the SHA-256 that code pins in runtime.json, with
    /// python.exe and pythonw.exe checked to be signed by the Python Software Foundation, unpacked into
    /// <c>&lt;program&gt;\runtime</c>;</item>
    /// <item>claude-voice's installer.py on that Python, which writes the same setup-status.json the
    /// page has always drawn.</item>
    /// </list>
    /// No PowerShell, nothing with its execution policy bypassed, no unsigned executable.
    /// </summary>
    internal static class ClaudeVoiceInstaller
    {
        private static string Work => Path.Combine(Path.GetTempPath(), "ImmersiveAI", "claude-voice-install");

        /// <summary>Fetches what it needs and starts installer.py. Returns its process; throws with a
        /// sentence a player can read when something before that fails.</summary>
        public static async Task<Process> StartAsync(string? engine, string? bigData, bool quiet, string? sourceFolder,
                                                     string cancelPath, Action<string> say)
        {
            Directory.CreateDirectory(Work);
            string source;
            if (!string.IsNullOrWhiteSpace(sourceFolder) && File.Exists(Path.Combine(sourceFolder!, VoiceAppInstall.Installer)))
                source = sourceFolder!;
            else
            {
                say("Getting the voice app from GitHub…");
                source = await FetchSourceAsync(cancelPath).ConfigureAwait(false);
            }
            Stop(cancelPath);

            var pin = VoiceAppInstall.ParsePin(ReadOrNull(Path.Combine(source, "runtime.json")))
                      ?? throw new InvalidOperationException("The voice app's download names no Python to run on (runtime.json).");
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = VoiceAppInstall.ProgramRoot(ClaudeVoiceApp.FindInstall()?.Record, local, File.Exists);
            var runtime = Path.Combine(root, "runtime");
            if (!VoiceAppInstall.RuntimeIsCurrent(ReadOrNull(Path.Combine(runtime, VoiceAppInstall.RuntimeMarker)), pin))
            {
                say("Getting Python " + pin.Version + " from python.org…");
                await PutRuntimeAsync(pin, root, cancelPath).ConfigureAwait(false);
            }
            Stop(cancelPath);

            say("Starting the voice app's installer…");
            var args = VoiceAppInstall.InstallerArgs(Path.Combine(source, VoiceAppInstall.Installer), root, source, engine, bigData, quiet);
            // Quiet: no window over a full-screen game, the page draws the status file. Otherwise a
            // console window of its own, saying each step in plain words.
            return Process.Start(new ProcessStartInfo(Path.Combine(runtime, "python.exe"), args)
            {
                WorkingDirectory = Work,
                UseShellExecute = !quiet,
                CreateNoWindow = quiet,
            }) ?? throw new InvalidOperationException("Windows would not start the voice app's installer.");
        }

        /// <summary>The source fetched from GitHub, once its installer has exited (it ran from there).
        /// Python's zip stays: it is what lets the next install skip that download.</summary>
        public static void CleanUp()
        {
            try { if (Directory.Exists(Path.Combine(Work, "src"))) Directory.Delete(Path.Combine(Work, "src"), true); } catch { }
            try { File.Delete(Path.Combine(Work, "claude-voice.zip")); } catch { }
        }

        private static void Stop(string cancelPath)
        {
            if (File.Exists(cancelPath)) throw new OperationCanceledException();
        }

        private static string? ReadOrNull(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; } catch { return null; }
        }

        private static async Task<string> FetchSourceAsync(string cancelPath)
        {
            var zip = Path.Combine(Work, "claude-voice.zip");
            await DownloadAsync(VoiceAppInstall.SourceZip, zip, cancelPath).ConfigureAwait(false);
            var dest = Path.Combine(Work, "src");
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            using (var archive = new ZipArchive(File.OpenRead(zip)))
                Unpack(archive, dest, _ => true);
            foreach (var installer in Directory.GetFiles(dest, VoiceAppInstall.Installer, SearchOption.AllDirectories))
            {
                var folder = Path.GetDirectoryName(installer)!;
                if (File.Exists(Path.Combine(folder, "voice_lib.py"))) return folder;
            }
            throw new InvalidOperationException("The voice app's download holds no installer.");
        }

        private static async Task PutRuntimeAsync(RuntimePin pin, string root, string cancelPath)
        {
            var zip = Path.Combine(Work, Path.GetFileName(new Uri(pin.Url).LocalPath));
            if (!File.Exists(zip) || !string.Equals(Sha256(zip), pin.Sha256, StringComparison.OrdinalIgnoreCase))
                await DownloadAsync(pin.Url, zip, cancelPath).ConfigureAwait(false);
            var got = Sha256(zip);
            if (!string.Equals(got, pin.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(zip);
                throw new InvalidOperationException("The Python download from python.org did not match its pinned SHA-256; nothing was installed.");
            }
            Stop(cancelPath);

            Directory.CreateDirectory(root);
            var staged = Path.Combine(root, "runtime.new");
            if (Directory.Exists(staged)) Directory.Delete(staged, true);
            using (var archive = new ZipArchive(File.OpenRead(zip)))
                Unpack(archive, staged, VoiceAppInstall.WantedFromRuntimeZip);
            foreach (var exe in new[] { "python.exe", "pythonw.exe" })
            {
                var path = Path.Combine(staged, exe);
                if (!SignedBy(path, pin.Signer))
                {
                    Directory.Delete(staged, true);
                    throw new InvalidOperationException(exe + " in the Python download is not signed by the " + pin.Signer + "; nothing was installed.");
                }
            }

            var runtime = Path.Combine(root, "runtime");
            if (Directory.Exists(runtime))
            {
                // A Python of an older pin under a running install: that install stops its own processes first.
                var python = Path.Combine(runtime, "python.exe");
                var installer = Path.Combine(root, "app", VoiceAppInstall.Installer);
                if (File.Exists(python) && File.Exists(installer))
                {
                    using (var stop = Process.Start(new ProcessStartInfo(python, VoiceAppInstall.Quote(installer) + " stop --root " + VoiceAppInstall.Quote(root))
                    { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Work }))
                    {
                        // Moved aside under a running engine, the old Python would keep it running.
                        if (stop != null && (!stop.WaitForExit(30000) || stop.ExitCode != 0))
                        {
                            try { Directory.Delete(staged, true); } catch { }
                            throw new InvalidOperationException("The voice app already on this computer would not stop. Close its panel, "
                                                                + "restart the game and try again.");
                        }
                    }
                }
                // Parked under a name of its own: one an earlier update could not delete (a file
                // still in use) never stands in the way of this one.
                foreach (var parked in Directory.GetDirectories(root, "runtime.old*"))
                    try { Directory.Delete(parked, true); } catch { /* still in use; the next one tries */ }
                var old = Path.Combine(root, "runtime.old-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture));
                Directory.Move(runtime, old);
                try { Directory.Delete(old, true); } catch { /* the next install sweeps it */ }
            }
            Directory.Move(staged, runtime);
            File.WriteAllText(Path.Combine(runtime, VoiceAppInstall.RuntimeMarker),
                "{\n  \"version\": \"" + pin.Version + "\",\n  \"url\": \"" + pin.Url + "\",\n  \"sha256\": \"" + pin.Sha256 + "\"\n}\n");
        }

        private static void Unpack(ZipArchive archive, string dest, Func<string, bool> wanted)
        {
            var full = Path.GetFullPath(dest) + Path.DirectorySeparatorChar;
            foreach (var entry in archive.Entries)
            {
                if (!wanted(entry.FullName)) continue;
                var path = Path.GetFullPath(Path.Combine(dest, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(full, StringComparison.OrdinalIgnoreCase)) continue;      // no escaping the folder
                if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(path); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var input = entry.Open())
                using (var output = File.Create(path))
                    input.CopyTo(output);
            }
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>No byte for this long and the download is given up as stalled.</summary>
        private static readonly TimeSpan Stall = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Downloads <paramref name="url"/> to <paramref name="dest"/>. HttpClient's own timeout ends at the
        /// headers, so a body that stops arriving would otherwise hang the install for good, with Cancel
        /// unable to reach it: a timer watches the player's cancel file, the time since the last byte and
        /// an overall deadline, and closes the stream when any of them says stop. A cancel throws
        /// <see cref="OperationCanceledException"/>; a stall or the deadline throws a sentence a player can read.
        /// </summary>
        private static async Task DownloadAsync(string url, string dest, string cancelPath)
        {
            var started = DateTime.UtcNow;
            var lastByte = DateTime.UtcNow.Ticks;
            string? why = null;
            using (var cts = new CancellationTokenSource())
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) })
            {
                var token = cts.Token;
                using (new Timer(_ =>
                {
                    if (token.IsCancellationRequested) return;
                    if (File.Exists(cancelPath)) why = "cancel";
                    else if (DateTime.UtcNow - new DateTime(Interlocked.Read(ref lastByte), DateTimeKind.Utc) > Stall) why = "stalled";
                    else if (DateTime.UtcNow - started > TimeSpan.FromMinutes(45)) why = "slow";
                    if (why != null) try { cts.Cancel(); } catch { }
                }, null, 1000, 1000))
                {
                    try
                    {
                        http.DefaultRequestHeaders.UserAgent.ParseAdd("ImmersiveAI");
                        using (var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                        {
                            res.EnsureSuccessStatusCode();
                            var part = dest + ".part";
                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            using (var input = await res.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            // On .NET Framework a pending network read does not always honour the token;
                            // closing the stream under it always ends it.
                            using (token.Register(() => { try { input.Dispose(); } catch { } }))
                            using (var output = File.Create(part))
                            {
                                var buffer = new byte[81920];
                                int n;
                                while ((n = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                                {
                                    output.Write(buffer, 0, n);
                                    Interlocked.Exchange(ref lastByte, DateTime.UtcNow.Ticks);
                                }
                            }
                            token.ThrowIfCancellationRequested();
                            if (File.Exists(dest)) File.Delete(dest);
                            File.Move(part, dest);
                        }
                    }
                    catch (Exception) when (why == "cancel")
                    {
                        throw new OperationCanceledException();
                    }
                    catch (Exception ex) when (why != null || ex is OperationCanceledException)
                    {
                        throw new InvalidOperationException("The download from " + new Uri(url).Host + " stopped arriving"
                            + (why == "slow" ? " in time" : string.Empty) + " — check your internet connection and try again.", ex);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ the signature

        /// <summary>Windows' own verdict on the file's Authenticode signature (WinVerifyTrust), and the
        /// signer's name. Both, because a valid signature by somebody else is no better than none.</summary>
        private static bool SignedBy(string path, string signer)
        {
            if (string.IsNullOrWhiteSpace(signer) || !File.Exists(path) || !Trusted(path)) return false;
            try
            {
                var cert = X509Certificate.CreateFromSignedFile(path);
                return cert.Subject.IndexOf("CN=" + signer, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static readonly Guid GenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            public uint cbStruct;
            public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WinTrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, ref WinTrustData data);

        private static bool Trusted(string path)
        {
            var file = new WinTrustFileInfo { cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo)), pcwszFilePath = path };
            var filePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinTrustFileInfo)));
            try
            {
                Marshal.StructureToPtr(file, filePtr, false);
                var data = new WinTrustData
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustData)),
                    dwUIChoice = 2,            // WTD_UI_NONE
                    fdwRevocationChecks = 0,   // WTD_REVOKE_NONE: no network round trip in a game
                    dwUnionChoice = 1,         // WTD_CHOICE_FILE
                    pFile = filePtr,
                    dwStateAction = 0,
                    dwProvFlags = 0x1000,      // WTD_CACHE_ONLY_URL_RETRIEVAL
                };
                return WinVerifyTrust(IntPtr.Zero, GenericVerifyV2, ref data) == 0;
            }
            catch { return false; }
            finally
            {
                Marshal.DestroyStructure(filePtr, typeof(WinTrustFileInfo));
                Marshal.FreeHGlobal(filePtr);
            }
        }
    }
}
