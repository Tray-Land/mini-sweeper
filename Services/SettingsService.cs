using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace MiniSweeper.Services;

/// <summary>
/// Typed wrapper over <see cref="ApplicationData.LocalSettings"/>. Every read falls back to a
/// default so a missing or corrupt value never crashes the app. Not for secrets: see
/// <see cref="Secrets"/> (build-time app keys) and <see cref="CredentialStore"/> (user tokens).
/// </summary>
/// <remarks>
/// LocalSettings values must be small (8 KB each) WinRT primitives. Store enums as int, and lists
/// or records as JSON through a source-generated <c>JsonSerializerContext</c> (trim-safe) with
/// <see cref="GetJson"/> / <see cref="SetJson"/>. Anything larger belongs in a file under
/// <c>ApplicationData.Current.LocalFolder</c>.
/// </remarks>
internal static class SettingsService
{
    /// <summary>False until the first launch has shown the flyout.</summary>
    public static bool HasLaunchedBefore
    {
        get => Get(nameof(HasLaunchedBefore), false);
        set => Set(nameof(HasLaunchedBefore), value);
    }

    /// <summary>Right-click cycles flag, question mark, none (classic behavior) when true.</summary>
    public static bool QuestionMarks
    {
        get => Get(nameof(QuestionMarks), true);
        set => Set(nameof(QuestionMarks), value);
    }

    /// <summary>The Windows 11 style board with emoji icons when true; the Windows 95 board when false.</summary>
    public static bool ModernTheme
    {
        get => Get(nameof(ModernTheme), true);
        set => Set(nameof(ModernTheme), value);
    }

    /// <summary>Index into <c>Difficulty.All</c>, or -1 for the custom board.</summary>
    public static int DifficultyIndex
    {
        get => Get(nameof(DifficultyIndex), 0);
        set => Set(nameof(DifficultyIndex), value);
    }

    /// <summary>The last custom board, as columns, rows, mines (defaults to the Intermediate board).</summary>
    public static (int Columns, int Rows, int Mines) CustomBoard
    {
        get => (Get("CustomColumns", 16), Get("CustomRows", 16), Get("CustomMines", 40));
        set
        {
            Set("CustomColumns", value.Columns);
            Set("CustomRows", value.Rows);
            Set("CustomMines", value.Mines);
        }
    }

    /// <summary>True once the player has set up a custom board.</summary>
    public static bool HasCustomBoard => Get("CustomColumns", 0) != 0;

    public static int GetBestSeconds(string difficultyKey) => Get($"Best_{difficultyKey}", 0);

    public static void SetBestSeconds(string difficultyKey, int seconds) => Set($"Best_{difficultyKey}", seconds);

    public static void ClearBestTimes()
    {
        try
        {
            IPropertySet values = ApplicationData.Current.LocalSettings.Values;
            foreach (string key in values.Keys.Where(k => k.StartsWith("Best_", StringComparison.Ordinal)).ToList())
            {
                values.Remove(key);
            }
        }
        catch
        {
            // Settings are best-effort; ignore storage failures.
        }
    }

    public static T? GetJson<T>(string key, JsonTypeInfo<T> typeInfo)
    {
        if (Get<string?>(key, null) is not { } json)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static void SetJson<T>(string key, T? value, JsonTypeInfo<T> typeInfo) =>
        Set(key, value is null ? null : JsonSerializer.Serialize(value, typeInfo));

    private static T Get<T>(string key, T defaultValue)
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out object? value) && value is T typed
                ? typed
                : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static void Set(string key, object? value)
    {
        try
        {
            if (value is null)
            {
                ApplicationData.Current.LocalSettings.Values.Remove(key);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values[key] = value;
            }
        }
        catch
        {
            // Settings are best-effort; ignore storage failures.
        }
    }
}
