using System.Globalization;

namespace SmartCalc.Core.Evaluation;

/// <summary>Định dạng kết quả để hiển thị: tối đa 15 chữ số có nghĩa, dấu chấm thập phân.</summary>
public static class NumberFormatter
{
    public const int SignificantDigits = 15;

    public static string Format(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "∞" : "-∞";

        // Làm tròn 15 chữ số có nghĩa: xoá nhiễu dạng 0.30000000000000004 hay 30.000000000000004
        string s = value.ToString("G" + SignificantDigits, CultureInfo.InvariantCulture);
        return s == "-0" ? "0" : s;
    }
}
