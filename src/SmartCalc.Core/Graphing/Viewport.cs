namespace SmartCalc.Core.Graphing;

/// <summary>Một điểm trong mặt phẳng (toạ độ thực hoặc toạ độ màn hình tuỳ ngữ cảnh).</summary>
public readonly record struct PlotPoint(double X, double Y);

/// <summary>
/// Vùng nhìn của đồ thị: khoảng [XMin, XMax] × [YMin, YMax] trong toạ độ thực.
/// Bất biến (record) — zoom/kéo trả về Viewport mới. Màn hình: trục y hướng xuống.
/// </summary>
public sealed record Viewport(double XMin, double XMax, double YMin, double YMax)
{
    public const double MinSpan = 1e-9;
    public const double MaxSpan = 1e9;

    public static Viewport Default { get; } = new(-10, 10, -7, 7);

    public double Width => XMax - XMin;
    public double Height => YMax - YMin;
    public double CenterX => (XMin + XMax) / 2;
    public double CenterY => (YMin + YMax) / 2;

    /// <summary>Toạ độ thực -> pixel trên vùng vẽ rộng w, cao h.</summary>
    public PlotPoint ToScreen(double x, double y, double w, double h) =>
        new((x - XMin) / Width * w, (YMax - y) / Height * h);

    /// <summary>Pixel -> toạ độ thực.</summary>
    public PlotPoint ToWorld(double px, double py, double w, double h) =>
        new(XMin + px / w * Width, YMax - py / h * Height);

    /// <summary>
    /// Phóng to/thu nhỏ quanh điểm (cx, cy) — điểm đó đứng yên trên màn hình (zoom theo vị trí chuột).
    /// factor &lt; 1: phóng to, &gt; 1: thu nhỏ. Vượt giới hạn [MinSpan, MaxSpan] thì giữ nguyên.
    /// </summary>
    public Viewport Zoom(double factor, double cx, double cy)
    {
        if (factor <= 0 || double.IsNaN(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        double w = Width * factor, h = Height * factor;
        if (w < MinSpan || h < MinSpan || w > MaxSpan || h > MaxSpan) return this;
        return new(cx - (cx - XMin) * factor, cx + (XMax - cx) * factor,
                   cy - (cy - YMin) * factor, cy + (YMax - cy) * factor);
    }

    public Viewport ZoomAtCenter(double factor) => Zoom(factor, CenterX, CenterY);

    /// <summary>Dịch vùng nhìn theo toạ độ thực.</summary>
    public Viewport Pan(double dx, double dy) => new(XMin + dx, XMax + dx, YMin + dy, YMax + dy);

    /// <summary>Kéo chuột (dxPx, dyPx) pixel: nội dung đi theo chuột, nên vùng nhìn dịch ngược lại.</summary>
    public Viewport PanPixels(double dxPx, double dyPx, double w, double h) =>
        Pan(-dxPx / w * Width, dyPx / h * Height);

    /// <summary>
    /// Giữ tỉ lệ 1:1 (1 đơn vị x = 1 đơn vị y trên màn hình) cho vùng vẽ w × h:
    /// giữ nguyên khoảng x, chỉnh khoảng y quanh tâm hiện tại.
    /// </summary>
    public Viewport WithAspect(double w, double h)
    {
        if (w <= 0 || h <= 0) return this;
        double half = Width * h / w / 2;
        return this with { YMin = CenterY - half, YMax = CenterY + half };
    }
}

/// <summary>Chọn bước lưới "đẹp" (1, 2, 5 × 10^k) và liệt kê các vạch chia.</summary>
public static class GridLines
{
    public static double NiceStep(double range, int targetLines = 10)
    {
        if (range <= 0 || targetLines <= 0) throw new ArgumentOutOfRangeException(nameof(range));
        double raw = range / targetLines;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / magnitude;
        double nice = norm < 1.5 ? 1 : norm < 3.5 ? 2 : norm < 7.5 ? 5 : 10;
        return nice * magnitude;
    }

    /// <summary>Các bội số của step nằm trong [min, max]. Số 0 được trả về đúng là 0 (không phải -0 hay 1e-17).</summary>
    public static IReadOnlyList<double> Ticks(double min, double max, double step)
    {
        var ticks = new List<double>();
        long first = (long)Math.Ceiling(min / step), last = (long)Math.Floor(max / step);
        for (long k = first; k <= last && ticks.Count < 1000; k++)
            ticks.Add(k == 0 ? 0 : k * step);
        return ticks;
    }
}
