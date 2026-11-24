using SmartCalc.Core.Errors;
using SmartCalc.Core.Evaluation;

namespace SmartCalc.Core.Parsing;

/// <summary>Bảng tên hàm và hằng số được hỗ trợ (không phân biệt hoa thường).</summary>
public static class Builtins
{
    public static readonly IReadOnlyDictionary<string, Func<double, EvalContext, int, double>> FunctionMap =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, Func<double, EvalContext, int, double>>(
        new Dictionary<string, Func<double, EvalContext, int, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sin"] = (x, ctx, p) => { return Clean(Math.Sin(ToRadians(x, ctx))); },
            ["cos"] = (x, ctx, p) => { return Clean(Math.Cos(ToRadians(x, ctx))); },
            ["tan"] = (x, ctx, p) => { double rad = ToRadians(x, ctx);
                if (Math.Abs(Math.Cos(rad)) < Epsilon)
                    throw Domain($"tan không xác định tại {Format(x)}", p);
                return Clean(Math.Tan(rad)); },
            ["asin"] = (x, ctx, p) => { if (x < -1 || x > 1) throw Domain("asin chỉ nhận giá trị trong [-1, 1]", p);
                return FromRadians(Math.Asin(x), ctx); },
            ["acos"] = (x, ctx, p) => { if (x < -1 || x > 1) throw Domain("acos chỉ nhận giá trị trong [-1, 1]", p);
                return FromRadians(Math.Acos(x), ctx); },
            ["atan"] = (x, ctx, p) => { return FromRadians(Math.Atan(x), ctx); },
            ["ln"] = (x, ctx, p) => { if (x <= 0) throw Domain("ln chỉ nhận số dương", p);
                return Math.Log(x); },
            ["log"] = (x, ctx, p) => { if (x <= 0) throw Domain("log chỉ nhận số dương", p);
                return Math.Log10(x); },
            ["sqrt"] = (x, ctx, p) => { if (x < 0) throw Domain("không lấy căn bậc hai của số âm", p);
                return Math.Sqrt(x); },
            ["abs"] = (x, ctx, p) => { return Math.Abs(x); },
            ["fact"] = (x, ctx, p) => { return Evaluator.Factorial(x, p); },
        });

    public static readonly IReadOnlyList<string> Functions = FunctionMap.Keys.ToArray();

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
    private const double Epsilon = 1e-15;

    // Ở chế độ DEG: rút gọn góc theo 360 trước khi đổi để sin(180) ~ 0, tan(90) bắt được lỗi
    private static double ToRadians(double x, EvalContext ctx) =>
        ctx.AngleMode == AngleMode.Deg ? (x % 360) * Math.PI / 180 : x;

    private static double FromRadians(double rad, EvalContext ctx) =>
        ctx.AngleMode == AngleMode.Deg ? rad * 180 / Math.PI : rad;

    private static double Clean(double v) => Math.Abs(v) < Epsilon ? 0 : v;

    private static string Format(double v) => NumberFormatter.Format(v);

    private static CalcException Domain(string detail, int position) =>
        new($"Lỗi miền xác định: {detail} (vị trí {position})", position);
}
