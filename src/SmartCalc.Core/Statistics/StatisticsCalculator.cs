using SmartCalc.Core.Errors;
using SmartCalc.Core.Lexing;

namespace SmartCalc.Core.Statistics;

/// <summary>Kết quả thống kê mô tả. Phương sai/độ lệch chuẩn MẪU cần ít nhất 2 giá trị (null nếu n = 1).</summary>
public sealed record StatisticsResult(
    int Count,
    decimal Sum,
    decimal Mean,
    decimal Median,
    IReadOnlyList<decimal> Modes,
    decimal Min,
    decimal Max,
    decimal PopulationVariance,
    decimal? SampleVariance,
    double PopulationStdDev,
    double? SampleStdDev)
{
    public decimal Range => Max - Min;
}

/// <summary>
/// Thống kê một dãy số. Tính bằng decimal (tổng, trung bình, phương sai chính xác, vd trung bình của 0.1 và 0.2 = 0.15);
/// riêng độ lệch chuẩn = căn bậc hai nên dùng double.
///   Phương sai tổng thể  σ² = Σ(xᵢ − x̄)² / n
///   Phương sai mẫu       s²  = Σ(xᵢ − x̄)² / (n − 1)   (hiệu chỉnh Bessel)
/// Tính theo 2 lượt (trung bình trước, rồi tổng bình phương độ lệch) để tránh sai số của công thức Σx² − n·x̄².
/// </summary>
public static class StatisticsCalculator
{
    /// <summary>
    /// Đọc dãy số. Phân tách bằng khoảng trắng, xuống dòng, dấu phẩy hoặc chấm phẩy; dấu thập phân là dấu chấm.
    /// Giá trị sai -> CalcException trỏ đúng vị trí ký tự đầu của giá trị đó.
    /// </summary>
    public static IReadOnlyList<decimal> Parse(string? text)
    {
        var values = new List<decimal>();
        if (string.IsNullOrEmpty(text)) return values;

        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && IsSeparator(text[i])) i++;
            if (i >= text.Length) break;

            int start = i;
            while (i < text.Length && !IsSeparator(text[i])) i++;
            string token = text[start..i];

            if (!NumberReader.TryParse(token, out decimal v))
                throw new CalcException($"'{token}' không phải là số (vị trí {start})", start);
            values.Add(v);
        }
        return values;
    }

    private static bool IsSeparator(char c) => char.IsWhiteSpace(c) || c is ',' or ';';

    public static StatisticsResult Compute(IReadOnlyList<decimal> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Count == 0) throw new CalcException("Chưa có dữ liệu");

        try
        {
            int n = data.Count;
            var sorted = data.OrderBy(v => v).ToList();

            decimal sum = 0;
            foreach (var v in data) sum += v;
            decimal mean = sum / n;

            decimal median = n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;

            decimal squares = 0;
            foreach (var v in data) squares += (v - mean) * (v - mean);
            decimal popVar = squares / n;
            decimal? sampleVar = n >= 2 ? squares / (n - 1) : null;

            return new StatisticsResult(
                n, sum, mean, median, Modes(data), sorted[0], sorted[^1],
                popVar, sampleVar,
                Math.Sqrt((double)popVar),
                sampleVar is decimal s ? Math.Sqrt((double)s) : null);
        }
        catch (OverflowException)
        {
            throw new CalcException("Tràn số: dữ liệu quá lớn để tính");
        }
    }

    public static StatisticsResult Compute(string text) => Compute(Parse(text));

    /// <summary>
    /// Mode = các giá trị xuất hiện nhiều nhất (có thể nhiều mode). Nếu mọi giá trị chỉ xuất hiện 1 lần -> không có mode (rỗng).
    /// 2 và 2.0 được coi là cùng một giá trị.
    /// </summary>
    public static IReadOnlyList<decimal> Modes(IReadOnlyList<decimal> data)
    {
        var counts = data.GroupBy(v => v).Select(g => (Value: g.Key, Count: g.Count())).ToList();
        if (counts.Count == 0) return [];
        int max = counts.Max(c => c.Count);
        if (max == 1) return [];
        return counts.Where(c => c.Count == max).Select(c => c.Value).OrderBy(v => v).ToList();
    }
}
