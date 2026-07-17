using System.IO;
using System.Text.Json;

namespace EasyLog.App.Infrastructure;

public sealed class UiPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public UiPreferencesStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppMetadata.ProductFolderName,
            "ui-preferences.json");
    }

    public string FilePath { get; }

    public UiPreferences Load()
    {
        if (!File.Exists(FilePath))
        {
            return UiPreferences.Default;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<UiPreferences>(json, JsonOptions) ?? UiPreferences.Default;
        }
        catch
        {
            return UiPreferences.Default;
        }
    }

    public void Save(UiPreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.Serialize(preferences, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}

public sealed class UiPreferences
{
    public const string DefaultAppFontFamily = "Segoe UI";

    public double LogFontSize { get; init; } = 13;

    public string AppFontFamily { get; init; } = DefaultAppFontFamily;

    public string LastOpenedLogFilePath { get; init; } = string.Empty;

    public Dictionary<string, double> ColumnWidths { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> ColumnDisplayIndexes { get; init; } = new(StringComparer.Ordinal);

    public bool IsSearchInAllLogs { get; init; } = false;

    public List<string> SearchHistory { get; init; } = new();

    public static UiPreferences Default { get; } = new();
}


