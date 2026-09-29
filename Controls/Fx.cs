using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace MiniSweeper.Controls;

/// <summary>Short Composition animations: pops, shakes, shockwaves and confetti.</summary>
internal static class Fx
{
    private static readonly Color[] ConfettiColors =
    [
        Color.FromArgb(255, 255, 0, 0), Color.FromArgb(255, 0, 0, 255), Color.FromArgb(255, 0, 160, 0),
        Color.FromArgb(255, 255, 220, 0), Color.FromArgb(255, 255, 0, 200), Color.FromArgb(255, 0, 200, 220),
    ];

    private static CubicBezierEasingFunction Ease(Compositor c) =>
        c.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.9f), new Vector2(0.3f, 1f));

    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    private static void Delay(KeyFrameAnimation a, int delayMs)
    {
        if (delayMs > 0)
        {
            a.DelayTime = Ms(delayMs);
            a.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        }
    }

    /// <summary>Scales an element in from <paramref name="from"/>, optionally overshooting.</summary>
    public static void Pop(UIElement e, double w, double h, float from = 0.5f, float overshoot = 1.15f, int ms = 180, int delayMs = 0)
    {
        Visual v = ElementCompositionPreview.GetElementVisual(e);
        Compositor c = v.Compositor;
        CubicBezierEasingFunction ease = Ease(c);
        v.CenterPoint = new Vector3((float)w / 2, (float)h / 2, 0);

        Vector3KeyFrameAnimation a = c.CreateVector3KeyFrameAnimation();
        a.InsertKeyFrame(0f, new Vector3(from, from, 1f));
        if (overshoot > 1f)
        {
            a.InsertKeyFrame(0.55f, new Vector3(overshoot, overshoot, 1f), ease);
        }

        a.InsertKeyFrame(1f, Vector3.One, ease);
        a.Duration = Ms(ms);
        Delay(a, delayMs);
        v.StartAnimation("Scale", a);
    }

    /// <summary>A new board: each square fades and scales in along a diagonal wave.</summary>
    public static void Intro(UIElement e, double size, int delayMs)
    {
        Visual v = ElementCompositionPreview.GetElementVisual(e);
        Compositor c = v.Compositor;
        CubicBezierEasingFunction ease = Ease(c);
        v.CenterPoint = new Vector3((float)size / 2, (float)size / 2, 0);

        Vector3KeyFrameAnimation scale = c.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0f, new Vector3(0.4f, 0.4f, 1f));
        scale.InsertKeyFrame(1f, Vector3.One, ease);
        scale.Duration = Ms(220);
        Delay(scale, delayMs);
        v.StartAnimation("Scale", scale);

        ScalarKeyFrameAnimation fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, 1f);
        fade.Duration = Ms(220);
        Delay(fade, delayMs);
        v.StartAnimation("Opacity", fade);
    }

    /// <summary>Rattles an element around its resting place, decaying to nothing.</summary>
    public static void Shake(UIElement e, float amplitude)
    {
        Visual v = ElementCompositionPreview.GetElementVisual(e);
        Vector3 rest = v.Offset;
        Vector3KeyFrameAnimation a = v.Compositor.CreateVector3KeyFrameAnimation();
        float[] x = [0, -1, 0.9f, -0.75f, 0.55f, -0.35f, 0.15f, 0];
        float[] y = [0, 0.5f, -0.6f, 0.4f, -0.3f, 0.2f, -0.1f, 0];
        for (int i = 0; i < x.Length; i++)
        {
            a.InsertKeyFrame(i / (float)(x.Length - 1), rest + new Vector3(x[i] * amplitude, y[i] * amplitude, 0));
        }

        a.Duration = Ms(480);
        v.StartAnimation("Offset", a);
    }

    /// <summary>An expanding ring that fades out, used as the explosion's shockwave.</summary>
    public static void Ring(Canvas layer, Point center, double radius, Color color, float maxScale, int ms = 600)
    {
        Ellipse ring = new()
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 4,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(ring, center.X - radius);
        Canvas.SetTop(ring, center.Y - radius);
        layer.Children.Add(ring);

        Visual v = ElementCompositionPreview.GetElementVisual(ring);
        Compositor c = v.Compositor;
        v.CenterPoint = new Vector3((float)radius, (float)radius, 0);
        CompositionScopedBatch batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);

        Vector3KeyFrameAnimation scale = c.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0f, new Vector3(0.2f, 0.2f, 1f));
        scale.InsertKeyFrame(1f, new Vector3(maxScale, maxScale, 1f), c.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.7f), new Vector2(0.3f, 1f)));
        scale.Duration = Ms(ms);
        v.StartAnimation("Scale", scale);

        ScalarKeyFrameAnimation fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0.9f);
        fade.InsertKeyFrame(1f, 0f);
        fade.Duration = Ms(ms);
        v.StartAnimation("Opacity", fade);

        batch.End();
        batch.Completed += (_, _) => layer.Children.Remove(ring);
    }

    /// <summary>A color wash over the given size that fades away.</summary>
    public static void Flash(Canvas layer, double w, double h, Color color, float peak)
    {
        Rectangle wash = new() { Width = w, Height = h, Fill = new SolidColorBrush(color), IsHitTestVisible = false };
        layer.Children.Add(wash);

        Visual v = ElementCompositionPreview.GetElementVisual(wash);
        Compositor c = v.Compositor;
        CompositionScopedBatch batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);
        ScalarKeyFrameAnimation fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, peak);
        fade.InsertKeyFrame(1f, 0f);
        fade.Duration = Ms(450);
        v.StartAnimation("Opacity", fade);
        batch.End();
        batch.Completed += (_, _) => layer.Children.Remove(wash);
    }

    /// <summary>Paper rains down over the layer, tumbling as it falls.</summary>
    public static void Confetti(Canvas layer, double w, double h, int count = 70)
    {
        Random rng = Random.Shared;
        List<Rectangle> pieces = [];
        Compositor? compositor = null;
        CompositionScopedBatch? batch = null;

        for (int i = 0; i < count; i++)
        {
            Rectangle piece = new()
            {
                Width = 5 + rng.Next(4),
                Height = 8 + rng.Next(6),
                Fill = new SolidColorBrush(ConfettiColors[rng.Next(ConfettiColors.Length)]),
                IsHitTestVisible = false,
            };
            layer.Children.Add(piece);
            pieces.Add(piece);

            Visual v = ElementCompositionPreview.GetElementVisual(piece);
            compositor ??= v.Compositor;
            batch ??= compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            v.CenterPoint = new Vector3((float)piece.Width / 2, (float)piece.Height / 2, 0);

            float x = (float)(rng.NextDouble() * w);
            float drift = (float)((rng.NextDouble() - 0.5) * 90);
            int fall = 1300 + rng.Next(1100);
            int delay = rng.Next(500);
            CubicBezierEasingFunction gravity = compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(0.9f, 0.7f));

            Vector3KeyFrameAnimation move = compositor.CreateVector3KeyFrameAnimation();
            move.InsertKeyFrame(0f, new Vector3(x, -14, 0));
            move.InsertKeyFrame(1f, new Vector3(x + drift, (float)h + 10, 0), gravity);
            move.Duration = Ms(fall);
            Delay(move, delay);
            v.StartAnimation("Offset", move);

            ScalarKeyFrameAnimation spin = compositor.CreateScalarKeyFrameAnimation();
            spin.InsertKeyFrame(0f, 0f);
            spin.InsertKeyFrame(1f, (float)((rng.NextDouble() - 0.5) * 1080));
            spin.Duration = Ms(fall);
            Delay(spin, delay);
            v.StartAnimation("RotationAngleInDegrees", spin);

            ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 1f);
            fade.InsertKeyFrame(0.8f, 1f);
            fade.InsertKeyFrame(1f, 0f);
            fade.Duration = Ms(fall);
            Delay(fade, delay);
            v.StartAnimation("Opacity", fade);
        }

        if (batch is null)
        {
            return;
        }

        batch.End();
        batch.Completed += (_, _) =>
        {
            foreach (Rectangle piece in pieces)
            {
                layer.Children.Remove(piece);
            }
        };
    }
}
