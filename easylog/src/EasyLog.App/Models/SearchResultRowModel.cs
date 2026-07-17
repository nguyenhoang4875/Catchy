using System.Windows.Media;
using EasyLog.Contracts.Models;

namespace EasyLog.App.Models;

/// <summary>
/// Thin UI wrapper for search result rows.
/// Stores a reference to the source <see cref="LogRecord"/> to avoid string duplication.
/// </summary>
public sealed class SearchResultRowModel
{
    private static readonly string[] PidCache = LogRowModel.BuildPublicIntCache(65536);

    private readonly LogRecord _record;

    private SearchResultRowModel(LogRecord record, Brush backgroundBrush, Brush foregroundBrush)
    {
        _record = record;
        BackgroundBrush = backgroundBrush;
        ForegroundBrush = foregroundBrush;
    }

    public long RowId => _record.RowId;
    public string Timestamp => _record.Timestamp.ToString("MM-dd HH:mm:ss.fff");
    public string Tag => _record.Tag;
    public string Pid => FormatPid(_record.Pid);
    public string Message => _record.Message;
    public Brush BackgroundBrush { get; }
    public Brush ForegroundBrush { get; }

    /// <summary>Returns the underlying LogRecord.</summary>
    public LogRecord Record => _record;

    public static SearchResultRowModel From(LogRecord record, Brush backgroundBrush, Brush foregroundBrush) =>
        new(record, backgroundBrush, foregroundBrush);

    private static string FormatPid(int? value)
    {
        if (!value.HasValue) return string.Empty;
        var v = value.Value;
        return (uint)v < (uint)PidCache.Length ? PidCache[v] : v.ToString();
    }
}
