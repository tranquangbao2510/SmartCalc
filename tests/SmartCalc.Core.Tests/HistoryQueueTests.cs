using System.Text.Json;
using SmartCalc.Core.History;

namespace SmartCalc.Core.Tests;

public sealed class HistoryQueueTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"SmartCalc-{Guid.NewGuid():N}.json");
    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Queue_DropsOldestOnEveryOverflow_AndRoundTripsNewestFirst()
    {
        var store = new HistoryStore(_path);
        for (int i = 0; i < 205; i++) store.Add(i.ToString(), i.ToString());
        var expected = Enumerable.Range(105, 100).Reverse().Select(i => i.ToString()).ToArray();
        Assert.Equal(expected, store.Entries.Select(e => e.Expression));
        Assert.Equal(expected, new HistoryStore(_path).Entries.Select(e => e.Expression));
        var json = JsonSerializer.Deserialize<HistoryEntry[]>(File.ReadAllText(_path))!;
        Assert.Equal(expected, json.Select(e => e.Expression));
    }

    [Fact]
    public void Queue_LoadsOversizedLegacyFile_ThenEvictsOldest()
    {
        var entries = Enumerable.Range(0, 125).Select(i => new HistoryEntry(i.ToString(), i.ToString(), DateTime.UnixEpoch)).ToArray();
        File.WriteAllText(_path, JsonSerializer.Serialize(entries));
        var store = new HistoryStore(_path);
        Assert.Equal(entries.Take(100), store.Entries);
        store.Add("new", "new");
        Assert.Equal("new", store.Entries[0].Expression);
        Assert.Equal(entries.Take(99), store.Entries.Skip(1));
        Assert.Equal(store.Entries, new HistoryStore(_path).Entries);
    }

    [Fact]
    public void Queue_RemovalPreservesOrderAndChangeEvents()
    {
        var store = new HistoryStore();
        var entries = Enumerable.Range(0, 100).Select(i => store.Add(i.ToString(), "0")).ToArray();
        int changes = 0;
        store.Changed += () => changes++;
        store.Remove(entries[50]);
        store.Remove(entries[50]);
        Assert.Equal(1, changes);
        Assert.Equal(entries.Where(e => e != entries[50]).Reverse(), store.Entries);
        store.Add("100", "100");
        Assert.Contains(entries[0], store.Entries);
        store.Add("101", "101");
        Assert.DoesNotContain(entries[0], store.Entries);
        Assert.Equal(100, store.Entries.Count);
        store.Clear();
        Assert.Empty(store.Entries);
        Assert.Equal(4, changes);
    }

    [Fact]
    public void Queue_RemoveEqualEntries_RemovesNewestOccurrence()
    {
        var same = new HistoryEntry("same", "0", DateTime.UnixEpoch);
        var middle = new HistoryEntry("middle", "0", DateTime.UnixEpoch);
        File.WriteAllText(_path, JsonSerializer.Serialize(new[] { same, middle, same }));
        var store = new HistoryStore(_path);
        store.Remove(same);
        Assert.Equal([middle, same], store.Entries);
    }
}
