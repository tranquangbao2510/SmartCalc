using SmartCalc.Core.Evaluation;
using SmartCalc.Core.Persistence;
using SmartCalc.Core.Programmer;

namespace SmartCalc.Core.Settings;

public enum AppTheme { Light, Dark }

/// <summary>Cài đặt người dùng, lưu ở %AppData%/SmartCalc/settings.json.</summary>
public sealed record AppSettings
{
    public AppTheme Theme { get; init; } = AppTheme.Light;
    public bool Topmost { get; init; }
    public AngleMode AngleMode { get; init; } = AngleMode.Deg;
    public int Mode { get; init; } = 1;          // 1..7 theo SPEC
    public double WindowWidth { get; init; } = 1100;
    public double WindowHeight { get; init; } = 720;
    public int ProgrammerRadix { get; init; } = 10;
    public WordSize WordSize { get; init; } = WordSize.QWord;

    public static string DefaultPath => Path.Combine(JsonFile.DefaultDirectory, "settings.json");

    public static AppSettings Load(string path)
    {
        var s = JsonFile.Load(path, () => new AppSettings());
        // Chặn giá trị hỏng
        return s with
        {
            Theme = Enum.IsDefined(s.Theme) ? s.Theme : AppTheme.Light,
            AngleMode = Enum.IsDefined(s.AngleMode) ? s.AngleMode : AngleMode.Deg,
            Mode = s.Mode is >= 1 and <= 7 ? s.Mode : 1,
            WindowWidth = s.WindowWidth is >= 360 and <= 10000 ? s.WindowWidth : 1100,
            WindowHeight = s.WindowHeight is >= 520 and <= 10000 ? s.WindowHeight : 720,
            ProgrammerRadix = s.ProgrammerRadix is 2 or 8 or 10 or 16 ? s.ProgrammerRadix : 10,
            WordSize = Enum.IsDefined(s.WordSize) ? s.WordSize : WordSize.QWord,
        };
    }

    public void Save(string path)
    {
        try { JsonFile.Save(path, this); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
