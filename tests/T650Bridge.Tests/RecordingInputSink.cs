using T650Bridge.Gesture;

namespace T650Bridge.Tests;

/// <summary>Records every input event as a short text entry instead of sending it to Windows.</summary>
internal sealed class RecordingInputSink : IInputSink
{
    public List<string> Events { get; } = [];

    public void MouseMove(int dx, int dy) => Events.Add($"Move:{dx},{dy}");

    public void LeftButton(bool down) => Events.Add(down ? "LeftDown" : "LeftUp");

    public void RightButton(bool down) => Events.Add(down ? "RightDown" : "RightUp");

    public void MiddleClick() => Events.Add("MiddleClick");

    public void Wheel(int delta) => Events.Add($"Wheel:{delta}");

    public void HorizontalWheel(int delta) => Events.Add($"HWheel:{delta}");

    public void KeyDown(byte key) => Events.Add($"KeyDown:{key:X2}");

    public void KeyUp(byte key) => Events.Add($"KeyUp:{key:X2}");

    public IEnumerable<string> Without(string prefix) => Events.Where(e => !e.StartsWith(prefix, StringComparison.Ordinal));
}
