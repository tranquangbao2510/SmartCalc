using System.Globalization;
using SmartCalc.Core.Errors;
using SmartCalc.Core.Lexing;
using SmartCalc.Core.Statistics;
using SmartCalc.Core.Units;

namespace SmartCalc.Core.Tests.Lexing;

public class SharedNumberReaderTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("-0.5", "-0.5")]
    [InlineData("−.5", "-0.5")]
    [InlineData("+12.", "12")]
    [InlineData("1.25e-2", "0.0125")]
    [InlineData("−1.25E+2", "-125")]
    [InlineData("1e−2", "0.01")]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950335")]
    [InlineData("-79228162514264337593543950335", "-79228162514264337593543950335")]
    [InlineData("1e-100", "0")]
    [InlineData("0.12345678901234567890123456789", "0.1234567890123456789012345679")]
    public void SharedDfa_ReadsSignedDecimalExactly(string text, string expected)
    {
        decimal number = decimal.Parse(expected, CultureInfo.InvariantCulture);
        Assert.True(NumberReader.TryParse(text, out decimal value));
        Assert.Equal(number, value);
        Assert.Equal(number, Assert.Single(StatisticsCalculator.Parse(text)));
        Assert.True(UnitConverter.TryParse(text, out decimal converted));
        Assert.Equal(number, converted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("−")]
    [InlineData(".")]
    [InlineData("1e")]
    [InlineData("1e+")]
    [InlineData("1.2.3")]
    [InlineData("2pi")]
    [InlineData("2sin(30)")]
    [InlineData("3(4+1)")]
    [InlineData("0xFF")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("--1")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("1e100")]
    public void SharedDfa_RejectsNonNumbersAndOverflow(string text)
    {
        Assert.False(NumberReader.TryParse(text, out _));
        Assert.False(UnitConverter.TryParse(text, out _));
        if (text.Length == 0) Assert.Empty(StatisticsCalculator.Parse(text));
        else
        {
            string input = "1;\n  " + text;
            var ex = Assert.Throws<CalcException>(() => StatisticsCalculator.Parse(input));
            Assert.Equal(5, ex.Position);
            Assert.Equal($"'{text}' không phải là số (vị trí 5)", ex.Message);
        }
    }

    [Fact]
    public void Statistics_KeepsAllSeparatorsAndSignedValues()
    {
        Assert.Equal([1m, -2m, -3m, 4.5m, .06m, 7m],
            StatisticsCalculator.Parse(" ,;\t1,−2; -3\r\n+4.5\t6e-2\u00a07 ;, "));
    }

    [Theory]
    [InlineData(" −1 234,50 ", "-1234.50")]
    [InlineData("+1,5e−2", "0.015")]
    [InlineData("\t.5\r\n", "0.5")]
    public void Units_KeepsCommaAndSpaceNormalization(string text, string expected)
    {
        Assert.True(UnitConverter.TryParse(text, out var value));
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), value);
    }
}
