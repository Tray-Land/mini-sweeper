using MiniSweeper.Logic;

namespace MiniSweeper.Services;

/// <summary>
/// The game in progress. The flyout closes after a minute hidden, so the game lives here rather
/// than in the page: reopening picks up exactly where the player left off.
/// </summary>
internal static class GameSession
{
    public static Difficulty Difficulty { get; private set; } =
        Difficulty.All[Math.Clamp(SettingsService.DifficultyIndex, 0, Difficulty.All.Count - 1)];

    public static MinesweeperGame Game { get; private set; } = new(Difficulty);

    public static void NewGame() => Game = new MinesweeperGame(Difficulty);

    public static void SetDifficulty(Difficulty difficulty)
    {
        Difficulty = difficulty;
        SettingsService.DifficultyIndex = Difficulty.All.ToList().IndexOf(difficulty);
        NewGame();
    }

    /// <summary>Best winning time in seconds for a difficulty, or 0 when there isn't one.</summary>
    public static int BestSeconds(Difficulty difficulty) => SettingsService.GetBestSeconds(difficulty.Name);

    /// <summary>Records a win; returns true when it beats the previous best.</summary>
    public static bool RecordWin(Difficulty difficulty, int seconds)
    {
        int best = BestSeconds(difficulty);
        if (best != 0 && best <= seconds)
        {
            return false;
        }

        SettingsService.SetBestSeconds(difficulty.Name, seconds);
        return true;
    }
}
