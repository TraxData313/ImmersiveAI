using System.Diagnostics;

namespace ImmersiveAI.VoiceHost;

/// <summary>
/// The hard requirement of this whole design: when the game goes, we go. A
/// stranded host holding four gigabytes of VRAM after the player has quit is the
/// worst thing an out-of-process engine can do to them, and it is invisible -
/// no window, no tray icon, just a machine that has quietly lost its graphics
/// memory. So the parent is watched by pid, stdin EOF is watched by the reader,
/// and the exit is taken by force if the polite one hesitates.
/// </summary>
public static class Watchdog
{
    private static readonly object Gate = new();
    private static bool _exiting;

    public static void WatchParent(int parentPid)
    {
        if (parentPid <= 0)
        {
            HostLog.Warn("no parent pid was given - the host will only exit on stdin EOF or 'quit'");
            return;
        }

        Process? parent = null;
        try { parent = Process.GetProcessById(parentPid); }
        catch (Exception ex)
        {
            HostLog.Error($"the parent process {parentPid} is already gone at startup: {ex.Message}");
            Bail(0, "parent already gone");
            return;
        }

        HostLog.Info($"watching parent pid {parentPid} ({Safe(() => parent!.ProcessName)})");

        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    if (parent!.HasExited) { Bail(0, "the parent process exited"); return; }
                }
                catch (Exception ex)
                {
                    // GetProcessById/HasExited throws once the handle is invalid:
                    // treat any doubt about the parent as the parent being gone.
                    Bail(0, "the parent process can no longer be seen: " + ex.Message);
                    return;
                }
                Thread.Sleep(1000);
            }
        })
        { IsBackground = true, Name = "parent-watchdog" };
        t.Start();
    }

    /// <summary>Log, then leave - and make sure we really leave.</summary>
    public static void Bail(int code, string why)
    {
        lock (Gate)
        {
            if (_exiting) return;
            _exiting = true;
        }
        HostLog.Info("exiting: " + why);
        HostLog.Flush();

        // Environment.Exit runs finalizers, and a finalizer waiting on a native
        // call inside ggml would hang forever. Give it a moment, then kill.
        var killer = new Thread(() =>
        {
            Thread.Sleep(2500);
            try { Process.GetCurrentProcess().Kill(); } catch { /* nothing left to try */ }
        })
        { IsBackground = true, Name = "last-resort" };
        killer.Start();

        Environment.Exit(code);
    }

    private static string Safe(Func<string> f)
    {
        try { return f(); } catch { return "?"; }
    }
}
