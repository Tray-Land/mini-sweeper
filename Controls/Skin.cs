using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MiniSweeper.Controls;

/// <summary>
/// The look of the board: the fixed Windows 95 palette (classic) or a Windows 11 style palette
/// that follows the taskbar's light/dark mode (modern). Controls read the current brushes when
/// they are built, so switching skins means rebuilding the board with <see cref="Apply"/> first.
/// </summary>
internal static class Skin
{
    private static readonly SolidColorBrush[] ClassicNumbers =
    [
        Win95.Black, Win95.Rgb(0, 0, 255), Win95.Rgb(0, 128, 0), Win95.Rgb(255, 0, 0), Win95.Rgb(0, 0, 128),
        Win95.Rgb(128, 0, 0), Win95.Rgb(0, 128, 128), Win95.Rgb(0, 0, 0), Win95.Rgb(128, 128, 128),
    ];

    private static SolidColorBrush[] _numbers = ClassicNumbers;

    public static bool IsModern { get; private set; }

    /// <summary>Raised controls: the panel and unopened cells.</summary>
    public static SolidColorBrush Face { get; private set; } = Win95.Face;

    /// <summary>Sunken wells: the counter bar and the board frame.</summary>
    public static SolidColorBrush Inset { get; private set; } = Win95.Face;

    /// <summary>An opened cell.</summary>
    public static SolidColorBrush Revealed { get; private set; } = Win95.Face;

    /// <summary>The hairline around raised modern controls.</summary>
    public static SolidColorBrush Stroke { get; private set; } = Win95.Shadow;

    /// <summary>The cell that was stepped on.</summary>
    public static SolidColorBrush Boom { get; private set; } = Win95.Red;

    /// <summary>Text and counters in the modern skin.</summary>
    public static SolidColorBrush Ink { get; private set; } = Win95.Black;

    public static Brush NumberBrush(int n) => _numbers[Math.Clamp(n, 0, 8)];

    /// <summary>Identifies what a built board looks like, to tell when it needs rebuilding.</summary>
    public static (bool Modern, bool Light) Key { get; private set; }

    public static void Apply(bool modern, bool light)
    {
        Key = (modern, modern && light);
        IsModern = modern;
        if (!modern)
        {
            _numbers = ClassicNumbers;
            Face = Inset = Revealed = Win95.Face;
            Stroke = Win95.Shadow;
            Boom = Win95.Red;
            Ink = Win95.Black;
            return;
        }

        if (light)
        {
            Face = Argb(255, 255, 255, 255);
            Inset = Argb(255, 232, 232, 232);
            Revealed = Argb(255, 214, 214, 214);
            Stroke = Argb(255, 200, 200, 200);
            Ink = Argb(255, 27, 27, 27);
            _numbers = Numbers(0x0067C0, 0x107C10, 0xC42B1C, 0x5C2E91, 0xA4262C, 0x038387, 0x1B1B1B, 0x6E6E6E);
        }
        else
        {
            Face = Argb(255, 74, 74, 74);
            Inset = Argb(255, 44, 44, 44);
            Revealed = Argb(255, 32, 32, 32);
            Stroke = Argb(255, 96, 96, 96);
            Ink = Argb(255, 255, 255, 255);
            _numbers = Numbers(0x60CDFF, 0x6CCB5F, 0xFF99A4, 0xC6A0F6, 0xF1707B, 0x4CC2C6, 0xE0E0E0, 0xA0A0A0);
        }

        Boom = Argb(255, 209, 52, 56);
    }

    private static SolidColorBrush Argb(byte a, byte r, byte g, byte b) => new(Color.FromArgb(a, r, g, b));

    private static SolidColorBrush[] Numbers(params int[] rgb) =>
        [Argb(255, 0, 0, 0), .. rgb.Select(c => Argb(255, (byte)(c >> 16), (byte)(c >> 8), (byte)c))];
}
