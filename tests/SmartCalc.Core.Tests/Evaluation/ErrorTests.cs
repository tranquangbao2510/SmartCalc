using SmartCalc.Core.Errors;
using SmartCalc.Core.Evaluation;

namespace SmartCalc.Core.Tests.Evaluation;

/// <summary>Mọi loại lỗi trong SPEC: kiểm tra cả vị trí (0-based) và từ khoá trong thông báo tiếng Việt.</summary>
public class ErrorTests
{
    private static CalcException Fail(string expr, EvalContext? ctx = null) =>
        Assert.Throws<CalcException>(() => Calculator.Evaluate(expr, ctx));

    private static void AssertError(string expr, int position, string keyword, EvalContext? ctx = null)
    {
        var ex = Fail(expr, ctx);
        Assert.Equal(position, ex.Position);
        Assert.Contains(keyword, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Ký tự lạ (từ lexer) ----------
    [Theory]
    [InlineData("2 # 3", 2)]
    [InlineData("sin(30)$", 7)]
    [InlineData("1+@", 2)]
    public void InvalidCharacter(string expr, int pos) => AssertError(expr, pos, "Ký tự không hợp lệ");

    // ---------- Thiếu ngoặc: trỏ vào '(' chưa được đóng ----------
    [Theory]
    [InlineData("(2+3", 0)]
    [InlineData("2*(3+(4-1)", 2)]
    [InlineData("sin(30", 3)]
    [InlineData("((1)", 0)]
    public void MissingCloseParen(string expr, int pos) => AssertError(expr, pos, "Thiếu dấu ')'");

    // ---------- Thừa ngoặc: trỏ vào ')' thừa ----------
    [Theory]
    [InlineData("2+3)", 3)]
    [InlineData("(1))", 3)]
    [InlineData(")", 0)]
    public void ExtraCloseParen(string expr, int pos)
    {
        var ex = Fail(expr);
        Assert.Equal(pos, ex.Position);
        Assert.True(ex.Message.Contains("Thừa dấu ')'") || ex.Message.Contains("Thiếu toán hạng"), ex.Message);
    }

    // ---------- Thiếu toán hạng ----------
    [Theory]
    [InlineData("2+", 2)]
    [InlineData("2*", 2)]
    [InlineData("*3", 0)]
    [InlineData("2*/3", 2)]
    [InlineData("2^", 2)]
    [InlineData("()", 1)]
    [InlineData("(2+)", 3)]
    [InlineData("!", 0)]
    [InlineData("-", 1)]
    [InlineData("", 0)]
    [InlineData("sqrt()", 5)]
    public void MissingOperand(string expr, int pos)
    {
        var ex = Fail(expr);
        Assert.Equal(pos, ex.Position);
        Assert.True(ex.Message.Contains("Thiếu") || ex.Message.Contains("rỗng"), ex.Message);
    }

    // ---------- Thiếu toán tử / ký hiệu không mong đợi ----------
    [Theory]
    [InlineData("2 3", 2, "Thiếu toán tử")]
    [InlineData("(1)2", 3, "Thiếu toán tử")]
    [InlineData("2,3", 1, "không hợp lệ")]
    public void UnexpectedToken(string expr, int pos, string keyword) => AssertError(expr, pos, keyword);

    // ---------- Chia cho 0: trỏ vào toán tử ----------
    [Theory]
    [InlineData("1/0", 1)]
    [InlineData("5 / (2-2)", 2)]
    [InlineData("5%0", 1)]
    [InlineData("0^-1", 1)]
    [InlineData("1 + 2/(1-1)", 5)]
    public void DivideByZero(string expr, int pos) => AssertError(expr, pos, "chia cho 0");

    // ---------- Miền xác định: trỏ vào tên hàm / toán tử ----------
    [Theory]
    [InlineData("sqrt(-1)", 0)]
    [InlineData("1+sqrt(-4)", 2)]
    [InlineData("ln(0)", 0)]
    [InlineData("ln(-1)", 0)]
    [InlineData("log(-5)", 0)]
    [InlineData("asin(2)", 0)]
    [InlineData("acos(-1.5)", 0)]
    [InlineData("tan(90)", 0)]
    [InlineData("tan(-270)", 0)]
    [InlineData("(-8)^(1/3)", 4)]
    public void Domain(string expr, int pos) => AssertError(expr, pos, "miền xác định");

    [Fact]
    public void Domain_TanPiOver2_InRadians() =>
        AssertError("tan(pi/2)", 0, "miền xác định", new EvalContext(AngleMode.Rad));

    // ---------- Giai thừa: trỏ vào '!' (hoặc tên hàm fact) ----------
    [Theory]
    [InlineData("2.5!", 3, "số nguyên không âm")]
    [InlineData("(-3)!", 4, "số nguyên không âm")]
    [InlineData("fact(2.5)", 0, "số nguyên không âm")]
    [InlineData("171!", 3, "quá lớn")]
    [InlineData("1000!", 4, "quá lớn")]
    [InlineData("fact(200)", 0, "quá lớn")]
    public void Factorial(string expr, int pos, string keyword) => AssertError(expr, pos, keyword);

    // ---------- Tràn số ----------
    [Theory]
    [InlineData("10^400", 2)]
    [InlineData("1e300*1e300", 5)]
    [InlineData("1e400", 0)]
    [InlineData("170!*1000", 4)]
    public void Overflow(string expr, int pos) => AssertError(expr, pos, "Tràn số");

    // ---------- Tên không xác định / dùng sai ----------
    [Theory]
    [InlineData("foo(2)", 0, "không xác định")]
    [InlineData("2+bar", 2, "không xác định")]
    [InlineData("sin 30", 4, "cần dấu '('")]
    [InlineData("sqrt+1", 4, "cần dấu '('")]
    [InlineData("2x", 1, "vẽ đồ thị")]
    public void Names(string expr, int pos, string keyword) => AssertError(expr, pos, keyword);
}
