using System.Globalization;

namespace SmartCalc.Core.Formatting;

/// <summary>Định dạng số decimal để hiển thị (dùng chung cho Đổi đơn vị, Thống kê).</summary>
public static class DecimalFormatter
{
    /// <summary>
    /// Hiển thị: làm tròn 15 chữ số có nghĩa, bỏ số 0 thừa, nhóm phần nguyên theo 3 bằng dấu cách.
    /// Số rất nhỏ (&lt; 1e-6) hoặc rất lớn (≥ 1e15) hiện dạng khoa học, vd 1.5E-09.
    /// (decimal chỉ có 28 chữ số sau dấu phẩy nên số cực nhỏ, vd 1e-17, chỉ còn ~11 chữ số có nghĩa.)
    /// </summary>
    public static string Format(decimal value)
    {
        if (value == 0) return "0";
        decimal abs = Math.Abs(value);
        if (abs < 0.000001m || abs >= 1_000_000_000_000_000m)
            return ((double)value).ToString("G15", CultureInfo.InvariantCulture);
        int magnitude = (int)Math.Floor(Math.Log10((double)Math.Abs(value)));
        int decimals = Math.Clamp(15 - magnitude - 1, 0, 28);
        decimal rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        if (rounded == 0) return "0";

        string s = rounded.ToString("0.############################", CultureInfo.InvariantCulture);
        bool negative = s.StartsWith('-');
        if (negative) s = s[1..];
        int dot = s.IndexOf('.');
        string intPart = dot < 0 ? s : s[..dot];
        string frac = dot < 0 ? "" : s[dot..];

        var grouped = new System.Text.StringBuilder();
        for (int i = 0; i < intPart.Length; i++)
        {
            if (i > 0 && (intPart.Length - i) % 3 == 0) grouped.Append(' ');
            grouped.Append(intPart[i]);
        }
        return (negative ? "-" : "") + grouped + frac;
    }
}
