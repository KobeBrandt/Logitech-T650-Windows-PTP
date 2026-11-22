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

    private static TouchpadSettings FromKey(RegistryKey key) =>
        FromValues(name => key.GetValue(name) is int value ? value : null);

    /// <summary>Builds settings from raw DWORD values. <paramref name="readValue"/> returns null for a missing value.</summary>
    internal static TouchpadSettings FromValues(Func<string, int?> readValue)
    {
        var d = Defaults;
        return new TouchpadSettings(
            TapsEnabled: ReadBool(readValue, "TapsEnabled", d.TapsEnabled),
            TwoFingerTapEnabled: ReadBool(readValue, "TwoFingerTapEnabled", d.TwoFingerTapEnabled),
            TapAndDragEnabled: ReadBool(readValue, "TapAndDrag", d.TapAndDragEnabled),
            RightClickZoneEnabled: ReadBool(readValue, "RightClickZoneEnabled", d.RightClickZoneEnabled),
            ScrollEnabled: ReadBool(readValue, "PanEnabled", d.ScrollEnabled),
            ZoomEnabled: ReadBool(readValue, "ZoomEnabled", d.ZoomEnabled),
            NaturalScrolling: readValue("ScrollDirection") is not int dir || dir == 0,
            CursorSpeed: Math.Clamp(readValue("CursorSpeed") ?? d.CursorSpeed, 0, MaxCursorSpeed),
            ThreeFingerSwipe: ToSwipe(readValue("ThreeFingerSlideEnabled"), d.ThreeFingerSwipe),
            FourFingerSwipe: ToSwipe(readValue("FourFingerSlideEnabled"), d.FourFingerSwipe),
            ThreeFingerTap: ToTap(readValue("ThreeFingerTapEnabled"), d.ThreeFingerTap),
            FourFingerTap: ToTap(readValue("FourFingerTapEnabled"), d.FourFingerTap));
    }

    private static bool ReadBool(Func<string, int?> readValue, string name, bool fallback) =>
        readValue(name) is int value ? value != 0 : fallback;

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
