using SmartCalc.Core.Evaluation;

namespace SmartCalc.Core.Tests.Evaluation;

public class EvaluatorTests
{
    private static readonly EvalContext Deg = new(AngleMode.Deg);
    private static readonly EvalContext Rad = new(AngleMode.Rad);

    private static double Eval(string s, EvalContext? ctx = null) => Calculator.Evaluate(s, ctx);
    private static string Show(string s, EvalContext? ctx = null) => Calculator.EvaluateToString(s, ctx);

    // ---------- Thứ tự ưu tiên & kết hợp ----------
    [Theory]
    [InlineData("2+3*4", 14)]
    [InlineData("(2+3)*4", 20)]
    [InlineData("2*3+4", 10)]
    [InlineData("10-2-3", 5)]
    [InlineData("100/10/5", 2)]
    [InlineData("2^3^2", 512)]
    [InlineData("(2^3)^2", 64)]
    [InlineData("2*3^2", 18)]
    [InlineData("7%3", 1)]
    [InlineData("2+7%3*2", 4)]
    [InlineData("((((1))))", 1)]
    [InlineData(" 1 +  2 ", 3)]
    public void Precedence_And_Associativity(string expr, double expected)
    {
        Assert.Equal(expected, Eval(expr));
    }

    // ---------- Số âm / dấu một ngôi ----------
    [Theory]
    [InlineData("-2^2", -4)]
    [InlineData("(-2)^2", 4)]
    [InlineData("-2^3", -8)]
    [InlineData("2^-1", 0.5)]
    [InlineData("2^-2", 0.25)]
    [InlineData("-3*-2", 6)]
    [InlineData("2--3", 5)]
    [InlineData("-(2+3)", -5)]
    [InlineData("+5", 5)]
    [InlineData("--5", 5)]
    [InlineData("-7%3", -1)]
    public void Unary(string expr, double expected)
    {
        Assert.Equal(expected, Eval(expr));
    }

    // ---------- Nhân ngầm ----------
    [Fact]
    public void ImplicitMultiplication()
    {
        Assert.Equal(2 * Math.PI, Eval("2pi"));
        Assert.Equal(15, Eval("3(4+1)"));
        Assert.Equal(8, Eval("(1+1)(2+2)"));
        Assert.Equal(1, Eval("2sin(30)", Deg), 12);
        Assert.Equal(2 * Math.PI * Math.PI, Eval("2pi^2"), 12);
        Assert.Equal(2 * Math.E, Eval("2e"), 12);
        Assert.Equal(3.5, Eval("7(1/2)"));
    }

    // ---------- Giai thừa ----------
    [Theory]
    [InlineData("0!", 1)]
    [InlineData("1!", 1)]
    [InlineData("5!", 120)]
    [InlineData("10!", 3628800)]
    [InlineData("3!!", 720)]
    [InlineData("-3!", -6)]
    [InlineData("2^3!", 64)]
    [InlineData("3!^2", 36)]
    [InlineData("(2+1)!", 6)]
    [InlineData("fact(5)", 120)]
    [InlineData("4!/2!", 12)]
    public void Factorial(string expr, double expected)
    {
        Assert.Equal(expected, Eval(expr));
    }

    [Fact]
    public void Factorial_170_IsMaximum()
    {
        Assert.Equal("7.25741561530799E+306", Show("170!"));
    }

    // ---------- Lượng giác DEG / RAD ----------
    [Theory]
    [InlineData("sin(30)", "0.5")]
    [InlineData("sin(90)", "1")]
    [InlineData("sin(180)", "0")]
    [InlineData("sin(-30)", "-0.5")]
    [InlineData("cos(60)", "0.5")]
    [InlineData("cos(90)", "0")]
    [InlineData("cos(0)", "1")]
    [InlineData("tan(45)", "1")]
    [InlineData("tan(0)", "0")]
    [InlineData("sin(390)", "0.5")]
    [InlineData("asin(1)", "90")]
    [InlineData("acos(0.5)", "60")]
    [InlineData("atan(1)", "45")]
    [InlineData("asin(sin(30))", "30")]
    public void Trig_Degrees(string expr, string expected)
    {
        Assert.Equal(expected, Show(expr, Deg));
    }

    [Theory]
    [InlineData("sin(pi/2)", "1")]
    [InlineData("sin(pi)", "0")]
    [InlineData("cos(pi)", "-1")]
    [InlineData("cos(pi/3)", "0.5")]
    [InlineData("tan(pi/4)", "1")]
    [InlineData("sin(30)", "-0.988031624092862")]
    [InlineData("atan(1)*4", "3.14159265358979")]
    [InlineData("asin(1)", "1.5707963267949")]
    public void Trig_Radians(string expr, string expected)
    {
        Assert.Equal(expected, Show(expr, Rad));
    }

    [Fact]
    public void DefaultAngleMode_IsDegrees()
    {
        Assert.Equal("0.5", Show("sin(30)"));
    }

    // ---------- Độ chính xác decimal ----------
    [Fact]
    public void DecimalArithmetic_IsExact()
    {
        Assert.Equal(0.3, Eval("0.1+0.2"));
        Assert.Equal("0.3", Show("0.1+0.2"));
        Assert.Equal(0.1, Eval("1-0.9"));
        Assert.Equal(0.3, Eval("0.1*3"));
        Assert.Equal(0, Eval("0.3-0.1-0.2"));
        Assert.Equal(0.01, Eval("0.1^2"));
        Assert.Equal(1.1, Eval("3.3/3"));
        Assert.Equal(0.1, Eval("0.7%0.3"));
    }

    [Fact]
    public void DisplayRounding_15SignificantDigits()
    {
        Assert.Equal("0.333333333333333", Show("1/3"));
        Assert.Equal("0.666666666666667", Show("2/3"));
        Assert.Equal("1", Show("1/3*3"));
        Assert.Equal("3.14159265358979", Show("pi"));
        Assert.Equal("1E+30", Show("10^30"));
        Assert.Equal("1E-20", Show("10^-20"));
        Assert.Equal("0", Show("-0"));
    }

    [Fact]
    public void LargeAndSmallNumbers_FallBackToDouble()
    {
        Assert.Equal(1e40, Eval("1e20*1e20"), 1e25);
        Assert.Equal(1e-30, Eval("1e-15*1e-15"), 1e-40);
        Assert.Equal(1.85184e-26, Eval("1.23456e-13*1.5e-13"), 1e-36);
    }

    // ---------- Hàm khác, hằng, Ans, x ----------
    [Theory]
    [InlineData("sqrt(16)", 4)]
    [InlineData("sqrt(2)^2", 2)]
    [InlineData("abs(-5)", 5)]
    [InlineData("ln(e)", 1)]
    [InlineData("log(1000)", 3)]
    [InlineData("log(0.01)", -2)]
    [InlineData("2^0.5*2^0.5", 2)]
    [InlineData("0^0", 1)]
    [InlineData("1.5e-3*2", 0.003)]
    public void Functions(string expr, double expected)
    {
        Assert.Equal(expected, Eval(expr), 12);
    }

    [Fact]
    public void Ans_UsesContext()
    {
        var ctx = new EvalContext(Ans: 10);
        Assert.Equal(25, Eval("Ans*2+5", ctx));
        Assert.Equal(0, Eval("Ans"));
    }

    [Fact]
    public void Variable_X_UsesContext()
    {
        var ctx = new EvalContext(X: 3);
        Assert.Equal(19, Eval("2x^2+1", ctx));
        Assert.Equal(1, Eval("sin(x)^2+cos(x)^2", ctx with { AngleMode = AngleMode.Rad }), 12);
    }
}
