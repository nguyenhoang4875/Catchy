using EasyLog.Contracts.Models;

namespace EasyLog.Engine;

/// <summary>
/// Raised when a live session flushes a batch of newly collected records to storage.
/// Subscribers should treat <see cref="Records"/> as a read-only snapshot owned by the event.
/// This event replaces per-record <see cref="LogRecordAppendedEventArgs"/> dispatch on hot paths
/// to avoid one dispatcher hop + one filter-match call per log line under heavy live load.
/// </summary>
public sealed class LogRecordsLiveAppendedEventArgs : EventArgs
{
    public LogRecordsLiveAppendedEventArgs(IReadOnlyList<LogRecord> records)
    {
        Records = records;
    }

    public IReadOnlyList<LogRecord> Records { get; }
}

