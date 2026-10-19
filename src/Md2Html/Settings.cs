using Microsoft.Win32;

namespace Md2Html;

/// <summary>Per-user settings in HKCU; autostart uses the standard Run key.</summary>
internal static class Settings
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "md2html";
    private const string SettingsKeyPath = @"Software\md2html";
    private const string AutoStartDisabledValue = "AutoStartDisabled";
    private const string NotificationsDisabledValue = "NotificationsDisabled";

    public static bool AutoStart
    {
        get => !GetFlag(AutoStartDisabledValue);
        set
        {
            SetFlag(AutoStartDisabledValue, !value);
            SyncAutoStart();
        }
    }

    public static bool Notifications
    {
        get => !GetFlag(NotificationsDisabledValue);
        set => SetFlag(NotificationsDisabledValue, !value);
    }

    /// <summary>Autostart is on unless the user turned it off; rewriting the path follows a moved exe.</summary>
    public static void SyncAutoStart()
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (AutoStart)
            run.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
        else
            run.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    private static bool GetFlag(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
        return key?.GetValue(name) is int v && v != 0;
    }

    private static void SetFlag(string name, bool value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath);
        key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
    }
}
