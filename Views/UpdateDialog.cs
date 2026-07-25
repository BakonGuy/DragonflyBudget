using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dragonfly.Services;
using MahApps.Metro.IconPacks;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

public class UpdateDialog : Window
{
    private readonly UpdateInfo _info;
    private readonly UpdateService _updater;
    private readonly StackPanel _root;
    private readonly Button _updateBtn;
    private readonly Button _skipBtn;
    private readonly Button _laterBtn;
    private readonly Button _viewBtn;
    private readonly StackPanel _progressPanel;
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _statusText;
    private CancellationTokenSource? _cts;

    private UpdateDialog(Window owner, UpdateInfo info, UpdateService updater)
    {
        _info = info;
        _updater = updater;

        Title = "Update Available";
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.Height;
        Width = 520;
        MinHeight = 300;
        MaxHeight = 600;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        Background = Res("Panel");

        _root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        // Header
        var headRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        headRow.Children.Add(Icon(PackIconRemixIconKind.DownloadFill, 22, Res("Accent")));
        headRow.Children.Add(new TextBlock
        {
            Text = $"Dragonfly {info.Version} is available",
            Style = St("H2"),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        _root.Children.Add(headRow);

        _root.Children.Add(new TextBlock
        {
            Text = $"Current: v{AppInfo.VersionString}  →  {info.Title}",
            Style = St("Sub"),
            Margin = new Thickness(0, 0, 0, 12),
        });

        // Release notes
        if (!string.IsNullOrWhiteSpace(info.Notes))
        {
            var notesBox = new Border
            {
                Style = St("Card"),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12),
                MaxHeight = 200,
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new TextBlock
                    {
                        Text = info.Notes,
                        Foreground = Res("TextDim"),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13,
                        LineHeight = 19,
                    }
                }
            };
            _root.Children.Add(notesBox);
        }

        // Asset info
        if (!string.IsNullOrEmpty(info.AssetName))
        {
            _root.Children.Add(new TextBlock
            {
                Text = $"{info.AssetName}  ({FormatSize(info.AssetSize)})",
                Style = St("Faint"),
                Margin = new Thickness(0, 0, 0, 16),
            });
        }

        // Progress panel (hidden initially)
        _progressPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed };
        _progressBar = new ProgressBar
        {
            Height = 6,
            Minimum = 0,
            Maximum = 1,
            Foreground = Res("AccentStrong"),
            Background = Res("Panel2"),
            BorderThickness = new Thickness(0),
        };
        _progressPanel.Children.Add(_progressBar);
        _statusText = new TextBlock
        {
            Style = St("Faint"),
            Margin = new Thickness(0, 6, 0, 0),
        };
        _progressPanel.Children.Add(_statusText);
        _root.Children.Add(_progressPanel);

        // Buttons
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        _viewBtn = Btn("View release", "Btn", (_, _) => OpenReleaseUrl());
        _viewBtn.Margin = new Thickness(0, 0, 8, 0);
        _viewBtn.Visibility = string.IsNullOrEmpty(info.DownloadUrl) ? Visibility.Visible : Visibility.Collapsed;

        _laterBtn = Btn("Later", "Btn", (_, _) => { DialogResult = false; });
        _laterBtn.Margin = new Thickness(0, 0, 8, 0);

        _skipBtn = Btn("Skip this version", "Btn", (_, _) => SkipVersion());
        _skipBtn.Margin = new Thickness(0, 0, 8, 0);

        _updateBtn = Btn("Update now", "BtnPrimary", (_, _) => StartDownload());
        _updateBtn.IsEnabled = !string.IsNullOrEmpty(info.DownloadUrl);
        if (string.IsNullOrEmpty(info.DownloadUrl))
            _updateBtn.ToolTip = "No installer asset available for this release.";

        btnRow.Children.Add(_viewBtn);
        btnRow.Children.Add(_skipBtn);
        btnRow.Children.Add(_laterBtn);
        btnRow.Children.Add(_updateBtn);
        _root.Children.Add(btnRow);

        Content = _root;
        SourceInitialized += (_, _) => NativeTheme.ApplyDark(this);
        Closed += (_, _) => _cts?.Cancel();
    }

    private void OpenReleaseUrl()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_info.HtmlUrl) { UseShellExecute = true }); }
        catch { }
    }

    private void SkipVersion()
    {
        var s = App.State.Settings;
        s.SkippedUpdateVersion = _info.Tag;
        App.State.Save();
        DialogResult = false;
    }

    private async void StartDownload()
    {
        _cts = new CancellationTokenSource();
        SetDownloadingState(true);

        var progress = new Progress<double>(p =>
        {
            Dispatcher.Invoke(() =>
            {
                _progressBar.Value = p;
                _statusText.Text = $"{(int)(p * 100)}%";
            });
        });

        var path = await _updater.DownloadAsync(_info, progress, _cts.Token);

        if (path != null)
        {
            _statusText.Text = "Installing...";
            _updater.LaunchInstallerAndExit(path);
        }
        else if (!_cts.Token.IsCancellationRequested)
        {
            SetDownloadingState(false);
            _statusText.Foreground = Res("Bad");
            _statusText.Text = "Download failed. ";
            var retryLink = new Button
            {
                Content = "Try again",
                Style = St("BtnGhost"),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 0, 0),
            };
            retryLink.Click += (_, _) => StartDownload();
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            var viewRelease = new Button
            {
                Content = "View release",
                Style = St("BtnGhost"),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(0, 0, 4, 0),
            };
            viewRelease.Click += (_, _) => OpenReleaseUrl();
            row.Children.Add(_statusText);
            row.Children.Add(viewRelease);
            row.Children.Add(retryLink);
            _progressPanel.Children.Clear();
            _progressPanel.Children.Add(row);
        }
    }

    private void SetDownloadingState(bool downloading)
    {
        _progressPanel.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        _updateBtn.IsEnabled = !downloading;
        _skipBtn.IsEnabled = !downloading;
        _laterBtn.IsEnabled = !downloading;
        _viewBtn.IsEnabled = !downloading;
        _progressBar.Value = 0;

        if (downloading)
        {
            _statusText.Foreground = Res("TextFaint");
            _statusText.Text = "Downloading...";
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB",
    };

    public static void Show(Window owner, UpdateInfo info, UpdateService updater)
    {
        new UpdateDialog(owner, info, updater).ShowDialog();
    }
}
