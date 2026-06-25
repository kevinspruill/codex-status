namespace CodexStatus.Core;

public enum AgentDisplayState
{
    Unknown,
    Idle,
    SessionStarted,
    Thinking,
    Planning,
    SpawningAgent,
    RunningAgent,
    RunningCommand,
    EditingFiles,
    UsingMcpTool,
    SearchingWeb,
    Compacting,
    WaitingForApproval,
    ProcessingResult,
    Done,
    Failed,
    Stale
}
