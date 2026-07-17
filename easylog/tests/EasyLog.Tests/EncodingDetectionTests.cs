using System.Text;

namespace EasyLog.Tests;

/// <summary>
/// Tests for the encoding detection used by EasyLogEngine.
/// Uses reflection to call the private DetectFileEncoding method.
/// </summary>
public static class EncodingDetectionTests
{
    static EncodingDetectionTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static void RunAll()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        Console.WriteLine("[Encoding] Running encoding detection tests...");

        TestUtf8FileDetectedCorrectly();
        TestCp949FileDetectedCorrectly();
        TestCp949KoreanAfter64KBDetectedCorrectly();
        TestUtf8KoreanAtBoundaryDetectedCorrectly();
        TestPureAsciiDefaultsToUtf8();

        Console.WriteLine("[Encoding] All encoding detection tests passed.");
    }

    private static void TestUtf8FileDetectedCorrectly()
    {
        // Create a temp file with UTF-8 Korean content
        var path = Path.GetTempFileName();
        try
        {
            var content = "03-31 21:22:31.277  5850  5850 D CCSVM: 커넥티드 서비스 센터: 080-700-6000\n";
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var result = LoadAndCheckKorean(path, "커넥티드 서비스 센터");
            Assert(result, "UTF-8 file: Korean text should be preserved");
            Console.WriteLine("  [PASS] UTF-8 Korean file detected correctly");
        }
        finally { File.Delete(path); }
    }

    private static void TestCp949FileDetectedCorrectly()
    {
        var path = Path.GetTempFileName();
        try
        {
            // Verify CP949 is available
            var cp949 = Encoding.GetEncoding(949); // Use code page number instead of name
            Console.WriteLine($"    [DEBUG] CP949 encoding resolved: {cp949.EncodingName}");
            var content = "03-31 21:22:31.277  5850  5850 D CCSVM: 커넥티드 서비스 센터: 080-700-6000\n";
            File.WriteAllBytes(path, cp949.GetBytes(content));

            var result = LoadAndCheckKorean(path, "커넥티드 서비스 센터");
            Assert(result, "CP949 file: Korean text should be preserved");
            Console.WriteLine("  [PASS] CP949 Korean file detected correctly");
        }
        finally { File.Delete(path); }
    }

    private static void TestCp949KoreanAfter64KBDetectedCorrectly()
    {
        // Key scenario: first 64KB is ASCII-only, Korean text appears after
        var path = Path.GetTempFileName();
        try
        {
            var cp949 = Encoding.GetEncoding(949); // Use code page number
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

            // Write >64KB of ASCII-only log lines
            var asciiLine = "03-31 21:22:31.277  5850  5850 D TestTag: This is an ASCII-only log line for testing encoding detection\n";
            var asciiBytes = Encoding.ASCII.GetBytes(asciiLine);
            var linesNeeded = (70 * 1024) / asciiBytes.Length + 1; // >64KB
            for (int i = 0; i < linesNeeded; i++)
                fs.Write(asciiBytes);

            // Write Korean line AFTER the 64KB mark
            var koreanLine = "03-31 21:22:31.277  5850  5850 D CCSVM: 커넥티드 서비스 센터: 080-700-6000\n";
            var koreanBytes = cp949.GetBytes(koreanLine);
            fs.Write(koreanBytes);
            fs.Flush();
            fs.Close();

            var result = LoadAndCheckKorean(path, "커넥티드 서비스 센터");
            Assert(result, "CP949 file with Korean after 64KB: Korean text should be preserved");
            Console.WriteLine("  [PASS] CP949 Korean after 64KB detected correctly");
        }
        finally { File.Delete(path); }
    }

    private static void TestUtf8KoreanAtBoundaryDetectedCorrectly()
    {
        // UTF-8 file where a 3-byte Korean char might straddle the sample boundary
        var path = Path.GetTempFileName();
        try
        {
            var sb = new StringBuilder();
            // Fill with ASCII up to near boundary, then add Korean
            while (Encoding.UTF8.GetByteCount(sb.ToString()) < 65530)
                sb.Append("A");
            sb.Append("한글테스트입니다\n");
            sb.Append("More Korean: 커넥티드 서비스\n");

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));

            var result = LoadAndCheckKorean(path, "한글테스트");
            Assert(result, "UTF-8 file with Korean at 64KB boundary should be preserved");
            Console.WriteLine("  [PASS] UTF-8 Korean at boundary detected correctly");
        }
        finally { File.Delete(path); }
    }

    private static void TestPureAsciiDefaultsToUtf8()
    {
        var path = Path.GetTempFileName();
        try
        {
            var content = "03-31 21:22:31.277  5850  5850 D TestTag: Pure ASCII content only\n";
            File.WriteAllText(path, content, Encoding.ASCII);

            // Just verify it loads without error
            var engine = EasyLog.Engine.EasyLogEngine.CreateDefault();
            var task = engine.LoadFileAsync(path);
            task.Wait();
            var records = task.Result.Records;
            Assert(records.Count > 0, "Pure ASCII file should load successfully");
            Assert(records[0].Message.Contains("Pure ASCII"), "Pure ASCII content should be preserved");
            engine.Dispose();
            Console.WriteLine("  [PASS] Pure ASCII defaults to UTF-8");
        }
        finally { File.Delete(path); }
    }

    private static bool LoadAndCheckKorean(string filePath, string expectedKorean)
    {
        var engine = EasyLog.Engine.EasyLogEngine.CreateDefault();
        try
        {
            var task = engine.LoadFileAsync(filePath);
            task.Wait();
            var records = task.Result.Records;

            foreach (var record in records)
            {
                if (record.Message.Contains(expectedKorean))
                    return true;
            }

            // Dump first few messages for debugging
            Console.WriteLine($"    [DEBUG] Expected to find '{expectedKorean}' but found:");
            for (int i = 0; i < Math.Min(records.Count, 5); i++)
            {
                Console.WriteLine($"    [DEBUG]   [{i}] {records[i].Message}");
            }
            return false;
        }
        finally
        {
            engine.Dispose();
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[Encoding Test FAILED] {message}");
    }
}

