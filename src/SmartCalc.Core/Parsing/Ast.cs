namespace SmartCalc.Core.Parsing;

/*
 * CÂY CÚ PHÁP TRỪU TƯỢNG (AST) — các record bất biến.
 * Position của mỗi nút = vị trí token "đại diện" của nút đó trong chuỗi nhập
 * (toán tử với nút nhị phân, tên hàm với lời gọi hàm, '!' với giai thừa ...),
 * để Evaluator báo lỗi đúng chỗ (vd chia cho 0 -> trỏ vào dấu '/').
 */

public abstract record Node(int Position);

/// <summary>Hằng số literal, vd 3.14, 1.5e-3.</summary>
public sealed record NumberNode(double Value, string Text, int Position) : Node(Position);

/// <summary>Số nguyên (chế độ lập trình): giữ nguyên 64 bit, không qua double để không mất chính xác.</summary>
public sealed record IntegerNode(ulong Value, string Text, int Position) : Node(Position);

/// <summary>Hằng có tên: pi, e, Ans.</summary>
public sealed record ConstantNode(string Name, int Position) : Node(Position);

/// <summary>Biến x (chỉ khi vẽ đồ thị).</summary>
public sealed record VariableNode(string Name, int Position) : Node(Position);

public enum UnaryOp { Plus, Minus, BitNot }   // BitNot (~) chỉ dùng ở chế độ lập trình

public sealed record UnaryNode(UnaryOp Op, Node Operand, int Position) : Node(Position);

public enum BinaryOp
{
    Add, Subtract, Multiply, Divide, Modulo, Power,
    BitAnd, BitOr, BitXor, ShiftLeft, ShiftRight,   // chế độ lập trình
}

/// <summary>Phép toán hai ngôi. Implicit = true nếu là nhân ngầm (2pi, 3(4+1)).</summary>
public sealed record BinaryNode(BinaryOp Op, Node Left, Node Right, int Position, bool Implicit = false)
    : Node(Position);

/// <summary>Giai thừa hậu tố n!.</summary>
public sealed record FactorialNode(Node Operand, int Position) : Node(Position);

/// <summary>Lời gọi hàm một đối số: sin(x), sqrt(x) ...</summary>
public sealed record CallNode(string Function, Node Argument, int Position) : Node(Position);

/// <summary>In AST dạng tiền tố (kiểu Lisp) — tiện cho test và minh hoạ cây.</summary>
public static class AstPrinter
{
    public static string ToPrefix(Node node) => node switch
    {
        NumberNode n => n.Text,
        IntegerNode i => i.Text,
        ConstantNode c => c.Name,
        VariableNode v => v.Name,
        UnaryNode u => $"({u.Op switch { UnaryOp.Minus => "neg", UnaryOp.BitNot => "~", _ => "pos" }} {ToPrefix(u.Operand)})",
        BinaryNode b => $"({Symbol(b.Op)} {ToPrefix(b.Left)} {ToPrefix(b.Right)})",
        FactorialNode f => $"(! {ToPrefix(f.Operand)})",
        CallNode c => $"({c.Function} {ToPrefix(c.Argument)})",
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    public static string Symbol(BinaryOp op) => op switch
    {
        BinaryOp.Add => "+",
        BinaryOp.Subtract => "-",
        BinaryOp.Multiply => "*",
        BinaryOp.Divide => "/",
        BinaryOp.Modulo => "%",
        BinaryOp.Power => "^",
        BinaryOp.BitAnd => "&",
        BinaryOp.BitOr => "|",
        BinaryOp.BitXor => "xor",
        BinaryOp.ShiftLeft => "<<",
        BinaryOp.ShiftRight => ">>",
        _ => "?",
    };
}
