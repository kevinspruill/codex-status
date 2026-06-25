using System.Windows;
using System.Windows.Input;
using CodexStatus.Core;
using Media = System.Windows.Media;

namespace CodexStatus.Tray;

public partial class FloatingPillWindow : Window
{
    private readonly CodexStatusSettings _settings;
    private System.Windows.Point _mouseDownPoint;
    private bool _dragged;

    public event EventHandler? FlyoutRequested;

    public FloatingPillWindow(CodexStatusSettings settings)
    {
        _settings = settings;
        InitializeComponent();
    }

    public void UpdateStatus(AggregatedStatus status, CodexStatusSettings settings)
    {
        Topmost = settings.AlwaysOnTop;
        var primary = status.Primary ?? new AgentStatusSnapshot
        {
            State = AgentDisplayState.Idle,
            Label = "Codex: Idle"
        };

        StatusDot.Fill = new Media.SolidColorBrush(GetStateColor(primary.State));
        StatusText.Text = BuildPillText(status, primary, settings);
        PositionIfNeeded();
    }

    public void PositionIfNeeded()
    {
        UpdateLayout();
        var workArea = SystemParameters.WorkArea;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;

        if (_settings.PillLeft is { } savedLeft && _settings.PillTop is { } savedTop)
        {
            Left = Clamp(savedLeft, workArea.Left, workArea.Right - width);
            Top = Clamp(savedTop, workArea.Top, workArea.Bottom - height);
            return;
        }

        Left = workArea.Right - width - 16;
        Top = workArea.Bottom - height - 16;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => PositionIfNeeded();

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownPoint = e.GetPosition(this);
        _dragged = false;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_settings.LockPillPosition || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _mouseDownPoint.X) < 4 && Math.Abs(current.Y - _mouseDownPoint.Y) < 4)
        {
            return;
        }

        _dragged = true;
        try
        {
            DragMove();
            KeepInsideWorkArea();
            _settings.PillLeft = Left;
            _settings.PillTop = Top;
            SettingsStore.Save(_settings);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragged)
        {
            FlyoutRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void KeepInsideWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Clamp(Left, workArea.Left, workArea.Right - ActualWidth);
        Top = Clamp(Top, workArea.Top, workArea.Bottom - ActualHeight);
    }

    private static string BuildPillText(AggregatedStatus status, AgentStatusSnapshot primary, CodexStatusSettings settings)
    {
        if (status.Sessions.Count > 1 && (status.ActiveCount > 1 || status.WaitingApprovalCount > 0))
        {
            return $"Codex: {status.ActiveCount} active | {status.WaitingApprovalCount} waiting approval";
        }

        var parts = new List<string> { primary.Label };
        var detail = primary.Detail;
        if (!string.IsNullOrWhiteSpace(detail)
            && !(settings.HideCommandText && primary.State == AgentDisplayState.RunningCommand))
        {
            parts.Add(detail);
        }

        if (settings.ShowElapsedTime)
        {
            parts.Add(FormatElapsed(primary));
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

    private static Media.Color GetStateColor(AgentDisplayState state) => state switch
    {
        AgentDisplayState.WaitingForApproval => Media.Color.FromRgb(245, 158, 11),
        AgentDisplayState.Done => Media.Color.FromRgb(34, 197, 94),
        AgentDisplayState.Failed => Media.Color.FromRgb(239, 68, 68),
        AgentDisplayState.Stale => Media.Color.FromRgb(148, 163, 184),
        AgentDisplayState.Idle or AgentDisplayState.Unknown => Media.Color.FromRgb(156, 163, 175),
        _ => Media.Color.FromRgb(59, 130, 246)
    };

    private static double Clamp(double value, double min, double max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }
}
