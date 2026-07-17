using EasyLog.Contracts.Enums;

namespace EasyLog.Contracts.Models;

public sealed record SessionState(
    Guid SessionId,
    string Name,
    SessionMode Mode,
    SessionRunState RunState,
    string? Source,
    long TotalRecords,
    long MatchedRecords,
    DateTimeOffset? StartedAt,
    string StatusMessage)
{
    public static SessionState Empty(string name = "No Session") =>
        new(Guid.Empty, name, SessionMode.File, SessionRunState.Created, null, 0, 0, null, "Ready");
}

