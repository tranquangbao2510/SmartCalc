using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalc.Core.Errors;
using SmartCalc.Core.Dates;

namespace SmartCalc.App.ViewModels;

/// <summary>
/// Chế độ Ngày tháng: (1) khoảng cách giữa 2 ngày, (2) cộng/trừ năm-tháng-tuần-ngày.
/// Mọi phép tính nằm trong DateCalculator (Core); VM chỉ đọc ô nhập và hiển thị kết quả.
/// </summary>
public sealed partial class DateViewModel : ObservableObject
{
    public DateViewModel()
    {
        string today = DateCalculator.Format(DateOnly.FromDateTime(DateTime.Today));
        FromText = today;
        ToText = today;
        StartText = today;
    }

    // ---------------- Tab ----------------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAddTab))]
    public partial bool IsDifferenceTab { get; set; } = true;

    public bool IsAddTab
    {
        get => !IsDifferenceTab;
        set { if (value) IsDifferenceTab = false; }
    }

    // ---------------- Khoảng cách giữa 2 ngày ----------------
    [ObservableProperty] public partial string FromText { get; set; } = "";
    [ObservableProperty] public partial string ToText { get; set; } = "";
    [ObservableProperty] public partial string FromInfo { get; private set; } = "";
    [ObservableProperty] public partial string ToInfo { get; private set; } = "";
    [ObservableProperty] public partial bool FromInvalid { get; private set; }
    [ObservableProperty] public partial bool ToInvalid { get; private set; }
    [ObservableProperty] public partial string DifferenceText { get; private set; } = "";
    [ObservableProperty] public partial string TotalDaysText { get; private set; } = "";

    partial void OnFromTextChanged(string value) => RecalculateDifference();
    partial void OnToTextChanged(string value) => RecalculateDifference();

    [RelayCommand] private void FromToday() => FromText = Today();
    [RelayCommand] private void ToToday() => ToText = Today();

    private void RecalculateDifference()
    {
        bool okFrom = ReadDate(FromText, out var from, out string fromInfo);
        bool okTo = ReadDate(ToText, out var to, out string toInfo);
        (FromInfo, FromInvalid, ToInfo, ToInvalid) = (fromInfo, !okFrom, toInfo, !okTo);

        if (!okFrom || !okTo)
        {
            DifferenceText = "—";
            TotalDaysText = "";
            return;
        }
        var diff = DateCalculator.Difference(from, to);
        DifferenceText = diff.Describe();
        // nhóm nghìn bằng dấu cách, thống nhất với chế độ Đổi đơn vị / Lập trình
        TotalDaysText = diff.TotalDays == 0 ? "" : $"Tổng cộng {diff.TotalDays.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ')} ngày";
    }

    // ---------------- Cộng / trừ ----------------
    [ObservableProperty] public partial string StartText { get; set; } = "";
    [ObservableProperty] public partial string StartInfo { get; private set; } = "";
    [ObservableProperty] public partial bool StartInvalid { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSubtract))]
    public partial bool IsAdd { get; set; } = true;

    public bool IsSubtract
    {
        get => !IsAdd;
        set { if (value) IsAdd = false; }
    }

    [ObservableProperty] public partial string YearsText { get; set; } = "0";
    [ObservableProperty] public partial string MonthsText { get; set; } = "0";
    [ObservableProperty] public partial string WeeksText { get; set; } = "0";
    [ObservableProperty] public partial string DaysText { get; set; } = "0";
    [ObservableProperty] public partial string AddResultText { get; private set; } = "";
    [ObservableProperty] public partial string AddErrorText { get; private set; } = "";

    partial void OnStartTextChanged(string value) => RecalculateAdd();
    partial void OnIsAddChanged(bool value) => RecalculateAdd();
    partial void OnYearsTextChanged(string value) => RecalculateAdd();
    partial void OnMonthsTextChanged(string value) => RecalculateAdd();
    partial void OnWeeksTextChanged(string value) => RecalculateAdd();
    partial void OnDaysTextChanged(string value) => RecalculateAdd();

    [RelayCommand] private void StartToday() => StartText = Today();

    private void RecalculateAdd()
    {
        bool okStart = ReadDate(StartText, out var start, out string info);
        (StartInfo, StartInvalid) = (info, !okStart);
        AddResultText = "";
        AddErrorText = "";
        if (!okStart) return;

        if (!ReadCount(YearsText, 9999, out int y) || !ReadCount(MonthsText, 120000, out int m)
            || !ReadCount(WeeksText, 600000, out int w) || !ReadCount(DaysText, 4000000, out int d))
        {
            AddErrorText = "Năm, tháng, tuần, ngày phải là số nguyên không âm";
            return;
        }

        try
        {
            AddResultText = DateCalculator.FormatLong(DateCalculator.Add(start, y, m, w, d, subtract: IsSubtract));
        }
        catch (CalcException ex)
        {
            AddErrorText = ex.Message;
        }
    }

    // ---------------- Tiện ích ----------------
    private static string Today() => DateCalculator.Format(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>Đọc ngày; info = thứ trong tuần (hợp lệ) hoặc thông báo lỗi.</summary>
    private static bool ReadDate(string text, out DateOnly date, out string info)
    {
        if (DateCalculator.TryParse(text, out date, out string error))
        {
            info = DateCalculator.DayOfWeekName(date) + (DateCalculator.IsLeapYear(date.Year) ? " · năm nhuận" : "");
            return true;
        }
        info = error;
        return false;
    }

    private static bool ReadCount(string text, int max, out int value)
    {
        if (string.IsNullOrWhiteSpace(text)) { value = 0; return true; }
        return int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= max;
    }
}
