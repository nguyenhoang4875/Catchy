using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using EasyLog.App;
using EasyLog.App.Models;
using NUnit.Framework;

namespace EasyLog.UiTests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class UiPersistenceTests
{
    private readonly string _preferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LogPilot",
        "ui-preferences.json");

    private readonly string _backupPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LogPilot",
        "ui-preferences.json.copilot-backup");

    private bool _hadOriginalPreferences;

    [SetUp]
    public void SetUp()
    {
        EnsureApplication();
        _hadOriginalPreferences = File.Exists(_preferencesPath);

        if (_hadOriginalPreferences)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_backupPath)!);
            File.Copy(_preferencesPath, _backupPath, overwrite: true);
        }
        else if (File.Exists(_backupPath))
        {
            File.Delete(_backupPath);
        }

        if (File.Exists(_preferencesPath))
        {
            File.Delete(_preferencesPath);
        }
    }

    [TearDown]
    public void TearDown()
    {
        if (_hadOriginalPreferences && File.Exists(_backupPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_preferencesPath)!);
            File.Copy(_backupPath, _preferencesPath, overwrite: true);
            File.Delete(_backupPath);
            return;
        }

        if (File.Exists(_preferencesPath))
        {
            File.Delete(_preferencesPath);
        }

        if (File.Exists(_backupPath))
        {
            File.Delete(_backupPath);
        }
    }

    [Test]
    public void MainWindow_Persists_And_Restores_Column_DisplayIndex_Order()
    {
        var firstSession = CreateWindow();
        try
        {
            var logGrid = GetGrid(firstSession, "LogGrid");
            var searchResultsGrid = GetGrid(firstSession, "SearchResultsGrid");

            GetColumn(logGrid, "Message").DisplayIndex = 0;
            GetColumn(searchResultsGrid, "Message").DisplayIndex = 0;

            InvokePrivate(firstSession, "CaptureCurrentColumnLayout");
        }
        finally
        {
            ShutdownMainWindow(firstSession);
        }

        Assert.That(File.Exists(_preferencesPath), Is.True, "ui-preferences.json should be created after capturing the column layout.");

        using (var document = JsonDocument.Parse(File.ReadAllText(_preferencesPath)))
        {
            var displayIndexes = document.RootElement.GetProperty("ColumnDisplayIndexes");
            Assert.Multiple(() =>
            {
                Assert.That(displayIndexes.GetProperty("LogGrid.Message").GetInt32(), Is.EqualTo(0));
                Assert.That(displayIndexes.GetProperty("SearchResultsGrid.Message").GetInt32(), Is.EqualTo(0));
            });
        }

        var secondSession = CreateWindow();
        try
        {
            InvokePrivate(secondSession, "ApplySavedColumnDisplayIndexes");

            var restoredLogOrder = GetColumnHeadersInDisplayOrder(GetGrid(secondSession, "LogGrid"));
            var restoredSearchOrder = GetColumnHeadersInDisplayOrder(GetGrid(secondSession, "SearchResultsGrid"));

            Assert.Multiple(() =>
            {
                Assert.That(restoredLogOrder[0], Is.EqualTo("Message"), $"Restored log order: {string.Join(",", restoredLogOrder)}");
                Assert.That(restoredSearchOrder[0], Is.EqualTo("Message"), $"Restored search order: {string.Join(",", restoredSearchOrder)}");
            });
        }
        finally
        {
            ShutdownMainWindow(secondSession);
        }
    }

    [Test]
    public void FilterEditorWindow_Treats_Run_Source_As_Outside_Without_Throwing()
    {
        var window = new FilterEditorWindow(
            dialogTitle: "Test",
            source: new FilterPresetModel(),
            colorOptions: new[] { new FilterColorOption("Test", "#4E79A7") },
            showNameField: false,
            showColorField: true);

        try
        {
            var result = (bool?)typeof(FilterEditorWindow)
                .GetMethod(
                    "IsClickInsideDialog",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(DependencyObject) },
                    modifiers: null)
                ?.Invoke(window, new object?[] { new Run("highlight") });

            Assert.That(result, Is.False);
        }
        finally
        {
            window.Close();
        }
    }

    private static void EnsureApplication()
    {
        if (Application.Current is null)
        {
            _ = new Application();
        }
    }

    private static MainWindow CreateWindow() => new();

    private static void ShutdownMainWindow(MainWindow window)
    {
        typeof(MainWindow)
            .GetMethod(
                "OnClosed",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(object), typeof(EventArgs) },
                modifiers: null)
            ?.Invoke(window, new object?[] { null, EventArgs.Empty });
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null)
            ?.Invoke(target, null);
    }

    private static DataGrid GetGrid(MainWindow window, string fieldName)
    {
        var field = typeof(MainWindow).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find field '{fieldName}'.");

        return (DataGrid)(field.GetValue(window) ?? throw new InvalidOperationException($"Field '{fieldName}' was null."));
    }

    private static DataGridColumn GetColumn(DataGrid grid, string header)
    {
        return grid.Columns.FirstOrDefault(column => string.Equals(column.Header?.ToString(), header, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Could not find column '{header}' in grid '{grid.Name}'.");
    }

    private static IReadOnlyList<string> GetColumnHeadersInDisplayOrder(DataGrid grid)
    {
        return grid.Columns
            .OrderBy(column => column.DisplayIndex)
            .Select(column => column.Header?.ToString() ?? string.Empty)
            .ToArray();
    }
}

