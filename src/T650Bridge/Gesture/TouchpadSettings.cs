using Microsoft.Win32;

namespace T650Bridge.Gesture;

/// <summary>What a three- or four-finger swipe does.</summary>
public enum SwipeAction
{
    None,
    SwitchApps,
    SwitchDesktops,
    AudioVolume
}

/// <summary>What a three- or four-finger tap does.</summary>
public enum TapAction
{
    None,
    Search,
    NotificationCenter,
    PlayPause,
    MiddleClick
}

/// <summary>
/// Snapshot of the user's Windows touchpad settings
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad).
/// Value meanings for the 3/4-finger entries follow community documentation, not an official Microsoft table.
/// </summary>
public sealed record TouchpadSettings(
    bool TapsEnabled,
    bool TwoFingerTapEnabled,
    bool TapAndDragEnabled,
    bool RightClickZoneEnabled,
    bool ScrollEnabled,
    bool ZoomEnabled,
    bool NaturalScrolling,
    int CursorSpeed,
    SwipeAction ThreeFingerSwipe,
    SwipeAction FourFingerSwipe,
    TapAction ThreeFingerTap,
    TapAction FourFingerTap)
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad";
    private const int DefaultCursorSpeed = 10;
    private const int MaxCursorSpeed = 20;

    /// <summary>Windows 11 out-of-the-box touchpad behavior.</summary>
    public static TouchpadSettings Defaults { get; } = new(
        TapsEnabled: true,
        TwoFingerTapEnabled: true,
        TapAndDragEnabled: true,
        RightClickZoneEnabled: true,
        ScrollEnabled: true,
        ZoomEnabled: true,
        NaturalScrolling: true,
        CursorSpeed: DefaultCursorSpeed,
        ThreeFingerSwipe: SwipeAction.SwitchApps,
        FourFingerSwipe: SwipeAction.SwitchDesktops,
        ThreeFingerTap: TapAction.Search,
        FourFingerTap: TapAction.NotificationCenter);

    /// <summary>Cursor speed as a multiplier, where the Windows default (10) is 1.0.</summary>
    public double CursorSpeedFactor => Math.Max(1, CursorSpeed) / (double)DefaultCursorSpeed;

    /// <summary>Reads the current settings. Missing values fall back to <see cref="Defaults"/>.</summary>
    public static TouchpadSettings Load()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
            return key == null ? Defaults : FromKey(key);
        }
        catch (Exception)
        {
            return Defaults;
        }
    }

    private static TouchpadSettings FromKey(RegistryKey key)
    {
        var d = Defaults;
        return new TouchpadSettings(
            TapsEnabled: ReadBool(key, "TapsEnabled", d.TapsEnabled),
            TwoFingerTapEnabled: ReadBool(key, "TwoFingerTapEnabled", d.TwoFingerTapEnabled),
            TapAndDragEnabled: ReadBool(key, "TapAndDrag", d.TapAndDragEnabled),
            RightClickZoneEnabled: ReadBool(key, "RightClickZoneEnabled", d.RightClickZoneEnabled),
            ScrollEnabled: ReadBool(key, "PanEnabled", d.ScrollEnabled),
            ZoomEnabled: ReadBool(key, "ZoomEnabled", d.ZoomEnabled),
            NaturalScrolling: ReadInt(key, "ScrollDirection") is not int dir || dir == 0,
            CursorSpeed: Math.Clamp(ReadInt(key, "CursorSpeed") ?? d.CursorSpeed, 0, MaxCursorSpeed),
            ThreeFingerSwipe: ToSwipe(ReadInt(key, "ThreeFingerSlideEnabled"), d.ThreeFingerSwipe),
            FourFingerSwipe: ToSwipe(ReadInt(key, "FourFingerSlideEnabled"), d.FourFingerSwipe),
            ThreeFingerTap: ToTap(ReadInt(key, "ThreeFingerTapEnabled"), d.ThreeFingerTap),
            FourFingerTap: ToTap(ReadInt(key, "FourFingerTapEnabled"), d.FourFingerTap));
    }

    private static int? ReadInt(RegistryKey key, string name) => key.GetValue(name) is int value ? value : null;

    private static bool ReadBool(RegistryKey key, string name, bool fallback) =>
        ReadInt(key, name) is int value ? value != 0 : fallback;

    private static SwipeAction ToSwipe(int? value, SwipeAction fallback) => value switch
    {
        null => fallback,
        0 => SwipeAction.SwitchApps,
        1 => SwipeAction.SwitchDesktops,
        2 => SwipeAction.AudioVolume,
        _ => SwipeAction.None
    };

    private static TapAction ToTap(int? value, TapAction fallback) => value switch
    {
        null => fallback,
        0 => TapAction.Search,
        1 => TapAction.NotificationCenter,
        2 => TapAction.PlayPause,
        3 => TapAction.MiddleClick,
        _ => TapAction.None
    };
}
