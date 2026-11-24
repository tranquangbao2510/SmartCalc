using SmartCalc.Core.Errors;
using SmartCalc.Core.Lexing;

namespace SmartCalc.Core.Parsing;

/*
 * PARSER CHẾ ĐỘ LẬP TRÌNH — đệ quy trên xuống, độ ưu tiên giống ngôn ngữ C.
 *
 * VĂN PHẠM (từ ưu tiên THẤP đến CAO), mọi toán tử hai ngôi kết hợp trái:
 *
 *   Expr    -> Or
 *   Or      -> Xor   ( '|' Xor )*                     OR
 *   Xor     -> And   ( '^' And )*                     XOR  ('^' là XOR, không phải luỹ thừa)
 *   And     -> Shift ( '&' Shift )*                   AND
 *   Shift   -> Sum   ( ('<<' | '>>') Sum )*           dịch bit
 *   Sum     -> Product ( ('+' | '-') Product )*
 *   Product -> Unary ( ('*' | '/' | '%') Unary )*
 *   Unary   -> ('-' | '+' | '~') Unary | Primary      '~' = NOT
 *   Primary -> Number | '(' Expr ')'
 *
 * Mỗi tầng hai ngôi có cùng một khuôn "Tầng_i -> Tầng_(i+1) (op Tầng_(i+1))*", nên được cài bằng một hàm
 * ParseLevel(i) duyệt mảng Levels (mỗi phần tử = một ký hiệu chưa kết thúc ở trên). Đây vẫn là đệ quy trên
 * xuống: ParseLevel(i) gọi ParseLevel(i+1) đúng như vế phải của luật sinh.
 *
 * Số được đọc theo cơ số đang chọn (Radix) và giữ dạng ulong trong IntegerNode.
 * Không có hằng, hàm, biến, giai thừa, nhân ngầm.
 */
public sealed class ProgrammerParser
{
    // Levels[i] = các toán tử của tầng i (0 = Or ... 5 = Product)
    private static readonly IReadOnlyDictionary<TokenKind, BinaryOp>[] Levels =
    [
        new Dictionary<TokenKind, BinaryOp> { [TokenKind.Pipe] = BinaryOp.BitOr },
        new Dictionary<TokenKind, BinaryOp> { [TokenKind.Caret] = BinaryOp.BitXor },
        new Dictionary<TokenKind, BinaryOp> { [TokenKind.Ampersand] = BinaryOp.BitAnd },
        new Dictionary<TokenKind, BinaryOp>
        {
            [TokenKind.ShiftLeft] = BinaryOp.ShiftLeft, [TokenKind.ShiftRight] = BinaryOp.ShiftRight,
        },
        new Dictionary<TokenKind, BinaryOp> { [TokenKind.Plus] = BinaryOp.Add, [TokenKind.Minus] = BinaryOp.Subtract },
        new Dictionary<TokenKind, BinaryOp>
        {
            [TokenKind.Star] = BinaryOp.Multiply, [TokenKind.Slash] = BinaryOp.Divide, [TokenKind.Percent] = BinaryOp.Modulo,
        },
    ];

    private static readonly HashSet<TokenKind> BinaryOperatorTokens = Levels.SelectMany(l => l.Keys).ToHashSet();

    private readonly IReadOnlyList<Token> _tokens;
    private readonly int _radix;
    private int _pos;

    private ProgrammerParser(IReadOnlyList<Token> tokens, int radix)
    {
        _tokens = tokens;
        _radix = radix;
    }

    public static Node Parse(string input, int radix = 10) =>
        Parse(Lexer.Tokenize(input, programmerMode: true, radix), radix);

    public static Node Parse(IReadOnlyList<Token> tokens, int radix)
    {
        var parser = new ProgrammerParser(tokens, radix);
        var tree = parser.ParseLevel(0);
        if (parser.Current.Kind != TokenKind.End) throw Unexpected(parser.Current);
        ParserLimits.CheckLength(tokens);
        return tree;
    }

    private int _depth;   // số mức đệ quy qua ParseUnary (xem ParserLimits)

    private Token Current => _tokens[_pos];
    private Token Previous => _tokens[_pos - 1];
    private Token Advance() => _tokens[_pos++];

    // Tầng_i -> Tầng_(i+1) ( op_i Tầng_(i+1) )*
    private Node ParseLevel(int level)
    {
        if (level == Levels.Length) return ParseUnary();

        var left = ParseLevel(level + 1);
        while (Levels[level].TryGetValue(Current.Kind, out var op))
        {
            var opToken = Advance();
            var right = ParseLevel(level + 1);
            left = new BinaryNode(op, left, right, opToken.Position);
        }
        return left;
    }

    // Unary -> ('-' | '+' | '~') Unary | Primary
    // Mọi vòng đệ quy (ngoặc, chuỗi dấu một ngôi) đều đi qua đây -> đếm độ sâu tại đây.
    private Node ParseUnary()
    {
        if (++_depth > ParserLimits.MaxDepth) throw ParserLimits.TooDeep(Current);
        try
        {
            return ParseUnaryCore();
        }
        finally
        {
            _depth--;
        }
    }

    private Node ParseUnaryCore()
    {
        var op = Current.Kind switch
        {
            TokenKind.Minus => UnaryOp.Minus,
            TokenKind.Plus => UnaryOp.Plus,
            TokenKind.Tilde => UnaryOp.BitNot,
            _ => (UnaryOp?)null,
        };
        if (op is null) return ParsePrimary();

        var tok = Advance();
        return new UnaryNode(op.Value, ParseUnary(), tok.Position);
    }

    // Primary -> Number | '(' Expr ')'
    private Node ParsePrimary()
    {
        var tok = Current;
        switch (tok.Kind)
        {
            case TokenKind.Number:
                Advance();
                return new IntegerNode(ParseInteger(tok, _radix), tok.Text, tok.Position);

            case TokenKind.LParen:
            {
                var open = Advance();
                var inner = ParseLevel(0);
                if (Current.Kind == TokenKind.RParen)
                {
                    Advance();
                    return inner;
                }
                if (Current.Kind == TokenKind.End)
                    throw new CalcException($"Thiếu dấu ')' cho '(' tại vị trí {open.Position}", open.Position);
                throw Unexpected(Current);
            }

            case TokenKind.End:
                throw Error(tok, _pos > 0 ? $"Thiếu toán hạng sau '{Previous.Text}'" : "Biểu thức rỗng");

            case TokenKind.RParen:
                throw Error(tok, _pos > 0 && Previous.Kind == TokenKind.LParen
                    ? "Thiếu biểu thức trong ngoặc"
                    : "Thiếu toán hạng trước ')'");

            case TokenKind.Identifier:
                throw Error(tok, $"Chế độ lập trình không dùng tên '{tok.Text}'");

            default:
                if (BinaryOperatorTokens.Contains(tok.Kind))
                    throw Error(tok, $"Thiếu toán hạng trước '{tok.Text}'");
                throw Unexpected(tok);
        }
    }

    /// <summary>Đọc literal số nguyên: tiền tố 0x/0o/0b (0b không có ở hệ HEX) hoặc theo cơ số đang chọn.</summary>
    public static ulong ParseInteger(Token tok, int radix)
    {
        string text = tok.Text;
        int start = 0;
        if (text.Length > 2 && text[0] == '0')
        {
            char p = char.ToLowerInvariant(text[1]);
            if (p == 'x') { radix = 16; start = 2; }
            else if (p == 'o') { radix = 8; start = 2; }
            else if (p == 'b' && radix != 16) { radix = 2; start = 2; }
        }

        ulong value = 0;
        try
        {
            for (int i = start; i < text.Length; i++)
            {
                int digit = HexDigit(text[i]);
                if (digit < 0 || digit >= radix)
                    throw new CalcException($"Chữ số '{text[i]}' không hợp lệ ở hệ {radix} (vị trí {tok.Position + i})", tok.Position + i);
                value = checked(value * (ulong)radix + (ulong)digit);
            }
        }
        catch (OverflowException)
        {
            throw new CalcException($"Tràn số: '{text}' vượt quá 64 bit (vị trí {tok.Position})", tok.Position);
        }
        return value;
    }

    private static int HexDigit(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };

    private static CalcException Error(Token at, string message) => new($"{message} (vị trí {at.Position})", at.Position);

    private static CalcException Unexpected(Token tok) => tok.Kind switch
    {
        TokenKind.RParen => new CalcException($"Thừa dấu ')' tại vị trí {tok.Position}", tok.Position),
        TokenKind.Number => new CalcException($"Thiếu toán tử trước '{tok.Text}' (vị trí {tok.Position})", tok.Position),
        TokenKind.LParen => new CalcException($"Thiếu toán tử trước '(' (vị trí {tok.Position})", tok.Position),
        _ => new CalcException($"Ký hiệu '{tok.Text}' không dùng được ở chế độ lập trình (vị trí {tok.Position})", tok.Position),
    };
}
