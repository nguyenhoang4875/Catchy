using System.Text;
using EasyLog.Contracts.Interfaces;
using EasyLog.Contracts.Models;

namespace EasyLog.Engine.Storage;

public sealed class RawSpoolStore : ILogStorage, IAsyncDisposable, IDisposable
{
    private readonly List<LogRecord> _records = new();
    private readonly FileStream _stream;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly RecentRingBuffer _recentRingBuffer;

    public RawSpoolStore(string? spoolFilePath = null, int recentCapacity = 2048)
    {
        SpoolFilePath = spoolFilePath ?? CreateDefaultSpoolFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(SpoolFilePath)!);

        _stream = new FileStream(
            SpoolFilePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 8192,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        _stream.Position = _stream.Length;

        _recentRingBuffer = new RecentRingBuffer(recentCapacity);
    }

    public string SpoolFilePath { get; }

    public int Count => _records.Count;

    public async ValueTask AppendAsync(LogRecord record, CancellationToken cancellationToken = default)
        => await AppendAsync(record, flushImmediately: true, cancellationToken).ConfigureAwait(false);

    public async ValueTask AppendAsync(LogRecord record, bool flushImmediately, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(record.ReconstructRawLine() + Environment.NewLine);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stream.Position = _stream.Length;
            await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (flushImmediately)
            {
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            _records.Add(record);
            _recentRingBuffer.Add(record);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask AppendRangeAsync(IReadOnlyList<LogRecord> records, bool flushImmediately, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder(records.Count * 128);
        for (var i = 0; i < records.Count; i++)
        {
            builder.AppendLine(records[i].ReconstructRawLine());
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stream.Position = _stream.Length;
            await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (flushImmediately)
            {
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            _records.EnsureCapacity(_records.Count + records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                _records.Add(record);
                _recentRingBuffer.Add(record);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Returns a copy of the current records. Safe for use during concurrent writes (live sessions).
    /// </summary>
    public IReadOnlyList<LogRecord> Snapshot()
    {
        _gate.Wait();
        try
        {
            return _records.ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Returns the internal list directly as a read-only view (O(1), no copy).
    /// Only safe when no concurrent writes are happening (e.g., after file load completion).
    /// </summary>
    public IReadOnlyList<LogRecord> SnapshotView() => _records;

    public IReadOnlyList<LogRecord> RecentSnapshot() => _recentRingBuffer.Snapshot();

    /// <summary>
    /// Bulk-append records to the in-memory list only (no disk I/O).
    /// Designed for high-performance file loading where the source file already exists on disk.
    /// </summary>
    public void BulkAppend(IReadOnlyList<LogRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        _gate.Wait();
        try
        {
            _records.EnsureCapacity(_records.Count + records.Count);
            foreach (var record in records)
            {
                _records.Add(record);
                _recentRingBuffer.Add(record);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Clear()
    {
        _records.Clear();
        _recentRingBuffer.Clear();
        _stream.SetLength(0);
        _stream.Position = 0;
    }

    public void Dispose()
    {
        _stream.Dispose();
        _gate.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return _stream.DisposeAsync();
    }

    private static string CreateDefaultSpoolFilePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "EasyLog", "spool");
        var fileName = $"session-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.log";
        return Path.Combine(root, fileName);
    }

    /// <summary>
    /// Deletes spool files older than the specified age from the default spool directory.
    /// Skips the file currently in use by this instance (if any).
    /// </summary>
    public static void CleanupOldSpoolFiles(TimeSpan maxAge)
    {
        var root = Path.Combine(Path.GetTempPath(), "EasyLog", "spool");
        if (!Directory.Exists(root))
            return;

        var cutoff = DateTime.UtcNow - maxAge;
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "session-*.log"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Best-effort: skip files that are locked or inaccessible
                }
            }
        }
        catch
        {
            // Best-effort: skip if directory enumeration fails
        }
    }
}

