using EasyLog.Contracts.Models;

namespace EasyLog.Engine.Diagnostics;

[Flags]
public enum DiagnosticFlags
{
    None = 0,
    Crash = 1,
    Anr = 2,
    NativeCrash = 4,
    Watchdog = 8,
    BurstCandidate = 16
}

public sealed class DiagnosticHighlighter
{
    public DiagnosticFlags Evaluate(LogRecord record)
    {
        var text = record.Message;
        var flags = DiagnosticFlags.None;

        if (text.Contains("FATAL EXCEPTION", StringComparison.OrdinalIgnoreCase))
        {
            flags |= DiagnosticFlags.Crash;
        }

        if (text.Contains("ANR in", StringComparison.OrdinalIgnoreCase))
        {
            flags |= DiagnosticFlags.Anr;
        }

        if (text.Contains("tombstone", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("native crash", StringComparison.OrdinalIgnoreCase))
        {
            flags |= DiagnosticFlags.NativeCrash;
        }

        if (text.Contains("watchdog", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("service restart", StringComparison.OrdinalIgnoreCase))
        {
            flags |= DiagnosticFlags.Watchdog;
        }

        return flags;
    }
}

