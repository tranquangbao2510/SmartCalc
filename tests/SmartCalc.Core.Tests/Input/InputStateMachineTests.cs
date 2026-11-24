using SmartCalc.Core.Evaluation;
using SmartCalc.Core.Input;

namespace SmartCalc.Core.Tests.Input;

public class InputStateMachineTests
{
    private static ScientificEngine Sci(InputStateMachine m) => (ScientificEngine)m.Engine;

    private static InputStateMachine Type(params string[] keys)
    {
        var m = new InputStateMachine();
        Press(m, keys);
        return m;
    }

    // "=" "C" "<" (backspace) "±" là phím lệnh, còn lại là text
    private static void Press(InputStateMachine m, params string[] keys)
    {
        foreach (var k in keys)
        {
            m.Process(k switch
            {
                "=" => InputEvent.EqualsKey,
                "C" => InputEvent.Clear,
                "<" => InputEvent.Backspace,
                "±" => InputEvent.Negate,
                _ => InputEvent.FromText(k),
            });
        }
    }

    // ---------- Bảng đầy đủ ----------
    [Fact]
    public void Table_CoversEveryStateAndKind()
    {
        foreach (var s in Enum.GetValues<InputState>())
            foreach (var k in Enum.GetValues<InputSymbol>())
                Assert.True(InputStateMachine.Table.ContainsKey((s, k)), $"Thiếu ô ({s}, {k})");
        Assert.Equal(
            Enum.GetValues<InputState>().Length * Enum.GetValues<InputSymbol>().Length,
            InputStateMachine.Table.Count);
    }

    [Fact]
    public void ActionTable_CoversEveryStateAndSymbol()
    {
        foreach (var state in Enum.GetValues<InputState>())
            foreach (var symbol in Enum.GetValues<InputSymbol>())
                Assert.True(InputStateMachine.ActionTable.ContainsKey((state, symbol)));
        Assert.Equal(InputStateMachine.Table.Count, InputStateMachine.ActionTable.Count);
    }

    // ---------- Empty ----------
    [Fact]
    public void Starts_Empty()
    {
        var m = new InputStateMachine();
        Assert.Equal(InputState.Empty, m.State);
        Assert.Equal("", m.Expression);
    }

    [Fact]
    public void Empty_Digit_GoesTyping()
    {
        var m = Type("7");
        Assert.Equal(InputState.Typing, m.State);
        Assert.Equal("7", m.Expression);
    }

    [Fact]
    public void Empty_Equals_Ignored()
    {
        var m = Type("=");
        Assert.Equal(InputState.Empty, m.State);
    }

    [Fact]
    public void Empty_Negate_StartsWithMinus()
    {
        var m = Type("±", "5");
        Assert.Equal("−5", m.Expression);
        Assert.Equal("-5", m.Preview);
    }

    // ---------- Typing ----------
    [Fact]
    public void Typing_ShowsPreview_WhenValid()
    {
        var m = Type("2", "+", "3");
        Assert.Equal("2+3", m.Expression);
        Assert.Equal("5", m.Preview);
        Assert.Equal(5, m.CurrentValue);

        Press(m, "×");
        Assert.Null(m.Preview); // "2+3×" chưa hợp lệ
    }

    [Fact]
    public void Typing_Equals_Success_GoesShowingResult()
    {
        var m = Type("2", "+", "3", "×", "4", "=");
        Assert.Equal(InputState.ShowingResult, m.State);
        Assert.Equal("14", m.Result);
        Assert.Equal("2+3×4", m.Expression);
        Assert.Equal(14, Sci(m).Ans);
        Assert.Null(m.Preview);
    }

    [Fact]
    public void Typing_Equals_Failure_GoesError_WithPosition()
    {
        var m = Type("1", "÷", "0", "=");
        Assert.Equal(InputState.Error, m.State);
        Assert.Equal(1, m.ErrorPosition);
        Assert.Contains("chia cho 0", m.ErrorMessage);
        Assert.Equal("", m.Result);
    }

    [Fact]
    public void Typing_Backspace_DeletesLast_ThenEmpty()
    {
        var m = Type("1", "2", "<");
        Assert.Equal("1", m.Expression);
        Assert.Equal(InputState.Typing, m.State);
        Press(m, "<");
        Assert.Equal(InputState.Empty, m.State);
        Assert.Equal("", m.Expression);
    }

    [Fact]
    public void Typing_Backspace_RemovesWholeFunctionName()
    {
        var m = Type("2", "+", "sin(");
        Press(m, "<");
        Assert.Equal("2+", m.Expression);
    }

    [Fact]
    public void Typing_Clear_GoesEmpty()
    {
        var m = Type("1", "+", "C");
        Assert.Equal(InputState.Empty, m.State);
        Assert.Equal("", m.Expression);
    }

    [Fact]
    public void Typing_Operator_ReplacesPreviousOperator()
    {
        var m = Type("2", "+", "×");
        Assert.Equal("2×", m.Expression);
        Press(m, "−", "3");          // trừ thì không thay (cho phép 2×−3)
        Assert.Equal("2×−3", m.Expression);
        Assert.Equal("-6", m.Preview);
    }

    [Fact]
    public void Typing_Negate_TogglesWholeExpression()
    {
        var m = Type("2", "+", "3", "±");
        Assert.Equal("−(2+3)", m.Expression);
        Assert.Equal("-5", m.Preview);
        Press(m, "±");
        Assert.Equal("2+3", m.Expression);
    }

    // ---------- ShowingResult ----------
    [Fact]
    public void ShowingResult_Digit_StartsNewExpression()
    {
        var m = Type("2", "+", "3", "=", "7");
        Assert.Equal(InputState.Typing, m.State);
        Assert.Equal("7", m.Expression);
        Assert.Equal("", m.Result);
    }

    [Fact]
    public void ShowingResult_Function_StartsNewExpression()
    {
        var m = Type("2", "=", "sin(");
        Assert.Equal("sin(", m.Expression);
    }

    [Fact]
    public void ShowingResult_Operator_ContinuesFromAns()
    {
        var m = Type("2", "+", "3", "=", "×", "2");
        Assert.Equal(InputState.Typing, m.State);
        Assert.Equal("Ans×2", m.Expression);
        Assert.Equal("10", m.Preview);
        Press(m, "=");
        Assert.Equal("10", m.Result);
        Assert.Equal(10, Sci(m).Ans);
    }

    [Fact]
    public void ShowingResult_PostfixOperator_ContinuesFromAns()
    {
        var m = Type("3", "=", "!", "=");
        Assert.Equal("6", m.Result);
    }

    [Fact]
    public void ShowingResult_Equals_And_Backspace_Ignored()
    {
        var m = Type("4", "=", "=", "<");
        Assert.Equal(InputState.ShowingResult, m.State);
        Assert.Equal("4", m.Result);
    }

    [Fact]
    public void ShowingResult_Negate_GivesMinusAns()
    {
        var m = Type("4", "=", "±");
        Assert.Equal("−Ans", m.Expression);
        Assert.Equal("-4", m.Preview);
    }

    // ---------- Error ----------
    [Fact]
    public void Error_Digit_ClearsError_AndStartsFresh()
    {
        var m = Type("1", "÷", "0", "=", "5");
        Assert.Equal(InputState.Typing, m.State);
        Assert.Equal("5", m.Expression);
        Assert.Equal("", m.ErrorMessage);
        Assert.Equal(-1, m.ErrorPosition);
    }

    [Fact]
    public void Error_Operator_ProcessedAsEmpty_NotFromAns()
    {
        var m = Type("1", "÷", "0", "=", "+");
        Assert.Equal(InputState.Typing, m.State);
        Assert.Equal("+", m.Expression);
    }

    [Theory]
    [InlineData("=")]
    [InlineData("<")]
    [InlineData("C")]
    public void Error_CommandKeys_ClearToEmpty(string key)
    {
        var m = Type("(", "=");
        Assert.Equal(InputState.Error, m.State);
        Press(m, key);
        Assert.Equal(InputState.Empty, m.State);
        Assert.Equal("", m.Expression);
    }

    [Fact]
    public void Error_KeepsAnsFromLastSuccess()
    {
        var m = Type("7", "=", "1", "÷", "0", "=", "Ans", "=");
        Assert.Equal("7", m.Result);
    }

    // ---------- Load (lịch sử) ----------
    [Theory]
    [InlineData("", InputState.Empty)]
    [InlineData("1 +", InputState.Typing)]
    [InlineData("1 =", InputState.ShowingResult)]
    [InlineData("1 ÷ 0 =", InputState.Error)]
    public void Load_FromAnyState_GoesTyping(string before, InputState expectedBefore)
    {
        var m = Type(before.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(expectedBefore, m.State);
        m.Process(InputEvent.Load("2×(3+4)"));
        Assert.Equal(InputState.Typing, m.State);
        Assert.Equal("2×(3+4)", m.Expression);
        Assert.Equal("14", m.Preview);
    }

    // ---------- Ans, góc, sự kiện ----------
    [Fact]
    public void AngleMode_AffectsEvaluation()
    {
        var m = Type("sin(", "9", "0", ")");
        Assert.Equal("1", m.Preview);
        Sci(m).AngleMode = AngleMode.Rad;
        m.Refresh();
        Assert.Equal("0.893996663600558", m.Preview);
    }

    [Fact]
    public void Evaluated_Event_Raised_OnSuccessOnly()
    {
        var m = new InputStateMachine();
        var log = new List<(string, string)>();
        m.Evaluated += (e, r) => log.Add((e, r));
        Press(m, "π", "×", "2", "=", "C", "1", "÷", "0", "=");
        Assert.Single(log);
        Assert.Equal(("π×2", "6.28318530717959"), log[0]);
    }

    [Fact]
    public void Evaluated_Event_SubstitutesAns_ForHistory()
    {
        var m = new InputStateMachine();
        var log = new List<(string Expr, string Result)>();
        m.Evaluated += (e, r) => log.Add((e, r));
        Press(m, "4", "2", "=", "×", "2", "=");
        Assert.Equal("Ans×2", m.Expression);          // màn hình vẫn hiện Ans
        Assert.Equal(("42×2", "84"), log[1]);          // lịch sử ghi giá trị thật
    }

    [Fact]
    public void DisplaySymbols_AreEvaluated()
    {
        Assert.Equal("3", Type("√(", "9", ")", "=").Result);
        Assert.Equal("0.25", Type("2", "^(−1)", "÷", "2", "=").Result);
        Assert.Equal("0.3", Type("0", ".", "1", "+", "0", ".", "2", "=").Result);
    }

    [Theory]
    [InlineData("7", InputKind.Operand)]
    [InlineData(".", InputKind.Operand)]
    [InlineData("(", InputKind.Operand)]
    [InlineData("sin(", InputKind.Operand)]
    [InlineData("Ans", InputKind.Operand)]
    [InlineData("π", InputKind.Operand)]
    [InlineData("+", InputKind.Operator)]
    [InlineData("−", InputKind.Operator)]
    [InlineData("×", InputKind.Operator)]
    [InlineData(")", InputKind.Operator)]
    [InlineData("!", InputKind.Operator)]
    [InlineData("^2", InputKind.Operator)]
    public void FromText_Classifies(string text, InputKind kind)
    {
        Assert.Equal(kind, InputEvent.FromText(text).Kind);
    }
}
