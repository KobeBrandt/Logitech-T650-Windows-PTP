using Microsoft.Win32;

namespace T650Bridge.UI;

/// <summary>
/// Manages the per-user "run at login" entry in HKCU\...\CurrentVersion\Run.
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Logitech T650 Bridge";
    private const string StartupArguments = "--silent";

    /// <summary>True when a run-at-login entry exists for this app.</summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>Registers the currently running exe to start hidden at login.</summary>
    public static void Enable()
    {
        string exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot resolve the running executable path.");

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, $"\"{exePath}\" {StartupArguments}", RegistryValueKind.String);
    }

    /// <summary>Removes the run-at-login entry if present.</summary>
    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
