using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalc.Core.Errors;
using SmartCalc.Core.Units;

namespace SmartCalc.App.ViewModels;

public sealed record CategoryItem(UnitCategory Category, string Name);

/// <summary>Một dòng trong bảng quy đổi: giá trị đầu vào ở mọi đơn vị của nhóm.</summary>
public sealed record ConversionRow(string Value, string Symbol, string Name);

/// <summary>Chế độ Đổi đơn vị: nhập số + chọn đơn vị nguồn/đích; tính lại ngay khi đổi bất kỳ thứ gì.</summary>
public sealed partial class UnitConverterViewModel : ObservableObject
{
    public UnitConverterViewModel()
    {
        Categories = new(Enum.GetValues<UnitCategory>().Select(c => new CategoryItem(c, UnitConverter.CategoryName(c))));
        SelectedCategory = Categories[0];
    }

    public ObservableCollection<CategoryItem> Categories { get; }
    public ObservableCollection<Unit> Units { get; } = [];
    public ObservableCollection<ConversionRow> AllConversions { get; } = [];

    [ObservableProperty] public partial CategoryItem SelectedCategory { get; set; }
    [ObservableProperty] public partial Unit? FromUnit { get; set; }
    [ObservableProperty] public partial Unit? ToUnit { get; set; }
    [ObservableProperty] public partial string InputText { get; set; } = "1";
    [ObservableProperty] public partial string ResultText { get; private set; } = "";
    [ObservableProperty] public partial string ErrorText { get; private set; } = "";
    [ObservableProperty] public partial bool HasError { get; private set; }

    partial void OnSelectedCategoryChanged(CategoryItem value)
    {
        var list = UnitConverter.Units[value.Category];
        Units.Clear();
        foreach (var u in list) Units.Add(u);
        // Mặc định: 2 đơn vị "thông dụng" đầu tiên khác nhau
        _suspend = true;
        FromUnit = list[Math.Min(DefaultFrom(value.Category), list.Count - 1)];
        ToUnit = list[Math.Min(DefaultTo(value.Category), list.Count - 1)];
        _suspend = false;
        Recalculate();
    }

    private bool _suspend;

    private static int DefaultFrom(UnitCategory c) => c switch
    {
        UnitCategory.Length => 4,       // m
        UnitCategory.Mass => 3,         // kg
        UnitCategory.Temperature => 0,  // °C
        UnitCategory.Area => 2,         // m²
        UnitCategory.Volume => 2,       // L
        UnitCategory.Speed => 1,        // km/h
        UnitCategory.Time => 5,         // h
        _ => 3,                         // MB
    };

    private static int DefaultTo(UnitCategory c) => c switch
    {
        UnitCategory.Length => 7,       // ft
        UnitCategory.Mass => 8,         // lb
        UnitCategory.Temperature => 1,  // °F
        UnitCategory.Area => 3,         // ha
        UnitCategory.Volume => 0,       // mL
        UnitCategory.Speed => 0,        // m/s
        UnitCategory.Time => 4,         // min
        _ => 2,                         // KB
    };

    partial void OnFromUnitChanged(Unit? value) => Recalculate();
    partial void OnToUnitChanged(Unit? value) => Recalculate();
    partial void OnInputTextChanged(string value) => Recalculate();

    [RelayCommand]
    private void Swap()
    {
        if (FromUnit is null || ToUnit is null) return;
        _suspend = true;
        (FromUnit, ToUnit) = (ToUnit, FromUnit);
        if (!HasError && ResultText.Length > 0) InputText = ResultText.Replace(" ", "");
        _suspend = false;
        Recalculate();
    }

    private void Recalculate()
    {
        if (_suspend || FromUnit is null || ToUnit is null) return;
        AllConversions.Clear();

        if (!UnitConverter.TryParse(InputText, out decimal value))
        {
            SetError(string.IsNullOrWhiteSpace(InputText) ? "" : "Giá trị không hợp lệ");
            return;
        }

        try
        {
            ResultText = UnitConverter.Format(UnitConverter.Convert(value, FromUnit, ToUnit));
            foreach (var u in Units)
                AllConversions.Add(new ConversionRow(UnitConverter.Format(UnitConverter.Convert(value, FromUnit, u)), u.Symbol, u.Name));
            ErrorText = "";
            HasError = false;
        }
        catch (CalcException ex)
        {
            SetError(ex.Message);
        }
    }

    private void SetError(string message)
    {
        ResultText = "";
        ErrorText = message;
        HasError = message.Length > 0;
    }
}
