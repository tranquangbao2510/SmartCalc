using SmartCalc.Core.Errors;
using SmartCalc.Core.Lexing;

namespace SmartCalc.Core.Parsing;

/// <summary>
/// Giới hạn an toàn cho parser đệ quy trên xuống và evaluator đệ quy.
/// Không có giới hạn thì "((((…1…))))" lồng 100 000 mức sẽ làm TRÀN STACK — lỗi này không bắt được bằng try/catch,
/// chương trình bị tắt ngay. Vì vậy ta chặn trước và báo CalcException như mọi lỗi khác.
///  - MaxDepth : số mức lồng tối đa (ngoặc, dấu một ngôi, luỹ thừa) — kiểm tra trong lúc đệ quy.
///  - MaxTokens: số ký hiệu tối đa — chặn chuỗi phẳng "1+1+…+1" (parser dùng vòng lặp nên không lồng,
///    nhưng tạo cây AST cao bằng số phép toán và evaluator duyệt cây bằng đệ quy).
/// </summary>
public static class ParserLimits
{
    public const int MaxDepth = 256;
    public const int MaxTokens = 1000;

    public static CalcException TooDeep(Token at) =>
        new($"Biểu thức lồng quá sâu (tối đa {MaxDepth} mức) tại vị trí {at.Position}", at.Position);

    /// <summary>Gọi SAU khi phân tích xong (để lỗi lồng sâu / cú pháp được báo trước).</summary>
    public static void CheckLength(IReadOnlyList<Token> tokens)
    {
        int count = tokens.Count - 1;   // không tính token End
        if (count <= MaxTokens) return;
        var at = tokens[MaxTokens];
        throw new CalcException($"Biểu thức quá dài (tối đa {MaxTokens} ký hiệu) tại vị trí {at.Position}", at.Position);
    }
}
