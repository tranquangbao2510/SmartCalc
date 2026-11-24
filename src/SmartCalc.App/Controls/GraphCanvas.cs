using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SmartCalc.Core.Evaluation;
using SmartCalc.Core.Graphing;

namespace SmartCalc.App.Controls;

/// <summary>Một hàm cần vẽ + màu nét.</summary>
public sealed record GraphPlot(PlotFunction Function, Brush Brush);

/// <summary>
/// Canvas vẽ đồ thị bằng OnRender (vẽ trực tiếp, không tạo hàng nghìn phần tử con).
/// Toán (lấy mẫu, ngắt nét ở điểm không xác định, đổi toạ độ, zoom/kéo, bước lưới) nằm trong Core.Graphing;
/// control chỉ chuyển sự kiện chuột thành phép biến đổi Viewport và vẽ kết quả.
///   - Lăn chuột: phóng to/thu nhỏ quanh vị trí chuột.   - Kéo chuột trái: di chuyển.   - Nhấp đúp: đặt lại.
/// </summary>
public sealed class GraphCanvas : Canvas
{
    private const double ZoomStep = 0.85;
    private Point? _dragStart;
    private Viewport? _dragViewport;

    public GraphCanvas()
    {
        ClipToBounds = true;
        Focusable = false;
        Cursor = Cursors.SizeAll;
    }

    // ===================== Dependency properties =====================
    public static readonly DependencyProperty ViewportProperty = DependencyProperty.Register(
        nameof(Viewport), typeof(Viewport), typeof(GraphCanvas),
        new FrameworkPropertyMetadata(Viewport.Default,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            null, CoerceViewport));

    // Mọi vùng nhìn được gán vào (kể cả "Đặt lại" từ ViewModel) đều chỉnh về tỉ lệ 1:1 theo kích thước canvas
    private static object CoerceViewport(DependencyObject d, object value)
    {
        var canvas = (GraphCanvas)d;
        var v = (Viewport)value;
        if (canvas.ActualWidth < 2 || canvas.ActualHeight < 2) return v;
        double expected = v.Width * canvas.ActualHeight / canvas.ActualWidth;
        return Math.Abs(v.Height - expected) <= expected * 1e-6 ? v : v.WithAspect(canvas.ActualWidth, canvas.ActualHeight);
    }

    public Viewport Viewport
    {
        get => (Viewport)GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    public static readonly DependencyProperty PlotsProperty = DependencyProperty.Register(
        nameof(Plots), typeof(IReadOnlyList<GraphPlot>), typeof(GraphCanvas),
        new FrameworkPropertyMetadata(Array.Empty<GraphPlot>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<GraphPlot> Plots
    {
        get => (IReadOnlyList<GraphPlot>)GetValue(PlotsProperty);
        set => SetValue(PlotsProperty, value);
    }

    /// <summary>Toạ độ thực dưới con trỏ chuột (null khi chuột ở ngoài) — để hiển thị "x = …, y = …".</summary>
    public static readonly DependencyProperty CursorPointProperty = DependencyProperty.Register(
        nameof(CursorPoint), typeof(PlotPoint?), typeof(GraphCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public PlotPoint? CursorPoint
    {
        get => (PlotPoint?)GetValue(CursorPointProperty);
        set => SetValue(CursorPointProperty, value);
    }

    public static readonly DependencyProperty GridBrushProperty = BrushProperty(nameof(GridBrush), Brushes.LightGray);
    public static readonly DependencyProperty AxisBrushProperty = BrushProperty(nameof(AxisBrush), Brushes.Gray);
    public static readonly DependencyProperty LabelBrushProperty = BrushProperty(nameof(LabelBrush), Brushes.Gray);

    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public Brush AxisBrush { get => (Brush)GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public Brush LabelBrush { get => (Brush)GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

    private static DependencyProperty BrushProperty(string name, Brush fallback) =>
        DependencyProperty.Register(name, typeof(Brush), typeof(GraphCanvas),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Đặt lại vùng nhìn mặc định (giữ tỉ lệ 1:1 theo kích thước hiện tại).</summary>
    public void ResetView() => Viewport = Viewport.Default.WithAspect(ActualWidth, ActualHeight);

    // ===================== Vẽ =====================
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        // Giữ 1 đơn vị x = 1 đơn vị y trên màn hình khi cửa sổ đổi kích thước
        Viewport = Viewport.WithAspect(ActualWidth, ActualHeight);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w < 2 || h < 2) return;

        var v = Viewport;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));   // để nhận sự kiện chuột ở mọi chỗ

        DrawGridAndAxes(dc, v, w, h);

        var pens = new Dictionary<Brush, Pen>();
        foreach (var plot in Plots)
        {
            if (!pens.TryGetValue(plot.Brush, out var pen))
            {
                pen = new Pen(plot.Brush, 2.4) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                pen.Freeze();
                pens[plot.Brush] = pen;
            }
            DrawFunction(dc, plot.Function, pen, v, w, h);
        }
    }

    private void DrawGridAndAxes(DrawingContext dc, Viewport v, double w, double h)
    {
        double step = GridLines.NiceStep(Math.Max(v.Width, v.Height) , 12);
        var gridPen = new Pen(GridBrush, 1);
        var axisPen = new Pen(AxisBrush, 1.4);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface("Segoe UI");

        // Toạ độ màn hình của 2 trục (kẹp vào mép nếu trục nằm ngoài vùng nhìn, để nhãn vẫn đọc được)
        var origin = v.ToScreen(0, 0, w, h);
        double axisY = Math.Clamp(origin.Y, 0, h);
        double axisX = Math.Clamp(origin.X, 0, w);

        foreach (double x in GridLines.Ticks(v.XMin, v.XMax, step))
        {
            double px = Snap(v.ToScreen(x, 0, w, h).X);
            dc.DrawLine(x == 0 ? axisPen : gridPen, new Point(px, 0), new Point(px, h));
            if (x == 0) continue;
            var text = Label(x, typeface, dpi);
            if (px - text.Width / 2 < 2 || px + text.Width / 2 > w - 2) continue;   // nhãn bị cắt ở mép -> bỏ
            double ty = axisY + 4 + text.Height > h ? axisY - text.Height - 4 : axisY + 4;
            dc.DrawText(text, new Point(px - text.Width / 2, ty));
        }

        foreach (double y in GridLines.Ticks(v.YMin, v.YMax, step))
        {
            double py = Snap(v.ToScreen(0, y, w, h).Y);
            dc.DrawLine(y == 0 ? axisPen : gridPen, new Point(0, py), new Point(w, py));
            if (y == 0) continue;
            var text = Label(y, typeface, dpi);
            if (py - text.Height / 2 < 2 || py + text.Height / 2 > h - 2) continue;
            double tx = axisX - text.Width - 6 < 0 ? axisX + 6 : axisX - text.Width - 6;
            dc.DrawText(text, new Point(tx, py - text.Height / 2));
        }

        // Trục (vẽ lại cho đè lên lưới) + nhãn gốc 0
        if (origin.Y >= 0 && origin.Y <= h) dc.DrawLine(axisPen, new Point(0, Snap(origin.Y)), new Point(w, Snap(origin.Y)));
        if (origin.X >= 0 && origin.X <= w) dc.DrawLine(axisPen, new Point(Snap(origin.X), 0), new Point(Snap(origin.X), h));
        if (origin.X >= 0 && origin.X <= w && origin.Y >= 0 && origin.Y <= h)
            dc.DrawText(Label(0, typeface, dpi), new Point(origin.X - 12, origin.Y + 4));
    }

    private FormattedText Label(double value, Typeface typeface, double dpi) =>
        new(NumberFormatter.Format(value), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 11, LabelBrush, dpi);

    private static double Snap(double v) => Math.Round(v) + 0.5;   // nét 1px sắc nét

    private static void DrawFunction(DrawingContext dc, PlotFunction f, Pen pen, Viewport v, double w, double h)
    {
        int samples = Math.Clamp((int)w, 50, 4000);   // ~1 mẫu mỗi pixel
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            foreach (var segment in FunctionSampler.Sample(f, v, samples))
            {
                var first = v.ToScreen(segment[0].X, segment[0].Y, w, h);
                ctx.BeginFigure(new Point(first.X, first.Y), isFilled: false, isClosed: false);
                ctx.PolyLineTo(segment.Skip(1).Select(p =>
                {
                    var s = v.ToScreen(p.X, p.Y, w, h);
                    return new Point(s.X, s.Y);
                }).ToList(), isStroked: true, isSmoothJoin: true);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    // ===================== Chuột: zoom, kéo, toạ độ =====================
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var p = e.GetPosition(this);
        var world = Viewport.ToWorld(p.X, p.Y, ActualWidth, ActualHeight);
        double factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        Viewport = Viewport.Zoom(factor, world.X, world.Y);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ClickCount == 2)
        {
            ResetView();
            e.Handled = true;
            return;
        }
        _dragStart = e.GetPosition(this);
        _dragViewport = Viewport;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        if (_dragStart is Point start && _dragViewport is Viewport from)
        {
            // Luôn tính từ vùng nhìn lúc bắt đầu kéo -> không cộng dồn sai số
            Viewport = from.PanPixels(p.X - start.X, p.Y - start.Y, ActualWidth, ActualHeight);
        }
        CursorPoint = Viewport.ToWorld(p.X, p.Y, ActualWidth, ActualHeight);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _dragStart = null;
        _dragViewport = null;
        ReleaseMouseCapture();
    }

    // Mất capture giữa chừng (Alt+Tab, hộp thoại hệ thống...) -> kết thúc kéo, tránh "kéo kẹt" ở lần rê chuột sau
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragStart = null;
        _dragViewport = null;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_dragStart is null) CursorPoint = null;
    }
}
