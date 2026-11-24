using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalc.Core.Input;

namespace SmartCalc.App.ViewModels;

/// <summary>
/// Phần dùng chung của mọi chế độ có bàn phím (Cơ bản/Khoa học, Lập trình): giữ một InputStateMachine (Core),
/// chuyển phím bấm thành InputEvent, và đọc DisplayModel ra các thuộc tính cho màn hình 2 dòng.
/// </summary>
public abstract partial class KeypadViewModel : ObservableObject
{
    protected KeypadViewModel(InputStateMachine machine) => Machine = machine;

    protected InputStateMachine Machine { get; }

    /// <summary>false khi chế độ đang ẩn — bỏ qua phím.</summary>
    protected virtual bool AcceptsInput => true;

    // ---------------- Màn hình ----------------
    [ObservableProperty] public partial InputState State { get; private set; }
    [ObservableProperty] public partial string TopText { get; private set; } = "";
    [ObservableProperty] public partial string TopMark { get; private set; } = "";
    [ObservableProperty] public partial string TopAfter { get; private set; } = "";
    [ObservableProperty] public partial string BottomText { get; private set; } = "0";
    [ObservableProperty] public partial bool IsError { get; private set; }
    [ObservableProperty] public partial bool IsPreview { get; private set; }

    /// <summary>Text để Ctrl+C: kết quả nếu có, ngược lại biểu thức.</summary>
    public string CopyText => State switch
    {
        InputState.ShowingResult => Machine.Result,
        InputState.Typing => Machine.Expression,
        _ => "",
    };

    protected void SyncDisplay()
    {
        var d = DisplayModel.From(Machine);
        State = Machine.State;
        TopText = d.TopText;
        TopMark = d.TopMark;
        TopAfter = d.TopAfter;
        BottomText = d.BottomText;
        IsError = d.IsError;
        IsPreview = d.IsPreview;
        OnDisplaySynced();
    }

    protected virtual void OnDisplaySynced() { }

    // ---------------- Nhập liệu ----------------
    protected void Process(InputEvent e)
    {
        if (!AcceptsInput) return;
        Machine.Process(e);
        SyncDisplay();
    }

    [RelayCommand]
    private void Input(string? text)
    {
        if (!string.IsNullOrEmpty(text)) Process(InputEvent.FromText(text));
    }

    [RelayCommand] private void Evaluate() => Process(InputEvent.EqualsKey);
    [RelayCommand] private void Clear() => Process(InputEvent.Clear);
    [RelayCommand] private void Backspace() => Process(InputEvent.Backspace);
    [RelayCommand] private void Negate() => Process(InputEvent.Negate);

    /// <summary>Ký tự gõ từ bàn phím -> text đưa vào biểu thức (null = bỏ qua). Mỗi chế độ ánh xạ riêng.</summary>
    protected abstract string? MapChar(char c);

    /// <summary>Xử lý một ký tự gõ; trả về true nếu đã dùng.</summary>
    public bool TypeChar(char c)
    {
        if (c == '=')
        {
            EvaluateCommand.Execute(null);
            return true;
        }
        string? text = MapChar(c);
        if (text is null) return false;
        InputCommand.Execute(text);
        return true;
    }

    /// <summary>Ctrl+V: dán text (bỏ ký tự điều khiển) như khi gõ.</summary>
    public void Paste(string? text)
    {
        if (text is null) return;
        string clean = new string(text.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length > 0) InputCommand.Execute(clean);
    }
}
