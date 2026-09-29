using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace MiniSweeper.Controls;

internal enum Face { Smile, Oh, Dead, Cool }

/// <summary>The smiley reset button: a yellow face on a raised gray key that sinks when pressed.</summary>
internal sealed class FaceView : BevelBorder
{
    private const double Center = 14;

    private readonly UIElement[] _eyes;
    private readonly UIElement _ohMouth;
    private readonly UIElement _smile;
    private readonly UIElement _frown;
    private readonly UIElement _deadEyes;
    private readonly UIElement _glasses;
    private readonly Canvas _canvas = new() { Width = 28, Height = 28 };

    private readonly TextBlock? _emoji;

    public FaceView() : base(2, radius: 16)
    {
        Width = 32;
        Height = 32;

        if (Skin.IsModern)
        {
            _emoji = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 19,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Content = _emoji;
            _eyes = [];
            _ohMouth = _smile = _frown = _deadEyes = _glasses = _canvas;
            Set(Face.Smile);
            return;
        }

        Content = _canvas;

        Ellipse head = new() { Width = 26, Height = 26, Fill = Win95.Yellow, Stroke = Win95.Black, StrokeThickness = 1 };
        Place(head, 1, 1);

        _eyes = [Eye(9.5), Eye(16)];
        _ohMouth = Place(new Ellipse { Width = 6, Height = 7.5, Fill = Win95.Black }, 11, 16);
        _smile = Curve(smile: true);
        _frown = Curve(smile: false);
        _deadEyes = DeadEyes();
        _glasses = Glasses();

        Set(Face.Smile);
    }

    public void Set(Face face)
    {
        if (_emoji is not null)
        {
            _emoji.Text = face switch { Face.Oh => "😮", Face.Dead => "😵", Face.Cool => "😎", _ => "🙂" };
            return;
        }

        bool cool = face == Face.Cool;
        bool dead = face == Face.Dead;
        foreach (UIElement e in _eyes)
        {
            e.Visibility = face is Face.Smile or Face.Oh ? Visibility.Visible : Visibility.Collapsed;
        }

        _ohMouth.Visibility = face == Face.Oh ? Visibility.Visible : Visibility.Collapsed;
        _smile.Visibility = face is Face.Smile or Face.Cool ? Visibility.Visible : Visibility.Collapsed;
        _frown.Visibility = dead ? Visibility.Visible : Visibility.Collapsed;
        _deadEyes.Visibility = dead ? Visibility.Visible : Visibility.Collapsed;
        _glasses.Visibility = cool ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetPressed(bool pressed)
    {
        SetRaised(!pressed);
        if (_emoji is not null)
        {
            FaceBrush = pressed ? Skin.Inset : Skin.Face;
            return;
        }

        _canvas.Margin = pressed ? new Thickness(1, 1, 0, 0) : new Thickness(0);
    }

    private UIElement Place(UIElement e, double x, double y)
    {
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        _canvas.Children.Add(e);
        return e;
    }

    private UIElement Eye(double x) => Place(new Ellipse { Width = 3.5, Height = 4.5, Fill = Win95.Black }, x, 8.5);

    private UIElement Curve(bool smile)
    {
        PointCollection points = [];
        for (double x = 8; x <= 20.01; x += 1.5)
        {
            double t = (x - Center) / 6;
            double bow = 1 - (t * t);
            points.Add(new Point(x, smile ? 15.5 + (4 * bow) : 21 - (3.5 * bow)));
        }

        return Place(new Polyline { Points = points, Stroke = Win95.Black, StrokeThickness = 1.6 }, 0, 0);
    }

    private UIElement DeadEyes()
    {
        Canvas c = new() { Width = 28, Height = 28 };
        foreach (double x in new[] { 7.5, 15.5 })
        {
            c.Children.Add(new Line { X1 = x, Y1 = 7.5, X2 = x + 5, Y2 = 12, Stroke = Win95.Black, StrokeThickness = 1.6 });
            c.Children.Add(new Line { X1 = x + 5, Y1 = 7.5, X2 = x, Y2 = 12, Stroke = Win95.Black, StrokeThickness = 1.6 });
        }

        return Place(c, 0, 0);
    }

    private UIElement Glasses()
    {
        Canvas c = new() { Width = 28, Height = 28 };
        foreach (double x in new[] { 6.5, 15 })
        {
            Rectangle lens = new() { Width = 6.5, Height = 5, Fill = Win95.Black };
            Canvas.SetLeft(lens, x);
            Canvas.SetTop(lens, 8);
            c.Children.Add(lens);
        }

        c.Children.Add(new Line { X1 = 13, Y1 = 9, X2 = 15, Y2 = 9, Stroke = Win95.Black, StrokeThickness = 1.5 });
        c.Children.Add(new Line { X1 = 6.5, Y1 = 9, X2 = 3.5, Y2 = 7.5, Stroke = Win95.Black, StrokeThickness = 1.2 });
        c.Children.Add(new Line { X1 = 21.5, Y1 = 9, X2 = 24.5, Y2 = 7.5, Stroke = Win95.Black, StrokeThickness = 1.2 });
        return Place(c, 0, 0);
    }
}
