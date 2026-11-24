using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SmartCalc.App;

/// <summary>Cho thanh tiêu đề hệ thống đổi màu theo theme (Windows 10 20H1+ / Windows 11).</summary>
internal static class WindowInterop
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void SetDarkTitleBar(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;          // chưa tạo handle: gọi lại ở SourceInitialized
        int value = dark ? 1 : 0;
        try
        {
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }
}
