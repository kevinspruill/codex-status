using System.Runtime.InteropServices;
using System.Text;

namespace CodexStatus.Core;

public static class PathHelpers
{
    public static string ResolveCodexHomePath()
    {
        var env = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return NormalizePathForDisplay(Environment.ExpandEnvironmentVariables(env));
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            userProfile = Environment.GetEnvironmentVariable("USERPROFILE")
                ?? Environment.GetEnvironmentVariable("HOME")
                ?? ".";
        }

        return NormalizePathForDisplay(Path.Combine(userProfile, ".codex"));
    }

    public static string GetDefaultStateDirectory(string codexHomePath) =>
        NormalizePathForDisplay(Path.Combine(codexHomePath, "statusbar"));

    public static string GetSettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodexStatus",
            "settings.json");

    public static string? GetRepoName(string? cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd))
        {
            return null;
        }

        var trimmed = cwd.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/');
        if (trimmed.Length == 0)
        {
            return null;
        }

        return Path.GetFileName(trimmed);
    }

    public static string NormalizePathForDisplay(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return expanded.Replace('/', '\\');
        }

        return expanded.Replace('\\', '/');
    }

    public static string ToSafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }
}
