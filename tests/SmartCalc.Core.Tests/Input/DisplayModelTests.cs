using SmartCalc.Core.Input;

namespace SmartCalc.Core.Tests.Input;

public class DisplayModelTests
{
    private static InputStateMachine Run(params InputEvent[] events)
    {
        var m = new InputStateMachine();
        foreach (var e in events) m.Process(e);
        return m;
    }

    [Fact]
    public void Empty_ShowsZero() =>
        Assert.Equal(new DisplayModel("", "", "", "0", false, false), DisplayModel.From(new InputStateMachine()));

    [Fact]
    public void Typing_ShowsExpressionAndPreview() =>
        Assert.Equal(new DisplayModel("2+3", "", "", "5", false, true),
            DisplayModel.From(Run(InputEvent.FromText("2+3"))));

    [Fact]
    public void Result_ShowsEqualsSign() =>
        Assert.Equal(new DisplayModel("2+3 =", "", "", "5", false, false),
            DisplayModel.From(Run(InputEvent.FromText("2+3"), InputEvent.EqualsKey)));

    [Fact]
    public void Error_SplitsAtPosition()
    {
        var d = DisplayModel.From(Run(InputEvent.FromText("1÷0+2"), InputEvent.EqualsKey));
        Assert.Equal(("1", "÷", "0+2"), (d.TopText, d.TopMark, d.TopAfter));
        Assert.True(d.IsError);
        Assert.Contains("chia cho 0", d.BottomText);
    }

    [Fact]
    public void Error_AtEnd_MarksBlankCell()
    {
        var d = DisplayModel.From(Run(InputEvent.FromText("2+"), InputEvent.EqualsKey));
        Assert.Equal(("2+", " ", ""), (d.TopText, d.TopMark, d.TopAfter));
    }
}
