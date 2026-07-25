using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>
/// Row reordering by dragging a grip handle. The grip is the drag <em>source</em> (carrying its
/// row's payload); the whole table is the drop <em>target</em>. Hovering anywhere over a row picks
/// the insertion point by the row's top/bottom half, and an accent line is drawn at that gap.
/// </summary>
public static class DragReorder
{
    private const string Format = "dragonfly-reorder";

    /// <summary>A themed "⠿" grip glyph that starts a reorder drag carrying <paramref name="payload"/>.</summary>
    public static TextBlock Handle(object payload)
    {
        var grip = new TextBlock
        {
            Text = "⠿",
            Foreground = Res("TextFaint"),
            FontSize = 15,
            Cursor = Cursors.SizeNS,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(2, 0, 6, 0),
            ToolTip = "Drag to reorder",
        };
        EnableSource(grip, payload);
        return grip;
    }

    private static void EnableSource(FrameworkElement handle, object payload)
    {
        bool pressed = false;
        Point start = default;
        handle.PreviewMouseLeftButtonDown += (_, e) => { pressed = true; start = e.GetPosition(handle); };
        handle.PreviewMouseLeftButtonUp += (_, _) => pressed = false;
        handle.MouseMove += (_, e) =>
        {
            if (!pressed || e.LeftButton != MouseButtonState.Pressed) return;
            var pt = e.GetPosition(handle);
            if (Math.Abs(pt.X - start.X) < 4 && Math.Abs(pt.Y - start.Y) < 4) return;
            pressed = false;
            DragDrop.DoDragDrop(handle, new DataObject(Format, payload), DragDropEffects.Move);
        };
    }

    /// <summary>
    /// Make <paramref name="table"/> a drop target over its whole area. <paramref name="rows"/> pairs
    /// each row's marker element (used to locate rows vertically) with its payload. On drop, the
    /// dragged payload, the row it lands on, and whether to insert <em>after</em> it are reported.
    /// </summary>
    public static void AttachTable(Grid table, List<(FrameworkElement Marker, object Payload)> rows, Action<object, object, bool> onDrop)
    {
        // A Grid with no background ignores drags over empty gaps — Transparent makes it all hittable.
        table.Background ??= Brushes.Transparent;

        DropLineAdorner? line = null;
        AdornerLayer? layer = null;

        void ShowLine(double y)
        {
            layer ??= AdornerLayer.GetAdornerLayer(table);
            if (layer == null) return;
            if (line == null) { line = new DropLineAdorner(table); layer.Add(line); }
            line.SetY(y);
        }
        void HideLine()
        {
            if (line != null && layer != null) { layer.Remove(line); line = null; }
        }

        table.AllowDrop = true;
        table.DragOver += (_, e) =>
        {
            if (!e.Data.GetDataPresent(Format)) { e.Effects = DragDropEffects.None; HideLine(); return; }
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            var loc = Locate(rows, table, e.GetPosition(table).Y);
            if (loc.HasValue) ShowLine(loc.Value.LineY);
        };
        table.DragLeave += (_, _) => HideLine();
        table.Drop += (_, e) =>
        {
            HideLine();
            if (!e.Data.GetDataPresent(Format)) return;
            var dragged = e.Data.GetData(Format)!;
            var loc = Locate(rows, table, e.GetPosition(table).Y);
            if (loc.HasValue && !ReferenceEquals(dragged, loc.Value.Target))
                onDrop(dragged, loc.Value.Target, loc.Value.After);
            e.Handled = true;
        };
    }

    /// <summary>
    /// Which row a pointer Y lands on, whether it's the bottom half, and the drop-line Y. The line
    /// sits at the insertion <em>gap</em> — centered between adjacent rows — so a single insert point
    /// always maps to one position (the bottom half of a row and the top half of the next agree).
    /// </summary>
    private static (object Target, bool After, double LineY)? Locate(
        List<(FrameworkElement Marker, object Payload)> rows, Grid table, double y)
    {
        if (rows.Count == 0) return null;

        var tops = new double[rows.Count];
        var bottoms = new double[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            tops[i] = rows[i].Marker.TransformToAncestor(table).Transform(new Point(0, 0)).Y;
            bottoms[i] = tops[i] + rows[i].Marker.ActualHeight;
        }

        // The gap the pointer indicates: 0 = above row 0, n = below the last row.
        int gap = rows.Count;
        int rowIndex = rows.Count - 1;
        bool after = true;
        for (int i = 0; i < rows.Count; i++)
        {
            if (y <= bottoms[i])
            {
                rowIndex = i;
                after = y >= (tops[i] + bottoms[i]) / 2;
                gap = after ? i + 1 : i;
                break;
            }
        }

        double lineY = gap <= 0 ? tops[0]
            : gap >= rows.Count ? bottoms[^1]
            : (bottoms[gap - 1] + tops[gap]) / 2;

        return (rows[rowIndex].Payload, after, lineY);
    }

    /// <summary>
    /// Move <paramref name="dragged"/> before/after <paramref name="target"/> within
    /// <paramref name="all"/>, then renumber every item's sort order 0..n via <paramref name="setOrder"/>.
    /// </summary>
    public static void Reorder<T>(IList<T> all, T dragged, T target, bool after, Action<T, int> setOrder)
    {
        int from = all.IndexOf(dragged);
        if (from < 0 || EqualityComparer<T>.Default.Equals(dragged, target)) return;
        all.RemoveAt(from);
        int to = all.IndexOf(target);
        if (to < 0) { all.Insert(from, dragged); return; }
        int insert = Math.Clamp(after ? to + 1 : to, 0, all.Count);
        all.Insert(insert, dragged);
        for (int i = 0; i < all.Count; i++) setOrder(all[i], i);
    }

    /// <summary>A thin accent line drawn across the table at a drop boundary.</summary>
    private sealed class DropLineAdorner : Adorner
    {
        private double _y;
        private readonly Pen _pen;
        private readonly Brush _dot;

        public DropLineAdorner(UIElement adorned) : base(adorned)
        {
            IsHitTestVisible = false;
            var accent = Res("Accent");
            _pen = new Pen(accent, 2);
            _pen.Freeze();
            _dot = accent;
        }

        public void SetY(double y) { _y = y; InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ((FrameworkElement)AdornedElement).ActualWidth;
            dc.DrawLine(_pen, new Point(2, _y), new Point(w - 2, _y));
            dc.DrawEllipse(_dot, null, new Point(2, _y), 3, 3);
            dc.DrawEllipse(_dot, null, new Point(w - 2, _y), 3, 3);
        }
    }
}
