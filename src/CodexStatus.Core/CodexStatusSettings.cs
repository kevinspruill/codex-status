namespace CodexStatus.Core;

public sealed class CodexStatusSettings
{
    public bool ShowFloatingPill { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool PlaySoundOnDone { get; set; }
    public bool PlaySoundOnApproval { get; set; } = true;
    public bool HideCommandText { get; set; }
    public bool RedactSecrets { get; set; } = true;
    public bool ShowElapsedTime { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool LockPillPosition { get; set; }
    public string CodexHomePath { get; set; }
    public string StateDirectory { get; set; }
    public int StaleAfterMinutes { get; set; } = 15;
    public int DoneVisibleSeconds { get; set; } = 20;
    public double? PillLeft { get; set; }
    public double? PillTop { get; set; }
    public string Theme { get; set; } = "system";

    public CodexStatusSettings()
    {
        CodexHomePath = PathHelpers.ResolveCodexHomePath();
        StateDirectory = PathHelpers.GetDefaultStateDirectory(CodexHomePath);
    }
}
