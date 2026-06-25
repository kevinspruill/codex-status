using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using CodexStatus.Core;
using Forms = System.Windows.Forms;

namespace CodexStatus.Tray;

internal sealed class TrayApplicationController : IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private CodexStatusSettings _settings = SettingsStore.Load();
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ToolStripMenuItem? _showPillMenuItem;
    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _pollTimer;
    private DispatcherTimer? _elapsedTimer;
    private FloatingPillWindow? _pillWindow;
    private SessionsFlyoutWindow? _flyoutWindow;
    private SettingsWindow? _settingsWindow;
    private AggregatedStatus _rawStatus = CreateIdleStatus();
    private AggregatedStatus _effectiveStatus = CreateIdleStatus();
    private Icon? _currentIcon;
    private AgentDisplayState? _previousPrimaryState;
    private bool _previousWaiting;
    private bool _hasRenderedStatus;

    public void Start()
    {
        Directory.CreateDirectory(_settings.StateDirectory);
        ConfigureNotifyIcon();
        ConfigureWatcher();
        ConfigureTimers();
        LoadState();

        if (_settings.ShowFloatingPill)
        {
            ShowPill();
        }
    }

    public void ApplySettings(CodexStatusSettings settings)
    {
        _settings = settings;
        Directory.CreateDirectory(_settings.StateDirectory);
        ConfigureWatcher();

        if (_settings.ShowFloatingPill)
        {
            ShowPill();
        }
        else
        {
            HidePill();
        }

        if (_pillWindow is not null)
        {
            _pillWindow.Topmost = _settings.AlwaysOnTop;
            _pillWindow.UpdateStatus(_effectiveStatus, _settings);
        }

        TrySetStartup();
        LoadState();
    }

    public void ReinstallHooks()
    {
        var hookPath = FindHookExecutable();
        if (hookPath is null)
        {
            System.Windows.MessageBox.Show(
                "CodexStatus.Hook.exe was not found. Build or publish CodexStatus.Hook, then try again.",
                "Codex Status",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var result = CodexHookInstaller.Install(_settings.CodexHomePath, hookPath);
            System.Windows.MessageBox.Show(
                $"Codex hooks updated.\n\nPath: {result.HooksPath}\nHandlers added: {result.AddedHandlerCount}\n\nCodex may require hook trust review before hooks run.",
                "Codex Status",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"Unable to install Codex hooks.\n\n{exception.Message}",
                "Codex Status",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenStateFolder() => OpenFolder(_settings.StateDirectory);

    private void ConfigureNotifyIcon()
    {
        _notifyIcon?.Dispose();
        _showPillMenuItem = new Forms.ToolStripMenuItem("Show floating pill")
        {
            Checked = _settings.ShowFloatingPill
        };
        _showPillMenuItem.Click += (_, _) => TogglePill();

        var menu = new Forms.ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            if (_showPillMenuItem is not null)
            {
                _showPillMenuItem.Checked = _settings.ShowFloatingPill;
                _showPillMenuItem.Text = _settings.ShowFloatingPill ? "Hide floating pill" : "Show floating pill";
            }
        };
        menu.Items.Add(_showPillMenuItem);
        menu.Items.Add("Open status folder", null, (_, _) => OpenFolder(_settings.StateDirectory));
        menu.Items.Add("Open Codex home folder", null, (_, _) => OpenFolder(_settings.CodexHomePath));
        menu.Items.Add("Reinstall Codex hooks", null, (_, _) => _dispatcher.Invoke(ReinstallHooks));
        menu.Items.Add("Settings", null, (_, _) => _dispatcher.Invoke(ShowSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => _dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown()));

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Visible = true,
            Text = "Codex Status"
        };
        _notifyIcon.DoubleClick += (_, _) => _dispatcher.Invoke(ShowFlyout);
    }

    private void ConfigureWatcher()
    {
        _watcher?.Dispose();
        Directory.CreateDirectory(_settings.StateDirectory);

        _watcher = new FileSystemWatcher(_settings.StateDirectory, StateFileNames.AggregatedStateFileName)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnStateFileChanged;
        _watcher.Changed += OnStateFileChanged;
        _watcher.Renamed += OnStateFileChanged;
    }

    private void ConfigureTimers()
    {
        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _pollTimer.Tick += (_, _) => LoadState();
        _pollTimer.Start();

        _elapsedTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _elapsedTimer.Tick += (_, _) => UpdateUi();
        _elapsedTimer.Start();
    }

    private void OnStateFileChanged(object sender, FileSystemEventArgs e) =>
        _dispatcher.BeginInvoke(LoadState);

    private void LoadState()
    {
        try
        {
            _rawStatus = AtomicJsonFile.Read<AggregatedStatus>(StateFileNames.GetStatePath(_settings.StateDirectory))
                ?? CreateIdleStatus();
        }
        catch
        {
            return;
        }

        _effectiveStatus = BuildEffectiveStatus(_rawStatus);
        UpdateUi();
    }

    private AggregatedStatus BuildEffectiveStatus(AggregatedStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var copy = new AggregatedStatus
        {
            SchemaVersion = status.SchemaVersion,
            UpdatedAt = status.UpdatedAt,
            Sessions = status.Sessions,
            ActiveCount = status.ActiveCount,
            WaitingApprovalCount = status.WaitingApprovalCount,
            FailedCount = status.FailedCount,
            Primary = status.Primary?.Clone()
        };

        if (copy.Primary is null)
        {
            copy.Primary = CreateIdleSnapshot(now);
            return copy;
        }

        if (now - status.UpdatedAt > TimeSpan.FromMinutes(Math.Max(1, _settings.StaleAfterMinutes)))
        {
            copy.Primary.State = AgentDisplayState.Stale;
            copy.Primary.Label = "Codex: Stale";
            copy.Primary.Detail = null;
            return copy;
        }

        if (copy.Primary.State == AgentDisplayState.Done
            && copy.Primary.CompletedAt is { } completedAt
            && now - completedAt > TimeSpan.FromSeconds(Math.Max(1, _settings.DoneVisibleSeconds)))
        {
            copy.Primary = CreateIdleSnapshot(now);
        }

        return copy;
    }

    private void UpdateUi()
    {
        var primary = _effectiveStatus.Primary ?? CreateIdleSnapshot(DateTimeOffset.UtcNow);
        PlayTransitionSounds(primary);

        if (_notifyIcon is not null)
        {
            var oldIcon = _currentIcon;
            _currentIcon = IconFactory.Create(primary.State);
            _notifyIcon.Icon = _currentIcon;
            _notifyIcon.Text = TruncateForTooltip(BuildTooltip(_effectiveStatus));
            oldIcon?.Dispose();
        }

        _pillWindow?.UpdateStatus(_effectiveStatus, _settings);
    }

    private void PlayTransitionSounds(AgentStatusSnapshot primary)
    {
        var waiting = primary.State == AgentDisplayState.WaitingForApproval || primary.WaitingOnApproval;
        if (_hasRenderedStatus)
        {
            if (_settings.PlaySoundOnApproval && !_previousWaiting && waiting)
            {
                SystemSounds.Exclamation.Play();
            }

            if (_settings.PlaySoundOnDone
                && _previousPrimaryState is not null
                && IsActiveState(_previousPrimaryState.Value)
                && primary.State == AgentDisplayState.Done)
            {
                SystemSounds.Asterisk.Play();
            }
        }

        _previousWaiting = waiting;
        _previousPrimaryState = primary.State;
        _hasRenderedStatus = true;
    }

    private void ShowPill()
    {
        if (_pillWindow is null)
        {
            _pillWindow = new FloatingPillWindow(_settings)
            {
                Topmost = _settings.AlwaysOnTop
            };
            _pillWindow.FlyoutRequested += (_, _) => ShowFlyout();
            _pillWindow.Closed += (_, _) => _pillWindow = null;
        }

        _pillWindow.UpdateStatus(_effectiveStatus, _settings);
        _pillWindow.Show();
        _pillWindow.PositionIfNeeded();
    }

    private void HidePill() => _pillWindow?.Hide();

    private void TogglePill()
    {
        _settings.ShowFloatingPill = !_settings.ShowFloatingPill;
        SettingsStore.Save(_settings);
        if (_settings.ShowFloatingPill)
        {
            ShowPill();
        }
        else
        {
            HidePill();
        }
    }

    private void ShowFlyout()
    {
        _flyoutWindow?.Close();
        _flyoutWindow = new SessionsFlyoutWindow(_effectiveStatus, _settings);
        _flyoutWindow.Closed += (_, _) => _flyoutWindow = null;
        _flyoutWindow.Show();

        if (_pillWindow is not null && _pillWindow.IsVisible)
        {
            _flyoutWindow.UpdateLayout();
            var workArea = SystemParameters.WorkArea;
            var width = _flyoutWindow.ActualWidth > 0 ? _flyoutWindow.ActualWidth : 360;
            var height = _flyoutWindow.ActualHeight > 0 ? _flyoutWindow.ActualHeight : 260;
            _flyoutWindow.Left = Math.Min(Math.Max(workArea.Left, _pillWindow.Left + _pillWindow.ActualWidth - width), workArea.Right - width);
            _flyoutWindow.Top = Math.Min(Math.Max(workArea.Top, _pillWindow.Top - 12 - height), workArea.Bottom - height);
        }

        _flyoutWindow.Activate();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, this);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void TrySetStartup()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                StartupRegistry.SetEnabled(_settings.StartWithWindows, executablePath);
            }
        }
        catch
        {
            System.Windows.MessageBox.Show(
                "Start with Windows could not be updated. The rest of your settings were saved.",
                "Codex Status",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static string? FindHookExecutable()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var configuration = GetBuildConfiguration(baseDirectory);
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "CodexStatus.Hook.exe"),
            Path.Combine(baseDirectory, "hook", "CodexStatus.Hook.exe"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "CodexStatus.Hook", "bin", configuration, "net10.0", "CodexStatus.Hook.exe")),
            Path.GetFullPath(Path.Combine(baseDirectory.Replace("CodexStatus.Tray", "CodexStatus.Hook"), "CodexStatus.Hook.exe"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string GetBuildConfiguration(string baseDirectory) =>
        baseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";

    private static AggregatedStatus CreateIdleStatus()
    {
        var now = DateTimeOffset.UtcNow;
        return new AggregatedStatus
        {
            UpdatedAt = now,
            Primary = CreateIdleSnapshot(now)
        };
    }

    private static AgentStatusSnapshot CreateIdleSnapshot(DateTimeOffset now) => new()
    {
        State = AgentDisplayState.Idle,
        Label = "Codex: Idle",
        StartedAt = now,
        UpdatedAt = now
    };

    private static string BuildTooltip(AggregatedStatus status)
    {
        var primary = status.Primary ?? CreateIdleSnapshot(DateTimeOffset.UtcNow);
        var lines = new List<string> { primary.Label };
        var location = primary.RepoName ?? primary.Cwd;
        if (!string.IsNullOrWhiteSpace(location))
        {
            lines.Add(location);
        }

        lines.Add(FormatElapsed(primary, showPrefix: true));
        if (status.Sessions.Count > 1)
        {
            lines.Add($"{status.ActiveCount} active, {status.WaitingApprovalCount} waiting approval");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatElapsed(AgentStatusSnapshot snapshot, bool showPrefix)
    {
        var elapsed = DateTimeOffset.UtcNow - snapshot.StartedAt;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var formatted = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        return showPrefix ? $"Elapsed {formatted}" : formatted;
    }

    private static string TruncateForTooltip(string value) =>
        value.Length <= 63 ? value : value[..60] + "...";

    private static bool IsActiveState(AgentDisplayState state) =>
        state is not AgentDisplayState.Idle
            and not AgentDisplayState.Done
            and not AgentDisplayState.Failed
            and not AgentDisplayState.Stale
            and not AgentDisplayState.Unknown;

    public void Dispose()
    {
        _pollTimer?.Stop();
        _elapsedTimer?.Stop();
        _watcher?.Dispose();
        _pillWindow?.Close();
        _flyoutWindow?.Close();
        _settingsWindow?.Close();
        _notifyIcon?.Dispose();
        _currentIcon?.Dispose();
    }
}
