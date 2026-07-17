using EasyLog.Contracts.Enums;
using EasyLog.Contracts.Models;

namespace EasyLog.Engine.Indexes;

public sealed class InMemoryLogIndexes
{
    private readonly Dictionary<LogLevel, List<long>> _byLevel = new();
    private readonly Dictionary<string, List<long>> _byTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, List<long>> _byPid = new();

    public void Add(LogRecord record)
    {
        if (!_byLevel.TryGetValue(record.Level, out var levelRows))
        {
            levelRows = new List<long>();
            _byLevel[record.Level] = levelRows;
        }

        levelRows.Add(record.RowId);

        if (!string.IsNullOrWhiteSpace(record.Tag))
        {
            if (!_byTag.TryGetValue(record.Tag, out var tagRows))
            {
                tagRows = new List<long>();
                _byTag[record.Tag] = tagRows;
            }

            tagRows.Add(record.RowId);
        }

        if (record.Pid is int pid)
        {
            if (!_byPid.TryGetValue(pid, out var pidRows))
            {
                pidRows = new List<long>();
                _byPid[pid] = pidRows;
            }

            pidRows.Add(record.RowId);
        }
    }

    public void AddRange(IReadOnlyList<LogRecord> records)
    {
        foreach (var record in records)
        {
            Add(record);
        }
    }

    public IReadOnlyCollection<long> GetByLevel(LogLevel level) =>
        _byLevel.TryGetValue(level, out var rows) ? rows : Array.Empty<long>();

    public IReadOnlyCollection<long> GetByTag(string tag) =>
        _byTag.TryGetValue(tag, out var rows) ? rows : Array.Empty<long>();

    public IReadOnlyCollection<long> GetByPid(int pid) =>
        _byPid.TryGetValue(pid, out var rows) ? rows : Array.Empty<long>();

    public void Clear()
    {
        _byLevel.Clear();
        _byTag.Clear();
        _byPid.Clear();
    }
}

