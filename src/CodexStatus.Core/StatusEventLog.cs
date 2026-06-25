using System.Text.Json;

namespace CodexStatus.Core;

public static class StatusEventLog
{
    public static void AppendEvent(string stateDirectory, AgentStatusSnapshot snapshot)
    {
        Directory.CreateDirectory(stateDirectory);
        var detail = SecretRedactor.Redact(snapshot.Detail);
        var line = JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            eventName = snapshot.LastHookEventName,
            sessionId = snapshot.SessionId,
            turnId = snapshot.TurnId,
            state = snapshot.State.ToString(),
            snapshot.Label,
            detail
        }, CodexJson.Default);

        File.AppendAllText(StateFileNames.GetEventsPath(stateDirectory), line + Environment.NewLine);
    }

    public static void AppendLog(string stateDirectory, string message)
    {
        Directory.CreateDirectory(stateDirectory);
        File.AppendAllText(
            StateFileNames.GetLogPath(stateDirectory),
            $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
    }
}
