using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalc.Core.Evaluation;
using SmartCalc.Core.History;
using SmartCalc.Core.Input;
using SmartCalc.Core.Memory;
using SmartCalc.Core.Settings;

namespace SmartCalc.App.ViewModels;

/// <summary>Một chế độ trong sidebar.</summary>
public sealed record ModeItem(int Index, string Name, string Icon, bool IsAvailable, bool IconIsText = false)
{
    public string Shortcut => $"Alt+{Index}";
    public string ToolTip => IsAvailable ? $"{Name} ({Shortcut})" : $"{Name} — sắp ra mắt ({Shortcut})";
}

/// <summary>Một ô nhớ hiển thị trong tab Bộ nhớ.</summary>
public sealed record MemoryItemViewModel(int Index, double Value, string Text);

/// <summary>
/// ViewModel chính = máy tính Cơ bản/Khoa học (kế thừa KeypadViewModel) + điều hướng chế độ, lịch sử, bộ nhớ,
/// theme, ghim, và các VM con của chế độ Lập trình / Đổi đơn vị.
/// Chỉ "nối dây": logic nằm trong Core (InputStateMachine, HistoryStore, MemoryStore, ...).
/// </summary>
public partial class MainViewModel : KeypadViewModel
{
    private readonly ScientificEngine _sci;
    private readonly HistoryStore _history;
    private readonly MemoryStore _memory = new();
    private readonly string? _settingsPath;
    private readonly Action<AppTheme>? _applyTheme;
    private AppSettings _settings;
    private bool _loading = true;

    public MainViewModel(AppSettings settings, string? settingsPath, HistoryStore history, Action<AppTheme>? applyTheme)
        : base(new InputStateMachine(new ScientificEngine()))
    {
        _sci = (ScientificEngine)Machine.Engine;
        _settings = settings;
        _settingsPath = settingsPath;
        _history = history;
        _applyTheme = applyTheme;

        Modes =
        [
            new(1, "Cơ bản", "", true),
            new(2, "Khoa học", "ƒx", true, IconIsText: true),
            new(3, "Lập trình", "", true),
            new(4, "Vẽ đồ thị", "", true),
            new(5, "Đổi đơn vị", "", true),
            new(6, "Thống kê", "Σ", true, IconIsText: true),
        ];

        Programmer = new ProgrammerViewModel(settings.ProgrammerRadix, settings.WordSize, () => IsProgrammerMode, SaveSettings);
        Converter = new UnitConverterViewModel();
        Graph = new GraphViewModel();
        Statistics = new StatisticsViewModel();

        _sci.AngleMode = settings.AngleMode;
        Machine.Evaluated += (expr, result) => _history.Add(expr, result);
        _history.Changed += SyncHistory;
        _memory.Changed += SyncMemory;

        SelectedMode = Modes[settings.Mode - 1];
        IsDark = settings.Theme == AppTheme.Dark;
        IsTopmost = settings.Topmost;
        AngleMode = settings.AngleMode;

        SyncHistory();
        SyncMemory();
        SyncDisplay();
        _loading = false;
    }

    // ============================ Chế độ ============================
    public ObservableCollection<ModeItem> Modes { get; }

    public ProgrammerViewModel Programmer { get; }
    public UnitConverterViewModel Converter { get; }
    public GraphViewModel Graph { get; }
    public StatisticsViewModel Statistics { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBasicMode), nameof(IsScientificMode), nameof(IsCalculatorMode),
        nameof(IsProgrammerMode), nameof(IsConverterMode), nameof(IsGraphMode), nameof(IsStatisticsMode), nameof(IsComingSoon),
        nameof(ShowSidePanel), nameof(ShowSidePanelToggle), nameof(IsSidePanelOverlay), nameof(MainColumnSpan))]
    public partial ModeItem SelectedMode { get; set; }

    public bool IsBasicMode => SelectedMode.Index == 1;
    public bool IsScientificMode => SelectedMode.Index == 2;
    /// <summary>Cơ bản hoặc Khoa học: dùng chung máy tính, lịch sử, bộ nhớ.</summary>
    public bool IsCalculatorMode => SelectedMode.Index is 1 or 2;
    public bool IsProgrammerMode => SelectedMode.Index == 3;
    public bool IsConverterMode => SelectedMode.Index == 5;
    public bool IsGraphMode => SelectedMode.Index == 4;
    public bool IsStatisticsMode => SelectedMode.Index == 6;
    public bool IsComingSoon => !SelectedMode.IsAvailable;

    partial void OnSelectedModeChanged(ModeItem value)
    {
        NotifyMemoryCommands();
        SaveSettings();
    }

    [RelayCommand]
    private void SelectMode(int index)
    {
        if (index >= 1 && index <= Modes.Count) SelectedMode = Modes[index - 1];
    }

    /// <summary>Chế độ đang hiện có bàn phím nào (để định tuyến phím gõ), null nếu không có.</summary>
    public KeypadViewModel? ActiveKeypad => IsCalculatorMode ? this : IsProgrammerMode ? Programmer : null;

    // ============================ Nhập liệu (Cơ bản/Khoa học) ============================
    protected override bool AcceptsInput => IsCalculatorMode;

    protected override string? MapChar(char c) => c switch
    {
        >= '0' and <= '9' => c.ToString(),
        '.' or ',' => ".",
        '+' => "+",
        '-' => "−",
        '*' => "×",
        '/' => "÷",
        '^' or '%' or '!' or '(' or ')' => c.ToString(),
        _ when char.IsAsciiLetter(c) => c.ToString(),   // gõ tên hàm: s, i, n, (
        _ => null,
    };

    protected override void OnDisplaySynced() => NotifyMemoryCommands();

    // ============================ DEG / RAD, 2nd ============================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AngleLabel))]
    public partial AngleMode AngleMode { get; set; }

    public string AngleLabel => AngleMode == AngleMode.Deg ? "DEG" : "RAD";

    partial void OnAngleModeChanged(AngleMode value)
    {
        _sci.AngleMode = value;
        Machine.Refresh();
        SyncDisplay();
        SaveSettings();
    }

    [RelayCommand]
    private void ToggleAngle() => AngleMode = AngleMode == AngleMode.Deg ? AngleMode.Rad : AngleMode.Deg;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinLabel), nameof(CosLabel), nameof(TanLabel), nameof(LnLabel), nameof(LogLabel),
        nameof(SinInsert), nameof(CosInsert), nameof(TanInsert), nameof(LnInsert), nameof(LogInsert))]
    public partial bool IsSecond { get; set; }

    [RelayCommand] private void ToggleSecond() => IsSecond = !IsSecond;

    public string SinLabel => IsSecond ? "sin⁻¹" : "sin";
    public string CosLabel => IsSecond ? "cos⁻¹" : "cos";
    public string TanLabel => IsSecond ? "tan⁻¹" : "tan";
    public string LnLabel => IsSecond ? "eˣ" : "ln";
    public string LogLabel => IsSecond ? "10ˣ" : "log";
    public string SinInsert => IsSecond ? "asin()" : "sin()";
    public string CosInsert => IsSecond ? "acos()" : "cos()";
    public string TanInsert => IsSecond ? "atan()" : "tan()";
    public string LnInsert => IsSecond ? "e^()" : "ln()";
    public string LogInsert => IsSecond ? "10^()" : "log()";

    // ============================ Bộ nhớ ============================
    public ObservableCollection<MemoryItemViewModel> MemoryItems { get; } = [];
    [ObservableProperty] public partial bool HasMemory { get; private set; }

    private bool CanUseMemory() => _memory.HasValue && IsCalculatorMode;
    private bool HasCurrentValue() => Machine.CurrentValue.HasValue && IsCalculatorMode;

    private void NotifyMemoryCommands()
    {
        MemoryClearCommand.NotifyCanExecuteChanged();
        MemoryRecallCommand.NotifyCanExecuteChanged();
        MemoryAddCommand.NotifyCanExecuteChanged();
        MemorySubtractCommand.NotifyCanExecuteChanged();
        MemorySaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUseMemory))]
    private void MemoryClear() => _memory.Clear();

    [RelayCommand(CanExecute = nameof(CanUseMemory))]
    private void MemoryRecall()
    {
        if (_memory.Recall() is { } v) InputCommand.Execute(ExpressionEditor.FormatForInsert(v));
    }

    [RelayCommand(CanExecute = nameof(HasCurrentValue))]
    private void MemoryAdd()
    {
        if (Machine.CurrentValue is { } v) _memory.Add(v);
    }

    [RelayCommand(CanExecute = nameof(HasCurrentValue))]
    private void MemorySubtract()
    {
        if (Machine.CurrentValue is { } v) _memory.Subtract(v);
    }

    [RelayCommand(CanExecute = nameof(HasCurrentValue))]
    private void MemorySave()
    {
        if (Machine.CurrentValue is { } v) _memory.Store(v);
    }

    [RelayCommand]
    private void RecallMemoryItem(MemoryItemViewModel? item)
    {
        if (item is not null) InputCommand.Execute(ExpressionEditor.FormatForInsert(item.Value));
    }

    [RelayCommand]
    private void ClearMemoryItem(MemoryItemViewModel? item)
    {
        if (item is not null) _memory.RemoveAt(item.Index);
    }

    [RelayCommand]
    private void AddToMemoryItem(MemoryItemViewModel? item)
    {
        if (item is not null && Machine.CurrentValue is { } v) _memory.AddAt(item.Index, v);
    }

    [RelayCommand]
    private void SubtractFromMemoryItem(MemoryItemViewModel? item)
    {
        if (item is not null && Machine.CurrentValue is { } v) _memory.SubtractAt(item.Index, v);
    }

    private void SyncMemory()
    {
        MemoryItems.Clear();
        for (int i = 0; i < _memory.Values.Count; i++)
            MemoryItems.Add(new MemoryItemViewModel(i, _memory.Values[i], NumberFormatter.Format(_memory.Values[i])));
        HasMemory = _memory.HasValue;
        NotifyMemoryCommands();
    }

    // ============================ Lịch sử ============================
    public ObservableCollection<HistoryEntry> History { get; } = [];
    [ObservableProperty] public partial bool HasHistory { get; private set; }

    [RelayCommand]
    private void LoadHistory(HistoryEntry? entry)
    {
        if (entry is null) return;
        Process(InputEvent.Load(entry.Expression));
        IsSidePanelOpen = false;   // cửa sổ hẹp: đóng panel phủ để thấy lại bàn phím
    }

    [RelayCommand]
    private void DeleteHistory(HistoryEntry? entry)
    {
        if (entry is not null) _history.Remove(entry);
    }

    [RelayCommand]
    private void ClearHistory() => _history.Clear();

    private void SyncHistory()
    {
        History.Clear();
        foreach (var e in _history.Entries) History.Add(e);
        HasHistory = History.Count > 0;
    }

    // ============================ Panel phải ============================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMemoryTab))]
    public partial bool IsHistoryTab { get; set; } = true;

    public bool IsMemoryTab
    {
        get => !IsHistoryTab;
        set { if (value) IsHistoryTab = false; }
    }

    /// <summary>Cửa sổ đủ rộng: panel phải luôn hiện cạnh bàn phím. Hẹp: panel hiện đè lên khi bấm nút.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSidePanel), nameof(ShowSidePanelToggle), nameof(IsSidePanelOverlay), nameof(MainColumnSpan))]
    public partial bool IsWide { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSidePanel), nameof(IsSidePanelOverlay), nameof(MainColumnSpan))]
    public partial bool IsSidePanelOpen { get; set; }

    /// <summary>Lịch sử/Bộ nhớ chỉ thuộc chế độ Cơ bản/Khoa học.</summary>
    public bool ShowSidePanel => IsCalculatorMode && (IsWide || IsSidePanelOpen);
    public bool ShowSidePanelToggle => IsCalculatorMode && !IsWide;
    public bool IsSidePanelOverlay => IsCalculatorMode && !IsWide && IsSidePanelOpen;

    /// <summary>Vùng nội dung chiếm 1 cột (có panel phải bên cạnh) hay cả 2 cột.</summary>
    public int MainColumnSpan => IsCalculatorMode && IsWide ? 1 : 2;

    [RelayCommand] private void ToggleSidePanel() => IsSidePanelOpen = !IsSidePanelOpen;

    [ObservableProperty] public partial bool IsSidebarExpanded { get; set; } = true;

    [RelayCommand] private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    // ============================ Theme, ghim ============================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeIcon), nameof(ThemeToolTip))]
    public partial bool IsDark { get; set; }

    public string ThemeIcon => IsDark ? "" : "";       // mặt trời / mặt trăng
    public string ThemeToolTip => IsDark ? "Chuyển sang giao diện sáng" : "Chuyển sang giao diện tối";

    partial void OnIsDarkChanged(bool value)
    {
        _applyTheme?.Invoke(value ? AppTheme.Dark : AppTheme.Light);
        SaveSettings();
    }

    [RelayCommand] private void ToggleTheme() => IsDark = !IsDark;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinIcon), nameof(PinToolTip))]
    public partial bool IsTopmost { get; set; }

    public string PinIcon => IsTopmost ? "" : "";      // bỏ ghim / ghim
    public string PinToolTip => IsTopmost ? "Bỏ ghim cửa sổ" : "Ghim cửa sổ luôn trên cùng";

    partial void OnIsTopmostChanged(bool value) => SaveSettings();

    [RelayCommand] private void ToggleTopmost() => IsTopmost = !IsTopmost;

    // ============================ Cài đặt ============================
    public void SaveWindowSize(double width, double height)
    {
        _settings = _settings with { WindowWidth = width, WindowHeight = height };
        SaveSettings();
    }

    private void SaveSettings()
    {
        if (_loading) return;
        _settings = _settings with
        {
            Theme = IsDark ? AppTheme.Dark : AppTheme.Light,
            Topmost = IsTopmost,
            AngleMode = AngleMode,
            Mode = SelectedMode.Index,
            ProgrammerRadix = Programmer.Radix,
            WordSize = Programmer.WordSize,
        };
        if (_settingsPath is not null) _settings.Save(_settingsPath);
    }
}
