using System.Globalization;
using SmartCalc.Core.Errors;

namespace SmartCalc.Core.Units;

public enum UnitCategory { Length, Mass, Temperature, Area, Volume, Speed, Time, Data }

/// <summary>
/// Một đơn vị đo. Chuyển đổi đi qua "đơn vị gốc" của nhóm:
///   giá trị_gốc = ToBase(giá trị),   giá trị = FromBase(giá trị_gốc).
/// Hầu hết đơn vị là tuyến tính (nhân hệ số Factor); nhiệt độ là affine (có cộng/trừ) nên dùng hàm riêng.
/// Tính bằng decimal để các hệ số chính xác (1 in = 0.0254 m đúng tuyệt đối).
/// </summary>
public sealed class Unit
{
    public string Name { get; }
    public string Symbol { get; }
    public UnitCategory Category { get; }
    private readonly Func<decimal, decimal> _toBase;
    private readonly Func<decimal, decimal> _fromBase;

    private Unit(UnitCategory category, string name, string symbol, Func<decimal, decimal> toBase, Func<decimal, decimal> fromBase)
    {
        Category = category;
        Name = name;
        Symbol = symbol;
        _toBase = toBase;
        _fromBase = fromBase;
    }

    /// <summary>Đơn vị tuyến tính: 1 đơn vị này = factor đơn vị gốc.</summary>
    public static Unit Linear(UnitCategory c, string name, string symbol, decimal factor) =>
        new(c, name, symbol, v => v * factor, v => v / factor);

    public static Unit Affine(UnitCategory c, string name, string symbol, Func<decimal, decimal> toBase, Func<decimal, decimal> fromBase) =>
        new(c, name, symbol, toBase, fromBase);

    public decimal ToBase(decimal value) => _toBase(value);
    public decimal FromBase(decimal value) => _fromBase(value);

    public string Display => $"{Name} ({Symbol})";
    public override string ToString() => Symbol;
}

public static class UnitConverter
{
    private const UnitCategory L = UnitCategory.Length, M = UnitCategory.Mass, T = UnitCategory.Temperature,
        A = UnitCategory.Area, V = UnitCategory.Volume, S = UnitCategory.Speed, Ti = UnitCategory.Time, D = UnitCategory.Data;

    public static string CategoryName(UnitCategory c) => c switch
    {
        UnitCategory.Length => "Độ dài",
        UnitCategory.Mass => "Khối lượng",
        UnitCategory.Temperature => "Nhiệt độ",
        UnitCategory.Area => "Diện tích",
        UnitCategory.Volume => "Thể tích",
        UnitCategory.Speed => "Tốc độ",
        UnitCategory.Time => "Thời gian",
        UnitCategory.Data => "Dữ liệu",
        _ => c.ToString(),
    };

    // Độ không tuyệt đối theo đơn vị gốc của nhiệt độ (K)
    private const decimal AbsoluteZero = 0m;

    /// <summary>Danh sách đơn vị theo nhóm. Đơn vị gốc: m, kg, K, m², L, km/h, s, bit.</summary>
    public static readonly IReadOnlyDictionary<UnitCategory, IReadOnlyList<Unit>> Units =
        new Dictionary<UnitCategory, IReadOnlyList<Unit>>
        {
            [L] =
            [
                Unit.Linear(L, "Nanômét", "nm", 0.000000001m),
                Unit.Linear(L, "Micrômét", "µm", 0.000001m),
                Unit.Linear(L, "Milimét", "mm", 0.001m),
                Unit.Linear(L, "Xentimét", "cm", 0.01m),
                Unit.Linear(L, "Mét", "m", 1m),
                Unit.Linear(L, "Kilômét", "km", 1000m),
                Unit.Linear(L, "Inch", "in", 0.0254m),
                Unit.Linear(L, "Foot", "ft", 0.3048m),
                Unit.Linear(L, "Yard", "yd", 0.9144m),
                Unit.Linear(L, "Dặm", "mi", 1609.344m),
                Unit.Linear(L, "Hải lý", "nmi", 1852m),
            ],
            [M] =
            [
                Unit.Linear(M, "Miligam", "mg", 0.000001m),
                Unit.Linear(M, "Gam", "g", 0.001m),
                Unit.Linear(M, "Lạng", "lạng", 0.1m),
                Unit.Linear(M, "Kilôgam", "kg", 1m),
                Unit.Linear(M, "Yến", "yến", 10m),
                Unit.Linear(M, "Tạ", "tạ", 100m),
                Unit.Linear(M, "Tấn", "t", 1000m),
                Unit.Linear(M, "Ounce", "oz", 0.028349523125m),
                Unit.Linear(M, "Pound", "lb", 0.45359237m),
            ],
            [T] =
            [
                // gốc = Kelvin
                Unit.Affine(T, "Độ C", "°C", c => c + 273.15m, k => k - 273.15m),
                Unit.Affine(T, "Độ F", "°F", f => (f - 32m) * 5m / 9m + 273.15m, k => (k - 273.15m) * 9m / 5m + 32m),
                Unit.Affine(T, "Kelvin", "K", k => k, k => k),
            ],
            [A] =
            [
                Unit.Linear(A, "Milimét vuông", "mm²", 0.000001m),
                Unit.Linear(A, "Xentimét vuông", "cm²", 0.0001m),
                Unit.Linear(A, "Mét vuông", "m²", 1m),
                Unit.Linear(A, "Hécta", "ha", 10000m),
                Unit.Linear(A, "Kilômét vuông", "km²", 1000000m),
                Unit.Linear(A, "Inch vuông", "in²", 0.00064516m),
                Unit.Linear(A, "Foot vuông", "ft²", 0.09290304m),
                Unit.Linear(A, "Mẫu Anh", "acre", 4046.8564224m),
            ],
            [V] =
            [
                Unit.Linear(V, "Mililít", "mL", 0.001m),
                Unit.Linear(V, "Xentimét khối", "cm³", 0.001m),
                Unit.Linear(V, "Lít", "L", 1m),
                Unit.Linear(V, "Mét khối", "m³", 1000m),
                Unit.Linear(V, "Thìa cà phê (Mỹ)", "tsp", 0.00492892159375m),
                Unit.Linear(V, "Thìa canh (Mỹ)", "tbsp", 0.01478676478125m),
                Unit.Linear(V, "Ounce chất lỏng (Mỹ)", "fl oz", 0.0295735295625m),
                Unit.Linear(V, "Cốc (Mỹ)", "cup", 0.2365882365m),
                Unit.Linear(V, "Gallon (Mỹ)", "gal", 3.785411784m),
                Unit.Linear(V, "Gallon (Anh)", "gal UK", 4.54609m),
            ],
            [S] =
            [
                // gốc = km/h để mọi hệ số đều là số thập phân hữu hạn (1 m/s = 3.6 km/h)
                Unit.Linear(S, "Mét/giây", "m/s", 3.6m),
                Unit.Linear(S, "Kilômét/giờ", "km/h", 1m),
                Unit.Linear(S, "Dặm/giờ", "mph", 1.609344m),
                Unit.Linear(S, "Foot/giây", "ft/s", 1.09728m),
                Unit.Linear(S, "Hải lý/giờ", "knot", 1.852m),
            ],
            [Ti] =
            [
                Unit.Linear(Ti, "Nano giây", "ns", 0.000000001m),
                Unit.Linear(Ti, "Micro giây", "µs", 0.000001m),
                Unit.Linear(Ti, "Mili giây", "ms", 0.001m),
                Unit.Linear(Ti, "Giây", "s", 1m),
                Unit.Linear(Ti, "Phút", "min", 60m),
                Unit.Linear(Ti, "Giờ", "h", 3600m),
                Unit.Linear(Ti, "Ngày", "ngày", 86400m),
                Unit.Linear(Ti, "Tuần", "tuần", 604800m),
                Unit.Linear(Ti, "Năm (365 ngày)", "năm", 31536000m),
            ],
            [D] =
            [
                Unit.Linear(D, "Bit", "bit", 1m),
                Unit.Linear(D, "Byte", "B", 8m),
                Unit.Linear(D, "Kilobyte", "KB", 8000m),
                Unit.Linear(D, "Megabyte", "MB", 8000000m),
                Unit.Linear(D, "Gigabyte", "GB", 8000000000m),
                Unit.Linear(D, "Terabyte", "TB", 8000000000000m),
                Unit.Linear(D, "Kibibyte", "KiB", 8192m),
                Unit.Linear(D, "Mebibyte", "MiB", 8388608m),
                Unit.Linear(D, "Gibibyte", "GiB", 8589934592m),
                Unit.Linear(D, "Tebibyte", "TiB", 8796093022208m),
            ],
        };

    public static Unit Find(UnitCategory category, string symbol) =>
        Units[category].First(u => u.Symbol == symbol);

    /// <summary>Đổi value từ đơn vị from sang to (cùng nhóm).</summary>
    public static decimal Convert(decimal value, Unit from, Unit to)
    {
        if (from.Category != to.Category)
            throw new ArgumentException("Hai đơn vị phải cùng nhóm.");
        try
        {
            decimal baseValue = from.ToBase(value);
            if (from.Category == UnitCategory.Temperature && baseValue < AbsoluteZero)
                throw new CalcException("Nhiệt độ thấp hơn độ không tuyệt đối (0 K)");
            return from == to ? value : to.FromBase(baseValue);
        }
        catch (OverflowException)
        {
            throw new CalcException("Tràn số: giá trị quá lớn để đổi");
        }
    }

    /// <summary>Đọc số người dùng gõ: chấp nhận dấu '.' hoặc ',' làm dấu thập phân, dấu '-' hoặc '−'.</summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Trim().Replace('−', '-').Replace(',', '.').Replace(" ", "");
        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Hiển thị số (xem <see cref="SmartCalc.Core.Formatting.DecimalFormatter"/>).</summary>
    public static string Format(decimal value) => SmartCalc.Core.Formatting.DecimalFormatter.Format(value);
}
