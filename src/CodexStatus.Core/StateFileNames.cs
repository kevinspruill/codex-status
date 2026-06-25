namespace CodexStatus.Core;

public static class StateFileNames
{
    public const string AggregatedStateFileName = "state.json";
    public const string SessionsDirectoryName = "sessions";
    public const string EventsFileName = "events.jsonl";
    public const string LogFileName = "codex-status.log";

    public static string GetStatePath(string stateDirectory) =>
        Path.Combine(stateDirectory, AggregatedStateFileName);

    public static string GetSessionsDirectory(string stateDirectory) =>
        Path.Combine(stateDirectory, SessionsDirectoryName);

    public static string GetSessionPath(string stateDirectory, string sessionId) =>
        Path.Combine(GetSessionsDirectory(stateDirectory), $"{PathHelpers.ToSafeFileName(sessionId)}.json");

    public static string GetEventsPath(string stateDirectory) =>
        Path.Combine(stateDirectory, EventsFileName);

    public static string GetLogPath(string stateDirectory) =>
        Path.Combine(stateDirectory, LogFileName);
}
