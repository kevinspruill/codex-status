using System.Text.Json;
using CodexStatus.Core;

namespace CodexStatus.Tests;

public sealed class CoreUtilityTests
{
    [Fact]
    public void SecretRedactorRedactsObviousSecrets()
    {
        var input = "OPENAI_API_KEY=sk-proj-abcdefghijklmnopqrstuvwxyz --password hunter2 token=abc123 Authorization: Bearer abcdefghijklmnopqrstuvwxyz AKIA1234567890ABCDEF";
        var redacted = SecretRedactor.Redact(input);

        Assert.DoesNotContain("sk-proj-abcdefghijklmnopqrstuvwxyz", redacted);
        Assert.DoesNotContain("hunter2", redacted);
        Assert.DoesNotContain("abc123", redacted);
        Assert.DoesNotContain("AKIA1234567890ABCDEF", redacted);
        Assert.Contains(SecretRedactor.Redacted, redacted);
    }

    [Fact]
    public void AggregatorPrioritizesWaitingThenRunningThenDone()
    {
        using var temp = new TempDirectory();
        var now = DateTimeOffset.Parse("2026-06-25T12:00:00Z");

        WriteSession(temp.Path, new AgentStatusSnapshot
        {
            SessionId = "done",
            State = AgentDisplayState.Done,
            Label = "Codex: Done",
            UpdatedAt = now.AddMinutes(3),
            CompletedAt = now.AddMinutes(3)
        });
        WriteSession(temp.Path, new AgentStatusSnapshot
        {
            SessionId = "running",
            State = AgentDisplayState.RunningCommand,
            Label = "Codex: Running command",
            UpdatedAt = now.AddMinutes(2)
        });
        WriteSession(temp.Path, new AgentStatusSnapshot
        {
            SessionId = "approval",
            State = AgentDisplayState.WaitingForApproval,
            Label = "Codex: Waiting for approval",
            WaitingOnApproval = true,
            UpdatedAt = now.AddMinutes(1)
        });

        var aggregate = StatusAggregator.Aggregate(temp.Path, now);

        Assert.Equal("approval", aggregate.Primary?.SessionId);
        Assert.Equal(2, aggregate.ActiveCount);
        Assert.Equal(1, aggregate.WaitingApprovalCount);
    }

    [Fact]
    public void AtomicJsonFileWritesReadableJson()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "state.json");
        var status = new AggregatedStatus
        {
            Primary = new AgentStatusSnapshot
            {
                SessionId = "s1",
                State = AgentDisplayState.Thinking,
                Label = "Codex: Thinking"
            }
        };

        AtomicJsonFile.Write(path, status);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Thinking", document.RootElement.GetProperty("primary").GetProperty("state").GetString());
    }

    [Fact]
    public void HookInstallerIsIdempotentAndDoesNotDuplicateHandlers()
    {
        using var temp = new TempDirectory();
        var hookExecutable = Path.Combine(temp.Path, "CodexStatus.Hook.exe");
        File.WriteAllText(hookExecutable, string.Empty);

        var first = CodexHookInstaller.Install(temp.Path, hookExecutable, DateTimeOffset.Parse("2026-06-25T12:00:00Z"));
        var second = CodexHookInstaller.Install(temp.Path, hookExecutable, DateTimeOffset.Parse("2026-06-25T12:01:00Z"));

        Assert.True(first.Modified);
        Assert.Equal(10, first.AddedHandlerCount);
        Assert.False(second.Modified);
        Assert.Equal(0, second.AddedHandlerCount);

        var json = File.ReadAllText(Path.Combine(temp.Path, "hooks.json"));
        Assert.Equal(20, CountOccurrences(json, "CodexStatus.Hook.exe"));
        Assert.DoesNotContain("codex-status-hook", json);
    }

    [Fact]
    public void HookInstallerUpgradesLegacyCommandAlias()
    {
        using var temp = new TempDirectory();
        var hookExecutable = Path.Combine(temp.Path, "CodexStatus.Hook.exe");
        File.WriteAllText(hookExecutable, string.Empty);
        File.WriteAllText(
            Path.Combine(temp.Path, "hooks.json"),
            """
            {
              "hooks": {
                "UserPromptSubmit": [
                  {
                    "hooks": [
                      {
                        "type": "command",
                        "command": "codex-status-hook --event UserPromptSubmit",
                        "commandWindows": "C:\\old\\CodexStatus.Hook.exe --event UserPromptSubmit",
                        "timeout": 5,
                        "statusMessage": "Updating Codex Status"
                      }
                    ]
                  }
                ]
              }
            }
            """);

        var result = CodexHookInstaller.Install(temp.Path, hookExecutable, DateTimeOffset.Parse("2026-06-25T12:00:00Z"));
        var json = File.ReadAllText(Path.Combine(temp.Path, "hooks.json"));

        Assert.True(result.Modified);
        Assert.Equal(9, result.AddedHandlerCount);
        Assert.DoesNotContain("codex-status-hook", json);
        Assert.Contains("\"command\": \"", json);
        Assert.Equal(20, CountOccurrences(json, "CodexStatus.Hook.exe"));
    }

    private static void WriteSession(string stateDirectory, AgentStatusSnapshot snapshot) =>
        AtomicJsonFile.Write(StateFileNames.GetSessionPath(stateDirectory, snapshot.SessionId), snapshot);

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
