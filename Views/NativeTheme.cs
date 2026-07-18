using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Dragonfly.Views;

/// <summary>Makes the OS window title bar dark to match the app's dark theme.</summary>
public static class NativeTheme
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_BORDER_COLOR = 34;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Call after the window handle exists (e.g. in OnSourceInitialized).</summary>
    public static void ApplyDark(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int useDark = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

        // Match the caption + border to the app background for a seamless look.
        int caption = ToColorRef((Color)ColorConverter.ConvertFromString("#14121C"));
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
        int border = ToColorRef((Color)ColorConverter.ConvertFromString("#2B2740"));
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }

    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
}
