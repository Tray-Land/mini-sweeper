using MiniSweeper.Logic;

namespace MiniSweeper.Services;

/// <summary>
/// The game in progress. The flyout closes after a minute hidden, so the game lives here rather
/// than in the page: reopening picks up exactly where the player left off.
/// </summary>
internal static class GameSession
{
    public static Difficulty Difficulty { get; private set; } = LoadDifficulty();

    public static MinesweeperGame Game { get; private set; } = new(Difficulty);

    /// <summary>The custom board the player last set up, whether or not it is the one in play.</summary>
    public static Difficulty CustomDifficulty { get; private set; } = LoadCustom();

    public static void NewGame() => Game = new MinesweeperGame(Difficulty);

    public static void SetDifficulty(Difficulty difficulty)
    {
        Difficulty = difficulty;
        SettingsService.DifficultyIndex = difficulty.IsCustom ? -1 : Difficulty.All.ToList().IndexOf(difficulty);
        NewGame();
    }

    /// <summary>Starts a game on a custom board; values outside the allowed range are pulled in.</summary>
    public static Difficulty SetCustom(int columns, int rows, int mines)
    {
        Difficulty custom = Difficulty.Custom(columns, rows, mines);
        SettingsService.CustomBoard = (custom.Columns, custom.Rows, custom.Mines);
        CustomDifficulty = custom;
        SetDifficulty(custom);
        return custom;
    }

    private static Difficulty LoadCustom()
    {
        (int columns, int rows, int mines) = SettingsService.CustomBoard;
        return Difficulty.Custom(columns, rows, mines);
    }

    private static Difficulty LoadDifficulty()
    {
        int index = SettingsService.DifficultyIndex;
        return index < 0 ? LoadCustom() : Difficulty.All[Math.Clamp(index, 0, Difficulty.All.Count - 1)];
    }

    /// <summary>Best winning time in seconds for a difficulty, or 0 when there isn't one.</summary>
    public static int BestSeconds(Difficulty difficulty) => SettingsService.GetBestSeconds(difficulty.Key);

    /// <summary>Records a win; returns true when it beats the previous best.</summary>
    public static bool RecordWin(Difficulty difficulty, int seconds)
    {
        int best = BestSeconds(difficulty);
        if (best != 0 && best <= seconds)
        {
            return false;
        }

        SettingsService.SetBestSeconds(difficulty.Key, seconds);
        return true;
    }
}
