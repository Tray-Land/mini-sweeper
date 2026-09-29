using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace MiniSweeper.Controls;

internal enum CellLook { Hidden, Flag, Question, Pressed, Empty, Number, Mine, Exploded, WrongFlag }

/// <summary>One square of the minefield. Glyphs are built lazily so untouched cells stay cheap.</summary>
internal sealed class CellView : Grid
{
    private readonly double _size;
    private readonly BevelBorder _bevel;
    private CellLook _look = CellLook.Hidden;
    private int _number;

    public CellView(double size)
    {
        _size = size;
        Width = size;
        Height = size;
        _bevel = new BevelBorder(size >= 28 ? 3 : 2, radius: 5);
        if (Skin.IsModern)
        {
            _bevel.Margin = new Thickness(1);
        }

        Children.Add(_bevel);
    }

    /// <summary>The flag, mine, number or question mark on top of the bevel, for animating.</summary>
    public UIElement? Overlay { get; private set; }

    public double Size => _size;

    public void Show(CellLook look, int number = 0)
    {
        if (look == _look && number == _number)
        {
            return;
        }

        _look = look;
        _number = number;
        bool raised = look is CellLook.Hidden or CellLook.Flag or CellLook.Question;
        _bevel.FaceBrush = look == CellLook.Exploded ? Skin.Boom : raised ? Skin.Face : Skin.Revealed;

        if (raised)
        {
            _bevel.SetRaised(true);
        }
        else
        {
            _bevel.SetFlat();
        }

        SetOverlay(Skin.IsModern ? ModernOverlay(look, number) : look switch
        {
            CellLook.Flag => (UIElement)Flag(_size),
            CellLook.Question => Text("?", Win95.Black),
            CellLook.Number => Text(number.ToString(), Skin.NumberBrush(number)),
            CellLook.Mine or CellLook.Exploded => Mine(_size),
            CellLook.WrongFlag => WrongFlag(_size),
            _ => null,
        });
    }

    /// <summary>The modern skin draws its icons as emoji.</summary>
    private UIElement? ModernOverlay(CellLook look, int number) => look switch
    {
        CellLook.Flag => Emoji("🚩"),
        CellLook.Question => Emoji("❓", 0.5),
        CellLook.Number => new TextBlock
        {
            Text = number.ToString(),
            Foreground = Skin.NumberBrush(number),
            FontFamily = new FontFamily("Segoe UI Variable Display"),
            FontWeight = FontWeights.Bold,
            FontSize = _size * 0.6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
        CellLook.Mine => Emoji("💣"),
        CellLook.Exploded => Emoji("💥"),
        CellLook.WrongFlag => WrongFlagEmoji(),
        _ => null,
    };

    private TextBlock Emoji(string glyph, double scale = 0.58) => new()
    {
        Text = glyph,
        FontFamily = new FontFamily("Segoe UI Emoji"),
        FontSize = _size * scale,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>A bomb with a cross over it: a flag that was on a safe square.</summary>
    private Grid WrongFlagEmoji()
    {
        Grid g = new();
        g.Children.Add(Emoji("💣", 0.5));
        g.Children.Add(Emoji("❌", 0.6));
        return g;
    }

    private void SetOverlay(UIElement? overlay)
    {
        if (Overlay is not null)
        {
            Children.Remove(Overlay);
        }

        Overlay = overlay;
        if (overlay is not null)
        {
            overlay.IsHitTestVisible = false;
            Children.Add(overlay);
        }
    }

    private TextBlock Text(string text, Brush brush) => new()
    {
        Text = text,
        Foreground = brush,
        FontFamily = new FontFamily("Segoe UI"),
        FontWeight = FontWeights.ExtraBold,
        FontSize = _size * 0.72,
        TextLineBounds = TextLineBounds.Tight,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Canvas Glyph(double size) => new() { Width = size, Height = size };

    private static Rectangle Rect(double x, double y, double w, double h, Brush fill)
    {
        Rectangle r = new() { Width = w, Height = h, Fill = fill };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        return r;
    }

    /// <summary>The classic red pennant on a black pole and base, drawn on a 16-unit grid.</summary>
    private static Canvas Flag(double s)
    {
        double k = s / 16;
        Canvas c = Glyph(s);
        Polygon pennant = new()
        {
            Fill = Win95.Red,
            Points = [new Point(9.2 * k, 3 * k), new Point(9.2 * k, 8.4 * k), new Point(3.6 * k, 5.7 * k)],
        };
        c.Children.Add(pennant);
        c.Children.Add(Rect(8.8 * k, 3 * k, 1.4 * k, 9 * k, Win95.Black));
        c.Children.Add(Rect(6.5 * k, 11 * k, 5.6 * k, 1.4 * k, Win95.Black));
        c.Children.Add(Rect(4.5 * k, 12.2 * k, 9.6 * k, 1.8 * k, Win95.Black));
        return c;
    }

    /// <summary>Black ball, four spikes, and the white glint.</summary>
    private static Canvas Mine(double s)
    {
        double k = s / 16;
        Canvas c = Glyph(s);
        void Spike(double x1, double y1, double x2, double y2) => c.Children.Add(new Line
        {
            X1 = x1 * k, Y1 = y1 * k, X2 = x2 * k, Y2 = y2 * k,
            Stroke = Win95.Black,
            StrokeThickness = Math.Max(1, 1.4 * k),
        });

        Spike(2.2, 8, 13.8, 8);
        Spike(8, 2.2, 8, 13.8);
        Spike(3.7, 3.7, 12.3, 12.3);
        Spike(12.3, 3.7, 3.7, 12.3);

        Ellipse body = new() { Width = 9 * k, Height = 9 * k, Fill = Win95.Black };
        Canvas.SetLeft(body, 3.5 * k);
        Canvas.SetTop(body, 3.5 * k);
        c.Children.Add(body);
        c.Children.Add(Rect(5.6 * k, 5.6 * k, 2 * k, 2 * k, Win95.Light));
        return c;
    }

    /// <summary>A mine with a red cross through it: a flag that was on a safe square.</summary>
    private static Canvas WrongFlag(double s)
    {
        double k = s / 16;
        Canvas c = Mine(s);
        foreach ((double x1, double y1, double x2, double y2) in new[] { (2.0, 2.0, 14.0, 14.0), (14.0, 2.0, 2.0, 14.0) })
        {
            c.Children.Add(new Line
            {
                X1 = x1 * k, Y1 = y1 * k, X2 = x2 * k, Y2 = y2 * k,
                Stroke = Win95.Red,
                StrokeThickness = Math.Max(1.5, 1.8 * k),
            });
        }

        return c;
    }
}
