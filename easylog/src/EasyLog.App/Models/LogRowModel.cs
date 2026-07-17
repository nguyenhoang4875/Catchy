using System.Windows.Media;
using EasyLog.Contracts.Enums;
using EasyLog.Contracts.Models;

namespace EasyLog.App.Models;

/// <summary>
/// Thin UI wrapper over <see cref="LogRecord"/>.
/// Stores a reference to the source record instead of copying its string fields,
/// reducing heap usage by ~50% for large datasets.
/// String properties (Timestamp, Pid, Tid) are computed on-demand;
/// with DataGrid virtualization only ~50 visible rows ever call the getters.
/// </summary>
public sealed class LogRowModel
{
    // Pre-cached level strings — avoids Enum.ToString() allocation per record
    private static readonly string[] LevelStrings = BuildLevelStrings();
    // Pre-cached Pid/Tid strings — avoids per-access allocations for common values
    private static readonly string[] PidCache = BuildIntCache(65536);

    private readonly LogRecord _record;

    private LogRowModel(LogRecord record, string diagnostics, Brush backgroundBrush, Brush foregroundBrush)
    {
        _record = record;
        Diagnostics = diagnostics;
        BackgroundBrush = backgroundBrush;
        ForegroundBrush = foregroundBrush;
    }

    public long RowId => _record.RowId;
    public string Timestamp => FormatTimestamp(_record.Timestamp);
    public string Level => LevelStrings[(int)_record.Level];
    public string Tag => _record.Tag;
    public string Pid => FormatNullableInt(_record.Pid);
    public string Tid => FormatNullableInt(_record.Tid);
    public string Message => _record.Message;
    public string Diagnostics { get; }
    public Brush BackgroundBrush { get; }
    public Brush ForegroundBrush { get; }

    /// <summary>Returns the underlying LogRecord for filtering / export operations.</summary>
    public LogRecord Record => _record;

    public static LogRowModel From(LogRecord record, string diagnostics, Brush? backgroundBrush = null, Brush? foregroundBrush = null) =>
        new(record, diagnostics,
            backgroundBrush ?? Brushes.Transparent,
            foregroundBrush ?? Brushes.Black);

    // ── helpers ───────────────────────────────────────────────

    private static string FormatNullableInt(int? value)
    {
        if (!value.HasValue) return string.Empty;
        var v = value.Value;
        return (uint)v < (uint)PidCache.Length ? PidCache[v] : v.ToString();
    }

    /// <summary>
    /// Manual timestamp formatting — avoids DateTimeOffset.ToString() heap allocation overhead.
    /// Output format: "MM-dd HH:mm:ss.fff"
    /// </summary>
    private static string FormatTimestamp(DateTimeOffset ts)
    {
        return string.Create(18, ts, static (span, t) =>
        {
            var d = t.DateTime;
            Write2(span, 0, d.Month);
            span[2] = '-';
            Write2(span, 3, d.Day);
            span[5] = ' ';
            Write2(span, 6, d.Hour);
            span[8] = ':';
            Write2(span, 9, d.Minute);
            span[11] = ':';
            Write2(span, 12, d.Second);
            span[14] = '.';
            var millis = d.Millisecond;
            span[15] = (char)('0' + millis / 100);
            span[16] = (char)('0' + (millis / 10) % 10);
            span[17] = (char)('0' + millis % 10);
        });
    }

    private static void Write2(Span<char> span, int offset, int value)
    {
        span[offset] = (char)('0' + value / 10);
        span[offset + 1] = (char)('0' + value % 10);
    }

    private static string[] BuildLevelStrings()
    {
        var values = Enum.GetValues<LogLevel>();
        var result = new string[values.Length];
        foreach (var v in values)
        {
            result[(int)v] = v.ToString();
        }
        return result;
    }

    private static string[] BuildIntCache(int count)
    {
        var cache = new string[count];
        for (var i = 0; i < count; i++)
            cache[i] = i.ToString();
        return cache;
    }

    /// <summary>Shared int-to-string cache builder for sibling model classes.</summary>
    internal static string[] BuildPublicIntCache(int count) => BuildIntCache(count);
}
