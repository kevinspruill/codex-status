using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CodexStatus.Core;

namespace CodexStatus.Tray;

public partial class SessionsFlyoutWindow : Window
{
    private bool _closeRequested;

    public SessionsFlyoutWindow(AggregatedStatus status, CodexStatusSettings settings)
    {
        InitializeComponent();
        SessionsList.ItemsSource = status.Sessions
            .OrderByDescending(static session => session.State == AgentDisplayState.WaitingForApproval || session.WaitingOnApproval)
            .ThenByDescending(static session => session.UpdatedAt)
            .Select(session => new SessionRow(
                session.Label,
                BuildMeta(session, settings),
                session.Detail ?? session.Model ?? string.Empty,
                BuildPayload(session)))
            .ToList();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RequestClose();
        }
    }

    private void OnDeactivated(object? sender, EventArgs e) => RequestClose();

    private void RequestClose()
    {
        if (_closeRequested)
        {
            return;
        }

        _closeRequested = true;
        Deactivated -= OnDeactivated;
        Dispatcher.BeginInvoke(() =>
        {
            if (IsVisible)
            {
                Close();
            }
        }, DispatcherPriority.Background);
    }

    private static string BuildMeta(AgentStatusSnapshot session, CodexStatusSettings settings)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(session.RepoName))
        {
            parts.Add(session.RepoName);
        }

        if (settings.ShowElapsedTime)
        {
            parts.Add(FormatElapsed(session));
        }

        if (!string.IsNullOrWhiteSpace(session.Model))
        {
            parts.Add(session.Model);
        }

        return string.Join(" | ", parts);
    }

    private static string FormatElapsed(AgentStatusSnapshot snapshot)
    {
        var elapsed = DateTimeOffset.UtcNow - snapshot.StartedAt;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private static string BuildPayload(AgentStatusSnapshot session)
    {
        if (session.PayloadFields is null || session.PayloadFields.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            session.PayloadFields.Select(static field => $"{field.Key}: {field.Value}"));
    }

    public sealed record SessionRow(string Label, string Meta, string Detail, string Payload);
}
