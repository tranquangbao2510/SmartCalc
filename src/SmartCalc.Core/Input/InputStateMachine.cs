using SmartCalc.Core.Errors;
using SmartCalc.Core.Evaluation;

namespace SmartCalc.Core.Input;

/*
 * MÁY TRẠNG THÁI NHẬP LIỆU — DFA toàn phần với bảng hành động riêng (máy Mealy).
 *
 * Tập trạng thái Q: Empty (rỗng), Typing (đang nhập), ShowingResult (hiện kết quả), Error (lỗi).
 * Phím thô InputKind được chuẩn hoá thành bảng chữ cái InputSymbol gồm 10 ký hiệu:
 * Operand, Operator, EqualsOk, EqualsError, Clear, BackspaceToEmpty, BackspaceKeep,
 * Negate, LoadEmpty, LoadOk.
 *
 * Cùng phím '=' có thể tính thành công hoặc lỗi; xoá lùi/nạp có thể cho chuỗi rỗng hoặc còn chữ.
 * Prepare xác định kết quả này TRƯỚC khi tra bảng, không sửa trạng thái và không ghi Ans.
 * Nhờ vậy mỗi cặp (trạng thái, ký hiệu) có đúng một đích, không cần điều kiện canh,
 * nhánh thất bại hay gọi lại Process để xử lý lỗi.
 *
 * Table là δ: (InputState, InputSymbol) -> InputState, quyết định trạng thái kế tiếp.
 * ActionTable là λ: cùng cặp khoá -> InputAction, quyết định thao tác dữ liệu/hiển thị.
 * Mỗi bảng có đủ 4 × 10 ô, kể cả mọi ký hiệu từ Error. Process tra hai bảng rồi thực thi;
 * hành động không quyết định lại trạng thái đích. Xem docs/state-machine.md.
 */

public enum InputState { Empty, Typing, ShowingResult, Error }

/// <summary>Lớp phím (bảng chữ cái đầu vào của máy trạng thái).</summary>
public enum InputKind
{
    Operand,   // số, '.', hằng (π, e, Ans), hàm ("sin("), '(' — thứ bắt đầu một toán hạng
    Operator,  // + − × ÷ ^ % ! ')' & | << >> và các phím hậu tố "^2", "^(−1)" — cần toán hạng bên trái
    Equals,    // '=' / Enter
    Clear,     // C / Esc
    Backspace, // ⌫
    Negate,    // ±
    Load,      // nạp nguyên biểu thức (bấm vào lịch sử)
}

public enum InputSymbol
{
    Operand, Operator, EqualsOk, EqualsError, Clear,
    BackspaceToEmpty, BackspaceKeep, Negate, LoadEmpty, LoadOk,
}

public enum InputAction
{
    Ignore, Append, StartNew, ContinueFromAns, CommitResult, ShowError,
    ClearAll, DeleteLast, ToggleNegate, NegateAns, Replace, ClearAndAppend, ClearAndReplace,
}

/// <summary>Một sự kiện nhập: lớp phím + text gắn kèm (vd Operand "7", Operator "×").</summary>
public readonly record struct InputEvent(InputKind Kind, string Text = "")
{
    public static InputEvent EqualsKey => new(InputKind.Equals);
    public static InputEvent Clear => new(InputKind.Clear);
    public static InputEvent Backspace => new(InputKind.Backspace);
    public static InputEvent Negate => new(InputKind.Negate);
    public static InputEvent Load(string expression) => new(InputKind.Load, expression);

    /// <summary>Phân lớp một đoạn text gõ vào theo ký tự đầu tiên (dùng cho bàn phím, nút bấm, dán).</summary>
    public static InputEvent FromText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return new InputEvent(IsOperatorStart(text[0]) ? InputKind.Operator : InputKind.Operand, text);
    }

    public static bool IsOperatorStart(char c) => c is
        '+' or '-' or '−' or '*' or '×' or '/' or '÷' or '^' or '%' or '!' or ')'
        or '&' or '|' or '<' or '>';   // toán tử bit (chế độ lập trình); '~' là một ngôi nên thuộc Operand
}


public sealed class InputStateMachine
{
    public static readonly IReadOnlyDictionary<(InputState, InputSymbol), InputState> Table =
        new System.Collections.ObjectModel.ReadOnlyDictionary<(InputState, InputSymbol), InputState>(
        new Dictionary<(InputState, InputSymbol), InputState>
        {
            [(InputState.Empty, InputSymbol.Operand)] = InputState.Typing,
            [(InputState.Empty, InputSymbol.Operator)] = InputState.Typing,
            [(InputState.Empty, InputSymbol.EqualsOk)] = InputState.Empty,
            [(InputState.Empty, InputSymbol.EqualsError)] = InputState.Empty,
            [(InputState.Empty, InputSymbol.Clear)] = InputState.Empty,
            [(InputState.Empty, InputSymbol.BackspaceToEmpty)] = InputState.Empty,
            [(InputState.Empty, InputSymbol.BackspaceKeep)] = InputState.Empty,
            [(InputState.Empty, InputSymbol.Negate)] = InputState.Typing,
            [(InputState.Empty, InputSymbol.LoadEmpty)] = InputState.Empty,
            [(InputState.Empty, InputSymbol.LoadOk)] = InputState.Typing,
            [(InputState.Typing, InputSymbol.Operand)] = InputState.Typing,
            [(InputState.Typing, InputSymbol.Operator)] = InputState.Typing,
            [(InputState.Typing, InputSymbol.EqualsOk)] = InputState.ShowingResult,
            [(InputState.Typing, InputSymbol.EqualsError)] = InputState.Error,
            [(InputState.Typing, InputSymbol.Clear)] = InputState.Empty,
            [(InputState.Typing, InputSymbol.BackspaceToEmpty)] = InputState.Empty,
            [(InputState.Typing, InputSymbol.BackspaceKeep)] = InputState.Typing,
            [(InputState.Typing, InputSymbol.Negate)] = InputState.Typing,
            [(InputState.Typing, InputSymbol.LoadEmpty)] = InputState.Empty,
            [(InputState.Typing, InputSymbol.LoadOk)] = InputState.Typing,
            [(InputState.ShowingResult, InputSymbol.Operand)] = InputState.Typing,
            [(InputState.ShowingResult, InputSymbol.Operator)] = InputState.Typing,
            [(InputState.ShowingResult, InputSymbol.EqualsOk)] = InputState.ShowingResult,
            [(InputState.ShowingResult, InputSymbol.EqualsError)] = InputState.ShowingResult,
            [(InputState.ShowingResult, InputSymbol.Clear)] = InputState.Empty,
            [(InputState.ShowingResult, InputSymbol.BackspaceToEmpty)] = InputState.ShowingResult,
            [(InputState.ShowingResult, InputSymbol.BackspaceKeep)] = InputState.ShowingResult,
            [(InputState.ShowingResult, InputSymbol.Negate)] = InputState.Typing,
            [(InputState.ShowingResult, InputSymbol.LoadEmpty)] = InputState.Empty,
            [(InputState.ShowingResult, InputSymbol.LoadOk)] = InputState.Typing,
            [(InputState.Error, InputSymbol.Operand)] = InputState.Typing,
            [(InputState.Error, InputSymbol.Operator)] = InputState.Typing,
            [(InputState.Error, InputSymbol.EqualsOk)] = InputState.Empty,
            [(InputState.Error, InputSymbol.EqualsError)] = InputState.Empty,
            [(InputState.Error, InputSymbol.Clear)] = InputState.Empty,
            [(InputState.Error, InputSymbol.BackspaceToEmpty)] = InputState.Empty,
            [(InputState.Error, InputSymbol.BackspaceKeep)] = InputState.Empty,
            [(InputState.Error, InputSymbol.Negate)] = InputState.Typing,
            [(InputState.Error, InputSymbol.LoadEmpty)] = InputState.Empty,
            [(InputState.Error, InputSymbol.LoadOk)] = InputState.Typing,
        });

    public static readonly IReadOnlyDictionary<(InputState, InputSymbol), InputAction> ActionTable =
        new System.Collections.ObjectModel.ReadOnlyDictionary<(InputState, InputSymbol), InputAction>(
        new Dictionary<(InputState, InputSymbol), InputAction>
        {
            [(InputState.Empty, InputSymbol.Operand)] = InputAction.Append,
            [(InputState.Empty, InputSymbol.Operator)] = InputAction.Append,
            [(InputState.Empty, InputSymbol.EqualsOk)] = InputAction.Ignore,
            [(InputState.Empty, InputSymbol.EqualsError)] = InputAction.Ignore,
            [(InputState.Empty, InputSymbol.Clear)] = InputAction.ClearAll,
            [(InputState.Empty, InputSymbol.BackspaceToEmpty)] = InputAction.Ignore,
            [(InputState.Empty, InputSymbol.BackspaceKeep)] = InputAction.Ignore,
            [(InputState.Empty, InputSymbol.Negate)] = InputAction.Append,
            [(InputState.Empty, InputSymbol.LoadEmpty)] = InputAction.Replace,
            [(InputState.Empty, InputSymbol.LoadOk)] = InputAction.Replace,
            [(InputState.Typing, InputSymbol.Operand)] = InputAction.Append,
            [(InputState.Typing, InputSymbol.Operator)] = InputAction.Append,
            [(InputState.Typing, InputSymbol.EqualsOk)] = InputAction.CommitResult,
            [(InputState.Typing, InputSymbol.EqualsError)] = InputAction.ShowError,
            [(InputState.Typing, InputSymbol.Clear)] = InputAction.ClearAll,
            [(InputState.Typing, InputSymbol.BackspaceToEmpty)] = InputAction.DeleteLast,
            [(InputState.Typing, InputSymbol.BackspaceKeep)] = InputAction.DeleteLast,
            [(InputState.Typing, InputSymbol.Negate)] = InputAction.ToggleNegate,
            [(InputState.Typing, InputSymbol.LoadEmpty)] = InputAction.Replace,
            [(InputState.Typing, InputSymbol.LoadOk)] = InputAction.Replace,
            [(InputState.ShowingResult, InputSymbol.Operand)] = InputAction.StartNew,
            [(InputState.ShowingResult, InputSymbol.Operator)] = InputAction.ContinueFromAns,
            [(InputState.ShowingResult, InputSymbol.EqualsOk)] = InputAction.Ignore,
            [(InputState.ShowingResult, InputSymbol.EqualsError)] = InputAction.Ignore,
            [(InputState.ShowingResult, InputSymbol.Clear)] = InputAction.ClearAll,
            [(InputState.ShowingResult, InputSymbol.BackspaceToEmpty)] = InputAction.Ignore,
            [(InputState.ShowingResult, InputSymbol.BackspaceKeep)] = InputAction.Ignore,
            [(InputState.ShowingResult, InputSymbol.Negate)] = InputAction.NegateAns,
            [(InputState.ShowingResult, InputSymbol.LoadEmpty)] = InputAction.Replace,
            [(InputState.ShowingResult, InputSymbol.LoadOk)] = InputAction.Replace,
            [(InputState.Error, InputSymbol.Operand)] = InputAction.ClearAndAppend,
            [(InputState.Error, InputSymbol.Operator)] = InputAction.ClearAndAppend,
            [(InputState.Error, InputSymbol.EqualsOk)] = InputAction.ClearAll,
            [(InputState.Error, InputSymbol.EqualsError)] = InputAction.ClearAll,
            [(InputState.Error, InputSymbol.Clear)] = InputAction.ClearAll,
            [(InputState.Error, InputSymbol.BackspaceToEmpty)] = InputAction.ClearAll,
            [(InputState.Error, InputSymbol.BackspaceKeep)] = InputAction.ClearAll,
            [(InputState.Error, InputSymbol.Negate)] = InputAction.ClearAndAppend,
            [(InputState.Error, InputSymbol.LoadEmpty)] = InputAction.ClearAndReplace,
            [(InputState.Error, InputSymbol.LoadOk)] = InputAction.ClearAndReplace,
        });

    // ======================= DỮ LIỆU HIỂN THỊ =======================
    // ======================= ENGINE TÍNH TOÁN =======================
    /// <summary>Phần "tính biểu thức" (Khoa học hoặc Lập trình). Bảng chuyển trạng thái dùng chung cho mọi engine.</summary>
    public IExpressionEngine Engine { get; }

    public InputStateMachine(IExpressionEngine? engine = null)
    {
        Engine = engine ?? new ScientificEngine();
    }

    // ======================= DỮ LIỆU HIỂN THỊ =======================
    public InputState State { get; private set; } = InputState.Empty;

    /// <summary>Biểu thức đang gõ / vừa tính (dòng trên màn hình).</summary>
    public string Expression { get; private set; } = "";

    /// <summary>Dữ liệu soạn thảo trong chuỗi gốc, không phải trạng thái hay ký hiệu của DFA.</summary>
    public int CursorPosition { get; private set; }

    public void MoveLeft() => MoveCursor(ExpressionEditor.MoveLeft);
    public void MoveRight() => MoveCursor(ExpressionEditor.MoveRight);
    public void Home() => MoveCursor(ExpressionEditor.Home);
    public void End() => MoveCursor(ExpressionEditor.End);
    public void SetCursorPosition(int position)
    {
        if (State is InputState.Typing or InputState.Empty)
            CursorPosition = ExpressionEditor.SetCursor(Expression, position).CursorPosition;
    }

    private void MoveCursor(Func<string, int, (string Expression, int CursorPosition)> move)
    {
        if (State is InputState.Typing or InputState.Empty)
            CursorPosition = move(Expression, CursorPosition).CursorPosition;
    }

    /// <summary>Kết quả đã định dạng (khi ShowingResult).</summary>
    public string Result => _last?.Display ?? "";

    /// <summary>Kết quả xem trước khi đang gõ (null nếu biểu thức chưa hợp lệ).</summary>
    public string? Preview => _preview?.Display;

    public string ErrorMessage { get; private set; } = "";
    public int ErrorPosition { get; private set; } = -1;

    private EngineResult? _preview;   // kết quả xem trước (Typing)
    private EngineResult? _last;      // kết quả của lần '=' gần nhất (ShowingResult)

    /// <summary>Kết quả "hiện tại": vừa tính xong (ShowingResult) hoặc xem trước (Typing); còn lại null.</summary>
    public EngineResult? CurrentResult => State switch
    {
        InputState.ShowingResult => _last,
        InputState.Typing => _preview,
        _ => null,
    };

    /// <summary>Giá trị "hiện tại" cho các phím bộ nhớ M+, M−, MS.</summary>
    public double? CurrentValue => CurrentResult?.Value;

    /// <summary>
    /// Phát ra khi tính thành công: (biểu thức để ghi lịch sử, kết quả đã định dạng).
    /// Ở chế độ Khoa học "Ans×2" được ghi thành "42×2" nên nạp lại lúc nào cũng đúng.
    /// </summary>
    public event Action<string, string>? Evaluated;

    // ======================= XỬ LÝ =======================
    private sealed record PreparedInput(InputSymbol Symbol, string Text = "", EngineResult? Result = null,
        CalcException? Error = null, int Cursor = 0);

    public InputState Process(InputEvent input)
    {
        var prepared = Prepare(input);
        var key = (State, prepared.Symbol);
        var next = Table[key];
        Execute(ActionTable[key], prepared);
        State = next;
        UpdatePreview();
        return State;
    }

    public void Refresh() => UpdatePreview();

    // Kết quả thành ký hiệu đầu vào TRƯỚC khi tra hai bảng; không sửa trạng thái ở đây.
    private PreparedInput Prepare(InputEvent input)
    {
        switch (input.Kind)
        {
            case InputKind.Equals:
                if (State != InputState.Typing) return new(InputSymbol.EqualsOk);
                try { return new(InputSymbol.EqualsOk, Result: Engine.Evaluate(Expression)); }
                catch (CalcException ex) { return new(InputSymbol.EqualsError, Error: ex); }
            case InputKind.Backspace:
                var edit = State == InputState.Typing ? ExpressionEditor.DeleteLast(Expression, CursorPosition) : (Expression: "", CursorPosition: 0);
                return new(edit.Expression.Length == 0 ? InputSymbol.BackspaceToEmpty : InputSymbol.BackspaceKeep,
                    edit.Expression, Cursor: edit.CursorPosition);
            case InputKind.Load:
                return new(input.Text.Length == 0 ? InputSymbol.LoadEmpty : InputSymbol.LoadOk, input.Text);
            case InputKind.Operand: return new(InputSymbol.Operand, input.Text);
            case InputKind.Operator: return new(InputSymbol.Operator, input.Text);
            case InputKind.Clear: return new(InputSymbol.Clear);
            case InputKind.Negate: return new(InputSymbol.Negate, "\u2212");
            default: throw new ArgumentOutOfRangeException(nameof(input));
        }
    }

    private void ClearAll()
    {
        Expression = "";
        CursorPosition = 0;
        _last = null;
        ErrorMessage = "";
        ErrorPosition = -1;
    }

    private void Execute(InputAction action, PreparedInput input)
    {
        switch (action)
        {
            case InputAction.Ignore: break;
            case InputAction.Append:
                (Expression, CursorPosition) = ExpressionEditor.Append(Expression, CursorPosition, input.Text);
                break;
            case InputAction.ClearAndAppend:
                ClearAll();
                (Expression, CursorPosition) = ExpressionEditor.Append(Expression, CursorPosition, input.Text);
                break;
            case InputAction.StartNew:
                (Expression, CursorPosition) = ExpressionEditor.Append("", 0, input.Text);
                _last = null;
                break;
            case InputAction.Replace:
                Expression = input.Text;
                CursorPosition = Expression.Length;
                _last = null;
                break;
            case InputAction.ClearAndReplace:
                ClearAll();
                Expression = input.Text;
                CursorPosition = Expression.Length;
                break;
            case InputAction.ContinueFromAns:
                (Expression, CursorPosition) = ExpressionEditor.Append(Engine.AnsText, Engine.AnsText.Length, input.Text);
                _last = null;
                break;
            case InputAction.NegateAns:
                Expression = "\u2212" + Engine.AnsText;
                CursorPosition = Expression.Length;
                _last = null;
                break;
            case InputAction.CommitResult:
                string forHistory = Engine.ForHistory(Expression);
                Engine.Commit(input.Result!);
                _last = input.Result;
                ErrorMessage = "";
                ErrorPosition = -1;
                Evaluated?.Invoke(forHistory, input.Result!.Display);
                break;
            case InputAction.ShowError:
                _last = null;
                ErrorMessage = input.Error!.Message;
                ErrorPosition = input.Error.Position;
                break;
            case InputAction.ClearAll: ClearAll(); break;
            case InputAction.DeleteLast:
                Expression = input.Text;
                CursorPosition = input.Cursor;
                break;
            case InputAction.ToggleNegate:
                (Expression, CursorPosition) = ExpressionEditor.ToggleNegate(Expression, CursorPosition);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private void UpdatePreview()
    {
        _preview = null;
        if (State != InputState.Typing) return;
        try
        {
            _preview = Engine.Evaluate(Expression);
        }
        catch (CalcException)
        {
            // biểu thức đang gõ dở — không xem trước
        }
    }
}
