using System.Runtime.InteropServices;

namespace T650Bridge.Gesture;

/// <summary>Sends input to Windows through the user32 mouse_event / keybd_event APIs.</summary>
public sealed class Win32InputSink : IInputSink
{
    private const uint MouseMoveFlag = 0x0001;
    private const uint LeftDownFlag = 0x0002;
    private const uint LeftUpFlag = 0x0004;
    private const uint RightDownFlag = 0x0008;
    private const uint RightUpFlag = 0x0010;
    private const uint MiddleDownFlag = 0x0020;
    private const uint MiddleUpFlag = 0x0040;
    private const uint WheelFlag = 0x0800;
    private const uint HorizontalWheelFlag = 0x01000;
    private const uint KeyDownFlag = 0x0000;
    private const uint KeyUpFlag = 0x0002;

    public void MouseMove(int dx, int dy) => NativeMethods.mouse_event(MouseMoveFlag, dx, dy, 0, UIntPtr.Zero);

    public void LeftButton(bool down) => SendButton(down ? LeftDownFlag : LeftUpFlag);

    public void RightButton(bool down) => SendButton(down ? RightDownFlag : RightUpFlag);

    public void MiddleClick()
    {
        SendButton(MiddleDownFlag);
        SendButton(MiddleUpFlag);
    }

    public void Wheel(int delta) => NativeMethods.mouse_event(WheelFlag, 0, 0, unchecked((uint)delta), UIntPtr.Zero);

    public void HorizontalWheel(int delta) =>
        NativeMethods.mouse_event(HorizontalWheelFlag, 0, 0, unchecked((uint)delta), UIntPtr.Zero);

    public void KeyDown(byte key) => NativeMethods.keybd_event(key, 0, KeyDownFlag, UIntPtr.Zero);

    public void KeyUp(byte key) => NativeMethods.keybd_event(key, 0, KeyUpFlag, UIntPtr.Zero);

    private static void SendButton(uint flag) => NativeMethods.mouse_event(flag, 0, 0, 0, UIntPtr.Zero);

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }
}
