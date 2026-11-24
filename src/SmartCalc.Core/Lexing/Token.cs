namespace SmartCalc.Core.Lexing;

/// <summary>Các loại token mà lexer nhận dạng được.</summary>
public enum TokenKind
{
    Number,      // 12, 3.14, .5, 1.5e-3; chế độ lập trình: 0xFF, 0b101, 0o17
    Identifier,  // sin, cos, pi, e, Ans, x ... (parser sẽ kiểm tra tên hợp lệ)
    Plus,        // +
    Minus,       // -
    Star,        // *
    Slash,       // /
    Caret,       // ^
    Percent,     // %
    Bang,        // !
    LParen,      // (
    RParen,      // )
    Comma,       // ,
    Ampersand,   // &   (AND bit)
    Pipe,        // |   (OR bit)
    Tilde,       // ~   (NOT bit)
    ShiftLeft,   // <<
    ShiftRight,  // >>
    End,         // kết thúc chuỗi nhập
}

/// <summary>
/// Một token: loại, chuỗi gốc (lexeme) và vị trí bắt đầu (0-based) trong chuỗi nhập.
/// </summary>
public sealed record Token(TokenKind Kind, string Text, int Position)
{
    public override string ToString() => $"{Kind}('{Text}')@{Position}";
}
