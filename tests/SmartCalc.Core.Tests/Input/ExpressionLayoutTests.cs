using SmartCalc.Core.Input;

namespace SmartCalc.Core.Tests.Input;

public class ExpressionLayoutTests
{
    public static IEnumerable<object[]> Examples()
    {
        yield return ["2^3", "23", "01", 0];
        yield return ["2^(x+1)", "2x+1", "0111", 0];
        yield return ["2^3^2", "232", "012", 0];
        yield return ["2^", "2", "0", 1];
        yield return ["2^(3+", "23+", "011", 1];
        yield return ["5^(−1)", "5−1", "011", 0];
        yield return ["abs(-5)", "|−5|", "", 0];
        yield return ["abs(abs(-2)-5)", "||−2|−5|", "", 0];
        yield return ["abs(3", "|3", "", 0];
        yield return ["sin(30)+2^3×(4−1)", "sin(30)+23×(4−1)", "0000000001000000", 0];
        yield return ["2^123.45", "2123.45", "0111111", 0];
        yield return ["2^1e-3", "21e−3", "01111", 0];
        yield return ["2^pi+3^π+4^e+5^Ans", "2pi+3π+4e+5Ans", "01100100100111", 0];
        yield return ["2^sin(30)+5", "2sin(30)+5", "0111111100", 0];
        yield return ["2^sin(30", "2sin(30", "0111111", 1];
        yield return ["2^-3+4", "2−3+4", "01100", 0];
        yield return ["2^(3^2)+4", "232+4", "01200", 0];
        yield return ["2^3^", "23", "01", 1];
        yield return ["2^^3", "23", "01", 1];
        yield return ["2^()", "2", "0", 1];
        yield return ["2^(3)+(4)", "23+(4)", "010000", 0];
        yield return ["2^(3×(4+1))", "23×(4+1)", "01111111", 0];
        yield return ["abs((2)-3)", "|(2)−3|", "", 0];
        yield return ["abs(sin(30)-abs(-2))", "|sin(30)−|−2||", "", 0];
        yield return ["ABS (3", "|3", "", 0];
        yield return ["abs(2^3)", "|23|", "0010", 0];
        yield return ["2^abs(-5)", "2|−5|", "01111", 0];
        yield return ["2^3!", "23!", "011", 0];
        yield return ["cos(2)+sqrt(4)", "cos(2)+sqrt(4)", "", 0];
        yield return ["2 ^ ( 3 + 4 ) + 5", "2   3 + 4  + 5", "00111111110000", 0];
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Layout_FormatsWithoutChangingSource(string source, string text, string levels, int placeholders)
    {
        var layout = ExpressionLayout.Create(source);
        Assert.False(layout.IsRawText);
        Assert.Equal(text, string.Concat(layout.Elements.Select(e => e.Text)));
        var visible = layout.Elements.Where(e => e.Kind != ExpressionElementKind.Placeholder).ToArray();
        Assert.Equal(levels.Length == 0 ? new string('0', text.Length) : levels,
            string.Concat(visible.Select(e => e.SuperscriptLevel.ToString())));
        Assert.Equal(placeholders, layout.Elements.Count(e => e.Kind == ExpressionElementKind.Placeholder));
        Assert.All(layout.Elements.Where(e => e.Kind == ExpressionElementKind.Placeholder), e => Assert.True(e.SuperscriptLevel > 0));
        Assert.All(layout.Elements, e => Assert.InRange(e.SourceStart, 0, source.Length));
    }

    [Theory]
    [InlineData("2^3", 1, "3", 2, 1)]
    [InlineData("2^(x+1)", 1, "x", 3, 1)]
    [InlineData("2^(x+1)", 2, "x", 3, 1)]
    [InlineData("2^(x+1)", 4, "+", 4, 1)]
    [InlineData("2^(x+1)+4", 6, "+", 7, 0)]
    [InlineData("2^3^2", 3, "2", 4, 2)]
    [InlineData("2^^3", 1, "", 2, 1)]
    [InlineData("2^^3", 2, "3", 3, 1)]
    [InlineData("2^", 1, "", 2, 1)]
    [InlineData("2^(3+", 5, "", 5, 1)]
    [InlineData("abs(-5)", 0, "|", 0, 0)]
    [InlineData("abs(-5)", 1, "|", 0, 0)]
    [InlineData("abs(-5)", 2, "|", 0, 0)]
    [InlineData("abs(-5)", 3, "|", 0, 0)]
    [InlineData("abs(-5)", 4, "−", 4, 0)]
    [InlineData("abs(-5)", 6, "|", 6, 0)]
    [InlineData("2^(3)", 4, " ", 5, 0)]
    [InlineData("2+", 2, " ", 2, 0)]
    public void Errors_MapOriginalPositionToVisibleElement(string source, int position, string text, int start, int level)
    {
        var layout = ExpressionLayout.Create(source, errorPosition: position);
        var marked = Assert.Single(layout.Elements, e => e.IsError);
        Assert.Equal(text, marked.Text);
        Assert.Equal(start, marked.SourceStart);
        Assert.Equal(level, marked.SuperscriptLevel);
        Assert.Same(marked, layout.Elements[layout.FindElement(position)]);
    }

    public static IEnumerable<object[]> Sources() => Examples().Select(example => new[] { example[0] });

    [Theory]
    [MemberData(nameof(Sources))]
    public void EveryOriginalPosition_CanBeHighlighted(string source)
    {
        for (int position = 0; position <= source.Length; position++)
        {
            var layout = ExpressionLayout.Create(source, errorPosition: position);
            var marked = Assert.Single(layout.Elements, e => e.IsError);
            Assert.Same(marked, layout.Elements[layout.FindElement(position)]);
        }
    }

    [Theory]
    [InlineData("2^3#")]
    [InlineData("abs(-5)+@")]
    [InlineData("1.2.3")]
    [InlineData("2^.")]
    public void LexerFailure_FallsBackToExactRawText(string source)
    {
        var layout = ExpressionLayout.Create(source, errorPosition: source.Length - 1);
        Assert.True(layout.IsRawText);
        Assert.Equal(source, string.Concat(layout.Elements.Select(e => e.Text)));
        Assert.All(layout.Elements, e => Assert.Equal(0, e.SuperscriptLevel));
        Assert.True(layout.Elements[^1].IsError);
    }

    [Theory]
    [InlineData("2^3")]
    [InlineData("F0^FF")]
    [InlineData("abs(-5)")]
    public void ProgrammerMode_KeepsExactRawText(string source)
    {
        var layout = ExpressionLayout.Create(source, mathNotation: false, errorPosition: 1);
        Assert.True(layout.IsRawText);
        Assert.Equal(source, string.Concat(layout.Elements.Select(e => e.Text)));
        Assert.All(layout.Elements, e => Assert.Equal(0, e.SuperscriptLevel));
        Assert.True(layout.Elements[1].IsError);
    }

    [Fact]
    public void NestedPowers_ShrinkButNeverBelowFiftyFivePercent()
    {
        var layout = ExpressionLayout.Create("2^3^4^5^6^7^8^9");
        Assert.Equal(Enumerable.Range(0, 8), layout.Elements.Select(e => e.SuperscriptLevel));
        Assert.Equal(1, layout.Elements[0].FontScale);
        Assert.Equal(.75, layout.Elements[1].FontScale);
        Assert.Equal(.5625, layout.Elements[2].FontScale);
        Assert.Equal(.55, layout.Elements[^1].FontScale);
        Assert.All(layout.Elements, e => Assert.InRange(e.FontScale, .55, 1));
    }

    [Fact]
    public void VeryDeepInput_FallsBackInsteadOfOverflowingStack()
    {
        string source = new string('(', 10_000) + "2^3" + new string(')', 10_000);
        var layout = ExpressionLayout.Create(source);
        Assert.True(layout.IsRawText);
        Assert.Equal(source, string.Concat(layout.Elements.Select(e => e.Text)));
    }

    [Fact]
    public void Layout_DoesNotChangeMachineOrHistoryText()
    {
        const string source = "2^3+abs(-5)";
        var machine = new InputStateMachine();
        string? history = null;
        machine.Evaluated += (expression, _) => history = expression;
        machine.Process(InputEvent.FromText(source));
        Assert.Equal("23+|−5|", string.Concat(ExpressionLayout.Create(machine.Expression).Elements.Select(e => e.Text)));
        Assert.Equal(source, machine.Expression);
        machine.Process(InputEvent.EqualsKey);
        Assert.Equal(source, machine.Expression);
        Assert.Equal(source, history);
        Assert.Equal("13", machine.Result);
    }
}
