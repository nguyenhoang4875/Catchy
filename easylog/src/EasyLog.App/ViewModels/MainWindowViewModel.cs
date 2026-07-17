using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using EasyLog.App.Infrastructure;
using EasyLog.App.Models;
using EasyLog.Contracts.Enums;
using EasyLog.Contracts.Models;
using EasyLog.Engine;
using EasyLog.Engine.Query;

namespace EasyLog.App.ViewModels;

public enum LogLoadMode
{
    Full,
    Filtered
}

public sealed record LiveLookbackOption(string Label, TimeSpan? LookbackWindow, string StatusSuffix);

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const int LiveUiBatchSize = 250;
    private const int LiveUiCatchupBatchSize = 1000;
    private static readonly LiveLookbackOption[] DefaultLiveLookbackOptions =
    {
        new("Live only", null, "showing new live entries"),
        new("Last 10 sec", TimeSpan.FromSeconds(10), "showing logs from the last 10 seconds and new entries"),
        new("Last 30 sec", TimeSpan.FromSeconds(30), "showing logs from the last 30 seconds and new entries"),
        new("Last 2 min", TimeSpan.FromMinutes(2), "showing logs from the last 2 minutes and new entries")
    };
    private readonly EasyLogEngine _engine;
    private readonly FilterPresetStore _filterPresetStore;
    private readonly UiPreferencesStore _uiPreferencesStore;
    private HashSet<long> _visibleRowIds = new();
    // RowId → LogRowModel lookup for O(1) jump-to-row from search results (Phase 3 — 5순위).
    // Maintained in lockstep with _visibleRowIds; replaces O(N) Records.FirstOrDefault scans
    // on every search result selection. ~24B × N (long key + ref) added memory; within the
    // headroom obtained from H section (Tag interning, RawLine 제거, LogRowModel 클래스화).
    // Note: this is a pure lookup map — NOT on-demand row materialization (which was the
    // failed VirtualLogList approach, see Context_Progress.md §I).
    private Dictionary<long, LogRowModel> _visibleRowsByRowId = new();
    private readonly ConcurrentQueue<LogRecord> _pendingLiveRecords = new();
    private bool _isFilterEditorOpen;
    private readonly Dictionary<string, (Brush Background, Brush Foreground)> _highlightBrushCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _columnWidthPreferences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _columnDisplayIndexPreferences = new(StringComparer.Ordinal);
    private IReadOnlyList<(FilterPresetModel Preset, FilterQuery Query)> _enabledFilterRulesCache = Array.Empty<(FilterPresetModel Preset, FilterQuery Query)>();
    private readonly DispatcherTimer _liveUiFlushTimer;
    private readonly DispatcherTimer _deviceMonitorTimer;
    private readonly IReadOnlyList<LiveLookbackOption> _liveLookbackOptions = DefaultLiveLookbackOptions;
    private string _windowTitle = AppMetadata.FormatWindowTitle();
    private string _statusText = "Ready";
    private string _filterText = string.Empty;
    private string _tagFilterText = string.Empty;
    private string _pidFilterText = string.Empty;
    private string _excludedFilterText = string.Empty;
    private string _excludedTagFilterText = string.Empty;
    private string _excludedPidFilterText = string.Empty;
    private string _filterPresetName = string.Empty;
    private bool _isBusy;
    private bool _isFileLoading;
    private CancellationTokenSource? _fileLoadCts;
    private bool _isLoadOptionVisible;
    private TaskCompletionSource<LogLoadMode?>? _loadOptionTcs;
    private bool _isLiveSessionRunning;
    private bool _isWaitingForDevice;
    private CancellationTokenSource? _waitForDeviceCts;
    private bool _isVerboseEnabled = true;
    private bool _isDebugEnabled = true;
    private bool _isInfoEnabled = true;
    private bool _isWarnEnabled = true;
    private bool _isErrorEnabled = true;
    private bool _isFatalEnabled = true;
    private bool _isLiveAppendPaused;
    private bool _isAutoScrollEnabled = true;
    private int _pausedAppendCount;
    private int _queuedPausedAppendCount;
    private bool _isResumingPausedAppends;
    private bool _isApplyingPresetState;
    private bool _isFilterSetDirty;
    private double _logFontSize = 13;
    private string _selectedAppFontFamily = UiPreferences.DefaultAppFontFamily;
    private string _currentFilterSetPath = string.Empty;
    private string _busyMessage = "Working...";
    private string _searchText = string.Empty;
    private bool _isSearchPaneVisible = true;
    private bool _isAiChatPanelVisible;
    private string _searchStatusText = "Search Tag / Message / PID. Use | for OR and & for AND.";
    private IReadOnlyList<string> _searchHighlightTerms = Array.Empty<string>();
    private LogRowModel? _selectedRow;
    private SearchResultRowModel? _selectedSearchResult;
    private FilterQuery _activeFilterQuery = FilterQuery.Empty;
    private DeviceInfo? _selectedDevice;
    private LiveLookbackOption _selectedLiveLookbackOption = DefaultLiveLookbackOptions[2];
    private FilterColorOption? _selectedFilterColor;
    private FilterPresetModel? _selectedFilterPreset;
    private bool _isAdbDeviceConnected;
    private string _adbConnectionStatusText = "No adb devices connected.";
    private bool _isSilentDeviceRefreshRunning;
    private int _adbConsecutiveFailureCount;
    private const int AdbMaxConsecutiveFailures = 3;
    private bool _isAllFilterRulesSelected;
    private string _lastOpenedLogFilePath = string.Empty;
    private bool _isSearchInAllLogs;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _filterApplyCts;
    private string _previewMessage = string.Empty;
    private string _previewHeader = string.Empty;
    private bool _isProgressiveFileRendering;
    private const int MaxSearchHistoryCount = 10;

    public MainWindowViewModel()
    {
        _engine = EasyLogEngine.CreateDefault();
        _filterPresetStore = new FilterPresetStore();
        _uiPreferencesStore = new UiPreferencesStore();
        _engine.LogRecordsLiveAppended += OnLogRecordsLiveAppended;
        _engine.LogRecordsBatchLoaded += OnLogRecordsBatchLoaded;
        _engine.SessionStateChanged += OnSessionStateChanged;

        _liveUiFlushTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _liveUiFlushTimer.Tick += OnLiveUiFlushTimerTick;
        _liveUiFlushTimer.Start();

        Records = new RangeObservableCollection<LogRowModel>();
        SearchResults = new RangeObservableCollection<SearchResultRowModel>();
        Devices = new ObservableCollection<DeviceInfo>();
        AppFontFamilies = new ObservableCollection<string>(CreateDefaultAppFontFamilies());
        FilterColorOptions = new ObservableCollection<FilterColorOption>(CreateDefaultFilterColors());
        FilterPresets = new ObservableCollection<FilterPresetModel>();
        SearchHistory = new ObservableCollection<string>();
        SelectedFilterColor = FilterColorOptions.FirstOrDefault();
        var uiPreferences = _uiPreferencesStore.Load();
        _logFontSize = Math.Clamp(uiPreferences.LogFontSize, 10, 24);
        _selectedAppFontFamily = NormalizeAppFontFamily(uiPreferences.AppFontFamily);
        _lastOpenedLogFilePath = uiPreferences.LastOpenedLogFilePath ?? string.Empty;
        _isSearchInAllLogs = uiPreferences.IsSearchInAllLogs;
        foreach (var term in uiPreferences.SearchHistory.Take(MaxSearchHistoryCount))
        {
            SearchHistory.Add(term);
        }
        foreach (var columnWidth in uiPreferences.ColumnWidths)
        {
            if (columnWidth.Value > 0)
            {
                _columnWidthPreferences[columnWidth.Key] = columnWidth.Value;
            }
        }

        foreach (var columnDisplayIndex in uiPreferences.ColumnDisplayIndexes)
        {
            if (columnDisplayIndex.Value >= 0)
            {
                _columnDisplayIndexPreferences[columnDisplayIndex.Key] = columnDisplayIndex.Value;
            }
        }

        Records.CollectionChanged += OnDisplayCollectionsChanged;
        SearchResults.CollectionChanged += OnDisplayCollectionsChanged;

        _deviceMonitorTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _deviceMonitorTimer.Tick += OnDeviceMonitorTimerTick;
        _deviceMonitorTimer.Start();

        LoadDemoCommand = new RelayCommand(() => _ = LoadDemoAsync(), () => !IsBusy && !IsLiveSessionRunning);
        LoadSampleCommand = new RelayCommand(() => _ = LoadSampleAsync(), () => !IsBusy && !IsLiveSessionRunning);
        OpenFileCommand = new RelayCommand(() => _ = OpenFileAsync(), () => !IsBusy);
        DiscoverDevicesCommand = new RelayCommand(() => _ = DiscoverDevicesAsync(), () => !IsBusy && !IsLiveSessionRunning);
        StartLiveCommand = new RelayCommand(() => _ = StartLiveAsync(), () => !IsBusy && !IsLiveSessionRunning && !IsWaitingForDevice);
        StartLiveWithLookbackCommand = new RelayCommand<string>(optionKey => _ = StartLiveWithLookbackAsync(optionKey), _ => !IsBusy && !IsLiveSessionRunning && !IsWaitingForDevice);
        SetLiveLookbackCommand = new RelayCommand<string>(SetLiveLookbackOption);
        StopLiveCommand = new RelayCommand(() => _ = StopLiveAsync(), () => IsLiveSessionRunning || IsWaitingForDevice);
        ExportLogsCommand = new RelayCommand(() => _ = ExportLogsAsync(), () => !IsBusy);
        ClearLogsCommand = new RelayCommand(() => _ = ClearLogsAsync(), () => !IsBusy && (Records.Count > 0 || SearchResults.Count > 0 || IsLiveSessionRunning));
        SearchLogsCommand = new RelayCommand(() => _ = SearchLogsAsync(), () => !IsBusy);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => !IsBusy && (IsSearchPaneVisible || !string.IsNullOrWhiteSpace(SearchText)));
        ApplyFilterCommand = new RelayCommand(() => _ = ApplyFilterAsync(), () => !IsBusy);
        ResetFiltersCommand = new RelayCommand(ResetFilters, () => !IsBusy);
        ClearCommand = new RelayCommand(Clear, () => !IsBusy && !IsLiveSessionRunning);
        DecreaseFontSizeCommand = new RelayCommand(DecreaseFontSize, () => LogFontSize > 10);
        IncreaseFontSizeCommand = new RelayCommand(IncreaseFontSize, () => LogFontSize < 24);
        SetAppFontFamilyCommand = new RelayCommand<string>(fontFamily => SelectedAppFontFamily = fontFamily ?? string.Empty);
        SaveCurrentFilterPresetCommand = new RelayCommand(() => _ = SaveCurrentFilterPresetAsync(), () => !IsBusy);
        LoadSelectedFilterPresetCommand = new RelayCommand(() => _ = LoadSelectedFilterPresetAsync(), () => !IsBusy && SelectedFilterPreset is not null);
        DeleteSelectedFilterPresetCommand = new RelayCommand(() => _ = DeleteSelectedFilterPresetAsync(), () => !IsBusy && SelectedFilterPreset is not null);
        NewFilterSetCommand = new RelayCommand(() => _ = NewFilterSetAsync(), () => !IsBusy);
        OpenFilterSetCommand = new RelayCommand(() => _ = OpenFilterSetAsync(), () => !IsBusy);
        SaveFilterSetCommand = new RelayCommand(() => _ = SaveFilterSetAsync(), () => !IsBusy);
        SaveFilterSetAsCommand = new RelayCommand(() => _ = SaveFilterSetAsAsync(), () => !IsBusy);
        EditQuickFilterCommand = new RelayCommand(() => _ = EditQuickFilterAsync(), () => !IsBusy);
        AddFilterRuleCommand = new RelayCommand(() => _ = AddFilterRuleAsync(), () => !IsBusy);
        EditSelectedFilterRuleCommand = new RelayCommand(() => _ = EditSelectedFilterRuleAsync(), () => !IsBusy && SelectedFilterPreset is not null);
        MoveSelectedFilterRuleUpCommand = new RelayCommand(() => _ = MoveSelectedFilterRuleAsync(-1), () => !IsBusy && CanMoveSelectedFilterRule(-1));
        MoveSelectedFilterRuleDownCommand = new RelayCommand(() => _ = MoveSelectedFilterRuleAsync(1), () => !IsBusy && CanMoveSelectedFilterRule(1));
        EnableSelectedFilterRuleCommand = new RelayCommand(() => _ = SetSelectedFilterRulesEnabledAsync(true), () => !IsBusy && CanSetSelectedFilterRulesEnabled(true));
        DisableSelectedFilterRuleCommand = new RelayCommand(() => _ = SetSelectedFilterRulesEnabledAsync(false), () => !IsBusy && CanSetSelectedFilterRulesEnabled(false));
        RemoveSearchHistoryItemCommand = new RelayCommand<string>(RemoveSearchHistoryItem);
    }

    public RangeObservableCollection<LogRowModel> Records { get; }

    public RangeObservableCollection<SearchResultRowModel> SearchResults { get; }

    public ObservableCollection<DeviceInfo> Devices { get; }

    public ObservableCollection<string> AppFontFamilies { get; }

    public ObservableCollection<FilterColorOption> FilterColorOptions { get; }

    public ObservableCollection<FilterPresetModel> FilterPresets { get; }

    public ObservableCollection<string> SearchHistory { get; }

    public IReadOnlyList<LiveLookbackOption> LiveLookbackOptions => _liveLookbackOptions;

    /// <summary>Returns all loaded log records (for plugin ILogContextProvider).</summary>
    public IReadOnlyList<LogRecord> GetAllLogRecords() => _engine.GetSnapshot();

    public RelayCommand LoadDemoCommand { get; }

    public RelayCommand LoadSampleCommand { get; }

    public RelayCommand OpenFileCommand { get; }

    public RelayCommand DiscoverDevicesCommand { get; }

    public RelayCommand StartLiveCommand { get; }

    public RelayCommand<string> StartLiveWithLookbackCommand { get; }

    public RelayCommand<string> SetLiveLookbackCommand { get; }

    public RelayCommand StopLiveCommand { get; }

    public RelayCommand ExportLogsCommand { get; }

    public RelayCommand ClearLogsCommand { get; }

    public RelayCommand SearchLogsCommand { get; }

    public RelayCommand ClearSearchCommand { get; }

    public RelayCommand ApplyFilterCommand { get; }

    public RelayCommand ResetFiltersCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand DecreaseFontSizeCommand { get; }

    public RelayCommand IncreaseFontSizeCommand { get; }

    public RelayCommand<string> SetAppFontFamilyCommand { get; }

    public RelayCommand SaveCurrentFilterPresetCommand { get; }

    public RelayCommand LoadSelectedFilterPresetCommand { get; }

    public RelayCommand DeleteSelectedFilterPresetCommand { get; }

    public RelayCommand NewFilterSetCommand { get; }

    public RelayCommand OpenFilterSetCommand { get; }

    public RelayCommand SaveFilterSetCommand { get; }

    public RelayCommand SaveFilterSetAsCommand { get; }

    public RelayCommand EditQuickFilterCommand { get; }

    public RelayCommand AddFilterRuleCommand { get; }

    public RelayCommand EditSelectedFilterRuleCommand { get; }

    public RelayCommand MoveSelectedFilterRuleUpCommand { get; }

    public RelayCommand MoveSelectedFilterRuleDownCommand { get; }

    public RelayCommand EnableSelectedFilterRuleCommand { get; }

    public RelayCommand DisableSelectedFilterRuleCommand { get; }

    public RelayCommand<string> RemoveSearchHistoryItemCommand { get; }

    public string WindowTitle
    {
        get => _windowTitle;
        set => SetProperty(ref _windowTitle, value);
    }

    public string AppVersionDisplay => AppMetadata.VersionDisplay;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public string TagFilterText
    {
        get => _tagFilterText;
        set
        {
            if (SetProperty(ref _tagFilterText, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public string PidFilterText
    {
        get => _pidFilterText;
        set
        {
            if (SetProperty(ref _pidFilterText, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public string ExcludedFilterText
    {
        get => _excludedFilterText;
        set
        {
            if (SetProperty(ref _excludedFilterText, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public string ExcludedTagFilterText
    {
        get => _excludedTagFilterText;
        set
        {
            if (SetProperty(ref _excludedTagFilterText, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public string ExcludedPidFilterText
    {
        get => _excludedPidFilterText;
        set
        {
            if (SetProperty(ref _excludedPidFilterText, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public string FilterPresetName
    {
        get => _filterPresetName;
        set => SetProperty(ref _filterPresetName, value);
    }

    public string CurrentFilterSetPath
    {
        get => _currentFilterSetPath;
        private set
        {
            if (SetProperty(ref _currentFilterSetPath, value))
            {
                RaisePropertyChanged(nameof(CurrentFilterSetDisplayName));
            }
        }
    }

    public string CurrentFilterSetDisplayName =>
        string.IsNullOrWhiteSpace(CurrentFilterSetPath) ? "Unsaved filter set" : Path.GetFileName(CurrentFilterSetPath);

    public string BusyMessage
    {
        get => _busyMessage;
        private set => SetProperty(ref _busyMessage, value);
    }

    public bool IsFilterSetDirty
    {
        get => _isFilterSetDirty;
        private set => SetProperty(ref _isFilterSetDirty, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ClearSearchCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SearchStatusText
    {
        get => _searchStatusText;
        private set => SetProperty(ref _searchStatusText, value);
    }

    public IReadOnlyList<string> SearchHighlightTerms
    {
        get => _searchHighlightTerms;
        private set => SetProperty(ref _searchHighlightTerms, value);
    }

    public bool IsSearchPaneVisible
    {
        get => _isSearchPaneVisible;
        private set
        {
            if (SetProperty(ref _isSearchPaneVisible, value))
            {
                ClearSearchCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsAiChatPanelVisible
    {
        get => _isAiChatPanelVisible;
        set => SetProperty(ref _isAiChatPanelVisible, value);
    }

    public bool IsSearchInAllLogs
    {
        get => _isSearchInAllLogs;
        set
        {
            if (SetProperty(ref _isSearchInAllLogs, value))
            {
                SaveUiPreferences();
            }
        }
    }

    public string PreviewMessage
    {
        get => _previewMessage;
        private set => SetProperty(ref _previewMessage, value);
    }

    public string PreviewHeader
    {
        get => _previewHeader;
        private set => SetProperty(ref _previewHeader, value);
    }

    public string QuickFilterSummary
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(FilterText)) parts.Add($"Text: {FilterText}");
            if (!string.IsNullOrWhiteSpace(TagFilterText)) parts.Add($"Tag: {TagFilterText}");
            if (!string.IsNullOrWhiteSpace(PidFilterText)) parts.Add($"PID: {PidFilterText}");
            if (!string.IsNullOrWhiteSpace(ExcludedFilterText)) parts.Add($"Exclude Text: {ExcludedFilterText}");
            if (!string.IsNullOrWhiteSpace(ExcludedTagFilterText)) parts.Add($"Exclude Tag: {ExcludedTagFilterText}");
            if (!string.IsNullOrWhiteSpace(ExcludedPidFilterText)) parts.Add($"Exclude PID: {ExcludedPidFilterText}");

            var levels = GetSelectedLevels();
            if (levels.Count is > 0 and < 6)
            {
                parts.Add($"Levels: {string.Join('/', levels.Select(x => x.ToString()[0]))}");
            }

            return parts.Count == 0 ? "No quick filter. Showing all logs." : string.Join(" | ", parts);
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                LoadDemoCommand.NotifyCanExecuteChanged();
                LoadSampleCommand.NotifyCanExecuteChanged();
                OpenFileCommand.NotifyCanExecuteChanged();
                DiscoverDevicesCommand.NotifyCanExecuteChanged();
                StartLiveCommand.NotifyCanExecuteChanged();
                StartLiveWithLookbackCommand.NotifyCanExecuteChanged();
                StopLiveCommand.NotifyCanExecuteChanged();
                ExportLogsCommand.NotifyCanExecuteChanged();
                ClearLogsCommand.NotifyCanExecuteChanged();
                SearchLogsCommand.NotifyCanExecuteChanged();
                ClearSearchCommand.NotifyCanExecuteChanged();
                ApplyFilterCommand.NotifyCanExecuteChanged();
                ResetFiltersCommand.NotifyCanExecuteChanged();
                ClearCommand.NotifyCanExecuteChanged();
                SaveCurrentFilterPresetCommand.NotifyCanExecuteChanged();
                LoadSelectedFilterPresetCommand.NotifyCanExecuteChanged();
                DeleteSelectedFilterPresetCommand.NotifyCanExecuteChanged();
                NewFilterSetCommand.NotifyCanExecuteChanged();
                OpenFilterSetCommand.NotifyCanExecuteChanged();
                SaveFilterSetCommand.NotifyCanExecuteChanged();
                SaveFilterSetAsCommand.NotifyCanExecuteChanged();
                EditQuickFilterCommand.NotifyCanExecuteChanged();
                AddFilterRuleCommand.NotifyCanExecuteChanged();
                EditSelectedFilterRuleCommand.NotifyCanExecuteChanged();
                MoveSelectedFilterRuleUpCommand.NotifyCanExecuteChanged();
                MoveSelectedFilterRuleDownCommand.NotifyCanExecuteChanged();
                EnableSelectedFilterRuleCommand.NotifyCanExecuteChanged();
                DisableSelectedFilterRuleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsFileLoading
    {
        get => _isFileLoading;
        private set => SetProperty(ref _isFileLoading, value);
    }

    public bool IsLoadOptionVisible
    {
        get => _isLoadOptionVisible;
        private set => SetProperty(ref _isLoadOptionVisible, value);
    }

    /// <summary>Called from UI buttons on the in-app load option overlay.</summary>
    public void SelectLoadOption(LogLoadMode? mode)
    {
        IsLoadOptionVisible = false;
        _loadOptionTcs?.TrySetResult(mode);
    }

    public bool IsLiveSessionRunning
    {
        get => _isLiveSessionRunning;
        set
        {
            if (SetProperty(ref _isLiveSessionRunning, value))
            {
                StartLiveCommand.NotifyCanExecuteChanged();
                StartLiveWithLookbackCommand.NotifyCanExecuteChanged();
                StopLiveCommand.NotifyCanExecuteChanged();
                ClearLogsCommand.NotifyCanExecuteChanged();
                LoadDemoCommand.NotifyCanExecuteChanged();
                LoadSampleCommand.NotifyCanExecuteChanged();
                OpenFileCommand.NotifyCanExecuteChanged();
                DiscoverDevicesCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsWaitingForDevice
    {
        get => _isWaitingForDevice;
        private set
        {
            if (SetProperty(ref _isWaitingForDevice, value))
            {
                StartLiveCommand.NotifyCanExecuteChanged();
                StopLiveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    public LiveLookbackOption SelectedLiveLookbackOption
    {
        get => _selectedLiveLookbackOption;
        set
        {
            if (SetProperty(ref _selectedLiveLookbackOption, value ?? DefaultLiveLookbackOptions[2]))
            {
                RaisePropertyChanged(nameof(IsLiveLookbackLiveOnlySelected));
                RaisePropertyChanged(nameof(IsLiveLookbackLast10SecondsSelected));
                RaisePropertyChanged(nameof(IsLiveLookbackLast30SecondsSelected));
                RaisePropertyChanged(nameof(IsLiveLookbackLast2MinutesSelected));
            }
        }
    }

    public bool IsLiveLookbackLiveOnlySelected => ReferenceEquals(SelectedLiveLookbackOption, DefaultLiveLookbackOptions[0]);

    public bool IsLiveLookbackLast10SecondsSelected => ReferenceEquals(SelectedLiveLookbackOption, DefaultLiveLookbackOptions[1]);

    public bool IsLiveLookbackLast30SecondsSelected => ReferenceEquals(SelectedLiveLookbackOption, DefaultLiveLookbackOptions[2]);

    public bool IsLiveLookbackLast2MinutesSelected => ReferenceEquals(SelectedLiveLookbackOption, DefaultLiveLookbackOptions[3]);

    public bool IsAdbDeviceConnected
    {
        get => _isAdbDeviceConnected;
        private set
        {
            if (SetProperty(ref _isAdbDeviceConnected, value))
            {
                RaisePropertyChanged(nameof(AdbConnectionIndicatorBrush));
            }
        }
    }

    public Brush AdbConnectionIndicatorBrush => IsAdbDeviceConnected ? Brushes.LimeGreen : Brushes.Red;

    public string AdbConnectionStatusText
    {
        get => _adbConnectionStatusText;
        private set => SetProperty(ref _adbConnectionStatusText, value);
    }

    public bool IsVerboseEnabled
    {
        get => _isVerboseEnabled;
        set
        {
            if (SetProperty(ref _isVerboseEnabled, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public bool IsDebugEnabled
    {
        get => _isDebugEnabled;
        set
        {
            if (SetProperty(ref _isDebugEnabled, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public bool IsInfoEnabled
    {
        get => _isInfoEnabled;
        set
        {
            if (SetProperty(ref _isInfoEnabled, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public bool IsWarnEnabled
    {
        get => _isWarnEnabled;
        set
        {
            if (SetProperty(ref _isWarnEnabled, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public bool IsErrorEnabled
    {
        get => _isErrorEnabled;
        set
        {
            if (SetProperty(ref _isErrorEnabled, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public bool IsFatalEnabled
    {
        get => _isFatalEnabled;
        set
        {
            if (SetProperty(ref _isFatalEnabled, value))
            {
                RaisePropertyChanged(nameof(QuickFilterSummary));
            }
        }
    }

    public bool IsLiveAppendPaused
    {
        get => _isLiveAppendPaused;
        set
        {
            if (!SetProperty(ref _isLiveAppendPaused, value))
            {
                return;
            }

            if (!value)
            {
                _ = ResumeLiveAppendAsync();
            }
        }
    }

    public bool IsAutoScrollEnabled
    {
        get => _isAutoScrollEnabled;
        set => SetProperty(ref _isAutoScrollEnabled, value);
    }

    public int PausedAppendCount
    {
        get => _pausedAppendCount;
        private set => SetProperty(ref _pausedAppendCount, value);
    }

    public double LogFontSize
    {
        get => _logFontSize;
        set
        {
            var clamped = Math.Clamp(value, 10, 24);
            if (SetProperty(ref _logFontSize, clamped))
            {
                SaveUiPreferences();
                RaisePropertyChanged(nameof(LogFontSizeLabel));
                DecreaseFontSizeCommand.NotifyCanExecuteChanged();
                IncreaseFontSizeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string LogFontSizeLabel => $"{LogFontSize:0}";

    public string SelectedAppFontFamily
    {
        get => _selectedAppFontFamily;
        set
        {
            var normalized = NormalizeAppFontFamily(value);
            if (SetProperty(ref _selectedAppFontFamily, normalized))
            {
                SaveUiPreferences();
            }
        }
    }

    public LogRowModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                PreviewMessage = value?.Message ?? string.Empty;
                PreviewHeader = value is not null
                    ? $"#{value.RowId}  {value.Timestamp}  [{value.Level}]  Tag={value.Tag}  PID={value.Pid}  TID={value.Tid}"
                    : string.Empty;
            }
        }
    }

    public SearchResultRowModel? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set
        {
            if (!SetProperty(ref _selectedSearchResult, value) || value is null)
            {
                return;
            }

            // Always show the search result message in Preview, even if the matching
            // row is not found in the current (filtered) Records collection.
            PreviewMessage = value.Message ?? string.Empty;
            PreviewHeader = $"#{value.RowId}  {value.Timestamp}  Tag={value.Tag}  PID={value.Pid}";

            // O(1) lookup via RowId map (Phase 3 — 5순위). Previously: Records.FirstOrDefault
            // linear scan up to N rows per search-result click.
            if (_visibleRowsByRowId.TryGetValue(value.RowId, out var matchingRow))
            {
                // Update SelectedRow without overwriting Preview (already set above)
                _selectedRow = matchingRow;
                RaisePropertyChanged(nameof(SelectedRow));
            }
        }
    }

    public FilterColorOption? SelectedFilterColor
    {
        get => _selectedFilterColor;
        set => SetProperty(ref _selectedFilterColor, value);
    }

    public FilterPresetModel? SelectedFilterPreset
    {
        get => _selectedFilterPreset;
        set
        {
            if (SetProperty(ref _selectedFilterPreset, value))
            {
                LoadSelectedFilterPresetCommand.NotifyCanExecuteChanged();
                DeleteSelectedFilterPresetCommand.NotifyCanExecuteChanged();
                EditSelectedFilterRuleCommand.NotifyCanExecuteChanged();
                MoveSelectedFilterRuleUpCommand.NotifyCanExecuteChanged();
                MoveSelectedFilterRuleDownCommand.NotifyCanExecuteChanged();
                NotifySelectedFilterRuleCommandState();
            }
        }
    }

    public double? GetColumnWidthPreference(string key)
    {
        if (string.IsNullOrWhiteSpace(key)
            || !_columnWidthPreferences.TryGetValue(key, out var width)
            || width <= 0)
        {
            return null;
        }

        return width;
    }

    public void SetColumnWidthPreference(string key, double width)
    {
        if (string.IsNullOrWhiteSpace(key)
            || double.IsNaN(width)
            || double.IsInfinity(width)
            || width <= 0)
        {
            return;
        }

        _columnWidthPreferences[key] = width;
    }

    public int? GetColumnDisplayIndexPreference(string key)
    {
        if (string.IsNullOrWhiteSpace(key)
            || !_columnDisplayIndexPreferences.TryGetValue(key, out var displayIndex)
            || displayIndex < 0)
        {
            return null;
        }

        return displayIndex;
    }

    public void SetColumnDisplayIndexPreference(string key, int displayIndex)
    {
        if (string.IsNullOrWhiteSpace(key)
            || displayIndex < 0)
        {
            return;
        }

        _columnDisplayIndexPreferences[key] = displayIndex;
    }

    public void PersistUiPreferences() => SaveUiPreferences();

    public bool AreAllFilterRulesSelected
    {
        get => _isAllFilterRulesSelected;
        set
        {
            if (!SetProperty(ref _isAllFilterRulesSelected, value))
            {
                return;
            }

            SetAllFilterRuleSelection(value);
        }
    }

    public string CheckedFilterRulesSummary
    {
        get
        {
            var checkedCount = FilterPresets.Count(x => x.IsBatchSelected);
            return checkedCount == 0
                ? "No checked rules"
                : $"{checkedCount:n0} checked";
        }
    }

    public async Task InitializeAsync()
    {
        // Clean up spool files older than 24 hours on startup
        _ = Task.Run(() => EasyLog.Engine.Storage.RawSpoolStore.CleanupOldSpoolFiles(TimeSpan.FromHours(24)));

        await LoadFilterPresetsAsync(_filterPresetStore.FilePath).ConfigureAwait(true);
        await TryAutoDiscoverSingleDeviceAsync().ConfigureAwait(true);
        Records.Clear();
        _visibleRowIds.Clear();
        _visibleRowsByRowId.Clear();
        SelectedRow = null;
        _lastOpenedLogFilePath = string.Empty;
        WindowTitle = AppMetadata.FormatWindowTitle();
        StatusText = "Ready. Open Log or Start Live to begin.";
        SearchStatusText = "Search Tag / Message / PID. Use | for OR and & for AND.";
    }

    private async Task LoadDemoAsync()
    {
        try
        {
            BusyMessage = "Loading embedded demo logs...";
            IsBusy = true;
            ClearSearch();
            ResetLiveInteractionState();
            Devices.Clear();
            SelectedDevice = null;
            var result = await _engine.LoadDemoAsync();
            ReplaceRecords(result.Records);
            StatusText = $"{result.State.StatusMessage} (embedded demo logs)";
            WindowTitle = AppMetadata.FormatWindowTitle("Demo Logs");
        }
        catch (Exception ex)
        {
            StatusText = $"Demo load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSampleAsync()
    {
        try
        {
            BusyMessage = "Loading sample logs...";
            IsBusy = true;
            ClearSearch();
            ResetLiveInteractionState();
            var samplePath = ResolveSampleLogPath();
            var result = await _engine.LoadFileAsync(samplePath);
            ReplaceRecords(result.Records);
            StatusText = $"{result.State.StatusMessage} ({Path.GetFileName(samplePath)})";
            WindowTitle = AppMetadata.FormatWindowTitle(Path.GetFileName(samplePath));
            UpdateLastOpenedLogFilePath(samplePath);
        }
        catch (Exception ex)
        {
            StatusText = $"Sample load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> LoadFileAsync(string filePath)
    {
        // 기본값: 전체 로딩
        return await LoadFilesAsync(new[] { filePath }, LogLoadMode.Full).ConfigureAwait(true);
    }

    private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open log file(s) or archive(s)",
            Filter = "Log & Archive files|*.log;*.logcat;*.txt;*.zip;*.7z|Log files|*.log;*.logcat;*.txt|Archives|*.zip;*.7z|All files|*.*",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (IsLiveSessionRunning)
        {
            await StopLiveAsync().ConfigureAwait(true);
        }
        var loadMode = await ShowLogLoadOptionDialogAsync().ConfigureAwait(true);
        if (loadMode == null)
            return;
        await LoadFilesAsync(dialog.FileNames.ToList(), loadMode.Value);
    }

    public async Task OpenDroppedFilesAsync(IReadOnlyList<string> filePaths)
    {
        if (IsLiveSessionRunning)
        {
            await StopLiveAsync().ConfigureAwait(true);
        }
        var loadMode = await ShowLogLoadOptionDialogAsync().ConfigureAwait(true);
        if (loadMode == null)
            return;
        await LoadFilesAsync(filePaths, loadMode.Value).ConfigureAwait(true);
    }

    private async Task<bool> LoadFilesAsync(IReadOnlyList<string> filePaths, LogLoadMode loadMode)
    {
        var tempDirs = new List<string>();
        _fileLoadCts?.Cancel();
        _fileLoadCts = new CancellationTokenSource();
        var ct = _fileLoadCts.Token;
        try
        {
            BusyMessage = "Preparing files...";
            IsBusy = true;
            IsFileLoading = true;
            ClearSearch();
            ResetLiveInteractionState();
            await Task.Yield();

            // Resolve archives and plain log files into a flat list of log file paths
            var resolvedLogFiles = new List<string>();
            var failedArchives = new List<(string FileName, string Error)>();
            for (var i = 0; i < filePaths.Count; i++)
            {
                var fp = filePaths[i];
                if (ArchiveExtractor.IsArchive(fp))
                {
                    BusyMessage = $"Extracting archive ({i + 1}/{filePaths.Count}): {Path.GetFileName(fp)}...";
                    await Task.Yield();
                    try
                    {
                        var extraction = await ArchiveExtractor.ExtractAsync(fp).ConfigureAwait(true);
                        tempDirs.Add(extraction.TempDirectory);
                        resolvedLogFiles.AddRange(extraction.LogFilePaths);
                    }
                    catch (Exception archiveEx)
                    {
                        failedArchives.Add((Path.GetFileName(fp), FormatExceptionMessage(archiveEx)));
                    }
                }
                else
                {
                    resolvedLogFiles.Add(fp);
                }
            }

            if (resolvedLogFiles.Count == 0)
            {
                if (failedArchives.Count > 0)
                {
                    StatusText = $"All {failedArchives.Count} archive(s) failed to extract: "
                        + string.Join("; ", failedArchives.Select(f => $"{f.FileName} ({f.Error})"));
                }
                else
                {
                    StatusText = "No log files found in the selected file(s).";
                }
                return false;
            }

            // Sort by natural file name order for split log files
            resolvedLogFiles.Sort(StringComparer.OrdinalIgnoreCase);

            BusyMessage = resolvedLogFiles.Count == 1
                ? $"Loading log file: {Path.GetFileName(resolvedLogFiles[0])}"
                : $"Loading {resolvedLogFiles.Count} log files...";
            await Task.Yield();

            InvalidateEnabledFilterRulesCache();
            var enabledRules = GetEnabledFilterRules();
            var useProgressiveRendering = loadMode == LogLoadMode.Full
                && resolvedLogFiles.Count == 1
                && enabledRules.Count == 0;

            _isProgressiveFileRendering = useProgressiveRendering;
            if (useProgressiveRendering)
            {
                DrainPendingLiveRecords();
                _visibleRowIds.Clear();
                _visibleRowsByRowId.Clear();
                Records.Clear();
                SelectedRow = null;
                StatusText = "Loading log file... showing first records as they are parsed.";
            }

            var result = await _engine.LoadMultipleFilesAsync(resolvedLogFiles, cancellationToken: ct).ConfigureAwait(true);

            IReadOnlyList<LogRecord> recordsToRender;
            bool isFiltered = false;
            if (loadMode == LogLoadMode.Full)
            {
                recordsToRender = result.Records;
            }
            else // LogLoadMode.Filtered
            {
                // 1. 현재 필터 조건
                var filterQuery = BuildCurrentFilterQuery();
                // 2. 필터 조건에 맞는 로그
                var filtered = _engine.ApplyFilter(filterQuery);
                // 3. 치명적 로그 판별 (CRASH, ANR, NativeCrash, Watchdog)
                var critical = result.Records.Where(r =>
                {
                    var flags = _engine.GetDiagnostics(r);
                    return flags.HasFlag(EasyLog.Engine.Diagnostics.DiagnosticFlags.Crash)
                        || flags.HasFlag(EasyLog.Engine.Diagnostics.DiagnosticFlags.Anr)
                        || flags.HasFlag(EasyLog.Engine.Diagnostics.DiagnosticFlags.NativeCrash)
                        || flags.HasFlag(EasyLog.Engine.Diagnostics.DiagnosticFlags.Watchdog);
                });
                // 4. 병합 및 중복 제거
                var merged = filtered.Concat(critical).Distinct().ToList();
                recordsToRender = merged;
                isFiltered = true;
            }

            if (enabledRules.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                BusyMessage = "Applying filter rules...";
                await Task.Yield();
                recordsToRender = await Task.Run(() => ApplyEnabledFilterRules(recordsToRender, enabledRules), ct).ConfigureAwait(true);
                isFiltered = true;
            }

            BusyMessage = $"Rendering {recordsToRender.Count:n0} records...";
            _isProgressiveFileRendering = false;
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            await ReplaceRecordsAsync(recordsToRender, ct).ConfigureAwait(true);

            var displayName = filePaths.Count == 1
                ? Path.GetFileName(filePaths[0])
                : $"{filePaths.Count} files";
            if (isFiltered)
            {
                StatusText = $"필터링된 로그 {recordsToRender.Count:n0}건 ({displayName})";
            }
            else
            {
                StatusText = $"전체 로그 {recordsToRender.Count:n0}건 ({displayName})";
            }

            // 아카이브 추출 실패 경고 표시
            if (failedArchives.Count > 0)
            {
                StatusText += $" | {failedArchives.Count} archive(s) skipped: {string.Join(", ", failedArchives.Select(f => f.FileName))}";
            }
            WindowTitle = AppMetadata.FormatWindowTitle(displayName);
            UpdateLastOpenedLogFilePath(filePaths[0]);
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusText = "File loading cancelled.";
            return false;
        }
        catch (Exception ex)
        {
            StatusText = $"File load failed: {FormatExceptionMessage(ex)}";
            return false;
        }
        finally
        {
            IsFileLoading = false;
            IsBusy = false;
            _isProgressiveFileRendering = false;
            // Cleanup extracted temp directories
            foreach (var tempDir in tempDirs)
            {
                ArchiveExtractor.CleanupTempDirectory(tempDir);
            }
        }
    }

    /// <summary>
    /// 로그 로딩 옵션 선택 팝업을 띄우고, 사용자가 선택한 옵션을 반환합니다.
    /// Full: 전체 로그 로딩, Filtered: 필터 적용 후 로딩, null: 취소
    /// </summary>
    private async Task<LogLoadMode?> ShowLogLoadOptionDialogAsync()
    {
        _loadOptionTcs = new TaskCompletionSource<LogLoadMode?>();
        IsLoadOptionVisible = true;
        var result = await _loadOptionTcs.Task.ConfigureAwait(true);
        _loadOptionTcs = null;
        return result;
    }

    private async Task DiscoverDevicesAsync()
    {
        try
        {
            // 수동 탐색 시 circuit breaker 리셋 및 자동 폴링 재개
            _adbConsecutiveFailureCount = 0;
            if (!_deviceMonitorTimer.IsEnabled)
            {
                _deviceMonitorTimer.Start();
            }

            BusyMessage = "Discovering adb devices...";
            IsBusy = true;
            var devices = await RefreshDevicesAsync().ConfigureAwait(true);
            StatusText = devices.Count == 0
                ? "No adb devices found. Connect a device and enable USB debugging."
                : $"Discovered {devices.Count} device(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Device discovery failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StartLiveAsync()
    {
        try
        {
            BusyMessage = "Starting live adb session...";
            IsBusy = true;
            await EnsureReadyDeviceSelectedAsync().ConfigureAwait(true);

            // If no ready device found, enter "waiting for device" mode
            if (SelectedDevice is null || !string.Equals(SelectedDevice.State, "device", StringComparison.OrdinalIgnoreCase))
            {
                IsBusy = false;
                await WaitForDeviceAndStartLiveAsync().ConfigureAwait(true);
                return;
            }

            await StartLiveSessionCoreAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to start live session: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StartLiveWithLookbackAsync(string? optionKey)
    {
        SetLiveLookbackOption(optionKey);
        await StartLiveAsync().ConfigureAwait(true);
    }

    private void SetLiveLookbackOption(string? optionKey)
    {
        SelectedLiveLookbackOption = ResolveLiveLookbackOption(optionKey);
    }

    private static LiveLookbackOption ResolveLiveLookbackOption(string? optionKey) => optionKey switch
    {
        "live" => DefaultLiveLookbackOptions[0],
        "10s" => DefaultLiveLookbackOptions[1],
        "30s" => DefaultLiveLookbackOptions[2],
        "2m" => DefaultLiveLookbackOptions[3],
        _ => DefaultLiveLookbackOptions[2]
    };

    /// <summary>
    /// Enters "waiting for device" mode. The existing DeviceMonitor timer (4s interval)
    /// will detect the device and automatically start the live session.
    /// Timeout: 60 seconds. User can cancel via Stop Live button.
    /// </summary>
    private async Task WaitForDeviceAndStartLiveAsync()
    {
        const int timeoutSeconds = 60;
        CancelWaitForDevice();
        _waitForDeviceCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        IsWaitingForDevice = true;
        StatusText = $"Waiting for adb device... (auto-start when connected, timeout {timeoutSeconds}s)";

        try
        {
            var ct = _waitForDeviceCts.Token;
            // Poll using the existing device monitor interval until a ready device appears
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(true);

                var devices = await _engine.DiscoverDevicesAsync(ct).ConfigureAwait(true);
                ApplyDiscoveredDevices(devices);

                var readyDevice = devices.FirstOrDefault(static x =>
                    string.Equals(x.State, "device", StringComparison.OrdinalIgnoreCase));

                if (readyDevice is not null)
                {
                    SelectedDevice = readyDevice;
                    IsWaitingForDevice = false;
                    StatusText = $"Device detected: {readyDevice}. Starting live session...";

                    try
                    {
                        BusyMessage = "Starting live adb session...";
                        IsBusy = true;
                        await StartLiveSessionCoreAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                    return;
                }

                StatusText = "Waiting for adb device... (will auto-start when connected)";
            }
        }
        catch (OperationCanceledException)
        {
            // User cancelled or timeout
        }
        finally
        {
            var wasTimeout = _waitForDeviceCts?.Token.IsCancellationRequested == true;
            IsWaitingForDevice = false;
            _waitForDeviceCts?.Dispose();
            _waitForDeviceCts = null;
            if (wasTimeout && !IsLiveSessionRunning)
            {
                StatusText = "Device wait timed out. Click Start Live to try again.";
            }
        }
    }

    private void CancelWaitForDevice()
    {
        _waitForDeviceCts?.Cancel();
        _waitForDeviceCts?.Dispose();
        _waitForDeviceCts = null;
        IsWaitingForDevice = false;
    }

    private async Task StartLiveSessionCoreAsync()
    {
        Records.Clear();
        _visibleRowIds.Clear();
        _visibleRowsByRowId.Clear();
        SelectedRow = null;
        _activeFilterQuery = BuildCurrentFilterQuery();
        IsLiveAppendPaused = false;
        PausedAppendCount = 0;
        IsAutoScrollEnabled = true;
        var lookbackOption = SelectedLiveLookbackOption;
        await _engine.StartLiveSessionAsync(SelectedDevice, lookbackWindow: lookbackOption.LookbackWindow);
        IsLiveSessionRunning = true;
        WindowTitle = SelectedDevice is null
            ? AppMetadata.FormatWindowTitle("ADB Live Session")
            : AppMetadata.FormatWindowTitle($"Live [{SelectedDevice.Serial}]");
        StatusText = $"{_engine.CurrentSession.StatusMessage} ({lookbackOption.StatusSuffix})";
    }

    private async Task ExportLogsAsync()
    {
        var snapshot = _engine.GetSnapshot();
        var lastRecordTimestamp = snapshot.Count > 0
            ? snapshot[^1].Timestamp
            : DateTimeOffset.Now;

        var dialog = new SaveFileDialog
        {
            Title = "Export monitored logs archive",
            Filter = "7z archive|*.7z|All files|*.*",
            FileName = $"{AppMetadata.ShortProductName}-{lastRecordTimestamp:yyyyMMdd-HHmmss}.7z",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            BusyMessage = "Exporting compressed log archive...";
            IsBusy = true;
            await _engine.ExportAsync(dialog.FileName).ConfigureAwait(true);
            StatusText = $"Exported compressed logs to '{Path.GetFileName(dialog.FileName)}'.";
        }
        catch (Exception ex)
        {
            StatusText = $"Log export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ClearLogsAsync()
    {
        DrainPendingLiveRecords();
        _visibleRowIds.Clear();
        _visibleRowsByRowId.Clear();
        Records.Clear();
        SearchResults.Clear();
        SelectedRow = null;
        SelectedSearchResult = null;
        Interlocked.Exchange(ref _queuedPausedAppendCount, 0);
        PausedAppendCount = 0;
        _engine.Clear(keepSessionMetadata: IsLiveSessionRunning);
        StatusText = IsLiveSessionRunning
            ? "Cleared current logs. Live session will continue collecting new entries."
            : "Cleared current logs.";
        ClearLogsCommand.NotifyCanExecuteChanged();
        return Task.CompletedTask;
    }

    private async Task SearchLogsAsync()
    {
        // Cancel any in-flight search
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        try
        {
            BusyMessage = "Searching logs...";
            IsBusy = true;
            await Task.Yield(); // Allow UI to render the busy overlay

            var ct = cts.Token;
            ct.ThrowIfCancellationRequested();

            var query = SearchText.Trim();
            var searchExpression = SearchExpression.Parse(query);
            SelectedSearchResult = null;

            if (searchExpression.IsEmpty)
            {
                IsSearchPaneVisible = true;
                SearchHighlightTerms = Array.Empty<string>();
                SearchStatusText = "Search Tag / Message / PID. Use | for OR and & for AND.";
                SearchResults.ReplaceRange(Array.Empty<SearchResultRowModel>());
                StatusText = "Enter at least one valid search term.";
                return;
            }

            SearchHighlightTerms = searchExpression.Terms;

            var highlightRules = GetEnabledFilterRules();

            // Pre-warm brush cache on UI thread (BrushConverter requires UI thread)
            foreach (var rule in highlightRules)
            {
                GetHighlightBrushPair(rule.Preset.ColorHex);
            }

            ct.ThrowIfCancellationRequested();

            // Run heavy search work on background thread
            var results = await Task.Run(() =>
            {
                var snapshot = _engine.GetSnapshot();
                IReadOnlyList<LogRecord> searchSource;
                if (IsSearchInAllLogs || highlightRules.Count == 0)
                {
                    searchSource = snapshot;
                }
                else
                {
                    searchSource = ApplyEnabledFilterRules(snapshot, highlightRules);
                }

                ct.ThrowIfCancellationRequested();

                return searchSource
                    .Where(record =>
                    {
                        ct.ThrowIfCancellationRequested();
                        return MatchesSearchExpression(record, searchExpression);
                    })
                    .Select(record =>
                    {
                        var brushes = ResolveHighlightBrushes(record, highlightRules);
                        return SearchResultRowModel.From(record, brushes.Background, brushes.Foreground);
                    })
                    .ToArray();
            }, ct).ConfigureAwait(true);

            ct.ThrowIfCancellationRequested();

            SearchResults.ReplaceRange(results);

            IsSearchPaneVisible = true;
            SearchStatusText = results.Length == 0
                ? $"No search results for '{query}'."
                : $"{results.Length:n0} result(s) for '{query}'. Select a row to jump in the log view.";
            StatusText = results.Length == 0
                ? $"No search results for '{query}'."
                : $"Search found {results.Length:n0} result(s) for '{query}'.";

            AddSearchHistoryItem(query);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            // Search was superseded by a newer search — silently ignore
        }
        finally
        {
            // Only clear IsBusy if this is still the active search
            if (_searchCts == cts)
            {
                IsBusy = false;
            }
        }
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        SearchHighlightTerms = Array.Empty<string>();
        SearchResults.ReplaceRange(Array.Empty<SearchResultRowModel>());
        SelectedSearchResult = null;
        IsSearchPaneVisible = true;
        SearchStatusText = "Search Tag / Message / PID. Use | for OR and & for AND.";
        StatusText = "Search cleared.";
    }

    private void AddSearchHistoryItem(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        // Remove existing duplicate (case-insensitive) to move it to the top
        for (var i = SearchHistory.Count - 1; i >= 0; i--)
        {
            if (string.Equals(SearchHistory[i], query, StringComparison.OrdinalIgnoreCase))
            {
                SearchHistory.RemoveAt(i);
            }
        }

        SearchHistory.Insert(0, query);

        // Trim to max history count
        while (SearchHistory.Count > MaxSearchHistoryCount)
        {
            SearchHistory.RemoveAt(SearchHistory.Count - 1);
        }

        SaveUiPreferences();
    }

    private void RemoveSearchHistoryItem(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        for (var i = SearchHistory.Count - 1; i >= 0; i--)
        {
            if (string.Equals(SearchHistory[i], query, StringComparison.OrdinalIgnoreCase))
            {
                SearchHistory.RemoveAt(i);
            }
        }

        SaveUiPreferences();
    }

    private async Task StopLiveAsync()
    {
        // Cancel device wait if in progress
        if (IsWaitingForDevice)
        {
            CancelWaitForDevice();
            StatusText = "Device wait cancelled.";
            return;
        }

        try
        {
            IsBusy = true;
            await _engine.StopLiveSessionAsync();
            if (PausedAppendCount > 0)
            {
                await RefreshRecordsFromEngineAsync().ConfigureAwait(true);
            }

            IsLiveAppendPaused = false;
            PausedAppendCount = 0;
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to stop live session: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsLiveSessionRunning = false;
            await RefreshDevicesSilentlyAsync().ConfigureAwait(true);
        }
    }

    private async Task ApplyFilterAsync()
    {
        try
        {
            IsBusy = true;
            _activeFilterQuery = BuildCurrentFilterQuery();
            await RefreshRecordsFromEngineAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = $"Filter apply failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Clear()
    {
        _engine.Clear();
        Records.Clear();
        ApplyDiscoveredDevices(Array.Empty<DeviceInfo>());
        SelectedRow = null;
        ResetFilters();
        ResetLiveInteractionState();
        WindowTitle = AppMetadata.FormatWindowTitle();
        StatusText = "Cleared";
    }

    private void DecreaseFontSize() => LogFontSize -= 1;

    private void IncreaseFontSize() => LogFontSize += 1;

    private void ResetFilters()
    {
        FilterText = string.Empty;
        TagFilterText = string.Empty;
        PidFilterText = string.Empty;
        ExcludedFilterText = string.Empty;
        ExcludedTagFilterText = string.Empty;
        ExcludedPidFilterText = string.Empty;
        IsVerboseEnabled = true;
        IsDebugEnabled = true;
        IsInfoEnabled = true;
        IsWarnEnabled = true;
        IsErrorEnabled = true;
        IsFatalEnabled = true;
        RaisePropertyChanged(nameof(QuickFilterSummary));
    }

    private void ReplaceRecords(IReadOnlyList<LogRecord> records)
    {
        DrainPendingLiveRecords();
        _visibleRowIds.Clear();
        _visibleRowsByRowId.Clear();
        var highlightRules = GetEnabledFilterRules();
        var rows = new List<LogRowModel>(records.Count);
        foreach (var record in records)
        {
            var row = CreateLogRowModel(record, highlightRules);
            _visibleRowIds.Add(record.RowId);
            _visibleRowsByRowId[record.RowId] = row;
            rows.Add(row);
        }

        Records.ReplaceRange(rows);

        SelectedRow = null;
    }

    private async Task ReplaceRecordsAsync(IReadOnlyList<LogRecord> records, CancellationToken ct = default)
    {
        DrainPendingLiveRecords();
        var highlightRules = GetEnabledFilterRules();

        // Pre-warm brush cache on UI thread (BrushConverter requires UI thread)
        foreach (var rule in highlightRules)
        {
            GetHighlightBrushPair(rule.Preset.ColorHex);
        }

        ct.ThrowIfCancellationRequested();

        // Build LogRowModel list on background thread(s) — parallelized for large datasets
        var (rows, rowIds, rowsByRowId) = await Task.Run(() =>
        {
            var count = records.Count;
            var list = new LogRowModel[count];
            var ids = new HashSet<long>(count);
            var byRowId = new Dictionary<long, LogRowModel>(count);

            if (count > 50_000 && highlightRules.Count == 0)
            {
                Parallel.For(0, count, new ParallelOptions { CancellationToken = ct }, i =>
                {
                    list[i] = LogRowModel.From(records[i], string.Empty);
                });
            }
            else if (count > 50_000)
            {
                Parallel.For(0, count, new ParallelOptions { CancellationToken = ct }, i =>
                {
                    list[i] = CreateLogRowModelFast(records[i], highlightRules);
                });
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    if (i % 10_000 == 0) ct.ThrowIfCancellationRequested();
                    list[i] = CreateLogRowModelFast(records[i], highlightRules);
                }
            }

            ct.ThrowIfCancellationRequested();

            // Build visible row IDs set + lookup map on background thread (Phase 3 — 5순위)
            foreach (var row in list)
            {
                ids.Add(row.RowId);
                byRowId[row.RowId] = row;
            }

            return (list, ids, byRowId);
        }).ConfigureAwait(true);

        _visibleRowIds = rowIds;
        _visibleRowsByRowId = rowsByRowId;
        Records.ReplaceRange(rows);
        SelectedRow = null;
    }

    private async Task RefreshRecordsFromEngineAsync(CancellationToken ct = default)
    {
        // Cancel any in-flight filter apply. Mirrors the search cancellation pattern (Prompt §11):
        // a newer filter change supersedes older work, IsBusy is only cleared by the active task,
        // and final UI mutation (ReplaceRecords) only runs when this task is still authoritative.
        _filterApplyCts?.Cancel();
        _filterApplyCts?.Dispose();
        var cts = ct.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : new CancellationTokenSource();
        _filterApplyCts = cts;
        var linkedToken = cts.Token;

        var wasBusy = IsBusy;
        if (!wasBusy)
        {
            BusyMessage = "Applying filters...";
            IsBusy = true;
            await Task.Yield();
        }

        try
        {
            linkedToken.ThrowIfCancellationRequested();
            var filterRules = GetEnabledFilterRules();
            var snapshot = await Task.Run(
                () => IsLiveSessionRunning ? _engine.GetSnapshot() : _engine.GetSnapshotView(), linkedToken).ConfigureAwait(true);
            linkedToken.ThrowIfCancellationRequested();

            var activeQuery = _activeFilterQuery;

            // Single-pass combined filter (Phase 2 — 2순위): apply QuickFilter and enabled Filter Rules
            // in one loop. Replaces the previous two-pass (`_engine.ApplyFilter` → `ApplyEnabledFilterRules`)
            // which allocated an intermediate baseRecords list and iterated the data twice.
            var filtered = await Task.Run(
                () => ApplyCombinedFilters(snapshot, activeQuery, filterRules), linkedToken).ConfigureAwait(true);
            linkedToken.ThrowIfCancellationRequested();

            // Generation guard: a newer refresh may have started while we awaited. Skip UI mutation
            // if we are no longer the active task — the newer task will write its own results.
            if (!ReferenceEquals(_filterApplyCts, cts))
            {
                return;
            }

            ReplaceRecords(filtered);
            Interlocked.Exchange(ref _queuedPausedAppendCount, 0);
            PausedAppendCount = 0;
            var hasAnyFilter = !activeQuery.IsEmpty || filterRules.Count > 0;
            StatusText = !hasAnyFilter
                ? _engine.CurrentSession.StatusMessage
                : $"Showing {filtered.Count:n0} / {snapshot.Count:n0}";
        }
        catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
        {
            // Filter apply was superseded — silently ignore.
        }
        finally
        {
            // Only clear IsBusy if this is still the active refresh.
            if (ReferenceEquals(_filterApplyCts, cts))
            {
                if (!wasBusy)
                {
                    IsBusy = false;
                }
            }
        }
    }

    /// <summary>
    /// Fire-and-forget wrapper for RefreshRecordsFromEngineAsync that catches and displays errors.
    /// </summary>
    private async Task SafeRefreshRecordsAsync()
    {
        try
        {
            await RefreshRecordsFromEngineAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = $"Filter apply failed: {FormatExceptionMessage(ex)}";
        }
    }

    private async Task ResumeLiveAppendAsync()
    {
        if (_isResumingPausedAppends)
        {
            return;
        }

        if (!IsLiveSessionRunning || PausedAppendCount == 0)
        {
            PausedAppendCount = 0;
            return;
        }

        _isResumingPausedAppends = true;
        try
        {
            await RefreshRecordsFromEngineAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = $"Resume failed: {ex.Message}";
        }
        finally
        {
            _isResumingPausedAppends = false;
        }
    }

    private void ResetLiveInteractionState()
    {
        IsLiveSessionRunning = false;
        IsLiveAppendPaused = false;
        DrainPendingLiveRecords();
        Interlocked.Exchange(ref _queuedPausedAppendCount, 0);
        PausedAppendCount = 0;
        IsAutoScrollEnabled = true;
    }

    private void DrainPendingLiveRecords()
    {
        while (_pendingLiveRecords.TryDequeue(out _))
        {
        }
    }

    private async Task EnsureReadyDeviceSelectedAsync()
    {
        if (SelectedDevice is not null)
        {
            return;
        }

        var devices = await RefreshDevicesAsync().ConfigureAwait(true);
        if (devices.Count == 0)
        {
            StatusText = "No adb devices found. Connect a device and click Discover Devices again.";
            return;
        }

        SelectedDevice = devices.FirstOrDefault(static x => string.Equals(x.State, "device", StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault();
    }

    private async Task<IReadOnlyList<DeviceInfo>> RefreshDevicesAsync()
    {
        var devices = await _engine.DiscoverDevicesAsync().ConfigureAwait(true);
        ApplyDiscoveredDevices(devices);
        return devices;
    }

    private async Task SaveCurrentFilterPresetAsync()
    {
        try
        {
            IsBusy = true;

            var presetName = FilterPresetName.Trim();
            if (string.IsNullOrWhiteSpace(presetName))
            {
                StatusText = "필터를 저장하려면 Preset Name을 입력하세요.";
                return;
            }

            var target = FilterPresets.FirstOrDefault(x => string.Equals(x.Name, presetName, StringComparison.OrdinalIgnoreCase));
            var isNew = target is null;
            if (target is null)
            {
                target = new FilterPresetModel();
                AttachFilterPreset(target);
                FilterPresets.Add(target);
                RaiseFilterRuleSelectionStateChanged();
            }

            _isApplyingPresetState = true;
            try
            {
                target.Name = presetName;
                target.TagFilterText = TagFilterText;
                target.PidFilterText = PidFilterText;
                target.TextFilterText = FilterText;
                target.ExcludedTagFilterText = ExcludedTagFilterText;
                target.ExcludedPidFilterText = ExcludedPidFilterText;
                target.ExcludedTextFilterText = ExcludedFilterText;
                target.IsVerboseEnabled = IsVerboseEnabled;
                target.IsDebugEnabled = IsDebugEnabled;
                target.IsInfoEnabled = IsInfoEnabled;
                target.IsWarnEnabled = IsWarnEnabled;
                target.IsErrorEnabled = IsErrorEnabled;
                target.IsFatalEnabled = IsFatalEnabled;
                target.ColorHex = SelectedFilterColor?.BackgroundHex ?? "#1D4ED8";
                target.IsEnabled = true;
            }
            finally
            {
                _isApplyingPresetState = false;
            }

            InvalidateEnabledFilterRulesCache();
            SelectedFilterPreset = target;
            await RefreshRecordsFromEngineAsync().ConfigureAwait(true);
            MarkFilterSetDirty(true);
            StatusText = isNew
                ? $"Saved filter preset '{presetName}'."
                : $"Updated filter preset '{presetName}'.";
        }
        catch (Exception ex)
        {
            StatusText = $"Filter preset save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSelectedFilterPresetAsync()
    {
        if (SelectedFilterPreset is null)
        {
            StatusText = "로드할 필터 프리셋을 선택하세요.";
            return;
        }

        try
        {
            IsBusy = true;
            ApplyPresetToCurrentFilter(SelectedFilterPreset);
            await ApplyFilterAsync().ConfigureAwait(true);
            StatusText = $"Loaded filter preset '{SelectedFilterPreset.Name}'.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteSelectedFilterPresetAsync()
    {
        if (SelectedFilterPreset is null)
        {
            StatusText = "삭제할 필터 프리셋을 선택하세요.";
            return;
        }

        try
        {
            IsBusy = true;
            var removedName = SelectedFilterPreset.Name;
            var removedIndex = FilterPresets.IndexOf(SelectedFilterPreset);
            var target = SelectedFilterPreset;

            DetachFilterPreset(target);
            FilterPresets.Remove(target);

            InvalidateEnabledFilterRulesCache();
            SelectedFilterPreset = FilterPresets.ElementAtOrDefault(Math.Clamp(removedIndex, 0, Math.Max(FilterPresets.Count - 1, 0)))
                ?? FilterPresets.LastOrDefault();
            RaiseFilterRuleSelectionStateChanged();
            await RefreshRecordsFromEngineAsync().ConfigureAwait(true);
            MarkFilterSetDirty(true);
            StatusText = $"Deleted filter preset '{removedName}'.";
        }
        catch (Exception ex)
        {
            StatusText = $"Filter preset delete failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task NewFilterSetAsync()
    {
        if (!await ConfirmDiscardOrSaveCurrentFilterSetAsync().ConfigureAwait(true))
        {
            return;
        }

        try
        {
            BusyMessage = "Creating new filter set...";
            IsBusy = true;
            await LoadFilterPresetsAsync(null, clearOnly: true).ConfigureAwait(true);
            CurrentFilterSetPath = string.Empty;
            MarkFilterSetDirty(false);
            StatusText = "Started a new filter set. Save As to create a file.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenFilterSetAsync()
    {
        if (!await ConfirmDiscardOrSaveCurrentFilterSetAsync().ConfigureAwait(true))
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Open filter set",
            Filter = "ALV filter set|*.json|All files|*.*",
            InitialDirectory = _filterPresetStore.DefaultDirectory,
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            BusyMessage = "Opening filter set...";
            IsBusy = true;
            await LoadFilterPresetsAsync(dialog.FileName).ConfigureAwait(true);
            MarkFilterSetDirty(false);
            StatusText = $"Loaded filter set '{Path.GetFileName(dialog.FileName)}'.";
        }
        catch (Exception ex)
        {
            StatusText = $"Filter set open failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveFilterSetAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentFilterSetPath))
        {
            await SaveFilterSetAsAsync().ConfigureAwait(true);
            return;
        }

        try
        {
            BusyMessage = "Saving filter set...";
            IsBusy = true;
            await _filterPresetStore.SaveAsync(FilterPresets, CurrentFilterSetPath).ConfigureAwait(true);
            MarkFilterSetDirty(false);
            StatusText = $"Saved filter set to '{Path.GetFileName(CurrentFilterSetPath)}'.";
        }
        catch (Exception ex)
        {
            StatusText = $"Filter set save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveFilterSetAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save filter set",
            Filter = "LogPilot filter set|*.json|All files|*.*",
            InitialDirectory = _filterPresetStore.DefaultDirectory,
            FileName = string.IsNullOrWhiteSpace(CurrentFilterSetPath) ? "filters.json" : Path.GetFileName(CurrentFilterSetPath),
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            BusyMessage = "Saving filter set...";
            IsBusy = true;
            await _filterPresetStore.SaveAsync(FilterPresets, dialog.FileName).ConfigureAwait(true);
            CurrentFilterSetPath = dialog.FileName;
            MarkFilterSetDirty(false);
            StatusText = $"Saved filter set as '{Path.GetFileName(dialog.FileName)}'.";
        }
        catch (Exception ex)
        {
            StatusText = $"Filter set save-as failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyPresetToCurrentFilter(FilterPresetModel preset)
    {
        FilterPresetName = preset.Name;
        TagFilterText = preset.TagFilterText;
        PidFilterText = preset.PidFilterText;
        FilterText = preset.TextFilterText;
        ExcludedTagFilterText = preset.ExcludedTagFilterText;
        ExcludedPidFilterText = preset.ExcludedPidFilterText;
        ExcludedFilterText = preset.ExcludedTextFilterText;
        IsVerboseEnabled = preset.IsVerboseEnabled;
        IsDebugEnabled = preset.IsDebugEnabled;
        IsInfoEnabled = preset.IsInfoEnabled;
        IsWarnEnabled = preset.IsWarnEnabled;
        IsErrorEnabled = preset.IsErrorEnabled;
        IsFatalEnabled = preset.IsFatalEnabled;
        SelectedFilterColor = FilterColorOptions.FirstOrDefault(x => string.Equals(x.BackgroundHex, preset.ColorHex, StringComparison.OrdinalIgnoreCase))
            ?? FilterColorOptions.FirstOrDefault();
        RaisePropertyChanged(nameof(QuickFilterSummary));
    }

    private async Task EditQuickFilterAsync()
    {
        var quickPreset = CreateQuickFilterPreset();
        var dialog = new FilterEditorWindow("Quick Filter", quickPreset, FilterColorOptions, showNameField: false, showColorField: false)
        {
            Owner = Application.Current?.MainWindow
        };

        _isFilterEditorOpen = true;
        try
        {
            if (dialog.ShowDialog() != true)
            {
                return;
            }
        }
        finally
        {
            _isFilterEditorOpen = false;
        }

        ApplyPresetToCurrentFilter(dialog.GetResult());
        await ApplyFilterAsync().ConfigureAwait(true);
    }

    private async Task AddFilterRuleAsync()
    {
        var preset = new FilterPresetModel
        {
            Name = $"Rule {FilterPresets.Count + 1}",
            ColorHex = SelectedFilterColor?.BackgroundHex ?? "#4E79A7"
        };

        var dialog = new FilterEditorWindow("Add Filter Rule", preset, FilterColorOptions, showNameField: true, showColorField: true)
        {
            Owner = Application.Current?.MainWindow
        };

        _isFilterEditorOpen = true;
        try
        {
            if (dialog.ShowDialog() != true)
            {
                return;
            }
        }
        finally
        {
            _isFilterEditorOpen = false;
        }

        var result = dialog.GetResult();
        result.IsBatchSelected = true;
        AttachFilterPreset(result);
        FilterPresets.Add(result);
        RaiseFilterRuleSelectionStateChanged();
        SelectedFilterPreset = result;
        InvalidateEnabledFilterRulesCache();
        await RefreshRecordsFromEngineAsync().ConfigureAwait(true);
        MarkFilterSetDirty(true);
        StatusText = $"Added filter rule '{result.Name}'.";
    }

    private async Task EditSelectedFilterRuleAsync()
    {
        if (SelectedFilterPreset is null)
        {
            return;
        }

        var workingCopy = new FilterPresetModel();
        workingCopy.LoadFrom(SelectedFilterPreset);

        var dialog = new FilterEditorWindow("Edit Filter Rule", workingCopy, FilterColorOptions, showNameField: true, showColorField: true)
        {
            Owner = Application.Current?.MainWindow
        };

        _isFilterEditorOpen = true;
        try
        {
            if (dialog.ShowDialog() != true)
            {
                return;
            }
        }
        finally
        {
            _isFilterEditorOpen = false;
        }

        _isApplyingPresetState = true;
        try
        {
            SelectedFilterPreset.LoadFrom(dialog.GetResult());
        }
        finally
        {
            _isApplyingPresetState = false;
        }

        MarkFilterSetDirty(true);

        if (SelectedFilterPreset.IsEnabled)
        {
            InvalidateEnabledFilterRulesCache();
            await RefreshRecordsFromEngineAsync().ConfigureAwait(true);
        }

        NotifySelectedFilterRuleCommandState();
        StatusText = $"Updated filter rule '{SelectedFilterPreset.Name}'.";
    }

    public async Task EditFilterRuleAsync(FilterPresetModel preset)
    {
        SelectedFilterPreset = preset;
        await EditSelectedFilterRuleAsync().ConfigureAwait(true);
    }

    public async Task DeleteFilterRuleAsync(FilterPresetModel preset)
    {
        SelectedFilterPreset = preset;
        await DeleteSelectedFilterPresetAsync().ConfigureAwait(true);
    }

    public void ToggleFilterRuleChecked(FilterPresetModel preset)
    {
        SelectedFilterPreset = preset;
        preset.IsBatchSelected = !preset.IsBatchSelected;
    }

    public void SelectFilterRule(FilterPresetModel preset) => SelectedFilterPreset = preset;

    public void MoveFilterRule(object sourceItem, object? targetItem)
    {
        if (sourceItem is not FilterPresetModel sourcePreset)
        {
            return;
        }

        var sourceIndex = FilterPresets.IndexOf(sourcePreset);
        if (sourceIndex < 0)
        {
            return;
        }

        var targetIndex = targetItem is FilterPresetModel targetPreset
            ? FilterPresets.IndexOf(targetPreset)
            : FilterPresets.Count - 1;

        MoveFilterRuleInternal(sourceIndex, targetIndex);
    }

    private Task MoveSelectedFilterRuleAsync(int direction)
    {
        if (SelectedFilterPreset is null)
        {
            return Task.CompletedTask;
        }

        var currentIndex = FilterPresets.IndexOf(SelectedFilterPreset);
        if (currentIndex < 0)
        {
            return Task.CompletedTask;
        }

        MoveFilterRuleInternal(currentIndex, currentIndex + direction);
        StatusText = direction < 0
            ? $"Moved filter rule '{SelectedFilterPreset.Name}' up."
            : $"Moved filter rule '{SelectedFilterPreset.Name}' down.";
        return Task.CompletedTask;
    }

    private void MoveFilterRuleInternal(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= FilterPresets.Count)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, FilterPresets.Count - 1);
        if (sourceIndex == targetIndex)
        {
            return;
        }

        var preset = FilterPresets[sourceIndex];
        FilterPresets.Move(sourceIndex, targetIndex);
        InvalidateEnabledFilterRulesCache();
        SelectedFilterPreset = preset;
        MarkFilterSetDirty(true);
        NotifySelectedFilterRuleCommandState();
        _ = SafeRefreshRecordsAsync();
    }

    private async Task SetSelectedFilterRulesEnabledAsync(bool enabled)
    {
        var checkedRules = GetCheckedFilterRules();
        FilterPresetModel[] targets;
        if (enabled && checkedRules.Count > 0)
        {
            targets = FilterPresets.ToArray();
        }
        else
        {
            targets = GetTargetFilterRules().ToArray();
        }

        if (targets.Length == 0)
        {
            return;
        }

        _isApplyingPresetState = true;
        try
        {
            foreach (var target in targets)
            {
                if (enabled && checkedRules.Count > 0)
                {
                    target.IsEnabled = target.IsBatchSelected;
                }
                else if (target.IsEnabled != enabled)
                {
                    target.IsEnabled = enabled;
                }
            }
        }
        finally
        {
            _isApplyingPresetState = false;
        }

        InvalidateEnabledFilterRulesCache();
        MarkFilterSetDirty(true);
        NotifySelectedFilterRuleCommandState();
        await RefreshRecordsFromEngineAsync().ConfigureAwait(true);
        StatusText = enabled
            ? checkedRules.Count > 0
                ? $"Enabled {checkedRules.Count:n0} checked filter rules and disabled unchecked rules."
                : targets.Length == 1
                    ? $"Enabled filter rule '{targets[0].Name}'."
                    : $"Enabled {targets.Length:n0} filter rules."
            : targets.Length == 1
                ? $"Disabled filter rule '{targets[0].Name}'."
                : $"Disabled {targets.Length:n0} filter rules.";
    }

    private async Task LoadFilterPresetsAsync(string? filePath = null, bool clearOnly = false)
    {
        foreach (var preset in FilterPresets)
        {
            DetachFilterPreset(preset);
        }

        FilterPresets.Clear();
        var targetPath = filePath;
        IReadOnlyList<FilterPresetModel> loadedPresets = Array.Empty<FilterPresetModel>();
        if (!clearOnly)
        {
            targetPath = string.IsNullOrWhiteSpace(targetPath) ? _filterPresetStore.FilePath : targetPath;
            loadedPresets = await _filterPresetStore.LoadAsync(targetPath).ConfigureAwait(true);
            CurrentFilterSetPath = File.Exists(targetPath) ? targetPath : string.Empty;
        }

        _isApplyingPresetState = true;
        try
        {
            foreach (var preset in loadedPresets)
            {
                AttachFilterPreset(preset);
                FilterPresets.Add(preset);
            }
        }
        finally
        {
            _isApplyingPresetState = false;
        }

        InvalidateEnabledFilterRulesCache();
        SelectedFilterPreset = FilterPresets.FirstOrDefault();
        RaiseFilterRuleSelectionStateChanged();
        MarkFilterSetDirty(false);

        if (_engine.GetSnapshot().Count > 0)
        {
            await RefreshRecordsFromEngineAsync().ConfigureAwait(true);
        }
    }

    private async Task<bool> ConfirmDiscardOrSaveCurrentFilterSetAsync()
    {
        if (!IsFilterSetDirty)
        {
            return true;
        }

        var result = MessageBox.Show(
            "현재 필터 세트가 저장되지 않았습니다. 저장하시겠습니까?",
            AppMetadata.ProductName,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (result == MessageBoxResult.Yes)
        {
            if (string.IsNullOrWhiteSpace(CurrentFilterSetPath))
            {
                await SaveFilterSetAsAsync().ConfigureAwait(true);
            }
            else
            {
                await SaveFilterSetAsync().ConfigureAwait(true);
            }

            return !IsFilterSetDirty;
        }

        return true;
    }

    private async Task TryAutoDiscoverSingleDeviceAsync()
    {
        try
        {
            var devices = await RefreshDevicesAsync().ConfigureAwait(true);
            if (devices.Count == 1)
            {
                SelectedDevice = devices[0];
                StatusText = $"1 adb device detected: {devices[0]}";
            }
        }
        catch
        {
            // 초기화 중 adb 환경이 없어도 앱 시작을 막지 않음
        }
    }

    private async Task PersistFilterPresetsAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentFilterSetPath))
        {
            MarkFilterSetDirty(true);
            return;
        }

        await _filterPresetStore.SaveAsync(FilterPresets, CurrentFilterSetPath).ConfigureAwait(true);
        MarkFilterSetDirty(false);
    }

    private void MarkFilterSetDirty(bool value) => IsFilterSetDirty = value;

    private void SaveUiPreferences() =>
        _uiPreferencesStore.Save(new UiPreferences
        {
            LogFontSize = LogFontSize,
            AppFontFamily = SelectedAppFontFamily,
            LastOpenedLogFilePath = _lastOpenedLogFilePath,
            ColumnWidths = new Dictionary<string, double>(_columnWidthPreferences, StringComparer.Ordinal),
            ColumnDisplayIndexes = new Dictionary<string, int>(_columnDisplayIndexPreferences, StringComparer.Ordinal),
            IsSearchInAllLogs = IsSearchInAllLogs,
            SearchHistory = SearchHistory.ToList()
        });

    private async Task TryRestoreLastOpenedLogFileAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastOpenedLogFilePath))
        {
            return;
        }

        if (!File.Exists(_lastOpenedLogFilePath))
        {
            _lastOpenedLogFilePath = string.Empty;
            SaveUiPreferences();
            StatusText = "The last opened log file could not be restored because the file was not found.";
            return;
        }

        var restored = await LoadFileAsync(_lastOpenedLogFilePath).ConfigureAwait(true);
        if (restored)
        {
            return;
        }

        _lastOpenedLogFilePath = string.Empty;
        SaveUiPreferences();
    }

    private void UpdateLastOpenedLogFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        _lastOpenedLogFilePath = filePath;
        SaveUiPreferences();
    }

    private void AttachFilterPreset(FilterPresetModel preset) => preset.PropertyChanged += OnFilterPresetPropertyChanged;

    private void DetachFilterPreset(FilterPresetModel preset) => preset.PropertyChanged -= OnFilterPresetPropertyChanged;

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

    private FilterQuery BuildCurrentFilterQuery()
    {
        var levels = GetSelectedLevels();
        var pidTerms = ParsePidTerms(PidFilterText);
        return new FilterQuery(
            Levels: levels.Count == 0 ? null : levels,
            TagContains: NullIfWhiteSpace(TagFilterText),
            Pid: pidTerms is { Count: 1 } ? pidTerms.First() : ParseNullablePid(PidFilterText),
            TextContains: NullIfWhiteSpace(FilterText),
            TagTerms: ParseSearchTerms(TagFilterText),
            Pids: pidTerms,
            TextTerms: ParseSearchTerms(FilterText),
            ExcludedTagTerms: ParseSearchTerms(ExcludedTagFilterText),
            ExcludedPids: ParsePidTerms(ExcludedPidFilterText),
            ExcludedTextTerms: ParseSearchTerms(ExcludedFilterText));
    }

    private void OnLogRecordsLiveAppended(object? sender, LogRecordsLiveAppendedEventArgs e)
    {
        var batch = e.Records;
        if (batch.Count == 0)
        {
            return;
        }

        if (IsLiveAppendPaused)
        {
            Interlocked.Add(ref _queuedPausedAppendCount, batch.Count);
            return;
        }

        // Capture once per batch to avoid repeated property reads / cache rebuilds.
        var filterRules = GetEnabledFilterRules();
        var activeFilterQuery = _activeFilterQuery;
        var hasQuickFilter = !activeFilterQuery.IsEmpty;
        var hasRules = filterRules.Count > 0;

        // Fast path: no filters at all → enqueue the whole batch unconditionally.
        if (!hasQuickFilter && !hasRules)
        {
            for (var i = 0; i < batch.Count; i++)
            {
                _pendingLiveRecords.Enqueue(batch[i]);
            }
            return;
        }

        for (var i = 0; i < batch.Count; i++)
        {
            var record = batch[i];

            if (hasQuickFilter && !_engine.MatchesFilter(record, activeFilterQuery))
            {
                continue;
            }

            if (hasRules && !MatchesEnabledFilterRules(record, filterRules))
            {
                continue;
            }

            _pendingLiveRecords.Enqueue(record);
        }
    }

    private void OnLogRecordsBatchLoaded(object? sender, LogRecordsBatchLoadedEventArgs e)
    {
        if (!_isProgressiveFileRendering || e.Records.Count == 0)
        {
            return;
        }

        _ = AppendProgressiveFileRecordsAsync(e.Records);
    }

    private async Task AppendProgressiveFileRecordsAsync(IReadOnlyList<LogRecord> records)
    {
        try
        {
            var rows = await Task.Run(() =>
            {
                var result = new LogRowModel[records.Count];
                for (var i = 0; i < records.Count; i++)
                {
                    result[i] = LogRowModel.From(records[i], string.Empty);
                }
                return result;
            }).ConfigureAwait(true);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!_isProgressiveFileRendering)
                {
                    return;
                }

                foreach (var row in rows)
                {
                    _visibleRowIds.Add(row.RowId);
                    _visibleRowsByRowId[row.RowId] = row;
                }

                Records.AddRange(rows);
            });
        }
        catch
        {
            // Progressive rendering is best-effort. The final ReplaceRecordsAsync call remains authoritative.
        }
    }

    private void OnLiveUiFlushTimerTick(object? sender, EventArgs e)
    {
        FlushPendingLiveRecords();

        var pausedCount = Volatile.Read(ref _queuedPausedAppendCount);
        if (PausedAppendCount != pausedCount)
        {
            PausedAppendCount = pausedCount;
        }
    }

    private void FlushPendingLiveRecords()
    {
        if (_pendingLiveRecords.IsEmpty || _isFilterEditorOpen)
        {
            return;
        }

        var highlightRules = GetEnabledFilterRules();
        var batchSize = _pendingLiveRecords.Count > LiveUiBatchSize ? LiveUiCatchupBatchSize : LiveUiBatchSize;
        var rowsToAdd = new List<LogRowModel>(batchSize);
        var added = 0;
        while (added < batchSize && _pendingLiveRecords.TryDequeue(out var record))
        {
            if (!_visibleRowIds.Add(record.RowId))
            {
                continue;
            }

            var row = CreateLogRowModel(record, highlightRules);
            _visibleRowsByRowId[record.RowId] = row;
            rowsToAdd.Add(row);
            added++;
        }

        if (rowsToAdd.Count == 0)
        {
            return;
        }

        Records.AddRange(rowsToAdd);
    }

    private void OnFilterPresetPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isApplyingPresetState)
        {
            return;
        }

        if (sender is not FilterPresetModel)
        {
            return;
        }

        if (e.PropertyName == nameof(FilterPresetModel.IsBatchSelected))
        {
            RaiseFilterRuleSelectionStateChanged();
            return;
        }

        if (ReferenceEquals(sender, SelectedFilterPreset)
            && e.PropertyName == nameof(FilterPresetModel.IsEnabled))
        {
            NotifySelectedFilterRuleCommandState();
        }

        MarkFilterSetDirty(true);

        var isFilterAffectingProperty = e.PropertyName is nameof(FilterPresetModel.IsEnabled)
            or nameof(FilterPresetModel.ColorHex)
            or nameof(FilterPresetModel.TextFilterText)
            or nameof(FilterPresetModel.TagFilterText)
            or nameof(FilterPresetModel.PidFilterText)
            or nameof(FilterPresetModel.ExcludedTextFilterText)
            or nameof(FilterPresetModel.ExcludedTagFilterText)
            or nameof(FilterPresetModel.ExcludedPidFilterText)
            or nameof(FilterPresetModel.IsVerboseEnabled)
            or nameof(FilterPresetModel.IsDebugEnabled)
            or nameof(FilterPresetModel.IsInfoEnabled)
            or nameof(FilterPresetModel.IsWarnEnabled)
            or nameof(FilterPresetModel.IsErrorEnabled)
            or nameof(FilterPresetModel.IsFatalEnabled);

        if (isFilterAffectingProperty)
        {
            var preset = (FilterPresetModel)sender;
            // IsEnabled 토글은 항상 갱신 필요, 그 외 속성 변경은 필터가 OFF면 갱신 불필요
            var needsRefresh = e.PropertyName == nameof(FilterPresetModel.IsEnabled) || preset.IsEnabled;

            InvalidateEnabledFilterRulesCache();
            if (needsRefresh)
            {
                _ = SafeRefreshRecordsAsync();
            }
        }
    }

    private async void OnSessionStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText = e.State.StatusMessage;

            // During file loading, pipe engine progress to the loading overlay
            if (_isFileLoading)
            {
                BusyMessage = e.State.StatusMessage;
            }

            if (e.State.RunState is not EasyLog.Contracts.Enums.SessionRunState.Running)
            {
                IsLiveSessionRunning = false;
                _ = RefreshDevicesSilentlyAsync();
            }
        });
    }

    private async void OnDeviceMonitorTimerTick(object? sender, EventArgs e)
    {
        await RefreshDevicesSilentlyAsync().ConfigureAwait(true);
    }

    private IReadOnlyCollection<LogLevel> GetSelectedLevels()
    {
        var levels = new List<LogLevel>(6);
        if (IsVerboseEnabled) levels.Add(LogLevel.Verbose);
        if (IsDebugEnabled) levels.Add(LogLevel.Debug);
        if (IsInfoEnabled) levels.Add(LogLevel.Info);
        if (IsWarnEnabled) levels.Add(LogLevel.Warn);
        if (IsErrorEnabled) levels.Add(LogLevel.Error);
        if (IsFatalEnabled) levels.Add(LogLevel.Fatal);
        return levels;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Unwraps AggregateException to show actual error details instead of the generic
    /// "One or more errors occurred." message.
    /// </summary>
    private static string FormatExceptionMessage(Exception ex)
    {
        if (ex is AggregateException aggEx)
        {
            var messages = aggEx.Flatten().InnerExceptions
                .Select(e => e.Message)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return messages.Length switch
            {
                0 => ex.Message,
                1 => messages[0],
                _ => string.Join("; ", messages)
            };
        }

        // Unwrap generic wrapper exceptions that hide the real cause
        if (ex.InnerException is not null
            && ex.Message.Contains("One or more errors", StringComparison.OrdinalIgnoreCase))
        {
            return FormatExceptionMessage(ex.InnerException);
        }

        return ex.Message;
    }

    private static IReadOnlyCollection<string>? ParseSearchTerms(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var terms = value
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return terms.Length == 0 ? null : terms;
    }

    private static IReadOnlyCollection<int>? ParsePidTerms(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var pids = value
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) ? (int?)pid : null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        return pids.Length == 0 ? null : pids;
    }

    private static int? ParseNullablePid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)
            ? pid
            : null;
    }

    private LogRowModel CreateLogRowModel(LogRecord record, IReadOnlyList<(FilterPresetModel Preset, FilterQuery Query)>? highlightRules = null)
    {
        var diagnostics = _engine.GetDiagnostics(record).ToString();
        var brushes = ResolveHighlightBrushes(record, highlightRules ?? GetEnabledFilterRules());
        return LogRowModel.From(record, diagnostics, brushes.Background, brushes.Foreground);
    }

    /// <summary>
    /// Lightweight version for bulk file loading — skips diagnostics to avoid per-record string searches.
    /// Uses frozen brushes that are safe for background thread access.
    /// </summary>
    private LogRowModel CreateLogRowModelFast(LogRecord record, IReadOnlyList<(FilterPresetModel Preset, FilterQuery Query)> highlightRules)
    {
        var brushes = ResolveHighlightBrushes(record, highlightRules);
        return LogRowModel.From(record, string.Empty, brushes.Background, brushes.Foreground);
    }

    private IReadOnlyList<(FilterPresetModel Preset, FilterQuery Query)> GetEnabledFilterRules() =>
        _enabledFilterRulesCache;

    private IReadOnlyList<LogRecord> ApplyEnabledFilterRules(
        IReadOnlyList<LogRecord> records,
        IReadOnlyList<(FilterPresetModel Preset, FilterQuery Query)> filterRules)
    {
        if (filterRules.Count == 0)
        {
            return records;
        }

        var filtered = new List<LogRecord>(Math.Max(records.Count / 4, 256));
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            if (MatchesEnabledFilterRules(record, filterRules))
            {
                filtered.Add(record);
            }
        }

        return filtered;
    }

    /// <summary>
    /// Single-pass combined filter: applies QuickFilter (top-bar) AND enabled Filter Rules in one loop.
    /// Avoids the intermediate baseRecords list and double iteration vs.
    /// <c>_engine.ApplyFilter</c> followed by <see cref="ApplyEnabledFilterRules"/>.
    /// Fast path: if both filters are empty, returns the input list unchanged.
    /// Phase 2 — 2순위 (FilterQuery 사전 정규화 + Quick/Rule 단일 pass).
    /// </summary>
    private IReadOnlyList<LogRecord> ApplyCombinedFilters(
        IReadOnlyList<LogRecord> records,
        FilterQuery quickFilter,
        IReadOnlyList<(FilterPresetModel Preset, FilterQuery Query)> filterRules)
    {
        var hasQuick = !quickFilter.IsEmpty;
        var hasRules = filterRules.Count > 0;

        if (!hasQuick && !hasRules)
        {
            return records;
        }

        var filtered = new List<LogRecord>(Math.Max(records.Count / 4, 256));
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];

            if (hasQuick && !_engine.MatchesFilter(record, quickFilter))
            {
                continue;
            }

            if (hasRules && !MatchesEnabledFilterRules(record, filterRules))
            {
                continue;
            }

            filtered.Add(record);
        }

        return filtered;
    }

    private bool MatchesEnabledFilterRules(
        LogRecord record,
        IReadOnlyList<(FilterPresetModel Preset, FilterQuery Query)> filterRules)
    {
        for (var i = 0; i < filterRules.Count; i++)
        {
            if (_engine.MatchesFilter(record, filterRules[i].Query))
            {
                return true;
            }
        }

        return false;
    }

    private (Brush Background, Brush Foreground) ResolveHighlightBrushes(LogRecord record, IReadOnlyList<(FilterPresetModel Preset, FilterQuery Query)> highlightRules)
    {
        foreach (var rule in highlightRules)
        {
            if (_engine.MatchesFilter(record, rule.Query))
            {
                return GetHighlightBrushPair(rule.Preset.ColorHex);
            }
        }

        return (Brushes.Transparent, Brushes.Black);
    }

    private (Brush Background, Brush Foreground) GetHighlightBrushPair(string colorHex)
    {
        if (_highlightBrushCache.TryGetValue(colorHex, out var brushes))
        {
            return brushes;
        }

        var background = (Brush)new BrushConverter().ConvertFromString(colorHex)!;
        if (background.CanFreeze)
        {
            background.Freeze();
        }

        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        var luminance = ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255d;
        var foreground = luminance > 0.6 ? Brushes.Black : Brushes.White;
        brushes = (background, foreground);
        _highlightBrushCache[colorHex] = brushes;
        return brushes;
    }

    private static IReadOnlyList<FilterColorOption> CreateDefaultFilterColors() =>
        new[]
        {
            new FilterColorOption("Steel Blue", "#4E79A7"),
            new FilterColorOption("Muted Teal", "#4C9F9A"),
            new FilterColorOption("Sage", "#7AA974"),
            new FilterColorOption("Olive", "#8C9A4D"),
            new FilterColorOption("Sand", "#C2A46A"),
            new FilterColorOption("Soft Amber", "#D9A441"),
            new FilterColorOption("Dusty Rose", "#B86B77"),
            new FilterColorOption("Terracotta", "#C97B63"),
            new FilterColorOption("Lavender", "#8E7DBE"),
            new FilterColorOption("Soft Violet", "#7563A8"),
            new FilterColorOption("Slate", "#5B7083"),
            new FilterColorOption("Moss", "#6B8F71")
        };

    private static IReadOnlyList<string> CreateDefaultAppFontFamilies() =>
        new[]
        {
            "Segoe UI",
            "Malgun Gothic",
            "Bahnschrift",
            "Consolas",
            "Cascadia Mono",
            "Arial",
            "Tahoma"
        };

    private string NormalizeAppFontFamily(string? fontFamily)
{
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return UiPreferences.DefaultAppFontFamily;
        }

        return AppFontFamilies.FirstOrDefault(x => string.Equals(x, fontFamily, StringComparison.OrdinalIgnoreCase))
            ?? UiPreferences.DefaultAppFontFamily;
    }

    private FilterPresetModel CreateQuickFilterPreset() =>
        new()
        {
            Name = "Quick Filter",
            TextFilterText = FilterText,
            TagFilterText = TagFilterText,
            PidFilterText = PidFilterText,
            ExcludedTextFilterText = ExcludedFilterText,
            ExcludedTagFilterText = ExcludedTagFilterText,
            ExcludedPidFilterText = ExcludedPidFilterText,
            IsVerboseEnabled = IsVerboseEnabled,
            IsDebugEnabled = IsDebugEnabled,
            IsInfoEnabled = IsInfoEnabled,
            IsWarnEnabled = IsWarnEnabled,
            IsErrorEnabled = IsErrorEnabled,
            IsFatalEnabled = IsFatalEnabled,
            ColorHex = SelectedFilterColor?.BackgroundHex ?? "#4E79A7"
        };

    private static bool Contains(string source, string query) =>
        source.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSearchExpression(LogRecord record, SearchExpression searchExpression) =>
        searchExpression.Matches(term =>
            Contains(record.Tag, term) ||
            Contains(record.Message, term) ||
            (record.Pid?.ToString(CultureInfo.InvariantCulture)?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));

    private bool CanSetSelectedFilterRulesEnabled(bool enabled) =>
        enabled
            ? (GetCheckedFilterRules().Count > 0
                ? FilterPresets.Any(x => x.IsEnabled != x.IsBatchSelected)
                : GetTargetFilterRules().Any(x => x.IsEnabled != enabled))
            : GetTargetFilterRules().Any(x => x.IsEnabled != enabled);

    private bool CanMoveSelectedFilterRule(int direction)
    {
        if (SelectedFilterPreset is null)
        {
            return false;
        }

        var index = FilterPresets.IndexOf(SelectedFilterPreset);
        if (index < 0)
        {
            return false;
        }

        var targetIndex = index + direction;
        return targetIndex >= 0 && targetIndex < FilterPresets.Count;
    }

    private IReadOnlyList<FilterPresetModel> GetCheckedFilterRules() =>
        FilterPresets.Where(x => x.IsBatchSelected).ToArray();

    private IReadOnlyList<FilterPresetModel> GetTargetFilterRules()
    {
        var checkedRules = GetCheckedFilterRules();
        if (checkedRules.Count > 0)
        {
            return checkedRules;
        }

        return SelectedFilterPreset is null
            ? Array.Empty<FilterPresetModel>()
            : new[] { SelectedFilterPreset };
    }

    private void SetAllFilterRuleSelection(bool isSelected)
    {
        foreach (var preset in FilterPresets)
        {
            preset.IsBatchSelected = isSelected;
        }

        _isAllFilterRulesSelected = isSelected;
        RaiseFilterRuleSelectionStateChanged();
    }

    private void RaiseFilterRuleSelectionStateChanged()
    {
        var allSelected = FilterPresets.Count > 0 && FilterPresets.All(x => x.IsBatchSelected);
        SetProperty(ref _isAllFilterRulesSelected, allSelected, nameof(AreAllFilterRulesSelected));
        RaisePropertyChanged(nameof(AreAllFilterRulesSelected));
        RaisePropertyChanged(nameof(CheckedFilterRulesSummary));
        NotifySelectedFilterRuleCommandState();
    }

    private void InvalidateEnabledFilterRulesCache()
    {
        _enabledFilterRulesCache = FilterPresets
            .Where(x => x.IsEnabled)
            .Select(x => (Preset: x, Query: x.ToFilterQuery(ParseSearchTerms, ParsePidTerms)))
            .ToArray();
    }

    private async Task RefreshDevicesSilentlyAsync()
    {
        if (_isSilentDeviceRefreshRunning || IsBusy)
        {
            return;
        }

        try
        {
            _isSilentDeviceRefreshRunning = true;
            var devices = await _engine.DiscoverDevicesAsync().ConfigureAwait(true);
            _adbConsecutiveFailureCount = 0;
            ApplyDiscoveredDevices(devices);
        }
        catch
        {
            _adbConsecutiveFailureCount++;
            ApplyDiscoveredDevices(Array.Empty<DeviceInfo>());
            if (_adbConsecutiveFailureCount >= AdbMaxConsecutiveFailures)
            {
                _deviceMonitorTimer.Stop();
                StatusText = "adb not available – auto-detection paused. Click 'Discover Devices' to retry.";
            }
        }
        finally
        {
            _isSilentDeviceRefreshRunning = false;
        }
    }

    private void ApplyDiscoveredDevices(IReadOnlyList<DeviceInfo> devices)
    {
        var previousSelectedSerial = SelectedDevice?.Serial;
        Devices.Clear();
        foreach (var device in devices)
        {
            Devices.Add(device);
        }

        SelectedDevice = Devices.FirstOrDefault(x => string.Equals(x.Serial, previousSelectedSerial, StringComparison.OrdinalIgnoreCase))
            ?? Devices.FirstOrDefault(static x => string.Equals(x.State, "device", StringComparison.OrdinalIgnoreCase))
            ?? Devices.FirstOrDefault();

        IsAdbDeviceConnected = Devices.Any(static x => string.Equals(x.State, "device", StringComparison.OrdinalIgnoreCase));
        AdbConnectionStatusText = IsAdbDeviceConnected
            ? "ADB device connected"
            : "No adb device connected";
    }

    private void OnDisplayCollectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ClearLogsCommand.NotifyCanExecuteChanged();
    }

    private void NotifySelectedFilterRuleCommandState()
    {
        EditSelectedFilterRuleCommand.NotifyCanExecuteChanged();
        MoveSelectedFilterRuleUpCommand.NotifyCanExecuteChanged();
        MoveSelectedFilterRuleDownCommand.NotifyCanExecuteChanged();
        EnableSelectedFilterRuleCommand.NotifyCanExecuteChanged();
        DisableSelectedFilterRuleCommand.NotifyCanExecuteChanged();
        DeleteSelectedFilterPresetCommand.NotifyCanExecuteChanged();
    }

    public void CancelFileLoad()
    {
        _fileLoadCts?.Cancel();
    }

    public void Dispose()
    {
        Records.CollectionChanged -= OnDisplayCollectionsChanged;
        SearchResults.CollectionChanged -= OnDisplayCollectionsChanged;
        _liveUiFlushTimer.Stop();
        _liveUiFlushTimer.Tick -= OnLiveUiFlushTimerTick;
        _deviceMonitorTimer.Stop();
        _deviceMonitorTimer.Tick -= OnDeviceMonitorTimerTick;
        CancelWaitForDevice();
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _filterApplyCts?.Cancel();
        _filterApplyCts?.Dispose();
        _engine.LogRecordsLiveAppended -= OnLogRecordsLiveAppended;
        _engine.LogRecordsBatchLoaded -= OnLogRecordsBatchLoaded;
        _engine.SessionStateChanged -= OnSessionStateChanged;
        _engine.Dispose();
    }
}


