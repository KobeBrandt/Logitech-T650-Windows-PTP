using T650Bridge.Gesture;
using static T650Bridge.Tests.GestureHarness;

namespace T650Bridge.Tests;

public class PrecisionGestureEngineTests
{
    private static string Down(byte key) => $"KeyDown:{key:X2}";

    private static string Up(byte key) => $"KeyUp:{key:X2}";

    [Fact]
    public void OneFinger_Move_SendsMouseMove()
    {
        var h = new GestureHarness();
        h.Move(3, 10, 0, Fingers(1));

        Assert.Contains(h.Input.Events, e => e.StartsWith("Move:", StringComparison.Ordinal));
    }

    [Fact]
    public void OneFinger_Tap_SendsLeftClick()
    {
        var h = new GestureHarness();
        h.Tap(Fingers(1));

        Assert.Equal(["LeftDown", "LeftUp"], h.Input.Events);
    }

    [Fact]
    public void OneFinger_Tap_WhenTapsDisabled_SendsNothing()
    {
        var h = new GestureHarness(TouchpadSettings.Defaults with { TapsEnabled = false });
        h.Tap(Fingers(1));

        Assert.Empty(h.Input.Events);
    }

    [Fact]
    public void OneFinger_SlowTouch_IsNotATap()
    {
        var h = new GestureHarness();
        h.Frame(Fingers(1));
        h.Advance(500);
        h.Lift();

        Assert.Empty(h.Input.Events);
    }

    [Fact]
    public void TwoFinger_Tap_SendsRightClick()
    {
        var h = new GestureHarness();
        h.Tap(Fingers(2));

        Assert.Equal(["RightDown", "RightUp"], h.Input.Events);
    }

    [Fact]
    public void TwoFinger_Tap_WhenTwoFingerTapDisabled_SendsNothing()
    {
        var h = new GestureHarness(TouchpadSettings.Defaults with { TwoFingerTapEnabled = false });
        h.Tap(Fingers(2));

        Assert.Empty(h.Input.Events);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, -1)]
    public void TwoFinger_DragDown_ScrollsByDirectionSetting(bool natural, int expectedSign)
    {
        var h = new GestureHarness(TouchpadSettings.Defaults with { NaturalScrolling = natural });
        h.Move(5, 0, 10, Fingers(2));

        var wheel = h.Input.Events.Where(e => e.StartsWith("Wheel:", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(wheel);
        Assert.All(wheel, e => Assert.Equal(expectedSign, Math.Sign(int.Parse(e[6..], System.Globalization.CultureInfo.InvariantCulture))));
    }

    [Fact]
    public void TwoFinger_Drag_WhenScrollDisabled_SendsNoWheel()
    {
        var h = new GestureHarness(TouchpadSettings.Defaults with { ScrollEnabled = false });
        h.Move(5, 0, 10, Fingers(2));

        Assert.DoesNotContain(h.Input.Events, e => e.Contains("Wheel", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoFinger_SpreadApart_ZoomsInWithCtrlWheel()
    {
        var h = new GestureHarness();
        for (int f = 0; f <= 4; f++)
            h.Frame((1, 1000 - (40 * f), 1000), (2, 1400 + (40 * f), 1000));

        Assert.Equal([Down(VirtualKey.Control), "Wheel:120", Up(VirtualKey.Control)], h.Input.Events.Take(3));
    }

    [Fact]
    public void TwoFinger_Pinch_WhenZoomDisabled_DoesNotZoom()
    {
        var h = new GestureHarness(TouchpadSettings.Defaults with { ZoomEnabled = false });
        for (int f = 0; f <= 4; f++)
            h.Frame((1, 1000 - (40 * f), 1000), (2, 1400 + (40 * f), 1000));

        Assert.DoesNotContain(Down(VirtualKey.Control), h.Input.Events);
    }

    [Fact]
    public void ThreeFinger_SwipeRight_HoldsAltAndTabsUntilLift()
    {
        var h = new GestureHarness();
        h.Move(4, 50, 0, Fingers(3));

        Assert.Equal([Down(VirtualKey.Alt), Down(VirtualKey.Tab), Up(VirtualKey.Tab)], h.Input.Events);

        h.Lift();
        Assert.Equal(Up(VirtualKey.Alt), h.Input.Events[^1]);
    }

    [Fact]
    public void ThreeFinger_LongSwipeRight_StepsThroughApps()
    {
        var h = new GestureHarness();
        h.Move(10, 50, 0, Fingers(3));

        Assert.True(h.Input.Events.Count(e => e == Down(VirtualKey.Tab)) >= 2);
    }

    [Fact]
    public void ThreeFinger_SwipeUp_OpensTaskView()
    {
        var h = new GestureHarness();
        h.Move(4, 0, -50, Fingers(3));

        Assert.Equal(
            [Down(VirtualKey.LeftWindows), Down(VirtualKey.Tab), Up(VirtualKey.Tab), Up(VirtualKey.LeftWindows)],
            h.Input.Events);
    }

    [Fact]
    public void ThreeFinger_Tap_OpensSearch()
    {
        var h = new GestureHarness();
        h.Tap(Fingers(3));

        Assert.Equal(
            [Down(VirtualKey.LeftWindows), Down(VirtualKey.S), Up(VirtualKey.S), Up(VirtualKey.LeftWindows)],
            h.Input.Events);
    }

    [Fact]
    public void ThreeFinger_Tap_MiddleClickSetting_SendsMiddleClick()
    {
        var h = new GestureHarness(TouchpadSettings.Defaults with { ThreeFingerTap = TapAction.MiddleClick });
        h.Tap(Fingers(3));

        Assert.Equal(["MiddleClick"], h.Input.Events);
    }

    [Fact]
    public void FourFinger_SwipeLeft_GoesToNextDesktop()
    {
        var h = new GestureHarness();
        h.Move(4, -50, 0, Fingers(4, x: 1500));

        Assert.Equal(
            [Down(VirtualKey.Control), Down(VirtualKey.LeftWindows), Down(VirtualKey.Right),
             Up(VirtualKey.Right), Up(VirtualKey.LeftWindows), Up(VirtualKey.Control)],
            h.Input.Events);
    }

    [Fact]
    public void FourFinger_SwipeRight_GoesToPreviousDesktop()
    {
        var h = new GestureHarness();
        h.Move(4, 50, 0, Fingers(4));

        Assert.Contains(Down(VirtualKey.Left), h.Input.Events);
    }

    [Fact]
    public void FourFinger_SwipeDown_ShowsDesktop()
    {
        var h = new GestureHarness();
        h.Move(4, 0, 50, Fingers(4));

        Assert.Equal(
            [Down(VirtualKey.LeftWindows), Down(VirtualKey.D), Up(VirtualKey.D), Up(VirtualKey.LeftWindows)],
            h.Input.Events);
    }

    [Fact]
    public void FourFinger_Tap_OpensNotificationCenter()
    {
        var h = new GestureHarness();
        h.Tap(Fingers(4));

        Assert.Equal(
            [Down(VirtualKey.LeftWindows), Down(VirtualKey.N), Up(VirtualKey.N), Up(VirtualKey.LeftWindows)],
            h.Input.Events);
    }

    [Fact]
    public void FourFinger_Swipe_WhenSetToNothing_SendsNothing()
    {
        var h = new GestureHarness(TouchpadSettings.Defaults with { FourFingerSwipe = SwipeAction.None });
        h.Move(4, 50, 0, Fingers(4));
        h.Lift();

        Assert.Empty(h.Input.Events);
    }

    [Fact]
    public void AudioSwipe_Up_StepsVolumeUp()
    {
        var h = new GestureHarness(TouchpadSettings.Defaults with { ThreeFingerSwipe = SwipeAction.AudioVolume });
        h.Move(10, 0, -50, Fingers(3, y: 1800));

        Assert.True(h.Input.Events.Count(e => e == Down(VirtualKey.VolumeUp)) >= 2);
    }

    [Fact]
    public void TapThenTouchAndMove_DragsWithLeftButton()
    {
        var h = new GestureHarness();
        h.Tap(Fingers(1));
        h.Advance(100);
        h.Move(5, 20, 0, Fingers(1));
        h.Lift();

        Assert.Equal(["LeftDown", "LeftUp", "LeftDown"], h.Input.Without("Move:").Take(3));
        Assert.Equal("LeftUp", h.Input.Events[^1]);
    }

    [Fact]
    public void TapThenLateTouchAndMove_DoesNotDrag()
    {
        var h = new GestureHarness();
        h.Tap(Fingers(1));
        h.Advance(1000);
        h.Move(5, 20, 0, Fingers(1));
        h.Lift();

        Assert.Equal(["LeftDown", "LeftUp"], h.Input.Without("Move:"));
    }

    [Fact]
    public void PhysicalClick_OneFinger_IsLeftClick()
    {
        var h = new GestureHarness();
        h.Frame(true, (1, 500, 500));
        h.Frame(false, (1, 500, 500));
        h.Lift();

        Assert.Equal(["LeftDown", "LeftUp"], h.Input.Events);
    }

    [Fact]
    public void PhysicalClick_TwoFingers_IsRightClick()
    {
        var h = new GestureHarness();
        h.Frame(true, (1, 500, 500), (2, 900, 500));
        h.Frame(false, (1, 500, 500), (2, 900, 500));
        h.Lift();

        Assert.Equal(["RightDown", "RightUp"], h.Input.Events);
    }

    [Theory]
    [InlineData(true, "RightDown")]
    [InlineData(false, "LeftDown")]
    public void PhysicalClick_BottomRightCorner_FollowsRightClickZoneSetting(bool zoneEnabled, string expected)
    {
        var h = new GestureHarness(TouchpadSettings.Defaults with { RightClickZoneEnabled = zoneEnabled });
        h.Frame(true, (1, 2600, 2200));

        Assert.Equal([expected], h.Input.Events);
    }
}
