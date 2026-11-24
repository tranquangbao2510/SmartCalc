namespace SmartCalc.Core.Graphing;

/*
 * LẤY MẪU HÀM ĐỂ VẼ — kết quả là các ĐOẠN NÉT LIỀN (polyline) rời nhau.
 *
 * Đi từ XMin đến XMax với `samples` bước đều. Nét bị NGẮT (không nối 2 điểm liền kề) khi:
 *   1. f không xác định tại điểm (sqrt(-1), 1/0, ln(0)...) -> bỏ điểm, kết thúc đoạn hiện tại.
 *   2. Hai điểm liền kề cùng xác định nhưng nhảy quá xa (> chiều cao vùng nhìn) VÀ giá trị ở giữa
 *      không nằm giữa hai đầu (hoặc không xác định) -> có tiệm cận đứng / gián đoạn (1/x, tan x)
 *      giữa hai mẫu. Nối lại sẽ thành một vạch dọc sai.
 *      Hàm liên tục dốc (x^3 khi thu nhỏ) có giá trị giữa nằm giữa hai đầu nên vẫn được nối.
 * Giá trị y quá xa vùng nhìn bị kẹp lại (vẫn ở ngoài màn hình) để WPF không phải vẽ toạ độ 1e300.
 */
public static class FunctionSampler
{
    public static IReadOnlyList<IReadOnlyList<PlotPoint>> Sample(PlotFunction f, Viewport view, int samples)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 2);

        var segments = new List<IReadOnlyList<PlotPoint>>();
        var current = new List<PlotPoint>();
        double dx = view.Width / samples;
        double clampLimit = view.Height * 20;
        double? prevY = null;
        double prevX = 0;

        void EndSegment()
        {
            if (current.Count >= 2) segments.Add(current);
            current = [];
        }

        for (int i = 0; i <= samples; i++)
        {
            double x = view.XMin + i * dx;
            double? y = f.Evaluate(x);

            if (y is null)
            {
                EndSegment();
                prevY = null;
                continue;
            }

            if (prevY is double py && IsDiscontinuity(f, prevX, py, x, y.Value, view.Height))
                EndSegment();

            current.Add(new PlotPoint(x, Math.Clamp(y.Value, view.YMin - clampLimit, view.YMax + clampLimit)));
            prevX = x;
            prevY = y;
        }

        EndSegment();
        return segments;
    }

    /// <summary>Giữa (x1, y1) và (x2, y2) có gián đoạn không (xem quy tắc 2 ở đầu file).</summary>
    public static bool IsDiscontinuity(PlotFunction f, double x1, double y1, double x2, double y2, double viewHeight)
    {
        if (Math.Abs(y2 - y1) <= viewHeight) return false;
        double? mid = f.Evaluate((x1 + x2) / 2);
        return mid is null || mid < Math.Min(y1, y2) || mid > Math.Max(y1, y2);
    }
}
