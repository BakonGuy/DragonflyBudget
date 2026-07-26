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
        Foreground = Res("Text");

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

        SourceInitialized += (_, _) => NativeTheme.Apply(this);
        Loaded += (_, _) => _firstControl?.Focus();
    }

    private FrameworkElement? _firstControl;

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
    // A half-width left field is on the current row waiting for a right-column partner. If the next
    // element isn't that partner, we must advance past it so nothing overlaps it.
    private bool _leftHalfOpen;

    /// <summary>Add a field spanning full width, or half (col 0/2) when half=true and paired.</summary>
    public void Add(string label, FrameworkElement control, bool full = true, bool rightColumn = false)
        => AddTracked(label, control, full, rightColumn);

    /// <summary>Like <see cref="Add"/>, but returns the field container so it can be shown/hidden.</summary>
    public FrameworkElement AddTracked(string label, FrameworkElement control, bool full = true, bool rightColumn = false)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        stack.Children.Add(new TextBlock { Text = label, Style = St("FieldLabel") });
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        stack.Children.Add(control);
        _firstControl ??= control;

        if (full)
        {
            if (_leftHalfOpen) { _row++; _leftHalfOpen = false; }
            Grid.SetColumnSpan(stack, 3);
            Grid.SetRow(stack, _row);
            _fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _fields.Children.Add(stack);
            _row++;
        }
        else if (!rightColumn)
        {
            if (_leftHalfOpen) _row++;   // previous left field had no partner; move to a fresh row
            Grid.SetColumn(stack, 0);
            Grid.SetRow(stack, _row);
            _fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _fields.Children.Add(stack);
            _leftHalfOpen = true;
        }
        else
        {
            if (!_leftHalfOpen) _fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(stack, 2);
            Grid.SetRow(stack, _row);
            _fields.Children.Add(stack);
            _row++;
            _leftHalfOpen = false;
        }
        return stack;
    }

    /// <summary>Returns the hint so callers can hide it or rewrite its text as fields change.</summary>
    public TextBlock AddHint(string text)
    {
        if (_leftHalfOpen) { _row++; _leftHalfOpen = false; }
        var tb = new TextBlock { Text = text, Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetColumnSpan(tb, 3);
        Grid.SetRow(tb, _row);
        _fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _fields.Children.Add(tb);
        _row++;
        return tb;
    }

    /// <summary>A small section divider/header to group related fields.</summary>
    public void AddSection(string title)
    {
        var tb = new TextBlock
        {
            Text = title.ToUpper(),
            Foreground = Res("Accent"),
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, _row == 0 ? 0 : 8, 0, 10),
        };
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

    /// <summary>Editable combo: pick a suggestion or type a new value. Text holds the result.</summary>
    public static ComboBox EditableCombo(IEnumerable<string> suggestions, string current, bool sort = true)
    {
        var c = new ComboBox { Style = St("ComboEditable") };
        var items = suggestions.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
        if (sort) items = items.OrderBy(x => x);
        foreach (var s in items) c.Items.Add(s);
        c.Text = current;
        return c;
    }

    /// <summary>
    /// "Pay from" picker: your tracked accounts appear first under a header, then a separator, then
    /// generic/custom names. You can still type any value. <see cref="ComboBox.Text"/> holds the result.
    /// </summary>
    public static ComboBox AccountCombo(IEnumerable<string> tracked, IEnumerable<string> extras, string current)
    {
        var c = new ComboBox { Style = St("ComboEditable") };
        var trackedList = tracked.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var extraList = extras.Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !trackedList.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();

        if (trackedList.Count > 0)
        {
            c.Items.Add(Header("YOUR ACCOUNTS"));
            foreach (var s in trackedList) c.Items.Add(s);
            if (extraList.Count > 0) c.Items.Add(new Separator());
        }
        foreach (var s in extraList) c.Items.Add(s);

        c.Text = current;
        return c;
    }

    // A non-selectable heading row for grouped combos.
    private static ComboBoxItem Header(string text) => new()
    {
        Content = text,
        IsEnabled = false,
        Focusable = false,
        FontSize = 10.5,
        FontWeight = FontWeights.SemiBold,
        Foreground = Res("TextFaint"),
    };
}
