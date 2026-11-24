using SmartCalc.Core.Input;
using SmartCalc.Core.Programmer;

namespace SmartCalc.Core.Tests.Input;

public class CursorEditorTests
{
    [Theory]
    [InlineData("1234", 2, "9", "12934", 3)]
    [InlineData("12+34", 3, "5", "12+534", 4)]
    [InlineData("2+99", 2, "×", "2×99", 2)]
    [InlineData("2×9", 2, "−", "2×−9", 3)]
    [InlineData("", 0, "abs()", "abs()", 4)]
    [InlineData("2", 1, "^()", "2^()", 3)]
    [InlineData("2+", 2, "^()", "2^()", 3)]
    [InlineData("", 0, "sin()", "sin()", 4)]
    [InlineData("", 0, "√()", "√()", 2)]
    [InlineData("abs()", 4, "sin()", "abs(sin())", 8)]
    [InlineData("abs(8)", 5, ")", "abs(8)", 6)]
    [InlineData("2^(3)", 4, ")", "2^(3)", 5)]
    [InlineData("2", 1, ")", "2)", 2)]
    public void Insert_ReturnsExpressionAndCursor(string source, int cursor, string text, string expected, int next)
        => Assert.Equal((expected, next), ExpressionEditor.Append(source, cursor, text));

    [Theory]
    [InlineData("123", 2, "13", 1)]
    [InlineData("123", 0, "123", 0)]
    [InlineData("abs()", 4, "", 0)]
    [InlineData("2^()", 3, "2", 1)]
    [InlineData("2+sin()", 6, "2+", 2)]
    [InlineData("sqrt()", 5, "", 0)]
    [InlineData("√()", 2, "", 0)]
    [InlineData("Ans+3", 3, "+3", 0)]
    [InlineData("sin(30)+4", 5, "sin(0)+4", 4)]
    [InlineData("abs(8)", 5, "abs()", 4)]
    [InlineData("2+sin(", 6, "2+", 2)]
    public void Backspace_DeletesBeforeCursor(string source, int cursor, string expected, int next)
        => Assert.Equal((expected, next), ExpressionEditor.DeleteLast(source, cursor));

    [Theory]
    [InlineData("sin(Ans)+abs(2)", 0, 0, 4)]
    [InlineData("sin(Ans)+abs(2)", 4, 0, 7)]
    [InlineData("sin(Ans)+abs(2)", 7, 4, 8)]
    [InlineData("sin(Ans)+abs(2)", 13, 9, 14)]
    [InlineData("2^()", 1, 0, 3)]
    [InlineData("2^()", 3, 1, 4)]
    [InlineData("√()", 2, 0, 3)]
    [InlineData("", 0, 0, 0)]
    public void Arrows_SkipWholeFunctionAndConstant(string source, int cursor, int left, int right)
    {
        Assert.Equal((source, left), ExpressionEditor.MoveLeft(source, cursor));
        Assert.Equal((source, right), ExpressionEditor.MoveRight(source, cursor));
        Assert.Equal((source, 0), ExpressionEditor.Home(source, cursor));
        Assert.Equal((source, source.Length), ExpressionEditor.End(source, cursor));
    }

    [Fact]
    public void MousePositions_SnapToEditingBoundaries()
    {
        const string source = "sin(Ans)+abs(2)";
        Assert.Equal([0, 4, 7, 8, 9, 13, 14, 15], ExpressionEditor.CursorStops(source));
        for (int position = -3; position <= source.Length + 3; position++)
            Assert.Contains(ExpressionEditor.SetCursor(source, position).CursorPosition, ExpressionEditor.CursorStops(source));
    }

    [Fact]
    public void ToggleNegate_AlwaysMovesToEnd()
    {
        Assert.Equal(("−(1+2)", 6), ExpressionEditor.ToggleNegate("1+2", 1));
        Assert.Equal(("1+2", 3), ExpressionEditor.ToggleNegate("−(1+2)", 1));
    }
}

public class CursorStateMachineTests
{
    private static void Press(InputStateMachine machine, params string[] keys)
    {
        foreach (string key in keys)
        {
            switch (key)
            {
                case "◀": machine.MoveLeft(); break;
                case "▶": machine.MoveRight(); break;
                case "Home": machine.Home(); break;
                case "End": machine.End(); break;
                case "⌫": machine.Process(InputEvent.Backspace); break;
                case "=": machine.Process(InputEvent.EqualsKey); break;
                default: machine.Process(InputEvent.FromText(key)); break;
            }
            Assert.InRange(machine.CursorPosition, 0, machine.Expression.Length);
        }
    }

    [Theory]
    [InlineData("abs() 8 =", "8", "abs(8)")]
    [InlineData("abs() 8 ▶ + 2 =", "10", "abs(8)+2")]
    [InlineData("2 ^() 3 ▶ + 1 =", "9", "2^(3)+1")]
    [InlineData("abs() − 3 ) × 2 =", "6", "abs(−3)×2")]
    [InlineData("sin() 30 ) + 1 =", "1.5", "sin(30)+1")]
    [InlineData("abs() sin() 30 ) ) × 2 =", "1", "abs(sin(30))×2")]
    public void TemplateKeyScenarios(string keys, string result, string expression)
    {
        var machine = new InputStateMachine();
        Press(machine, keys.Split(' '));
        Assert.Equal(InputState.ShowingResult, machine.State);
        Assert.Equal(result, machine.Result);
        Assert.Equal(expression, machine.Expression);
    }

    [Fact]
    public void InsertAndDeleteInMiddle_DoNotChangeDfaTables()
    {
        var machine = new InputStateMachine();
        Press(machine, "12+34", "Home", "▶", "▶", "▶", "5");
        Assert.Equal("12+534", machine.Expression);
        Assert.Equal(4, machine.CursorPosition);
        Press(machine, "⌫", "Home", "⌫");
        Assert.Equal("12+34", machine.Expression);
        Assert.Equal(InputState.Typing, machine.State);
        Assert.Equal(0, machine.CursorPosition);
        Assert.Equal(40, InputStateMachine.Table.Count);
        Assert.Equal(40, InputStateMachine.ActionTable.Count);
        Assert.Equal(10, Enum.GetValues<InputSymbol>().Length);
    }

    [Fact]
    public void Navigation_DoesNotEvaluateCommitOrChangeState()
    {
        var machine = new InputStateMachine();
        int commits = 0;
        machine.Evaluated += (_, _) => commits++;
        Press(machine, "abs()", "8");
        string? preview = machine.Preview;
        Press(machine, "Home", "▶", "End", "◀");
        Assert.Equal("abs(8)", machine.Expression);
        Assert.Equal(InputState.Typing, machine.State);
        Assert.Equal(preview, machine.Preview);
        Assert.Equal(0, commits);
        Press(machine, "=");
        int cursor = machine.CursorPosition;
        Press(machine, "Home", "End", "◀", "▶");
        machine.SetCursorPosition(0);
        Assert.Equal(cursor, machine.CursorPosition);
        Assert.Equal(InputState.ShowingResult, machine.State);
        Assert.Equal(1, commits);
        Press(machine, "1/0", "=");
        cursor = machine.CursorPosition;
        Press(machine, "Home", "End", "◀", "▶");
        Assert.Equal(InputState.Error, machine.State);
        Assert.Equal(cursor, machine.CursorPosition);
    }

    [Fact]
    public void EmptyTemplate_BackspaceClearsPair()
    {
        var machine = new InputStateMachine();
        Press(machine, "abs()", "⌫");
        Assert.Equal(InputState.Empty, machine.State);
        Assert.Equal("", machine.Expression);
        Assert.Equal(0, machine.CursorPosition);
        Press(machine, "2", "^()", "⌫");
        Assert.Equal("2", machine.Expression);
        Assert.Equal(1, machine.CursorPosition);
    }

    [Fact]
    public void LoadNegateAndResultContinuation_SetCursorCorrectly()
    {
        var machine = new InputStateMachine();
        machine.Process(InputEvent.Load("abs()"));
        Assert.Equal(5, machine.CursorPosition);
        machine.Process(InputEvent.Clear);
        Press(machine, "2", "=", "^()", "3", ")", "=");
        Assert.Equal("8", machine.Result);
        Press(machine, "sin()");
        Assert.Equal("sin()", machine.Expression);
        Assert.Equal(4, machine.CursorPosition);
        machine.Process(InputEvent.Negate);
        Assert.Equal(machine.Expression.Length, machine.CursorPosition);
        machine.Process(InputEvent.Clear);
        Assert.Equal(0, machine.CursorPosition);
    }

    [Fact]
    public void Programmer_UsesSameCursorWithoutPowerTemplates()
    {
        var machine = new InputStateMachine(new ProgrammerEngine());
        Press(machine, "2", "^", "3", "◀", "1", "=");
        Assert.Equal("2^13", machine.Expression);
        Assert.Equal("15", machine.Result);
    }
}

public class CursorLayoutTests
{
    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("abs()", 4, 0)]
    [InlineData("abs(8)", 4, 0)]
    [InlineData("abs(8)", 5, 0)]
    [InlineData("2^()", 3, 1)]
    [InlineData("2^()", 4, 0)]
    [InlineData("2^(3)+1", 4, 1)]
    [InlineData("2^(3)+1", 5, 0)]
    [InlineData("2^(3^(4))", 6, 2)]
    [InlineData("2^(3^(4))", 7, 2)]
    [InlineData("2^(3^(4))", 8, 1)]
    [InlineData("2^(3^(4))", 9, 0)]
    [InlineData("2^3", 1, 0)]
    [InlineData("2^3", 2, 1)]
    public void Caret_UsesSourcePositionAndExponentLevel(string source, int position, int level)
    {
        var layout = ExpressionLayout.Create(source, cursorPosition: position);
        var caret = Assert.Single(layout.Elements, e => e.Kind == ExpressionElementKind.Caret);
        Assert.Equal(position, caret.SourceStart);
        Assert.Equal(level, caret.SuperscriptLevel);
    }

    [Theory]
    [InlineData("abs()", "||")]
    [InlineData("sin()", "sin()")]
    [InlineData("2^()", "2")]
    public void EmptyTemplates_HaveOnePlaceholder(string source, string text)
    {
        var layout = ExpressionLayout.Create(source);
        Assert.Single(layout.Elements, e => e.Kind == ExpressionElementKind.Placeholder);
        Assert.Equal(text, string.Concat(layout.Elements.Select(e => e.Text)));
    }

    [Fact]
    public void Caret_DoesNotBreakErrorMappingOrProgrammerRawText()
    {
        var layout = ExpressionLayout.Create("2^(3)+1", errorPosition: 1, cursorPosition: 4);
        Assert.Equal("3", Assert.Single(layout.Elements, e => e.IsError).Text);
        Assert.True(layout.Elements[layout.FindElement(1)].IsError);
        var raw = ExpressionLayout.Create("F0^FF", mathNotation: false, cursorPosition: 3);
        Assert.Equal("F0^FF", string.Concat(raw.Elements.Select(e => e.Text)));
        Assert.All(raw.Elements, e => Assert.Equal(0, e.SuperscriptLevel));
    }
}
