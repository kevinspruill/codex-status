using System.Text;
using System.Text.Json;
using CodexStatus.Core;

Console.InputEncoding = Encoding.UTF8;

var options = ExecOptions.Parse(args);
var stateDirectory = options.StateDirectory
    ?? SettingsStore.Load().StateDirectory
    ?? PathHelpers.GetDefaultStateDirectory(PathHelpers.ResolveCodexHomePath());
var snapshots = new Dictionary<string, AgentStatusSnapshot>(StringComparer.OrdinalIgnoreCase);

Directory.CreateDirectory(stateDirectory);

while (await Console.In.ReadLineAsync() is { } line)
{
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    try
    {
        using var document = JsonDocument.Parse(line);
        var preliminary = ExecJsonEventReducer.Reduce(document.RootElement);
        if (!snapshots.TryGetValue(preliminary.SessionId, out var existing))
        {
            existing = AtomicJsonFile.Read<AgentStatusSnapshot>(
                StateFileNames.GetSessionPath(stateDirectory, preliminary.SessionId));
        }

        var snapshot = ExecJsonEventReducer.Reduce(document.RootElement, existing);
        snapshots[snapshot.SessionId] = snapshot;
        AtomicJsonFile.Write(StateFileNames.GetSessionPath(stateDirectory, snapshot.SessionId), snapshot);
        StatusAggregator.WriteAggregate(stateDirectory);
    }
    catch (Exception exception)
    {
        StatusEventLog.AppendLog(stateDirectory, $"exec-json adapter error: {exception.Message}");
        if (options.Debug)
        {
            Console.Error.WriteLine(exception);
        }
    }
}

return 0;

internal sealed class ExecOptions
{
    public string? StateDirectory { get; private init; }
    public bool Debug { get; private init; }

    public static ExecOptions Parse(string[] args)
    {
        string? stateDirectory = null;
        var debug = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--state-dir" when index + 1 < args.Length:
                    stateDirectory = args[++index];
                    break;
                case "--debug":
                    debug = true;
                    break;
            }
        }

        return new ExecOptions
        {
            StateDirectory = stateDirectory,
            Debug = debug
        };
    }
}
