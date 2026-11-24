using SmartCalc.Core.Errors;
using SmartCalc.Core.Statistics;

namespace SmartCalc.Core.Tests.Statistics;

public class StatisticsTests
{
    private static StatisticsResult Stats(string text) => StatisticsCalculator.Compute(text);

    [Fact]
    public void ClassicDataset_AllMeasures()
    {
        // Bộ dữ liệu kinh điển: trung bình 5, độ lệch chuẩn tổng thể 2
        var s = Stats("2 4 4 4 5 5 7 9");
        Assert.Equal(8, s.Count);
        Assert.Equal(40m, s.Sum);
        Assert.Equal(5m, s.Mean);
        Assert.Equal(4.5m, s.Median);
        Assert.Equal([4m], s.Modes);
        Assert.Equal(2m, s.Min);
        Assert.Equal(9m, s.Max);
        Assert.Equal(7m, s.Range);
        Assert.Equal(4m, s.PopulationVariance);
        Assert.Equal(2, s.PopulationStdDev, 12);
        Assert.Equal(32m / 7m, s.SampleVariance);
        Assert.Equal(Math.Sqrt(32.0 / 7.0), s.SampleStdDev!.Value, 12);
    }

    [Theory]
    [InlineData("3 1 2", 2)]               // lẻ: phần tử giữa sau khi sắp xếp
    [InlineData("4 1 3 2", 2.5)]           // chẵn: trung bình 2 phần tử giữa
    [InlineData("7", 7)]
    [InlineData("-5 10 -20", -5)]
    [InlineData("1 2 3 100", 2.5)]         // không bị ảnh hưởng bởi giá trị ngoại lai
    public void Median(string text, decimal expected) => Assert.Equal(expected, Stats(text).Median);

    [Theory]
    [InlineData("1 1 2 3", new[] { 1.0 })]
    [InlineData("1 1 2 2 3", new[] { 1.0, 2.0 })]       // nhiều mode
    [InlineData("3 3 1 1", new[] { 1.0, 3.0 })]         // trả về theo thứ tự tăng
    [InlineData("1 2 3", new double[0])]                 // không có mode
    [InlineData("2 2.0 3", new[] { 2.0 })]               // 2 và 2.0 là cùng giá trị
    public void Modes(string text, double[] expected) =>
        Assert.Equal(expected.Select(v => (decimal)v), Stats(text).Modes);

    [Fact]
    public void SingleValue_SampleVarianceUndefined()
    {
        var s = Stats("42");
        Assert.Equal(0m, s.PopulationVariance);
        Assert.Equal(0, s.PopulationStdDev);
        Assert.Null(s.SampleVariance);
        Assert.Null(s.SampleStdDev);
    }

    [Fact]
    public void Decimal_IsExact()
    {
        var s = Stats("0.1 0.2");
        Assert.Equal(0.3m, s.Sum);
        Assert.Equal(0.15m, s.Mean);
        Assert.Equal(0.0025m, s.PopulationVariance);
        Assert.Equal(0.005m, s.SampleVariance);
    }

    [Fact]
    public void LargeOffset_VarianceStillExact()
    {
        // công thức Σx² − n·x̄² dễ mất chính xác khi dữ liệu lớn nhưng chênh lệch nhỏ; cách 2 lượt thì không
        var s = Stats("1000000001 1000000002 1000000003");
        Assert.Equal(1000000002m, s.Mean);
        Assert.Equal(1m, s.SampleVariance);
    }

    [Fact]
    public void NegativeAndIdentical()
    {
        Assert.Equal(-2m, Stats("-1 -2 -3").Mean);
        var same = Stats("5 5 5 5");
        Assert.Equal(0m, same.SampleVariance);
        Assert.Equal([5m], same.Modes);
    }

    // ---------- Đọc dữ liệu ----------
    [Theory]
    [InlineData("1 2 3", 3)]
    [InlineData("1,2,3", 3)]
    [InlineData("1; 2;3", 3)]
    [InlineData("1\n2\r\n3\t4", 4)]
    [InlineData("  1   2  ", 2)]
    [InlineData("1,5", 2)]                 // dấu phẩy là dấu phân tách, không phải dấu thập phân
    [InlineData("", 0)]
    public void Parse_Separators(string text, int count) =>
        Assert.Equal(count, StatisticsCalculator.Parse(text).Count);

    [Fact]
    public void Parse_NumberForms() =>
        Assert.Equal([1.5m, -2m, -3m, 1000m, 0.5m], StatisticsCalculator.Parse("1.5 -2 −3 1e3 .5"));

    [Theory]
    [InlineData("1 2 abc", 4, "abc")]
    [InlineData("x", 0, "x")]
    [InlineData("1; 2..5", 3, "2..5")]
    public void Parse_Invalid_ReportsPosition(string text, int position, string token)
    {
        var ex = Assert.Throws<CalcException>(() => StatisticsCalculator.Parse(text));
        Assert.Equal(position, ex.Position);
        Assert.Contains($"'{token}'", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ,; ")]
    public void Empty_IsError(string text)
    {
        var ex = Assert.Throws<CalcException>(() => Stats(text));
        Assert.Contains("Chưa có dữ liệu", ex.Message);
    }

    [Fact]
    public void Overflow_IsError() =>
        Assert.Throws<CalcException>(() => Stats(string.Join(' ', Enumerable.Repeat("1e28", 10))));
}
