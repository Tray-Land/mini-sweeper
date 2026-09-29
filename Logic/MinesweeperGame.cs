using System.Diagnostics;

namespace MiniSweeper.Logic;

/// <summary>A board size. <see cref="CellSize"/> is the logical pixel size the flyout draws cells at.</summary>
public sealed record Difficulty(string Name, int Columns, int Rows, int Mines, int CellSize)
{
    public static readonly Difficulty Beginner = new("Beginner", 9, 9, 10, 32);
    public static readonly Difficulty Intermediate = new("Intermediate", 16, 16, 40, 20);
    public static readonly Difficulty Expert = new("Expert", 20, 20, 80, 16);

    public static IReadOnlyList<Difficulty> All { get; } = [Beginner, Intermediate, Expert];
}

public enum CellMark { None, Flag, Question }

public enum GameState { Ready, Playing, Won, Lost }

/// <summary>A cell opened by a reveal and how many steps of flood fill away from the click it was.</summary>
public readonly record struct Opened(int Index, int Depth);

/// <summary>
/// Pure Minesweeper rules with no UI dependency. The first reveal places the mines, so the first
/// click is never a mine, and (when the board has room) never touches one either.
/// </summary>
public sealed class MinesweeperGame
{
    private readonly bool[] _mines;
    private readonly bool[] _revealed;
    private readonly CellMark[] _marks;
    private readonly byte[] _adjacent;
    private readonly Random _random;
    private readonly Stopwatch _clock = new();
    private int _revealedCount;
    private int _flags;

    public MinesweeperGame(Difficulty difficulty, Random? random = null)
    {
        Difficulty = difficulty;
        _random = random ?? Random.Shared;
        int count = difficulty.Columns * difficulty.Rows;
        _mines = new bool[count];
        _revealed = new bool[count];
        _marks = new CellMark[count];
        _adjacent = new byte[count];
    }

    public Difficulty Difficulty { get; }
    public int Columns => Difficulty.Columns;
    public int Rows => Difficulty.Rows;
    public int Count => _mines.Length;
    public GameState State { get; private set; } = GameState.Ready;

    /// <summary>The mine that ended the game, or -1.</summary>
    public int ExplodedIndex { get; private set; } = -1;

    /// <summary>Mines minus flags; goes negative when the player over-flags.</summary>
    public int MinesLeft => Difficulty.Mines - _flags;

    public TimeSpan Elapsed => _clock.Elapsed;

    public bool IsMine(int index) => _mines[index];
    public bool IsRevealed(int index) => _revealed[index];
    public CellMark MarkAt(int index) => _marks[index];
    public int Adjacent(int index) => _adjacent[index];

    /// <summary>Stops the clock while the flyout is hidden.</summary>
    public void Pause()
    {
        if (State == GameState.Playing)
        {
            _clock.Stop();
        }
    }

    public void Resume()
    {
        if (State == GameState.Playing)
        {
            _clock.Start();
        }
    }

    public IEnumerable<int> Neighbors(int index)
    {
        int c = index % Columns;
        int r = index / Columns;
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                int nc = c + dc;
                int nr = r + dr;
                if ((dr != 0 || dc != 0) && nc >= 0 && nc < Columns && nr >= 0 && nr < Rows)
                {
                    yield return nr * Columns + nc;
                }
            }
        }
    }

    /// <summary>Cycles none, flag, and (optionally) question mark. Returns false when nothing can change.</summary>
    public bool ToggleMark(int index, bool allowQuestion)
    {
        if (State is GameState.Won or GameState.Lost || _revealed[index])
        {
            return false;
        }

        CellMark next = _marks[index] switch
        {
            CellMark.None => CellMark.Flag,
            CellMark.Flag => allowQuestion ? CellMark.Question : CellMark.None,
            _ => CellMark.None,
        };

        if (_marks[index] == CellMark.Flag)
        {
            _flags--;
        }

        if (next == CellMark.Flag)
        {
            _flags++;
        }

        _marks[index] = next;
        return true;
    }

    /// <summary>Opens a cell, flooding outwards from empty ones. A mine ends the game.</summary>
    public IReadOnlyList<Opened> Reveal(int index)
    {
        if (State is GameState.Won or GameState.Lost || _revealed[index] || _marks[index] == CellMark.Flag)
        {
            return [];
        }

        if (State == GameState.Ready)
        {
            PlaceMines(index);
            State = GameState.Playing;
            _clock.Start();
        }

        return Open([index]);
    }

    /// <summary>
    /// On a revealed number whose flags match it, opens every unflagged neighbor. A wrong flag
    /// means one of those is a mine.
    /// </summary>
    public IReadOnlyList<Opened> Chord(int index)
    {
        if (State != GameState.Playing || !_revealed[index] || _adjacent[index] == 0)
        {
            return [];
        }

        int flags = 0;
        List<int> targets = [];
        foreach (int n in Neighbors(index))
        {
            if (_marks[n] == CellMark.Flag)
            {
                flags++;
            }
            else if (!_revealed[n])
            {
                targets.Add(n);
            }
        }

        return flags == _adjacent[index] ? Open(targets) : [];
    }

    private List<Opened> Open(IEnumerable<int> sources)
    {
        List<Opened> opened = [];
        Queue<Opened> frontier = new();

        void Visit(int i, int depth)
        {
            if (_revealed[i] || _marks[i] == CellMark.Flag)
            {
                return;
            }

            if (_mines[i])
            {
                if (State != GameState.Lost)
                {
                    State = GameState.Lost;
                    ExplodedIndex = i;
                    _clock.Stop();
                }

                return;
            }

            _revealed[i] = true;
            _revealedCount++;
            _marks[i] = CellMark.None;
            opened.Add(new Opened(i, depth));
            if (_adjacent[i] == 0)
            {
                frontier.Enqueue(new Opened(i, depth));
            }
        }

        foreach (int s in sources)
        {
            Visit(s, 0);
        }

        while (frontier.Count > 0)
        {
            Opened current = frontier.Dequeue();
            foreach (int n in Neighbors(current.Index))
            {
                Visit(n, current.Depth + 1);
            }
        }

        if (State == GameState.Playing && _revealedCount == Count - Difficulty.Mines)
        {
            State = GameState.Won;
            _clock.Stop();
            for (int i = 0; i < Count; i++)
            {
                if (_mines[i])
                {
                    _marks[i] = CellMark.Flag;
                }
            }

            _flags = Difficulty.Mines;
        }

        return opened;
    }

    private void PlaceMines(int safeIndex)
    {
        HashSet<int> excluded = [safeIndex, .. Neighbors(safeIndex)];
        if (Count - excluded.Count < Difficulty.Mines)
        {
            excluded = [safeIndex];
        }

        int[] candidates = Enumerable.Range(0, Count).Where(i => !excluded.Contains(i)).ToArray();
        _random.Shuffle(candidates);
        foreach (int i in candidates.Take(Difficulty.Mines))
        {
            _mines[i] = true;
        }

        for (int i = 0; i < Count; i++)
        {
            _adjacent[i] = (byte)Neighbors(i).Count(n => _mines[n]);
        }
    }
}
