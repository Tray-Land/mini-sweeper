using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace MiniSweeper.Controls;

/// <summary>A three-digit red seven-segment counter, with the dim unlit segments a real LED shows.</summary>
internal sealed class LedDisplay : BevelBorder
{
    private const int DigitCount = 3;
    private const double DigitWidth = 13;
    private const double DigitHeight = 23;
    private const double DigitGap = 1;

    private static readonly SolidColorBrush On = Win95.Rgb(255, 30, 20);
    private static readonly SolidColorBrush Off = Win95.Rgb(64, 0, 0);

    // Segment bits: a top, b top right, c bottom right, d bottom, e bottom left, f top left, g middle.
    private static readonly int[] Glyphs =
    [
        0b0111111, 0b0000110, 0b1011011, 0b1001111, 0b1100110,
        0b1101101, 0b1111101, 0b0000111, 0b1111111, 0b1101111,
    ];

    private const int Minus = 0b1000000;

    private readonly Polygon[][] _segments = new Polygon[DigitCount][];
    private int _value = int.MinValue;

    private readonly TextBlock? _text;
    private readonly string _emoji;

    /// <param name="emoji">Icon shown before the number in the modern skin.</param>
    public LedDisplay(string emoji = "") : base(1, raised: false, radius: 8)
    {
        _emoji = emoji;
        if (Skin.IsModern)
        {
            Width = 76;
            Height = 34;
            FaceBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            _text = new TextBlock
            {
                Foreground = Skin.Ink,
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI Emoji"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 17,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Content = _text;
            Value = 0;
            return;
        }

        Width = (DigitCount * DigitWidth) + ((DigitCount - 1) * DigitGap) + 8;
        Height = DigitHeight + 8;
        FaceBrush = Win95.Black;

        Canvas canvas = new() { Margin = new Thickness(3) };
        for (int d = 0; d < DigitCount; d++)
        {
            double left = d * (DigitWidth + DigitGap);
            _segments[d] = new Polygon[7];
            for (int s = 0; s < 7; s++)
            {
                Polygon p = new() { Points = SegmentPoints(s), Fill = Off };
                Canvas.SetLeft(p, left);
                _segments[d][s] = p;
                canvas.Children.Add(p);
            }
        }

        Content = canvas;
        Value = 0;
    }

    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Clamp(value, -99, 999);
            if (clamped == _value)
            {
                return;
            }

            _value = clamped;
            if (_text is not null)
            {
                _text.Text = $"{_emoji} {clamped}";
                return;
            }

            int magnitude = Math.Abs(clamped);
            int[] masks = new int[DigitCount];
            if (clamped < 0)
            {
                masks[0] = Minus;
                masks[1] = Glyphs[magnitude / 10 % 10];
                masks[2] = Glyphs[magnitude % 10];
            }
            else
            {
                masks[0] = Glyphs[magnitude / 100 % 10];
                masks[1] = Glyphs[magnitude / 10 % 10];
                masks[2] = Glyphs[magnitude % 10];
            }

            for (int d = 0; d < DigitCount; d++)
            {
                for (int s = 0; s < 7; s++)
                {
                    _segments[d][s].Fill = (masks[d] & (1 << s)) != 0 ? On : Off;
                }
            }
        }
    }

    private static PointCollection SegmentPoints(int segment)
    {
        const double t = 1.5;
        PointCollection Horizontal(double x1, double x2, double y) =>
            [new Point(x1, y), new Point(x1 + t, y - t), new Point(x2 - t, y - t), new Point(x2, y), new Point(x2 - t, y + t), new Point(x1 + t, y + t)];
        PointCollection Vertical(double x, double y1, double y2) =>
            [new Point(x, y1), new Point(x + t, y1 + t), new Point(x + t, y2 - t), new Point(x, y2), new Point(x - t, y2 - t), new Point(x - t, y1 + t)];

        const double mid = 11.5;
        return segment switch
        {
            0 => Horizontal(2.5, 10.5, 1.5),
            1 => Vertical(11.5, 2.5, mid - 0.5),
            2 => Vertical(11.5, mid + 0.5, 20.5),
            3 => Horizontal(2.5, 10.5, 21.5),
            4 => Vertical(1.5, mid + 0.5, 20.5),
            5 => Vertical(1.5, 2.5, mid - 0.5),
            _ => Horizontal(2.5, 10.5, mid),
        };
    }
}
