using SmartCalc.Core.Evaluation;

namespace SmartCalc.Core.Input;

/// <summary>Các thao tác soạn thảo chuỗi biểu thức (thuần tuý, không trạng thái — dễ test).</summary>
public static class ExpressionEditor
{
    // Toán tử hai ngôi: gõ toán tử mới ngay sau toán tử cũ thì thay thế (2+ rồi × -> 2×)
    private static bool IsBinaryOperator(char c) => c is '+' or '-' or '−' or '*' or '×' or '/' or '÷' or '^' or '%';

    public static string Append(string expression, string text)
        => Append(expression, expression.Length, text).Expression;

    /// <summary>Chèn tại con trỏ; khuôn kết thúc bằng () đặt con trỏ vào trong.</summary>
    public static (string Expression, int CursorPosition) Append(string expression, int cursorPosition, string text)
    {
        int cursor = Math.Clamp(cursorPosition, 0, expression.Length);
        if (text == ")" && cursor < expression.Length && expression[cursor] == ')')
            return (expression, cursor + 1);

        if ((text.Length == 1 || text == "^()") && cursor > 0
            && IsBinaryOperator(text[0]) && text[0] is not ('-' or '−')
            && IsBinaryOperator(expression[cursor - 1]))
        {
            expression = expression.Remove(--cursor, 1);
        }
        string result = expression.Insert(cursor, text);
        int next = cursor + text.Length;
        if (text.EndsWith("()", StringComparison.Ordinal)) next--;
        return (result, next);
    }

    /// <summary>Xoá phần tử cuối: cả tên hàm "sin(" hoặc tên "Ans"/"pi" một lần, còn lại xoá 1 ký tự.</summary>
    public static string DeleteLast(string expression)
        => DeleteLast(expression, expression.Length).Expression;

    public static (string Expression, int CursorPosition) DeleteLast(string expression, int cursorPosition)
    {
        int cursor = Math.Clamp(cursorPosition, 0, expression.Length);
        if (cursor == 0) return (expression, 0);
        int start = MoveLeft(expression, cursor).CursorPosition;
        int end = cursor;
        if (expression[cursor - 1] == '(' && cursor < expression.Length && expression[cursor] == ')') end++;
        return (expression.Remove(start, end - start), start);
    }

    /// <summary>Ranh giới soạn thảo: không dừng giữa tên hàm/hằng, √( hoặc ^(.</summary>
    public static IReadOnlyList<int> CursorStops(string expression)
    {
        var stops = new List<int> { 0 };
        int i = 0;
        while (i < expression.Length)
        {
            if (char.IsAsciiLetter(expression[i]))
            {
                while (i < expression.Length && char.IsAsciiLetter(expression[i])) i++;
                if (i < expression.Length && expression[i] == '(') i++;
            }
            else if (expression[i] is '^' or '√' && i + 1 < expression.Length && expression[i + 1] == '(') i += 2;
            else i++;
            stops.Add(i);
        }
        return stops.AsReadOnly();
    }

    public static (string Expression, int CursorPosition) MoveLeft(string expression, int cursorPosition) =>
        (expression, CursorStops(expression).LastOrDefault(p => p < Math.Clamp(cursorPosition, 0, expression.Length)));

    public static (string Expression, int CursorPosition) MoveRight(string expression, int cursorPosition) =>
        (expression, CursorStops(expression).FirstOrDefault(p => p > Math.Clamp(cursorPosition, 0, expression.Length), expression.Length));

    public static (string Expression, int CursorPosition) Home(string expression, int cursorPosition) => (expression, 0);
    public static (string Expression, int CursorPosition) End(string expression, int cursorPosition) => (expression, expression.Length);

    public static (string Expression, int CursorPosition) SetCursor(string expression, int cursorPosition) =>
        (expression, CursorStops(expression).MinBy(p => Math.Abs((long)p - cursorPosition)));

    public static (string Expression, int CursorPosition) ToggleNegate(string expression, int cursorPosition)
    {
        string result = ToggleNegate(expression);
        return (result, result.Length);
    }

    /// <summary>Đổi dấu cả biểu thức: e -> −(e), và −(e) -> e.</summary>
    public static string ToggleNegate(string expression)
    {
        if (expression.StartsWith("−(", StringComparison.Ordinal) && expression.EndsWith(')') && MatchingParen(expression, 1) == expression.Length - 1)
            return expression[2..^1];
        return $"−({expression})";
    }

    /// <summary>Định dạng một số để chèn vào biểu thức (số âm được bọc ngoặc).</summary>
    public static string FormatForInsert(double value)
    {
        string s = NumberFormatter.Format(value);
        return value < 0 ? $"(−{s[1..]})" : s;
    }

    /// <summary>
    /// Thay mọi tên "Ans" (đứng riêng, không nằm trong tên dài hơn) bằng giá trị của nó — dùng khi ghi lịch sử,
    /// để nạp lại sau này vẫn ra đúng kết quả cũ. Bọc ngoặc nếu dính liền ký tự trước (vd "2Ans" -> "2(42)").
    /// </summary>
    public static string SubstituteAns(string expression, double ans)
    {
        string value = FormatForInsert(ans);
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < expression.Length)
        {
            bool isWord = string.CompareOrdinal(expression, i, "Ans", 0, 3) == 0
                          && (i == 0 || !char.IsAsciiLetter(expression[i - 1]))
                          && (i + 3 >= expression.Length || !char.IsAsciiLetter(expression[i + 3]));
            if (!isWord)
            {
                sb.Append(expression[i++]);
                continue;
            }
            bool glued = i > 0 && (char.IsLetterOrDigit(expression[i - 1]) || expression[i - 1] is '.' or ')' or '!');
            sb.Append(glued && !value.StartsWith('(') ? $"({value})" : value);
            i += 3;
        }
        return sb.ToString();
    }

    private static int MatchingParen(string s, int open)
    {
        int depth = 0;
        for (int i = open; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')' && --depth == 0) return i;
        }
        return -1;
    }
}
