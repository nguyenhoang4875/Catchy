using EasyLog.Contracts.Models;

namespace EasyLog.Engine.Storage;

public sealed class RecentRingBuffer
{
    private readonly int _capacity;
    private readonly Queue<LogRecord> _items;

    public RecentRingBuffer(int capacity = 1024)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        _items = new Queue<LogRecord>(capacity);
    }

    public void Add(LogRecord record)
    {
        if (_items.Count == _capacity)
        {
            _items.Dequeue();
        }

        _items.Enqueue(record);
    }

    public IReadOnlyList<LogRecord> Snapshot() => _items.ToArray();

    public void Clear() => _items.Clear();
}

