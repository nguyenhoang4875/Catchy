using EasyLog.Contracts.Enums;

namespace EasyLog.Contracts.Models;

public sealed record FilterQuery(
    IReadOnlyCollection<LogLevel>? Levels = null,
    string? TagContains = null,
    int? Pid = null,
    string? TextContains = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    bool CaseSensitive = false,
    IReadOnlyCollection<string>? TagTerms = null,
    IReadOnlyCollection<int>? Pids = null,
    IReadOnlyCollection<string>? TextTerms = null,
    IReadOnlyCollection<string>? ExcludedTagTerms = null,
    IReadOnlyCollection<int>? ExcludedPids = null,
    IReadOnlyCollection<string>? ExcludedTextTerms = null)
{
    public static FilterQuery Empty { get; } = new();

    public bool IsEmpty =>
        Levels is null or { Count: 0 }
        && string.IsNullOrWhiteSpace(TagContains)
        && Pid is null
        && string.IsNullOrWhiteSpace(TextContains)
        && From is null
        && To is null
        && TagTerms is null or { Count: 0 }
        && Pids is null or { Count: 0 }
        && TextTerms is null or { Count: 0 }
        && ExcludedTagTerms is null or { Count: 0 }
        && ExcludedPids is null or { Count: 0 }
        && ExcludedTextTerms is null or { Count: 0 };
}
