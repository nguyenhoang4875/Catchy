using System.Diagnostics;
using EasyLog.Engine.Collectors.File;
using EasyLog.Engine.Diagnostics;
using EasyLog.Engine.Indexes;
using EasyLog.Engine.Parsers;
using EasyLog.Engine.Query;
using EasyLog.Engine.Storage;

namespace EasyLog.Benchmarks;

internal static class Program
{
    public static async Task<int> Main()
    {
        try
        {
            var samplePath = ResolveSampleLogPath();
            var collector = new FileLogCollector();
            var parser = new ThreadtimeLogParser();
            var indexes = new InMemoryLogIndexes();
            var queryEngine = new LogQueryEngine(indexes);
            var diagnostics = new DiagnosticHighlighter();
            await using var storage = new RawSpoolStore();

            var sw = Stopwatch.StartNew();
            long rowId = 0;
            await foreach (var line in collector.CollectAsync(EasyLog.Contracts.Models.CollectionRequest.ForFile(samplePath)))
            {
                var record = parser.ParseOrFallback(line, ++rowId);
                await storage.AppendAsync(record);
                queryEngine.Index(record);
                _ = diagnostics.Evaluate(record);
            }

            sw.Stop();
            var records = storage.Snapshot();
            var filtered = queryEngine.Apply(records, new EasyLog.Contracts.Models.FilterQuery(TextContains: "FATAL"));

            Console.WriteLine($"Sample: {samplePath}");
            Console.WriteLine($"Records: {records.Count}");
            Console.WriteLine($"ElapsedMs: {sw.ElapsedMilliseconds}");
            Console.WriteLine($"FatalMatches: {filtered.Count}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Benchmark failed: {ex.Message}");
            return 1;
        }
    }

    private static string ResolveSampleLogPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "sample-logs", "aaos-sample.log");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("샘플 로그 파일을 찾을 수 없습니다.", "sample-logs/aaos-sample.log");
    }
}

