using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MiniSweeper.Controls;
using MiniSweeper.Logic;
using MiniSweeper.Services;
using Windows.Foundation;
using Windows.UI;

namespace MiniSweeper.Views;

/// <summary>
/// The flyout's content: a Windows 95 Minesweeper window. The game itself lives in
/// <see cref="GameSession"/>; this page draws it, feeds it input, and plays the animations.
/// Nothing runs while the flyout is hidden: the clock and effects timers stop in <see cref="OnHidden"/>.
/// </summary>
public sealed partial class FlyoutPage : Page, IDisposable
{
    private enum Press { None, Reveal, Chord }

    // Matches the page's Padding in XAML (12 each side) and the window's floor size.
    private const double PagePadding = 12;
    private const double MinWindowWidth = 340;
    private const double MinWindowHeight = 420;

    /// <summary>The board changed size (new difficulty), so the window needs to follow.</summary>
    public event Action? BoardResized;

    private readonly DispatcherQueueTimer _clockTimer;
    private readonly DispatcherQueueTimer _fxTimer;
    private readonly PriorityQueue<Action, long> _fxQueue = new();
    private readonly Stopwatch _fxClock = Stopwatch.StartNew();
    private readonly List<int> _pressedCells = [];

    private MinesweeperGame _game = GameSession.Game;
    private CellView[] _cells = [];
    private bool[] _pending = [];
    private Canvas _board = null!;
    private Canvas _fx = null!;
    private BevelBorder _panel = null!;
    private FaceView _face = null!;
    private Button _faceButton = null!;
    private LedDisplay _mines = null!;
    private LedDisplay _time = null!;
    private double _cellSize;
    private int? _fitCell;

    private Press _press;
    private bool _animate;
    private bool _introPending;
    private string? _resultText;

    public FlyoutPage()
    {
        InitializeComponent();
        TitleText.Text = App.DisplayName;

        _clockTimer = DispatcherQueue.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromMilliseconds(100);
        _clockTimer.Tick += (_, _) => UpdateClock();

        _fxTimer = DispatcherQueue.CreateTimer();
        _fxTimer.Interval = TimeSpan.FromMilliseconds(16);
        _fxTimer.Tick += (_, _) => RunDueEffects();

#if DEBUG
        // F9 clears the board, to check the win celebration without playing a whole game.
        KeyboardAccelerator cheat = new() { Key = Windows.System.VirtualKey.F9 };
        cheat.Invoked += (_, e) =>
        {
            e.Handled = true;
            if (_game.State == GameState.Ready)
            {
                Apply(_game.Reveal(_game.Count / 2));
            }

            for (int i = 0; i < _game.Count; i++)
            {
                if (!_game.IsMine(i))
                {
                    Apply(_game.Reveal(i));
                }
            }
        };
        KeyboardAccelerators.Add(cheat);
#endif

        Skin.Apply(SettingsService.ModernTheme, SystemThemeService.IsLight);
        BuildDifficultyMenu();
        BuildPanel();
        _introPending = _game.State == GameState.Ready;
        UpdateAll();
    }

    /// <summary>The flyout opened: the clock picks up where it paused.</summary>
    public void OnShown()
    {
        _animate = true;
        if (!ReferenceEquals(_game, GameSession.Game))
        {
            // Settings started a custom game while the page was away.
            BuildDifficultyMenu();
            Reload();
        }

        RefreshSkin();
        _game.Resume();
        UpdateHud();
        UpdateBest();
        StartClockIfPlaying();

        if (_introPending)
        {
            _introPending = false;
            PlayIntro();
        }
    }

    /// <summary>The flyout hid: pause the clock, finish pending effects instantly, and stop every timer.</summary>
    public void OnHidden()
    {
        _animate = false;
        _clockTimer.Stop();
        CancelPress();
        FlushEffects();
        _game.Pause();
    }

    public void Dispose()
    {
        OnHidden();
        _fxTimer.Stop();
    }

    public void NewGame()
    {
        GameSession.NewGame();
        Reload();
    }

    // ---- Building ------------------------------------------------------------------------

    private void Reload()
    {
        _game = GameSession.Game;
        _resultText = null;
        _fitCell = null;
        BuildPanel();
        UpdateAll();
        BoardResized?.Invoke();
        PlayIntro();
    }

    /// <summary>
    /// Sizes the board for the screen and returns the logical window size that holds it: cells keep
    /// their preferred size unless <paramref name="available"/> is too small, then shrink to fit.
    /// </summary>
    public Size FitToScreen(double availableWidth, double availableHeight)
    {
        Difficulty d = _game.Difficulty;
        _panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double chromeW = _panel.DesiredSize.Width - (d.Columns * _cellSize) + (2 * PagePadding);
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double chromeH = DesiredSize.Height - (d.Rows * _cellSize);

        int fit = (int)Math.Min(
            Math.Min(d.CellSize, (availableWidth - chromeW) / d.Columns),
            (availableHeight - chromeH) / d.Rows);
        fit = Math.Max(Difficulty.MinCellSize, fit);
        if (fit != _cellSize)
        {
            _fitCell = fit;
            BuildPanel();
            UpdateAll();
        }

        double width = Math.Max(MinWindowWidth, (d.Columns * _cellSize) + chromeW);
        Measure(new Size(width, double.PositiveInfinity));
        double height = Math.Max(MinWindowHeight, DesiredSize.Height);
        return new Size(Math.Min(width, availableWidth), Math.Min(height, availableHeight));
    }

    /// <summary>Redraws the board when the skin setting or the taskbar's light/dark mode changed since it was built.</summary>
    private void RefreshSkin()
    {
        bool modern = SettingsService.ModernTheme;
        bool light = SystemThemeService.IsLight;
        if (Skin.Key == (modern, modern && light))
        {
            return;
        }

        Skin.Apply(modern, light);
        BuildPanel();
        UpdateAll();
        _introPending = true;
    }

    private void BuildDifficultyMenu()
    {
        DifficultyMenu.Items.Clear();
        DifficultyButton.Content = GameSession.Difficulty.Name;

        // The player's custom board sits in the list once they have set one up.
        IEnumerable<Difficulty> boards = SettingsService.HasCustomBoard
            ? [.. Difficulty.All, GameSession.CustomDifficulty]
            : Difficulty.All;
        foreach (Difficulty d in boards)
        {
            RadioMenuFlyoutItem item = new()
            {
                Text = $"{d.Name}  ({d.Columns}×{d.Rows}, {d.Mines} mines)",
                GroupName = "Difficulty",
                IsChecked = d == GameSession.Difficulty,
            };
            item.Click += (_, _) =>
            {
                GameSession.SetDifficulty(d);
                DifficultyButton.Content = d.Name;
                Reload();
            };
            DifficultyMenu.Items.Add(item);
        }

        DifficultyMenu.Items.Add(new MenuFlyoutSeparator());
        MenuFlyoutItem custom = new() { Text = "+ Custom…", Icon = new FontIcon { Glyph = "" } };
        custom.Click += (_, _) => App.Current.ShowSettings(toCustomBoard: true);
        DifficultyMenu.Items.Add(custom);
    }

    private void BuildPanel()
    {
        _fxQueue.Clear();
        _fxTimer.Stop();
        _press = Press.None;
        _pressedCells.Clear();

        Difficulty d = _game.Difficulty;
        _cellSize = _fitCell ?? d.CellSize;
        _cells = new CellView[_game.Count];
        _pending = new bool[_game.Count];

        _board = new Canvas
        {
            Width = d.Columns * _cellSize,
            Height = d.Rows * _cellSize,
            Background = new SolidColorBrush(Colors.Transparent),
        };
        AutomationProperties.SetName(_board, $"Minefield, {d.Columns} by {d.Rows}, {d.Mines} mines");
        for (int i = 0; i < _cells.Length; i++)
        {
            CellView cell = new(_cellSize);
            Canvas.SetLeft(cell, i % d.Columns * _cellSize);
            Canvas.SetTop(cell, i / d.Columns * _cellSize);
            _cells[i] = cell;
            _board.Children.Add(cell);
            SyncCell(i);
        }

        _board.PointerPressed += Board_PointerPressed;
        _board.PointerMoved += Board_PointerMoved;
        _board.PointerReleased += Board_PointerReleased;
        _board.PointerCanceled += (_, _) => CancelPress();
        _board.PointerCaptureLost += (_, _) => CancelPress();

        _mines = new LedDisplay("🚩");
        _time = new LedDisplay("⏱️");
        _face = new FaceView();
        _faceButton = new Button
        {
            Content = _face,
            Width = 32,
            Height = 32,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Background = new SolidColorBrush(Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(_faceButton, "New game");
        ToolTipService.SetToolTip(_faceButton, "New game (F2)");
        _faceButton.Click += (_, _) => NewGame();
        _faceButton.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => _face.SetPressed(true)), true);
        _faceButton.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => _face.SetPressed(false)), true);
        _faceButton.PointerExited += (_, _) => _face.SetPressed(false);
        _faceButton.PointerCaptureLost += (_, _) => _face.SetPressed(false);

        Grid hudGrid = new() { Margin = new Thickness(3) };
        hudGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hudGrid.ColumnDefinitions.Add(new ColumnDefinition());
        hudGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _mines.VerticalAlignment = VerticalAlignment.Center;
        _time.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_faceButton, 1);
        Grid.SetColumn(_time, 2);
        hudGrid.Children.Add(_mines);
        hudGrid.Children.Add(_faceButton);
        hudGrid.Children.Add(_time);
        AutomationProperties.SetName(_mines, "Mines left");
        AutomationProperties.SetName(_time, "Seconds");

        BevelBorder hud = new(2, raised: false, radius: 10) { Width = _board.Width + 6, Height = 44, Content = hudGrid };
        BevelBorder boardFrame = new(3, raised: false, radius: 8) { ContentPadding = 3, Content = _board };
        StackPanel stack = new() { Spacing = 6, Margin = new Thickness(6) };
        stack.Children.Add(hud);
        stack.Children.Add(boardFrame);

        _panel = new BevelBorder(3, radius: 12) { Content = stack };
        _fx = new Canvas { IsHitTestVisible = false };
        Grid root = new();
        root.Children.Add(_panel);
        root.Children.Add(_fx);
        PanelHost.Child = root;
    }

    private void PlayIntro()
    {
        if (!_animate)
        {
            return;
        }

        int cols = _game.Columns;
        for (int i = 0; i < _cells.Length; i++)
        {
            Fx.Intro(_cells[i], _cellSize, ((i % cols) + (i / cols)) * 14);
        }

        Fx.Pop(_faceButton, 32, 32, 0.6f, 1.2f, 260);
    }

    // ---- Rendering -----------------------------------------------------------------------

    /// <summary>Draws one cell from the model's state (except cells whose reveal animation hasn't fired yet).</summary>
    private void SyncCell(int i)
    {
        CellView cell = _cells[i];
        if (_game.IsRevealed(i) && !_pending[i])
        {
            int n = _game.Adjacent(i);
            cell.Show(n == 0 ? CellLook.Empty : CellLook.Number, n);
            return;
        }

        CellMark mark = _game.MarkAt(i);
        bool mine = _game.IsMine(i);
        if (_game.State == GameState.Lost)
        {
            if (i == _game.ExplodedIndex)
            {
                cell.Show(CellLook.Exploded);
                return;
            }

            if (mine && mark != CellMark.Flag)
            {
                cell.Show(CellLook.Mine);
                return;
            }

            if (!mine && mark == CellMark.Flag)
            {
                cell.Show(CellLook.WrongFlag);
                return;
            }
        }

        cell.Show(mark switch
        {
            CellMark.Flag => CellLook.Flag,
            CellMark.Question => CellLook.Question,
            _ => CellLook.Hidden,
        });
    }

    private void UpdateAll()
    {
        UpdateHud();
        UpdateStatus();
        UpdateBest();
        UpdateTray();
        _face.Set(_game.State switch { GameState.Won => Face.Cool, GameState.Lost => Face.Dead, _ => Face.Smile });
    }

    private void UpdateHud()
    {
        _mines.Value = _game.MinesLeft;
        _time.Value = (int)_game.Elapsed.TotalSeconds;
    }

    private void UpdateClock()
    {
        if (_game.State == GameState.Playing)
        {
            _time.Value = (int)_game.Elapsed.TotalSeconds;
        }
    }

    private void StartClockIfPlaying()
    {
        if (_game.State == GameState.Playing && _animate)
        {
            _clockTimer.Start();
        }
        else
        {
            _clockTimer.Stop();
        }
    }

    private void UpdateStatus()
    {
        StatusText.Text = _resultText ?? _game.State switch
        {
            GameState.Ready => "Click any square to begin. Right-click plants a flag.",
            GameState.Playing => "Click a number (or middle-click) to clear around it.",
            GameState.Lost => "Boom! Press the face or F2 to try again.",
            _ => string.Empty,
        };
    }

    private void UpdateBest()
    {
        static string Format(int seconds) =>
            seconds == 0 ? "–" : seconds < 60 ? $"{seconds} s" : $"{seconds / 60}:{seconds % 60:00} min";

        BestText.Text = "Best times\n" + string.Join("\n",
            Difficulty.All.Append(_game.Difficulty).Distinct().Select(d =>
                $"{(d.IsCustom ? $"Custom {d.Columns}×{d.Rows}, {d.Mines}" : d.Name)}: {Format(GameSession.BestSeconds(d))}"));
    }

    private void UpdateTray()
    {
        App.Current.SetTrayStatus(_game.State switch
        {
            GameState.Playing => $"{_game.Difficulty.Name}: {_game.MinesLeft} mines left",
            GameState.Won => $"{_game.Difficulty.Name}: cleared in {(int)_game.Elapsed.TotalSeconds} s",
            GameState.Lost => $"{_game.Difficulty.Name}: boom",
            _ => null,
        });
    }

    // ---- Input ---------------------------------------------------------------------------

    private int IndexAt(Point p)
    {
        int c = (int)Math.Floor(p.X / _cellSize);
        int r = (int)Math.Floor(p.Y / _cellSize);
        return c < 0 || r < 0 || c >= _game.Columns || r >= _game.Rows ? -1 : (r * _game.Columns) + c;
    }

    private void Board_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_game.State is GameState.Won or GameState.Lost)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(_board);
        PointerPointProperties props = point.Properties;
        int index = IndexAt(point.Position);
        if (index < 0)
        {
            return;
        }

        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && props.IsRightButtonPressed))
        {
            _press = Press.Chord;
        }
        else if (props.IsLeftButtonPressed)
        {
            _press = Press.Reveal;
        }
        else if (props.IsRightButtonPressed)
        {
            ToggleMark(index);
            return;
        }
        else
        {
            return;
        }

        _board.CapturePointer(e.Pointer);
        _face.Set(Face.Oh);
        ShowPressed(index);
    }

    private void Board_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_press != Press.None)
        {
            ShowPressed(IndexAt(e.GetCurrentPoint(_board).Position));
        }
    }

    private void Board_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(_board);
        PointerPointProperties props = point.Properties;
        if (_press == Press.None || props.IsLeftButtonPressed || props.IsRightButtonPressed || props.IsMiddleButtonPressed)
        {
            return;
        }

        Press mode = _press;
        int index = IndexAt(point.Position);
        _board.ReleasePointerCaptures();
        CancelPress();
        if (index < 0)
        {
            return;
        }

        if (mode == Press.Chord || _game.IsRevealed(index))
        {
            Apply(_game.Chord(index));
        }
        else
        {
            Apply(_game.Reveal(index));
        }
    }

    /// <summary>Sinks the cell under the pointer (or, when chording, the unopened squares around it).</summary>
    private void ShowPressed(int index)
    {
        ClearPressed();
        if (index < 0)
        {
            return;
        }

        IEnumerable<int> targets = _press == Press.Chord ? _game.Neighbors(index).Append(index) : [index];
        foreach (int t in targets)
        {
            if (!_game.IsRevealed(t) && _game.MarkAt(t) != CellMark.Flag && !_pending[t])
            {
                _cells[t].Show(CellLook.Pressed);
                _pressedCells.Add(t);
            }
        }
    }

    private void ClearPressed()
    {
        foreach (int i in _pressedCells)
        {
            SyncCell(i);
        }

        _pressedCells.Clear();
    }

    private void CancelPress()
    {
        if (_press == Press.None)
        {
            return;
        }

        _press = Press.None;
        ClearPressed();
        if (_game.State is GameState.Ready or GameState.Playing)
        {
            _face.Set(Face.Smile);
        }
    }

    private void ToggleMark(int index)
    {
        if (!_game.ToggleMark(index, SettingsService.QuestionMarks))
        {
            return;
        }

        SyncCell(index);
        if (_animate)
        {
            Fx.Pop(_cells[index], _cellSize, _cellSize, 0.7f, 1.2f, 170);
        }

        UpdateHud();
        UpdateTray();
    }

    // ---- Game flow and effects -----------------------------------------------------------

    /// <summary>
    /// Plays a reveal. Cells open in a ripple: the depth of the flood fill sets each cell's delay,
    /// so a big opening spreads outwards instead of appearing at once.
    /// </summary>
    private void Apply(IReadOnlyList<Opened> opened)
    {
        int maxDepth = opened.Count == 0 ? 0 : opened.Max(o => o.Depth);
        int step = maxDepth == 0 ? 0 : Math.Clamp(700 / maxDepth, 8, 30);

        foreach (Opened o in opened)
        {
            if (o.Depth == 0)
            {
                RevealCell(o.Index);
                continue;
            }

            int index = o.Index;
            _pending[index] = true;
            Schedule(() =>
            {
                _pending[index] = false;
                RevealCell(index);
            }, o.Depth * step);
        }

        if (_game.State == GameState.Lost)
        {
            Schedule(Explode, maxDepth * step);
        }
        else if (_game.State == GameState.Won)
        {
            Schedule(Celebrate, (maxDepth * step) + 120);
        }

        StartClockIfPlaying();
        UpdateHud();
        UpdateStatus();
        UpdateTray();
    }

    private void RevealCell(int index)
    {
        SyncCell(index);
        if (_animate)
        {
            Fx.Pop(_cells[index], _cellSize, _cellSize, 0.72f, 1f, 150);
        }
    }

    private void Explode()
    {
        int hit = _game.ExplodedIndex;
        _clockTimer.Stop();
        _face.Set(Face.Dead);
        _cells[hit].Show(CellLook.Exploded);
        UpdateHud();

        if (_animate)
        {
            Fx.Pop(_cells[hit].Overlay!, _cellSize, _cellSize, 0.3f, 1.8f, 320);
            Point center = CellCenter(hit);
            Fx.Ring(_fx, center, _cellSize, Color.FromArgb(255, 255, 140, 0), (float)(Math.Max(_panel.ActualWidth, _panel.ActualHeight) / _cellSize));
            Fx.Flash(_fx, _panel.ActualWidth, _panel.ActualHeight, Color.FromArgb(255, 255, 0, 0), 0.4f);
            Fx.Shake(_panel, 8);
            Fx.Pop(_faceButton, 32, 32, 0.7f, 1.25f, 240);
        }

        int cols = _game.Columns;
        for (int i = 0; i < _cells.Length; i++)
        {
            if (i == hit)
            {
                continue;
            }

            bool mine = _game.IsMine(i);
            CellMark mark = _game.MarkAt(i);
            CellLook look;
            if (mine && mark != CellMark.Flag)
            {
                look = CellLook.Mine;
            }
            else if (!mine && mark == CellMark.Flag)
            {
                look = CellLook.WrongFlag;
            }
            else
            {
                continue;
            }

            double dist = Math.Sqrt(Math.Pow((i % cols) - (hit % cols), 2) + Math.Pow((i / cols) - (hit / cols), 2));
            int index = i;
            Schedule(() =>
            {
                _cells[index].Show(look);
                if (_animate && _cells[index].Overlay is { } glyph)
                {
                    Fx.Pop(glyph, _cellSize, _cellSize, 0.3f, 1.4f, 240);
                }
            }, (int)Math.Min(1100, 80 + (dist * 45)));
        }

        UpdateStatus();
        UpdateTray();
    }

    private void Celebrate()
    {
        int seconds = Math.Max(1, (int)_game.Elapsed.TotalSeconds);
        bool isBest = GameSession.RecordWin(_game.Difficulty, seconds);
        _resultText = isBest
            ? $"Cleared in {seconds} s. New best time!"
            : $"Cleared in {seconds} s. Press the face or F2 to play again.";
        _clockTimer.Stop();
        _time.Value = seconds;
        _mines.Value = 0;
        _face.Set(Face.Cool);

        int cols = _game.Columns;
        for (int i = 0; i < _cells.Length; i++)
        {
            if (!_game.IsMine(i))
            {
                continue;
            }

            int index = i;
            Schedule(() =>
            {
                SyncCell(index);
                if (_animate)
                {
                    Fx.Pop(_cells[index], _cellSize, _cellSize, 0.5f, 1.35f, 240);
                }
            }, ((i % cols) + (i / cols)) * 22);
        }

        if (_animate)
        {
            Fx.Pop(_faceButton, 32, 32, 0.6f, 1.4f, 320);
            Fx.Confetti(_fx, _panel.ActualWidth, _panel.ActualHeight);
        }

        UpdateStatus();
        UpdateBest();
        UpdateTray();
    }

    private Point CellCenter(int index) =>
        _cells[index].TransformToVisual(_fx).TransformPoint(new Point(_cellSize / 2, _cellSize / 2));

    private void Schedule(Action action, int delayMs)
    {
        _fxQueue.Enqueue(action, _fxClock.ElapsedMilliseconds + delayMs);
        if (!_fxTimer.IsRunning)
        {
            _fxTimer.Start();
        }
    }

    private void RunDueEffects()
    {
        long now = _fxClock.ElapsedMilliseconds;
        while (_fxQueue.TryPeek(out Action? action, out long due) && due <= now)
        {
            _fxQueue.Dequeue();
            action();
        }

        if (_fxQueue.Count == 0)
        {
            _fxTimer.Stop();
        }
    }

    /// <summary>Runs everything still scheduled right now, so the board is in its final state.</summary>
    private void FlushEffects()
    {
        while (_fxQueue.TryDequeue(out Action? action, out _))
        {
            action();
        }

        _fxTimer.Stop();
    }

    // ---- Chrome --------------------------------------------------------------------------

    private void NewGame_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NewGame();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => App.Current.ShowSettings();
}
