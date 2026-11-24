namespace SmartCalc.Core.Errors;

/// <summary>
/// Lỗi trong quá trình xử lý biểu thức (lexer, parser, evaluator).
/// Message bằng tiếng Việt; Position là chỉ số (0-based) của ký tự gây lỗi trong chuỗi nhập,
/// hoặc -1 nếu lỗi không gắn với vị trí cụ thể.
/// </summary>
public class CalcException : Exception
{
    public int Position { get; }

    public CalcException(string message, int position = -1) : base(message)
    {
        Position = position;
    }
}
