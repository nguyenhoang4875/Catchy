using EasyLog.Contracts.Models;

namespace EasyLog.Engine;

public sealed class LogRecordsBatchLoadedEventArgs : EventArgs
{
    public LogRecordsBatchLoadedEventArgs(IReadOnlyList<LogRecord> records)
    {
        Records = records;
    }

    public IReadOnlyList<LogRecord> Records { get; }
}
