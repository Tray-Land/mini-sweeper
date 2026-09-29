using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiniSweeper.Services;
using Windows.ApplicationModel;

namespace MiniSweeper.Views;

/// <summary>Settings, shown inside the flyout in place of the game. Changes apply immediately.</summary>
public sealed partial class SettingsPage : Page
{
    private readonly Action _goBack;
    private bool _loading = true;

    public SettingsPage(Action goBack)
    {
        _goBack = goBack;
        InitializeComponent();
        VersionText.Text = $"{App.DisplayName} {GetVersion()}";
        _loading = false;
    }

    /// <summary>The page appeared: re-read state, which may have changed in Windows Settings.</summary>
    public void OnShown()
    {
        _loading = true;
        QuestionToggle.IsOn = SettingsService.QuestionMarks;
        ThemeCombo.SelectedIndex = SettingsService.ModernTheme ? 0 : 1;
        _loading = false;
        _ = LoadStartupStateAsync();
        BackButton.Focus(FocusState.Programmatic);
    }

    private static string GetVersion()
    {
        try
        {
            PackageVersion v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch
        {
            return "(unpackaged)";
        }
    }

    private async Task LoadStartupStateAsync() => ShowStartupState(await StartupService.GetStateAsync());

    private void ShowStartupState(StartupTaskState? state)
    {
        _loading = true;
        StartupToggle.IsOn = state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

        // The user (Task Manager, Settings > Apps > Startup) or policy has the final say; say
        // where to change it instead of offering a dead toggle.
        StartupToggle.IsEnabled = state is StartupTaskState.Enabled or StartupTaskState.Disabled;
        StartupDescription.Text = state switch
        {
            StartupTaskState.DisabledByUser => "Turned off in Settings > Apps > Startup. Turn it on there.",
            StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy => "Managed by your organization.",
            null => "Only available when the app is installed.",
            _ => "Keep the tray icon ready after you sign in.",
        };
        _loading = false;
    }

    private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            ShowStartupState(await StartupService.SetEnabledAsync(StartupToggle.IsOn));
        }
    }

    private void QuestionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            SettingsService.QuestionMarks = QuestionToggle.IsOn;
        }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && ThemeCombo.SelectedIndex >= 0)
        {
            SettingsService.ModernTheme = ThemeCombo.SelectedIndex == 0;
        }
    }

    private void ResetBest_Click(object sender, RoutedEventArgs e) => SettingsService.ClearBestTimes();

    private void BackButton_Click(object sender, RoutedEventArgs e) => _goBack();
}
