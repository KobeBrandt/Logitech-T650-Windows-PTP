using T650Bridge.Gesture;

namespace T650Bridge.Tests;

public class TouchpadSettingsTests
{
    private static TouchpadSettings From(params (string Name, int Value)[] values)
    {
        var map = values.ToDictionary(v => v.Name, v => v.Value);
        return TouchpadSettings.FromValues(name => map.TryGetValue(name, out int value) ? value : null);
    }

    [Fact]
    public void NoValues_GivesWindowsDefaults()
    {
        Assert.Equal(TouchpadSettings.Defaults, From());
    }

    [Fact]
    public void Defaults_MatchWindows11()
    {
        var d = TouchpadSettings.Defaults;
        Assert.Equal(SwipeAction.SwitchApps, d.ThreeFingerSwipe);
        Assert.Equal(SwipeAction.SwitchDesktops, d.FourFingerSwipe);
        Assert.Equal(TapAction.Search, d.ThreeFingerTap);
        Assert.Equal(TapAction.NotificationCenter, d.FourFingerTap);
        Assert.True(d.NaturalScrolling);
    }

    [Theory]
    [InlineData("TapsEnabled")]
    [InlineData("TwoFingerTapEnabled")]
    [InlineData("TapAndDrag")]
    [InlineData("RightClickZoneEnabled")]
    [InlineData("PanEnabled")]
    [InlineData("ZoomEnabled")]
    public void ZeroValue_TurnsFeatureOff(string name)
    {
        var s = From((name, 0));
        bool value = name switch
        {
            "TapsEnabled" => s.TapsEnabled,
            "TwoFingerTapEnabled" => s.TwoFingerTapEnabled,
            "TapAndDrag" => s.TapAndDragEnabled,
            "RightClickZoneEnabled" => s.RightClickZoneEnabled,
            "PanEnabled" => s.ScrollEnabled,
            _ => s.ZoomEnabled
        };
        Assert.False(value);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void ScrollDirection_MapsToNaturalScrolling(int raw, bool natural)
    {
        Assert.Equal(natural, From(("ScrollDirection", raw)).NaturalScrolling);
    }

    [Theory]
    [InlineData(0, SwipeAction.SwitchApps)]
    [InlineData(1, SwipeAction.SwitchDesktops)]
    [InlineData(2, SwipeAction.AudioVolume)]
    [InlineData(3, SwipeAction.None)]
    public void SlideValue_MapsToSwipeAction(int raw, SwipeAction expected)
    {
        Assert.Equal(expected, From(("ThreeFingerSlideEnabled", raw)).ThreeFingerSwipe);
        Assert.Equal(expected, From(("FourFingerSlideEnabled", raw)).FourFingerSwipe);
    }

    [Theory]
    [InlineData(0, TapAction.Search)]
    [InlineData(1, TapAction.NotificationCenter)]
    [InlineData(2, TapAction.PlayPause)]
    [InlineData(3, TapAction.MiddleClick)]
    [InlineData(9, TapAction.None)]
    public void TapValue_MapsToTapAction(int raw, TapAction expected)
    {
        Assert.Equal(expected, From(("ThreeFingerTapEnabled", raw)).ThreeFingerTap);
        Assert.Equal(expected, From(("FourFingerTapEnabled", raw)).FourFingerTap);
    }

    [Theory]
    [InlineData(10, 1.0)]
    [InlineData(20, 2.0)]
    [InlineData(0, 0.1)]
    [InlineData(99, 2.0)]
    public void CursorSpeed_IsClampedAndScaled(int raw, double factor)
    {
        Assert.Equal(factor, From(("CursorSpeed", raw)).CursorSpeedFactor, precision: 3);
    }
}
