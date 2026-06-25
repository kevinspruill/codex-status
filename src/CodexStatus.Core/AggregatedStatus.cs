namespace CodexStatus.Core;

public sealed class AggregatedStatus
{
    public int SchemaVersion { get; set; } = SchemaVersions.Current;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public AgentStatusSnapshot? Primary { get; set; }
    public List<AgentStatusSnapshot> Sessions { get; set; } = [];
    public int ActiveCount { get; set; }
    public int WaitingApprovalCount { get; set; }
    public int FailedCount { get; set; }
}
