namespace CodexStatus.Core;

public sealed record HookInstallResult(
    string HooksPath,
    string? BackupPath,
    int AddedHandlerCount,
    bool Modified);
