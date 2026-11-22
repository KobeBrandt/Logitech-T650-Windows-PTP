using System.Runtime.InteropServices;

namespace T650Bridge.Gesture;

/// <summary>Thin wrapper over the Win32 mouse_event / keybd_event input APIs.</summary>
public static class InputSender
{
    public const byte VK_TAB = 0x09;
    public const byte VK_SHIFT = 0x10;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_MENU = 0x12; // Alt
    public const byte VK_LEFT = 0x25;
    public const byte VK_RIGHT = 0x27;
    public const byte VK_D = 0x44;
    public const byte VK_N = 0x4E;
    public const byte VK_S = 0x53;
    public const byte VK_LWIN = 0x5B;
    public const byte VK_VOLUME_DOWN = 0xAE;
    public const byte VK_VOLUME_UP = 0xAF;
    public const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    public const byte VK_MEDIA_PREV_TRACK = 0xB1;
    public const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

    /// <summary>One notch of a standard mouse wheel.</summary>
    public const int WheelDelta = 120;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL = 0x01000;
    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public static void MouseMove(int dx, int dy) => mouse_event(MOUSEEVENTF_MOVE, dx, dy, 0, UIntPtr.Zero);

    public static void LeftButton(bool down) =>
        mouse_event(down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

    public static void RightButton(bool down) =>
        mouse_event(down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);

    public static void LeftClick()
    {
        LeftButton(down: true);
        LeftButton(down: false);
    }

    public static void RightClick()
    {
        RightButton(down: true);
        RightButton(down: false);
    }

    public static void MiddleClick()
    {
        mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, UIntPtr.Zero);
    }

    public static void Wheel(int delta) => mouse_event(MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)delta), UIntPtr.Zero);

    public static void HorizontalWheel(int delta) =>
        mouse_event(MOUSEEVENTF_HWHEEL, 0, 0, unchecked((uint)delta), UIntPtr.Zero);

    public static void KeyDown(byte key) => keybd_event(key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);

    public static void KeyUp(byte key) => keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

    /// <summary>Presses keys in order, then releases them in reverse order.</summary>
    public static void KeyCombo(params byte[] keys)
    {
        foreach (byte key in keys) KeyDown(key);
        for (int i = keys.Length - 1; i >= 0; i--) KeyUp(keys[i]);
    }

    /// <summary>Ctrl + one wheel notch: zoom in (positive) or out (negative) in most apps.</summary>
    public static void ZoomStep(bool zoomIn)
    {
        KeyDown(VK_CONTROL);
        Wheel(zoomIn ? WheelDelta : -WheelDelta);
        KeyUp(VK_CONTROL);
    }
}
