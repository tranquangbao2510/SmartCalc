using SmartCalc.Core.Errors;
using SmartCalc.Core.Units;

namespace SmartCalc.Core.Tests.Units;

public class UnitConverterTests
{
    private static decimal Conv(decimal v, UnitCategory c, string from, string to) =>
        UnitConverter.Convert(v, UnitConverter.Find(c, from), UnitConverter.Find(c, to));

    private static string Show(decimal v, UnitCategory c, string from, string to) =>
        UnitConverter.Format(Conv(v, c, from, to));

    // ---------- Nhiệt độ (affine) ----------
    [Theory]
    [InlineData(100, "°C", "°F", "212")]
    [InlineData(0, "°C", "°F", "32")]
    [InlineData(-40, "°C", "°F", "-40")]          // điểm hai thang trùng nhau
    [InlineData(37, "°C", "°F", "98.6")]
    [InlineData(98.6, "°F", "°C", "37")]
    [InlineData(32, "°F", "°C", "0")]
    [InlineData(212, "°F", "°C", "100")]
    [InlineData(0, "°C", "K", "273.15")]
    [InlineData(0, "K", "°C", "-273.15")]
    [InlineData(0, "K", "°F", "-459.67")]
    [InlineData(-459.67, "°F", "K", "0")]
    [InlineData(300, "K", "°F", "80.33")]
    [InlineData(25, "°C", "°C", "25")]
    public void Temperature(decimal value, string from, string to, string expected) =>
        Assert.Equal(expected, Show(value, UnitCategory.Temperature, from, to));

    [Theory]
    [InlineData(-300, "°C")]
    [InlineData(-1, "K")]
    [InlineData(-500, "°F")]
    public void Temperature_BelowAbsoluteZero_IsError(decimal value, string from)
    {
        var ex = Assert.Throws<CalcException>(() => Conv(value, UnitCategory.Temperature, from, "K"));
        Assert.Contains("độ không tuyệt đối", ex.Message);
    }

    [Fact]
    public void Temperature_RoundTrip_IsStable()
    {
        decimal f = Conv(23.5m, UnitCategory.Temperature, "°C", "°F");
        Assert.Equal("23.5", UnitConverter.Format(Conv(f, UnitCategory.Temperature, "°F", "°C")));
    }

    // ---------- Các nhóm tuyến tính ----------
    [Theory]
    [InlineData(UnitCategory.Length, 1, "in", "cm", "2.54")]
    [InlineData(UnitCategory.Length, 1, "mi", "km", "1.609344")]
    [InlineData(UnitCategory.Length, 1, "ft", "in", "12")]
    [InlineData(UnitCategory.Length, 1, "km", "nm", "1 000 000 000 000")]
    [InlineData(UnitCategory.Mass, 1, "lb", "kg", "0.45359237")]
    [InlineData(UnitCategory.Mass, 1, "tạ", "yến", "10")]
    [InlineData(UnitCategory.Mass, 3, "lạng", "g", "300")]
    [InlineData(UnitCategory.Area, 1, "ha", "m²", "10 000")]
    [InlineData(UnitCategory.Area, 1, "acre", "ha", "0.40468564224")]
    [InlineData(UnitCategory.Volume, 1, "m³", "L", "1 000")]
    [InlineData(UnitCategory.Volume, 1, "gal", "L", "3.785411784")]
    [InlineData(UnitCategory.Speed, 10, "m/s", "km/h", "36")]
    [InlineData(UnitCategory.Speed, 100, "km/h", "m/s", "27.7777777777778")]
    [InlineData(UnitCategory.Speed, 1, "knot", "km/h", "1.852")]
    [InlineData(UnitCategory.Time, 1, "ngày", "s", "86 400")]
    [InlineData(UnitCategory.Time, 90, "min", "h", "1.5")]
    [InlineData(UnitCategory.Data, 1, "B", "bit", "8")]
    [InlineData(UnitCategory.Data, 1, "KiB", "B", "1 024")]
    [InlineData(UnitCategory.Data, 1, "GB", "MB", "1 000")]
    [InlineData(UnitCategory.Data, 1, "GiB", "MiB", "1 024")]
    [InlineData(UnitCategory.Data, 1, "TB", "GiB", "931.322574615479")]
    public void Linear(UnitCategory c, decimal value, string from, string to, string expected) =>
        Assert.Equal(expected, Show(value, c, from, to));

    [Fact]
    public void EveryCategory_HasUnits_AndRoundTrips()
    {
        foreach (var (category, units) in UnitConverter.Units)
        {
            Assert.True(units.Count >= 3, $"{category}");
            foreach (var from in units)
                foreach (var to in units)
                {
                    decimal there = UnitConverter.Convert(50m, from, to);
                    if (Math.Abs(there) < 0.000000001m) continue;   // decimal mất chữ số có nghĩa ở cỡ này
                    decimal back = UnitConverter.Convert(there, to, from);
                    Assert.Equal("50", UnitConverter.Format(back));
                }
        }
    }

    [Fact]
    public void DifferentCategories_Throws() =>
        Assert.Throws<ArgumentException>(() => UnitConverter.Convert(1,
            UnitConverter.Find(UnitCategory.Length, "m"), UnitConverter.Find(UnitCategory.Mass, "kg")));

    // ---------- Nhập / hiển thị ----------
    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData("12,5", 12.5)]
    [InlineData("−3", -3)]
    [InlineData(" 1 000 ", 1000)]
    [InlineData("1e3", 1000)]
    public void TryParse_Valid(string text, decimal expected)
    {
        Assert.True(UnitConverter.TryParse(text, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    public void TryParse_Invalid(string text) => Assert.False(UnitConverter.TryParse(text, out _));

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1234567.891, "1 234 567.891")]
    [InlineData(-0.5, "-0.5")]
    [InlineData(0.1000, "0.1")]
    [InlineData(0.000012, "0.000012")]
    [InlineData(0.0000001234, "1.234E-07")]
    [InlineData(2500000000000000, "2.5E+15")]
    public void Format(decimal value, string expected) => Assert.Equal(expected, UnitConverter.Format(value));
}
