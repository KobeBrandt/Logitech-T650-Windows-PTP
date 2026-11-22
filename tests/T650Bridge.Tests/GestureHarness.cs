using T650Bridge.Gesture;
using T650Bridge.Touch;

namespace T650Bridge.Tests;

/// <summary>Drives a <see cref="PrecisionGestureEngine"/> with synthetic frames and a fake clock.</summary>
internal sealed class GestureHarness
{
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public GestureHarness(TouchpadSettings? settings = null)
    {
        Engine = new PrecisionGestureEngine(Input, () => _now)
        {
            Settings = settings ?? TouchpadSettings.Defaults
        };
    }

    public RecordingInputSink Input { get; } = new();

    public PrecisionGestureEngine Engine { get; }

    public void Advance(double milliseconds) => _now = _now.AddMilliseconds(milliseconds);

    /// <summary>Sends one frame with the given contacts (id, x, y).</summary>
    public void Frame(bool button, params (byte Id, int X, int Y)[] contacts)
    {
        var frame = new TouchFrame { PhysicalButtonPressed = button };
        foreach (var (id, x, y) in contacts)
            frame.Contacts.Add(new TouchContact { Id = id, X = (ushort)x, Y = (ushort)y });
        Engine.ProcessFrame(frame);
        Advance(10);
    }

    public void Frame(params (byte Id, int X, int Y)[] contacts) => Frame(false, contacts);

    public void Lift() => Frame(false);

    /// <summary>Puts fingers down at their start points and moves them all by (dx, dy) per frame.</summary>
    public void Move(int frames, int dx, int dy, params (byte Id, int X, int Y)[] start)
    {
        for (int f = 0; f <= frames; f++)
            Frame(start.Select(c => (c.Id, c.X + (dx * f), c.Y + (dy * f))).ToArray());
    }

    public void Tap(params (byte Id, int X, int Y)[] contacts)
    {
        Frame(contacts);
        Advance(50);
        Lift();
    }

    public static (byte, int, int)[] Fingers(int count, int x = 1000, int y = 1000) =>
        Enumerable.Range(1, count).Select(i => ((byte)i, x + (i * 200), y)).ToArray();
}
