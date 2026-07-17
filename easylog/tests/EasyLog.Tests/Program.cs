using System.Diagnostics;
using System.Reflection;
using EasyLog.Contracts.Enums;
using EasyLog.Contracts.Models;
using EasyLog.Engine;
using EasyLog.Engine.Collectors.Adb;
using EasyLog.Engine.Diagnostics;
using EasyLog.Engine.Parsers;
using EasyLog.Engine.Query;
using EasyLog.Engine.Storage;
using EasyLog.Engine.Indexes;

namespace EasyLog.Tests;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var failures = new List<string>();
        var runAdbSmoke = args.Contains("--adb-smoke", StringComparer.OrdinalIgnoreCase);

        Run(TestThreadtimeParser, failures);
        Run(TestQueryEngine, failures);
        Run(TestSearchExpression, failures);
        Run(TestDiagnostics, failures);
        Run(TestAdbLookbackArguments, failures);
        Run(EncodingDetectionTests.RunAll, failures);
        await RunAsync(TestRawSpoolStoreAsync, failures);
        await RunAsync(TestCompressedExportAsync, failures);
        await RunAsync(TestMultiFileSameTimestampOrderingAsync, failures);

        if (runAdbSmoke)
        {
            Console.WriteLine("[INFO] Running adb collector smoke...");
            await RunAsync(TestAdbCollectorSmokeAsync, failures);
            Console.WriteLine("[INFO] Running engine live session smoke...");
            await RunAsync(TestEngineLiveSessionSmokeAsync, failures);
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("[PASS] EasyLog smoke tests passed.");
            if (runAdbSmoke)
            {
                Console.WriteLine("[PASS] adb smoke checks passed.");
            }
            return 0;
        }

        Console.Error.WriteLine("[FAIL] EasyLog smoke tests failed:");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($" - {failure}");
        }

        return 1;
    }

    private static void TestThreadtimeParser()
    {
        var parser = new ThreadtimeLogParser();
        const string line = "03-26 10:15:02.001  1666  1666 E AndroidRuntime: FATAL EXCEPTION: main";

        var success = parser.TryParse(line, 1, out var record);
        Assert(success, "threadtime parser should parse valid log line");
        Assert(record.Level == LogLevel.Error, "level should be Error");
        Assert(record.Tag == "AndroidRuntime", "tag should be AndroidRuntime");
        Assert(record.Pid == 1666, "pid should be parsed");
        Assert(record.Message.Contains("FATAL EXCEPTION", StringComparison.Ordinal), "message should contain fatal exception");
    }

    private static async Task TestRawSpoolStoreAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "EasyLog.Tests", $"spool-{Guid.NewGuid():N}.log");
        await using var store = new RawSpoolStore(tempFile, recentCapacity: 2);

        await store.AppendAsync(new LogRecord(1, DateTimeOffset.Now, LogLevel.Info, "Tag1", 100, 200, "hello"));
        await store.AppendAsync(new LogRecord(2, DateTimeOffset.Now, LogLevel.Warn, "Tag2", 101, 201, "world"));
        await store.AppendAsync(new LogRecord(3, DateTimeOffset.Now, LogLevel.Error, "Tag3", 102, 202, "boom"));
        await store.AppendRangeAsync(new[]
        {
            new LogRecord(4, DateTimeOffset.Now, LogLevel.Info, "Tag4", 103, 203, "batch-1"),
            new LogRecord(5, DateTimeOffset.Now, LogLevel.Info, "Tag5", 104, 204, "batch-2")
        }, flushImmediately: true);

        Assert(store.Snapshot().Count == 5, "storage snapshot should contain all appended records");
        Assert(store.RecentSnapshot().Count == 2, "recent snapshot should respect ring buffer capacity");
        Assert(File.Exists(tempFile), "spool file should exist");
    }

    private static async Task TestCompressedExportAsync()
    {
        var sevenZipPath = ResolveSevenZipExecutablePath();
        if (sevenZipPath is null)
        {
            Console.WriteLine("[INFO] Skipping compressed export test because 7z was not found.");
            return;
        }

        using var engine = EasyLogEngine.CreateDefault();
        await engine.LoadDemoAsync().ConfigureAwait(false);

        var exportRoot = Path.Combine(Path.GetTempPath(), "EasyLog.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(exportRoot);
        var archivePath = Path.Combine(exportRoot, "demo-export.7z");

        try
        {
            await engine.ExportAsync(archivePath).ConfigureAwait(false);
            Assert(File.Exists(archivePath), "compressed export should create a 7z archive");

            var listing = await ListSevenZipEntriesAsync(sevenZipPath, archivePath).ConfigureAwait(false);
            Assert(listing.Contains("until-", StringComparison.OrdinalIgnoreCase), "exported split log names should include the last log timestamp");
            Assert(listing.Contains("LogPilot-part001", StringComparison.OrdinalIgnoreCase), "compressed export should contain split log parts");
        }
        finally
        {
            try
            {
                Directory.Delete(exportRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static async Task TestMultiFileSameTimestampOrderingAsync()
    {
        // Regression: when loading multiple files, records that share an identical timestamp
        // down to the millisecond must preserve their original raw-data line order within
        // each file. The parallel load path previously used an unstable List<T>.Sort, which
        // scrambled the relative order of same-timestamp records.
        var root = Path.Combine(Path.GetTempPath(), "EasyLog.Tests", $"sameordering-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        // File A: all lines share 10:15:02.001; File B: all lines share 10:15:03.001.
        // Distinct ms timestamps across files keep the merge trivial so the assertion isolates
        // the within-file stability behavior.
        var fileA = Path.Combine(root, "a.log");
        var fileB = Path.Combine(root, "b.log");

        var linesA = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            linesA.Add($"03-26 10:15:02.001  1666  1666 I OrderTag: A{i:D2}");
        }

        var linesB = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            linesB.Add($"03-26 10:15:03.001  1777  1777 I OrderTag: B{i:D2}");
        }

        await File.WriteAllLinesAsync(fileA, linesA).ConfigureAwait(false);
        await File.WriteAllLinesAsync(fileB, linesB).ConfigureAwait(false);

        try
        {
            using var engine = EasyLogEngine.CreateDefault();
            var result = await engine.LoadMultipleFilesAsync(new[] { fileA, fileB }).ConfigureAwait(false);
            var records = result.Records;

            Assert(records.Count == 40, "all 40 records should be loaded from the two files");

            var actualA = records
                .Where(static r => r.Message.StartsWith("A", StringComparison.Ordinal))
                .Select(static r => r.Message)
                .ToArray();
            var actualB = records
                .Where(static r => r.Message.StartsWith("B", StringComparison.Ordinal))
                .Select(static r => r.Message)
                .ToArray();

            var expectedA = Enumerable.Range(0, 20).Select(static i => $"A{i:D2}").ToArray();
            var expectedB = Enumerable.Range(0, 20).Select(static i => $"B{i:D2}").ToArray();

            Assert(actualA.SequenceEqual(expectedA, StringComparer.Ordinal),
                "same-timestamp records from file A must keep their original line order");
            Assert(actualB.SequenceEqual(expectedB, StringComparer.Ordinal),
                "same-timestamp records from file B must keep their original line order");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void TestQueryEngine()
    {
        var indexes = new InMemoryLogIndexes();
        var queryEngine = new LogQueryEngine(indexes);
        var records = new[]
        {
            new LogRecord(1, DateTimeOffset.Now, LogLevel.Info, "CarService", 100, 100, "Vehicle HAL connected"),
            new LogRecord(2, DateTimeOffset.Now, LogLevel.Warn, "ActivityManager", 200, 200, "ANR in com.example.navigation"),
            new LogRecord(3, DateTimeOffset.Now, LogLevel.Error, "AndroidRuntime", 300, 300, "FATAL EXCEPTION: main")
        };

        foreach (var record in records)
        {
            queryEngine.Index(record);
        }

        var filtered = queryEngine.Apply(records, new FilterQuery(Levels: new[] { LogLevel.Warn, LogLevel.Error }, TextContains: "ANR"));
        Assert(filtered.Count == 1, "filter should narrow results down to one warning record");
        Assert(filtered[0].Tag == "ActivityManager", "filtered result should be ActivityManager");

        var multiTextFiltered = queryEngine.Apply(records, new FilterQuery(TextTerms: new[] { "HAL", "FATAL" }));
        Assert(multiTextFiltered.Count == 2, "multi text filter should match any configured term");

        var multiTagFiltered = queryEngine.Apply(records, new FilterQuery(TagTerms: new[] { "Activity", "Runtime" }));
        Assert(multiTagFiltered.Count == 2, "multi tag filter should match any configured tag term");

        var multiPidFiltered = queryEngine.Apply(records, new FilterQuery(Pids: new[] { 100, 300 }));
        Assert(multiPidFiltered.Count == 2, "multi pid filter should match any configured pid");

        var excludeTagFiltered = queryEngine.Apply(records, new FilterQuery(ExcludedTagTerms: new[] { "Runtime" }));
        Assert(excludeTagFiltered.Count == 2, "exclude tag filter should remove matching records");

        var excludeTextFiltered = queryEngine.Apply(records, new FilterQuery(TextTerms: new[] { "ANR", "FATAL" }, ExcludedTextTerms: new[] { "ANR" }));
        Assert(excludeTextFiltered.Count == 1 && excludeTextFiltered[0].Tag == "AndroidRuntime", "exclude text filter should remove matching text from results");

        var excludePidFiltered = queryEngine.Apply(records, new FilterQuery(ExcludedPids: new[] { 200 }));
        Assert(excludePidFiltered.Count == 2, "exclude pid filter should remove matching pid records");
    }

    private static void TestDiagnostics()
    {
        var highlighter = new DiagnosticHighlighter();
        var record = new LogRecord(1, DateTimeOffset.Now, LogLevel.Error, "AndroidRuntime", 1, 1, "FATAL EXCEPTION: main");
        var flags = highlighter.Evaluate(record);
        Assert(flags.HasFlag(DiagnosticFlags.Crash), "diagnostics should flag crash");
    }

    private static void TestSearchExpression()
    {
        var record = new LogRecord(
            1,
            DateTimeOffset.Now,
            LogLevel.Error,
            "AndroidRuntime",
            1234,
            1234,
            "FATAL EXCEPTION in Vehicle HAL");

        Assert(SearchExpression.Parse("fatal").Matches(term => MatchesRecord(record, term)), "single search term should match tag/message/pid");
        Assert(SearchExpression.Parse("fatal|watchdog").Matches(term => MatchesRecord(record, term)), "| should work as OR");
        Assert(SearchExpression.Parse("fatal&vehicle").Matches(term => MatchesRecord(record, term)), "& should require both terms to match");
        Assert(!SearchExpression.Parse("fatal&watchdog").Matches(term => MatchesRecord(record, term)), "& should fail when any term is missing");
        Assert(SearchExpression.Parse("watchdog|fatal&vehicle").Matches(term => MatchesRecord(record, term)), "& should bind tighter than |");
        Assert(SearchExpression.Parse("  fatal  |  1234 & runtime  ").Matches(term => MatchesRecord(record, term)), "operators should ignore surrounding whitespace");
        var highlightTerms = SearchExpression.Parse("watchdog|fatal&vehicle|FATAL").Terms;
        Assert(highlightTerms.SequenceEqual(new[] { "watchdog", "vehicle", "fatal" }), "highlight terms should be unique and sorted by descending length");
        Assert(SearchExpression.Parse("||&&").IsEmpty, "operator-only query should be treated as empty");
    }

    private static void TestAdbLookbackArguments()
    {
        var collector = new AdbLogCollector();
        var method = typeof(AdbLogCollector).GetMethod("BuildLogcatArguments", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildLogcatArguments should exist for testing.");

        var lookbackRequest = CollectionRequest.ForAdb(deviceSerial: "ABC123", liveStartFrom: new DateTimeOffset(2026, 3, 26, 10, 15, 0, TimeSpan.Zero));
        var lookbackArguments = (string?)method.Invoke(collector, new object[] { lookbackRequest })
            ?? throw new InvalidOperationException("BuildLogcatArguments should return a string.");

        Assert(lookbackArguments.Contains("-T \"2026-03-26 10:15:00.000\"", StringComparison.Ordinal), "lookback request should format an absolute -T timestamp");

        var defaultRequest = CollectionRequest.ForAdb(deviceSerial: "ABC123");
        var defaultArguments = (string?)method.Invoke(collector, new object[] { defaultRequest })
            ?? throw new InvalidOperationException("BuildLogcatArguments should return a string.");

        Assert(defaultArguments.Contains("-T 1", StringComparison.Ordinal), "default live request should continue from the current point");
    }

    private static async Task TestAdbCollectorSmokeAsync()
    {
        var collector = new AdbLogCollector();
        var device = await GetReadyDeviceAsync(collector).ConfigureAwait(false);
        var marker = $"EASYLOG_ADB_COLLECTOR_{Guid.NewGuid():N}";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var readTask = WaitForCollectorMarkerAsync(collector, device.Serial, marker, cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token).ConfigureAwait(false);
        await EmitTestLogsAsync(device.Serial, marker, 3, cts.Token).ConfigureAwait(false);

        var matchedLine = await readTask.ConfigureAwait(false);
        Assert(matchedLine.Contains(marker, StringComparison.Ordinal), "adb collector should observe emitted device log line");
    }

    private static async Task TestEngineLiveSessionSmokeAsync()
    {
        using var engine = EasyLogEngine.CreateDefault();
        var devices = await engine.DiscoverDevicesAsync().ConfigureAwait(false);
        var device = devices.FirstOrDefault(static x => string.Equals(x.State, "device", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("adb smoke test requires at least one ready device.");

        var marker = $"EASYLOG_ENGINE_LIVE_{Guid.NewGuid():N}";
        var appendedRecordSource = new TaskCompletionSource<LogRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionFaultSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnRecordAppended(object? sender, LogRecordAppendedEventArgs e)
        {
            if (e.Record.Message.Contains(marker, StringComparison.Ordinal))
            {
                appendedRecordSource.TrySetResult(e.Record);
            }
        }

        void OnSessionStateChanged(object? sender, SessionStateChangedEventArgs e)
        {
            if (e.State.RunState is SessionRunState.Faulted)
            {
                sessionFaultSource.TrySetResult(e.State.StatusMessage);
            }
        }

        engine.LogRecordAppended += OnRecordAppended;
        engine.SessionStateChanged += OnSessionStateChanged;
        try
        {
            await engine.StartLiveSessionAsync(device).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            await EmitTestLogsAsync(device.Serial, marker, 3, CancellationToken.None).ConfigureAwait(false);

            var completedTask = await Task.WhenAny(
                    appendedRecordSource.Task,
                    sessionFaultSource.Task,
                    Task.Delay(TimeSpan.FromSeconds(25)))
                .ConfigureAwait(false);

            if (completedTask == sessionFaultSource.Task)
            {
                throw new InvalidOperationException(await sessionFaultSource.Task.ConfigureAwait(false));
            }

            if (completedTask != appendedRecordSource.Task)
            {
                throw new TimeoutException($"Timed out waiting for live append. Current session status: {engine.CurrentSession.StatusMessage}");
            }

            var record = await appendedRecordSource.Task.ConfigureAwait(false);
            Assert(record.Tag == "EasyLogSmoke", "engine live session should parse emitted test tag");
            Assert(record.Message.Contains(marker, StringComparison.Ordinal), "engine live session should capture emitted marker message");
            Assert(engine.CurrentSession.RunState == SessionRunState.Running, "engine live session should be running while collecting logs");
        }
        finally
        {
            engine.LogRecordAppended -= OnRecordAppended;
            engine.SessionStateChanged -= OnSessionStateChanged;
            await engine.StopLiveSessionAsync().ConfigureAwait(false);
        }

        Assert(engine.CurrentSession.RunState == SessionRunState.Stopped, "engine live session should stop cleanly");
        Assert(engine.CurrentSession.TotalRecords > 0, "engine live session should collect at least one record");
    }

    private static void Run(Action test, ICollection<string> failures)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            failures.Add(ex.Message);
        }
    }

    private static async Task RunAsync(Func<Task> test, ICollection<string> failures)
    {
        try
        {
            await test();
        }
        catch (Exception ex)
        {
            failures.Add($"{test.Method.Name}: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool MatchesRecord(LogRecord record, string term) =>
        record.Tag.Contains(term, StringComparison.OrdinalIgnoreCase)
        || record.Message.Contains(term, StringComparison.OrdinalIgnoreCase)
        || (record.Pid?.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);

    private static async Task<DeviceInfo> GetReadyDeviceAsync(AdbLogCollector collector)
    {
        var devices = await collector.DiscoverDevicesAsync().ConfigureAwait(false);
        return devices.FirstOrDefault(static x => string.Equals(x.State, "device", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("adb smoke test requires at least one ready device.");
    }

    private static async Task<string> WaitForCollectorMarkerAsync(AdbLogCollector collector, string serial, string marker, CancellationToken cancellationToken)
    {
        await foreach (var line in collector.CollectAsync(
                           CollectionRequest.ForAdb(deviceSerial: serial, buffers: new[] { LogBufferKind.Main }),
                           cancellationToken).ConfigureAwait(false))
        {
            if (line.Contains(marker, StringComparison.Ordinal))
            {
                return line;
            }
        }

        throw new TimeoutException("Timed out before collector observed the emitted device log line.");
    }

    private static async Task EmitTestLogsAsync(string serial, string marker, int count, CancellationToken cancellationToken)
    {
        for (var i = 0; i < count; i++)
        {
            await EmitTestLogAsync(serial, $"{marker}_{i + 1}", cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EmitTestLogAsync(string serial, string marker, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = $"-s {serial} shell log -t EasyLogSmoke {marker}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to emit adb smoke test log: {error.Trim()}");
        }
    }

    private static string? ResolveSevenZipExecutablePath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static async Task<string> ListSevenZipEntriesAsync(string sevenZipPath, string archivePath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = $"l \"{archivePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to inspect 7z archive: {error.Trim()}");
        }

        return output;
    }
}

