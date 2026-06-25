using System.Text.Json.Nodes;

namespace CodexStatus.Core;

public static class CodexHookInstaller
{
    private static readonly (string EventName, string? Matcher)[] HookEvents =
    [
        ("SessionStart", "startup|resume|clear|compact"),
        ("UserPromptSubmit", null),
        ("PreToolUse", "*"),
        ("PermissionRequest", "*"),
        ("PostToolUse", "*"),
        ("PreCompact", "manual|auto"),
        ("PostCompact", "manual|auto"),
        ("SubagentStart", "*"),
        ("SubagentStop", "*"),
        ("Stop", null)
    ];

    public static HookInstallResult Install(
        string codexHomePath,
        string hookExecutablePath,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(codexHomePath))
        {
            throw new ArgumentException("Codex home path is required.", nameof(codexHomePath));
        }

        if (string.IsNullOrWhiteSpace(hookExecutablePath))
        {
            throw new ArgumentException("Hook executable path is required.", nameof(hookExecutablePath));
        }

        Directory.CreateDirectory(codexHomePath);
        var installedHookExecutablePath = InstallHookExecutable(codexHomePath, hookExecutablePath);
        var hooksPath = Path.Combine(codexHomePath, "hooks.json");
        var existed = File.Exists(hooksPath);
        var root = LoadRoot(hooksPath);
        var hooks = EnsureObject(root, "hooks");
        var added = 0;
        var modified = false;

        foreach (var hookEvent in HookEvents)
        {
            if (EnsureHandler(hooks, hookEvent.EventName, hookEvent.Matcher, installedHookExecutablePath, out var addedHandler))
            {
                modified = true;
                if (addedHandler)
                {
                    added++;
                }
            }
        }

        string? backupPath = null;
        if (modified)
        {
            if (existed)
            {
                backupPath = $"{hooksPath}.bak.{(now ?? DateTimeOffset.Now):yyyyMMddHHmmss}";
                File.Copy(hooksPath, backupPath, overwrite: false);
            }

            AtomicJsonFile.Write(hooksPath, root, CodexJson.Indented);
        }

        return new HookInstallResult(hooksPath, backupPath, added, modified);
    }

    private static string InstallHookExecutable(string codexHomePath, string hookExecutablePath)
    {
        var sourcePath = Path.GetFullPath(hookExecutablePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Hook executable was not found.", sourcePath);
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath)
            ?? throw new ArgumentException("Hook executable path must include a directory.", nameof(hookExecutablePath));
        var installDirectory = Path.Combine(PathHelpers.GetDefaultStateDirectory(codexHomePath), "hook");
        Directory.CreateDirectory(installDirectory);

        var installPath = Path.Combine(installDirectory, Path.GetFileName(sourcePath));
        if (!string.Equals(
                Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                var destinationPath = Path.Combine(installDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(file, destinationPath, overwrite: true);
            }
        }

        return PathHelpers.NormalizePathForDisplay(installPath);
    }

    private static JsonObject LoadRoot(string hooksPath)
    {
        if (!File.Exists(hooksPath))
        {
            return [];
        }

        var parsed = JsonNode.Parse(File.ReadAllText(hooksPath));
        return parsed as JsonObject ?? [];
    }

    private static JsonObject EnsureObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }

    private static bool EnsureHandler(
        JsonObject hooks,
        string eventName,
        string? matcher,
        string hookExecutablePath,
        out bool addedHandler)
    {
        addedHandler = false;
        var eventArray = hooks[eventName] as JsonArray;
        if (eventArray is null)
        {
            eventArray = [];
            hooks[eventName] = eventArray;
        }

        var command = BuildCommand(hookExecutablePath, eventName);
        var legacyCommand = $"codex-status-hook --event {eventName}";
        var existingHook = FindExistingHook(eventArray, command, legacyCommand, eventName);
        if (existingHook is not null)
        {
            return NormalizeHook(existingHook, command);
        }

        var group = new JsonObject();
        if (!string.IsNullOrWhiteSpace(matcher))
        {
            group["matcher"] = matcher;
        }

        group["hooks"] = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "command",
                ["command"] = command,
                ["commandWindows"] = command,
                ["timeout"] = 5,
                ["statusMessage"] = "Updating Codex Status"
            }
        };

        eventArray.Add(group);
        addedHandler = true;
        return true;
    }

    private static string BuildCommand(string hookExecutablePath, string eventName)
    {
        var executable = hookExecutablePath.Any(char.IsWhiteSpace)
            ? $"\"{hookExecutablePath}\""
            : hookExecutablePath;
        return $"{executable} --event {eventName}";
    }

    private static JsonObject? FindExistingHook(JsonArray eventArray, string command, string legacyCommand, string eventName)
    {
        foreach (var groupNode in eventArray)
        {
            if (groupNode is not JsonObject group || group["hooks"] is not JsonArray hookArray)
            {
                continue;
            }

            foreach (var hookNode in hookArray)
            {
                if (hookNode is not JsonObject hook)
                {
                    continue;
                }

                var existingCommandWindows = hook["commandWindows"]?.GetValue<string>();
                var existingCommand = hook["command"]?.GetValue<string>();
                if (string.Equals(existingCommandWindows, command, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(existingCommand, command, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(existingCommand, legacyCommand, StringComparison.OrdinalIgnoreCase)
                    || IsCodexStatusHookCommand(existingCommand, eventName)
                    || IsCodexStatusHookCommand(existingCommandWindows, eventName))
                {
                    return hook;
                }
            }
        }

        return null;
    }

    private static bool IsCodexStatusHookCommand(string? command, string eventName) =>
        command is not null
        && command.Contains("CodexStatus.Hook.exe", StringComparison.OrdinalIgnoreCase)
        && command.Contains($"--event {eventName}", StringComparison.OrdinalIgnoreCase);

    private static bool NormalizeHook(JsonObject hook, string command)
    {
        var modified = false;
        modified |= SetIfDifferent(hook, "type", "command");
        modified |= SetIfDifferent(hook, "command", command);
        modified |= SetIfDifferent(hook, "commandWindows", command);
        modified |= SetIfDifferent(hook, "statusMessage", "Updating Codex Status");

        if (hook["timeout"]?.GetValue<int>() != 5)
        {
            hook["timeout"] = 5;
            modified = true;
        }

        return modified;
    }

    private static bool SetIfDifferent(JsonObject hook, string propertyName, string value)
    {
        if (hook[propertyName]?.GetValue<string>() == value)
        {
            return false;
        }

        hook[propertyName] = value;
        return true;
    }
}
