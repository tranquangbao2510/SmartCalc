using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalc.Core.Errors;
using SmartCalc.Core.Evaluation;
using SmartCalc.Core.Formatting;
using SmartCalc.Core.Statistics;

namespace SmartCalc.App.ViewModels;

/// <summary>Một dòng kết quả: tên đại lượng, ký hiệu, giá trị.</summary>
public sealed record StatRow(string Name, string Symbol, string Value, bool IsGroupStart = false);

/// <summary>Chế độ Thống kê: nhập dãy số, tính lại ngay khi gõ. Toán nằm trong StatisticsCalculator (Core).</summary>
public sealed partial class StatisticsViewModel : ObservableObject
{
    public StatisticsViewModel() => InputText = "2 4 4 4 5 5 7 9";

    public ObservableCollection<StatRow> Rows { get; } = [];

    [ObservableProperty] public partial string InputText { get; set; } = "";
    [ObservableProperty] public partial string ErrorText { get; private set; } = "";
    [ObservableProperty] public partial bool HasError { get; private set; }
    [ObservableProperty] public partial string SortedText { get; private set; } = "";

    partial void OnInputTextChanged(string value) => Recalculate();

    [RelayCommand] private void ClearInput() => InputText = "";

    private void Recalculate()
    {
        Rows.Clear();
        SortedText = "";
        ErrorText = "";
        try
        {
            var data = StatisticsCalculator.Parse(InputText);
            if (data.Count == 0)
            {
                HasError = false;
                return;
            }
            var s = StatisticsCalculator.Compute(data);

            Rows.Add(new("Số phần tử", "n", s.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Rows.Add(new("Tổng", "Σx", D(s.Sum)));
            Rows.Add(new("Trung bình", "x̄", D(s.Mean), IsGroupStart: true));
            Rows.Add(new("Trung vị", "Me", D(s.Median)));
            Rows.Add(new("Mode", "Mo", s.Modes.Count == 0 ? "Không có" : string.Join("; ", s.Modes.Select(D))));
            Rows.Add(new("Nhỏ nhất", "min", D(s.Min), IsGroupStart: true));
            Rows.Add(new("Lớn nhất", "max", D(s.Max)));
            Rows.Add(new("Khoảng biến thiên", "R", D(s.Range)));
            Rows.Add(new("Phương sai mẫu", "s²", s.SampleVariance is decimal sv ? D(sv) : "— (cần ≥ 2 giá trị)", IsGroupStart: true));
            Rows.Add(new("Độ lệch chuẩn mẫu", "s", s.SampleStdDev is double ss ? NumberFormatter.Format(ss) : "—"));
            Rows.Add(new("Phương sai tổng thể", "σ²", D(s.PopulationVariance), IsGroupStart: true));
            Rows.Add(new("Độ lệch chuẩn tổng thể", "σ", NumberFormatter.Format(s.PopulationStdDev)));

            SortedText = string.Join("  ", data.OrderBy(v => v).Take(200).Select(D)) + (data.Count > 200 ? "  …" : "");
            HasError = false;
        }
        catch (CalcException ex)
        {
            ErrorText = ex.Message;
            HasError = true;
        }
    }

    private static string D(decimal v) => DecimalFormatter.Format(v);
}
