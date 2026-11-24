using SmartCalc.Core.Input;

namespace SmartCalc.Core.Tests.Input;

public class ExpressionEditorTests
{
    [Theory]
    [InlineData("12", "12", "1")]
    [InlineData("2+sin(", "2+sin(", "2+")]
    [InlineData("Ans×", "Ans×", "Ans")]
    [InlineData("Ans", "Ans", "")]
    [InlineData("2(", "2(", "2")]
    [InlineData("3π", "3π", "3")]
    [InlineData("x", "x", "")]
    public void DeleteLast(string _, string input, string expected)
    {
        Assert.Equal(expected, ExpressionEditor.DeleteLast(input));
    }

    [Theory]
    [InlineData("5", "−(5)")]
    [InlineData("−(5)", "5")]
    [InlineData("−(1)+(2)", "−(−(1)+(2))")] // ngoặc ở vị trí 1 không bao cả biểu thức -> bọc thêm
    public void ToggleNegate(string input, string expected)
    {
        Assert.Equal(expected, ExpressionEditor.ToggleNegate(input));
    }

    [Theory]
    [InlineData("Ans×2", 42, "42×2")]
    [InlineData("Ans^2", -3, "(−3)^2")]
    [InlineData("2Ans", 42, "2(42)")]
    [InlineData("πAns", 1.5, "π(1.5)")]
    [InlineData("Ans+Ans", 7, "7+7")]
    [InlineData("sin(Ans)", 30, "sin(30)")]
    [InlineData("Answer+1", 7, "Answer+1")]   // không thay bên trong tên dài hơn
    [InlineData("1+2", 7, "1+2")]
    public void SubstituteAns(string input, double ans, string expected)
    {
        string result = ExpressionEditor.SubstituteAns(input, ans);
        Assert.Equal(expected, result);
        if (!input.Contains("Answer"))
            Assert.Equal(Calculator.Evaluate(input, new(Ans: ans)), Calculator.Evaluate(result), 12);
    }

    [Theory]
    [InlineData(5, "5")]
    [InlineData(-2.5, "(−2.5)")]
    [InlineData(1e30, "1E+30")]
    public void FormatForInsert(double value, string expected)
    {
        Assert.Equal(expected, ExpressionEditor.FormatForInsert(value));
        Assert.Equal(value, Calculator.Evaluate(ExpressionEditor.FormatForInsert(value)));
    }
}
