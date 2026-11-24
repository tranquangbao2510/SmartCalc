using SmartCalc.Core.Errors;
using SmartCalc.Core.Evaluation;
using SmartCalc.Core.Graphing;
using SmartCalc.Core.History;
using SmartCalc.Core.Input;
using SmartCalc.Core.Lexing;
using SmartCalc.Core.Parsing;
using SmartCalc.Core.Programmer;
using SmartCalc.Core.Settings;
using SmartCalc.Core.Statistics;
using SmartCalc.Core.Units;

namespace SmartCalc.Core.Tests;

/// <summary>Ca biên: chuỗi rỗng, ngoặc lồng sâu, biểu thức rất dài, số cực lớn / cực nhỏ, dữ liệu hỏng.</summary>
public class EmptyInputTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n ")]
    public void Calculator_Empty_IsCleanError(string input)
    {
        var ex = Assert.Throws<CalcException>(() => Calculator.Evaluate(input));
        Assert.Contains("rỗng", ex.Message);
        Assert.Equal(input.Length, ex.Position);
    }

    [Fact]
    public void Lexer_Empty_OnlyEnd() => Assert.Single(Lexer.Tokenize(""));

    [Fact]
    public void Programmer_Empty_IsCleanError() =>
        Assert.Throws<CalcException>(() => ProgrammerCalculator.Evaluate("", 10, WordSize.QWord));

    [Fact]
    public void StateMachine_EqualsOnEmpty_StaysEmpty()
    {
        var m = new InputStateMachine();
        m.Process(InputEvent.EqualsKey);
        m.Process(InputEvent.Backspace);
        m.Process(InputEvent.Load(""));
        Assert.Equal(InputState.Empty, m.State);
    }

    [Fact]
    public void Others_Empty()
    {
        Assert.Throws<CalcException>(() => PlotFunction.Compile(""));
        Assert.Throws<CalcException>(() => StatisticsCalculator.Compute(""));
        Assert.False(UnitConverter.TryParse("", out _));
    }
}

public class DeepNestingTests
{
    private static string Nest(int depth, string inner = "1") => new string('(', depth) + inner + new string(')', depth);

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(200)]
    public void ModeratelyDeepParens_Work(int depth) => Assert.Equal(1, Calculator.Evaluate(Nest(depth)));

    [Theory]
    [InlineData(300)]
    [InlineData(5_000)]
    [InlineData(100_000)]   // trước đây: tràn stack, làm sập cả chương trình
    public void TooDeepParens_IsCleanError(int depth)
    {
        var ex = Assert.Throws<CalcException>(() => Calculator.Evaluate(Nest(depth)));
        Assert.Contains("lồng quá sâu", ex.Message);
        Assert.InRange(ex.Position, 0, depth);
    }

    [Fact]
    public void DeepUnaryChain_IsCleanError() =>
        Assert.Contains("lồng quá sâu", Assert.Throws<CalcException>(() => Calculator.Evaluate(new string('-', 50_000) + "1")).Message);

    [Fact]
    public void DeepPowerChain_IsCleanError() =>
        Assert.Throws<CalcException>(() => Calculator.Evaluate(string.Join("^", Enumerable.Repeat("1", 20_000))));

    [Fact]
    public void ModeratePowerChain_IsRightAssociative() =>
        Assert.Equal(1, Calculator.Evaluate(string.Join("^", Enumerable.Repeat("1", 100))));

    [Fact]
    public void VeryLongFlatChain_IsCleanError()
    {
        // 1+1+...+1 không lồng nhưng tạo cây AST rất cao -> giới hạn độ dài
        var ex = Assert.Throws<CalcException>(() => Calculator.Evaluate(string.Join("+", Enumerable.Repeat("1", 100_000))));
        Assert.Contains("quá dài", ex.Message);
    }

    [Fact]
    public void LongButAllowedChain_Works() =>
        Assert.Equal(400, Calculator.Evaluate(string.Join("+", Enumerable.Repeat("1", 400))));

    [Fact]
    public void UnbalancedDeep_ReportsOutermostMissingParen()
    {
        var ex = Assert.Throws<CalcException>(() => Calculator.Evaluate(new string('(', 100) + "1"));
        Assert.Contains("Thiếu dấu ')'", ex.Message);
        Assert.Equal(99, ex.Position);   // '(' trong cùng chưa đóng
    }

    [Fact]
    public void Programmer_DeepNesting_IsCleanError()
    {
        Assert.Equal(7, ProgrammerCalculator.Evaluate(Nest(100, "7"), 10, WordSize.QWord));
        Assert.Throws<CalcException>(() => ProgrammerCalculator.Evaluate(Nest(100_000, "7"), 10, WordSize.QWord));
        Assert.Throws<CalcException>(() => ProgrammerCalculator.Evaluate(new string('~', 100_000) + "1", 10, WordSize.QWord));
    }

    [Fact]
    public void Graph_DeepNesting_IsCleanError() =>
        Assert.False(PlotFunction.TryCompile(Nest(100_000, "x"), out _, out _));

    [Fact]
    public void StateMachine_DeepPaste_GoesToError()
    {
        var m = new InputStateMachine();
        m.Process(InputEvent.FromText(Nest(10_000)));
        Assert.Null(m.Preview);
        m.Process(InputEvent.EqualsKey);
        Assert.Equal(InputState.Error, m.State);
    }
}

public class ExtremeNumberTests
{
    [Theory]
    [InlineData("1e308*10", 5)]
    [InlineData("1e308+1e308", 5)]
    [InlineData("-1e308*10", 6)]
    [InlineData("1e400", 0)]
    [InlineData("10^309", 2)]
    [InlineData("2^1e10", 1)]
    [InlineData("170!*171", 4)]
    public void Overflow_IsCleanError(string expr, int position)
    {
        var ex = Assert.Throws<CalcException>(() => Calculator.Evaluate(expr));
        Assert.Contains("Tràn số", ex.Message);
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void VeryLongDigitString_IsOverflow() =>
        Assert.Contains("Tràn số", Assert.Throws<CalcException>(() => Calculator.Evaluate(new string('9', 400))).Message);

    [Theory]
    [InlineData("1.7976931348623157e308", "1.79769313486232E+308")]   // double.MaxValue
    [InlineData("1e308", "1E+308")]
    [InlineData("99999999999999999999999999999", "1E+29")]           // vượt decimal -> chuyển sang double
    [InlineData("79228162514264337593543950335+1", "7.92281625142643E+28")]
    [InlineData("1e27*1e27", "1E+54")]
    [InlineData("123456789012345678", "1.23456789012346E+17")]
    public void LargeButFinite(string expr, string expected) =>
        Assert.Equal(expected, Calculator.EvaluateToString(expr));

    [Theory]
    [InlineData("0.1^50", 1e-50)]          // trước đây ra 0: decimal hụt về 0
    [InlineData("0.5^1000", 9.332636185032189e-302)]
    [InlineData("1e-10/1e27", 1e-37)]      // trước đây ra 0
    [InlineData("1e-10*1e-10", 1e-20)]
    [InlineData("1e-300*1e-300", 0)]       // dưới cả double -> 0 thật
    [InlineData("5e-324", 5e-324)]         // số double nhỏ nhất
    public void TinyNumbers_DoNotUnderflowToZero(string expr, double expected)
    {
        double v = Calculator.Evaluate(expr);
        if (expected == 0) Assert.Equal(0, v);
        else Assert.Equal(1, v / expected, 9);
    }

    [Fact]
    public void DecimalExactness_StillHolds()
    {
        Assert.Equal(0, Calculator.Evaluate("0.3-0.1-0.2"));
        Assert.Equal(0.3, Calculator.Evaluate("0.1+0.2"));
        Assert.Equal(0, Calculator.Evaluate("5%5"));
        Assert.Equal(0, Calculator.Evaluate("0*1e-20"));
    }

    [Fact]
    public void Programmer_Extremes()
    {
        Assert.Equal(long.MinValue, ProgrammerCalculator.Evaluate("-9223372036854775808", 10, WordSize.QWord));
        Assert.Equal(long.MinValue, ProgrammerCalculator.Evaluate("9223372036854775807+1", 10, WordSize.QWord));
        Assert.Equal(-1, ProgrammerCalculator.Evaluate("18446744073709551615", 10, WordSize.QWord));
        Assert.Throws<CalcException>(() => ProgrammerCalculator.Evaluate("18446744073709551616", 10, WordSize.QWord));
        Assert.Equal(long.MinValue, ProgrammerCalculator.Evaluate("-9223372036854775808/-1", 10, WordSize.QWord));
        Assert.Equal(0, ProgrammerCalculator.Evaluate("-9223372036854775808%-1", 10, WordSize.QWord));
    }

    [Fact]
    public void Statistics_LargeValues()
    {
        var s = StatisticsCalculator.Compute("79228162514264337593543950335");
        Assert.Equal(79228162514264337593543950335m, s.Max);
        Assert.Throws<CalcException>(() => StatisticsCalculator.Compute("79228162514264337593543950335 1"));
        Assert.Throws<CalcException>(() => StatisticsCalculator.Parse("1e40"));   // vượt decimal
    }

    [Fact]
    public void Units_HugeValue_IsOverflowError() =>
        Assert.Throws<CalcException>(() => UnitConverter.Convert(79228162514264337593543950335m,
            UnitConverter.Find(UnitCategory.Length, "km"), UnitConverter.Find(UnitCategory.Length, "nm")));

    [Fact]
    public void Graph_HugeX_IsUndefinedNotCrash()
    {
        var f = PlotFunction.Compile("y = x^1000");
        Assert.Null(f.Evaluate(1e10));
        Assert.Equal(1, f.Evaluate(1));
    }
}

public sealed class CorruptFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SmartCalcTests", Guid.NewGuid().ToString("N"));

    public CorruptFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Settings_InvalidEnums_AreSanitized()
    {
        string path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, """{ "Theme": 7, "AngleMode": 9, "WordSize": 3, "ProgrammerRadix": 3 }""");
        var s = AppSettings.Load(path);
        Assert.Equal(AppTheme.Light, s.Theme);
        Assert.Equal(AngleMode.Deg, s.AngleMode);
        Assert.Equal(WordSize.QWord, s.WordSize);
        Assert.Equal(10, s.ProgrammerRadix);
    }

    [Fact]
    public void Settings_NullJson_GivesDefaults()
    {
        string path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "null");
        Assert.Equal(new AppSettings(), AppSettings.Load(path));
    }

    [Fact]
    public void History_NullAndPartialEntries_AreDropped()
    {
        string path = Path.Combine(_dir, "history.json");
        File.WriteAllText(path, """[ null, { "Expression": "1+1", "Result": "2" }, { "Result": "5" }, { "Expression": "2", "Result": null } ]""");
        var h = new HistoryStore(path);
        Assert.Single(h.Entries);
        Assert.Equal("1+1", h.Entries[0].Expression);
    }
}

public class CultureIndependenceTests
{
    [Theory]
    [InlineData("vi-VN")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void Results_DoNotDependOnMachineLocale(string culture)
    {
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
            Assert.Equal("0.5", Calculator.EvaluateToString("1/2"));
            Assert.Equal("1 234.5", UnitConverter.Format(1234.5m));
            Assert.Equal(2.5m, StatisticsCalculator.Compute("2 3").Mean);
            Assert.Equal("−(1.5)", ExpressionEditor.ToggleNegate("1.5"));
            Assert.Equal("1.5", ExpressionEditor.ToggleNegate("−(1.5)"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }
}
