using EasyLog.Contracts.Models;
using EasyLog.Engine.Indexes;

namespace EasyLog.Engine.Query;

public sealed class LogQueryEngine
{
    private readonly InMemoryLogIndexes _indexes;

    public LogQueryEngine(InMemoryLogIndexes indexes)
    {
        _indexes = indexes;
    }

    public IReadOnlyList<LogRecord> Apply(IEnumerable<LogRecord> source, FilterQuery query)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);

        if (source is IReadOnlyList<LogRecord> list)
        {
            var result = new List<LogRecord>(Math.Max(list.Count / 4, 256));
            for (var i = 0; i < list.Count; i++)
            {
                if (Matches(list[i], query))
                    result.Add(list[i]);
            }
            return result;
        }

        return source.Where(x => Matches(x, query)).ToList();
    }

    public bool Matches(LogRecord record, FilterQuery query)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(query);

        if (query.Levels is { Count: > 0 })
        {
            // Use simple iteration instead of allocating a new HashSet per call.
            // LogLevel enum has at most 7 values, so linear scan is faster.
            var found = false;
            foreach (var level in query.Levels)
            {
                if (level == record.Level)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                return false;
            }
        }

        // Tag terms: prefer multi-term collection; fall back to single legacy term without
        // allocating a temporary array (previously EnumerateTerms returned `new[] { singleTerm }`).
        var tagTermsList = query.TagTerms;
        if (tagTermsList is { Count: > 0 })
        {
            if (!AnyTermContains(tagTermsList, record.Tag, query.CaseSensitive))
            {
                return false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(query.TagContains))
        {
            if (!Contains(record.Tag, query.TagContains!, query.CaseSensitive))
            {
                return false;
            }
        }

        // PIDs: prefer multi-PID set; fall back to single legacy PID without temp array.
        var pidsList = query.Pids;
        if (pidsList is { Count: > 0 })
        {
            if (!record.Pid.HasValue || !pidsList.Contains(record.Pid.Value))
            {
                return false;
            }
        }
        else if (query.Pid.HasValue)
        {
            if (!record.Pid.HasValue || record.Pid.Value != query.Pid.Value)
            {
                return false;
            }
        }

        if (query.ExcludedTagTerms is { Count: > 0 } && AnyTermContains(query.ExcludedTagTerms, record.Tag, query.CaseSensitive))
        {
            return false;
        }

        if (query.ExcludedPids is { Count: > 0 } && record.Pid.HasValue && query.ExcludedPids.Contains(record.Pid.Value))
        {
            return false;
        }

        // Text terms: prefer multi-term; fall back to single legacy term without temp array.
        var textTermsList = query.TextTerms;
        if (textTermsList is { Count: > 0 })
        {
            if (!AnyTermContainsInMessageOrTag(textTermsList, record.Message, record.Tag, query.CaseSensitive))
            {
                return false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(query.TextContains))
        {
            if (!Contains(record.Message, query.TextContains!, query.CaseSensitive)
                && !Contains(record.Tag, query.TextContains!, query.CaseSensitive))
            {
                return false;
            }
        }

        if (query.ExcludedTextTerms is { Count: > 0 } &&
            AnyTermContainsInMessageOrTag(query.ExcludedTextTerms, record.Message, record.Tag, query.CaseSensitive))
        {
            return false;
        }

        if (query.From is not null && record.Timestamp < query.From.Value)
        {
            return false;
        }

        if (query.To is not null && record.Timestamp > query.To.Value)
        {
            return false;
        }

        return true;
    }

    public void Index(LogRecord record) => _indexes.Add(record);

    public void ClearIndexes() => _indexes.Clear();

    private static bool Contains(string source, string value, bool caseSensitive) =>
        source.Contains(value, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>Checks if any term in the collection is contained in the source string. Avoids LINQ delegate allocation.</summary>
    private static bool AnyTermContains(IReadOnlyCollection<string> terms, string source, bool caseSensitive)
    {
        foreach (var term in terms)
        {
            if (Contains(source, term, caseSensitive))
                return true;
        }
        return false;
    }

    /// <summary>Checks if any term matches in message or tag. Avoids LINQ delegate allocation.</summary>
    private static bool AnyTermContainsInMessageOrTag(IReadOnlyCollection<string> terms, string message, string tag, bool caseSensitive)
    {
        foreach (var term in terms)
        {
            if (Contains(message, term, caseSensitive) || Contains(tag, term, caseSensitive))
                return true;
        }
        return false;
    }
}

