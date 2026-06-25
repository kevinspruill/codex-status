namespace CodexStatus.Core;

public static class SettingsStore
{
    public static CodexStatusSettings Load(string? path = null)
    {
        path ??= PathHelpers.GetSettingsPath();
        try
        {
            var settings = AtomicJsonFile.Read<CodexStatusSettings>(path) ?? new CodexStatusSettings();
            Normalize(settings);
            return settings;
        }
        catch
        {
            return new CodexStatusSettings();
        }
    }

    public static void Save(CodexStatusSettings settings, string? path = null)
    {
        path ??= PathHelpers.GetSettingsPath();
        Normalize(settings);
        AtomicJsonFile.Write(path, settings);
    }

    private static void Normalize(CodexStatusSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.CodexHomePath))
        {
            settings.CodexHomePath = PathHelpers.ResolveCodexHomePath();
        }

        settings.CodexHomePath = PathHelpers.NormalizePathForDisplay(settings.CodexHomePath);

        if (string.IsNullOrWhiteSpace(settings.StateDirectory))
        {
            settings.StateDirectory = PathHelpers.GetDefaultStateDirectory(settings.CodexHomePath);
        }

        settings.StateDirectory = PathHelpers.NormalizePathForDisplay(settings.StateDirectory);
    }
}
