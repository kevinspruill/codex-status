using Microsoft.Win32;

namespace CodexStatus.Tray;

internal static class StartupRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Codex Status";

    public static void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key?.SetValue(ValueName, $"\"{executablePath}\"");
        }
        else
        {
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
