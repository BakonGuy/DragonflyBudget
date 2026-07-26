using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>
/// Day-of-month selector: type a number (1–31), or click the arrow to pick from a grid. Styled to
/// match the app's combo boxes — a single bordered box with the arrow tucked inside on the right.
/// </summary>
public class DayPicker : Grid
{
    private readonly TextBox _box = new();
    private readonly Border _outer;
    private readonly Popup _popup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Bottom };

    public DayPicker(int day = 1)
    {
        _box.Text = Math.Clamp(day, 1, 31).ToString();
        _box.MaxLength = 2;
        _box.Background = Brushes.Transparent;
        _box.BorderThickness = new Thickness(0);
        _box.Foreground = Res("Text");
        _box.CaretBrush = Res("Text");
        _box.SelectionBrush = Res("Accent");
        _box.FontSize = 14;
        _box.VerticalContentAlignment = VerticalAlignment.Center;
        _box.Padding = new Thickness(8, 7, 4, 7);
        _box.PreviewTextInput += (_, e) => e.Handled = !int.TryParse(e.Text, out int _);
        _box.LostFocus += (_, _) => _box.Text = Day.ToString();

        var toggle = new ToggleButton { Style = St("IconToggle"), Width = 30 };
        // StaysOpen=false closes the popup on any outside click — including a click on the toggle
        // itself. Guard against that click immediately re-opening it (the classic reopen flicker).
        var closedAt = DateTime.MinValue;
        toggle.Checked += (_, _) =>
        {
            if ((DateTime.Now - closedAt).TotalMilliseconds < 250) { toggle.IsChecked = false; return; }
            _popup.IsOpen = true;
        };
        toggle.Unchecked += (_, _) => _popup.IsOpen = false;
        _popup.Closed += (_, _) => { closedAt = DateTime.Now; toggle.IsChecked = false; };

        var inner = new Grid();
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetColumn(_box, 0); inner.Children.Add(_box);
        SetColumn(toggle, 1); inner.Children.Add(toggle);

        _outer = new Border
        {
            Background = Res("Bg"),
            BorderBrush = Res("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = inner,
        };
        // Focus highlight to match the other inputs.
        _box.GotKeyboardFocus += (_, _) => _outer.BorderBrush = Res("AccentStrong");
        _box.LostKeyboardFocus += (_, _) => _outer.BorderBrush = Res("BorderBrush");

        _popup.PlacementTarget = _outer;
        _popup.Child = BuildGrid();

        Children.Add(_outer);
        Children.Add(_popup);
    }

    /// <summary>Selected day, clamped to 1–31.</summary>
    public int Day => int.TryParse(_box.Text, out var d) ? Math.Clamp(d, 1, 31) : 1;

    private Border BuildGrid()
    {
        var wrap = new UniformGrid { Columns = 7, Width = 250 };
        for (int d = 1; d <= 31; d++)
        {
            int day = d;
            var b = new Button
            {
                Content = day.ToString(),
                Style = St("BtnGhost"),
                Padding = new Thickness(2, 4, 2, 4),
                Margin = new Thickness(1),
                MinWidth = 32,
            };
            b.Click += (_, _) => { _box.Text = day.ToString(); _popup.IsOpen = false; };
            wrap.Children.Add(b);
        }
        var popupRoot = new Border
        {
            Background = Res("Panel"),
            BorderBrush = Res("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 4, 0, 0),
            Child = wrap,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 3, Opacity = 0.5 },
        };
        // A Popup is its own visual tree, so it can't inherit the window's text colour.
        System.Windows.Documents.TextElement.SetForeground(popupRoot, Res("Text"));
        return popupRoot;
    }
}
