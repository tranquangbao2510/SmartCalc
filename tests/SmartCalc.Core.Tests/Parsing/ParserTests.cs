using SmartCalc.Core.Parsing;

namespace SmartCalc.Core.Tests.Parsing;

/// <summary>Kiểm tra HÌNH DẠNG cây AST (in dạng tiền tố) — chứng minh thứ tự ưu tiên và tính kết hợp.</summary>
public class ParserTests
{
    private static string Tree(string input, bool allowVariable = false) =>
        AstPrinter.ToPrefix(Parser.Parse(input, allowVariable));

    [Theory]
    // Ưu tiên: * / % trên + -
    [InlineData("2+3*4", "(+ 2 (* 3 4))")]
    [InlineData("2*3+4", "(+ (* 2 3) 4)")]
    [InlineData("(2+3)*4", "(* (+ 2 3) 4)")]
    [InlineData("8-6%4", "(- 8 (% 6 4))")]
    // Kết hợp trái cho + - * /
    [InlineData("10-2-3", "(- (- 10 2) 3)")]
    [InlineData("100/10/5", "(/ (/ 100 10) 5)")]
    // ^ kết hợp phải và ưu tiên trên * /
    [InlineData("2^3^2", "(^ 2 (^ 3 2))")]
    [InlineData("2*3^2", "(* 2 (^ 3 2))")]
    // Dấu một ngôi thấp hơn ^ nhưng cao hơn * /
    [InlineData("-2^2", "(neg (^ 2 2))")]
    [InlineData("(-2)^2", "(^ (neg 2) 2)")]
    [InlineData("2^-1", "(^ 2 (neg 1))")]
    [InlineData("-2*3", "(* (neg 2) 3)")]
    [InlineData("2*-3", "(* 2 (neg 3))")]
    [InlineData("--2", "(neg (neg 2))")]
    [InlineData("+2", "(pos 2)")]
    // Giai thừa: cao nhất
    [InlineData("3!", "(! 3)")]
    [InlineData("3!!", "(! (! 3))")]
    [InlineData("-3!", "(neg (! 3))")]
    [InlineData("2^3!", "(^ 2 (! 3))")]
    [InlineData("3!^2", "(^ (! 3) 2)")]
    // Nhân ngầm
    [InlineData("2pi", "(* 2 pi)")]
    [InlineData("3(4+1)", "(* 3 (+ 4 1))")]
    [InlineData("(1+1)(2+2)", "(* (+ 1 1) (+ 2 2))")]
    [InlineData("2sin(30)", "(* 2 (sin 30))")]
    [InlineData("2pi^2", "(* 2 (^ pi 2))")]
    [InlineData("1+2pi", "(+ 1 (* 2 pi))")]
    // Hàm, hằng
    [InlineData("sqrt(16)", "(sqrt 16)")]
    [InlineData("sin(cos(0))", "(sin (cos 0))")]
    [InlineData("ln(e)", "(ln e)")]
    [InlineData("Ans*2", "(* Ans 2)")]
    [InlineData("SIN(PI)", "(sin pi)")]
    [InlineData("1.5e-3*2", "(* 1.5e-3 2)")]
    // Ví dụ trong docs/grammar.md
    [InlineData("-2^2 + 3(1+1)!", "(+ (neg (^ 2 2)) (* 3 (! (+ 1 1))))")]
    public void Tree_Shape(string input, string expected)
    {
        Assert.Equal(expected, Tree(input));
    }

    [Theory]
    [InlineData("2x", "(* 2 x)")]
    [InlineData("x^2+3x-1", "(- (+ (^ x 2) (* 3 x)) 1)")]
    [InlineData("sin(x)", "(sin x)")]
    public void Variable_WhenAllowed(string input, string expected)
    {
        Assert.Equal(expected, Tree(input, allowVariable: true));
    }

    [Fact]
    public void Nodes_KeepOperatorPositions()
    {
        var root = Assert.IsType<BinaryNode>(Parser.Parse("12 + 3/4"));
        Assert.Equal(BinaryOp.Add, root.Op);
        Assert.Equal(3, root.Position);
        var div = Assert.IsType<BinaryNode>(root.Right);
        Assert.Equal(6, div.Position);
    }

    [Fact]
    public void ImplicitMultiplication_IsMarked()
    {
        var node = Assert.IsType<BinaryNode>(Parser.Parse("3(4)"));
        Assert.True(node.Implicit);
        Assert.Equal(1, node.Position);
        Assert.False(Assert.IsType<BinaryNode>(Parser.Parse("3*4")).Implicit);
    }
}
