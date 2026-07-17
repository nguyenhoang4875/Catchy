using EasyLog.Contracts.Models;

namespace EasyLog.Contracts.Interfaces;

public interface ILogStorage
{
    ValueTask AppendAsync(LogRecord record, CancellationToken cancellationToken = default);

    IReadOnlyList<LogRecord> Snapshot();

    void Clear();
}

