using System.Windows;
using System.Windows.Controls;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>A small themed yes/no confirmation modal. Returns true when the user confirms.</summary>
public class ConfirmDialog : Window
{
    private ConfirmDialog(Window owner, string title, string message, string confirmText, bool danger)
    {
        Title = title;
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.Height;
        Width = 460;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        Background = Res("Panel");
        Foreground = Res("Text");

        var root = new DockPanel { Margin = new Thickness(24, 20, 24, 20) };

        var head = new TextBlock { Text = title, Style = St("H2"), Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0),
        };
        var cancel = Btn("Cancel", "Btn", (_, _) => { DialogResult = false; });
        cancel.Margin = new Thickness(0, 0, 8, 0);
        var confirm = Btn(confirmText, "BtnPrimary", (_, _) => { DialogResult = true; });
        if (danger) confirm.Background = Res("Bad");
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);

        root.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Res("TextDim"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            LineHeight = 20,
        });

        Content = root;
        SourceInitialized += (_, _) => NativeTheme.Apply(this);
    }

    /// <summary>Show a confirmation and return whether the user confirmed.</summary>
    public static bool Ask(Window owner, string title, string message, string confirmText = "Confirm", bool danger = false)
        => new ConfirmDialog(owner, title, message, confirmText, danger).ShowDialog() == true;
}
