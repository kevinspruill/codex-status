using System.Text.Json;
using CodexStatus.Core;

namespace CodexStatus.Tests;

public sealed class HookEventReducerTests
{
    [Fact]
    public void UserPromptSubmitMapsToThinking()
    {
        var snapshot = Reduce("""{"hook_event_name":"UserPromptSubmit","session_id":"s1"}""");

        Assert.Equal(AgentDisplayState.Thinking, snapshot.State);
        Assert.Equal("Codex: Thinking", snapshot.Label);
    }

    [Fact]
    public void PreToolUseBashMapsToRunningCommand()
    {
        var snapshot = Reduce("""{"hook_event_name":"PreToolUse","session_id":"s1","tool_name":"Bash","tool_input":{"command":"npm test"}}""");

        Assert.Equal(AgentDisplayState.RunningCommand, snapshot.State);
        Assert.Equal("npm test", snapshot.CommandPreview);
        Assert.Equal("npm test", snapshot.Detail);
    }

    [Fact]
    public void PreToolUseApplyPatchMapsToEditingFiles()
    {
        var snapshot = Reduce("""{"hook_event_name":"PreToolUse","session_id":"s1","tool_name":"apply_patch","tool_input":{"command":"patch text"}}""");

        Assert.Equal(AgentDisplayState.EditingFiles, snapshot.State);
        Assert.Equal("patch text", snapshot.CommandPreview);
    }

    [Fact]
    public void PreToolUseMcpToolMapsToUsingMcpTool()
    {
        var snapshot = Reduce("""{"hook_event_name":"PreToolUse","session_id":"s1","tool_name":"mcp__server__tool","tool_input":{}}""");

        Assert.Equal(AgentDisplayState.UsingMcpTool, snapshot.State);
        Assert.Equal("mcp__server__tool", snapshot.Detail);
    }

    [Fact]
    public void PermissionRequestMapsToWaitingForApproval()
    {
        var snapshot = Reduce("""{"hook_event_name":"PermissionRequest","session_id":"s1","tool_name":"Bash","tool_input":{"description":"Run tests"}}""");

        Assert.Equal(AgentDisplayState.WaitingForApproval, snapshot.State);
        Assert.True(snapshot.WaitingOnApproval);
        Assert.Equal("Run tests", snapshot.ApprovalReason);
    }

    [Fact]
    public void HookPayloadFieldsAreFlattenedForTrayDisplay()
    {
        var snapshot = Reduce("""{"hook_event_name":"PostToolUse","session_id":"s1","turn_id":"t1","tool_name":"Bash","tool_input":{"command":"npm test","env":{"CI":true}},"tool_response":{"exit_code":0,"stdout":"ok"},"transcript_path":null}""");

        Assert.NotNull(snapshot.PayloadFields);
        Assert.Equal("PostToolUse", snapshot.PayloadFields["hook_event_name"]);
        Assert.Equal("s1", snapshot.PayloadFields["session_id"]);
        Assert.Equal("t1", snapshot.PayloadFields["turn_id"]);
        Assert.Equal("Bash", snapshot.PayloadFields["tool_name"]);
        Assert.Equal("{object: 2 fields}", snapshot.PayloadFields["tool_input"]);
        Assert.Equal("npm test", snapshot.PayloadFields["tool_input.command"]);
        Assert.Equal("true", snapshot.PayloadFields["tool_input.env.CI"]);
        Assert.Equal("0", snapshot.PayloadFields["tool_response.exit_code"]);
        Assert.Equal("ok", snapshot.PayloadFields["tool_response.stdout"]);
        Assert.Equal("null", snapshot.PayloadFields["transcript_path"]);
    }

    [Fact]
    public void HookPayloadFieldsRespectCommandHidingAndSecretRedaction()
    {
        using var document = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","session_id":"s1","tool_name":"Bash","tool_input":{"command":"OPENAI_API_KEY=sk-proj-abcdefghijklmnopqrstuvwxyz npm test --password hunter2","note":"token=abc123"}}""");

        var snapshot = HookEventReducer.Reduce(
            document,
            options: new StatusReducerOptions
            {
                HideCommandText = true,
                RedactSecrets = true
            });

        Assert.NotNull(snapshot.PayloadFields);
        Assert.Equal("[hidden]", snapshot.PayloadFields["tool_input.command"]);
        Assert.Equal($"token={SecretRedactor.Redacted}", snapshot.PayloadFields["tool_input.note"]);
    }

    [Fact]
    public void StopMapsToDone()
    {
        var snapshot = Reduce("""{"hook_event_name":"Stop","session_id":"s1"}""");

        Assert.Equal(AgentDisplayState.Done, snapshot.State);
        Assert.Equal("Codex: Done", snapshot.Label);
        Assert.NotNull(snapshot.CompletedAt);
    }

    [Fact]
    public void SubagentStartIncrementsActiveSubagentCount()
    {
        var snapshot = Reduce("""{"hook_event_name":"SubagentStart","session_id":"s1","agent_id":"a1","agent_type":"review"}""");

        Assert.Equal(1, snapshot.ActiveSubagentCount);
        Assert.Equal(AgentDisplayState.SpawningAgent, snapshot.State);
        Assert.Equal("a1", snapshot.AgentId);
    }

    [Fact]
    public void SubagentStopDecrementsButNeverBelowZero()
    {
        var existing = new AgentStatusSnapshot
        {
            SessionId = "s1",
            ActiveSubagentCount = 0
        };

        var snapshot = Reduce("""{"hook_event_name":"SubagentStop","session_id":"s1"}""", existing);

        Assert.Equal(0, snapshot.ActiveSubagentCount);
        Assert.Equal(AgentDisplayState.Thinking, snapshot.State);
    }

    private static AgentStatusSnapshot Reduce(string json, AgentStatusSnapshot? existing = null)
    {
        using var document = JsonDocument.Parse(json);
        return HookEventReducer.Reduce(document, existing);
    }
}
