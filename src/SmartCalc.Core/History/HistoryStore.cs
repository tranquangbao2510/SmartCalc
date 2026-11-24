using SmartCalc.Core.Persistence;

namespace SmartCalc.Core.History;

public sealed record HistoryEntry(string Expression, string Result, DateTime Time);

/// <summary>Lịch sử phép tính, mới nhất ở đầu, tối đa <see cref="MaxEntries"/> mục. Lưu JSON.</summary>
public sealed class HistoryStore
{
    public const int MaxEntries = 100;

    private Queue<HistoryEntry> _entries;
    private readonly string? _path;

    /// <param name="path">File JSON; null = chỉ giữ trong bộ nhớ (dùng cho test).</param>
    public HistoryStore(string? path = null)
    {
        _path = path;
        var loaded = path is null ? [] : JsonFile.Load(path, () => new List<HistoryEntry>());
        // JSON và UI mới nhất trước; Queue giữ mục cũ nhất ở đầu.
        _entries = new Queue<HistoryEntry>(loaded
            .Where(e => e is not null && !string.IsNullOrEmpty(e.Expression) && e.Result is not null)
            .Take(MaxEntries).Reverse());
    }

    public static string DefaultPath => Path.Combine(JsonFile.DefaultDirectory, "history.json");

    public IReadOnlyList<HistoryEntry> Entries => _entries.Reverse().ToArray();

    public event Action? Changed;

    public HistoryEntry Add(string expression, string result)
    {
        var entry = new HistoryEntry(expression, result, DateTime.Now);
        if (_entries.Count == MaxEntries) _entries.Dequeue();
        _entries.Enqueue(entry);
        Persist();
        return entry;
    }

    public void Remove(HistoryEntry entry)
    {
        var entries = _entries.ToArray();
        // Queue xếp cũ trước, nên lần khớp cuối chính là mục khớp mới nhất.
        int newestMatch = Array.LastIndexOf(entries, entry);
        if (newestMatch < 0) return;

        _entries = new Queue<HistoryEntry>(entries.Where((_, index) => index != newestMatch));
        Persist();
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
            try { JsonFile.Save(_path, Entries); }
            catch (IOException) { /* không lưu được thì vẫn chạy tiếp, chỉ mất lịch sử khi tắt */ }
            catch (UnauthorizedAccessException) { }
        }
        Changed?.Invoke();
    }
}
