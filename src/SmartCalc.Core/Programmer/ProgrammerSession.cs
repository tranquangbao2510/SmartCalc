using SmartCalc.Core.Input;

namespace SmartCalc.Core.Programmer;

/// <summary>Engine chế độ Lập trình: số nguyên có dấu WordSize bit, nhập theo cơ số Radix.</summary>
public sealed class ProgrammerEngine : IExpressionEngine
{
    public int Radix { get; set; } = 10;
    public WordSize WordSize { get; set; } = WordSize.QWord;
    public long Last { get; private set; }

    public EngineResult Evaluate(string expression)
    {
        long v = ProgrammerCalculator.Evaluate(expression, Radix, WordSize);
        return new EngineResult(ProgrammerFormatter.Format(v, Radix, WordSize, group: true), v, v);
    }

    public void Commit(EngineResult result) => Last = result.Integer ?? 0;

    /// <summary>Không có tên "Ans" (ở hệ HEX chữ A là chữ số) — nối tiếp bằng chính giá trị theo cơ số đang nhập.</summary>
    public string AnsText => ProgrammerFormatter.FormatForInput(Last, Radix, WordSize);

    public string ForHistory(string expression) => expression;
}

/// <summary>
/// Phiên làm việc chế độ Lập trình = máy trạng thái nhập liệu (dùng chung bảng) + ProgrammerEngine,
/// cộng các thao tác riêng: đổi cơ số nhập, đổi kích thước word, hiển thị song song 4 hệ.
/// </summary>
public sealed class ProgrammerSession
{
    public ProgrammerEngine Engine { get; } = new();
    public InputStateMachine Machine { get; }

    public ProgrammerSession(int radix = 10, WordSize size = WordSize.QWord)
    {
        Engine.Radix = radix;
        Engine.WordSize = size;
        Machine = new InputStateMachine(Engine);
    }

    public int Radix => Engine.Radix;
    public WordSize WordSize => Engine.WordSize;

    /// <summary>Giá trị đang hiện (kết quả hoặc xem trước), null nếu chưa có.</summary>
    public long? CurrentValue => Machine.CurrentResult?.Integer;

    /// <summary>Giá trị ở cả 4 hệ (HEX, DEC, OCT, BIN), đã nhóm chữ số. Chưa có giá trị thì hiện 0.</summary>
    public IReadOnlyList<(int Radix, string Text)> AllRadixes()
    {
        long v = CurrentValue ?? 0;
        return ProgrammerFormatter.Radixes.Select(r => (r, ProgrammerFormatter.Format(v, r, WordSize, group: true))).ToList();
    }

    /// <summary>Chữ số c có được nhập ở cơ số hiện tại không (để làm mờ phím / bỏ qua phím gõ).</summary>
    public bool IsDigitAllowed(char c)
    {
        int d = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'A' and <= 'F' => c - 'A' + 10,
            >= 'a' and <= 'f' => c - 'a' + 10,
            _ => -1,
        };
        return d >= 0 && d < Radix;
    }

    /// <summary>Đổi cơ số nhập: giá trị đang hiện được viết lại theo cơ số mới (biểu thức dở dang thì xoá).</summary>
    public void SetRadix(int radix)
    {
        if (radix is not (2 or 8 or 10 or 16)) throw new ArgumentOutOfRangeException(nameof(radix));
        Reformat(() => Engine.Radix = radix);
    }

    /// <summary>Đổi kích thước word: giá trị đang hiện bị cắt bớt bit cao (vd 300 -> BYTE = 44).</summary>
    public void SetWordSize(WordSize size) => Reformat(() => Engine.WordSize = size);

    private void Reformat(Action change)
    {
        long? value = CurrentValue;
        var state = Machine.State;
        change();

        if (value is long v)
        {
            v = WordSizes.Normalize(v, Engine.WordSize);
            Machine.Process(InputEvent.Load(ProgrammerFormatter.FormatForInput(v, Engine.Radix, Engine.WordSize)));
            if (state == InputState.ShowingResult) Machine.Process(InputEvent.EqualsKey);
        }
        else if (state is InputState.Typing or InputState.Error)
        {
            // biểu thức viết theo cơ số cũ không còn đọc đúng được nữa
            Machine.Process(InputEvent.Clear);
        }
    }
}
