using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>
/// Shared row affordances for the code-built tables. Every list screen is a Grid of cells rather
/// than a real ItemsControl, so "which row am I on?" and "let me click the row to edit it" have to
/// be added by hand — this is that, in one place, so the screens behave identically.
/// </summary>
public sealed class RowHover
{
    private readonly Grid _table;
    private readonly int _columnSpan;
    private readonly Brush _brush;
    private readonly List<Border> _rows = new();

    /// <param name="columnSpan">How many columns a row spans — the table's column count.</param>
    public RowHover(Grid table, int columnSpan)
    {
        _table = table;
        _columnSpan = columnSpan;
        // Light enough to sit over a status wash (overdue red, cleared dimming) without hiding it.
        var b = new SolidColorBrush(((SolidColorBrush)Res("Text")).Color) { Opacity = 0.06 };
        b.Freeze();
        _brush = b;
    }

    /// <summary>
    /// Add the highlight layer for a grid row. Call this <em>before</em> placing that row's cells:
    /// a Grid paints children in the order they were added, so the overlay has to go in first to
    /// end up behind the content.
    /// </summary>
    public void Add(int row)
    {
        // Transparent rather than null so the full row width is hit-testable, and not itself
        // hit-test-visible so it never swallows a click meant for a cell.
        var hover = new Border { Background = Brushes.Transparent, IsHitTestVisible = false };
        Grid.SetRow(hover, row);
        Grid.SetColumnSpan(hover, _columnSpan);
        _table.Children.Add(hover);
        _rows.Add(hover);
    }

    /// <summary>Wire up the tracking. Call once, after all rows are built.</summary>
    public void Attach()
    {
        if (_rows.Count == 0) return;

        // Driven from one handler on the table, not per-cell MouseEnter/Leave: the cells don't tile
        // the row (there are margins between them), so per-cell events flicker as the pointer
        // crosses a gap. Mapping the pointer's Y to a row is stable across the whole width.
        _table.Background ??= Brushes.Transparent;   // so the gaps register the pointer too
        _table.MouseMove += (_, e) =>
        {
            double y = e.GetPosition(_table).Y;
            foreach (var h in _rows)
            {
                double top = h.TranslatePoint(new Point(0, 0), _table).Y;
                Set(h, y >= top && y < top + h.ActualHeight);
            }
        };
        _table.MouseLeave += (_, _) =>
        {
            foreach (var h in _rows) Set(h, false);
        };
    }

    private void Set(Border h, bool on)
    {
        var want = on ? _brush : Brushes.Transparent;
        if (!ReferenceEquals(h.Background, want)) h.Background = want;
    }

    /// <summary>
    /// Wrap a row's name cell so clicking it runs <paramref name="onClick"/> — a second way into the
    /// edit dialog, so a long list doesn't force a trip to the button on the far right just to edit
    /// the thing you're already pointing at.
    /// </summary>
    public static Border ClickToEdit(UIElement child, Action onClick, string tooltip = "Click to edit")
    {
        // Background must be set (not null) or the gaps between text and badges won't take the click.
        var cell = new Border
        {
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = child,
            ToolTip = tooltip,
        };
        cell.MouseLeftButtonUp += (_, e) =>
        {
            // A drag-reorder also ends in a mouse-up over this cell; the grip marks those handled.
            if (e.Handled) return;
            onClick();
        };
        return cell;
    }
}
