using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalc.App.Controls;
using SmartCalc.Core.Evaluation;
using SmartCalc.Core.Graphing;

namespace SmartCalc.App.ViewModels;

/// <summary>Một ô nhập hàm y = f(x) với màu riêng.</summary>
public sealed partial class FunctionEntryViewModel : ObservableObject
{
    private readonly Action _changed;

    public FunctionEntryViewModel(int index, Color color, string text, Action changed)
    {
        Index = index;
        Brush = new SolidColorBrush(color);
        Brush.Freeze();
        _changed = changed;
        Text = text;
    }

    public int Index { get; }
    public SolidColorBrush Brush { get; }
    public string Label => $"y{Index}";

    [ObservableProperty] public partial string Text { get; set; } = "";
    [ObservableProperty] public partial bool IsVisible { get; set; } = true;
    [ObservableProperty] public partial string Error { get; private set; } = "";
    [ObservableProperty] public partial bool HasError { get; private set; }

    /// <summary>Hàm đã biên dịch (null nếu ô trống hoặc lỗi).</summary>
    public PlotFunction? Function { get; private set; }

    partial void OnTextChanged(string value)
    {
        Function = null;
        Error = "";
        if (!string.IsNullOrWhiteSpace(value))
        {
            if (PlotFunction.TryCompile(value, out var f, out var error)) Function = f;
            else Error = error!.Message;
        }
        HasError = Error.Length > 0;
        _changed();
    }

    partial void OnIsVisibleChanged(bool value) => _changed();

    [RelayCommand] private void ToggleVisible() => IsVisible = !IsVisible;
    [RelayCommand] private void ClearText() => Text = "";
}

/// <summary>Chế độ Vẽ đồ thị: tối đa 3 hàm, mỗi hàm một màu. Vẽ do GraphCanvas, toán nằm trong Core.Graphing.</summary>
public sealed partial class GraphViewModel : ObservableObject
{
    public const int MaxFunctions = 3;

    // Màu đủ tương phản trên cả nền sáng và tối
    private static readonly Color[] Colors =
    [
        Color.FromRgb(0x3B, 0x82, 0xF6),   // xanh dương
        Color.FromRgb(0xEF, 0x44, 0x44),   // đỏ
        Color.FromRgb(0x10, 0xB9, 0x81),   // xanh lá
    ];

    private static readonly string[] Samples = ["sin(x)", "x^2/4 - 2", ""];

    public GraphViewModel()
    {
        Functions = new(Enumerable.Range(0, MaxFunctions)
            .Select(i => new FunctionEntryViewModel(i + 1, Colors[i], Samples[i], RebuildPlots)));
        RebuildPlots();
    }

    public ObservableCollection<FunctionEntryViewModel> Functions { get; }

    /// <summary>Danh sách nét cần vẽ (gán mới mỗi lần thay đổi để GraphCanvas vẽ lại).</summary>
    [ObservableProperty] public partial IReadOnlyList<GraphPlot> Plots { get; private set; } = [];

    [ObservableProperty] public partial Viewport Viewport { get; set; } = Viewport.Default;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CursorText))]
    public partial PlotPoint? CursorPoint { get; set; }

    public string CursorText => CursorPoint is { } p
        ? $"x = {Round(p.X)}   y = {Round(p.Y)}"
        : "Cuộn chuột: phóng to/thu nhỏ · Kéo: di chuyển · Nhấp đúp: đặt lại";

    // Làm tròn theo độ phóng hiện tại (đủ 3 chữ số sau bước lưới)
    private string Round(double v)
    {
        double step = GridLines.NiceStep(Viewport.Width, 10);
        int decimals = Math.Clamp(3 - (int)Math.Floor(Math.Log10(step)), 0, 12);
        return NumberFormatter.Format(Math.Round(v, decimals));
    }

    private void RebuildPlots()
    {
        if (Functions is null) return;   // đang khởi tạo
        Plots = Functions
            .Where(f => f.IsVisible && f.Function is not null)
            .Select(f => new GraphPlot(f.Function!, f.Brush))
            .ToList();
    }

    [RelayCommand] private void ZoomIn() => Viewport = Viewport.ZoomAtCenter(0.7);
    [RelayCommand] private void ZoomOut() => Viewport = Viewport.ZoomAtCenter(1 / 0.7);
    [RelayCommand] private void ResetView() => Viewport = Viewport.Default;
}
