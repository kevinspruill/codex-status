using System.Windows;
using CodexStatus.Core;

namespace CodexStatus.Tray;

public partial class SettingsWindow : Window
{
    private readonly CodexStatusSettings _settings;
    private readonly TrayApplicationController _controller;

    internal SettingsWindow(CodexStatusSettings settings, TrayApplicationController controller)
    {
        _settings = settings;
        _controller = controller;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        ShowFloatingPillCheckBox.IsChecked = _settings.ShowFloatingPill;
        AlwaysOnTopCheckBox.IsChecked = _settings.AlwaysOnTop;
        PlaySoundOnDoneCheckBox.IsChecked = _settings.PlaySoundOnDone;
        PlaySoundOnApprovalCheckBox.IsChecked = _settings.PlaySoundOnApproval;
        HideCommandTextCheckBox.IsChecked = _settings.HideCommandText;
        RedactSecretsCheckBox.IsChecked = _settings.RedactSecrets;
        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        CodexHomePathTextBox.Text = _settings.CodexHomePath;
        StateDirectoryTextBox.Text = _settings.StateDirectory;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Close();
    }

    private void OnOpenStateFolder(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        _controller.OpenStateFolder();
        Close();
    }

    private void OnReinstallHooks(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        _controller.ReinstallHooks();
        Close();
    }

    private void SaveSettings()
    {
        _settings.ShowFloatingPill = ShowFloatingPillCheckBox.IsChecked == true;
        _settings.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true;
        _settings.PlaySoundOnDone = PlaySoundOnDoneCheckBox.IsChecked == true;
        _settings.PlaySoundOnApproval = PlaySoundOnApprovalCheckBox.IsChecked == true;
        _settings.HideCommandText = HideCommandTextCheckBox.IsChecked == true;
        _settings.RedactSecrets = RedactSecretsCheckBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _settings.CodexHomePath = CodexHomePathTextBox.Text.Trim();
        _settings.StateDirectory = StateDirectoryTextBox.Text.Trim();

        SettingsStore.Save(_settings);
        _controller.ApplySettings(_settings);
    }
}
