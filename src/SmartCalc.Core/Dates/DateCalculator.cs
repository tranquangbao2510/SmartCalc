using System.Globalization;
using SmartCalc.Core.Errors;

namespace SmartCalc.Core.Dates;

/// <summary>Khoảng cách giữa hai ngày theo lịch: năm, tháng, tuần, ngày + tổng số ngày.</summary>
public sealed record DateDifference(int Years, int Months, int Weeks, int Days, int TotalDays)
{
    /// <summary>Mô tả tiếng Việt, bỏ các thành phần bằng 0, vd "1 năm, 2 tháng, 3 ngày".</summary>
    public string Describe()
    {
        if (TotalDays == 0) return "Cùng một ngày";
        var parts = new List<string>();
        if (Years > 0) parts.Add($"{Years} năm");
        if (Months > 0) parts.Add($"{Months} tháng");
        if (Weeks > 0) parts.Add($"{Weeks} tuần");
        if (Days > 0) parts.Add($"{Days} ngày");
        return string.Join(", ", parts);
    }
}

/// <summary>
/// Tính ngày tháng theo lịch Gregory (năm nhuận: chia hết cho 4, trừ năm chia hết cho 100 nhưng không chia hết cho 400).
/// </summary>
public static class DateCalculator
{
    public const string DateFormat = "dd/MM/yyyy";

    public static bool IsLeapYear(int year) => (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;

    public static int DaysInMonth(int year, int month) => month switch
    {
        2 => IsLeapYear(year) ? 29 : 28,
        4 or 6 or 9 or 11 => 30,
        _ => 31,
    };

    /// <summary>
    /// Khoảng cách giữa a và b (không phụ thuộc thứ tự). Đếm năm trọn, rồi tháng trọn, phần còn lại tách thành tuần + ngày.
    /// Cộng tháng vào ngày cuối tháng sẽ "kẹp" về ngày cuối của tháng đích (31/01 + 1 tháng = 28/02 hoặc 29/02).
    /// </summary>
    public static DateDifference Difference(DateOnly a, DateOnly b)
    {
        var (start, end) = a <= b ? (a, b) : (b, a);
        int totalDays = end.DayNumber - start.DayNumber;

        int years = end.Year - start.Year;
        if (start.AddYears(years) > end) years--;

        int months = 0;
        // Tối đa 11 tháng (12 tháng đã được tính thành 1 năm). Dùng TryAddMonths để không vượt năm 9999.
        var anchor = start.AddYears(years);
        while (months < 11 && TryAddMonths(anchor, months + 1, out var next) && next <= end) months++;
        var cursor = start.AddYears(years).AddMonths(months);

        int rest = end.DayNumber - cursor.DayNumber;
        return new DateDifference(years, months, rest / 7, rest % 7, totalDays);
    }

    /// <summary>Cộng (hoặc trừ) năm, tháng, tuần, ngày vào một ngày. Thứ tự: năm -> tháng -> ngày.</summary>
    public static DateOnly Add(DateOnly start, int years, int months, int weeks, int days, bool subtract = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(years);
        ArgumentOutOfRangeException.ThrowIfNegative(months);
        ArgumentOutOfRangeException.ThrowIfNegative(weeks);
        ArgumentOutOfRangeException.ThrowIfNegative(days);

        int sign = subtract ? -1 : 1;
        long totalDays = (long)weeks * 7 + days;   // long: weeks * 7 có thể tràn int
        if (totalDays > DateOnly.MaxValue.DayNumber)
            throw new CalcException("Kết quả nằm ngoài phạm vi 01/01/0001 – 31/12/9999");
        try
        {
            return start.AddYears(sign * years).AddMonths(sign * months).AddDays(sign * (int)totalDays);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new CalcException("Kết quả nằm ngoài phạm vi 01/01/0001 – 31/12/9999");
        }
    }

    /// <summary>Cộng k tháng nếu kết quả còn trong phạm vi (không ném ngoại lệ ở biên năm 9999).</summary>
    private static bool TryAddMonths(DateOnly date, int k, out DateOnly result)
    {
        long monthIndex = (long)date.Year * 12 + date.Month - 1 + k;
        if (monthIndex > 9999L * 12 + 11)
        {
            result = default;
            return false;
        }
        result = date.AddMonths(k);
        return true;
    }

    /// <summary>Đọc ngày dạng d/M/yyyy (chấp nhận dấu '/', '-', '.'). Sai -> false và thông báo lỗi tiếng Việt.</summary>
    public static bool TryParse(string? text, out DateOnly date, out string error)
    {
        date = default;
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Chưa nhập ngày";
            return false;
        }

        var parts = text.Trim().Split('/', '-', '.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int d)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int m)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int y))
        {
            error = "Ngày phải có dạng dd/MM/yyyy";
            return false;
        }
        if (parts[0].Length == 4) (d, y) = (y, d);   // chấp nhận cả dạng ISO yyyy-MM-dd
        if (y < 1 || y > 9999) { error = "Năm phải từ 1 đến 9999"; return false; }
        if (m < 1 || m > 12) { error = "Tháng phải từ 1 đến 12"; return false; }
        if (d < 1 || d > DaysInMonth(y, m))
        {
            error = m == 2 && d == 29 ? $"Năm {y} không phải năm nhuận, tháng 2 chỉ có 28 ngày"
                                      : $"Tháng {m}/{y} chỉ có {DaysInMonth(y, m)} ngày";
            return false;
        }
        date = new DateOnly(y, m, d);
        return true;
    }

    public static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string DayOfWeekName(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        _ => "Chủ Nhật",
    };

    /// <summary>Vd "Thứ Bảy, 29/02/2020".</summary>
    public static string FormatLong(DateOnly date) => $"{DayOfWeekName(date)}, {Format(date)}";
}
