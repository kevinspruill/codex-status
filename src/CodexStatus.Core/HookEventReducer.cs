using System.Text.Json;

namespace CodexStatus.Core;

public static class HookEventReducer
{
    private const int MaxPayloadValueLength = 1_200;
    private const int MaxPayloadDepth = 8;

    public static AgentStatusSnapshot Reduce(
        JsonDocument document,
        AgentStatusSnapshot? existing = null,
        string? fallbackEventName = null,
        StatusReducerOptions? options = null,
        DateTimeOffset? now = null) =>
        Reduce(document.RootElement, existing, fallbackEventName, options, now);

    public static AgentStatusSnapshot Reduce(
        JsonElement hookEvent,
        AgentStatusSnapshot? existing = null,
        string? fallbackEventName = null,
        StatusReducerOptions? options = null,
        DateTimeOffset? now = null)
    {
        options ??= new StatusReducerOptions();
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var snapshot = existing?.Clone() ?? new AgentStatusSnapshot { StartedAt = timestamp };
        var eventName = JsonElementReader.GetString(hookEvent, "hook_event_name", "hookEventName", "event_name", "eventName", "event")
            ?? fallbackEventName
            ?? "Unknown";

        snapshot.SchemaVersion = SchemaVersions.Current;
        snapshot.Source = JsonElementReader.GetString(hookEvent, "source") ?? snapshot.Source;
        snapshot.Backend = "hooks";
        snapshot.SessionId = JsonElementReader.GetString(hookEvent, "session_id", "sessionId") ?? snapshot.SessionId;
        snapshot.TurnId = JsonElementReader.GetString(hookEvent, "turn_id", "turnId") ?? snapshot.TurnId;
        snapshot.Cwd = NormalizeNullablePath(JsonElementReader.GetString(hookEvent, "cwd", "current_working_directory", "currentWorkingDirectory")) ?? snapshot.Cwd;
        snapshot.RepoName = PathHelpers.GetRepoName(snapshot.Cwd) ?? snapshot.RepoName;
        snapshot.Model = JsonElementReader.GetString(hookEvent, "model") ?? snapshot.Model;
        snapshot.LastHookEventName = eventName;
        snapshot.LastRawEventType = JsonElementReader.GetString(hookEvent, "type", "raw_event_type", "rawEventType") ?? snapshot.LastRawEventType;
        snapshot.PayloadFields = BuildPayloadFields(hookEvent, options);
        snapshot.UpdatedAt = timestamp;
        snapshot.Error = null;

        var transcriptPath = JsonElementReader.GetString(hookEvent, "transcript_path", "transcriptPath");
        if (!string.IsNullOrWhiteSpace(transcriptPath))
        {
            snapshot.Metadata ??= [];
            snapshot.Metadata["transcriptPath"] = PathHelpers.NormalizePathForDisplay(transcriptPath);
        }

        var toolName = JsonElementReader.GetString(hookEvent, "tool_name", "toolName");
        var toolInput = JsonElementReader.GetObject(hookEvent, "tool_input", "toolInput");

        switch (eventName)
        {
            case "SessionStart":
                snapshot.State = AgentDisplayState.SessionStarted;
                snapshot.Label = "Codex session started";
                snapshot.Detail = JsonElementReader.GetString(hookEvent, "source");
                snapshot.CompletedAt = null;
                snapshot.WaitingOnApproval = false;
                break;

            case "UserPromptSubmit":
                snapshot.State = AgentDisplayState.Thinking;
                snapshot.Label = "Codex: Thinking";
                snapshot.StartedAt = timestamp;
                snapshot.UpdatedAt = timestamp;
                snapshot.CompletedAt = null;
                snapshot.Detail = null;
                snapshot.WaitingOnApproval = false;
                break;

            case "SubagentStart":
                snapshot.State = AgentDisplayState.SpawningAgent;
                snapshot.Label = "Codex: Spawning agent";
                snapshot.Detail = JsonElementReader.GetString(hookEvent, "agent_type", "agentType");
                snapshot.ActiveSubagentCount += 1;
                snapshot.AgentId = JsonElementReader.GetString(hookEvent, "agent_id", "agentId") ?? snapshot.AgentId;
                snapshot.CompletedAt = null;
                break;

            case "SubagentStop":
                snapshot.ActiveSubagentCount = Math.Max(0, snapshot.ActiveSubagentCount - 1);
                if (snapshot.ActiveSubagentCount > 0)
                {
                    snapshot.State = AgentDisplayState.RunningAgent;
                    snapshot.Label = "Codex: Agent running";
                }
                else
                {
                    snapshot.State = AgentDisplayState.Thinking;
                    snapshot.Label = "Codex: Thinking";
                }

                snapshot.Detail = JsonElementReader.GetString(hookEvent, "agent_type", "agentType") ?? snapshot.Detail;
                break;

            case "PreToolUse":
                ApplyPreToolUse(snapshot, toolName, toolInput, options);
                snapshot.CompletedAt = null;
                break;

            case "PermissionRequest":
                snapshot.State = AgentDisplayState.WaitingForApproval;
                snapshot.WaitingOnApproval = true;
                snapshot.Label = "Codex: Waiting for approval";
                snapshot.ToolName = toolName;
                snapshot.Detail = GetToolInputString(toolInput, "description", "reason") ?? toolName;
                snapshot.ApprovalReason = snapshot.Detail;
                snapshot.CompletedAt = null;
                break;

            case "PostToolUse":
                snapshot.WaitingOnApproval = false;
                snapshot.State = AgentDisplayState.ProcessingResult;
                snapshot.Label = "Codex: Processing result";
                snapshot.ToolName = toolName;
                snapshot.Detail = toolName;
                break;

            case "PreCompact":
                snapshot.State = AgentDisplayState.Compacting;
                snapshot.Label = "Codex: Compacting context";
                snapshot.Detail = JsonElementReader.GetString(hookEvent, "trigger");
                snapshot.CompletedAt = null;
                break;

            case "PostCompact":
                snapshot.State = AgentDisplayState.Thinking;
                snapshot.Label = "Codex: Thinking";
                snapshot.Detail = null;
                snapshot.CompletedAt = null;
                break;

            case "Stop":
                snapshot.WaitingOnApproval = false;
                snapshot.State = AgentDisplayState.Done;
                snapshot.Label = "Codex: Done";
                snapshot.Detail = null;
                snapshot.CompletedAt = timestamp;
                break;

            default:
                snapshot.State = AgentDisplayState.Unknown;
                snapshot.Label = "Codex: Active";
                snapshot.Detail = eventName;
                break;
        }

        return snapshot;
    }

    private static void ApplyPreToolUse(
        AgentStatusSnapshot snapshot,
        string? toolName,
        JsonElement? toolInput,
        StatusReducerOptions options)
    {
        snapshot.ToolName = toolName;
        snapshot.WaitingOnApproval = false;

        if (string.Equals(toolName, "Bash", StringComparison.Ordinal))
        {
            var command = GetCommandPreview(toolInput, options);
            snapshot.State = AgentDisplayState.RunningCommand;
            snapshot.Label = "Codex: Running command";
            snapshot.CommandPreview = command;
            snapshot.Detail = options.HideCommandText ? null : command;
            return;
        }

        if (string.Equals(toolName, "apply_patch", StringComparison.Ordinal)
            || string.Equals(toolName, "Edit", StringComparison.Ordinal)
            || string.Equals(toolName, "Write", StringComparison.Ordinal))
        {
            snapshot.State = AgentDisplayState.EditingFiles;
            snapshot.Label = "Codex: Editing files";
            snapshot.CommandPreview = GetCommandPreview(toolInput, options);
            snapshot.Detail = snapshot.CommandPreview ?? toolName;
            return;
        }

        if (!string.IsNullOrWhiteSpace(toolName) && toolName.StartsWith("mcp__", StringComparison.Ordinal))
        {
            snapshot.State = AgentDisplayState.UsingMcpTool;
            snapshot.Label = "Codex: Using MCP tool";
            snapshot.Detail = toolName;
            return;
        }

        snapshot.State = AgentDisplayState.UsingMcpTool;
        snapshot.Label = "Codex: Using tool";
        snapshot.Detail = toolName;
    }

    private static string? GetCommandPreview(JsonElement? toolInput, StatusReducerOptions options)
    {
        var command = GetToolInputString(toolInput, "command", "cmd");
        if (command is null)
        {
            return null;
        }

        if (options.RedactSecrets)
        {
            command = SecretRedactor.Redact(command) ?? string.Empty;
        }

        return Truncate(command, 160);
    }

    private static string? GetToolInputString(JsonElement? toolInput, params string[] names)
    {
        if (toolInput is null)
        {
            return null;
        }

        return JsonElementReader.GetString(toolInput.Value, names);
    }

    private static string? NormalizeNullablePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : PathHelpers.NormalizePathForDisplay(path);

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 3)] + "...";
    }

    private static Dictionary<string, string>? BuildPayloadFields(JsonElement hookEvent, StatusReducerOptions options)
    {
        if (hookEvent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in hookEvent.EnumerateObject())
        {
            AddPayloadField(fields, property.Name, property.Value, options, depth: 0);
        }

        return fields.Count == 0 ? null : fields;
    }

    private static void AddPayloadField(
        Dictionary<string, string> fields,
        string path,
        JsonElement value,
        StatusReducerOptions options,
        int depth)
    {
        fields[path] = FormatPayloadValue(path, value, options);

        if (depth >= MaxPayloadDepth)
        {
            return;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    AddPayloadField(fields, $"{path}.{property.Name}", property.Value, options, depth + 1);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    AddPayloadField(fields, $"{path}[{index++}]", item, options, depth + 1);
                }

                break;
        }
    }

    private static string FormatPayloadValue(string path, JsonElement value, StatusReducerOptions options)
    {
        var formatted = value.ValueKind switch
        {
            JsonValueKind.Object => $"{{object: {value.EnumerateObject().Count()} fields}}",
            JsonValueKind.Array => $"[array: {value.GetArrayLength()} items]",
            JsonValueKind.String => FormatPayloadString(path, value.GetString(), options),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Undefined => "undefined",
            _ => value.ToString()
        };

        if (options.RedactSecrets)
        {
            formatted = SecretRedactor.Redact(formatted) ?? string.Empty;
        }

        return Truncate(formatted, MaxPayloadValueLength);
    }

    private static string FormatPayloadString(string path, string? value, StatusReducerOptions options)
    {
        if (options.HideCommandText && IsCommandPayloadPath(path))
        {
            return "[hidden]";
        }

        return value ?? string.Empty;
    }

    private static bool IsCommandPayloadPath(string path) =>
        string.Equals(path, "command", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".command", StringComparison.OrdinalIgnoreCase);
}
