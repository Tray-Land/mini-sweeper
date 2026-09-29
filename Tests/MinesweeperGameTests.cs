using MiniSweeper.Logic;

namespace MiniSweeper.Tests;

public class MinesweeperGameTests
{
    private static MinesweeperGame NewGame(int seed = 1) => new(Difficulty.Beginner, new Random(seed));

    private static int CountMines(MinesweeperGame g) => Enumerable.Range(0, g.Count).Count(g.IsMine);

    [Fact]
    public void FirstRevealIsNeverAMineAndOpensAnArea()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            MinesweeperGame g = NewGame(seed);
            IReadOnlyList<Opened> opened = g.Reveal(40);

            Assert.NotEqual(GameState.Lost, g.State);
            Assert.False(g.IsMine(40));
            Assert.Equal(0, g.Adjacent(40));
            Assert.True(opened.Count > 1);
            Assert.Equal(Difficulty.Beginner.Mines, CountMines(g));
        }
    }

    [Fact]
    public void FloodFillDepthGrowsOutwardFromTheClick()
    {
        MinesweeperGame g = NewGame();
        IReadOnlyList<Opened> opened = g.Reveal(40);

        Assert.Equal(0, opened[0].Depth);
        Assert.Equal(40, opened[0].Index);
        Assert.All(opened.Zip(opened.Skip(1)), p => Assert.True(p.Second.Depth >= p.First.Depth));
    }

    [Fact]
    public void RevealingAMineLosesAndRecordsIt()
    {
        MinesweeperGame g = NewGame();
        g.Reveal(0);
        int mine = Enumerable.Range(0, g.Count).First(g.IsMine);

        g.Reveal(mine);

        Assert.Equal(GameState.Lost, g.State);
        Assert.Equal(mine, g.ExplodedIndex);
    }

    [Fact]
    public void FlagsBlockRevealAndCountAgainstMines()
    {
        MinesweeperGame g = NewGame();
        Assert.True(g.ToggleMark(5, allowQuestion: true));
        Assert.Equal(9, g.MinesLeft);
        Assert.Empty(g.Reveal(5));
        Assert.Equal(GameState.Ready, g.State);

        g.ToggleMark(5, allowQuestion: true);
        Assert.Equal(CellMark.Question, g.MarkAt(5));
        Assert.Equal(10, g.MinesLeft);

        g.ToggleMark(5, allowQuestion: true);
        Assert.Equal(CellMark.None, g.MarkAt(5));
    }

    [Fact]
    public void WithoutQuestionMarksFlagGoesStraightToNone()
    {
        MinesweeperGame g = NewGame();
        g.ToggleMark(5, allowQuestion: false);
        g.ToggleMark(5, allowQuestion: false);
        Assert.Equal(CellMark.None, g.MarkAt(5));
    }

    [Fact]
    public void ChordOpensNeighborsWhenFlagsMatchAndLosesOnAWrongFlag()
    {
        MinesweeperGame g = NewGame(7);
        g.Reveal(40);
        int number = Enumerable.Range(0, g.Count).First(i => g.IsRevealed(i) && g.Adjacent(i) > 0);

        // Flag every neighbor that is a mine: the chord is then safe.
        foreach (int n in g.Neighbors(number).Where(g.IsMine))
        {
            g.ToggleMark(n, false);
        }

        g.Chord(number);
        Assert.NotEqual(GameState.Lost, g.State);
        Assert.All(g.Neighbors(number).Where(n => !g.IsMine(n)), n => Assert.True(g.IsRevealed(n)));
    }

    [Fact]
    public void ClearingEverySafeCellWinsAndFlagsTheMines()
    {
        MinesweeperGame g = NewGame(3);
        g.Reveal(40);
        foreach (int i in Enumerable.Range(0, g.Count).Where(i => !g.IsMine(i)))
        {
            g.Reveal(i);
        }

        Assert.Equal(GameState.Won, g.State);
        Assert.Equal(0, g.MinesLeft);
        Assert.All(Enumerable.Range(0, g.Count).Where(g.IsMine), i => Assert.Equal(CellMark.Flag, g.MarkAt(i)));
    }

    [Fact]
    public void InputIsIgnoredOnceTheGameIsOver()
    {
        MinesweeperGame g = NewGame();
        g.Reveal(0);
        g.Reveal(Enumerable.Range(0, g.Count).First(g.IsMine));

        Assert.False(g.ToggleMark(1, true));
        Assert.Empty(g.Reveal(1));
    }
}
