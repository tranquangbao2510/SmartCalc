using System.Globalization;
using SmartCalc.Core.Errors;
using SmartCalc.Core.Parsing;

namespace SmartCalc.Core.Evaluation;

public enum AngleMode { Deg, Rad }

/// <summary>Ngữ cảnh tính toán: đơn vị góc, giá trị Ans, giá trị biến x (khi vẽ đồ thị).</summary>
/// <param name="Fast">true = bỏ qua đường decimal, tính thẳng bằng double (dùng khi vẽ đồ thị: tính hàng nghìn điểm,
/// cần nhanh hơn cần chính xác tuyệt đối). Các kiểm tra lỗi (chia 0, miền xác định...) vẫn giữ nguyên.</param>
public sealed record EvalContext(AngleMode AngleMode = AngleMode.Deg, double Ans = 0, double? X = null, bool Fast = false);

/*
 * EVALUATOR — duyệt AST theo thứ tự sau (post-order): tính các con trước rồi áp phép toán ở nút cha.
 *
 * Kiểu số:
 *  - + - * / % : tính bằng decimal (cơ số 10) nên 0.1 + 0.2 = 0.3 chính xác.
 *    double -> decimal đi qua chuỗi round-trip "R" (17 chữ số) để KHÔNG mất chữ số như phép ép (decimal)x
 *    (phép ép chỉ giữ 15 chữ số -> pi, 1/3 bị cắt). 0.1 vẫn thành đúng 0.1m vì "R" của 0.1 là "0.1".
 *    Chỉ dùng decimal khi toán hạng và kết quả nằm trong "vùng an toàn" [1e-10, 1e27] (hoặc = 0);
 *    ngoài vùng đó decimal mất chữ số có nghĩa hoặc tràn, nên chuyển sang double.
 *  - Hàm siêu việt (sin, ln, sqrt, ^ số mũ không nguyên ...) : double.
 * Mọi lỗi ném CalcException với Position = vị trí của nút gây lỗi.
 */
public sealed class Evaluator
{
    private const double SafeMin = 1e-10;   // 17 chữ số + 10 số 0 đầu vẫn vừa 28 chữ số thập phân của decimal
    private const double SafeMax = 1e27;
    private const int MaxFactorial = 170;    // 171! vượt quá double

    private readonly EvalContext _ctx;

    public Evaluator(EvalContext? context = null)
    {
        _ctx = context ?? new EvalContext();
    }

    public double Evaluate(Node node)
    {
        double result = node switch
        {
            NumberNode n => n.Value,
            ConstantNode c => Constant(c),
            VariableNode v => _ctx.X ?? throw new CalcException("Chưa có giá trị cho biến x", v.Position),
            UnaryNode u => u.Op == UnaryOp.Minus ? -Evaluate(u.Operand) : Evaluate(u.Operand),
            BinaryNode b => Binary(b),
            FactorialNode f => Factorial(Evaluate(f.Operand), f.Position),
            CallNode call => Call(call),
            _ => throw new CalcException("Nút AST không hỗ trợ", node.Position),
        };

        if (double.IsInfinity(result))
            throw new CalcException($"Tràn số tại vị trí {node.Position}", node.Position);
        if (double.IsNaN(result))
            throw new CalcException($"Kết quả không xác định tại vị trí {node.Position}", node.Position);
        return result;
    }

    private double Constant(ConstantNode c) => c.Name switch
    {
        "pi" => Math.PI,
        "e" => Math.E,
        "Ans" => _ctx.Ans,
        _ => throw new CalcException($"Hằng không xác định '{c.Name}'", c.Position),
    };

    // ---------------- Phép toán hai ngôi ----------------
    private double Binary(BinaryNode b)
    {
        double l = Evaluate(b.Left);
        double r = Evaluate(b.Right);
        int p = b.Position;

        if (_ctx.Fast)
        {
            // Đường nhanh khi vẽ đồ thị: double thuần. Chia/mod cho 0 rơi xuống switch dưới để báo lỗi như thường.
            switch (b.Op)
            {
                case BinaryOp.Add: return l + r;
                case BinaryOp.Subtract: return l - r;
                case BinaryOp.Multiply: return l * r;
                case BinaryOp.Divide when r != 0: return l / r;
                case BinaryOp.Modulo when r != 0: return l % r;
            }
        }

        switch (b.Op)
        {
            case BinaryOp.Add: return Exact(l, r, (x, y) => x + y, l + r);
            case BinaryOp.Subtract: return Exact(l, r, (x, y) => x - y, l - r);
            case BinaryOp.Multiply: return Exact(l, r, (x, y) => x * y, l * r, zeroIsUnderflow: l != 0 && r != 0);
            case BinaryOp.Divide:
                if (r == 0) throw DivideByZero(p);
                return Exact(l, r, (x, y) => x / y, l / r, zeroIsUnderflow: l != 0);
            case BinaryOp.Modulo:
                if (r == 0) throw DivideByZero(p);
                return Exact(l, r, (x, y) => x % y, l % r); // dấu của kết quả theo số bị chia (như C#)
            case BinaryOp.Power:
                return Power(l, r, p, _ctx.Fast);
            default:
                throw new CalcException("Toán tử không hỗ trợ", p);
        }
    }

    internal static bool IsSafe(double v) => v == 0 || (Math.Abs(v) >= SafeMin && Math.Abs(v) <= SafeMax);

    internal static decimal ToDecimal(double v) =>
        decimal.Parse(v.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Tính bằng decimal nếu an toàn, ngược lại dùng kết quả double dự phòng.</summary>
    /// <param name="zeroIsUnderflow">true khi kết quả đúng chắc chắn khác 0 (nhân/chia hai số khác 0): decimal ra 0 nghĩa là
    /// đã hụt (underflow, vd 1e-10 / 1e27) -> dùng double. Cộng/trừ thì KHÔNG: 0.3 − 0.1 − 0.2 = 0 bằng decimal mới là đúng.</param>
    internal static double Exact(double a, double b, Func<decimal, decimal, decimal> op, double fallback, bool zeroIsUnderflow = false)
    {
        if (IsSafe(a) && IsSafe(b))
        {
            try
            {
                double d = (double)op(ToDecimal(a), ToDecimal(b));
                if (d == 0 && zeroIsUnderflow) return fallback;
                if (IsSafe(d)) return d;
            }
            catch (OverflowException) { }
        }
        return fallback;
    }

    private static double Power(double b, double e, int position, bool fast = false)
    {
        if (b == 0 && e < 0) throw DivideByZero(position);

        // Số mũ nguyên + cơ số an toàn: nhân decimal (bình phương liên tiếp) cho kết quả chính xác, vd 0.1^2 = 0.01
        if (!fast && e == Math.Floor(e) && Math.Abs(e) <= 1000 && IsSafe(b))
        {
            try
            {
                decimal acc = 1m, x = ToDecimal(b);
                for (long n = (long)Math.Abs(e); n > 0; n >>= 1)
                {
                    if ((n & 1) == 1) acc *= x;
                    if (n > 1) x *= x;
                }
                if (e < 0) acc = 1m / acc;
                double d = (double)acc;
                // acc = 0 với cơ số khác 0 là hụt (vd 0.1^50) chứ không phải kết quả 0 -> để Math.Pow tính
                if (d != 0 && IsSafe(d)) return d;
            }
            catch (OverflowException) { }
            catch (DivideByZeroException) { }
        }

        double result = Math.Pow(b, e);
        if (double.IsNaN(result))
            throw new CalcException(
                $"Lỗi miền xác định: luỹ thừa cơ số âm với số mũ không nguyên (vị trí {position})", position);
        if (double.IsInfinity(result))
            throw new CalcException($"Tràn số tại vị trí {position}", position);
        return result;
    }

    // ---------------- Giai thừa ----------------
    internal static double Factorial(double n, int position)
    {
        if (n < 0 || n != Math.Floor(n))
            throw new CalcException(
                $"Giai thừa chỉ áp dụng cho số nguyên không âm (vị trí {position})", position);
        if (n > MaxFactorial)
            throw new CalcException($"Giai thừa quá lớn: tối đa {MaxFactorial}! (vị trí {position})", position);

        double result = 1;
        for (int i = 2; i <= (int)n; i++) result *= i;
        return result;
    }

    // ---------------- Hàm ----------------
    private double Call(CallNode call)
    {
        double x = Evaluate(call.Argument);
        int p = call.Position;
        if (!FunctionTable.FunctionMap.TryGetValue(call.Function, out var function))
            throw new CalcException($"Hàm không xác định '{call.Function}'", p);
        return function(x, _ctx, p);
    }

    private static CalcException DivideByZero(int position) =>
        new($"Không thể chia cho 0 (vị trí {position})", position);

}
