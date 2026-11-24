namespace SmartCalc.Core.Lexing;

/*
 * ÔTÔMAT HỮU HẠN ĐƠN ĐỊNH (DFA) CHO LEXER — cài đặt dạng bảng (table-driven).
 *
 *   M = (Q, Σ, δ, q0, F)
 *   - Q  : enum DfaState
 *   - Σ  : bảng chữ cái được gom thành các lớp ký tự (enum CharClass) để bảng δ nhỏ gọn
 *   - δ  : ma trận _table[state, charClass] -> state (ô trống = Dead, tức "không có cạnh")
 *   - q0 : DfaState.Start
 *   - F  : các khoá của _accepting (trạng thái kết thúc -> loại token) và _skip (khoảng trắng)
 *
 * Mỗi token là một lần chạy DFA từ Start; lexer áp dụng quy tắc "khớp dài nhất" (maximal munch):
 * chạy tới khi gặp Dead, rồi lùi về trạng thái kết thúc gần nhất đã đi qua.
 * Xem docs/dfa-lexer.md để có bảng chuyển trạng thái đầy đủ và sơ đồ.
 */

/// <summary>Lớp ký tự: phân hoạch bảng chữ cái đầu vào.</summary>
public enum CharClass
{
    Zero,      // '0'
    One,       // '1'
    Oct,       // '2'..'7'
    Dec,       // '8', '9'
    Dot,       // '.'
    LetterE,   // 'e', 'E'  (số mũ, cũng là chữ số hex)
    LetterB,   // 'b', 'B'  (tiền tố 0b, cũng là chữ số hex)
    LetterO,   // 'o', 'O'  (tiền tố 0o)
    LetterX,   // 'x', 'X'  (tiền tố 0x)
    HexLetter, // 'a','c','d','f' (hoa/thường) — các chữ số hex còn lại
    Letter,    // các chữ cái ASCII khác
    Symbol,    // 'π', '√' — ký hiệu một ký tự, luôn là một định danh riêng (bí danh của pi, sqrt)
    Plus, Minus, Star, Slash, Caret, Percent, Bang,
    LParen, RParen, Comma,
    Amp, Pipe, Tilde, Less, Greater,
    Space,     // khoảng trắng
    Other,     // mọi ký tự còn lại -> ký tự lạ
}

/// <summary>Tập trạng thái Q của DFA.</summary>
public enum DfaState
{
    Dead = 0,   // trạng thái chết (không có cạnh) — giá trị mặc định của ô trong bảng
    Start,      // q0

    // --- Số thập phân ---
    Zero,       // "0"            (kết thúc) — tách riêng để rẽ nhánh sang 0x/0b/0o
    Int,        // "12"           (kết thúc)
    IntDot,     // "12."          (kết thúc)
    LeadDot,    // "."            (chưa kết thúc, cần chữ số: ".5")
    Frac,       // "12.5", ".5"   (kết thúc)
    ExpMark,    // "1.5e"         (chưa kết thúc)
    ExpSign,    // "1.5e-"        (chưa kết thúc)
    Exp,        // "1.5e-3"       (kết thúc)

    // --- Số có tiền tố (chỉ ở chế độ lập trình) ---
    HexPrefix,  // "0x"  (kết thúc Error khi chưa có chữ số)
    Hex,        // "0xFF"(kết thúc)
    BinPrefix,  // "0b"  (kết thúc Error khi chưa có chữ số)
    Bin,        // "0b101"
    OctPrefix,  // "0o"  (kết thúc Error khi chưa có chữ số)
    OctNum,     // "0o17"
    RadixInt,   // số nguyên không tiền tố theo cơ số đang chọn, vd "FF" ở hệ HEX (kết thúc)
    BadNumber,  // số sai: kết thúc sinh Error, thắng token số ngắn hơn

    // --- Định danh ---
    Ident,      // "sin", "pi", "Ans" (kết thúc)
    SymbolIdent,// "π", "√"           (kết thúc, không có cạnh ra -> "πAns" tách thành "π", "Ans")

    // --- Khoảng trắng ---
    Space,      // (kết thúc, nhưng bỏ qua — không sinh token)

    // --- Toán tử / dấu câu (mỗi cái 1 trạng thái kết thúc) ---
    Plus, Minus, Star, Slash, Caret, Percent, Bang,
    LParen, RParen, Comma,
    Amp, Pipe, Tilde,
    Less,       // "<"  (chưa kết thúc)
    ShiftLeft,  // "<<"
    Greater,    // ">"  (chưa kết thúc)
    ShiftRight, // ">>"
}

/// <summary>
/// Bảng chuyển trạng thái của DFA. Bảng được dựng một lần trong constructor;
/// ở chế độ lập trình có thêm các cạnh cho 0x/0b/0o và toán tử bit.
/// </summary>
public sealed class LexerDfa
{
    private static readonly int StateCount = Enum.GetValues<DfaState>().Length;
    private static readonly int ClassCount = Enum.GetValues<CharClass>().Length;

    // δ: ma trận [trạng thái, lớp ký tự] -> trạng thái kế tiếp
    private readonly DfaState[,] _table = new DfaState[StateCount, ClassCount];

    // F: trạng thái kết thúc -> loại token sinh ra
    private readonly Dictionary<DfaState, TokenKind> _accepting = new();

    // Trạng thái kết thúc nhưng không sinh token (khoảng trắng)
    private readonly HashSet<DfaState> _skip = new() { DfaState.Space };

    public bool ProgrammerMode { get; }

    /// <summary>Cơ số của số không có tiền tố (chỉ có ý nghĩa ở chế độ lập trình): 2, 8, 10, 16.</summary>
    public int Radix { get; }

    public LexerDfa(bool programmerMode = false, int radix = 10)
    {
        if (radix is not (2 or 8 or 10 or 16)) throw new ArgumentOutOfRangeException(nameof(radix));
        ProgrammerMode = programmerMode;
        Radix = programmerMode ? radix : 10;
        BuildTable();
    }

    /// <summary>Hàm chuyển δ(q, c).</summary>
    public DfaState Next(DfaState state, CharClass cls) => _table[(int)state, (int)cls];

    public bool IsAccepting(DfaState state) => _accepting.ContainsKey(state) || _skip.Contains(state);

    public bool IsSkip(DfaState state) => _skip.Contains(state);

    public TokenKind TokenKindOf(DfaState state) => _accepting[state];

    /// <summary>Phân lớp một ký tự vào CharClass (ánh xạ Σ -> lớp ký tự).</summary>
    public static CharClass Classify(char c)
    {
        switch (c)
        {
            case '0': return CharClass.Zero;
            case '1': return CharClass.One;
            case >= '2' and <= '7': return CharClass.Oct;
            case '8' or '9': return CharClass.Dec;
            case '.': return CharClass.Dot;
            case 'e' or 'E': return CharClass.LetterE;
            case 'b' or 'B': return CharClass.LetterB;
            case 'o' or 'O': return CharClass.LetterO;
            case 'x' or 'X': return CharClass.LetterX;
            case 'a' or 'A' or 'c' or 'C' or 'd' or 'D' or 'f' or 'F': return CharClass.HexLetter;
            case >= 'a' and <= 'z':
            case >= 'A' and <= 'Z': return CharClass.Letter;
            case 'π' or '√': return CharClass.Symbol;   // ký hiệu trên bàn phím (bí danh của pi, sqrt)
            case '+': return CharClass.Plus;
            case '-' or '−': return CharClass.Minus;   // '−' (dấu trừ Unicode)
            case '*' or '×': return CharClass.Star;    // '×'
            case '/' or '÷': return CharClass.Slash;   // '÷'
            case '^': return CharClass.Caret;
            case '%': return CharClass.Percent;
            case '!': return CharClass.Bang;
            case '(': return CharClass.LParen;
            case ')': return CharClass.RParen;
            case ',': return CharClass.Comma;
            case '&': return CharClass.Amp;
            case '|': return CharClass.Pipe;
            case '~': return CharClass.Tilde;
            case '<': return CharClass.Less;
            case '>': return CharClass.Greater;
        }
        return char.IsWhiteSpace(c) ? CharClass.Space : CharClass.Other;
    }

    // ------------------------------------------------------------------
    // Dựng bảng δ. Mỗi dòng Add(...) là một cạnh của sơ đồ trạng thái.
    // ------------------------------------------------------------------
    private void BuildTable()
    {
        CharClass[] digits = { CharClass.Zero, CharClass.One, CharClass.Oct, CharClass.Dec };
        CharClass[] letters =
        {
            CharClass.LetterE, CharClass.LetterB, CharClass.LetterO, CharClass.LetterX,
            CharClass.HexLetter, CharClass.Letter,
        };

        Accept(TokenKind.Error, DfaState.BadNumber);
        Add(DfaState.BadNumber, [.. digits, .. letters, CharClass.Dot], DfaState.BadNumber);

        // ===== Định danh: [a-zA-Z]+ =====
        Add(DfaState.Start, letters, DfaState.Ident);
        Add(DfaState.Ident, letters, DfaState.Ident);
        Add(DfaState.Start, CharClass.Symbol, DfaState.SymbolIdent);
        Accept(TokenKind.Identifier, DfaState.Ident, DfaState.SymbolIdent);

        // ===== Khoảng trắng: \s+ (bỏ qua) =====
        Add(DfaState.Start, CharClass.Space, DfaState.Space);
        Add(DfaState.Space, CharClass.Space, DfaState.Space);

        // ===== Toán tử, ngoặc, dấu phẩy: mỗi ký tự -> 1 trạng thái kết thúc =====
        Single(CharClass.Plus, DfaState.Plus, TokenKind.Plus);
        Single(CharClass.Minus, DfaState.Minus, TokenKind.Minus);
        Single(CharClass.Star, DfaState.Star, TokenKind.Star);
        Single(CharClass.Slash, DfaState.Slash, TokenKind.Slash);
        Single(CharClass.Caret, DfaState.Caret, TokenKind.Caret);
        Single(CharClass.Percent, DfaState.Percent, TokenKind.Percent);
        Single(CharClass.Bang, DfaState.Bang, TokenKind.Bang);
        Single(CharClass.LParen, DfaState.LParen, TokenKind.LParen);
        Single(CharClass.RParen, DfaState.RParen, TokenKind.RParen);
        Single(CharClass.Comma, DfaState.Comma, TokenKind.Comma);

        if (!ProgrammerMode)
        {
            BuildDecimalNumbers(digits);
            return;
        }

        // ===== Chế độ lập trình: số nguyên theo cơ số Radix, không tiền tố =====
        // (dựng SAU định danh: ở hệ HEX, cạnh Start --a..f--> RadixInt ghi đè cạnh sang Ident)
        CharClass[] hexDigits = [.. digits, CharClass.LetterE, CharClass.LetterB, CharClass.HexLetter];
        CharClass[] binDigits = [CharClass.Zero, CharClass.One];
        CharClass[] octDigits = [CharClass.Zero, CharClass.One, CharClass.Oct];
        CharClass[] radixDigits = Radix switch { 2 => binDigits, 8 => octDigits, 16 => hexDigits, _ => digits };

        Add(DfaState.Start, radixDigits, DfaState.RadixInt);
        Add(DfaState.Start, CharClass.Zero, DfaState.Zero);
        Add(DfaState.Zero, radixDigits, DfaState.RadixInt);
        Add(DfaState.RadixInt, radixDigits, DfaState.RadixInt);

        // Mọi hậu tố sai cơ số đều là cạnh tới BadNumber; cạnh hợp lệ ghi đè bên dưới.
        foreach (var state in new[] { DfaState.Zero, DfaState.RadixInt,
                     DfaState.HexPrefix, DfaState.Hex, DfaState.BinPrefix, DfaState.Bin,
                     DfaState.OctPrefix, DfaState.OctNum })
            Add(state, [.. digits, .. letters, CharClass.Dot], DfaState.BadNumber);
        Add(DfaState.Zero, radixDigits, DfaState.RadixInt);
        Add(DfaState.RadixInt, radixDigits, DfaState.RadixInt);

        // ===== Tiền tố 0x.., 0o.., 0b.. (0b không dùng ở hệ HEX vì 'b' là chữ số hex) =====
        Accept(TokenKind.Error, DfaState.HexPrefix, DfaState.BinPrefix, DfaState.OctPrefix);
        Add(DfaState.Zero, CharClass.LetterX, DfaState.HexPrefix);
        Add(DfaState.HexPrefix, hexDigits, DfaState.Hex);
        Add(DfaState.Hex, hexDigits, DfaState.Hex);

        Add(DfaState.Zero, CharClass.LetterO, DfaState.OctPrefix);
        Add(DfaState.OctPrefix, octDigits, DfaState.OctNum);
        Add(DfaState.OctNum, octDigits, DfaState.OctNum);

        if (Radix != 16)
        {
            Add(DfaState.Zero, CharClass.LetterB, DfaState.BinPrefix);
            Add(DfaState.BinPrefix, binDigits, DfaState.Bin);
            Add(DfaState.Bin, binDigits, DfaState.Bin);
        }

        Accept(TokenKind.Number, DfaState.Zero, DfaState.RadixInt, DfaState.Hex, DfaState.Bin, DfaState.OctNum);

        // ===== Toán tử bit: & | ~ << >> =====
        Single(CharClass.Amp, DfaState.Amp, TokenKind.Ampersand);
        Single(CharClass.Pipe, DfaState.Pipe, TokenKind.Pipe);
        Single(CharClass.Tilde, DfaState.Tilde, TokenKind.Tilde);
        Add(DfaState.Start, CharClass.Less, DfaState.Less);
        Add(DfaState.Less, CharClass.Less, DfaState.ShiftLeft);
        Add(DfaState.Start, CharClass.Greater, DfaState.Greater);
        Add(DfaState.Greater, CharClass.Greater, DfaState.ShiftRight);
        Accept(TokenKind.ShiftLeft, DfaState.ShiftLeft);
        Accept(TokenKind.ShiftRight, DfaState.ShiftRight);
    }

    // ===== Số thập phân (chế độ thường): [0-9]+ ('.' [0-9]*)? ([eE] [+-]? [0-9]+)?  |  '.' [0-9]+ (...)? =====
    private void BuildDecimalNumbers(CharClass[] digits)
    {
        Add(DfaState.Start, CharClass.Zero, DfaState.Zero);
        Add(DfaState.Start, [CharClass.One, CharClass.Oct, CharClass.Dec], DfaState.Int);
        Add(DfaState.Start, CharClass.Dot, DfaState.LeadDot);

        Add(DfaState.Zero, digits, DfaState.Int);
        Add(DfaState.Zero, CharClass.Dot, DfaState.IntDot);
        Add(DfaState.Zero, CharClass.LetterE, DfaState.ExpMark);

        Add(DfaState.Int, digits, DfaState.Int);
        Add(DfaState.Int, CharClass.Dot, DfaState.IntDot);
        Add(DfaState.Int, CharClass.LetterE, DfaState.ExpMark);

        Add(DfaState.IntDot, digits, DfaState.Frac);
        Add(DfaState.IntDot, CharClass.LetterE, DfaState.ExpMark);

        Add(DfaState.LeadDot, digits, DfaState.Frac);

        Add(DfaState.Frac, digits, DfaState.Frac);
        Add(DfaState.Frac, CharClass.LetterE, DfaState.ExpMark);

        Add(DfaState.ExpMark, [CharClass.Plus, CharClass.Minus], DfaState.ExpSign);
        Add(DfaState.ExpMark, digits, DfaState.Exp);
        Add(DfaState.ExpSign, digits, DfaState.Exp);
        Add(DfaState.Exp, digits, DfaState.Exp);

        Accept(TokenKind.Number, DfaState.Zero, DfaState.Int, DfaState.IntDot, DfaState.Frac, DfaState.Exp);
        Add(DfaState.IntDot, CharClass.Dot, DfaState.BadNumber);
        Add(DfaState.Frac, CharClass.Dot, DfaState.BadNumber);
        Add(DfaState.Exp, CharClass.Dot, DfaState.BadNumber);
    }

    private void Add(DfaState from, CharClass cls, DfaState to) => _table[(int)from, (int)cls] = to;

    private void Add(DfaState from, CharClass[] classes, DfaState to)
    {
        foreach (var cls in classes) Add(from, cls, to);
    }

    private void Accept(TokenKind kind, params DfaState[] states)
    {
        foreach (var s in states) _accepting[s] = kind;
    }

    private void Single(CharClass cls, DfaState state, TokenKind kind)
    {
        Add(DfaState.Start, cls, state);
        Accept(kind, state);
    }
}
