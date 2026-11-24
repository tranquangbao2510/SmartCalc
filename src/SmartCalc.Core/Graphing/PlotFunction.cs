using SmartCalc.Core.Errors;
using SmartCalc.Core.Evaluation;
using SmartCalc.Core.Parsing;

namespace SmartCalc.Core.Graphing;

/// <summary>
/// Một hàm y = f(x) đã "biên dịch": phân tích cú pháp MỘT lần (tái sử dụng Lexer DFA + Parser đệ quy
/// với biến x được phép), sau đó đánh giá AST tại rất nhiều giá trị x.
/// </summary>
public sealed class PlotFunction
{
    private readonly Node _ast;

    public string Source { get; }

    /// <summary>Đơn vị góc khi đánh giá sin/cos/tan (đồ thị mặc định dùng radian).</summary>
    public AngleMode AngleMode { get; init; } = AngleMode.Rad;

    private PlotFunction(string source, Node ast)
    {
        Source = source;
        _ast = ast;
    }

    /// <summary>
    /// Biên dịch "y = x^2", "f(x) = sin(x)" hoặc "x^2". Lỗi cú pháp -> CalcException,
    /// vị trí lỗi tính trên chuỗi gốc (tiền tố "y =" được thay bằng dấu cách, không cắt bỏ).
    /// </summary>
    public static PlotFunction Compile(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string body = BlankOutPrefix(text);
        if (string.IsNullOrWhiteSpace(body))
            throw new CalcException("Biểu thức rỗng", text.Length);
        return new PlotFunction(text, Parser.Parse(body, allowVariable: true));
    }

    public static bool TryCompile(string text, out PlotFunction? function, out CalcException? error)
    {
        try
        {
            function = Compile(text);
            error = null;
            return true;
        }
        catch (CalcException ex)
        {
            function = null;
            error = ex;
            return false;
        }
    }

    /// <summary>f(x), hoặc null nếu không xác định tại x (chia 0, ngoài miền xác định, tràn số...).</summary>
    public double? Evaluate(double x)
    {
        try
        {
            return new Evaluator(new EvalContext(AngleMode, 0, x, Fast: true)).Evaluate(_ast);
        }
        catch (CalcException)
        {
            return null;
        }
    }

    /// <summary>Đánh giá tại nhiều điểm; phần tử null = không xác định.</summary>
    public IReadOnlyList<double?> EvaluateMany(IEnumerable<double> xs) => xs.Select(Evaluate).ToList();

    // "y = ..." hoặc "f(x) = ..." -> thay phần tiền tố bằng dấu cách để giữ nguyên vị trí ký tự.
    private static string BlankOutPrefix(string text)
    {
        int eq = text.IndexOf('=');
        if (eq < 0) return text;
        string head = text[..eq].Replace(" ", "");
        if (head is "y" or "Y" or "f(x)" or "y(x)")
            return new string(' ', eq + 1) + text[(eq + 1)..];
        return text;   // dấu '=' ở chỗ khác -> để lexer báo "ký tự không hợp lệ" đúng vị trí
    }
}
