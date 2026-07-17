using EasyLog.Contracts.Enums;

namespace EasyLog.Contracts.Models;

public sealed record LogRecord(
    long RowId,
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Tag,
    int? Pid,
    int? Tid,
    string Message,
    LogBufferKind Buffer = LogBufferKind.Unknown,
    string? ProcessName = null)
{
    /// <summary>
    /// Reconstructs the raw logcat threadtime line from parsed fields.
    /// Used for export and backward-compatible APIs.
    /// </summary>
    public string ReconstructRawLine()
    {
        var pid = Pid?.ToString() ?? "?";
        var tid = Tid?.ToString() ?? "?";
        var levelChar = Level switch
        {
            LogLevel.Verbose => 'V',
            LogLevel.Debug => 'D',
            LogLevel.Info => 'I',
            LogLevel.Warn => 'W',
            LogLevel.Error => 'E',
            LogLevel.Fatal => 'F',
            _ => '?'
        };

        return string.IsNullOrEmpty(Tag)
            ? $"{Timestamp:MM-dd HH:mm:ss.fff} {pid,5} {tid,5} {levelChar} {Message}"
            : $"{Timestamp:MM-dd HH:mm:ss.fff} {pid,5} {tid,5} {levelChar} {Tag}: {Message}";
    }
}

