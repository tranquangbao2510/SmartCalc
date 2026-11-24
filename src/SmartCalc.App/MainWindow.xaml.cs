using System.Windows;
using System.Windows.Input;
using SmartCalc.App.ViewModels;

namespace SmartCalc.App;

/// <summary>
/// Code-behind chỉ lo phần thuần giao diện: chuyển phím bàn phím thành lệnh của VM,
/// clipboard, co giãn bố cục theo độ rộng cửa sổ, màu thanh tiêu đề.
/// </summary>
public partial class MainWindow : Window
{
    private const double WideThreshold = 860;     // >= : panel lịch sử luôn hiện bên phải
    private const double SidebarThreshold = 720;  // <  : sidebar tự thu gọn thành cột biểu tượng

    private readonly MainViewModel _vm;
    private bool? _wasWideForSidebar;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;

        SourceInitialized += (_, _) => WindowInterop.SetDarkTitleBar(this, _vm.IsDark);
        SizeChanged += (_, _) => UpdateLayoutMode();
        // Đổi chế độ: ô nhập của chế độ cũ bị ẩn nhưng vẫn giữ focus -> trả focus về cửa sổ để phím đến đúng bàn phím mới
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedMode)) Keyboard.Focus(this);
        };
        Closing += (_, _) =>
        {
            var bounds = WindowState == WindowState.Normal ? new Size(Width, Height) : RestoreBounds.Size;
            _vm.SaveWindowSize(bounds.Width, bounds.Height);
        };
    }

    private void UpdateLayoutMode()
    {
        _vm.IsWide = ActualWidth >= WideThreshold;
        if (_vm.IsWide) _vm.IsSidePanelOpen = false;

        // Chỉ tự thu/mở sidebar khi vượt ngưỡng, để người dùng vẫn bấm ☰ được
        bool wideForSidebar = ActualWidth >= SidebarThreshold;
        if (_wasWideForSidebar != wideForSidebar)
        {
            _vm.IsSidebarExpanded = wideForSidebar;
            _wasWideForSidebar = wideForSidebar;
        }
    }

    // Đang gõ trong ô nhập (Đổi đơn vị) thì để ô đó tự xử lý phím
    private static bool IsTypingInTextBox =>
        Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase { IsVisible: true };

    // ---------- Ký tự gõ: chuyển cho bàn phím của chế độ đang mở (Cơ bản/Khoa học hoặc Lập trình) ----------
    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        base.OnPreviewTextInput(e);
        if (IsTypingInTextBox || _vm.ActiveKeypad is not { } keypad) return;
        foreach (char c in e.Text)
            if (keypad.TypeChar(c)) e.Handled = true;
    }

    // ---------- Phím điều khiển và phím tắt ----------
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        var mods = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Alt+1..6: đổi chế độ (ở mọi nơi, kể cả khi đang gõ trong ô nhập)
        if (mods == ModifierKeys.Alt && key is >= Key.D1 and <= Key.D6)
        {
            _vm.SelectModeCommand.Execute(key - Key.D0);
            e.Handled = true;
            return;
        }

        if (IsTypingInTextBox || _vm.ActiveKeypad is not { } keypad) return;

        if (mods == ModifierKeys.Control)
        {
            if (key == Key.C)
            {
                if (keypad.CopyText.Length > 0) TrySetClipboard(keypad.CopyText);
                e.Handled = true;
            }
            else if (key == Key.V)
            {
                if (TryGetClipboardText() is { } text) keypad.Paste(text);
                e.Handled = true;
            }
            return;
        }

        if (mods != ModifierKeys.None && mods != ModifierKeys.Shift) return;

        switch (key)
        {
            case Key.Enter:
                keypad.EvaluateCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
            case Key.Delete:
                keypad.ClearCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Back:
                keypad.BackspaceCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private static string? TryGetClipboardText()
    {
        try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
        catch (System.Runtime.InteropServices.COMException) { return null; }   // clipboard đang bị app khác giữ
    }

    private static void TrySetClipboard(string text)
    {
        try { Clipboard.SetText(text); }
        catch (System.Runtime.InteropServices.COMException) { /* clipboard đang bị app khác giữ */ }
    }
}
