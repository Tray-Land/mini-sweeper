using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiniSweeper.Logic;
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
    public void OnShown(bool toCustomBoard = false)
    {
        _loading = true;
        QuestionToggle.IsOn = SettingsService.QuestionMarks;
        ThemeCombo.SelectedIndex = SettingsService.ModernTheme ? 0 : 1;
        LoadCustomBoxes();
        _loading = false;
        _ = LoadStartupStateAsync();

        if (toCustomBoard)
        {
            ColumnsBox.Focus(FocusState.Programmatic);
            // The page is not in the window yet; scroll once it has been laid out.
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                SettingsScroll.UpdateLayout();
                double top = CustomHeader.TransformToVisual((UIElement)SettingsScroll.Content).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
                SettingsScroll.ChangeView(null, top, null, disableAnimation: true);
            });
        }
        else
        {
            BackButton.Focus(FocusState.Programmatic);
        }
    }

    private void LoadCustomBoxes()
    {
        ColumnsBox.Minimum = Difficulty.MinColumns;
        ColumnsBox.Maximum = Difficulty.MaxColumns;
        RowsBox.Minimum = Difficulty.MinRows;
        RowsBox.Maximum = Difficulty.MaxRows;
        MinesBox.Minimum = 1;

        Difficulty board = GameSession.CustomDifficulty;
        ColumnsBox.Value = board.Columns;
        RowsBox.Value = board.Rows;
        UpdateMinesLimit();
        MinesBox.Value = board.Mines;
    }

    // The mine count can't exceed what the chosen size holds.
    private void UpdateMinesLimit()
    {
        int columns = double.IsNaN(ColumnsBox.Value) ? Difficulty.MinColumns : (int)ColumnsBox.Value;
        int rows = double.IsNaN(RowsBox.Value) ? Difficulty.MinRows : (int)RowsBox.Value;
        MinesBox.Maximum = Difficulty.MaxMines(columns, rows);
        if (!double.IsNaN(MinesBox.Value) && MinesBox.Value > MinesBox.Maximum)
        {
            MinesBox.Value = MinesBox.Maximum;
        }
    }

    private void CustomBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_loading)
        {
            UpdateMinesLimit();
        }
    }

    private void PlayCustom_Click(object sender, RoutedEventArgs e)
    {
        static int Read(NumberBox box, int fallback) => double.IsNaN(box.Value) ? fallback : (int)box.Value;

        GameSession.SetCustom(Read(ColumnsBox, Difficulty.MinColumns), Read(RowsBox, Difficulty.MinRows), Read(MinesBox, 1));
        _goBack();
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
