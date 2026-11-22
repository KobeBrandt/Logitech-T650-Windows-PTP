namespace T650Bridge.Gesture;

/// <summary>Destination for synthesized mouse and keyboard input.</summary>
public interface IInputSink
{
    /// <summary>One notch of a standard mouse wheel.</summary>
    const int WheelDelta = 120;

    void MouseMove(int dx, int dy);

    void LeftButton(bool down);

    void RightButton(bool down);

    void MiddleClick();

    void Wheel(int delta);

    void HorizontalWheel(int delta);

    void KeyDown(byte key);

    void KeyUp(byte key);

    void LeftClick()
    {
        LeftButton(down: true);
        LeftButton(down: false);
    }

    void RightClick()
    {
        RightButton(down: true);
        RightButton(down: false);
    }

    /// <summary>Presses keys in order, then releases them in reverse order.</summary>
    void KeyCombo(params byte[] keys)
    {
        foreach (byte key in keys) KeyDown(key);
        for (int i = keys.Length - 1; i >= 0; i--) KeyUp(keys[i]);
    }

    /// <summary>Ctrl + one wheel notch: zoom in (positive) or out (negative) in most apps.</summary>
    void ZoomStep(bool zoomIn)
    {
        KeyDown(VirtualKey.Control);
        Wheel(zoomIn ? WheelDelta : -WheelDelta);
        KeyUp(VirtualKey.Control);
    }
}
