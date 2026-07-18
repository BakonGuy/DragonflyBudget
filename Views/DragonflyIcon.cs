using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dragonfly.Views;

/// <summary>
/// Vector dragonfly (top-down: filled wings + solid body), matching the reference art.
/// Three tiers, all drawn in a 240 x 200 space centered on x = 120:
///   • Small  — beefed body so it survives at taskbar / title-bar sizes (16–32px).
///   • Medium — the elegant sidebar mark.
///   • Large  — the medium geometry; the official logo for big displays (see dragonfly.svg).
/// </summary>
public static class DragonflyIcon
{
    public static readonly Color Accent = (Color)ColorConverter.ConvertFromString("#A78BFA");

    // ── MEDIUM: sidebar mark; also the reference for Large. ──
    public static Drawing BuildMediumDrawing(Color color)
    {
        var brush = new SolidColorBrush(color); brush.Freeze();
        var wings = new GeometryGroup { FillRule = FillRule.Nonzero };
        wings.Children.Add(FilledWing(176, 66, 60, 21, -20)); // upper right
        wings.Children.Add(FilledWing(64, 66, 60, 21, 20));   // upper left
        wings.Children.Add(FilledWing(168, 110, 48, 17, 20)); // lower right
        wings.Children.Add(FilledWing(72, 110, 48, 17, -20)); // lower left

        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(brush, null, wings));
        g.Children.Add(new GeometryDrawing(brush, null, MediumBody()));
        g.Freeze();
        return g;
    }

    // ── SMALL: taskbar / title bar. Heavier body so it doesn't vanish. ──
    public static Drawing BuildSmallDrawing(Color color)
    {
        var brush = new SolidColorBrush(color); brush.Freeze();
        var wings = new GeometryGroup { FillRule = FillRule.Nonzero };
        wings.Children.Add(FilledWing(178, 62, 58, 19, -20)); // upper right
        wings.Children.Add(FilledWing(62, 62, 58, 19, 20));   // upper left
        wings.Children.Add(FilledWing(170, 100, 46, 15, 20)); // lower right
        wings.Children.Add(FilledWing(70, 100, 46, 15, -20)); // lower left

        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(brush, null, wings));
        g.Children.Add(new GeometryDrawing(brush, null, SmallBody()));
        g.Freeze();
        return g;
    }

    // ── bodies ──
    private static Geometry MediumBody()
    {
        const double w = 1.25;
        var b = new GeometryGroup();
        b.Children.Add(new EllipseGeometry(new Point(109, 30), 8.5 * w, 8.5 * w)); // head lobe
        b.Children.Add(new EllipseGeometry(new Point(131, 30), 8.5 * w, 8.5 * w)); // head lobe
        b.Children.Add(new EllipseGeometry(new Point(120, 42), 15 * w, 13));       // head
        b.Children.Add(new EllipseGeometry(new Point(120, 78), 13 * w, 27));       // thorax
        b.Children.Add(Tail(2, 180));
        return b;
    }

    private static Geometry SmallBody()
    {
        const double w = 1.7;
        var b = new GeometryGroup();
        b.Children.Add(new EllipseGeometry(new Point(107, 26), 9 * w, 9)); // head lobe
        b.Children.Add(new EllipseGeometry(new Point(133, 26), 9 * w, 9)); // head lobe
        b.Children.Add(new EllipseGeometry(new Point(120, 40), 15 * w, 15)); // head
        b.Children.Add(new EllipseGeometry(new Point(120, 80), 13 * w, 30)); // thorax
        b.Children.Add(Tail(6, 190));
        return b;
    }

    private static EllipseGeometry FilledWing(double cx, double cy, double rx, double ry, double angle)
    {
        var geo = new EllipseGeometry(new Point(cx, cy), rx, ry) { Transform = new RotateTransform(angle, cx, cy) };
        geo.Freeze();
        return geo;
    }

    private static Geometry Tail(double o, double tip)
    {
        var fig = new PathFigure { StartPoint = new Point(111 - o, 98), IsClosed = true };
        fig.Segments.Add(new BezierSegment(new Point(115 - o, 145), new Point(116, 176), new Point(120, tip), true));
        fig.Segments.Add(new BezierSegment(new Point(124, 176), new Point(125 + o, 145), new Point(129 + o, 98), true));
        var path = new PathGeometry();
        path.Figures.Add(fig);
        path.Freeze();
        return path;
    }

    // ── images ──
    /// <summary>Sidebar-sized (and Large) mark.</summary>
    public static DrawingImage BuildMediumImage(Color color) => new(BuildMediumDrawing(color));
    /// <summary>Small mark for tiny surfaces.</summary>
    public static DrawingImage BuildSmallImage(Color color) => new(BuildSmallDrawing(color));

    /// <summary>Window / taskbar icon, built from the Small glyph.</summary>
    public static ImageSource MakeIcon() => RenderAt(BuildSmallDrawing(Accent), 64);

    private static BitmapSource RenderAt(Drawing drawing, int size)
    {
        double pad = size * 0.06;
        double scale = (size - pad * 2) / 240.0;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(pad, size * 0.14));
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawDrawing(drawing);
            dc.Pop();
            dc.Pop();
        }
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }
}
