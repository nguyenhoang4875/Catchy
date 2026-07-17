using System.Diagnostics;
using System.IO.Compression;

namespace EasyLog.Engine;

/// <summary>
/// Extracts log files from archive formats (.zip, .7z).
/// </summary>
public static class ArchiveExtractor
{
    private static readonly HashSet<string> LogExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".logcat", ".txt"
    };

    /// <summary>
    /// Returns true if the given file path has a supported archive extension.
    /// </summary>
    public static bool IsArchive(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".7z", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts an archive to a temp directory and returns log file paths sorted by name (natural order).
    /// </summary>
    public static async Task<ArchiveExtractionResult> ExtractAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Archive file not found.", archivePath);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "LogPilot-Extract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var ext = Path.GetExtension(archivePath);
            if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                await ExtractZipAsync(archivePath, tempDir, cancellationToken).ConfigureAwait(false);
            }
            else if (ext.Equals(".7z", StringComparison.OrdinalIgnoreCase))
            {
                await Extract7zAsync(archivePath, tempDir, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new NotSupportedException($"Unsupported archive format: {ext}");
            }

            var logFiles = Directory.EnumerateFiles(tempDir, "*.*", SearchOption.AllDirectories)
                .Where(f => LogExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ArchiveExtractionResult(tempDir, logFiles);
        }
        catch
        {
            // Cleanup temp dir on failure
            TryDeleteDirectory(tempDir);
            throw;
        }
    }

    /// <summary>
    /// Deletes a temp extraction directory. Best-effort, ignores errors.
    /// </summary>
    public static void CleanupTempDirectory(string tempDir)
    {
        TryDeleteDirectory(tempDir);
    }

    private static Task ExtractZipAsync(string archivePath, string destDir, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ZipFile.ExtractToDirectory(archivePath, destDir, overwriteFiles: true);
        }, cancellationToken);
    }

    private static async Task Extract7zAsync(string archivePath, string destDir, CancellationToken cancellationToken)
    {
        var sevenZipPath = ResolveSevenZipExecutablePath();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = $"x -y -o\"{destDir}\" \"{archivePath}\"",
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
            throw new InvalidOperationException("7z extraction tool not found. Please ensure 7-Zip is installed.", ex);
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
                // best-effort kill
            }
        });

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"7z extraction failed. {stdErr}{stdOut}".Trim());
        }
    }

    internal static string ResolveSevenZipExecutablePath()
    {
        var bundledSevenZip = Path.Combine(AppContext.BaseDirectory, "tools", "7z.exe");
        if (File.Exists(bundledSevenZip))
        {
            return bundledSevenZip;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var candidates = new[]
        {
            Path.Combine(programFiles, "7-Zip", "7z.exe"),
            Path.Combine(programFilesX86, "7-Zip", "7z.exe"),
            "7z.exe"
        };

        foreach (var candidate in candidates)
        {
            if (candidate.Contains(Path.DirectorySeparatorChar) || candidate.Contains(Path.AltDirectorySeparatorChar))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                continue;
            }

            return candidate;
        }

        return "7z.exe";
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}

/// <summary>
/// Result of an archive extraction containing temp directory path and extracted log file paths.
/// </summary>
public sealed record ArchiveExtractionResult(string TempDirectory, IReadOnlyList<string> LogFilePaths);

