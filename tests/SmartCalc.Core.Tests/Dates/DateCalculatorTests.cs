using SmartCalc.Core.Errors;
using SmartCalc.Core.Dates;

namespace SmartCalc.Core.Tests.Dates;

public class DateCalculatorTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    // ---------- Năm nhuận ----------
    [Theory]
    [InlineData(2024, true)]
    [InlineData(2023, false)]
    [InlineData(2000, true)]    // chia hết cho 400
    [InlineData(1900, false)]   // chia hết cho 100 nhưng không cho 400
    [InlineData(2100, false)]
    [InlineData(1600, true)]
    public void IsLeapYear(int year, bool expected)
    {
        Assert.Equal(expected, DateCalculator.IsLeapYear(year));
        Assert.Equal(DateTime.IsLeapYear(year), DateCalculator.IsLeapYear(year));
    }

    [Fact]
    public void DaysInMonth_MatchesFramework()
    {
        for (int y = 1896; y <= 2104; y++)
            for (int m = 1; m <= 12; m++)
                Assert.Equal(DateTime.DaysInMonth(y, m), DateCalculator.DaysInMonth(y, m));
    }

    // ---------- Khoảng cách qua tháng 2 / năm nhuận ----------
    [Theory]
    [InlineData(2024, 2, 28, 2024, 3, 1, 2)]     // 2024 nhuận: có 29/02
    [InlineData(2023, 2, 28, 2023, 3, 1, 1)]
    [InlineData(2000, 2, 28, 2000, 3, 1, 2)]     // 2000 nhuận
    [InlineData(1900, 2, 28, 1900, 3, 1, 1)]     // 1900 không nhuận
    [InlineData(2024, 1, 1, 2025, 1, 1, 366)]
    [InlineData(2023, 1, 1, 2024, 1, 1, 365)]
    [InlineData(2000, 1, 1, 2100, 1, 1, 36525)]  // 25 năm nhuận (2000..2096), 2100 không nhuận
    [InlineData(1900, 1, 1, 2000, 1, 1, 36524)]  // 1900 không nhuận -> 24 năm nhuận
    [InlineData(2020, 2, 29, 2024, 2, 29, 1461)] // 4 năm có 1 ngày nhuận
    public void TotalDays_AcrossLeapYears(int y1, int m1, int d1, int y2, int m2, int d2, int days)
    {
        Assert.Equal(days, DateCalculator.Difference(D(y1, m1, d1), D(y2, m2, d2)).TotalDays);
        Assert.Equal(days, DateCalculator.Difference(D(y2, m2, d2), D(y1, m1, d1)).TotalDays); // đối xứng
    }

    [Theory]
    [InlineData(2024, 1, 1, 2024, 1, 1, 0, 0, 0, 0)]
    [InlineData(2024, 1, 1, 2024, 1, 10, 0, 0, 1, 2)]
    [InlineData(2020, 1, 15, 2024, 3, 20, 4, 2, 0, 5)]
    [InlineData(2023, 1, 31, 2023, 3, 1, 0, 1, 0, 1)]    // 31/01 + 1 tháng = 28/02, còn 1 ngày
    [InlineData(2024, 1, 31, 2024, 3, 1, 0, 1, 0, 1)]    // 31/01 + 1 tháng = 29/02 (nhuận), còn 1 ngày
    [InlineData(2020, 2, 29, 2021, 2, 28, 1, 0, 0, 0)]   // 29/02 + 1 năm kẹp về 28/02
    [InlineData(2020, 2, 29, 2021, 3, 1, 1, 0, 0, 1)]
    [InlineData(1999, 12, 31, 2000, 1, 1, 0, 0, 0, 1)]
    [InlineData(2024, 3, 10, 2024, 5, 31, 0, 2, 3, 0)]
    public void Difference_YearsMonthsWeeksDays(int y1, int m1, int d1, int y2, int m2, int d2,
        int years, int months, int weeks, int days)
    {
        var diff = DateCalculator.Difference(D(y1, m1, d1), D(y2, m2, d2));
        Assert.Equal((years, months, weeks, days), (diff.Years, diff.Months, diff.Weeks, diff.Days));
    }

    [Theory]
    [InlineData(2024, 1, 1, 2024, 1, 1, "Cùng một ngày")]
    [InlineData(2020, 1, 15, 2024, 3, 20, "4 năm, 2 tháng, 5 ngày")]
    [InlineData(2024, 1, 1, 2024, 1, 15, "2 tuần")]
    public void Describe(int y1, int m1, int d1, int y2, int m2, int d2, string expected) =>
        Assert.Equal(expected, DateCalculator.Difference(D(y1, m1, d1), D(y2, m2, d2)).Describe());

    // ---------- Cộng / trừ ----------
    [Theory]
    [InlineData(2024, 2, 28, 0, 0, 0, 1, false, 2024, 2, 29)]
    [InlineData(2023, 2, 28, 0, 0, 0, 1, false, 2023, 3, 1)]
    [InlineData(2024, 2, 29, 1, 0, 0, 0, false, 2025, 2, 28)]   // kẹp về ngày cuối tháng
    [InlineData(2024, 1, 31, 0, 1, 0, 0, false, 2024, 2, 29)]
    [InlineData(2024, 3, 31, 0, 1, 0, 0, true, 2024, 2, 29)]
    [InlineData(2024, 1, 1, 0, 0, 2, 3, false, 2024, 1, 18)]
    [InlineData(2024, 3, 1, 0, 0, 0, 1, true, 2024, 2, 29)]
    [InlineData(2000, 1, 1, 1, 2, 3, 4, false, 2001, 3, 26)]
    [InlineData(2001, 3, 26, 1, 2, 3, 4, true, 2000, 1, 1)]
    public void Add(int y, int m, int d, int years, int months, int weeks, int days, bool subtract, int ey, int em, int ed) =>
        Assert.Equal(D(ey, em, ed), DateCalculator.Add(D(y, m, d), years, months, weeks, days, subtract));

    [Fact]
    public void Add_OutOfRange_IsError()
    {
        Assert.Throws<CalcException>(() => DateCalculator.Add(D(9999, 12, 31), 0, 0, 0, 1));
        Assert.Throws<CalcException>(() => DateCalculator.Add(D(1, 1, 1), 0, 0, 0, 1, subtract: true));
    }

    // ---------- Đọc / hiển thị ----------
    [Theory]
    [InlineData("29/02/2024", 2024, 2, 29)]
    [InlineData("1/2/2023", 2023, 2, 1)]
    [InlineData("01-12-1999", 1999, 12, 1)]
    [InlineData(" 31.12.2000 ", 2000, 12, 31)]
    [InlineData("2024-02-29", 2024, 2, 29)]        // dạng ISO
    public void TryParse_Valid(string text, int y, int m, int d)
    {
        Assert.True(DateCalculator.TryParse(text, out var date, out _));
        Assert.Equal(D(y, m, d), date);
    }

    [Theory]
    [InlineData("29/02/2023", "không phải năm nhuận")]
    [InlineData("29/02/1900", "không phải năm nhuận")]
    [InlineData("31/04/2024", "chỉ có 30 ngày")]
    [InlineData("1/13/2024", "Tháng phải từ 1 đến 12")]
    [InlineData("01/01/0", "Năm phải từ 1 đến 9999")]
    [InlineData("2023-02-29", "không phải năm nhuận")]
    [InlineData("hôm nay", "dd/MM/yyyy")]
    [InlineData("", "Chưa nhập")]
    public void TryParse_Invalid(string text, string keyword)
    {
        Assert.False(DateCalculator.TryParse(text, out _, out string error));
        Assert.Contains(keyword, error);
    }

    [Fact]
    public void FormatLong_VietnameseWeekday()
    {
        Assert.Equal("Thứ Bảy, 29/02/2020", DateCalculator.FormatLong(D(2020, 2, 29)));
        Assert.Equal("Chủ Nhật, 01/01/2023", DateCalculator.FormatLong(D(2023, 1, 1)));
    }
}
