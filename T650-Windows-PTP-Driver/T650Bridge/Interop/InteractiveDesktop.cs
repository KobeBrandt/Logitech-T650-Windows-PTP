using System.Runtime.InteropServices;

namespace T650Bridge.Interop;

/// <summary>
/// Attaches the process and calling thread to the interactive desktop (winsta0\Default),
/// so synthesized input reaches the user's session.
/// </summary>
public static class InteractiveDesktop
{
    private const uint WINSTA_ALL_ACCESS = 0x37F;
    private const uint DESKTOP_ALL_ACCESS = 0x1FF;

    [ThreadStatic]
    private static bool _threadAttached;

    /// <summary>Attaches the calling thread once. Later calls on the same thread do nothing.</summary>
    public static void EnsureAttachedForCurrentThread()
    {
        if (_threadAttached) return;
        Attach();
        _threadAttached = true;
    }

    /// <summary>Attaches the process window station and the calling thread's desktop. Failures are ignored.</summary>
    public static void Attach()
    {
        IntPtr hWinSta = OpenWindowStation("winsta0", false, WINSTA_ALL_ACCESS);
        if (hWinSta == IntPtr.Zero) return;

        SetProcessWindowStation(hWinSta);
        IntPtr hDesktop = OpenDesktop("Default", 0, false, DESKTOP_ALL_ACCESS);
        if (hDesktop != IntPtr.Zero)
            SetThreadDesktop(hDesktop);
    }

    /// <summary>Reads the cursor position. Returns false when the desktop is not accessible.</summary>
    public static bool TryGetCursorPosition(out int x, out int y)
    {
        bool ok = GetCursorPos(out var point);
        x = point.X;
        y = point.Y;
        return ok;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenWindowStation(string lpszWinSta, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessWindowStation(IntPtr hWinSta);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT lpPoint);
}
