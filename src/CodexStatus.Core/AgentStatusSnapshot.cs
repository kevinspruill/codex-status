namespace CodexStatus.Core;

public sealed class AgentStatusSnapshot
{
    public int SchemaVersion { get; set; } = SchemaVersions.Current;
    public string Source { get; set; } = "codex";
    public string Backend { get; set; } = "hooks";
    public string SessionId { get; set; } = "unknown";
    public string? TurnId { get; set; }
    public string? AgentId { get; set; }
    public string? Cwd { get; set; }
    public string? RepoName { get; set; }
    public string? Model { get; set; }
    public AgentDisplayState State { get; set; } = AgentDisplayState.Unknown;
    public string Label { get; set; } = "Codex: Active";
    public string? Detail { get; set; }
    public string? ToolName { get; set; }
    public string? CommandPreview { get; set; }
    public string? ApprovalReason { get; set; }
    public int ActiveSubagentCount { get; set; }
    public bool WaitingOnApproval { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastHookEventName { get; set; }
    public string? LastRawEventType { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, string>? PayloadFields { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    public AgentStatusSnapshot Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        Source = Source,
        Backend = Backend,
        SessionId = SessionId,
        TurnId = TurnId,
        AgentId = AgentId,
        Cwd = Cwd,
        RepoName = RepoName,
        Model = Model,
        State = State,
        Label = Label,
        Detail = Detail,
        ToolName = ToolName,
        CommandPreview = CommandPreview,
        ApprovalReason = ApprovalReason,
        ActiveSubagentCount = ActiveSubagentCount,
        WaitingOnApproval = WaitingOnApproval,
        StartedAt = StartedAt,
        UpdatedAt = UpdatedAt,
        CompletedAt = CompletedAt,
        LastHookEventName = LastHookEventName,
        LastRawEventType = LastRawEventType,
        Error = Error,
        PayloadFields = PayloadFields is null ? null : new Dictionary<string, string>(PayloadFields),
        Metadata = Metadata is null ? null : new Dictionary<string, string>(Metadata)
    };
}
