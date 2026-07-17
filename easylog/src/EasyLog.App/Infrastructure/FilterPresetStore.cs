using System.IO;
using System.Text.Json;
using EasyLog.App.Models;

namespace EasyLog.App.Infrastructure;

public sealed class FilterPresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public FilterPresetStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            AppContext.BaseDirectory,
            "LogFilter",
            "filters.json");
        DefaultDirectory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(DefaultDirectory);
    }

    public string FilePath { get; }

    public string DefaultDirectory { get; }

    public async Task<IReadOnlyList<FilterPresetModel>> LoadAsync(CancellationToken cancellationToken = default) =>
        await LoadAsync(FilePath, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<FilterPresetModel>> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return Array.Empty<FilterPresetModel>();
        }

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        var items = await JsonSerializer.DeserializeAsync<List<FilterPresetModel>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return items ?? new List<FilterPresetModel>();
    }

    public async Task SaveAsync(IEnumerable<FilterPresetModel> presets, CancellationToken cancellationToken = default) =>
        await SaveAsync(presets, FilePath, cancellationToken).ConfigureAwait(false);

    public async Task SaveAsync(IEnumerable<FilterPresetModel> presets, string filePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream, presets.ToList(), JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}



