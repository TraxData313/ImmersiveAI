using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ImmersiveAI.VoiceHost;

/// <summary>
/// stdout is the protocol channel. ggml, CUDA and any other native passenger in
/// this process writes wherever it pleases, and one printf lands in the middle
/// of a JSON line and desynchronises the game. So before a single native library
/// is loaded we:
///   1. duplicate fd 1 and keep the duplicate for OURSELVES (the protocol),
///   2. point fd 1 at NUL, so the C runtime's printf goes nowhere,
///   3. SetStdHandle(STD_OUTPUT_HANDLE, NUL) as well, so a statically linked CRT
///      initialising later also starts out pointed at NUL,
///   4. redirect managed Console.Out/Error into the log file.
/// Optionally the same is done to stderr, which additionally removes the classic
/// child-process deadlock: a parent that redirects stderr and never drains it
/// blocks the child forever once the pipe buffer fills with engine chatter.
/// Every step fails soft: if any of it does not work we simply hand back the
/// ordinary standard output stream.
/// </summary>
public static class StdoutGuard
{
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;
    private const int O_WRONLY = 0x0001;
    private const int O_BINARY = 0x8000;

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sec,
                                             uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32", SetLastError = true)]
    private static extern bool SetStdHandle(int which, IntPtr handle);

    [DllImport("ucrtbase.dll", EntryPoint = "_dup", SetLastError = true)]
    private static extern int CrtDup(int fd);

    [DllImport("ucrtbase.dll", EntryPoint = "_dup2", SetLastError = true)]
    private static extern int CrtDup2(int fd1, int fd2);

    [DllImport("ucrtbase.dll", EntryPoint = "_get_osfhandle", SetLastError = true)]
    private static extern IntPtr CrtGetOsfHandle(int fd);

    [DllImport("ucrtbase.dll", EntryPoint = "_open_osfhandle", SetLastError = true)]
    private static extern int CrtOpenOsfHandle(IntPtr osHandle, int flags);

    /// <summary>
    /// Returns the stream the protocol must be written to. Call ONCE, before any
    /// native library is loaded.
    /// </summary>
    public static Stream Install(bool keepStderr)
    {
        Stream protocol;
        try
        {
            protocol = TakeOverStdout();
        }
        catch (Exception ex)
        {
            HostLog.Warn("stdout guard could not be installed, using plain stdout: " + ex.Message);
            protocol = Console.OpenStandardOutput();
        }

        if (!keepStderr)
        {
            try { SilenceStderr(); }
            catch (Exception ex) { HostLog.Warn("stderr could not be silenced: " + ex.Message); }
        }

        // Managed strays (anything that still calls Console.WriteLine) go to the log.
        try
        {
            Console.SetOut(new HostLog.Writer("stdout"));
            Console.SetError(new HostLog.Writer("stderr"));
        }
        catch { /* not worth failing over */ }

        return protocol;
    }

    private static Stream TakeOverStdout()
    {
        int dup = CrtDup(1);
        if (dup < 0) throw new IOException("_dup(1) failed");

        IntPtr ours = CrtGetOsfHandle(dup);
        if (ours == IntPtr.Zero || ours == new IntPtr(-1) || ours == new IntPtr(-2))
            throw new IOException("_get_osfhandle failed");

        var stream = new FileStream(new SafeFileHandle(ours, ownsHandle: true), FileAccess.Write, 1, false);

        IntPtr nul = OpenNul();
        int nulFd = CrtOpenOsfHandle(nul, O_WRONLY | O_BINARY);
        if (nulFd >= 0) CrtDup2(nulFd, 1);        // printf -> NUL
        SetStdHandle(STD_OUTPUT_HANDLE, nul);     // a late CRT starts at NUL too

        HostLog.Info("stdout guard installed: protocol on a private duplicate, fd 1 -> NUL");
        return stream;
    }

    private static void SilenceStderr()
    {
        IntPtr nul = OpenNul();
        int nulFd = CrtOpenOsfHandle(nul, O_WRONLY | O_BINARY);
        if (nulFd >= 0) CrtDup2(nulFd, 2);
        SetStdHandle(STD_ERROR_HANDLE, nul);
        HostLog.Info("stderr -> NUL (engine chatter lives in this log; pass --keep-stderr to keep it)");
    }

    private static IntPtr OpenNul()
    {
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_RW = 0x00000003;
        const uint OPEN_EXISTING = 3;
        IntPtr nul = CreateFileW("NUL", GENERIC_WRITE, FILE_SHARE_RW, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (nul == new IntPtr(-1)) throw new IOException("cannot open NUL (win32 " + Marshal.GetLastWin32Error() + ")");
        return nul;
    }
}
