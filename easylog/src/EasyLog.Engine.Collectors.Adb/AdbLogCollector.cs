using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using EasyLog.Contracts.Enums;
using EasyLog.Contracts.Interfaces;
using EasyLog.Contracts.Models;

namespace EasyLog.Engine.Collectors.Adb;

public sealed class AdbLogCollector : ILogCollector
{
    private readonly string? _configuredAdbPath;

    public AdbLogCollector(string? configuredAdbPath = null)
    {
        _configuredAdbPath = configuredAdbPath;
    }

    public async Task<IReadOnlyList<DeviceInfo>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        var adbPath = ResolveAdbPath(null);
        var lines = await RunProcessCaptureLinesAsync(adbPath, "devices -l", cancellationToken).ConfigureAwait(false);
        var devices = new List<DeviceInfo>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length < 2)
            {
                continue;
            }

            var serial = tokens[0];
            var state = tokens[1];
            string? product = null;
            string? model = null;
            string? device = null;

            foreach (var token in tokens.Skip(2))
            {
                if (token.StartsWith("product:")) product = token[8..];
                else if (token.StartsWith("model:")) model = token[6..].Replace('_', ' ');
                else if (token.StartsWith("device:")) device = token[7..];
            }

            devices.Add(new DeviceInfo(serial, state, product, model, device));
        }

        return new ReadOnlyCollection<DeviceInfo>(devices);
    }

    public async IAsyncEnumerable<string> CollectAsync(
        CollectionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request.Mode != SessionMode.LiveAdb)
        {
            throw new ArgumentException("AdbLogCollector는 LiveAdb 모드 요청만 처리할 수 있습니다.", nameof(request));
        }

        var adbPath = ResolveAdbPath(request.AdbPath);
        using var process = BuildLogcatProcess(adbPath, request);
        try
        {
            try
            {
                process.Start();
            }
            catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
            {
                throw CreateAdbLaunchException(adbPath, ex);
            }

            await using var registration = cancellationToken.Register(() =>
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
                    // 취소 시 정리 최선 시도
                }
            }).AsAsyncDisposable();

            while (!process.StandardOutput.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                if (line is not null)
                {
                    yield return line;
                }
            }

            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw CreateAdbCommandException("adb logcat", error);
            }
        }
        finally
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
                // 열거 조기 종료 시 정리 최선 시도
            }
        }
    }

    private Process BuildLogcatProcess(string adbPath, CollectionRequest request)
    {
        var arguments = BuildLogcatArguments(request);
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
    }

    private string BuildLogcatArguments(CollectionRequest request)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.DeviceSerial))
        {
            parts.Add($"-s {request.DeviceSerial}");
        }

        if (request.LiveStartFrom.HasValue)
        {
            var startFrom = request.LiveStartFrom.Value.DateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            parts.Add($"logcat -v threadtime -T \"{startFrom}\"");
        }
        else
        {
            parts.Add("logcat -v threadtime -T 1");
        }

        var buffers = request.Buffers is { Count: > 0 }
            ? request.Buffers
            : new[] { LogBufferKind.Main, LogBufferKind.System, LogBufferKind.Crash, LogBufferKind.Events };

        foreach (var buffer in buffers)
        {
            var bufferName = buffer switch
            {
                LogBufferKind.Main => "main",
                LogBufferKind.System => "system",
                LogBufferKind.Crash => "crash",
                LogBufferKind.Events => "events",
                LogBufferKind.Radio => "radio",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(bufferName))
            {
                parts.Add($"-b {bufferName}");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.AdditionalArguments))
        {
            parts.Add(request.AdditionalArguments);
        }

        return string.Join(' ', parts);
    }

    private string ResolveAdbPath(string? requestAdbPath)
    {
        if (!string.IsNullOrWhiteSpace(requestAdbPath))
        {
            return requestAdbPath;
        }

        if (!string.IsNullOrWhiteSpace(_configuredAdbPath))
        {
            return _configuredAdbPath;
        }

        var bundledAdb = Path.Combine(AppContext.BaseDirectory, "tools", "adb.exe");
        if (File.Exists(bundledAdb))
        {
            return bundledAdb;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var sdkAdb = Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe");
        if (File.Exists(sdkAdb))
        {
            return sdkAdb;
        }

        return "adb";
    }

    private static async Task<IReadOnlyList<string>> RunProcessCaptureLinesAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var lines = new List<string>();
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw CreateAdbLaunchException(fileName, ex);
        }
        while (!process.StandardOutput.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
            if (line is not null)
            {
                lines.Add(line);
            }
        }

        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw CreateAdbCommandException("adb devices", error);
        }

        return lines;
    }

    private static Exception CreateAdbLaunchException(string adbPath, Exception innerException) =>
        new InvalidOperationException(
            $"adb를 실행할 수 없습니다. PATH 또는 adb 경로를 확인하세요. 현재 경로: {adbPath}",
            innerException);

    private static Exception CreateAdbCommandException(string commandName, string? error)
    {
        var message = error?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return new InvalidOperationException($"{commandName} 실행에 실패했습니다.");
        }

        if (message.Contains("no devices/emulators found", StringComparison.OrdinalIgnoreCase))
        {
            return new InvalidOperationException("adb 장치를 찾지 못했습니다. 장치를 연결하고 USB 디버깅 상태를 확인하세요.");
        }

        if (message.Contains("device unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return new InvalidOperationException("adb 장치가 unauthorized 상태입니다. 장치 화면에서 USB 디버깅 허용을 승인하세요.");
        }

        if (message.Contains("device offline", StringComparison.OrdinalIgnoreCase))
        {
            return new InvalidOperationException("adb 장치가 offline 상태입니다. 케이블/연결 상태를 확인한 뒤 다시 시도하세요.");
        }

        return new InvalidOperationException($"{commandName} 실행 실패: {message}");
    }
}

file static class CancellationTokenRegistrationExtensions
{
    public static IAsyncDisposable AsAsyncDisposable(this CancellationTokenRegistration registration) =>
        new RegistrationAsyncDisposable(registration);

    private sealed class RegistrationAsyncDisposable : IAsyncDisposable
    {
        private CancellationTokenRegistration _registration;

        public RegistrationAsyncDisposable(CancellationTokenRegistration registration)
        {
            _registration = registration;
        }

        public ValueTask DisposeAsync()
        {
            _registration.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

