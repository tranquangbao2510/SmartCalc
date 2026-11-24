using SmartCalc.Core.Persistence;

namespace SmartCalc.Core.History;

public sealed record HistoryEntry(string Expression, string Result, DateTime Time);

/// <summary>Lịch sử phép tính, mới nhất ở đầu, tối đa <see cref="MaxEntries"/> mục. Lưu JSON.</summary>
public sealed class HistoryStore
{
    public const int MaxEntries = 100;

    private readonly List<HistoryEntry> _entries;
    private readonly string? _path;

    /// <param name="path">File JSON; null = chỉ giữ trong bộ nhớ (dùng cho test).</param>
    public HistoryStore(string? path = null)
    {
        _path = path;
        _entries = path is null ? [] : JsonFile.Load(path, () => new List<HistoryEntry>());
        // File sửa tay / hỏng một phần: bỏ mục null hoặc thiếu biểu thức / kết quả
        _entries.RemoveAll(e => e is null || string.IsNullOrEmpty(e.Expression) || e.Result is null);
        if (_entries.Count > MaxEntries) _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
    }

    public static string DefaultPath => Path.Combine(JsonFile.DefaultDirectory, "history.json");

    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public event Action? Changed;

    public HistoryEntry Add(string expression, string result)
    {
        var entry = new HistoryEntry(expression, result, DateTime.Now);
        _entries.Insert(0, entry);
        if (_entries.Count > MaxEntries) _entries.RemoveAt(_entries.Count - 1);
        Persist();
        return entry;
    }

    public void Remove(HistoryEntry entry)
    {
        if (_entries.Remove(entry)) Persist();
    }

    public void Clear()
    {
        _entries.Clear();
        Persist();
    }

    private void Persist()
    {
        if (_path is not null)
        {
            try { JsonFile.Save(_path, _entries); }
            catch (IOException) { /* không lưu được thì vẫn chạy tiếp, chỉ mất lịch sử khi tắt */ }
            catch (UnauthorizedAccessException) { }
        }
        Changed?.Invoke();
    }
}
