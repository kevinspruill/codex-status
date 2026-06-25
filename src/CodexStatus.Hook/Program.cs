using System.Text;
using System.Text.Json;
using CodexStatus.Core;

Console.InputEncoding = Encoding.UTF8;

var arguments = CommandLineOptions.Parse(args);
var settings = SettingsStore.Load();
var stateDirectory = arguments.StateDirectory
    ?? settings.StateDirectory
    ?? PathHelpers.GetDefaultStateDirectory(PathHelpers.ResolveCodexHomePath());

try
{
    Directory.CreateDirectory(stateDirectory);
    var stdin = await Console.In.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(stdin))
    {
        return 0;
    }

    stdin = stdin.TrimStart('\uFEFF');
    using var document = JsonDocument.Parse(stdin);
    var eventName = GetString(document.RootElement, "hook_event_name", "hookEventName", "event_name", "eventName")
        ?? arguments.EventName;
    var sessionId = GetString(document.RootElement, "session_id", "sessionId") ?? "unknown";
    var sessionPath = StateFileNames.GetSessionPath(stateDirectory, sessionId);
    var existing = AtomicJsonFile.Read<AgentStatusSnapshot>(sessionPath);

    var snapshot = HookEventReducer.Reduce(
        document,
        existing,
        eventName,
        new StatusReducerOptions
        {
            HideCommandText = settings.HideCommandText,
            RedactSecrets = settings.RedactSecrets
        });

    AtomicJsonFile.Write(StateFileNames.GetSessionPath(stateDirectory, snapshot.SessionId), snapshot);
    StatusAggregator.WriteAggregate(stateDirectory);
    StatusEventLog.AppendEvent(stateDirectory, snapshot);
}
catch (Exception exception)
{
    try
    {
        StatusEventLog.AppendLog(stateDirectory, $"hook error: {exception}");
    }
    catch
    {
        // The hook must not break Codex even if local logging fails.
    }

    if (arguments.Debug)
    {
        Console.Error.WriteLine(exception);
    }
}

return 0;

static string? GetString(JsonElement element, params string[] names)
{
    if (element.ValueKind != JsonValueKind.Object)
    {
        return null;
    }

    foreach (var name in names)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }
    }

    return null;
}

internal sealed class CommandLineOptions
{
    public string? EventName { get; private init; }
    public string? StateDirectory { get; private init; }
    public bool Debug { get; private init; }

    public static CommandLineOptions Parse(string[] args)
    {
        string? eventName = null;
        string? stateDirectory = null;
        var debug = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--event" when index + 1 < args.Length:
                    eventName = args[++index];
                    break;
                case "--state-dir" when index + 1 < args.Length:
                    stateDirectory = args[++index];
                    break;
                case "--debug":
                    debug = true;
                    break;
            }
        }

        return new CommandLineOptions
        {
            EventName = eventName,
            StateDirectory = stateDirectory,
            Debug = debug
        };
    }
}
