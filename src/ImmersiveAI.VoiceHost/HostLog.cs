using System.Text;

namespace ImmersiveAI.VoiceHost;

/// <summary>
/// The only place this process is allowed to be chatty. stdout is the protocol
/// channel - one stray line there corrupts it - so everything the host has to
/// say goes here: a rolling text file beside the mod's config folder.
/// Never throws: a log that cannot be written is not worth a dead voice.
/// </summary>
public static class HostLog
{
    private static readonly object Gate = new();
    private static string? _path;
    private static long _written;
    private const long MaxBytes = 1_000_000;   // then roll to .1 and start over

    public static string? Path => _path;

    public static string DefaultPath()
    {
        try
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord",
                                          "Configs", "ImmersiveAI", "voicehost.log");
        }
        catch
        {
            return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ImmersiveAI.voicehost.log");
        }
    }

    public static void Init(string? path)
    {
        lock (Gate)
        {
            _path = string.IsNullOrWhiteSpace(path) ? DefaultPath() : path;
            try
            {
                string? dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_path!));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);
                var fi = new FileInfo(_path!);
                _written = fi.Exists ? fi.Length : 0;
            }
            catch { /* the log is a convenience, never a dependency */ }
        }
        Info($"--- voice host start  pid={Environment.ProcessId}  {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERR ", message);

    public static void Error(string message, Exception ex) =>
        Write("ERR ", message + " :: " + ex.GetType().Name + ": " + ex.Message +
                      (ex.StackTrace is null ? "" : Environment.NewLine + ex.StackTrace));

    private static void Write(string level, string message)
    {
        if (_path is null) return;
        string line = $"{DateTime.Now:HH:mm:ss.fff} {level} {message}{Environment.NewLine}";
        lock (Gate)
        {
            try
            {
                if (_written > MaxBytes) Roll();
                File.AppendAllText(_path!, line, new UTF8Encoding(false));
                _written += line.Length;
            }
            catch { /* swallow: logging must never take the voice down */ }
        }
    }

    private static void Roll()
    {
        try
        {
            string old = _path + ".1";
            if (File.Exists(old)) File.Delete(old);
            File.Move(_path!, old);
        }
        catch { /* if the roll fails we simply keep appending */ }
        _written = 0;
    }

    public static void Flush() { /* AppendAllText is already flushed; kept for symmetry */ }

    /// <summary>A TextWriter that swallows managed Console output into the log.</summary>
    public sealed class Writer : TextWriter
    {
        private readonly string _tag;
        private readonly StringBuilder _buffer = new();
        public Writer(string tag) => _tag = tag;
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            lock (_buffer)
            {
                if (value == '\n') { Dump(); return; }
                if (value == '\r') return;
                _buffer.Append(value);
                if (_buffer.Length > 4000) Dump();
            }
        }

        public override void Write(string? value)
        {
            if (value is null) return;
            foreach (char c in value) Write(c);
        }

        private void Dump()
        {
            if (_buffer.Length == 0) return;
            string s = _buffer.ToString();
            _buffer.Clear();
            Info($"[{_tag}] {s}");
        }

        public override void Flush() { lock (_buffer) Dump(); }
    }
}
