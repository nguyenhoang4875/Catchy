using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using EasyLog.App.Models;
using EasyLog.Contracts.Models;

namespace EasyLog.App.Infrastructure;

/// <summary>
/// A virtualized list that wraps <see cref="LogRecord"/> arrays and creates
/// <see cref="LogRowModel"/> instances on-demand via the indexer.
/// Only the ~50 rows visible in the WPF DataGrid ever materialise a LogRowModel,
/// dramatically reducing memory and UI-thread work for large log sessions.
/// </summary>
public sealed class VirtualLogList : IList<LogRowModel>, IList, INotifyCollectionChanged, INotifyPropertyChanged
{
    private const int LruCapacity = 512;

    private LogRecord[] _records = Array.Empty<LogRecord>();
    private Dictionary<long, int> _rowIdToIndex = new();
    private Func<LogRecord, LogRowModel> _factory = DefaultFactory;

    // LRU cache: index → LogRowModel
    private readonly Dictionary<int, LinkedListNode<(int Index, LogRowModel Model)>> _cacheMap = new(LruCapacity + 16);
    private readonly LinkedList<(int Index, LogRowModel Model)> _cacheOrder = new();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Public API ──────────────────────────────────────────────

    /// <summary>Replaces the entire backing array. Clears LRU cache and rebuilds RowId index.</summary>
    public void Reset(LogRecord[] records, Func<LogRecord, LogRowModel> factory)
    {
        _records = records ?? Array.Empty<LogRecord>();
        _factory = factory ?? DefaultFactory;
        RebuildIndex();
        ClearCache();
        RaiseReset();
    }

    /// <summary>Appends records to the end silently (no collection change event).
    /// Call <see cref="NotifyAppend"/> afterwards to raise a single Reset event at a controlled time.</summary>
    public void AppendSilent(LogRecord[] batch, Func<LogRecord, LogRowModel> factory)
    {
        if (batch is null || batch.Length == 0) return;

        _factory = factory ?? _factory;
        var oldLen = _records.Length;
        var newArr = new LogRecord[oldLen + batch.Length];
        Array.Copy(_records, newArr, oldLen);
        Array.Copy(batch, 0, newArr, oldLen, batch.Length);
        _records = newArr;

        // Update index for new records only
        for (var i = 0; i < batch.Length; i++)
        {
            _rowIdToIndex[batch[i].RowId] = oldLen + i;
        }

        _hasPendingAppend = true;
    }

    /// <summary>True if AppendSilent was called but NotifyAppend has not been called yet.</summary>
    public bool HasPendingAppend => _hasPendingAppend;
    private bool _hasPendingAppend;

    /// <summary>Fires a Reset notification for any silently appended data.</summary>
    public void NotifyAppend()
    {
        if (!_hasPendingAppend) return;
        _hasPendingAppend = false;
        RaiseReset();
    }

    /// <summary>Clears all records.</summary>
    public void Clear()
    {
        _records = Array.Empty<LogRecord>();
        _rowIdToIndex.Clear();
        ClearCache();
        RaiseReset();
    }

    /// <summary>O(1) lookup of RowId → list index. Returns -1 if not found.</summary>
    public int FindIndexByRowId(long rowId)
    {
        return _rowIdToIndex.TryGetValue(rowId, out var idx) ? idx : -1;
    }

    /// <summary>Returns the underlying LogRecord at the given index (no LogRowModel creation).</summary>
    public LogRecord GetRecord(int index) => _records[index];

    /// <summary>Updates the factory delegate (e.g. when highlight rules change) and invalidates the LRU cache.</summary>
    public void UpdateFactory(Func<LogRecord, LogRowModel> factory)
    {
        _factory = factory ?? DefaultFactory;
        ClearCache();
        // Fire Reset so DataGrid re-queries visible rows with new highlight colors
        RaiseReset();
    }

    // ── IList<LogRowModel> ──────────────────────────────────────

    public int Count => _records.Length;
    public bool IsReadOnly => true;

    public LogRowModel this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_records.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            // LRU cache hit
            if (_cacheMap.TryGetValue(index, out var node))
            {
                _cacheOrder.Remove(node);
                _cacheOrder.AddFirst(node);
                return node.Value.Model;
            }

            // Cache miss — create on demand
            var model = _factory(_records[index]);
            var newNode = new LinkedListNode<(int, LogRowModel)>((index, model));
            _cacheOrder.AddFirst(newNode);
            _cacheMap[index] = newNode;

            // Evict if over capacity
            if (_cacheMap.Count > LruCapacity)
            {
                var last = _cacheOrder.Last!;
                _cacheMap.Remove(last.Value.Index);
                _cacheOrder.RemoveLast();
            }

            return model;
        }
        set => throw new NotSupportedException();
    }

    public int IndexOf(LogRowModel item)
    {
        if (item is null) return -1;
        return FindIndexByRowId(item.RowId);
    }

    public bool Contains(LogRowModel item) => IndexOf(item) >= 0;

    public void CopyTo(LogRowModel[] array, int arrayIndex)
    {
        for (var i = 0; i < _records.Length; i++)
            array[arrayIndex + i] = this[i];
    }

    public IEnumerator<LogRowModel> GetEnumerator()
    {
        for (var i = 0; i < _records.Length; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Mutation — not supported
    public void Add(LogRowModel item) => throw new NotSupportedException();
    public void Insert(int index, LogRowModel item) => throw new NotSupportedException();
    public bool Remove(LogRowModel item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();

    // ── IList (non-generic, required by WPF ItemsControl) ───────

    object IList.this[int index]
    {
        get => this[index];
#pragma warning disable CS8769
        set => throw new NotSupportedException();
#pragma warning restore CS8769
    }

    bool IList.IsFixedSize => false;
    bool IList.IsReadOnly => true;
    int ICollection.Count => _records.Length;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => Clear();
    bool IList.Contains(object? value) => value is LogRowModel m && Contains(m);
    int IList.IndexOf(object? value) => value is LogRowModel m ? IndexOf(m) : -1;
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();

    void ICollection.CopyTo(Array array, int index)
    {
        for (var i = 0; i < _records.Length; i++)
            array.SetValue(this[i], index + i);
    }

    // ── Private helpers ─────────────────────────────────────────

    private void RebuildIndex()
    {
        var records = _records;
        var dict = new Dictionary<long, int>(records.Length);
        for (var i = 0; i < records.Length; i++)
        {
            dict[records[i].RowId] = i;
        }
        _rowIdToIndex = dict;
    }

    private void ClearCache()
    {
        _cacheMap.Clear();
        _cacheOrder.Clear();
    }

    private void RaiseReset()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private static LogRowModel DefaultFactory(LogRecord r) =>
        LogRowModel.From(r, string.Empty);
}




