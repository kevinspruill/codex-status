using System.Text.Json;

namespace CodexStatus.Core;

public static class ExecJsonEventReducer
{
    public static AgentStatusSnapshot Reduce(
        JsonElement execEvent,
        AgentStatusSnapshot? existing = null,
        DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var snapshot = existing?.Clone() ?? new AgentStatusSnapshot { StartedAt = timestamp };
        var eventType = JsonElementReader.GetString(execEvent, "type", "event", "name") ?? "unknown";
        var itemKind = GetItemKind(execEvent);

        snapshot.SchemaVersion = SchemaVersions.Current;
        snapshot.Source = "codex";
        snapshot.Backend = "exec-json";
        snapshot.SessionId = JsonElementReader.GetString(execEvent, "session_id", "sessionId", "thread_id", "threadId")
            ?? snapshot.SessionId
            ?? "exec";
        snapshot.TurnId = JsonElementReader.GetString(execEvent, "turn_id", "turnId") ?? snapshot.TurnId;
        snapshot.Cwd = PathHelpers.NormalizePathForDisplay(JsonElementReader.GetString(execEvent, "cwd") ?? snapshot.Cwd ?? string.Empty);
        if (string.IsNullOrWhiteSpace(snapshot.Cwd))
        {
            snapshot.Cwd = null;
        }

        snapshot.RepoName = PathHelpers.GetRepoName(snapshot.Cwd) ?? snapshot.RepoName;
        snapshot.Model = JsonElementReader.GetString(execEvent, "model") ?? snapshot.Model;
        snapshot.LastRawEventType = eventType;
        snapshot.UpdatedAt = timestamp;

        switch (eventType)
        {
            case "thread.started":
                snapshot.State = AgentDisplayState.SessionStarted;
                snapshot.Label = "Codex session started";
                snapshot.CompletedAt = null;
                break;

            case "turn.started":
                snapshot.State = AgentDisplayState.Thinking;
                snapshot.Label = "Codex: Thinking";
                snapshot.StartedAt = timestamp;
                snapshot.CompletedAt = null;
                break;

            case "item.started":
                ApplyItemStarted(snapshot, itemKind, execEvent);
                snapshot.CompletedAt = null;
                break;

            case "item.completed":
                snapshot.State = AgentDisplayState.ProcessingResult;
                snapshot.Label = "Codex: Processing result";
                snapshot.Detail = itemKind;
                break;

            case "turn.completed":
                snapshot.State = AgentDisplayState.Done;
                snapshot.Label = "Codex: Done";
                snapshot.WaitingOnApproval = false;
                snapshot.CompletedAt = timestamp;
                break;

            case "turn.failed":
            case "error":
                snapshot.State = AgentDisplayState.Failed;
                snapshot.Label = "Codex: Failed";
                snapshot.Error = JsonElementReader.GetString(execEvent, "error", "message");
                snapshot.Detail = snapshot.Error;
                snapshot.CompletedAt = timestamp;
                break;

            default:
                snapshot.State = AgentDisplayState.Unknown;
                snapshot.Label = "Codex: Active";
                snapshot.Detail = eventType;
                break;
        }

        return snapshot;
    }

    private static void ApplyItemStarted(AgentStatusSnapshot snapshot, string? itemKind, JsonElement execEvent)
    {
        switch (itemKind)
        {
            case "reason":
            case "reasoning":
                snapshot.State = AgentDisplayState.Thinking;
                snapshot.Label = "Codex: Thinking";
                break;

            case "command_execution":
            case "commandExecution":
                snapshot.State = AgentDisplayState.RunningCommand;
                snapshot.Label = "Codex: Running command";
                snapshot.CommandPreview = SecretRedactor.Redact(JsonElementReader.GetString(execEvent, "command", "cmd"));
                snapshot.Detail = snapshot.CommandPreview;
                break;

            case "file_change":
            case "fileChange":
                snapshot.State = AgentDisplayState.EditingFiles;
                snapshot.Label = "Codex: Editing files";
                snapshot.Detail = JsonElementReader.GetString(execEvent, "path", "file");
                break;

            case "mcp_tool_call":
            case "mcpToolCall":
                snapshot.State = AgentDisplayState.UsingMcpTool;
                snapshot.Label = "Codex: Using MCP tool";
                snapshot.ToolName = JsonElementReader.GetString(execEvent, "tool_name", "toolName");
                snapshot.Detail = snapshot.ToolName;
                break;

            case "web_search":
            case "webSearch":
                snapshot.State = AgentDisplayState.SearchingWeb;
                snapshot.Label = "Codex: Searching web";
                snapshot.Detail = JsonElementReader.GetString(execEvent, "query");
                break;

            case "plan":
                snapshot.State = AgentDisplayState.Planning;
                snapshot.Label = "Codex: Planning";
                break;

            default:
                snapshot.State = AgentDisplayState.ProcessingResult;
                snapshot.Label = "Codex: Processing result";
                snapshot.Detail = itemKind;
                break;
        }
    }

    private static string? GetItemKind(JsonElement execEvent)
    {
        var kind = JsonElementReader.GetString(execEvent, "item_type", "itemType", "kind", "reason");
        if (!string.IsNullOrWhiteSpace(kind))
        {
            return kind;
        }

        var item = JsonElementReader.GetObject(execEvent, "item");
        return item is null ? null : JsonElementReader.GetString(item.Value, "type", "kind", "reason");
    }
}
