using EasyLog.Contracts.Models;

namespace EasyLog.Engine;

public sealed class LogRecordAppendedEventArgs : EventArgs
{
    public LogRecordAppendedEventArgs(LogRecord record)
    {
        Record = record;
    }

    public LogRecord Record { get; }
}

