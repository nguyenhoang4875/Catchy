using System.Text;
using System.Diagnostics;
using EasyLog.Contracts.Enums;
using EasyLog.Contracts.Models;
using EasyLog.Engine.Collectors.Adb;
using EasyLog.Engine.Collectors.File;
using EasyLog.Engine.Diagnostics;
using EasyLog.Engine.Indexes;
using EasyLog.Engine.Parsers;
using EasyLog.Engine.Query;
using EasyLog.Engine.Storage;

namespace EasyLog.Engine;

public sealed class EasyLogEngine : IAsyncDisposable, IDisposable
{
    private const int LiveStatusUpdateRecordInterval = 250;
    private const long ExportChunkSizeBytes = 50L * 1024L * 1024L;
    private static readonly TimeSpan LiveStatusUpdateTimeInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan LiveReconnectInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LiveReconnectDevicePollInterval = TimeSpan.FromSeconds(2);
    private readonly AdbLogCollector _adbCollector;
    private readonly FileLogCollector _fileCollector;
    private readonly ThreadtimeLogParser _parser;
    private readonly RawSpoolStore _storage;
    private readonly InMemoryLogIndexes _indexes;
    private readonly LogQueryEngine _queryEngine;
    private readonly DiagnosticHighlighter _diagnostics;
    private readonly object _liveSessionGate = new();
    private long _rowId;
    private SessionState _sessionState = SessionState.Empty();
    private CancellationTokenSource? _liveSessionCts;
    private Task? _liveSessionTask;
    private long _clearGeneration;

    public EasyLogEngine(
        AdbLogCollector adbCollector,
        FileLogCollector fileCollector,
        ThreadtimeLogParser parser,
        RawSpoolStore storage,
        InMemoryLogIndexes indexes,
        LogQueryEngine queryEngine,
        DiagnosticHighlighter diagnostics)
    {
        _adbCollector = adbCollector;
        _fileCollector = fileCollector;
        _parser = parser;
        _storage = storage;
        _indexes = indexes;
        _queryEngine = queryEngine;
        _diagnostics = diagnostics;
    }

    public SessionState CurrentSession => _sessionState;

    public bool IsLiveSessionRunning => _liveSessionTask is { IsCompleted: false };

    public event EventHandler<LogRecordAppendedEventArgs>? LogRecordAppended;

    /// <summary>
    /// Raised per live-session storage flush with the batch of records that was just persisted.
    /// Prefer this over <see cref="LogRecordAppended"/> for UI/ViewModel consumers to avoid
    /// per-record dispatcher hops on hot paths.
    /// </summary>
    public event EventHandler<LogRecordsLiveAppendedEventArgs>? LogRecordsLiveAppended;

    public event EventHandler<LogRecordsBatchLoadedEventArgs>? LogRecordsBatchLoaded;

    public event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;

    public static EasyLogEngine CreateDefault(string? spoolPath = null)
    {
        var indexes = new InMemoryLogIndexes();
        var storage = new RawSpoolStore(spoolPath);
        var queryEngine = new LogQueryEngine(indexes);

        return new EasyLogEngine(
            new AdbLogCollector(),
            new FileLogCollector(),
            new ThreadtimeLogParser(),
            storage,
            indexes,
            queryEngine,
            new DiagnosticHighlighter());
    }

    public async Task<IReadOnlyList<DeviceInfo>> DiscoverDevicesAsync(CancellationToken cancellationToken = default) =>
        await _adbCollector.DiscoverDevicesAsync(cancellationToken).ConfigureAwait(false);

    public Task StartLiveSessionAsync(DeviceInfo? device, CancellationToken cancellationToken = default, TimeSpan? lookbackWindow = null) =>
        StartLiveSessionAsync(device?.Serial, cancellationToken, lookbackWindow);

    public Task StartLiveSessionAsync(string? deviceSerial = null, CancellationToken cancellationToken = default, TimeSpan? lookbackWindow = null)
    {
        lock (_liveSessionGate)
        {
            if (IsLiveSessionRunning)
            {
                throw new InvalidOperationException("이미 라이브 세션이 실행 중입니다.");
            }

            ResetSession(
                string.IsNullOrWhiteSpace(deviceSerial) ? "ADB Live Session" : $"ADB Live Session ({deviceSerial})",
                SessionMode.LiveAdb,
                deviceSerial ?? "default-device",
                "Starting adb live session...");

            _liveSessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _liveSessionTask = RunLiveSessionAsync(deviceSerial, lookbackWindow, _liveSessionCts.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopLiveSessionAsync()
    {
        Task? sessionTask;
        CancellationTokenSource? cts;

        lock (_liveSessionGate)
        {
            sessionTask = _liveSessionTask;
            cts = _liveSessionCts;
            _liveSessionTask = null;
            _liveSessionCts = null;
        }

        if (cts is null || sessionTask is null)
        {
            return;
        }

        cts.Cancel();

        try
        {
            await sessionTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 정상 종료 경로
        }
        finally
        {
            cts.Dispose();
        }

        UpdateSessionState(_sessionState with
        {
            RunState = SessionRunState.Stopped,
            StatusMessage = $"Live session stopped. {_storage.Count:n0} records collected."
        });
    }

    public async Task<SessionLoadResult> LoadFileAsync(string filePath, int? maxLines = null, CancellationToken cancellationToken = default)
    {
        return await LoadMultipleFilesAsync(new[] { filePath }, maxLines, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SessionLoadResult> LoadMultipleFilesAsync(IReadOnlyList<string> filePaths, int? maxLines = null, CancellationToken cancellationToken = default)
    {
        if (filePaths is null || filePaths.Count == 0)
        {
            throw new ArgumentException("At least one file path is required.", nameof(filePaths));
        }

        var sessionName = filePaths.Count == 1
            ? Path.GetFileName(filePaths[0])
            : $"{filePaths.Count} files ({Path.GetFileName(filePaths[0])}, ...)";

        ResetSession(sessionName, SessionMode.File, filePaths[0], "Loading file(s)...");

        await Task.Run(() => LoadFilesBatchCore(filePaths, maxLines, cancellationToken), cancellationToken).ConfigureAwait(false);

        // Use SnapshotView (O(1), no copy) — safe because file loading is complete and no concurrent writes.
        var records = _storage.SnapshotView();
        var count = records.Count;
        UpdateSessionState(_sessionState with
        {
            RunState = SessionRunState.Stopped,
            TotalRecords = count,
            MatchedRecords = count,
            StatusMessage = filePaths.Count == 1
                ? $"Loaded {count:n0} records"
                : $"Loaded {count:n0} records from {filePaths.Count} files"
        });

        return new SessionLoadResult(CurrentSession, records, filePaths[0]);
    }

    private void LoadFilesBatchCore(IReadOnlyList<string> filePaths, int? maxLines, CancellationToken cancellationToken)
    {
        // Single file or maxLines limit: use sequential loading
        if (filePaths.Count <= 1 || maxLines.HasValue)
        {
            LoadFilesSequentialCore(filePaths, maxLines, cancellationToken);
            return;
        }

        // Multiple files: parse in parallel, then merge in file order
        LoadFilesParallelCore(filePaths, cancellationToken);
    }

    private void LoadFilesSequentialCore(IReadOnlyList<string> filePaths, int? maxLines, CancellationToken cancellationToken)
    {
        const int batchSize = 50_000;
        var batch = new List<LogRecord>(batchSize);
        var totalLoaded = 0;
        var hitLimit = false;
        var errorCount = 0;
        string? firstErrorDetail = null;

        for (var fileIndex = 0; fileIndex < filePaths.Count && !hitLimit; fileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = filePaths[fileIndex];

            UpdateSessionState(_sessionState with
            {
                StatusMessage = $"Loading file {fileIndex + 1}/{filePaths.Count}: {Path.GetFileName(filePath)}..."
            });

            try
            {
                // Use synchronous StreamReader with large buffer and auto-detected encoding
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.SequentialScan);
                using var reader = CreateEncodingAwareReader(filePath, stream);

                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    batch.Add(_parser.ParseOrFallback(line, NextRowId()));
                    totalLoaded++;

                    if (batch.Count >= batchSize)
                    {
                        var loadedBatch = batch.ToArray();
                        _storage.BulkAppend(loadedBatch);
                        LogRecordsBatchLoaded?.Invoke(this, new LogRecordsBatchLoadedEventArgs(loadedBatch));
                        batch.Clear();

                        UpdateSessionState(_sessionState with
                        {
                            TotalRecords = totalLoaded,
                            MatchedRecords = totalLoaded,
                            StatusMessage = $"Loading file {fileIndex + 1}/{filePaths.Count}: {totalLoaded:n0} records..."
                        });
                    }

                    if (maxLines.HasValue && totalLoaded >= maxLines.Value)
                    {
                        hitLimit = true;
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                errorCount++;
                firstErrorDetail ??= $"{Path.GetFileName(filePath)}: {ex.GetType().Name}: {ex.Message}";
                // Skip this file and continue with the next one
            }
        }

        // Flush remaining batch
        if (batch.Count > 0)
        {
            var loadedBatch = batch.ToArray();
            _storage.BulkAppend(loadedBatch);
            LogRecordsBatchLoaded?.Invoke(this, new LogRecordsBatchLoadedEventArgs(loadedBatch));
        }

        if (errorCount > 0 && totalLoaded == 0)
        {
            throw new InvalidOperationException(
                $"All {errorCount} file(s) failed to load. {firstErrorDetail}");
        }
    }

    private void LoadFilesParallelCore(IReadOnlyList<string> filePaths, CancellationToken cancellationToken)
    {
        var perFileResults = new List<LogRecord>?[filePaths.Count];
        var parsedCount = 0;
        var errorCount = 0;
        string? firstErrorDetail = null;
        var fileCount = filePaths.Count;

        // Phase 1: Parse all files in parallel
        Parallel.For(0, fileCount, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(fileCount, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        }, fileIndex =>
        {
            var filePath = filePaths[fileIndex];

            try
            {
                var records = new List<LogRecord>();

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.SequentialScan);
                using var reader = CreateEncodingAwareReader(filePath, stream);

                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // RowIds are assigned via Interlocked — thread-safe, unique, but interleaved across files
                    records.Add(_parser.ParseOrFallback(line, NextRowId()));
                }

                perFileResults[fileIndex] = records;
                var loaded = Interlocked.Add(ref parsedCount, records.Count);

                UpdateSessionState(_sessionState with
                {
                    TotalRecords = loaded,
                    MatchedRecords = loaded,
                    StatusMessage = $"Parsing: {loaded:n0} records from {fileCount} files..."
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errorCount);
                Interlocked.CompareExchange(ref firstErrorDetail,
                    $"{Path.GetFileName(filePath)}: {ex.GetType().Name}: {ex.Message}", null);
            }
        });

        // Phase 2: K-way merge across files (Phase 3 — 4순위).
        // Replaces the previous "concatenate-all then global sort" approach which was
        // O(N log N) over the entire dataset and allocated a single large `merged` list
        // before sorting in-place with an unstable introsort. The new approach:
        //   1) Sort each file's records by (Timestamp, RowId) in place — typically near-sorted,
        //      so introsort is fast. List<T>.Sort is an UNSTABLE introsort, so a Timestamp-only
        //      comparer would scramble the relative order of records sharing an identical
        //      timestamp (down to ms). Within a single file, RowId is assigned sequentially in
        //      file-line order (NextRowId during the sequential per-file read), so using RowId
        //      as the secondary key restores the original raw-data line order for ties.
        //   2) K-way merge using a min-heap keyed by (Timestamp, fileIndex). The fileIndex
        //      tie-breaker guarantees a deterministic order across runs when timestamps tie
        //      across files (Plan §5-2 append-only ordering, TC#14 stability guard).
        // Memory: still O(N) for the final merged list (BulkAppend needs it), but we avoid
        // the global sort's working buffer behavior on very large datasets.
        UpdateSessionState(_sessionState with
        {
            StatusMessage = errorCount > 0
                ? $"Sorting {parsedCount:n0} records by timestamp ({errorCount} file(s) skipped)..."
                : $"Sorting {parsedCount:n0} records by timestamp..."
        });

        for (var i = 0; i < perFileResults.Length; i++)
        {
            var fileRecords = perFileResults[i];
            if (fileRecords is { Count: > 1 })
            {
                // Stable ordering for identical timestamps: tie-break by RowId, which is
                // assigned in file-line order during parsing. This preserves the original
                // raw-data line order for records sharing the same ms-precision timestamp.
                fileRecords.Sort(static (a, b) =>
                {
                    var c = a.Timestamp.CompareTo(b.Timestamp);
                    return c != 0 ? c : a.RowId.CompareTo(b.RowId);
                });
            }
        }

        var merged = new List<LogRecord>(parsedCount);
        // PriorityQueue<TElement, TPriority>: element = (fileIdx, recIdx), priority =
        // (Timestamp, fileIdx). Comparer uses Timestamp first, fileIdx as deterministic
        // tie-breaker. fileIdx-only tie-breaker (no lineIdx) is sufficient because
        // each file's records are pre-sorted and enqueued in order, so within a single
        // file the priority comparison sequence is monotonic.
        var pq = new PriorityQueue<(int FileIdx, int RecIdx), (DateTimeOffset Ts, int FileIdx)>(
            Comparer<(DateTimeOffset Ts, int FileIdx)>.Create(static (a, b) =>
            {
                var c = a.Ts.CompareTo(b.Ts);
                return c != 0 ? c : a.FileIdx.CompareTo(b.FileIdx);
            }));

        for (var f = 0; f < perFileResults.Length; f++)
        {
            var list = perFileResults[f];
            if (list is { Count: > 0 })
            {
                pq.Enqueue((f, 0), (list[0].Timestamp, f));
            }
        }

        while (pq.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (fileIdx, recIdx) = pq.Dequeue();
            var fileList = perFileResults[fileIdx]!;
            merged.Add(fileList[recIdx]);
            var nextIdx = recIdx + 1;
            if (nextIdx < fileList.Count)
            {
                pq.Enqueue((fileIdx, nextIdx), (fileList[nextIdx].Timestamp, fileIdx));
            }
            else
            {
                // Release each file's list once exhausted to keep peak memory low.
                perFileResults[fileIdx] = null;
            }
        }

        // Reassign RowIds in sorted order (1-based sequential)
        Interlocked.Exchange(ref _rowId, 0);
        for (var i = 0; i < merged.Count; i++)
        {
            merged[i] = merged[i] with { RowId = NextRowId() };
        }

        _storage.BulkAppend(merged);

        if (errorCount > 0 && parsedCount == 0)
        {
            throw new InvalidOperationException(
                $"All {errorCount} file(s) failed to load. {firstErrorDetail}");
        }
    }

    public async Task<SessionLoadResult> LoadDemoAsync(CancellationToken cancellationToken = default)
    {
        ResetSession("Demo Session", SessionMode.File, "embedded-demo", "Loading demo...");

        foreach (var line in DemoLines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = _parser.ParseOrFallback(line, NextRowId());
            await _storage.AppendAsync(record, flushImmediately: false, cancellationToken).ConfigureAwait(false);
        }

        await _storage.FlushAsync(cancellationToken).ConfigureAwait(false);

        var records = _storage.SnapshotView();
        UpdateSessionState(_sessionState with
        {
            RunState = SessionRunState.Stopped,
            TotalRecords = records.Count,
            MatchedRecords = records.Count,
            StatusMessage = $"Demo loaded: {records.Count:n0} records"
        });

        return new SessionLoadResult(CurrentSession, records, "embedded-demo");
    }

    public IReadOnlyList<LogRecord> ApplyFilter(FilterQuery query)
    {
        var snapshot = IsLiveSessionRunning ? _storage.Snapshot() : _storage.SnapshotView();
        var filtered = _queryEngine.Apply(snapshot, query);
        UpdateSessionState(_sessionState with
        {
            MatchedRecords = filtered.Count,
            StatusMessage = $"Showing {filtered.Count:n0} / {snapshot.Count:n0}"
        });

        return filtered;
    }

    public bool MatchesFilter(LogRecord record, FilterQuery query) => _queryEngine.Matches(record, query);

    public DiagnosticFlags GetDiagnostics(LogRecord record) => _diagnostics.Evaluate(record);

    public IReadOnlyList<LogRecord> GetSnapshot() => _storage.Snapshot();

    /// <summary>
    /// Returns the internal record list as a read-only view (O(1), no copy).
    /// Only safe when no concurrent writes are happening (i.e., live session is stopped).
    /// </summary>
    public IReadOnlyList<LogRecord> GetSnapshotView() => _storage.SnapshotView();

    /// <summary>Returns the current record count without copying the collection.</summary>
    public int RecordCount => _storage.Count;

    public async Task ExportAsync(string filePath, FilterQuery? query = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var records = IsLiveSessionRunning ? _storage.Snapshot() : _storage.SnapshotView();
        var selectedRecords = query is null ? records : _queryEngine.Apply(records, query);
        if (selectedRecords.Count == 0)
        {
            throw new InvalidOperationException("내보낼 로그가 없습니다.");
        }

        var archivePath = Path.ChangeExtension(filePath, ".7z") ?? filePath + ".7z";
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        var exportDirectory = Path.Combine(Path.GetTempPath(), "EasyLog", "export", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(exportDirectory);

        try
        {
            await WriteSplitExportPartsAsync(selectedRecords, exportDirectory, cancellationToken).ConfigureAwait(false);
            await CreateSevenZipArchiveAsync(archivePath, exportDirectory, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                Directory.Delete(exportDirectory, recursive: true);
            }
            catch
            {
                // 임시 export 폴더 정리 최선 시도
            }
        }
    }

    public void Clear(bool keepSessionMetadata = false)
    {
        Interlocked.Increment(ref _clearGeneration);
        _storage.Clear();
        _queryEngine.ClearIndexes();
        _rowId = 0;

        if (!keepSessionMetadata)
        {
            UpdateSessionState(SessionState.Empty());
            return;
        }

        UpdateSessionState(_sessionState with
        {
            TotalRecords = 0,
            MatchedRecords = 0,
            StatusMessage = IsLiveSessionRunning
                ? "Live session running. 0 records collected."
                : "Current logs cleared."
        });
    }

    public void Dispose() => _storage.Dispose();

    public ValueTask DisposeAsync() => _storage.DisposeAsync();

    private async Task RunLiveSessionAsync(string? deviceSerial, TimeSpan? lookbackWindow, CancellationToken cancellationToken)
    {
        var isFirstAttempt = true;

        while (!cancellationToken.IsCancellationRequested)
        {
            var liveStorageBatch = new List<LogRecord>(LiveStatusUpdateRecordInterval);
            var liveStorageBatchGeneration = Volatile.Read(ref _clearGeneration);
            try
            {
                if (!isFirstAttempt)
                {
                    // Wait for device to become ready again before reconnecting
                    await WaitForDeviceReadyAsync(deviceSerial, cancellationToken).ConfigureAwait(false);

                    UpdateSessionState(_sessionState with
                    {
                        RunState = SessionRunState.Running,
                        StatusMessage = $"Reconnected. {_storage.Count:n0} records collected."
                    });
                }

                var applyInitialLookback = isFirstAttempt && lookbackWindow.HasValue;
                isFirstAttempt = false;

                var recordsSinceLastStatusUpdate = 0;
                var liveStatusStopwatch = Stopwatch.StartNew();
                // Apply lookback only to the first connection. Reconnects resume from near-current point
                // to avoid replaying the same 30-second window repeatedly.
                DateTimeOffset? liveStartFrom = applyInitialLookback
                    ? DateTimeOffset.Now.Subtract(lookbackWindow.GetValueOrDefault())
                    : null;

                await foreach (var line in _adbCollector.CollectAsync(CollectionRequest.ForAdb(deviceSerial, liveStartFrom: liveStartFrom), cancellationToken).ConfigureAwait(false))
                {
                    var currentGeneration = Volatile.Read(ref _clearGeneration);
                    if (currentGeneration != liveStorageBatchGeneration)
                    {
                        liveStorageBatch.Clear();
                        recordsSinceLastStatusUpdate = 0;
                        liveStorageBatchGeneration = currentGeneration;
                    }

                    var record = _parser.ParseOrFallback(line, NextRowId());
                    liveStorageBatch.Add(record);
                    // Skip index building — indexes are not queried during filtering.
                    recordsSinceLastStatusUpdate++;

                    if (recordsSinceLastStatusUpdate >= LiveStatusUpdateRecordInterval ||
                        liveStatusStopwatch.Elapsed >= LiveStatusUpdateTimeInterval)
                    {
                        await PublishLiveProgressAsync(liveStorageBatch, liveStorageBatchGeneration, cancellationToken).ConfigureAwait(false);
                        recordsSinceLastStatusUpdate = 0;
                        liveStatusStopwatch.Restart();
                    }
                }

                // logcat ended cleanly (rare — usually means device disconnected gracefully)
                if (recordsSinceLastStatusUpdate > 0)
                {
                    await FlushLiveStorageBatchAsync(liveStorageBatch, liveStorageBatchGeneration, CancellationToken.None).ConfigureAwait(false);
                }

                UpdateSessionState(_sessionState with
                {
                    RunState = SessionRunState.Running,
                    TotalRecords = _storage.Count,
                    MatchedRecords = _storage.Count,
                    StatusMessage = $"ADB connection lost. Waiting for device to reconnect... ({_storage.Count:n0} records kept)"
                });

                // Brief delay before reconnect attempt
                await Task.Delay(LiveReconnectInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await FlushLiveStorageBatchAsync(liveStorageBatch, liveStorageBatchGeneration, CancellationToken.None).ConfigureAwait(false);
                UpdateSessionState(_sessionState with
                {
                    RunState = SessionRunState.Stopped,
                    TotalRecords = _storage.Count,
                    MatchedRecords = _storage.Count,
                    StatusMessage = $"Live session stopped. {_storage.Count:n0} records collected."
                });
                throw;
            }
            catch (Exception)
            {
                // ADB error (device disconnected, unauthorized, etc.) — attempt reconnect
                await FlushLiveStorageBatchAsync(liveStorageBatch, liveStorageBatchGeneration, CancellationToken.None).ConfigureAwait(false);

                UpdateSessionState(_sessionState with
                {
                    RunState = SessionRunState.Running,
                    TotalRecords = _storage.Count,
                    MatchedRecords = _storage.Count,
                    StatusMessage = $"ADB connection lost. Waiting for device to reconnect... ({_storage.Count:n0} records kept)"
                });

                // Brief delay before reconnect attempt
                await Task.Delay(LiveReconnectInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Polls for a ready device (state="device") at 2s intervals.
    /// Used by auto-reconnect to wait until the device finishes rebooting.
    /// </summary>
    private async Task WaitForDeviceReadyAsync(string? targetSerial, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var devices = await _adbCollector.DiscoverDevicesAsync(cancellationToken).ConfigureAwait(false);
                var ready = string.IsNullOrWhiteSpace(targetSerial)
                    ? devices.FirstOrDefault(static x => string.Equals(x.State, "device", StringComparison.OrdinalIgnoreCase))
                    : devices.FirstOrDefault(x => string.Equals(x.Serial, targetSerial, StringComparison.OrdinalIgnoreCase)
                                                  && string.Equals(x.State, "device", StringComparison.OrdinalIgnoreCase));
                if (ready is not null)
                {
                    return;
                }
            }
            catch
            {
                // adb itself might not be responding during device reboot
            }

            await Task.Delay(LiveReconnectDevicePollInterval, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task PublishLiveProgressAsync(List<LogRecord> liveStorageBatch, long batchGeneration, CancellationToken cancellationToken)
    {
        await FlushLiveStorageBatchAsync(liveStorageBatch, batchGeneration, cancellationToken).ConfigureAwait(false);
        UpdateSessionState(_sessionState with
        {
            RunState = SessionRunState.Running,
            TotalRecords = _storage.Count,
            MatchedRecords = _storage.Count,
            StatusMessage = $"Live session running. {_storage.Count:n0} records collected."
        });
    }

    private async Task FlushLiveStorageBatchAsync(List<LogRecord> liveStorageBatch, long batchGeneration, CancellationToken cancellationToken)
    {
        if (liveStorageBatch.Count == 0)
        {
            await _storage.FlushAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (Volatile.Read(ref _clearGeneration) != batchGeneration)
        {
            liveStorageBatch.Clear();
            await _storage.FlushAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await _storage.AppendRangeAsync(liveStorageBatch, flushImmediately: true, cancellationToken).ConfigureAwait(false);

        // Snapshot the batch once, then dispatch both the batch event (preferred, used by ViewModel)
        // and the legacy per-record event (kept for backward compatibility — e.g., smoke tests).
        // Per-record dispatch now runs from the flush point in *batched* form rather than on the
        // hot collect loop, so the legacy event no longer adds a dispatcher hop per log line.
        LogRecord[]? snapshot = null;
        var batchHandler = LogRecordsLiveAppended;
        var singleHandler = LogRecordAppended;
        if (batchHandler is not null || singleHandler is not null)
        {
            snapshot = liveStorageBatch.ToArray();
        }

        liveStorageBatch.Clear();

        if (snapshot is null)
        {
            return;
        }

        if (batchHandler is not null)
        {
            batchHandler.Invoke(this, new LogRecordsLiveAppendedEventArgs(snapshot));
        }

        if (singleHandler is not null)
        {
            for (var i = 0; i < snapshot.Length; i++)
            {
                singleHandler.Invoke(this, new LogRecordAppendedEventArgs(snapshot[i]));
            }
        }
    }

    private void ResetSession(string name, SessionMode mode, string? source, string status)
    {
        _storage.Clear();
        _queryEngine.ClearIndexes();
        _rowId = 0;
        UpdateSessionState(new SessionState(
            Guid.NewGuid(),
            name,
            mode,
            SessionRunState.Running,
            source,
            0,
            0,
            DateTimeOffset.Now,
            status));
    }

    private static async Task WriteSplitExportPartsAsync(IReadOnlyList<LogRecord> records, string exportDirectory, CancellationToken cancellationToken)
    {
        StreamWriter? writer = null;
        string? tempPartPath = null;
        long currentSize = 0;
        var partIndex = 1;
        DateTimeOffset lastRecordTimestamp = records[0].Timestamp;

        try
        {
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = record.ReconstructRawLine() + Environment.NewLine;
                var lineSize = Encoding.UTF8.GetByteCount(line);

                if (writer is null)
                {
                    tempPartPath = Path.Combine(exportDirectory, $"part-{partIndex:000}.tmp");
                    writer = CreateExportPartWriter(tempPartPath);
                }

                if (currentSize > 0 && currentSize + lineSize > ExportChunkSizeBytes)
                {
                    await FinalizeExportPartAsync(writer, tempPartPath!, exportDirectory, partIndex, lastRecordTimestamp).ConfigureAwait(false);
                    writer = null;
                    tempPartPath = null;
                    currentSize = 0;
                    partIndex++;

                    tempPartPath = Path.Combine(exportDirectory, $"part-{partIndex:000}.tmp");
                    writer = CreateExportPartWriter(tempPartPath);
                }

                await writer.WriteAsync(line).ConfigureAwait(false);
                currentSize += lineSize;
                lastRecordTimestamp = record.Timestamp;
            }

            if (writer is not null && tempPartPath is not null)
            {
                await FinalizeExportPartAsync(writer, tempPartPath, exportDirectory, partIndex, lastRecordTimestamp).ConfigureAwait(false);
            }
        }
        finally
        {
            if (writer is not null)
            {
                await writer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static StreamWriter CreateExportPartWriter(string filePath)
    {
        var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous);
        return new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static async Task FinalizeExportPartAsync(StreamWriter writer, string tempPartPath, string exportDirectory, int partIndex, DateTimeOffset lastRecordTimestamp)
    {
        await writer.FlushAsync().ConfigureAwait(false);
        await writer.DisposeAsync().ConfigureAwait(false);

        var finalFileName = $"LogPilot-part{partIndex:000}-until-{lastRecordTimestamp:yyyyMMdd-HHmmss}.log";
        var finalFilePath = Path.Combine(exportDirectory, finalFileName);
        File.Move(tempPartPath, finalFilePath, overwrite: true);
    }

    private static async Task CreateSevenZipArchiveAsync(string archivePath, string exportDirectory, CancellationToken cancellationToken)
    {
        var sevenZipPath = ResolveSevenZipExecutablePath();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = $"a -t7z -y \"{archivePath}\" .\\*.log",
                WorkingDirectory = exportDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("7z 압축 도구를 찾지 못했습니다. 7-Zip이 설치되어 있는지 확인하세요.", ex);
        }

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        });

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"7z 아카이브 생성에 실패했습니다. {stdErr}{stdOut}".Trim());
        }
    }

    private static string ResolveSevenZipExecutablePath() => ArchiveExtractor.ResolveSevenZipExecutablePath();

    private void UpdateSessionState(SessionState state, Exception? error = null)
    {
        _sessionState = state;
        SessionStateChanged?.Invoke(this, new SessionStateChangedEventArgs(state, error));
    }

    private long NextRowId() => Interlocked.Increment(ref _rowId);

    /// <summary>
    /// Detects the best encoding for a file by reading samples from multiple
    /// positions (start, middle, end) to reliably detect non-ASCII content.
    /// Uses strict UTF-8 decoding first, then falls back to CP949 / EUC-KR.
    /// </summary>
    private static Encoding DetectFileEncoding(string filePath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        byte[] sample;
        try
        {
            sample = ReadEncodingSample(filePath);
        }
        catch
        {
            return Encoding.UTF8;
        }

        if (sample.Length == 0)
            return Encoding.UTF8;

        // BOM takes absolute priority
        if (sample.Length >= 3 && sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
            return Encoding.UTF8;
        if (sample.Length >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
            return Encoding.Unicode;
        if (sample.Length >= 2 && sample[0] == 0xFE && sample[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        // If the entire sample is ASCII (0x00-0x7F), encoding doesn't matter
        if (!HasNonAsciiByte(sample))
            return Encoding.UTF8;

        // Trim sample to a safe UTF-8 boundary to avoid false rejection
        // from a multi-byte sequence split at the buffer edge
        var safeSample = TrimToUtf8Boundary(sample);

        // Try strict UTF-8
        try
        {
            var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            utf8Strict.GetString(safeSample);
            return Encoding.UTF8;
        }
        catch (DecoderFallbackException)
        {
            // Not valid UTF-8
        }

        // Try CP949 (superset of EUC-KR)
        // Try CP949 (superset of EUC-KR)
        try
        {
            var cp949 = Encoding.GetEncoding(949);
            var decoded = cp949.GetString(sample);
            if (!decoded.Contains('\uFFFD'))
            {
                // Verify it actually produces Korean characters
                foreach (var c in decoded)
                {
                    if (c >= 0xAC00 && c <= 0xD7A3) // Hangul syllable range
                        return cp949;
                }
                // No Hangul found but no replacement chars either — acceptable
                return cp949;
            }
        }
        catch
        {
            // ignore
        }

        // Try EUC-KR (code page 51949)
        try
        {
            var eucKr = Encoding.GetEncoding(51949);
            var decoded = eucKr.GetString(sample);
            if (!decoded.Contains('\uFFFD'))
                return eucKr;
        }
        catch
        {
            // ignore
        }

        return Encoding.UTF8;
    }

    /// <summary>
    /// Reads encoding-detection samples from multiple file positions (start, middle, end)
    /// so that non-ASCII content anywhere in the file can be captured.
    /// </summary>
    private static byte[] ReadEncodingSample(string filePath)
    {
        const int chunkSize = 64 * 1024;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
        var fileLength = fs.Length;

        if (fileLength == 0)
            return Array.Empty<byte>();

        // Small file: read the whole thing
        if (fileLength <= chunkSize * 3)
        {
            var all = new byte[(int)fileLength];
            _ = fs.Read(all, 0, all.Length);
            return all;
        }

        // Large file: read start, middle, and end chunks
        var startBuf = new byte[chunkSize];
        var startRead = fs.Read(startBuf, 0, chunkSize);

        // If the start chunk already has non-ASCII, that's enough
        if (HasNonAsciiByte(startBuf.AsSpan(0, startRead)))
            return startBuf.AsSpan(0, startRead).ToArray();

        // Read from middle
        var midPos = fileLength / 2;
        fs.Seek(midPos, SeekOrigin.Begin);
        var midBuf = new byte[chunkSize];
        var midRead = fs.Read(midBuf, 0, chunkSize);

        if (HasNonAsciiByte(midBuf.AsSpan(0, midRead)))
        {
            // Combine start + mid for a better sample
            var combined = new byte[startRead + midRead];
            Buffer.BlockCopy(startBuf, 0, combined, 0, startRead);
            Buffer.BlockCopy(midBuf, 0, combined, startRead, midRead);
            return combined;
        }

        // Read from near end
        var endPos = Math.Max(0, fileLength - chunkSize);
        fs.Seek(endPos, SeekOrigin.Begin);
        var endBuf = new byte[chunkSize];
        var endRead = fs.Read(endBuf, 0, chunkSize);

        if (HasNonAsciiByte(endBuf.AsSpan(0, endRead)))
        {
            var combined = new byte[startRead + endRead];
            Buffer.BlockCopy(startBuf, 0, combined, 0, startRead);
            Buffer.BlockCopy(endBuf, 0, combined, startRead, endRead);
            return combined;
        }

        // Entire file appears to be ASCII
        return startBuf.AsSpan(0, startRead).ToArray();
    }

    private static bool HasNonAsciiByte(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            if (b > 0x7F)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Trims a byte array to avoid ending in the middle of a UTF-8 multi-byte sequence.
    /// This prevents false <see cref="DecoderFallbackException"/> for valid UTF-8 data.
    /// </summary>
    private static byte[] TrimToUtf8Boundary(byte[] data)
    {
        if (data.Length == 0)
            return data;

        var end = data.Length;

        // Walk back from the end while we see continuation bytes (10xxxxxx)
        while (end > 0 && (data[end - 1] & 0xC0) == 0x80)
            end--;

        // If we stopped at a multi-byte start byte, check if the sequence is complete
        if (end > 0 && data[end - 1] >= 0x80)
        {
            var startByte = data[end - 1];
            int expectedLen;
            if ((startByte & 0xE0) == 0xC0) expectedLen = 2;
            else if ((startByte & 0xF0) == 0xE0) expectedLen = 3;
            else if ((startByte & 0xF8) == 0xF0) expectedLen = 4;
            else expectedLen = 1;

            var actualLen = data.Length - end + 1;
            if (actualLen < expectedLen)
                end--; // Remove the incomplete start byte
        }

        if (end == data.Length)
            return data;

        return data.AsSpan(0, end).ToArray();
    }

    /// <summary>
    /// Creates a StreamReader for a log file with auto-detected encoding.
    /// </summary>
    private static StreamReader CreateEncodingAwareReader(string filePath, FileStream stream)
    {
        var encoding = DetectFileEncoding(filePath);
        return new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 65536);
    }

    private static readonly string[] DemoLines =
    {
        "03-26 10:15:00.123  1234  2000 I CarService: Vehicle HAL connected",
        "03-26 10:15:00.456  1234  2001 D CarAudioService: active zone changed to 0",
        "03-26 10:15:01.789  1555  1555 W ActivityManager: ANR in com.example.navigation",
        "03-26 10:15:02.001  1666  1666 E AndroidRuntime: FATAL EXCEPTION: main",
        "03-26 10:15:03.150  1777  1778 I CarPowerManager: power state changed to ON"
    };
}

