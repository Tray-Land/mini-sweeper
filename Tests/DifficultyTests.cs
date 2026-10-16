using MiniSweeper.Logic;

namespace MiniSweeper.Tests;

public class DifficultyTests
{
    [Fact]
    public void Custom_ValuesInRange_AreKept()
    {
        Difficulty d = Difficulty.Custom(12, 10, 15);

        Assert.Equal((12, 10, 15), (d.Columns, d.Rows, d.Mines));
        Assert.True(d.IsCustom);
    }

    [Fact]
    public void Custom_ValuesOutOfRange_AreClamped()
    {
        Difficulty tooBig = Difficulty.Custom(500, 500, 100000);
        Difficulty tooSmall = Difficulty.Custom(1, 1, 0);

        Assert.Equal((Difficulty.MaxColumns, Difficulty.MaxRows), (tooBig.Columns, tooBig.Rows));
        Assert.Equal(Difficulty.MaxMines(tooBig.Columns, tooBig.Rows), tooBig.Mines);
        Assert.Equal((Difficulty.MinColumns, Difficulty.MinRows, 1), (tooSmall.Columns, tooSmall.Rows, tooSmall.Mines));
    }

    [Fact]
    public void Key_CustomBoards_DifferBySize()
    {
        Assert.NotEqual(Difficulty.Custom(10, 10, 10).Key, Difficulty.Custom(10, 10, 11).Key);
        Assert.Equal("Beginner", Difficulty.Beginner.Key);
    }

    [Fact]
    public void Game_MaxMinesCustomBoard_FirstClickStillSafe()
    {
        Difficulty d = Difficulty.Custom(8, 8, Difficulty.MaxMines(8, 8));
        MinesweeperGame game = new(d);

        game.Reveal(27);

        Assert.NotEqual(GameState.Lost, game.State);
    }
}
