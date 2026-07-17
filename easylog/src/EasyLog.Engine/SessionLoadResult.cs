using EasyLog.Contracts.Models;

namespace EasyLog.Engine;

public sealed record SessionLoadResult(
    SessionState State,
    IReadOnlyList<LogRecord> Records,
    string? SourcePath = null);

