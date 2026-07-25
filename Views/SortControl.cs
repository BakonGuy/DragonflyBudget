using System.Windows;
using System.Windows.Controls;
using Dragonfly.Models;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>
/// A compact "Sort: [field] [↑/↓]" control bound to a <see cref="SortPref"/>. Picking a field or
/// flipping the direction mutates the pref in place and calls <c>onChanged</c> (save + refresh).
/// </summary>
public class SortControl : StackPanel
{
    public SortControl(SortPref pref, (string Key, string Label)[] options, Action onChanged)
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;

        Children.Add(new TextBlock
        {
            Text = "Sort",
            Foreground = Res("TextFaint"),
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        var combo = new ComboBox { Style = St("Combo"), MinWidth = 128, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (_, label) in options) combo.Items.Add(label);
        int current = Array.FindIndex(options, o => o.Key == pref.Key);
        combo.SelectedIndex = current < 0 ? 0 : current;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0) return;
            pref.Key = options[combo.SelectedIndex].Key;
            onChanged();
        };
        Children.Add(combo);

        var dir = Btn(pref.Descending ? "↓" : "↑", "BtnGhost", (_, _) => { });
        dir.MinWidth = 34;
        dir.VerticalAlignment = VerticalAlignment.Center;
        dir.Margin = new Thickness(4, 0, 0, 0);
        dir.ToolTip = "Toggle sort direction";
        dir.Click += (_, _) =>
        {
            pref.Descending = !pref.Descending;
            onChanged();
        };
        Children.Add(dir);
    }
}
