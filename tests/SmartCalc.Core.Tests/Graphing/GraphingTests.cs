using SmartCalc.Core.Errors;
using SmartCalc.Core.Evaluation;
using SmartCalc.Core.Graphing;

namespace SmartCalc.Core.Tests.Graphing;

public class PlotFunctionTests
{
    private static readonly double[] Xs = [-2, -1, 0, 1, 2];

    // ---------- Đánh giá f(x) tại nhiều điểm ----------
    [Theory]
    [InlineData("y = x^2", new double[] { 4, 1, 0, 1, 4 })]
    [InlineData("x^2", new double[] { 4, 1, 0, 1, 4 })]
    [InlineData("f(x) = 2x + 1", new double[] { -3, -1, 1, 3, 5 })]
    [InlineData("y = -x^2", new double[] { -4, -1, 0, -1, -4 })]        // -x^2 = -(x^2)
    [InlineData("y = x^3 - x", new double[] { -6, 0, 0, 0, 6 })]
    [InlineData("y = abs(x)", new double[] { 2, 1, 0, 1, 2 })]
    [InlineData("y = 3", new double[] { 3, 3, 3, 3, 3 })]               // hàm hằng
    [InlineData("y = x(x+1)", new double[] { 2, 0, 0, 2, 6 })]          // nhân ngầm với biến
    public void EvaluateMany_Polynomials(string text, double[] expected)
    {
        var values = PlotFunction.Compile(text).EvaluateMany(Xs);
        Assert.Equal(expected.Select(v => (double?)v), values);
    }

    [Fact]
    public void EvaluateMany_Trig_UsesRadians()
    {
        var f = PlotFunction.Compile("y = sin(x)");
        var values = f.EvaluateMany([0, Math.PI / 6, Math.PI / 2, Math.PI]);
        Assert.Equal(0, values[0]!.Value, 12);
        Assert.Equal(0.5, values[1]!.Value, 12);
        Assert.Equal(1, values[2]!.Value, 12);
        Assert.Equal(0, values[3]!.Value, 12);
    }

    [Fact]
    public void EvaluateMany_MatchesCalculator()
    {
        // Đường tính nhanh (double) cho cùng kết quả với máy tính (decimal) trong sai số hiển thị
        var f = PlotFunction.Compile("y = (x^2 - 3x + 0.1) / (x + 7)");
        for (double x = -5; x <= 5; x += 0.37)
        {
            double expected = Calculator.Evaluate("(x^2 - 3x + 0.1) / (x + 7)", new EvalContext(AngleMode.Rad, X: x));
            Assert.Equal(expected, f.Evaluate(x)!.Value, 10);
        }
    }

    // ---------- Điểm không xác định -> null (không ném lỗi) ----------
    [Theory]
    [InlineData("y = sqrt(x)", -1)]
    [InlineData("y = 1/x", 0)]
    [InlineData("y = ln(x)", 0)]
    [InlineData("y = ln(x)", -3)]
    [InlineData("y = log(x)", -0.5)]
    [InlineData("y = asin(x)", 2)]
    [InlineData("y = x!", -1)]
    [InlineData("y = x!", 0.5)]
    [InlineData("y = 10^x", 400)]               // tràn số
    [InlineData("y = (x-1)/(x-1)", 1)]
    public void UndefinedPoint_IsNull(string text, double x) =>
        Assert.Null(PlotFunction.Compile(text).Evaluate(x));

    [Fact]
    public void Tan_AtHalfPi_IsUndefined() =>
        Assert.Null(PlotFunction.Compile("y = tan(x)").Evaluate(Math.PI / 2));

    [Fact]
    public void EvaluateMany_MixesDefinedAndUndefined()
    {
        var values = PlotFunction.Compile("y = sqrt(x)").EvaluateMany([-4, -1, 0, 1, 4, 9]);
        Assert.Equal([null, null, 0, 1, 2, 3], values);
    }

    // ---------- Lỗi cú pháp: báo vị trí trên chuỗi gốc ----------
    [Theory]
    [InlineData("y = 2+", 6)]
    [InlineData("y = x)", 5)]
    [InlineData("y = foo", 4)]
    [InlineData("y = 2 # x", 6)]
    [InlineData("y = (x", 4)]
    [InlineData("y = x = 1", 6)]                 // '=' thứ hai là ký tự không hợp lệ
    public void CompileError_PositionInOriginalText(string text, int position)
    {
        Assert.False(PlotFunction.TryCompile(text, out var f, out var error));
        Assert.Null(f);
        Assert.Equal(position, error!.Position);
    }

    [Theory]
    [InlineData("")]
    [InlineData("y =")]
    [InlineData("   ")]
    public void Empty_IsError(string text) => Assert.Throws<CalcException>(() => PlotFunction.Compile(text));
}

public class FunctionSamplerTests
{
    private static IReadOnlyList<IReadOnlyList<PlotPoint>> Sample(string text, Viewport? view = null, int samples = 400) =>
        FunctionSampler.Sample(PlotFunction.Compile(text), view ?? Viewport.Default, samples);

    [Fact]
    public void ContinuousFunction_IsOneSegment()
    {
        var segs = Sample("y = sin(x)");
        Assert.Single(segs);
        Assert.Equal(401, segs[0].Count);
    }

    [Fact]
    public void SteepButContinuous_IsNotBroken()
    {
        // x^3 nhảy hơn chiều cao vùng nhìn giữa 2 mẫu khi thu nhỏ, nhưng vẫn liên tục -> 1 nét
        Assert.Single(Sample("y = x^3", samples: 100));
    }

    [Fact]
    public void Reciprocal_BreaksAtZero_NoBogusVerticalLine()
    {
        var segs = Sample("y = 1/x");
        Assert.Equal(2, segs.Count);
        Assert.All(segs[0], p => Assert.True(p.X < 0));
        Assert.All(segs[1], p => Assert.True(p.X > 0));
    }

    [Fact]
    public void Reciprocal_BreaksEvenWhenZeroIsNotSampled()
    {
        // 301 mẫu trên [-10, 10]: không mẫu nào rơi đúng x = 0
        var segs = Sample("y = 1/x", samples: 301);
        Assert.Equal(2, segs.Count);
        Assert.True(segs[0][^1].X < 0 && segs[1][0].X > 0);
    }

    [Fact]
    public void Tan_BreaksAtEachAsymptote()
    {
        // Trên [-5, 5] có 4 tiệm cận: ±π/2, ±3π/2 -> 5 nét
        var view = new Viewport(-5, 5, -7, 7);
        var segs = Sample("y = tan(x)", view, 500);
        Assert.Equal(5, segs.Count);
        foreach (var seg in segs)
            for (int i = 1; i < seg.Count; i++)
                Assert.True(seg[i].Y >= seg[i - 1].Y, "tan tăng trên mỗi nhánh — không có đoạn nối ngược qua tiệm cận");
    }

    [Fact]
    public void Sqrt_OnlyDrawnWhereDefined()
    {
        var segs = Sample("y = sqrt(x)");
        Assert.Single(segs);
        Assert.All(segs[0], p => Assert.True(p.X >= 0));
    }

    [Fact]
    public void Ln_StartsAfterZero() =>
        Assert.All(Sample("y = ln(x)").SelectMany(s => s), p => Assert.True(p.X > 0));

    [Fact]
    public void NowhereDefined_IsEmpty() => Assert.Empty(Sample("y = sqrt(-1 - x^2)"));

    [Fact]
    public void HugeValues_AreClamped()
    {
        var view = Viewport.Default;
        double limit = view.Height * 20;
        foreach (var p in Sample("y = 1/x^4").SelectMany(s => s))
            Assert.InRange(p.Y, view.YMin - limit, view.YMax + limit);
    }
}

public class ViewportTests
{
    private const double W = 800, H = 560;

    [Fact]
    public void ScreenWorld_RoundTrip()
    {
        var v = new Viewport(-10, 10, -7, 7);
        var s = v.ToScreen(2.5, -3, W, H);
        var back = v.ToWorld(s.X, s.Y, W, H);
        Assert.Equal(2.5, back.X, 9);
        Assert.Equal(-3, back.Y, 9);
        Assert.Equal(new PlotPoint(400, 280), v.ToScreen(0, 0, W, H));   // gốc toạ độ ở giữa
        Assert.Equal(new PlotPoint(0, 0), v.ToScreen(-10, 7, W, H));      // góc trên trái
    }

    [Fact]
    public void Zoom_KeepsPointUnderCursorFixed()
    {
        var v = new Viewport(-10, 10, -7, 7);
        var cursor = new PlotPoint(3, 2);
        var screenBefore = v.ToScreen(cursor.X, cursor.Y, W, H);
        var z = v.Zoom(0.5, cursor.X, cursor.Y);
        Assert.Equal(10, z.Width, 9);
        Assert.Equal(7, z.Height, 9);
        var screenAfter = z.ToScreen(cursor.X, cursor.Y, W, H);
        Assert.Equal(screenBefore.X, screenAfter.X, 6);
        Assert.Equal(screenBefore.Y, screenAfter.Y, 6);
    }

    [Fact]
    public void Zoom_OutThenIn_RestoresView()
    {
        var v = new Viewport(-10, 10, -7, 7);
        var r = v.Zoom(2, 1, 1).Zoom(0.5, 1, 1);
        Assert.Equal(v.XMin, r.XMin, 9);
        Assert.Equal(v.YMax, r.YMax, 9);
    }

    [Fact]
    public void Zoom_BeyondLimits_IsIgnored()
    {
        var tiny = new Viewport(0, 2e-9, 0, 2e-9);
        Assert.Same(tiny, tiny.ZoomAtCenter(0.1));
        var huge = new Viewport(-4e8, 4e8, -4e8, 4e8);
        Assert.Same(huge, huge.ZoomAtCenter(10));
    }

    [Fact]
    public void PanPixels_ContentFollowsMouse()
    {
        var v = new Viewport(-10, 10, -7, 7);
        // kéo sang phải 80 px (= 2 đơn vị) và xuống 40 px (= 1 đơn vị): vùng nhìn dịch sang trái và lên trên
        var p = v.PanPixels(80, 40, W, H);
        Assert.Equal(-12, p.XMin, 9);
        Assert.Equal(8, p.YMax, 9);
        Assert.Equal(v.Width, p.Width, 9);
    }

    [Fact]
    public void WithAspect_MakesUnitsSquare()
    {
        var v = new Viewport(-10, 10, -1, 1).WithAspect(800, 400);
        Assert.Equal(10, v.Height, 9);
        Assert.Equal(0, v.CenterY, 9);
        Assert.Equal(v.Width / 800, v.Height / 400, 12);
    }

    [Theory]
    [InlineData(20, 10, 2)]
    [InlineData(14, 10, 1)]
    [InlineData(100, 10, 10)]
    [InlineData(0.37, 10, 0.05)]
    [InlineData(3000, 8, 500)]
    public void NiceStep(double range, int lines, double expected) =>
        Assert.Equal(expected, GridLines.NiceStep(range, lines), 12);

    [Fact]
    public void Ticks_IncludeExactZero()
    {
        var ticks = GridLines.Ticks(-1, 1, 0.5);
        Assert.Equal([-1, -0.5, 0, 0.5, 1], ticks);
        Assert.Contains(0.0, GridLines.Ticks(-0.3, 0.7, 0.1));
        Assert.Equal(2, GridLines.Ticks(0.05, 0.25, 0.1).Count);
    }
}
