namespace CodexStatus.Core;

public sealed class StatusReducerOptions
{
    public bool HideCommandText { get; set; }
    public bool RedactSecrets { get; set; } = true;
}
