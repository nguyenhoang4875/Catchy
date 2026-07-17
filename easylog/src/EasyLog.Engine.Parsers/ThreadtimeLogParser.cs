using System.Collections.Concurrent;
using EasyLog.Contracts.Enums;
using EasyLog.Contracts.Interfaces;
using EasyLog.Contracts.Models;

namespace EasyLog.Engine.Parsers;

/// <summary>
/// Parses Android logcat threadtime format lines using manual Span parsing (zero-regex).
/// Supports:
///   - Standard:          MM-DD HH:mm:ss.fff  PID  TID LEVEL TAG: MESSAGE
///   - Year prefix:       YYYY-MM-DD HH:mm:ss.fff  PID  TID LEVEL TAG: MESSAGE
///   - Microseconds:      MM-DD HH:mm:ss.ffffff  PID  TID LEVEL TAG: MESSAGE
///   - UID field:         MM-DD HH:mm:ss.fff  PID  TID  UID LEVEL TAG: MESSAGE
///   - No tag (kernel):   MM-DD HH:mm:ss.fff  PID  TID LEVEL MESSAGE
/// </summary>
public sealed class ThreadtimeLogParser : ILogParser
{
    private static int _cachedYear = DateTimeOffset.Now.Year;
    private readonly ConcurrentDictionary<string, string> _tagInternPool = new(StringComparer.Ordinal);

    public bool TryParse(string rawLine, long rowId, out LogRecord record)
    {
        ArgumentNullException.ThrowIfNull(rawLine);

        // Fast rejection: skip empty, section dividers, and very short lines
        if (rawLine.Length < 20 || rawLine[0] == '-')
        {
            record = CreateRawFallback(rawLine, rowId);
            return false;
        }

        var span = rawLine.AsSpan();
        var pos = 0;

        // --- Optional year prefix: "YYYY-" ---
        int year;
        if (span.Length > 5 && span[4] == '-' && IsDigit(span[0]) && IsDigit(span[3]))
        {
            year = ParseFourDigits(span, 0);
            pos = 5; // skip "YYYY-"
        }
        else
        {
            year = _cachedYear;
        }

        // --- Timestamp: "MM-DD HH:mm:ss.fff" (18 chars min) ---
        if (span.Length - pos < 18)
        {
            record = CreateRawFallback(rawLine, rowId);
            return false;
        }

        // Validate timestamp structure: dd-dd dd:dd:dd.ddd
        if (span[pos + 2] != '-' || span[pos + 5] != ' ' ||
            span[pos + 8] != ':' || span[pos + 11] != ':' || span[pos + 14] != '.')
        {
            record = CreateRawFallback(rawLine, rowId);
            return false;
        }

        var month = ParseTwoDigits(span, pos);
        var day = ParseTwoDigits(span, pos + 3);
        var hour = ParseTwoDigits(span, pos + 6);
        var minute = ParseTwoDigits(span, pos + 9);
        var second = ParseTwoDigits(span, pos + 12);
        var millis = ParseThreeDigits(span, pos + 15);

        pos += 18; // past "MM-DD HH:mm:ss.fff"

        // Skip optional microsecond digits (up to 3 extra digits)
        while (pos < span.Length && IsDigit(span[pos]))
        {
            pos++;
        }

        // Build timestamp
        DateTimeOffset timestamp;
        if (month < 1 || month > 12 || day < 1 || day > 31 ||
            hour > 23 || minute > 59 || second > 59 || millis > 999)
        {
            timestamp = DateTimeOffset.UtcNow;
        }
        else
        {
            try
            {
                timestamp = new DateTimeOffset(year, month, day, hour, minute, second, millis, TimeZoneInfo.Local.BaseUtcOffset);
            }
            catch
            {
                timestamp = DateTimeOffset.UtcNow;
            }
        }

        // --- Skip whitespace ---
        pos = SkipSpaces(span, pos);

        // --- PID ---
        var pidStart = pos;
        pos = SkipDigits(span, pos);
        if (pos == pidStart)
        {
            record = CreateRawFallback(rawLine, rowId);
            return false;
        }

        var pid = ParseIntFromSpan(span, pidStart, pos);
        pos = SkipSpaces(span, pos);

        // --- TID ---
        var tidStart = pos;
        pos = SkipDigits(span, pos);
        if (pos == tidStart)
        {
            record = CreateRawFallback(rawLine, rowId);
            return false;
        }

        var tid = ParseIntFromSpan(span, tidStart, pos);
        pos = SkipSpaces(span, pos);

        // --- Optional UID (digits before level char) ---
        if (pos < span.Length && IsDigit(span[pos]))
        {
            pos = SkipDigits(span, pos);
            pos = SkipSpaces(span, pos);
        }

        // --- Level ---
        if (pos >= span.Length)
        {
            record = CreateRawFallback(rawLine, rowId);
            return false;
        }

        var levelChar = span[pos];
        var level = ParseLevelChar(levelChar);
        if (level == LogLevel.Unknown)
        {
            record = CreateRawFallback(rawLine, rowId);
            return false;
        }

        pos++; // past level char
        pos = SkipSpaces(span, pos);

        // --- TAG: MESSAGE  or  MESSAGE ---
        string tag;
        string message;

        // Search for ": " separator (tag: message)
        var colonIdx = -1;
        for (var i = pos; i < span.Length - 1; i++)
        {
            if (span[i] == ':' && span[i + 1] == ' ')
            {
                colonIdx = i;
                break;
            }
        }

        if (colonIdx >= 0)
        {
            tag = InternTag(span[pos..colonIdx].Trim().ToString());
            message = span[(colonIdx + 2)..].ToString();
        }
        else
        {
            // No tag — kernel-style
            tag = string.Empty;
            message = pos < span.Length ? span[pos..].ToString() : string.Empty;
        }

        record = new LogRecord(rowId, timestamp, level, tag, pid, tid, message);
        return true;
    }

    public LogRecord ParseOrFallback(string rawLine, long rowId)
    {
        TryParse(rawLine, rowId, out var record);
        return record;
    }

    private static LogRecord CreateRawFallback(string rawLine, long rowId) =>
        new(
            rowId,
            DateTimeOffset.UtcNow,
            LogLevel.Unknown,
            "RAW",
            null,
            null,
            rawLine);

    private static LogLevel ParseLevelChar(char c) => c switch
    {
        'V' => LogLevel.Verbose,
        'D' => LogLevel.Debug,
        'I' => LogLevel.Info,
        'W' => LogLevel.Warn,
        'E' => LogLevel.Error,
        'F' => LogLevel.Fatal,
        'S' => LogLevel.Fatal,   // Silent — map to Fatal for visibility
        _ => LogLevel.Unknown
    };

    private static bool IsDigit(char c) => (uint)(c - '0') <= 9;

    private static int SkipSpaces(ReadOnlySpan<char> span, int pos)
    {
        while (pos < span.Length && span[pos] == ' ')
        {
            pos++;
        }

        return pos;
    }

    private static int SkipDigits(ReadOnlySpan<char> span, int pos)
    {
        while (pos < span.Length && IsDigit(span[pos]))
        {
            pos++;
        }

        return pos;
    }

    private static int ParseTwoDigits(ReadOnlySpan<char> span, int offset)
    {
        return (span[offset] - '0') * 10 + (span[offset + 1] - '0');
    }

    private static int ParseThreeDigits(ReadOnlySpan<char> span, int offset)
    {
        return (span[offset] - '0') * 100 + (span[offset + 1] - '0') * 10 + (span[offset + 2] - '0');
    }

    private static int ParseFourDigits(ReadOnlySpan<char> span, int offset)
    {
        return (span[offset] - '0') * 1000 + (span[offset + 1] - '0') * 100 +
               (span[offset + 2] - '0') * 10 + (span[offset + 3] - '0');
    }

    private static int ParseIntFromSpan(ReadOnlySpan<char> span, int start, int end)
    {
        var result = 0;
        for (var i = start; i < end; i++)
        {
            result = result * 10 + (span[i] - '0');
        }

        return result;
    }

    /// <summary>
    /// Interns tag strings so that identical tags share the same string instance.
    /// Typical Android logs have ~200-500 unique tags across millions of records,
    /// so this dramatically reduces heap usage.
    /// Thread-safe: uses ConcurrentDictionary for parallel file loading.
    /// </summary>
    private string InternTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            return string.Empty;

        return _tagInternPool.GetOrAdd(tag, tag);
    }

    /// <summary>
    /// Clears the intern pool. Call when resetting the parser for a new session.
    /// </summary>
    public void ClearInternPool() => _tagInternPool.Clear();
}
