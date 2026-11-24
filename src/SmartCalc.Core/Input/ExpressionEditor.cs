using SmartCalc.Core.Evaluation;

namespace SmartCalc.Core.Input;

/// <summary>Các thao tác soạn thảo chuỗi biểu thức (thuần tuý, không trạng thái — dễ test).</summary>
public static class ExpressionEditor
{
    // Toán tử hai ngôi: gõ toán tử mới ngay sau toán tử cũ thì thay thế (2+ rồi × -> 2×)
    private static bool IsBinaryOperator(char c) => c is '+' or '-' or '−' or '*' or '×' or '/' or '÷' or '^' or '%';

    public static string Append(string expression, string text)
    {
        if (text.Length == 1 && expression.Length > 0
            && IsBinaryOperator(text[0]) && text[0] is not ('-' or '−')
            && IsBinaryOperator(expression[^1]))
        {
            return expression[..^1] + text;
        }
        return expression + text;
    }

    /// <summary>Xoá phần tử cuối: cả tên hàm "sin(" hoặc tên "Ans"/"pi" một lần, còn lại xoá 1 ký tự.</summary>
    public static string DeleteLast(string expression)
    {
        if (expression.Length == 0) return expression;

        int end = expression.Length;
        if (expression[^1] == '(') end--;           // "sin(" -> xét phần chữ trước '('
        int start = end;
        while (start > 0 && char.IsAsciiLetter(expression[start - 1])) start--;

        // Có chữ cái liền trước (tên hàm/hằng) -> xoá cả tên; nếu không chỉ xoá 1 ký tự
        return start < end ? expression[..start] : expression[..^1];
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
