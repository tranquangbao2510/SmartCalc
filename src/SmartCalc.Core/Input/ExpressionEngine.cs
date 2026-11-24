using SmartCalc.Core.Evaluation;

namespace SmartCalc.Core.Input;

/// <summary>Kết quả tính: text hiển thị, giá trị số (cho bộ nhớ), và giá trị nguyên chính xác (chế độ lập trình).</summary>
public sealed record EngineResult(string Display, double Value, long? Integer = null);

/// <summary>
/// "Bộ tính" cắm vào InputStateMachine. Máy trạng thái chỉ biết các thao tác này nên cùng một bảng chuyển
/// trạng thái dùng được cho chế độ Khoa học (ScientificEngine) và Lập trình (ProgrammerEngine).
/// </summary>
public interface IExpressionEngine
{
    /// <summary>Tính biểu thức, KHÔNG thay đổi trạng thái engine (dùng cả cho xem trước). Lỗi -> CalcException.</summary>
    EngineResult Evaluate(string expression);

    /// <summary>Gọi khi bấm '=' thành công: ghi nhớ kết quả (Ans).</summary>
    void Commit(EngineResult result);

    /// <summary>Text đại diện kết quả trước, dùng khi nối tiếp (ShowingResult + toán tử).</summary>
    string AnsText { get; }

    /// <summary>Biểu thức để ghi lịch sử (gọi trước Commit).</summary>
    string ForHistory(string expression);
}

/// <summary>Engine chế độ Cơ bản/Khoa học: Lexer -> Parser -> Evaluator (decimal/double), có Ans và DEG/RAD.</summary>
public sealed class ScientificEngine : IExpressionEngine
{
    public AngleMode AngleMode { get; set; } = AngleMode.Deg;
    public double Ans { get; private set; }

    public EngineResult Evaluate(string expression)
    {
        double value = Calculator.Evaluate(expression, new EvalContext(AngleMode, Ans));
        return new EngineResult(NumberFormatter.Format(value), value);
    }

    public void Commit(EngineResult result) => Ans = result.Value;

    public string AnsText => "Ans";

    public string ForHistory(string expression) => ExpressionEditor.SubstituteAns(expression, Ans);
}
