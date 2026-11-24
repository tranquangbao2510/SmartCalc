using System.Globalization;
using System.Text;
using SmartCalc.Core.Errors;
using SmartCalc.Core.Parsing;

namespace SmartCalc.Core.Programmer;

/// <summary>Kích thước word: số bit dùng để lưu giá trị (số có dấu, bù hai).</summary>
public enum WordSize { Byte = 8, Word = 16, DWord = 32, QWord = 64 }

public static class WordSizes
{
    public static int Bits(this WordSize size) => (int)size;

    /// <summary>Cắt giá trị về đúng số bit rồi mở rộng dấu (sign-extend): vd BYTE: 255 -> -1, 300 -> 44.</summary>
    public static long Normalize(long value, WordSize size) => size switch
    {
        WordSize.Byte => unchecked((sbyte)value),
        WordSize.Word => unchecked((short)value),
        WordSize.DWord => unchecked((int)value),
        _ => value,
    };

    /// <summary>Dãy bit (không dấu) của giá trị trong word, vd BYTE: -1 -> 0xFF.</summary>
    public static ulong ToBits(long value, WordSize size) =>
        size == WordSize.QWord ? unchecked((ulong)value) : unchecked((ulong)value) & ((1UL << size.Bits()) - 1);

    public static string Label(this WordSize size) => size switch
    {
        WordSize.Byte => "BYTE",
        WordSize.Word => "WORD",
        WordSize.DWord => "DWORD",
        _ => "QWORD",
    };

    /// <summary>Thứ tự vòng khi bấm nút kích thước (giống máy tính Windows): QWORD -> DWORD -> WORD -> BYTE -> QWORD.</summary>
    public static WordSize Next(this WordSize size) => size switch
    {
        WordSize.QWord => WordSize.DWord,
        WordSize.DWord => WordSize.Word,
        WordSize.Word => WordSize.Byte,
        _ => WordSize.QWord,
    };
}

/*
 * EVALUATOR SỐ NGUYÊN — duyệt AST (post-order) với số học bù hai trên WordSize bit.
 *  - Mọi kết quả trung gian được Normalize: tràn thì quay vòng như kiểu số nguyên trong C (vd BYTE: 127+1 = -128).
 *  - Literal phải vừa WordSize bit (không dấu), vd BYTE nhận 0..FF; lớn hơn -> lỗi tràn số.
 *  - '/' và '%' chia nguyên, làm tròn về 0 (như C#). Chia cho 0 -> lỗi.
 *  - '>>' là dịch số học (giữ bit dấu). Dịch >= số bit: '<<' cho 0, '>>' cho 0 hoặc -1.
 */
public sealed class IntegerEvaluator
{
    private readonly WordSize _size;

    public IntegerEvaluator(WordSize size) => _size = size;

    private long N(long v) => WordSizes.Normalize(v, _size);

    public long Evaluate(Node node) => node switch
    {
        IntegerNode i => Literal(i),
        UnaryNode u => Unary(u),
        BinaryNode b => Binary(b),
        _ => throw new CalcException("Phép toán không dùng được ở chế độ lập trình", node.Position),
    };

    private long Literal(IntegerNode i)
    {
        if (_size != WordSize.QWord && i.Value >= 1UL << _size.Bits())
            throw new CalcException(
                $"Tràn số: '{i.Text}' vượt quá {_size.Label()} ({_size.Bits()} bit) (vị trí {i.Position})", i.Position);
        return N(unchecked((long)i.Value));
    }

    private long Unary(UnaryNode u)
    {
        long v = Evaluate(u.Operand);
        return u.Op switch
        {
            UnaryOp.Minus => N(unchecked(-v)),
            UnaryOp.BitNot => N(~v),
            _ => v,
        };
    }

    private long Binary(BinaryNode b)
    {
        long l = Evaluate(b.Left);
        long r = Evaluate(b.Right);
        int bits = _size.Bits();

        switch (b.Op)
        {
            case BinaryOp.Add: return N(unchecked(l + r));
            case BinaryOp.Subtract: return N(unchecked(l - r));
            case BinaryOp.Multiply: return N(unchecked(l * r));
            case BinaryOp.Divide:
                if (r == 0) throw DivideByZero(b.Position);
                return r == -1 ? N(unchecked(-l)) : N(l / r);   // MIN / -1 tràn: quay vòng thay vì ném lỗi
            case BinaryOp.Modulo:
                if (r == 0) throw DivideByZero(b.Position);
                return r == -1 ? 0 : N(l % r);
            case BinaryOp.BitAnd: return N(l & r);
            case BinaryOp.BitOr: return N(l | r);
            case BinaryOp.BitXor: return N(l ^ r);
            case BinaryOp.ShiftLeft:
                CheckShift(r, b.Position);
                return r >= bits ? 0 : N(l << (int)r);
            case BinaryOp.ShiftRight:
                CheckShift(r, b.Position);
                return r >= bits ? (l < 0 ? -1 : 0) : N(l >> (int)r);
            default:
                throw new CalcException("Phép toán không dùng được ở chế độ lập trình", b.Position);
        }
    }

    private static void CheckShift(long count, int position)
    {
        if (count < 0)
            throw new CalcException($"Số bit dịch phải không âm (vị trí {position})", position);
    }

    private static CalcException DivideByZero(int position) => new($"Không thể chia cho 0 (vị trí {position})", position);
}

/// <summary>Định dạng số nguyên theo cơ số (HEX/OCT/BIN theo bù hai trong word, DEC có dấu).</summary>
public static class ProgrammerFormatter
{
    public static readonly IReadOnlyList<int> Radixes = [16, 10, 8, 2];

    public static string RadixLabel(int radix) => radix switch
    {
        16 => "HEX",
        10 => "DEC",
        8 => "OCT",
        2 => "BIN",
        _ => throw new ArgumentOutOfRangeException(nameof(radix)),
    };

    /// <param name="group">Nhóm chữ số cho dễ đọc: HEX/BIN theo 4, OCT/DEC theo 3, cách nhau bởi dấu cách.</param>
    public static string Format(long value, int radix, WordSize size, bool group = false)
    {
        string digits = radix == 10
            ? value.ToString(CultureInfo.InvariantCulture)
            : Convert.ToString(unchecked((long)WordSizes.ToBits(value, size)), radix).ToUpperInvariant();
        if (!group) return digits;

        bool negative = digits.StartsWith('-');
        if (negative) digits = digits[1..];
        int width = radix is 16 or 2 ? 4 : 3;
        var sb = new StringBuilder();
        for (int i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % width == 0) sb.Append(' ');
            sb.Append(digits[i]);
        }
        return (negative ? "-" : "") + sb;
    }

    /// <summary>Text để chèn lại vào biểu thức theo cơ số đang nhập (số âm ở DEC dùng dấu trừ một ngôi).</summary>
    public static string FormatForInput(long value, int radix, WordSize size)
    {
        string s = Format(value, radix, size);
        return s.StartsWith('-') ? "−" + s[1..] : s;
    }
}

/// <summary>Mặt tiền: chuỗi -> Lexer (DFA, chế độ lập trình, theo cơ số) -> ProgrammerParser -> IntegerEvaluator.</summary>
public static class ProgrammerCalculator
{
    public static long Evaluate(string expression, int radix, WordSize size) =>
        new IntegerEvaluator(size).Evaluate(ProgrammerParser.Parse(expression, radix));
}
