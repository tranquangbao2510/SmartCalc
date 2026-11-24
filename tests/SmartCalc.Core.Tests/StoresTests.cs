using SmartCalc.Core.Evaluation;
using SmartCalc.Core.History;
using SmartCalc.Core.Memory;
using SmartCalc.Core.Settings;

namespace SmartCalc.Core.Tests;

public class MemoryStoreTests
{
    [Fact]
    public void Empty_RecallIsNull()
    {
        var m = new MemoryStore();
        Assert.False(m.HasValue);
        Assert.Null(m.Recall());
    }

    [Fact]
    public void Store_PushesOnTop()
    {
        var m = new MemoryStore();
        m.Store(1);
        m.Store(2);
        Assert.Equal([2.0, 1.0], m.Values);
        Assert.Equal(2, m.Recall());
    }

    [Fact]
    public void AddSubtract_OnTop_DecimalExact()
    {
        var m = new MemoryStore();
        m.Add(0.1);                // rỗng -> tạo mới
        m.Add(0.2);
        Assert.Equal(0.3, m.Recall());
        m.Subtract(1);
        Assert.Equal(-0.7, m.Recall());
        Assert.Single(m.Values);
    }

    [Fact]
    public void Subtract_OnEmpty_StoresNegative()
    {
        var m = new MemoryStore();
        m.Subtract(5);
        Assert.Equal(-5, m.Recall());
    }

    [Fact]
    public void PerItem_Operations_And_Clear()
    {
        var m = new MemoryStore();
        m.Store(10);
        m.Store(20);
        m.AddAt(1, 5);
        m.SubtractAt(0, 1);
        Assert.Equal([19.0, 15.0], m.Values);
        m.RemoveAt(0);
        Assert.Equal([15.0], m.Values);
        int changes = 0;
        m.Changed += () => changes++;
        m.Clear();
        Assert.False(m.HasValue);
        Assert.Equal(1, changes);
    }
}

public sealed class HistoryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SmartCalcTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "history.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Add_NewestFirst()
    {
        var h = new HistoryStore();
        h.Add("1+1", "2");
        h.Add("2×3", "6");
        Assert.Equal(["2×3", "1+1"], h.Entries.Select(e => e.Expression));
    }

    [Fact]
    public void KeepsAtMostMaxEntries()
    {
        var h = new HistoryStore();
        for (int i = 0; i < HistoryStore.MaxEntries + 10; i++) h.Add($"{i}", $"{i}");
        Assert.Equal(HistoryStore.MaxEntries, h.Entries.Count);
        Assert.Equal($"{HistoryStore.MaxEntries + 9}", h.Entries[0].Expression);
    }

    [Fact]
    public void SavesAndLoads_Json()
    {
        var h = new HistoryStore(FilePath);
        h.Add("0.1+0.2", "0.3");
        h.Add("sin(30)", "0.5");
        Assert.True(File.Exists(FilePath));

        var loaded = new HistoryStore(FilePath);
        Assert.Equal(["sin(30)", "0.1+0.2"], loaded.Entries.Select(e => e.Expression));
        Assert.Equal("0.5", loaded.Entries[0].Result);
    }

    [Fact]
    public void RemoveAndClear_Persist()
    {
        var h = new HistoryStore(FilePath);
        var a = h.Add("1", "1");
        h.Add("2", "2");
        h.Remove(a);
        Assert.Single(new HistoryStore(FilePath).Entries);
        h.Clear();
        Assert.Empty(new HistoryStore(FilePath).Entries);
    }

    [Fact]
    public void CorruptFile_GivesEmptyHistory()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");
        Assert.Empty(new HistoryStore(FilePath).Entries);
    }
}

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SmartCalcTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void MissingFile_GivesDefaults()
    {
        var s = AppSettings.Load(FilePath);
        Assert.Equal(new AppSettings(), s);
        Assert.Equal(AppTheme.Light, s.Theme);
        Assert.Equal(AngleMode.Deg, s.AngleMode);
        Assert.Equal(1, s.Mode);
    }

    [Fact]
    public void RoundTrip()
    {
        var s = new AppSettings { Theme = AppTheme.Dark, Topmost = true, AngleMode = AngleMode.Rad, Mode = 2 };
        s.Save(FilePath);
        Assert.Equal(s, AppSettings.Load(FilePath));
        Assert.Contains("\"Dark\"", File.ReadAllText(FilePath)); // enum lưu dạng chữ cho dễ đọc
    }

    [Fact]
    public void InvalidValues_AreSanitized()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "Mode": 42, "WindowWidth": -5 }""");
        var s = AppSettings.Load(FilePath);
        Assert.Equal(1, s.Mode);
        Assert.Equal(1100, s.WindowWidth);
    }

    [Fact]
    public void CorruptFile_GivesDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "garbage");
        Assert.Equal(new AppSettings(), AppSettings.Load(FilePath));
    }
}
