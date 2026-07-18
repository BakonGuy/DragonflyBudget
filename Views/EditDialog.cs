using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>Code-built modal form with consistent dark styling. Returns true on Save.</summary>
public class EditDialog : Window
{
    private readonly Grid _fields = new();
    private readonly StackPanel _leftActions = new() { Orientation = Orientation.Horizontal };
    public bool DeleteRequested { get; private set; }
    private Func<bool>? _validate;

    public EditDialog(string title, Window owner)
    {
        Title = title;
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.Height;
        Width = 560;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        Background = Res("Panel");

        _fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        _fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var root = new DockPanel { Margin = new Thickness(24, 20, 24, 20) };

        var head = new TextBlock { Text = title, Style = St("H2"), Margin = new Thickness(0, 0, 0, 18) };
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        var actions = new Grid { Margin = new Thickness(0, 22, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.Children.Add(_leftActions);
        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Btn("Cancel", "Btn", (_, _) => { DialogResult = false; });
        var save = Btn("Save", "BtnPrimary", (_, _) =>
        {
            if (_validate != null && !_validate()) return;
            DialogResult = true;
        });
        cancel.Margin = new Thickness(0, 0, 8, 0);
        rightBtns.Children.Add(cancel);
        rightBtns.Children.Add(save);
        Grid.SetColumn(rightBtns, 1);
        actions.Children.Add(rightBtns);
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);

        root.Children.Add(_fields);
        Content = root;

        SourceInitialized += (_, _) => NativeTheme.ApplyDark(this);
    }

    public void OnValidate(Func<bool> validate) => _validate = validate;

    public void EnableDelete(Action onConfirm)
    {
        bool armed = false;
        var btn = Btn("Delete", "BtnGhost", (_, _) => { });
        btn.Foreground = Res("Bad");
        Button real = btn;
        real.Click += (_, _) =>
        {
            if (!armed) { armed = true; real.Content = "Really delete?"; return; }
            DeleteRequested = true;
            onConfirm();
            DialogResult = false;
        };
        _leftActions.Children.Add(real);
    }

    private int _row;

    /// <summary>Add a field spanning full width, or half (col 0/2) when half=true and paired.</summary>
    public void Add(string label, FrameworkElement control, bool full = true, bool rightColumn = false)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        stack.Children.Add(new TextBlock { Text = label, Style = St("FieldLabel") });
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        stack.Children.Add(control);

        if (full)
        {
            Grid.SetColumnSpan(stack, 3);
            Grid.SetRow(stack, _row);
            _fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _fields.Children.Add(stack);
            _row++;
        }
        else
        {
            Grid.SetColumn(stack, rightColumn ? 2 : 0);
            Grid.SetRow(stack, _row);
            if (!rightColumn) _fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _fields.Children.Add(stack);
            if (rightColumn) _row++;
        }
    }

    public void AddHint(string text)
    {
        var tb = new TextBlock { Text = text, Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetColumnSpan(tb, 3);
        Grid.SetRow(tb, _row);
        _fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _fields.Children.Add(tb);
        _row++;
    }

    // ── field factories ──
    public static TextBox Text(string value, string placeholder = "")
    {
        var tb = new TextBox { Text = value, Style = St("Input"), Tag = placeholder };
        return tb;
    }

    public static TextBox Notes(string value)
    {
        var tb = new TextBox { Text = value, Style = St("Input"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        return tb;
    }

    public static ComboBox Combo(IEnumerable<string> items, string selected)
    {
        var c = new ComboBox { Style = St("Combo") };
        foreach (var i in items) c.Items.Add(i);
        c.SelectedItem = selected;
        if (c.SelectedItem == null && c.Items.Count > 0) c.SelectedIndex = 0;
        return c;
    }
}
