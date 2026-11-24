using System.Globalization;

namespace SmartCalc.Core.Lexing;

/// <summary>Đọc một số decimal: cú pháp do DFA của Lexer quyết định, không nhận biểu thức.</summary>
public static class NumberReader
{
    private static readonly LexerDfa Dfa = new();

    public static bool TryParse(string text, out decimal value)
    {
        value = 0;
        int start = text.Length > 0 && text[0] is '+' or '-' or '−' ? 1 : 0;
        var state = DfaState.Start;
        for (int i = start; i < text.Length; i++)
            state = Dfa.Next(state, LexerDfa.Classify(text[i]));
        if (!Dfa.IsAccepting(state) || Dfa.IsSkip(state) || Dfa.TokenKindOf(state) != TokenKind.Number)
            return false;

        // Chỉ chuyển giá trị sau khi DFA đã nhận trọn số; decimal giữ nguyên quy tắc làm tròn/tràn.
        try
        {
            value = decimal.Parse(text.Replace('−', '-'), NumberStyles.Float, CultureInfo.InvariantCulture);
            return true;
        }
        catch (OverflowException) { return false; }
    }
}
