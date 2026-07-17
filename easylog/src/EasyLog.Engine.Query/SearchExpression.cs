namespace EasyLog.Engine.Query;

public sealed class SearchExpression
{
    private readonly string[][] _orGroups;

    private SearchExpression(string[][] orGroups)
    {
        _orGroups = orGroups;
    }

    public bool IsEmpty => _orGroups.Length == 0;

    public IReadOnlyList<string> Terms => _orGroups
        .SelectMany(static group => group)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(static term => term.Length)
        .ToArray();

    public static SearchExpression Parse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Empty;
        }

        var orGroups = query
            .Split('|', StringSplitOptions.TrimEntries)
            .Select(group => group
                .Split('&', StringSplitOptions.TrimEntries)
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .ToArray())
            .Where(group => group.Length > 0)
            .ToArray();

        return orGroups.Length == 0
            ? Empty
            : new SearchExpression(orGroups);
    }

    public bool Matches(Func<string, bool> termMatcher)
    {
        ArgumentNullException.ThrowIfNull(termMatcher);

        foreach (var group in _orGroups)
        {
            if (group.All(termMatcher))
            {
                return true;
            }
        }

        return false;
    }

    public static SearchExpression Empty { get; } = new(Array.Empty<string[]>());
}

