using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalc.Core.Programmer;

namespace SmartCalc.App.ViewModels;

/// <summary>Một dòng HEX/DEC/OCT/BIN; bấm vào để chọn cơ số nhập.</summary>
public sealed partial class RadixRowViewModel(int radix) : ObservableObject
{
    public int Radix { get; } = radix;
    public string Label { get; } = ProgrammerFormatter.RadixLabel(radix);
    [ObservableProperty] public partial string Text { get; set; } = "0";
    [ObservableProperty] public partial bool IsSelected { get; set; }
}

/// <summary>
/// Chế độ Lập trình: bàn phím + màn hình dùng chung KeypadViewModel (cùng bảng máy trạng thái),
/// engine là ProgrammerEngine. Logic đổi cơ số / kích thước word nằm trong ProgrammerSession (Core).
/// </summary>
public sealed partial class ProgrammerViewModel : KeypadViewModel
{
    private readonly ProgrammerSession _session;
    private readonly Func<bool> _isActive;
    private readonly Action _settingsChanged;

    public ProgrammerViewModel(int radix, WordSize size, Func<bool> isActive, Action settingsChanged)
        : this(new ProgrammerSession(radix, size), isActive, settingsChanged) { }

    private ProgrammerViewModel(ProgrammerSession session, Func<bool> isActive, Action settingsChanged)
        : base(session.Machine)
    {
        _session = session;
        _isActive = isActive;
        _settingsChanged = settingsChanged;
        RadixRows = new(ProgrammerFormatter.Radixes.Select(r => new RadixRowViewModel(r)));
        SyncDisplay();
    }

    public int Radix => _session.Radix;
    public WordSize WordSize => _session.WordSize;
    public string WordSizeLabel => WordSize.Label();

    public ObservableCollection<RadixRowViewModel> RadixRows { get; }

    // Phím chữ số bật/tắt theo cơ số
    public bool CanDigit2To7 => Radix >= 8;
    public bool CanDigit8To9 => Radix >= 10;
    public bool CanHexDigit => Radix == 16;

    protected override bool AcceptsInput => _isActive();

    protected override void OnDisplaySynced()
    {
        foreach (var (row, (r, text)) in RadixRows.Zip(_session.AllRadixes()))
        {
            row.Text = text;
            row.IsSelected = r == Radix;
        }
    }

    [RelayCommand]
    private void SelectRadix(RadixRowViewModel? row)
    {
        if (row is null || row.Radix == Radix) return;
        _session.SetRadix(row.Radix);
        AfterConfigChanged();
    }

    [RelayCommand]
    private void CycleWordSize()
    {
        _session.SetWordSize(WordSize.Next());
        AfterConfigChanged();
    }

    private void AfterConfigChanged()
    {
        SyncDisplay();
        OnPropertyChanged(nameof(Radix));
        OnPropertyChanged(nameof(WordSize));
        OnPropertyChanged(nameof(WordSizeLabel));
        OnPropertyChanged(nameof(CanDigit2To7));
        OnPropertyChanged(nameof(CanDigit8To9));
        OnPropertyChanged(nameof(CanHexDigit));
        _settingsChanged();
    }

    /// <summary>Bàn phím: chữ số (chỉ những chữ số hợp lệ ở cơ số hiện tại), a-f ở HEX, toán tử kiểu C.</summary>
    protected override string? MapChar(char c)
    {
        if (char.IsAsciiHexDigit(c))
            return _session.IsDigitAllowed(c) ? char.ToUpperInvariant(c).ToString() : null;
        return c switch
        {
            '+' => "+",
            '-' => "−",
            '*' => "×",
            '/' => "÷",
            '%' or '(' or ')' or '&' or '|' or '^' or '~' or '<' or '>' => c.ToString(),
            _ => null,
        };
    }
}
