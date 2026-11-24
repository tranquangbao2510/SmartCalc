using System.Globalization;
using SmartCalc.Core.Errors;
using SmartCalc.Core.Lexing;

namespace SmartCalc.Core.Parsing;

/*
 * PARSER ĐỆ QUY TRÊN XUỐNG (recursive descent) — mỗi ký hiệu chưa kết thúc là 1 hàm Parse*.
 *
 * VĂN PHẠM (EBNF), từ độ ưu tiên THẤP đến CAO:
 *
 *   Expr    -> Term (('+' | '-') Term)*                      cộng/trừ, kết hợp trái
 *   Term    -> Factor (('*' | '/' | '%') Factor | Implicit)*  nhân/chia/mod, kết hợp trái
 *   Implicit-> Factor   khi token kế là Identifier hoặc '('   nhân ngầm: 2pi, 3(4+1), 2sin(30)
 *   Factor  -> ('-' | '+') Factor | Power                     dấu một ngôi
 *   Power   -> Postfix ('^' Factor)?                          luỹ thừa, kết hợp phải
 *   Postfix -> Primary '!'*                                   giai thừa
 *   Primary -> Number | Constant | Variable
 *            | Func '(' Expr ')' | '(' Expr ')'
 *
 *   Constant = pi | e | Ans,  Variable = x (chỉ khi vẽ đồ thị),
 *   Func = sin cos tan asin acos atan ln log sqrt abs fact
 *
 * KHÁC SPEC: SPEC ghi "Factor -> Unary ('^' Factor)?, Unary -> ('-'|'+') Unary | Postfix",
 * khi đó -2^2 bị hiểu là (-2)^2 = 4. Ta đảo hai tầng để dấu trừ một ngôi có ưu tiên THẤP hơn '^'
 * (giống quy ước toán học): -2^2 = -(2^2) = -4, đồng thời vẫn cho phép 2^-1 vì vế phải của '^' là Factor.
 * Xem docs/grammar.md.
 */
public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly bool _allowVariable;
    private int _pos;

    private Parser(IReadOnlyList<Token> tokens, bool allowVariable)
    {
        _tokens = tokens;
        _allowVariable = allowVariable;
    }

    /// <summary>Phân tích chuỗi biểu thức thành AST. allowVariable = cho phép biến x (vẽ đồ thị).</summary>
    public static Node Parse(string input, bool allowVariable = false) =>
        Parse(Lexer.Tokenize(input), allowVariable);

    public static Node Parse(IReadOnlyList<Token> tokens, bool allowVariable = false)
    {
        var parser = new Parser(tokens, allowVariable);
        var tree = parser.ParseExpr();
        if (parser.Current.Kind != TokenKind.End)
            throw Unexpected(parser.Current);
        ParserLimits.CheckLength(tokens);
        return tree;
    }

    private int _depth;   // số mức đệ quy hiện tại qua ParseFactor (xem ParserLimits)

    private Token Current => _tokens[_pos];
    private Token Previous => _tokens[_pos - 1];
    private Token Advance() => _tokens[_pos++];
    private bool At(params TokenKind[] kinds) => kinds.Contains(Current.Kind);

    // Expr -> Term (('+' | '-') Term)*
    private Node ParseExpr()
    {
        var left = ParseTerm();
        while (At(TokenKind.Plus, TokenKind.Minus))
        {
            var op = Advance();
            var right = ParseTerm();
            left = new BinaryNode(op.Kind == TokenKind.Plus ? BinaryOp.Add : BinaryOp.Subtract, left, right, op.Position);
        }
        return left;
    }

    // Term -> Factor (('*' | '/' | '%') Factor | Implicit)*
    private Node ParseTerm()
    {
        var left = ParseFactor();
        while (true)
        {
            if (At(TokenKind.Star, TokenKind.Slash, TokenKind.Percent))
            {
                var op = Advance();
                var kind = op.Kind switch
                {
                    TokenKind.Star => BinaryOp.Multiply,
                    TokenKind.Slash => BinaryOp.Divide,
                    _ => BinaryOp.Modulo,
                };
                left = new BinaryNode(kind, left, ParseFactor(), op.Position);
            }
            else if (At(TokenKind.Identifier, TokenKind.LParen))
            {
                // Nhân ngầm: sau một Factor mà gặp tên hoặc '(' thì coi như có dấu '*'
                int position = Current.Position;
                left = new BinaryNode(BinaryOp.Multiply, left, ParseFactor(), position, Implicit: true);
            }
            else
            {
                return left;
            }
        }
    }

    // Factor -> ('-' | '+') Factor | Power
    // Mọi vòng đệ quy (ngoặc, dấu một ngôi, vế phải của '^') đều đi qua đây -> đếm độ sâu tại đây.
    private Node ParseFactor()
    {
        if (++_depth > ParserLimits.MaxDepth) throw ParserLimits.TooDeep(Current);
        try
        {
            if (At(TokenKind.Minus, TokenKind.Plus))
            {
                var op = Advance();
                var operand = ParseFactor();
                return new UnaryNode(op.Kind == TokenKind.Minus ? UnaryOp.Minus : UnaryOp.Plus, operand, op.Position);
            }
            return ParsePower();
        }
        finally
        {
            _depth--;
        }
    }

    // Power -> Postfix ('^' Factor)?      — đệ quy phải => kết hợp phải: 2^3^2 = 2^(3^2)
    private Node ParsePower()
    {
        var b = ParsePostfix();
        if (!At(TokenKind.Caret)) return b;
        var op = Advance();
        var exponent = ParseFactor();
        return new BinaryNode(BinaryOp.Power, b, exponent, op.Position);
    }

    // Postfix -> Primary '!'*
    private Node ParsePostfix()
    {
        var node = ParsePrimary();
        while (At(TokenKind.Bang))
            node = new FactorialNode(node, Advance().Position);
        return node;
    }

    // Primary -> Number | Constant | Variable | Func '(' Expr ')' | '(' Expr ')'
    private Node ParsePrimary()
    {
        var tok = Current;
        switch (tok.Kind)
        {
            case TokenKind.Number:
                Advance();
                return new NumberNode(ParseNumber(tok), tok.Text, tok.Position);

            case TokenKind.Identifier:
                return ParseIdentifier();

            case TokenKind.LParen:
                return ParseParenthesized();

            case TokenKind.End:
                throw MissingOperand(tok, _pos > 0 ? $"Thiếu toán hạng sau '{Previous.Text}'" : "Biểu thức rỗng");

            case TokenKind.RParen:
                throw MissingOperand(tok, _pos > 0 && Previous.Kind == TokenKind.LParen
                    ? "Thiếu biểu thức trong ngoặc"
                    : "Thiếu toán hạng trước ')'");

            case TokenKind.Star or TokenKind.Slash or TokenKind.Percent or TokenKind.Caret or TokenKind.Bang:
                throw MissingOperand(tok, $"Thiếu toán hạng trước '{tok.Text}'");

            default:
                throw Unexpected(tok);
        }
    }

    private Node ParseIdentifier()
    {
        var tok = Advance();

        if (Builtins.Find(Builtins.Functions, tok.Text) is { } func)
        {
            if (!At(TokenKind.LParen))
                throw new CalcException($"Hàm '{func}' cần dấu '(' theo sau (vị trí {Current.Position})", Current.Position);
            var arg = ParseParenthesized();
            return new CallNode(func, arg, tok.Position);
        }

        if (Builtins.Find(Builtins.Constants, tok.Text) is { } constant)
            return new ConstantNode(constant, tok.Position);

        if (string.Equals(tok.Text, Builtins.Variable, StringComparison.OrdinalIgnoreCase))
        {
            if (!_allowVariable)
                throw new CalcException($"Biến 'x' chỉ dùng được khi vẽ đồ thị (vị trí {tok.Position})", tok.Position);
            return new VariableNode(Builtins.Variable, tok.Position);
        }

        throw new CalcException($"Tên không xác định '{tok.Text}' tại vị trí {tok.Position}", tok.Position);
    }

    // '(' Expr ')'
    private Node ParseParenthesized()
    {
        var open = Advance(); // '('
        var inner = ParseExpr();
        if (At(TokenKind.RParen))
        {
            Advance();
            return inner;
        }
        if (At(TokenKind.End))
            throw new CalcException($"Thiếu dấu ')' cho '(' tại vị trí {open.Position}", open.Position);
        throw Unexpected(Current);
    }

    // Đổi lexeme số -> double. Hỗ trợ cả 0x/0b/0o (khi lexer ở chế độ lập trình).
    private static double ParseNumber(Token tok)
    {
        string text = tok.Text;
        double value;
        if (text.Length > 2 && text[0] == '0' && char.ToLowerInvariant(text[1]) is 'x' or 'b' or 'o')
        {
            int radix = char.ToLowerInvariant(text[1]) switch { 'x' => 16, 'b' => 2, _ => 8 };
            try
            {
                value = Convert.ToUInt64(text[2..], radix);
            }
            catch (OverflowException)
            {
                throw new CalcException($"Tràn số: '{text}' quá lớn (vị trí {tok.Position})", tok.Position);
            }
        }
        else
        {
            value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        if (double.IsInfinity(value))
            throw new CalcException($"Tràn số: '{text}' quá lớn (vị trí {tok.Position})", tok.Position);
        return value;
    }

    private static CalcException MissingOperand(Token at, string message) =>
        new($"{message} (vị trí {at.Position})", at.Position);

    private static CalcException Unexpected(Token tok) => tok.Kind switch
    {
        TokenKind.RParen => new CalcException($"Thừa dấu ')' tại vị trí {tok.Position}", tok.Position),
        TokenKind.Number => new CalcException($"Thiếu toán tử trước '{tok.Text}' (vị trí {tok.Position})", tok.Position),
        _ => new CalcException($"Ký hiệu '{tok.Text}' không hợp lệ ở vị trí {tok.Position}", tok.Position),
    };
}
