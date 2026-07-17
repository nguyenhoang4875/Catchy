using System.Runtime.CompilerServices;
using System.Text;
using EasyLog.Contracts.Interfaces;
using EasyLog.Contracts.Models;

namespace EasyLog.Engine.Collectors.File;

public sealed class FileLogCollector : ILogCollector
{
    public async IAsyncEnumerable<string> CollectAsync(
        CollectionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request.Mode != Contracts.Enums.SessionMode.File)
        {
            throw new ArgumentException("FileLogCollector는 파일 모드 요청만 처리할 수 있습니다.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SourcePath))
        {
            throw new ArgumentException("파일 경로가 비어 있습니다.", nameof(request));
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Try strict UTF-8 first (throws on invalid bytes), then fallback encodings
        var encoding = DetectEncoding(request.SourcePath);

        await using var stream = new FileStream(
            request.SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            request.ReadBufferSize,
            FileOptions.SequentialScan | FileOptions.Asynchronous);

        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is not null)
            {
                yield return line;
            }
        }
    }

    private static Encoding DetectEncoding(string filePath)
    {
        const int chunkSize = 64 * 1024;

        byte[] sample;
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
            var fileLength = fs.Length;

            if (fileLength == 0)
                return Encoding.UTF8;

            if (fileLength <= chunkSize * 3)
            {
                sample = new byte[(int)fileLength];
                _ = fs.Read(sample, 0, sample.Length);
            }
            else
            {
                // Read start chunk
                var startBuf = new byte[chunkSize];
                var startRead = fs.Read(startBuf, 0, chunkSize);
                sample = startBuf.AsSpan(0, startRead).ToArray();

                // If start is all ASCII, try middle and end
                if (!HasNonAscii(sample))
                {
                    var midBuf = new byte[chunkSize];
                    fs.Seek(fileLength / 2, SeekOrigin.Begin);
                    var midRead = fs.Read(midBuf, 0, chunkSize);

                    if (HasNonAscii(midBuf.AsSpan(0, midRead)))
                    {
                        var combined = new byte[startRead + midRead];
                        Buffer.BlockCopy(startBuf, 0, combined, 0, startRead);
                        Buffer.BlockCopy(midBuf, 0, combined, startRead, midRead);
                        sample = combined;
                    }
                    else
                    {
                        var endBuf = new byte[chunkSize];
                        fs.Seek(Math.Max(0, fileLength - chunkSize), SeekOrigin.Begin);
                        var endRead = fs.Read(endBuf, 0, chunkSize);

                        if (HasNonAscii(endBuf.AsSpan(0, endRead)))
                        {
                            var combined = new byte[startRead + endRead];
                            Buffer.BlockCopy(startBuf, 0, combined, 0, startRead);
                            Buffer.BlockCopy(endBuf, 0, combined, startRead, endRead);
                            sample = combined;
                        }
                    }
                }
            }
        }
        catch
        {
            return Encoding.UTF8;
        }

        // BOM check
        if (sample.Length >= 3 && sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
            return Encoding.UTF8;
        if (sample.Length >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
            return Encoding.Unicode;
        if (sample.Length >= 2 && sample[0] == 0xFE && sample[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        // All ASCII — encoding doesn't matter
        if (!HasNonAscii(sample))
            return Encoding.UTF8;

        // Trim to safe UTF-8 boundary (avoid incomplete multi-byte at end)
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

        // Try CP949
        try
        {
            var cp949 = Encoding.GetEncoding(949);
            var decoded = cp949.GetString(sample);
            if (!decoded.Contains('\uFFFD'))
                return cp949;
        }
        catch { }

        return Encoding.UTF8;
    }

    private static bool HasNonAscii(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            if (b > 0x7F) return true;
        }
        return false;
    }

    private static byte[] TrimToUtf8Boundary(byte[] data)
    {
        if (data.Length == 0) return data;
        var end = data.Length;
        while (end > 0 && (data[end - 1] & 0xC0) == 0x80) end--;
        if (end > 0 && data[end - 1] >= 0x80)
        {
            var sb = data[end - 1];
            var expected = (sb & 0xE0) == 0xC0 ? 2 : (sb & 0xF0) == 0xE0 ? 3 : (sb & 0xF8) == 0xF0 ? 4 : 1;
            if (data.Length - end + 1 < expected) end--;
        }
        return end == data.Length ? data : data.AsSpan(0, end).ToArray();
    }
}

