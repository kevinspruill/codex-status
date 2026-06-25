namespace CodexStatus.Core;

public static class StatusAggregator
{
    public static AggregatedStatus Aggregate(string stateDirectory, DateTimeOffset? now = null)
    {
        var sessionsDirectory = StateFileNames.GetSessionsDirectory(stateDirectory);
        var sessions = new List<AgentStatusSnapshot>();

        if (Directory.Exists(sessionsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(sessionsDirectory, "*.json"))
            {
                try
                {
                    var session = AtomicJsonFile.Read<AgentStatusSnapshot>(file);
                    if (session is not null)
                    {
                        sessions.Add(session);
                    }
                }
                catch
                {
                    // A partially copied or externally edited session should not break aggregation.
                }
            }
        }

        sessions = sessions
            .OrderByDescending(static session => session.UpdatedAt)
            .ToList();

        return new AggregatedStatus
        {
            SchemaVersion = SchemaVersions.Current,
            UpdatedAt = now ?? DateTimeOffset.UtcNow,
            Sessions = sessions,
            Primary = SelectPrimary(sessions),
            ActiveCount = sessions.Count(IsActive),
            WaitingApprovalCount = sessions.Count(static session => session.WaitingOnApproval || session.State == AgentDisplayState.WaitingForApproval),
            FailedCount = sessions.Count(static session => session.State == AgentDisplayState.Failed)
        };
    }

    public static AggregatedStatus WriteAggregate(string stateDirectory, DateTimeOffset? now = null)
    {
        var aggregate = Aggregate(stateDirectory, now);
        AtomicJsonFile.Write(StateFileNames.GetStatePath(stateDirectory), aggregate);
        return aggregate;
    }

    public static bool IsActive(AgentStatusSnapshot session) =>
        session.State is not AgentDisplayState.Idle
            and not AgentDisplayState.Done
            and not AgentDisplayState.Failed
            and not AgentDisplayState.Stale;

    private static AgentStatusSnapshot? SelectPrimary(IReadOnlyCollection<AgentStatusSnapshot> sessions)
    {
        return sessions
            .Where(static session => session.WaitingOnApproval || session.State == AgentDisplayState.WaitingForApproval)
            .OrderByDescending(static session => session.UpdatedAt)
            .FirstOrDefault()
            ?? sessions
                .Where(IsActive)
                .OrderByDescending(static session => session.UpdatedAt)
                .FirstOrDefault()
            ?? sessions
                .Where(static session => session.State is AgentDisplayState.Done or AgentDisplayState.Failed)
                .OrderByDescending(static session => session.UpdatedAt)
                .FirstOrDefault()
            ?? sessions
                .OrderByDescending(static session => session.UpdatedAt)
                .FirstOrDefault();
    }
}
