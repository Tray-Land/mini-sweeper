using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MiniSweeper.Controls;

/// <summary>
/// The fixed Windows 95 palette of the classic skin. In that skin the board deliberately ignores
/// the app theme: it is its own little gray window, the same in light and dark. See <see cref="Skin"/>.
/// </summary>
internal static class Win95
{
    public static readonly SolidColorBrush Face = Rgb(192, 192, 192);
    public static readonly SolidColorBrush Light = Rgb(255, 255, 255);
    public static readonly SolidColorBrush Shadow = Rgb(128, 128, 128);
    public static readonly SolidColorBrush Black = Rgb(0, 0, 0);
    public static readonly SolidColorBrush Red = Rgb(255, 0, 0);
    public static readonly SolidColorBrush Yellow = Rgb(255, 255, 0);

    public static SolidColorBrush Rgb(byte r, byte g, byte b) => new(Color.FromArgb(255, r, g, b));
}

/// <summary>
/// A Windows 95 style bevel built from three nested borders (cheap enough for hundreds of
/// cells): a dark outer, a light L-shape over it, and the face inside. Swapping the two edge
/// brushes flips raised and sunken.
/// </summary>
internal class BevelBorder : Grid
{
    private readonly Border _outer = new();
    private readonly Border _mid = new();
    private readonly Border _inner = new();
    private readonly int _thickness;

    /// <param name="thickness">Classic bevel width.</param>
    /// <param name="raised">Raised or sunken.</param>
    /// <param name="radius">Corner radius, used by the modern skin only.</param>
    public BevelBorder(int thickness, bool raised = true, double radius = 4)
    {
        _thickness = thickness;
        _inner.Background = Skin.IsModern && !raised ? Skin.Inset : Skin.Face;
        if (Skin.IsModern)
        {
            CornerRadius r = new(radius);
            _outer.CornerRadius = r;
            _mid.CornerRadius = r;
            _inner.CornerRadius = r;
        }

        Children.Add(_outer);
        _outer.Child = _mid;
        _mid.Child = _inner;
        SetRaised(raised);
    }

    /// <summary>Space between the frame and its content (modern skin only).</summary>
    public double ContentPadding
    {
        set => _inner.Padding = Skin.IsModern ? new Thickness(value) : new Thickness(0);
    }

    public Brush FaceBrush
    {
        get => _inner.Background;
        set => _inner.Background = value;
    }

    /// <summary>What sits inside the bevel.</summary>
    public UIElement? Content
    {
        get => _inner.Child;
        set => _inner.Child = value;
    }

    public void SetRaised(bool raised)
    {
        if (Skin.IsModern)
        {
            // Raised: a filled tile with a hairline. Sunken: a plain well, no line.
            SetModernEdge(raised ? 1 : 0);
            return;
        }

        _mid.Margin = new Thickness(0, 0, _thickness, _thickness);
        _inner.Margin = new Thickness(_thickness, _thickness, 0, 0);
        _outer.Background = raised ? Win95.Shadow : Win95.Light;
        _mid.Background = raised ? Win95.Light : Win95.Shadow;
    }

    /// <summary>The revealed-cell look: a single gray line on the top and left.</summary>
    public void SetFlat()
    {
        if (Skin.IsModern)
        {
            SetModernEdge(0);
            return;
        }

        _mid.Margin = new Thickness(0);
        _inner.Margin = new Thickness(1, 1, 0, 0);
        _outer.Background = Win95.Shadow;
        _mid.Background = Win95.Shadow;
    }

    private void SetModernEdge(double width)
    {
        _mid.Margin = new Thickness(0);
        _inner.Margin = new Thickness(0);
        _outer.Background = null;
        _mid.Background = null;
        _outer.BorderBrush = Skin.Stroke;
        _outer.BorderThickness = new Thickness(width);
    }
}
