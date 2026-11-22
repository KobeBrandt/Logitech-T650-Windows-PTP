using T650Bridge.Interop;
using T650Bridge.Touch;
using static T650Bridge.Gesture.InputSender;

namespace T650Bridge.Gesture;

/// <summary>
/// Precision Gesture Engine for Logitech T650.
/// Translates raw touch frames into Windows pointer, click, scroll, zoom and multi-finger gestures,
/// following the user's Windows touchpad settings (see <see cref="TouchpadSettings"/>).
/// </summary>
public class PrecisionGestureEngine
{
    private enum TwoFingerMode { Undecided, Scroll, Zoom, Ignored }

    // Tuning constants (touch units: pad is 0..2832 x 0..2364)
    private const double CursorSensitivity = 1.15;
    private const double SwipeTravelThreshold = 140.0;   // ~6mm movement to trigger a 3/4 finger swipe
    private const double SwipeRepeatTravel = 220.0;      // extra travel per repeated app switch / volume step
    private const double TapMaxTravel = 60.0;            // max distance for a tap
    private const double TapMaxDurationMs = 300.0;       // max contact time for a tap
    private const double TapAndDragWindowMs = 300.0;     // max gap between tap and touch-down to start a drag
    private const double ScrollDecisionTravel = 25.0;    // two-finger travel that commits to scrolling
    private const double ZoomDecisionChange = 150.0;     // finger spread change that commits to zooming
    private const double ZoomStepDistance = 80.0;        // spread change per zoom notch
    private const double ScrollGain = 6.0;
    private const double ScrollEmitThreshold = 30.0;
    private const double RightClickZoneMinX = FrameAssembler.MaxX * 0.5;
    private const double RightClickZoneMinY = FrameAssembler.MaxY * 0.8;

    // Touch session tracking
    private bool _sessionActive;
    private DateTime _sessionStartTime = DateTime.MinValue;
    private int _maxFingersInSession;
    private double _totalDX;
    private double _totalDY;
    private readonly Dictionary<byte, (ushort X, ushort Y)> _lastContacts = new();

    // Multi-finger swipe state
    private bool _swipeTriggered;
    private bool _swipeHorizontal;
    private double _swipeLastAxisTotal;
    private bool _altHeld;

    // Two-finger scroll / zoom state
    private TwoFingerMode _twoFingerMode;
    private double _pinchStartDistance;
    private double _pinchLastDistance;
    private double _zoomAccum;
    private double _scrollAccumY;
    private double _scrollAccumX;

    // Tap-and-drag state
    private DateTime _lastTapTime = DateTime.MinValue;
    private bool _dragCandidate;
    private bool _dragging;

    // Physical button state
    private bool _isPhysicalDown;
    private bool _physicalIsRight;
    private bool _physicalUsedInSession;

    public event Action<string>? GestureTriggered;

    /// <summary>Current Windows touchpad settings. Replaced as a whole when the user changes them.</summary>
    public TouchpadSettings Settings { get; set; } = TouchpadSettings.Defaults;

    public void ProcessFrame(TouchFrame frame)
    {
        InteractiveDesktop.EnsureAttachedForCurrentThread();

        var contacts = frame.Contacts.Where(c => c.IsTouching).ToList();
        HandlePhysicalButton(frame.PhysicalButtonPressed, contacts);

        if (contacts.Count == 0)
        {
            EndSession();
            return;
        }

        if (!_sessionActive)
            StartSession(contacts.Count);
        else if (contacts.Count > _maxFingersInSession)
            _maxFingersInSession = contacts.Count; // fingers may land slightly out of sync

        var (frameDx, frameDy, tracked) = ComputeAverageDelta(contacts);
        _totalDX += frameDx;
        _totalDY += frameDy;

        DispatchGesture(contacts, frameDx, frameDy, tracked);
        RecordContacts(contacts);
    }

    private void DispatchGesture(List<TouchContact> contacts, double frameDx, double frameDy, bool tracked)
    {
        var settings = Settings;
        switch (_maxFingersInSession)
        {
            case 1:
                HandleOneFinger(frameDx, frameDy, tracked, settings);
                break;
            case 2:
                HandleTwoFingers(contacts, frameDx, frameDy, tracked, settings);
                break;
            case 3:
                HandleSwipe(settings.ThreeFingerSwipe, fingers: 3);
                break;
            default:
                HandleSwipe(settings.FourFingerSwipe, fingers: 4);
                break;
        }
    }

    #region Session lifecycle

    private void StartSession(int count)
    {
        var settings = Settings;
        _sessionActive = true;
        _sessionStartTime = DateTime.UtcNow;
        _maxFingersInSession = count;
        _totalDX = 0;
        _totalDY = 0;
        _swipeTriggered = false;
        _twoFingerMode = TwoFingerMode.Undecided;
        _pinchStartDistance = -1;
        _zoomAccum = 0;
        _scrollAccumY = 0;
        _scrollAccumX = 0;
        _physicalUsedInSession = _isPhysicalDown;
        _lastContacts.Clear();

        bool recentTap = (_sessionStartTime - _lastTapTime).TotalMilliseconds < TapAndDragWindowMs;
        _dragCandidate = count == 1 && recentTap && settings.TapsEnabled && settings.TapAndDragEnabled;
    }

    private void EndSession()
    {
        if (!_sessionActive) return;

        if (_altHeld)
        {
            KeyUp(VK_MENU);
            _altHeld = false;
        }

        if (_dragging)
        {
            LeftButton(down: false);
            _dragging = false;
            GestureTriggered?.Invoke("Tap-and-Drag: END");
        }
        else if (IsTap())
        {
            HandleTap(_maxFingersInSession, Settings);
        }

        _dragCandidate = false;
        _sessionActive = false;
        _lastContacts.Clear();
    }

    private bool IsTap()
    {
        double duration = (DateTime.UtcNow - _sessionStartTime).TotalMilliseconds;
        double travel = Math.Sqrt(_totalDX * _totalDX + _totalDY * _totalDY);
        return !_swipeTriggered
            && !_physicalUsedInSession
            && _twoFingerMode == TwoFingerMode.Undecided
            && duration < TapMaxDurationMs
            && travel < TapMaxTravel;
    }

    private (double Dx, double Dy, bool Tracked) ComputeAverageDelta(List<TouchContact> contacts)
    {
        double dx = 0, dy = 0;
        int tracked = 0;
        foreach (var c in contacts)
        {
            if (!_lastContacts.TryGetValue(c.Id, out var prev)) continue;
            dx += c.X - prev.X;
            dy += c.Y - prev.Y;
            tracked++;
        }
        return tracked == 0 ? (0, 0, false) : (dx / tracked, dy / tracked, true);
    }

    private void RecordContacts(List<TouchContact> contacts)
    {
        _lastContacts.Clear();
        foreach (var c in contacts)
            _lastContacts[c.Id] = (c.X, c.Y);
    }

    #endregion

    #region One finger: pointer and tap-and-drag

    private void HandleOneFinger(double frameDx, double frameDy, bool tracked, TouchpadSettings settings)
    {
        if (!tracked) return;

        if (_dragCandidate && !_dragging && TotalTravel() > TapMaxTravel)
        {
            _dragging = true;
            LeftButton(down: true);
            GestureTriggered?.Invoke("Tap-and-Drag: START");
        }

        if (Math.Abs(frameDx) <= 0.1 && Math.Abs(frameDy) <= 0.1) return;

        double gain = CursorSensitivity * PointerAcceleration(frameDx, frameDy) * settings.CursorSpeedFactor;
        int moveX = (int)Math.Round(frameDx * gain);
        int moveY = (int)Math.Round(frameDy * gain);
        if (moveX != 0 || moveY != 0)
            MouseMove(moveX, moveY);
    }

    private static double PointerAcceleration(double dx, double dy)
    {
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > 35) return 2.0;
        if (dist > 20) return 1.5;
        if (dist > 8) return 1.2;
        return 1.0;
    }

    private double TotalTravel() => Math.Sqrt(_totalDX * _totalDX + _totalDY * _totalDY);

    #endregion

    #region Two fingers: scroll and pinch zoom

    private void HandleTwoFingers(
        List<TouchContact> contacts, double frameDx, double frameDy, bool tracked, TouchpadSettings settings)
    {
        double? spread = contacts.Count >= 2 ? Distance(contacts[0], contacts[1]) : null;
        if (spread is double s && _pinchStartDistance < 0)
        {
            _pinchStartDistance = s;
            _pinchLastDistance = s;
        }

        if (_twoFingerMode == TwoFingerMode.Undecided)
            _twoFingerMode = DecideTwoFingerMode(spread, settings);

        if (_twoFingerMode == TwoFingerMode.Scroll && tracked)
            Scroll(frameDx, frameDy, settings);
        else if (_twoFingerMode == TwoFingerMode.Zoom && spread is double current)
            Zoom(current);
    }

    private TwoFingerMode DecideTwoFingerMode(double? spread, TouchpadSettings settings)
    {
        double spreadChange = spread is double s ? Math.Abs(s - _pinchStartDistance) : 0;
        double panTravel = TotalTravel();

        if (settings.ZoomEnabled && spreadChange >= ZoomDecisionChange && spreadChange > panTravel)
        {
            GestureTriggered?.Invoke("Gesture: Pinch Zoom");
            return TwoFingerMode.Zoom;
        }
        if (panTravel >= ScrollDecisionTravel)
            return settings.ScrollEnabled ? TwoFingerMode.Scroll : TwoFingerMode.Ignored;
        return TwoFingerMode.Undecided;
    }

    private void Scroll(double frameDx, double frameDy, TouchpadSettings settings)
    {
        // Fingers moving down give frameDy > 0. Positive WHEEL_DELTA scrolls UP.
        // Natural: content follows the fingers, so fingers down scrolls UP.
        double verticalSign = settings.NaturalScrolling ? 1.0 : -1.0;
        _scrollAccumY += verticalSign * frameDy * ScrollGain;
        _scrollAccumX += frameDx * ScrollGain;

        if (Math.Abs(_scrollAccumY) >= ScrollEmitThreshold)
        {
            int ticks = (int)_scrollAccumY;
            _scrollAccumY -= ticks;
            Wheel(ticks);
        }

        if (Math.Abs(_scrollAccumX) >= ScrollEmitThreshold)
        {
            int ticks = (int)_scrollAccumX;
            _scrollAccumX -= ticks;
            HorizontalWheel(ticks);
        }
    }

    private void Zoom(double spread)
    {
        _zoomAccum += spread - _pinchLastDistance;
        _pinchLastDistance = spread;

        while (Math.Abs(_zoomAccum) >= ZoomStepDistance)
        {
            bool zoomIn = _zoomAccum > 0; // fingers spreading apart zooms in
            ZoomStep(zoomIn);
            _zoomAccum -= zoomIn ? ZoomStepDistance : -ZoomStepDistance;
        }
    }

    private static double Distance(TouchContact a, TouchContact b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    #endregion

    #region Three / four fingers: swipes

    private void HandleSwipe(SwipeAction action, int fingers)
    {
        if (action == SwipeAction.None) return;

        if (!_swipeTriggered)
        {
            if (TotalTravel() < SwipeTravelThreshold) return;

            _swipeTriggered = true;
            _swipeHorizontal = Math.Abs(_totalDX) > Math.Abs(_totalDY);
            _swipeLastAxisTotal = _swipeHorizontal ? _totalDX : _totalDY;
            FireSwipe(action, fingers, Math.Sign(_swipeLastAxisTotal));
            return;
        }

        if (!IsRepeatable(action)) return;

        double axisTotal = _swipeHorizontal ? _totalDX : _totalDY;
        double delta = axisTotal - _swipeLastAxisTotal;
        if (Math.Abs(delta) < SwipeRepeatTravel) return;

        _swipeLastAxisTotal = axisTotal;
        FireRepeat(action, Math.Sign(delta));
    }

    private bool IsRepeatable(SwipeAction action) =>
        (action == SwipeAction.SwitchApps && _swipeHorizontal)
        || (action == SwipeAction.AudioVolume && !_swipeHorizontal);

    private void FireSwipe(SwipeAction action, int fingers, int sign)
    {
        string name = $"{fingers}-Finger Swipe {DirectionName(sign)}";
        switch (action)
        {
            case SwipeAction.SwitchApps:
                FireAppSwitchSwipe(name, sign);
                break;
            case SwipeAction.SwitchDesktops:
                FireDesktopSwipe(name, sign);
                break;
            case SwipeAction.AudioVolume:
                FireAudioSwipe(name, sign);
                break;
        }
    }

    private string DirectionName(int sign) => _swipeHorizontal
        ? (sign > 0 ? "RIGHT" : "LEFT")
        : (sign < 0 ? "UP" : "DOWN");

    private void FireAppSwitchSwipe(string name, int sign)
    {
        if (!_swipeHorizontal)
        {
            FireTaskViewOrDesktop(name, sign);
            return;
        }

        GestureTriggered?.Invoke($"Gesture: {name} (App Switcher: Alt+Tab, held while fingers stay down)");
        KeyDown(VK_MENU);
        _altHeld = true;
        AppSwitchStep(forward: sign > 0);
    }

    private void FireDesktopSwipe(string name, int sign)
    {
        if (!_swipeHorizontal)
        {
            FireTaskViewOrDesktop(name, sign);
            return;
        }

        // Content follows the fingers: swiping left brings in the desktop on the right.
        bool next = sign < 0;
        GestureTriggered?.Invoke($"Gesture: {name} ({(next ? "Next Desktop: Ctrl+Win+Right" : "Prev Desktop: Ctrl+Win+Left")})");
        KeyCombo(VK_CONTROL, VK_LWIN, next ? VK_RIGHT : VK_LEFT);
    }

    private void FireAudioSwipe(string name, int sign)
    {
        if (_swipeHorizontal)
        {
            bool nextTrack = sign > 0;
            GestureTriggered?.Invoke($"Gesture: {name} ({(nextTrack ? "Next Track" : "Previous Track")})");
            KeyCombo(nextTrack ? VK_MEDIA_NEXT_TRACK : VK_MEDIA_PREV_TRACK);
            return;
        }

        GestureTriggered?.Invoke($"Gesture: {name} ({(sign < 0 ? "Volume Up" : "Volume Down")})");
        VolumeStep(up: sign < 0);
    }

    private void FireTaskViewOrDesktop(string name, int sign)
    {
        if (sign < 0)
        {
            GestureTriggered?.Invoke($"Gesture: {name} (Task View: Win+Tab)");
            KeyCombo(VK_LWIN, VK_TAB);
        }
        else
        {
            GestureTriggered?.Invoke($"Gesture: {name} (Show Desktop: Win+D)");
            KeyCombo(VK_LWIN, VK_D);
        }
    }

    private void FireRepeat(SwipeAction action, int sign)
    {
        if (action == SwipeAction.SwitchApps)
            AppSwitchStep(forward: sign > 0);
        else
            VolumeStep(up: sign < 0);
    }

    private static void AppSwitchStep(bool forward)
    {
        if (forward)
            KeyCombo(VK_TAB);
        else
            KeyCombo(VK_SHIFT, VK_TAB);
    }

    private static void VolumeStep(bool up) => KeyCombo(up ? VK_VOLUME_UP : VK_VOLUME_DOWN);

    #endregion

    #region Taps

    private void HandleTap(int fingers, TouchpadSettings settings)
    {
        switch (fingers)
        {
            case 1 when settings.TapsEnabled:
                GestureTriggered?.Invoke("Tap: 1-Finger (Left Click)");
                LeftClick();
                _lastTapTime = DateTime.UtcNow;
                break;
            case 2 when settings.TapsEnabled && settings.TwoFingerTapEnabled:
                GestureTriggered?.Invoke("Tap: 2-Finger (Right Click)");
                RightClick();
                break;
            case 3:
                RunTapAction(settings.ThreeFingerTap, fingers);
                break;
            case >= 4:
                RunTapAction(settings.FourFingerTap, fingers);
                break;
        }
    }

    private void RunTapAction(TapAction action, int fingers)
    {
        if (action == TapAction.None) return;

        GestureTriggered?.Invoke($"Tap: {fingers}-Finger ({action})");
        switch (action)
        {
            case TapAction.Search:
                KeyCombo(VK_LWIN, VK_S);
                break;
            case TapAction.NotificationCenter:
                KeyCombo(VK_LWIN, VK_N);
                break;
            case TapAction.PlayPause:
                KeyCombo(VK_MEDIA_PLAY_PAUSE);
                break;
            case TapAction.MiddleClick:
                MiddleClick();
                break;
        }
    }

    #endregion

    #region Physical button

    private void HandlePhysicalButton(bool pressed, List<TouchContact> contacts)
    {
        if (pressed && !_isPhysicalDown)
        {
            _isPhysicalDown = true;
            _physicalUsedInSession = true;
            _physicalIsRight = IsRightClickPress(contacts, Settings);
            PressPhysical(down: true);
        }
        else if (!pressed && _isPhysicalDown)
        {
            _isPhysicalDown = false;
            PressPhysical(down: false);
        }
    }

    private void PressPhysical(bool down)
    {
        string state = down ? "DOWN" : "UP";
        if (_physicalIsRight)
        {
            RightButton(down);
            GestureTriggered?.Invoke($"Physical Right Click: {state}");
        }
        else
        {
            LeftButton(down);
            GestureTriggered?.Invoke($"Physical Click: {state}");
        }
    }

    /// <summary>Two-finger press, or a one-finger press in the bottom-right zone, is a right click.</summary>
    private static bool IsRightClickPress(List<TouchContact> contacts, TouchpadSettings settings)
    {
        if (contacts.Count >= 2) return true;
        if (!settings.RightClickZoneEnabled || contacts.Count != 1) return false;
        return contacts[0].X >= RightClickZoneMinX && contacts[0].Y >= RightClickZoneMinY;
    }

    #endregion
}
