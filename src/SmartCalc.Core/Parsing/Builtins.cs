namespace SmartCalc.Core.Parsing;

/// <summary>Bảng tên hàm và hằng số được hỗ trợ (không phân biệt hoa thường).</summary>
public static class Builtins
{
    public static readonly IReadOnlyList<string> Functions =
        ["sin", "cos", "tan", "asin", "acos", "atan", "ln", "log", "sqrt", "abs", "fact"];

    public static readonly IReadOnlyList<string> Constants = ["pi", "e", "Ans"];

    public const string Variable = "x";

    /// <summary>Ký hiệu hiển thị trên bàn phím -> tên chuẩn.</summary>
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["π"] = "pi",
        ["√"] = "sqrt",
    };

    /// <summary>Trả về tên chuẩn (đúng chữ hoa/thường) nếu có trong danh sách, ngược lại null.</summary>
    public static string? Find(IReadOnlyList<string> names, string name)
    {
        if (Aliases.TryGetValue(name, out var canonical)) name = canonical;
        return names.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    }
}
