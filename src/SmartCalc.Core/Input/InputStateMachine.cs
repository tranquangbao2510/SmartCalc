using SmartCalc.Core.Errors;
using SmartCalc.Core.Evaluation;

namespace SmartCalc.Core.Input;

/*
 * MÁY TRẠNG THÁI NHẬP LIỆU (máy Mealy: đầu ra = hành động gắn trên cạnh).
 *
 *   Trạng thái : Empty, Typing, ShowingResult, Error
 *   Đầu vào    : InputKind (lớp phím) — Operand, Operator, Equals, Clear, Backspace, Negate, Load
 *   Bảng       : Table[(trạng thái, lớp phím)] = (hành động, trạng thái kế, trạng thái kế khi hành động thất bại)
 *
 * Bảng phủ ĐỦ mọi cặp (trạng thái, lớp phím) — có test kiểm tra — nên không có if-else rải rác:
 * Process() chỉ tra bảng rồi thực thi hành động. Một số hành động có "điều kiện canh" (guard):
 *   - Evaluate  : thành công -> Next (ShowingResult), lỗi -> OnFailure (Error)
 *   - DeleteLast/Replace : biểu thức còn ký tự -> Next (Typing), rỗng -> OnFailure (Empty)
 * Xem docs/state-machine.md.
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

public enum InputAction
{
    Ignore,          // không làm gì
    Append,          // nối text vào biểu thức
    StartNew,        // bỏ kết quả cũ, bắt đầu biểu thức mới bằng text
    ContinueFromAns, // biểu thức mới = "Ans" + text
    Evaluate,        // tính biểu thức
    ClearAll,        // xoá hết
    DeleteLast,      // xoá ký tự/tên hàm cuối
    ToggleNegate,    // đổi dấu cả biểu thức: e <-> −(e)
    NegateAns,       // biểu thức mới = "−Ans"
    Replace,         // thay cả biểu thức bằng text
    Replay,          // xoá lỗi, về Empty rồi xử lý lại chính phím đó
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

public sealed record Transition(InputAction Action, InputState Next, InputState? OnFailure = null);

public sealed class InputStateMachine
{
    // ======================= BẢNG CHUYỂN TRẠNG THÁI =======================
    public static readonly IReadOnlyDictionary<(InputState, InputKind), Transition> Table =
        new Dictionary<(InputState, InputKind), Transition>
        {
            // ---- Empty: chưa có gì ----
            [(InputState.Empty, InputKind.Operand)]   = new(InputAction.Append, InputState.Typing),
            [(InputState.Empty, InputKind.Operator)]  = new(InputAction.Append, InputState.Typing),
            [(InputState.Empty, InputKind.Equals)]    = new(InputAction.Ignore, InputState.Empty),
            [(InputState.Empty, InputKind.Clear)]     = new(InputAction.ClearAll, InputState.Empty),
            [(InputState.Empty, InputKind.Backspace)] = new(InputAction.Ignore, InputState.Empty),
            [(InputState.Empty, InputKind.Negate)]    = new(InputAction.Append, InputState.Typing),
            [(InputState.Empty, InputKind.Load)]      = new(InputAction.Replace, InputState.Typing, InputState.Empty),

            // ---- Typing: đang gõ biểu thức ----
            [(InputState.Typing, InputKind.Operand)]   = new(InputAction.Append, InputState.Typing),
            [(InputState.Typing, InputKind.Operator)]  = new(InputAction.Append, InputState.Typing),
            [(InputState.Typing, InputKind.Equals)]    = new(InputAction.Evaluate, InputState.ShowingResult, InputState.Error),
            [(InputState.Typing, InputKind.Clear)]     = new(InputAction.ClearAll, InputState.Empty),
            [(InputState.Typing, InputKind.Backspace)] = new(InputAction.DeleteLast, InputState.Typing, InputState.Empty),
            [(InputState.Typing, InputKind.Negate)]    = new(InputAction.ToggleNegate, InputState.Typing),
            [(InputState.Typing, InputKind.Load)]      = new(InputAction.Replace, InputState.Typing, InputState.Empty),

            // ---- ShowingResult: vừa bấm '=' thành công ----
            [(InputState.ShowingResult, InputKind.Operand)]   = new(InputAction.StartNew, InputState.Typing),
            [(InputState.ShowingResult, InputKind.Operator)]  = new(InputAction.ContinueFromAns, InputState.Typing),
            [(InputState.ShowingResult, InputKind.Equals)]    = new(InputAction.Ignore, InputState.ShowingResult),
            [(InputState.ShowingResult, InputKind.Clear)]     = new(InputAction.ClearAll, InputState.Empty),
            [(InputState.ShowingResult, InputKind.Backspace)] = new(InputAction.Ignore, InputState.ShowingResult),
            [(InputState.ShowingResult, InputKind.Negate)]    = new(InputAction.NegateAns, InputState.Typing),
            [(InputState.ShowingResult, InputKind.Load)]      = new(InputAction.Replace, InputState.Typing, InputState.Empty),

            // ---- Error: phím bất kỳ -> xoá lỗi, xử lý như Empty ----
            [(InputState.Error, InputKind.Operand)]   = new(InputAction.Replay, InputState.Empty),
            [(InputState.Error, InputKind.Operator)]  = new(InputAction.Replay, InputState.Empty),
            [(InputState.Error, InputKind.Equals)]    = new(InputAction.Replay, InputState.Empty),
            [(InputState.Error, InputKind.Clear)]     = new(InputAction.Replay, InputState.Empty),
            [(InputState.Error, InputKind.Backspace)] = new(InputAction.Replay, InputState.Empty),
            [(InputState.Error, InputKind.Negate)]    = new(InputAction.Replay, InputState.Empty),
            [(InputState.Error, InputKind.Load)]      = new(InputAction.Replay, InputState.Empty),
        };

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
    public InputState Process(InputEvent input)
    {
        var transition = Table[(State, input.Kind)];

        if (transition.Action == InputAction.Replay)
        {
            Execute(InputAction.ClearAll, input);
            State = transition.Next;           // = Empty
            return Process(input);
        }

        bool ok = Execute(transition.Action, input);
        State = ok ? transition.Next : transition.OnFailure ?? transition.Next;
        UpdatePreview();
        return State;
    }

    /// <summary>Gọi lại khi đổi cấu hình engine (DEG/RAD, cơ số, kích thước word) để cập nhật xem trước.</summary>
    public void Refresh() => UpdatePreview();

    // Thực thi hành động; trả về false khi "điều kiện canh" thất bại (xem bảng: cột OnFailure).
    private bool Execute(InputAction action, InputEvent input)
    {
        switch (action)
        {
            case InputAction.Ignore:
                return true;
            case InputAction.Append:
                Expression = ExpressionEditor.Append(Expression, input.Kind == InputKind.Negate ? "−" : input.Text);
                return true;
            case InputAction.StartNew:
                Expression = input.Text;
                _last = null;
                return true;
            case InputAction.ContinueFromAns:
                Expression = Engine.AnsText + input.Text;
                _last = null;
                return true;
            case InputAction.NegateAns:
                Expression = "−" + Engine.AnsText;
                _last = null;
                return true;
            case InputAction.Evaluate:
                return Evaluate();
            case InputAction.ClearAll:
                Expression = "";
                _last = null;
                ErrorMessage = "";
                ErrorPosition = -1;
                return true;
            case InputAction.DeleteLast:
                Expression = ExpressionEditor.DeleteLast(Expression);
                return Expression.Length > 0;
            case InputAction.ToggleNegate:
                Expression = ExpressionEditor.ToggleNegate(Expression);
                return true;
            case InputAction.Replace:
                Expression = input.Text;
                _last = null;
                return Expression.Length > 0;
            default:
                throw new InvalidOperationException($"Hành động không hỗ trợ: {action}");
        }
    }

    private bool Evaluate()
    {
        try
        {
            var result = Engine.Evaluate(Expression);
            string forHistory = Engine.ForHistory(Expression);   // trước Commit: còn Ans cũ
            Engine.Commit(result);
            _last = result;
            ErrorMessage = "";
            ErrorPosition = -1;
            Evaluated?.Invoke(forHistory, result.Display);
            return true;
        }
        catch (CalcException ex)
        {
            _last = null;
            ErrorMessage = ex.Message;
            ErrorPosition = ex.Position;
            return false;
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
