using System;
using System.IO;

namespace ImmersiveAI
{
    /// <summary>
    /// The mod's plain diagnostics log — a rolling log.txt beside config.json, because toasts
    /// vanish and a Steam bug report needs something to paste. One generation of history is
    /// kept (log_old.txt). Best-effort everywhere: logging must never cost more than the line
    /// it records, and no chat content is written beyond what an error line itself carries.
    /// </summary>
    public static class ModLog
    {
        private static readonly object Gate = new object();
        private const long MaxBytes = 512 * 1024;

        public static string LogPath => Path.Combine(ModConfig.ConfigDirectory, "log.txt");

        public static void Info(string message) => Write("INFO ", message);
        public static void Warn(string message) => Write("WARN ", message);
        public static void Error(string message) => Write("ERROR", message);

        public static void Error(string context, Exception ex) =>
            Write("ERROR", context + " — " + (ex == null ? "?" : ex.GetType().Name + ": " + ex.Message));

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(ModConfig.ConfigDirectory);
                    var path = LogPath;
                    try
                    {
                        var info = new FileInfo(path);
                        if (info.Exists && info.Length > MaxBytes)
                        {
                            var old = Path.Combine(ModConfig.ConfigDirectory, "log_old.txt");
                            if (File.Exists(old)) File.Delete(old);
                            File.Move(path, old);
                        }
                    }
                    catch { /* rotation is a nicety */ }

                    File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {message}\r\n");
                }
            }
            catch { /* the log must never take anything down */ }
        }
    }
}
