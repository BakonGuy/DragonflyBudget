using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dragonfly.Models;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>
/// Drag-resizable column dividers for a code-built table Grid, with the layout saved per screen.
///
/// Two rules drive the whole thing:
///
/// 1. Dragging a divider moves that divider and nothing else. A drag only ever writes the two
///    columns touching it, from a snapshot taken on mouse-down — never from the running layout,
///    which is a pass behind and makes the divider drift away from the cursor.
/// 2. The table is exactly as wide as the space it is given, always. Column widths are held as
///    ratios of that space, so nothing can be pushed off the edge, a resized window recomputes
///    every column, and a saved layout means the same thing at any window size.
///
/// The table is expected to have exactly one star column (the flex column: it takes whatever is
/// left over) and to leave it unmanaged. Auto columns are sized by their own content and are also
/// unmanaged — a divider steps over them to reach the nearest column it can resize, which makes an
/// Auto column between two dividers a region that splits itself.
///
/// Apply after all rows are added. Double-click a divider to reset the screen to its defaults.
/// </summary>
public static class ColumnResize
{
    /// <summary>The only limit on a drag: how close in pixels one divider may come to the next.</summary>
    private const double MinGap = 24;

    /// <param name="cols">Resizable columns (fixed widths); saved and restored together.</param>
    /// <param name="handleCols">Which columns get a divider on their left edge.</param>
    public static void Apply(Grid table, string screen, AppSettings settings, Action save,
        IReadOnlyList<int> cols, IReadOnlyList<int> handleCols)
        => Apply(new[] { table }, screen, settings, save, cols, handleCols);

    /// <summary>
    /// Several stacked tables of the same column shape, resized together under one saved layout.
    /// Dragging a divider in any of them moves that column in all of them — adjacent tables with the
    /// same columns have to stay aligned, and a per-table key would let them drift apart. The tables
    /// must share the same column model; the first one defines it.
    /// </summary>
    public static void Apply(IReadOnlyList<Grid> tables, string screen, AppSettings settings, Action save,
        IReadOnlyList<int> cols, IReadOnlyList<int> handleCols)
    {
        if (tables.Count == 0 || cols.Count == 0) return;
        var defs = tables[0].ColumnDefinitions;

        var defaults = cols.Select(c => defs[c].Width).ToList();
        int flex = Enumerable.Range(0, defs.Count).FirstOrDefault(i => defs[i].Width.IsStar, -1);

        // A saved layout only means anything against the column model it came from, so the key
        // carries that model's shape. Change the columns or the dividers and the old entry is simply
        // never matched again — nothing stale is resurrected and there is nothing to migrate.
        string key = $"{screen}|{string.Join("", defaults.Select(d => d.IsStar ? 'S' : d.IsAuto ? 'A' : 'P'))}"
                   + $"|f{flex}|h{string.Join(",", handleCols)}|r";

        // Each managed column's share of the resizable space, as a fraction. Null until the table has
        // been measured at least once, since the declared pixel defaults can't be turned into shares
        // before we know how much space there is to share.
        List<double>? ratios = null;
        if (settings.ColumnWidths.TryGetValue(key, out var saved) && saved.Count == cols.Count
            && saved.All(r => r > 0 && r < 1) && saved.Sum() < 1)
        {
            ratios = saved.ToList();
        }

        foreach (var t in tables)
        {
            var tdefs = t.ColumnDefinitions;
            int rows = Math.Max(1, t.RowDefinitions.Count);
            foreach (int ci in handleCols)
            {
                if (ci <= 0 || ci >= tdefs.Count) continue;

                // A divider resizes the nearest resizable column on each side, stepping over any Auto
                // column in between. Those are sized by their own content and are not ours to move.
                int right = Enumerable.Range(ci, tdefs.Count - ci).FirstOrDefault(i => cols.Contains(i), -1);
                int left = Enumerable.Range(0, ci).Reverse().FirstOrDefault(i => cols.Contains(i) || i == flex, -1);
                if (right < 0 || left < 0) continue;

                var dragged = t;
                AddDivider(t, tdefs, ci, left, right, flex, rows, persist: () =>
                {
                    // Measured from the table actually dragged, then pushed to all of them, so the
                    // others catch up the moment the drag ends rather than at the next rebuild.
                    double space = Resizable(dragged);
                    if (space <= 0) return;
                    ratios = cols.Select(c => dragged.ColumnDefinitions[c].ActualWidth / space).ToList();
                    settings.ColumnWidths[key] = ratios;
                    LayoutAll();
                    save();
                }, reset: () =>
                {
                    ratios = null;
                    settings.ColumnWidths.Remove(key);
                    LayoutAll();
                    save();
                });
            }
            t.SizeChanged += (_, e) => { if (e.WidthChanged) Layout(t); };
        }

        LayoutAll();

        void LayoutAll() { foreach (var t in tables) Layout(t); }

        // Space the managed columns and the flex column share: everything except the Auto columns,
        // which have already measured themselves against their content.
        double Resizable(Grid t)
        {
            var tdefs = t.ColumnDefinitions;
            double avail = t.ActualWidth;
            if (avail <= 0) return 0;
            for (int i = 0; i < tdefs.Count; i++)
                if (i != flex && !cols.Contains(i)) avail -= tdefs[i].ActualWidth;
            return avail;
        }

        // Turn the ratios into pixel widths for the space currently available. Everything the user
        // set is preserved as a proportion, so this both restores their layout and keeps the table
        // inside its bounds — the widths are derived from the available width, never independent
        // of it, which is what makes overflow impossible rather than merely unlikely.
        void Layout(Grid t)
        {
            var tdefs = t.ColumnDefinitions;
            double space = Resizable(t);
            if (space <= 0) return;

            ratios ??= defaults.Select(d => d.Value / space).ToList();

            double flexMin = flex >= 0 ? Math.Max(tdefs[flex].MinWidth, MinGap) : 0;
            double budget = Math.Max(MinGap * cols.Count, space - flexMin);

            var px = ratios.Select(r => Math.Max(MinGap, r * space)).ToList();
            double sum = px.Sum();
            if (sum > budget)
            {
                double f = budget / sum;
                px = px.Select(w => Math.Max(MinGap, w * f)).ToList();
            }

            for (int k = 0; k < cols.Count; k++)
                tdefs[cols[k]].Width = new GridLength(px[k]);
        }
    }

    /// <param name="ci">Column whose left edge the divider is drawn on.</param>
    /// <param name="left">Column the drag shrinks/grows on the left — may be the flex column.</param>
    /// <param name="right">Column the drag grows/shrinks on the right.</param>
    private static void AddDivider(Grid table, ColumnDefinitionCollection defs, int ci, int left, int right,
        int flex, int rows, Action persist, Action reset)
    {
        var line = new Border
        {
            Width = 1.5,
            Background = Res("BorderSoft"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 6, 0, 6),
        };
        var handle = new Border
        {
            Width = 11,
            Background = Brushes.Transparent,   // grab area, straddling the boundary
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(-5, 0, 0, 0),
            Cursor = Cursors.SizeWE,
            Child = line,
            ToolTip = "Drag to resize · double-click to reset",
        };
        Grid.SetColumn(handle, ci);
        Grid.SetRow(handle, 0);
        Grid.SetRowSpan(handle, rows);
        Panel.SetZIndex(handle, 60);   // above the cell content it overlaps

        bool flexLeft = left == flex;
        bool dragging = false;
        double startX = 0, startLeft = 0, startRight = 0;

        handle.MouseEnter += (_, _) => line.Background = Res("Accent");
        handle.MouseLeave += (_, _) => { if (!dragging) line.Background = Res("BorderSoft"); };

        handle.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                reset();
                e.Handled = true;
                return;
            }
            // Snapshot: every frame of this drag is measured against these, so the divider tracks the
            // cursor exactly instead of accumulating a pass-behind error.
            dragging = true;
            startX = e.GetPosition(table).X;
            startLeft = defs[left].ActualWidth;
            startRight = defs[right].ActualWidth;
            line.Background = Res("Accent");
            handle.CaptureMouse();
            e.Handled = true;
        };

        handle.MouseMove += (_, e) =>
        {
            if (!dragging) return;
            double dx = e.GetPosition(table).X - startX;

            // Both branches keep the total width of the columns involved exactly as it was, so a
            // drag can never make the table wider than the space it has.
            if (flexLeft)
            {
                // Shrink the fixed column and the flex column grows into the gap by itself; the
                // columns further right are untouched, so no other divider moves.
                double flexMin = Math.Max(defs[left].MinWidth, MinGap);
                dx = Math.Clamp(dx, flexMin - startLeft, startRight - MinGap);
                defs[right].Width = new GridLength(startRight - dx);
            }
            else
            {
                // Two fixed columns trade width: their total is unchanged, so the flex column and
                // every other divider stay exactly where they are.
                dx = Math.Clamp(dx, MinGap - startLeft, startRight - MinGap);
                defs[left].Width = new GridLength(startLeft + dx);
                defs[right].Width = new GridLength(startRight - dx);
            }
        };

        handle.MouseLeftButtonUp += (_, e) =>
        {
            if (!dragging) return;
            dragging = false;
            handle.ReleaseMouseCapture();
            if (!handle.IsMouseOver) line.Background = Res("BorderSoft");
            persist();
            e.Handled = true;
        };

        table.Children.Add(handle);
    }
}
