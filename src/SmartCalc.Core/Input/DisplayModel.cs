namespace SmartCalc.Core.Input;

/// <summary>
/// Nội dung màn hình 2 dòng, suy ra từ trạng thái máy (dùng chung cho mọi chế độ có bàn phím).
///   Dòng trên = biểu thức; khi lỗi tách 3 phần TopText | TopMark | TopAfter để tô đỏ ký tự ở vị trí lỗi.
///   Dòng dưới = kết quả / xem trước / thông báo lỗi.
/// </summary>
public sealed record DisplayModel(
    string TopText, string TopMark, string TopAfter, string BottomText, bool IsError, bool IsPreview)
{
    public static DisplayModel From(InputStateMachine m)
    {
        string expr = m.Expression;
        switch (m.State)
        {
            case InputState.Typing:
                return new(expr, "", "", m.Preview ?? "", false, true);

            case InputState.ShowingResult:
                return new(expr + " =", "", "", m.Result, false, false);

            case InputState.Error:
                int pos = m.ErrorPosition;
                if (pos < 0 || pos > expr.Length)
                    return new(expr, "", "", m.ErrorMessage, true, false);
                string mark = pos < expr.Length ? expr[pos].ToString() : " ";   // lỗi ở cuối: tô một ô trống
                string after = pos + 1 < expr.Length ? expr[(pos + 1)..] : "";
                return new(expr[..pos], mark, after, m.ErrorMessage, true, false);

            default:
                return new("", "", "", "0", false, false);
        }
    }
}
