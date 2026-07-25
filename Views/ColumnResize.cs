using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dragonfly.Models;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>
/// Makes selected columns of a code-built table Grid drag-resizable, persisting the widths per
/// screen. A visible handle sits on each resizable column's left edge and moves the boundary,
/// directly adjusting the column widths (a leading star column absorbs the slack). Apply after all
/// rows are added. Double-click a handle to reset the screen's columns to defaults.
/// </summary>
public static class ColumnResize
{
    private const double Min = 50;

    /// <param name="resizableCols">Fixed-pixel columns whose widths are saved/restored (each &gt; 0).</param>
    /// <param name="handleCols">
    /// Which of those columns get a drag handle on their left edge. Defaults to all of
    /// <paramref name="resizableCols"/>. Omit a boundary (e.g. between a left- and right-aligned pair)
    /// where a visible divider isn't wanted — the gap is then just computed layout space.
    /// </param>
    public static void Apply(Grid table, string screen, IReadOnlyList<int> resizableCols, AppSettings settings, Action save,
        IReadOnlyList<int>? handleCols = null)
    {
        var defs = table.ColumnDefinitions;
        handleCols ??= resizableCols;

        // Restore saved widths (only when the shape still matches what we saved).
        if (settings.ColumnWidths.TryGetValue(screen, out var saved) && saved.Count == resizableCols.Count)
        {
            for (int k = 0; k < resizableCols.Count; k++)
                if (saved[k] >= Min && saved[k] <= 1400)
                    defs[resizableCols[k]].Width = new GridLength(saved[k]);
        }

        int rows = Math.Max(1, table.RowDefinitions.Count);
        foreach (int ci in handleCols)
        {
            if (ci <= 0) continue;   // needs a column to its left to resize against

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
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(-5, 0, 0, 0),   // straddle the column boundary
                Cursor = Cursors.SizeWE,
                Child = line,
                ToolTip = "Drag to resize · double-click to reset",
            };
            Grid.SetColumn(handle, ci);
            Grid.SetRow(handle, 0);
            Grid.SetRowSpan(handle, rows);
            Panel.SetZIndex(handle, 60);

            bool dragging = false;
            double lastX = 0;

            handle.MouseEnter += (_, _) => line.Background = Res("Accent");
            handle.MouseLeave += (_, _) => { if (!dragging) line.Background = Res("BorderSoft"); };

            handle.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 2)   // reset to defaults
                {
                    settings.ColumnWidths.Remove(screen);
                    save();
                    e.Handled = true;
                    return;
                }
                dragging = true;
                lastX = e.GetPosition(table).X;
                handle.CaptureMouse();
                e.Handled = true;
            };
            handle.MouseMove += (_, e) =>
            {
                if (!dragging) return;
                double x = e.GetPosition(table).X;
                Adjust(defs, ci, x - lastX);
                lastX = x;
            };
            handle.MouseLeftButtonUp += (_, e) =>
            {
                if (!dragging) return;
                dragging = false;
                handle.ReleaseMouseCapture();
                if (!handle.IsMouseOver) line.Background = Res("BorderSoft");
                settings.ColumnWidths[screen] = resizableCols.Select(c => defs[c].ActualWidth).ToList();
                save();
                e.Handled = true;
            };

            table.Children.Add(handle);
        }
    }

    /// <summary>Move the boundary at the left edge of column <paramref name="ci"/> by <paramref name="dx"/>.</summary>
    private static void Adjust(ColumnDefinitionCollection defs, int ci, double dx)
    {
        double curCi = defs[ci].ActualWidth;
        if (defs[ci - 1].Width.IsStar)
        {
            // Star neighbour absorbs the slack: just resize this column.
            double n = curCi - dx;
            if (n >= Min) defs[ci].Width = new GridLength(n);
        }
        else
        {
            double curPrev = defs[ci - 1].ActualWidth;
            double np = curPrev + dx, nc = curCi - dx;
            if (np >= Min && nc >= Min)
            {
                defs[ci - 1].Width = new GridLength(np);
                defs[ci].Width = new GridLength(nc);
            }
        }
    }
}
