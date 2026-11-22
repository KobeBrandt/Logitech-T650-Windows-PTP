using Microsoft.Win32;

namespace T650Bridge.UI;

/// <summary>
/// Per-user preferences stored in HKCU\Software\LogitechT650Ptp\Bridge.
/// </summary>
public static class UserSettings
{
    private const string SettingsKeyPath = @"Software\LogitechT650Ptp\Bridge";
    private const string NaturalScrollingValueName = "NaturalScrolling";
    private const bool NaturalScrollingDefault = true;

    /// <summary>Reads the natural scrolling preference. Returns the default when unset or unreadable.</summary>
    public static bool LoadNaturalScrolling()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, writable: false);
            return key?.GetValue(NaturalScrollingValueName) is int value
                ? value != 0
                : NaturalScrollingDefault;
        }
        catch (Exception)
        {
            return NaturalScrollingDefault;
        }
    }

    /// <summary>Saves the natural scrolling preference.</summary>
    public static void SaveNaturalScrolling(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath, writable: true);
        key.SetValue(NaturalScrollingValueName, enabled ? 1 : 0, RegistryValueKind.DWord);
    }
}
