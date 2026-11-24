using SmartCalc.Core.Input;

namespace SmartCalc.Core.Tests.Input;

public class InputTableTests
{
    public static IEnumerable<object[]> Cells()
    {
        // Cột theo InputSymbol; các kết quả mong đợi độc lập với bảng trong Core.
        string[] symbols = ["Operand", "Operator", "EqualsOk", "EqualsError", "Clear", "BackspaceToEmpty", "BackspaceKeep", "Negate", "LoadEmpty", "LoadOk"];
        string[] states = ["Empty", "Typing", "ShowingResult", "Error"];
        string[] next =
        [
            "Typing Typing Empty Empty Empty Empty Empty Typing Empty Typing",
            "Typing Typing ShowingResult Error Empty Empty Typing Typing Empty Typing",
            "Typing Typing ShowingResult ShowingResult Empty ShowingResult ShowingResult Typing Empty Typing",
            "Typing Typing Empty Empty Empty Empty Empty Typing Empty Typing",
        ];
        string[] actions =
        [
            "Append Append Ignore Ignore ClearAll Ignore Ignore Append Replace Replace",
            "Append Append CommitResult ShowError ClearAll DeleteLast DeleteLast ToggleNegate Replace Replace",
            "StartNew ContinueFromAns Ignore Ignore ClearAll Ignore Ignore NegateAns Replace Replace",
            "ClearAndAppend ClearAndAppend ClearAll ClearAll ClearAll ClearAll ClearAll ClearAndAppend ClearAndReplace ClearAndReplace",
        ];
        for (int row = 0; row < states.Length; row++)
            for (int column = 0; column < symbols.Length; column++)
                yield return [Enum.Parse<InputState>(states[row]), Enum.Parse<InputSymbol>(symbols[column]),
                    Enum.Parse<InputState>(next[row].Split(' ')[column]), Enum.Parse<InputAction>(actions[row].Split(' ')[column])];
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void BothTables_EveryCellHasExpectedValue(InputState state, InputSymbol symbol, InputState next, InputAction action)
    {
        Assert.Equal(next, InputStateMachine.Table[(state, symbol)]);
        Assert.Equal(action, InputStateMachine.ActionTable[(state, symbol)]);
    }

    public static IEnumerable<object[]> KeyScenarios()
    {
        foreach (var state in Enum.GetValues<InputState>())
        {
            bool typing = state == InputState.Typing, result = state == InputState.ShowingResult;
            yield return [state, InputKind.Operand, "5", InputState.Typing, typing ? "125" : "5"];
            yield return [state, InputKind.Operator, "+", InputState.Typing, typing ? "12+" : result ? "Ans+" : "+"];
            yield return [state, InputKind.Equals, "", typing || result ? InputState.ShowingResult : InputState.Empty, typing || result ? "12" : ""];
            yield return [state, InputKind.Clear, "", InputState.Empty, ""];
            yield return [state, InputKind.Backspace, "", typing ? InputState.Typing : result ? InputState.ShowingResult : InputState.Empty, typing ? "1" : result ? "12" : ""];
            yield return [state, InputKind.Negate, "", InputState.Typing, typing ? "−(12)" : result ? "−Ans" : "−"];
            yield return [state, InputKind.Load, "7+8", InputState.Typing, "7+8"];
            yield return [state, InputKind.Load, "", InputState.Empty, ""];
        }
    }

    [Theory]
    [MemberData(nameof(KeyScenarios))]
    public void OldKeyBehavior_FromEveryState(InputState before, InputKind kind, string text, InputState next, string expression)
    {
        var machine = new InputStateMachine();
        if (before != InputState.Empty) machine.Process(InputEvent.FromText("12"));
        if (before is InputState.ShowingResult or InputState.Error) machine.Process(InputEvent.EqualsKey);
        if (before == InputState.Error)
        {
            machine.Process(InputEvent.Load("1/0"));
            machine.Process(InputEvent.EqualsKey);
        }
        Assert.Equal(before, machine.State);
        machine.Process(new InputEvent(kind, text));
        Assert.Equal(next, machine.State);
        Assert.Equal(expression, machine.Expression);
        Assert.Equal("", machine.ErrorMessage);
        Assert.Equal(-1, machine.ErrorPosition);
    }

    [Fact]
    public void Equals_CommitsOnce_AndKeepsErrorPosition()
    {
        var engine = new CountingEngine();
        var machine = new InputStateMachine(engine);
        int events = 0;
        machine.Evaluated += (_, _) => events++;
        machine.Process(InputEvent.Load("12"));
        int previews = engine.Evaluations;
        machine.Process(InputEvent.EqualsKey);
        Assert.Equal(previews + 1, engine.Evaluations);
        Assert.Equal(1, engine.Commits);
        Assert.Equal(1, events);
        machine.Process(InputEvent.EqualsKey);
        Assert.Equal(previews + 1, engine.Evaluations);
        Assert.Equal(1, events);
        machine.Process(InputEvent.Load("bad"));
        machine.Process(InputEvent.EqualsKey);
        Assert.Equal(InputState.Error, machine.State);
        Assert.Equal("Lỗi thử nghiệm", machine.ErrorMessage);
        Assert.Equal(2, machine.ErrorPosition);
        Assert.Equal(1, engine.Commits);
        Assert.Equal(1, events);
    }

    private sealed class CountingEngine : IExpressionEngine
    {
        public int Evaluations { get; private set; }
        public int Commits { get; private set; }
        public string AnsText => "Ans";
        public EngineResult Evaluate(string expression)
        {
            Evaluations++;
            if (expression == "bad") throw new SmartCalc.Core.Errors.CalcException("Lỗi thử nghiệm", 2);
            return new("12", 12);
        }
        public void Commit(EngineResult result) => Commits++;
        public string ForHistory(string expression) => expression;
    }
}
