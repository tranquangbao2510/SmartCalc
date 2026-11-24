using SmartCalc.Core.Errors;

namespace SmartCalc.Core.Lexing;

/// <summary>
/// Lexer chạy DFA (xem <see cref="LexerDfa"/>) theo quy tắc khớp dài nhất (maximal munch):
///   1. Từ vị trí hiện tại, chạy DFA từ Start, ghi nhớ trạng thái kết thúc cuối cùng đã đi qua.
///   2. Khi gặp Dead (hoặc hết chuỗi): cắt token tới trạng thái kết thúc đó.
///   3. Nếu chưa từng qua trạng thái kết thúc nào -> báo lỗi CalcException kèm vị trí.
/// Không dùng Regex.
/// </summary>
public sealed class Lexer
{
    private readonly LexerDfa _dfa;

    public Lexer(bool programmerMode = false, int radix = 10)
    {
        _dfa = new LexerDfa(programmerMode, radix);
    }

    public static IReadOnlyList<Token> Tokenize(string input, bool programmerMode = false, int radix = 10) =>
        new Lexer(programmerMode, radix).Scan(input);

    public IReadOnlyList<Token> Scan(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tokens = new List<Token>();
        int pos = 0;

        while (pos < input.Length)
        {
            var state = DfaState.Start;
            int i = pos;
            int lastAcceptEnd = -1;              // vị trí ngay sau lexeme dài nhất được chấp nhận
            var lastAcceptState = DfaState.Dead;

            // Chạy DFA cho tới khi không còn cạnh đi tiếp
            while (i < input.Length)
            {
                var next = _dfa.Next(state, LexerDfa.Classify(input[i]));
                if (next == DfaState.Dead) break;
                state = next;
                i++;
                if (_dfa.IsAccepting(state))
                {
                    lastAcceptEnd = i;
                    lastAcceptState = state;
                }
            }

            if (lastAcceptEnd < 0)
                throw ErrorAt(input, pos);

            if (!_dfa.IsSkip(lastAcceptState))
            {
                var kind = _dfa.TokenKindOf(lastAcceptState);
                tokens.Add(new Token(kind, input[pos..lastAcceptEnd], pos));
                if (kind == TokenKind.Number) CheckNumberEnd(input, lastAcceptEnd, lastAcceptState);
            }

            pos = lastAcceptEnd;
        }

        tokens.Add(new Token(TokenKind.End, "", input.Length));
        return tokens;
    }

    // Số không được dính liền dấu '.' (vd "1.2.3"). Số có tiền tố, và mọi số ở chế độ lập trình, không được dính
    // chữ/số phía sau: đó là chữ số sai cơ số (vd "0b102", "0xFG", "102" ở hệ BIN, "1A" ở hệ DEC).
    private void CheckNumberEnd(string input, int end, DfaState state)
    {
        if (end >= input.Length) return;
        char c = input[end];
        bool strict = _dfa.ProgrammerMode || LexerDfa.IsRadixNumber(state);
        if (c == '.' || (strict && char.IsAsciiLetterOrDigit(c)))
            throw new CalcException($"Số không hợp lệ: ký tự '{c}' tại vị trí {end}", end);
    }

    private CalcException ErrorAt(string input, int pos)
    {
        char c = input[pos];
        // Ký tự không có cạnh nào từ Start -> ký tự lạ
        if (_dfa.Next(DfaState.Start, LexerDfa.Classify(c)) == DfaState.Dead)
            return new CalcException($"Ký tự không hợp lệ '{c}' tại vị trí {pos}", pos);
        // Có cạnh nhưng không đi tới được trạng thái kết thúc (vd "." đứng một mình, "<" thiếu "<")
        return new CalcException($"Ký hiệu không hoàn chỉnh '{c}' tại vị trí {pos}", pos);
    }
}
