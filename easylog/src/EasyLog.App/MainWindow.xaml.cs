using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using EasyLog.App.Models;
using EasyLog.App.ViewModels;

namespace EasyLog.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _scrollToLatestPending;
    private Point _filterRuleDragStartPoint;
    private object? _filterRuleDragSource;
    private readonly List<(DataGridColumn Column, EventHandler Handler)> _trackedColumnWidthHandlers = new();
    private readonly DispatcherTimer _columnWidthPersistenceTimer;
    private bool _isApplyingSavedColumnLayout;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        _viewModel.Records.CollectionChanged += OnRecordsCollectionChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _columnWidthPersistenceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _columnWidthPersistenceTimer.Tick += OnColumnWidthPersistenceTimerTick;
        PreviewKeyDown += OnWindowPreviewKeyDown;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ApplySavedColumnDisplayIndexes();
        ApplySavedColumnWidths();
        RegisterColumnOrderTracking();
        RegisterColumnWidthTracking();
        await _viewModel.InitializeAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        PreviewKeyDown -= OnWindowPreviewKeyDown;
        _columnWidthPersistenceTimer.Stop();
        _columnWidthPersistenceTimer.Tick -= OnColumnWidthPersistenceTimerTick;
        CaptureCurrentColumnLayout();
        UnregisterColumnOrderTracking();
        UnregisterColumnWidthTracking();
        _viewModel.Records.CollectionChanged -= OnRecordsCollectionChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Dispose();
    }

    private void OnSearchInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CloseSearchHistoryPopup();
            if (_viewModel.SearchLogsCommand.CanExecute(null))
            {
                _viewModel.SearchLogsCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (SearchHistoryPopup.IsOpen)
            {
                CloseSearchHistoryPopup();
                e.Handled = true;
            }
            return;
        }

        if (e.Key is Key.Down or Key.Up && _viewModel.SearchHistory.Count > 0)
        {
            if (!SearchHistoryPopup.IsOpen)
            {
                OpenSearchHistoryPopup();
            }

            var listBox = SearchHistoryListBox;
            var currentIndex = listBox.SelectedIndex;
            if (e.Key == Key.Down)
            {
                listBox.SelectedIndex = currentIndex < listBox.Items.Count - 1 ? currentIndex + 1 : 0;
            }
            else // Key.Up
            {
                listBox.SelectedIndex = currentIndex > 0 ? currentIndex - 1 : listBox.Items.Count - 1;
            }

            if (listBox.SelectedItem is string selectedTerm)
            {
                _viewModel.SearchText = selectedTerm;
                SearchInputBox.CaretIndex = selectedTerm.Length;
            }

            e.Handled = true;
            return;
        }
    }

    private void OnSearchInputGotFocus(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SearchHistory.Count > 0 && string.IsNullOrEmpty(_viewModel.SearchText))
        {
            OpenSearchHistoryPopup();
        }
    }

    private void OnSearchInputLostFocus(object sender, RoutedEventArgs e)
    {
        // Delay closing to allow click on popup items
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!SearchInputBox.IsFocused && !SearchHistoryListBox.IsKeyboardFocusWithin)
            {
                CloseSearchHistoryPopup();
            }
        }, DispatcherPriority.Background);
    }

    private void OnSearchHistoryItemClick(object sender, MouseButtonEventArgs e)
    {
        // Check if the click was on the delete button ??if so, don't select the item
        if (FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (SearchHistoryListBox.SelectedItem is string selectedTerm)
        {
            _viewModel.SearchText = selectedTerm;
            CloseSearchHistoryPopup();
            SearchInputBox.Focus();
            SearchInputBox.CaretIndex = selectedTerm.Length;

            if (_viewModel.SearchLogsCommand.CanExecute(null))
            {
                _viewModel.SearchLogsCommand.Execute(null);
            }
        }
    }

    private void OnSearchHistoryListBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SearchHistoryListBox.SelectedItem is string selectedTerm)
        {
            _viewModel.SearchText = selectedTerm;
            CloseSearchHistoryPopup();
            SearchInputBox.Focus();

            if (_viewModel.SearchLogsCommand.CanExecute(null))
            {
                _viewModel.SearchLogsCommand.Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CloseSearchHistoryPopup();
            SearchInputBox.Focus();
            e.Handled = true;
        }
    }

    private void OpenSearchHistoryPopup()
    {
        if (_viewModel.SearchHistory.Count == 0)
        {
            return;
        }

        SearchHistoryListBox.SelectedIndex = -1;
        SearchHistoryPopup.IsOpen = true;
    }

    private void CloseSearchHistoryPopup()
    {
        SearchHistoryPopup.IsOpen = false;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchInputBox.Focus();
            SearchInputBox.SelectAll();
            if (_viewModel.SearchHistory.Count > 0)
            {
                OpenSearchHistoryPopup();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            // If a TextBox has selected text, let it handle Ctrl+C natively (search box, preview, etc.)
            if (Keyboard.FocusedElement is TextBox { SelectedText.Length: > 0 })
                return;

            // Check if keyboard focus is directly inside a DataGrid
            if (Keyboard.FocusedElement is DependencyObject focused
                && FindAncestor<DataGrid>(focused) is DataGrid ownerGrid
                && (ownerGrid == LogGrid || ownerGrid == SearchResultsGrid)
                && ownerGrid.SelectedItems.Count > 0)
            {
                CopySelectedRows(ownerGrid, messageOnly: false);
                e.Handled = true;
                return;
            }

            // Fallback: DataGridCell.Focusable is False, so keyboard focus may stay on
            // a previous control even after clicking a DataGrid row. Use mouse-over /
            // selection heuristic to find the active grid.
            var activeGrid = GetActiveLogGrid();
            if (activeGrid is not null && activeGrid.SelectedItems.Count > 0)
            {
                CopySelectedRows(activeGrid, messageOnly: false);
                e.Handled = true;
            }
            return;
        }

        // Escape key ??clear focus from search box or data grid, return focus to window
        if (e.Key == Key.Escape)
        {
            if (SearchHistoryPopup.IsOpen)
            {
                CloseSearchHistoryPopup();
            }

            // Move focus to the main window itself (neutral element)
            FocusManager.SetFocusedElement(this, this);
            Keyboard.ClearFocus();
            e.Handled = true;
            return;
        }

        // Navigation keys ??only when a log grid is active (mouse over or has selection)
        // and the focus is NOT inside a TextBox (e.g. search box).
        if (Keyboard.FocusedElement is TextBox)
            return;

        var grid = GetActiveLogGrid();
        if (grid is null)
            return;

        var scrollViewer = FindDescendant<ScrollViewer>(grid);
        if (scrollViewer is null)
            return;

        switch (e.Key)
        {
            case Key.Home:
                _viewModel.IsAutoScrollEnabled = false;
                scrollViewer.ScrollToTop();
                if (grid.Items.Count > 0)
                    grid.SelectedItem = grid.Items[0];
                e.Handled = true;
                break;

            case Key.End when Keyboard.Modifiers == ModifierKeys.None:
                if (ReferenceEquals(grid, LogGrid))
                {
                    _viewModel.IsAutoScrollEnabled = true;
                }

                NavigateGridToEnd(grid);
                e.Handled = true;
                break;

            case Key.PageUp:
                _viewModel.IsAutoScrollEnabled = false;
                NavigateGridPage(grid, scrollViewer, -1);
                e.Handled = true;
                break;

            case Key.PageDown:
                NavigateGridPage(grid, scrollViewer, +1);
                e.Handled = true;
                break;

            case Key.Up:
                _viewModel.IsAutoScrollEnabled = false;
                NavigateGridRow(grid, scrollViewer, -1);
                e.Handled = true;
                break;

            case Key.Down:
                NavigateGridRow(grid, scrollViewer, +1);
                e.Handled = true;
                break;
        }
    }

    private static void NavigateGridPage(DataGrid grid, ScrollViewer scrollViewer, int direction)
    {
        if (grid.Items.Count == 0)
            return;

        // When CanContentScroll is true (virtualizing), ViewportHeight is already
        // in logical units (visible item count), not pixels.
        int visibleRows;
        if (scrollViewer.CanContentScroll)
        {
            visibleRows = Math.Max(1, (int)scrollViewer.ViewportHeight);
        }
        else
        {
            double rowHeight = 24.0;
            if (grid.ItemContainerGenerator.ContainerFromIndex(Math.Max(0, grid.SelectedIndex)) is DataGridRow renderedRow
                && renderedRow.ActualHeight > 0)
            {
                rowHeight = renderedRow.ActualHeight;
            }
            visibleRows = Math.Max(1, (int)(scrollViewer.ViewportHeight / rowHeight));
        }

        var currentIndex = grid.SelectedIndex >= 0 ? grid.SelectedIndex : 0;
        var newIndex = currentIndex + (direction * visibleRows);

        if (newIndex < 0)
            newIndex = 0;
        else if (newIndex >= grid.Items.Count)
            newIndex = grid.Items.Count - 1;

        grid.SelectedItem = grid.Items[newIndex];
        grid.ScrollIntoView(grid.SelectedItem);
    }

    private static void NavigateGridRow(DataGrid grid, ScrollViewer scrollViewer, int direction)
    {
        if (grid.Items.Count == 0)
            return;

        var currentIndex = grid.SelectedIndex;
        var newIndex = currentIndex + direction;

        if (newIndex < 0)
            newIndex = 0;
        else if (newIndex >= grid.Items.Count)
            newIndex = grid.Items.Count - 1;

        grid.SelectedItem = grid.Items[newIndex];
        grid.ScrollIntoView(grid.SelectedItem);
    }

    private void NavigateGridToEnd(DataGrid grid)
    {
        if (grid.Items.Count == 0)
            return;

        var lastItem = grid.Items[grid.Items.Count - 1];
        grid.SelectedItem = lastItem;
        grid.CurrentItem = lastItem;
        grid.ScrollIntoView(lastItem);
        grid.Focus();

        _ = Dispatcher.BeginInvoke(() =>
        {
            grid.UpdateLayout();
            grid.ScrollIntoView(lastItem);
            if (grid.ItemContainerGenerator.ContainerFromItem(lastItem) is DataGridRow row)
            {
                row.Focus();
            }
            else
            {
                grid.Focus();
            }
        }, DispatcherPriority.Background);
    }

    private DataGrid? GetActiveLogGrid()
    {
        // Check which grid the mouse is currently over, or which has a selection.
        if (SearchResultsGrid.IsMouseOver && SearchResultsGrid.SelectedItems.Count > 0)
            return SearchResultsGrid;
        if (LogGrid.IsMouseOver && LogGrid.SelectedItems.Count > 0)
            return LogGrid;

        // Fallback: whichever has a selection (prefer LogGrid).
        if (LogGrid.SelectedItems.Count > 0)
            return LogGrid;
        if (SearchResultsGrid.SelectedItems.Count > 0)
            return SearchResultsGrid;

        return null;
    }

    private void OnListBoxContextMenuAddClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.AddFilterRuleCommand.CanExecute(null))
        {
            _viewModel.AddFilterRuleCommand.Execute(null);
        }
    }


    private void OnFilterRulesListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!TryGetFilterPreset(e.OriginalSource as DependencyObject, out var preset)
            || FindAncestor<CheckBox>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        _viewModel.SelectFilterRule(preset);
        if (_viewModel.EditSelectedFilterRuleCommand.CanExecute(null))
        {
            _viewModel.EditSelectedFilterRuleCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnLogGridPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel.IsAutoScrollEnabled)
        {
            _viewModel.IsAutoScrollEnabled = false;
        }
    }

    private void OnLogGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is not null)
        {
            if (_viewModel.IsAutoScrollEnabled)
            {
                _viewModel.IsAutoScrollEnabled = false;
            }

            // Move keyboard focus to the grid so arrow / PageUp / PageDown keys work
            if (sender is DataGrid grid && !grid.IsKeyboardFocusWithin)
            {
                grid.Focus();
            }
        }
    }



    private void OnLogGridScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!_viewModel.IsAutoScrollEnabled)
        {
            return;
        }

        if (e.ExtentHeightChange != 0)
        {
            return;
        }

        var isAtBottom = e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 1;
        if (!isAtBottom)
        {
            _viewModel.IsAutoScrollEnabled = false;
        }
    }

    private void OnRecordsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_viewModel.IsAutoScrollEnabled)
        {
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            RequestScrollToLatest();
            return;
        }

        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null || e.NewItems.Count == 0)
        {
            return;
        }

        RequestScrollToLatest();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsAutoScrollEnabled) && _viewModel.IsAutoScrollEnabled)
        {
            RequestScrollToLatest();
        }

        if (e.PropertyName == nameof(MainWindowViewModel.SelectedRow) && _viewModel.SelectedRow is not null)
        {
            _ = Dispatcher.BeginInvoke(() => LogGrid.ScrollIntoView(_viewModel.SelectedRow), DispatcherPriority.Background);
        }
    }

    private void RequestScrollToLatest()
    {
        if (_scrollToLatestPending)
        {
            return;
        }

        _scrollToLatestPending = true;
        _ = Dispatcher.BeginInvoke(() =>
        {
            _scrollToLatestPending = false;
            ScrollToLatestRecord();
        }, DispatcherPriority.Background);
    }

    private void ScrollToLatestRecord()
    {
        if (!_viewModel.IsAutoScrollEnabled || LogGrid.Items.Count == 0)
        {
            return;
        }

        var lastItem = LogGrid.Items[LogGrid.Items.Count - 1];
        LogGrid.ScrollIntoView(lastItem);
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        var supportedFiles = ResolveSupportedFiles(files);
        if (supportedFiles.Count == 0)
        {
            return;
        }

        await _viewModel.OpenDroppedFilesAsync(supportedFiles);
        e.Handled = true;
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        e.Effects = files?.Any(f => IsSupportedFile(f) || System.IO.Directory.Exists(f)) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Resolves dropped items into a flat list of supported files.
    /// If a folder is dropped, it is scanned recursively for supported files.
    /// </summary>
    private static List<string> ResolveSupportedFiles(string[]? items)
    {
        var result = new List<string>();
        if (items is null)
        {
            return result;
        }

        foreach (var item in items)
        {
            if (System.IO.Directory.Exists(item))
            {
                // Folder dropped ??scan for supported files recursively
                try
                {
                    var filesInDir = System.IO.Directory.EnumerateFiles(item, "*.*", System.IO.SearchOption.AllDirectories)
                        .Where(IsSupportedFile);
                    result.AddRange(filesInDir);
                }
                catch
                {
                    // Best-effort: skip folders that can't be enumerated
                }
            }
            else if (IsSupportedFile(item))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static bool IsSupportedFile(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath);
        return extension.Equals(".log", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".logcat", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".7z", StringComparison.OrdinalIgnoreCase);
    }

    private void OnFilterRulesListBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _filterRuleDragStartPoint = e.GetPosition(FilterRulesListBox);
        _filterRuleDragSource = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext;
    }

    private void OnFilterRulesListBoxPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TryGetFilterPreset(e.OriginalSource as DependencyObject, out var preset))
        {
            _viewModel.SelectFilterRule(preset);
        }
    }

    private void OnFilterRuleItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FilterPresetModel preset })
        {
            return;
        }

        if (FindAncestor<CheckBox>(e.OriginalSource as DependencyObject) is not null)
        {
            _viewModel.SelectFilterRule(preset);
            return;
        }

        _viewModel.ToggleFilterRuleChecked(preset);
    }

    private async void OnEditFilterRuleMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetContextMenuPreset(sender, out var preset))
        {
            return;
        }

        await _viewModel.EditFilterRuleAsync(preset);
    }

    private async void OnDeleteFilterRuleMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetContextMenuPreset(sender, out var preset))
        {
            return;
        }

        await _viewModel.DeleteFilterRuleAsync(preset);
    }

    private void OnFilterRulesListBoxMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _filterRuleDragSource is null)
        {
            return;
        }

        var position = e.GetPosition(FilterRulesListBox);
        if (Math.Abs(position.X - _filterRuleDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _filterRuleDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(FilterRulesListBox, _filterRuleDragSource, DragDropEffects.Move);
    }

    private void OnFilterRulesListBoxDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(FilterPresetModel)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFilterRulesListBoxDrop(object sender, DragEventArgs e)
    {
        var sourcePreset = e.Data.GetData(typeof(FilterPresetModel));
        var targetPreset = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext;

        if (sourcePreset is null)
        {
            return;
        }

        _viewModel.MoveFilterRule(sourcePreset, targetPreset);
        _filterRuleDragSource = null;
        e.Handled = true;
    }

    private void ApplySavedColumnDisplayIndexes()
    {
        _isApplyingSavedColumnLayout = true;
        try
        {
            foreach (var grid in EnumerateTrackableGrids())
            {
                ApplySavedColumnDisplayIndexes(grid);
            }
        }
        finally
        {
            _isApplyingSavedColumnLayout = false;
        }
    }

    private void ApplySavedColumnWidths()
    {
        _isApplyingSavedColumnLayout = true;
        try
        {
            foreach (var (grid, column) in EnumerateTrackableColumns())
            {
                var key = GetColumnPreferenceKey(grid, column);
                var savedWidth = _viewModel.GetColumnWidthPreference(key);
                if (savedWidth is not > 0)
                {
                    continue;
                }

                column.Width = new DataGridLength(savedWidth.Value, DataGridLengthUnitType.Pixel);
            }
        }
        finally
        {
            _isApplyingSavedColumnLayout = false;
        }
    }

    private void RegisterColumnOrderTracking()
    {
        UnregisterColumnOrderTracking();
        LogGrid.ColumnReordered += OnTrackableGridColumnReordered;
        SearchResultsGrid.ColumnReordered += OnTrackableGridColumnReordered;
    }

    private void UnregisterColumnOrderTracking()
    {
        LogGrid.ColumnReordered -= OnTrackableGridColumnReordered;
        SearchResultsGrid.ColumnReordered -= OnTrackableGridColumnReordered;
    }

    private void RegisterColumnWidthTracking()
    {
        UnregisterColumnWidthTracking();

        var widthPropertyDescriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
        if (widthPropertyDescriptor is null)
        {
            return;
        }

        foreach (var (grid, column) in EnumerateTrackableColumns())
        {
            EventHandler handler = (_, _) => OnTrackedColumnWidthChanged(grid, column);
            widthPropertyDescriptor.AddValueChanged(column, handler);
            _trackedColumnWidthHandlers.Add((column, handler));
        }
    }

    private void UnregisterColumnWidthTracking()
    {
        var widthPropertyDescriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
        if (widthPropertyDescriptor is null)
        {
            _trackedColumnWidthHandlers.Clear();
            return;
        }

        foreach (var (column, handler) in _trackedColumnWidthHandlers)
        {
            widthPropertyDescriptor.RemoveValueChanged(column, handler);
        }

        _trackedColumnWidthHandlers.Clear();
    }

    private void OnTrackedColumnWidthChanged(DataGrid grid, DataGridColumn column)
    {
        if (_isApplyingSavedColumnLayout)
        {
            return;
        }

        if (!TryGetPersistedColumnWidth(column, out var width))
        {
            return;
        }

        _viewModel.SetColumnWidthPreference(GetColumnPreferenceKey(grid, column), width);
        ScheduleColumnWidthPersistence();
    }

    private void OnTrackableGridColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (_isApplyingSavedColumnLayout || sender is not DataGrid grid)
        {
            return;
        }

        CaptureCurrentColumnDisplayIndexes(grid);
        ScheduleColumnWidthPersistence();
    }

    private void CaptureCurrentColumnLayout()
    {
        foreach (var (grid, column) in EnumerateTrackableColumns())
        {
            if (!TryGetPersistedColumnWidth(column, out var width))
            {
                continue;
            }

            _viewModel.SetColumnWidthPreference(GetColumnPreferenceKey(grid, column), width);
        }

        foreach (var grid in EnumerateTrackableGrids())
        {
            CaptureCurrentColumnDisplayIndexes(grid);
        }

        _viewModel.PersistUiPreferences();
    }

    private void OnColumnWidthPersistenceTimerTick(object? sender, EventArgs e)
    {
        _columnWidthPersistenceTimer.Stop();
        _viewModel.PersistUiPreferences();
    }

    private void ScheduleColumnWidthPersistence()
    {
        _columnWidthPersistenceTimer.Stop();
        _columnWidthPersistenceTimer.Start();
    }

    private IEnumerable<(DataGrid Grid, DataGridColumn Column)> EnumerateTrackableColumns()
    {
        foreach (var grid in EnumerateTrackableGrids())
        {
            foreach (var column in grid.Columns)
            {
                yield return (grid, column);
            }
        }
    }

    private IEnumerable<DataGrid> EnumerateTrackableGrids()
    {
        yield return LogGrid;
        yield return SearchResultsGrid;
    }

    private static string GetColumnPreferenceKey(DataGrid grid, DataGridColumn column)
    {
        var headerText = column.Header?.ToString();
        if (string.IsNullOrWhiteSpace(headerText))
        {
            headerText = $"Column{grid.Columns.IndexOf(column)}";
        }

        return $"{grid.Name}.{headerText}";
    }

    private static bool TryGetPersistedColumnWidth(DataGridColumn column, out double width)
    {
        width = column.ActualWidth;
        return !double.IsNaN(width)
               && !double.IsInfinity(width)
               && width > 0;
    }

    private void ApplySavedColumnDisplayIndexes(DataGrid grid)
    {
        var orderedColumns = grid.Columns
            .Cast<DataGridColumn>()
            .Select((column, declarationIndex) => new
            {
                Column = column,
                DeclarationIndex = declarationIndex,
                SavedDisplayIndex = GetSavedColumnDisplayIndex(grid, column)
            })
            .OrderBy(x => x.SavedDisplayIndex ?? int.MaxValue)
            .ThenBy(x => x.DeclarationIndex)
            .ToList();

        for (var targetDisplayIndex = 0; targetDisplayIndex < orderedColumns.Count; targetDisplayIndex++)
        {
            var column = orderedColumns[targetDisplayIndex].Column;
            if (column.DisplayIndex != targetDisplayIndex)
            {
                column.DisplayIndex = targetDisplayIndex;
            }
        }
    }

    private int? GetSavedColumnDisplayIndex(DataGrid grid, DataGridColumn column)
    {
        var savedDisplayIndex = _viewModel.GetColumnDisplayIndexPreference(GetColumnPreferenceKey(grid, column));
        return savedDisplayIndex is >= 0 && savedDisplayIndex < grid.Columns.Count
            ? savedDisplayIndex
            : null;
    }

    private void CaptureCurrentColumnDisplayIndexes(DataGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            _viewModel.SetColumnDisplayIndexPreference(GetColumnPreferenceKey(grid, column), column.DisplayIndex);
        }
    }


    private static T? FindAncestor<T>(DependencyObject? dependencyObject)
        where T : DependencyObject
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is T target)
            {
                return target;
            }

            dependencyObject = GetParentObject(dependencyObject);
        }

        return null;
    }

    private void OnCopyRowsClick(object sender, RoutedEventArgs e)
    {
        var grid = GetContextMenuOwnerGrid(sender);
        if (grid is null) return;
        CopySelectedRows(grid, messageOnly: false);
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void OnLoadOptionFullClick(object sender, RoutedEventArgs e)
        => _viewModel.SelectLoadOption(LogLoadMode.Full);

    private void OnLoadOptionFilteredClick(object sender, RoutedEventArgs e)
        => _viewModel.SelectLoadOption(LogLoadMode.Filtered);

    private void OnLoadOptionCancelClick(object sender, RoutedEventArgs e)
        => _viewModel.SelectLoadOption(null);

    private void OnLoadOptionBackdropMouseDown(object sender, MouseButtonEventArgs e)
        => _viewModel.SelectLoadOption(null);

    private void OnCancelFileLoadClick(object sender, RoutedEventArgs e)
    {
        _viewModel.CancelFileLoad();
    }

    private void OnCopyMessageClick(object sender, RoutedEventArgs e)
    {
        var grid = GetContextMenuOwnerGrid(sender);
        if (grid is null) return;
        CopySelectedRows(grid, messageOnly: true);
    }

    private static DataGrid? GetContextMenuOwnerGrid(object sender)
    {
        if (sender is MenuItem { Parent: ContextMenu contextMenu })
        {
            return contextMenu.PlacementTarget as DataGrid;
        }
        return null;
    }

    private static void CopySelectedRows(DataGrid grid, bool messageOnly)
    {
        var selectedItems = grid.SelectedItems;
        if (selectedItems is null || selectedItems.Count == 0) return;

        // Copy items into a list and sort by RowId to guarantee display order,
        // because WPF SelectedItems returns items in selection order (not display order).
        var items = new List<object>(selectedItems.Count);
        foreach (var item in selectedItems)
            items.Add(item);
        items.Sort((a, b) =>
        {
            long rowA = a is LogRowModel la ? la.RowId : a is SearchResultRowModel sa ? sa.RowId : 0;
            long rowB = b is LogRowModel lb ? lb.RowId : b is SearchResultRowModel sb ? sb.RowId : 0;
            return rowA.CompareTo(rowB);
        });

        var sb = new System.Text.StringBuilder();
        foreach (var item in items)
        {
            string? rowId = null, timestamp = null, level = null, tag = null, pid = null, tid = null, message = null;

            if (item is LogRowModel logRow)
            {
                rowId = logRow.RowId.ToString();
                timestamp = logRow.Timestamp;
                level = logRow.Level;
                tag = logRow.Tag;
                pid = logRow.Pid;
                tid = logRow.Tid;
                message = logRow.Message;
            }
            else if (item is SearchResultRowModel searchRow)
            {
                rowId = searchRow.RowId.ToString();
                timestamp = searchRow.Timestamp;
                tag = searchRow.Tag;
                pid = searchRow.Pid;
                message = searchRow.Message;
            }
            else
            {
                continue;
            }

            if (messageOnly)
            {
                sb.AppendLine(message);
            }
            else
            {
                sb.AppendLine($"{rowId}\t{timestamp}\t{level}\t{tag}\t{pid}\t{tid}\t{message}");
            }
        }

        if (sb.Length > 0)
        {
            Clipboard.SetDataObject(sb.ToString());
        }
    }

    private static DependencyObject? GetParentObject(DependencyObject dependencyObject)
    {
        return dependencyObject switch
        {
            Visual or Visual3D => VisualTreeHelper.GetParent(dependencyObject),
            FrameworkContentElement frameworkContentElement => frameworkContentElement.Parent,
            ContentElement contentElement => ContentOperations.GetParent(contentElement),
            _ => LogicalTreeHelper.GetParent(dependencyObject)
        };
    }

    private static bool TryGetFilterPreset(DependencyObject? source, out FilterPresetModel preset)
    {
        preset = null!;
        var listBoxItem = FindAncestor<ListBoxItem>(source);
        if (listBoxItem?.DataContext is not FilterPresetModel filterPreset)
        {
            return false;
        }

        preset = filterPreset;
        return true;
    }

    private static bool TryGetContextMenuPreset(object sender, out FilterPresetModel preset)
    {
        preset = null!;
        if (sender is not MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement { DataContext: FilterPresetModel filterPreset } } })
        {
            return false;
        }

        preset = filterPreset;
        return true;
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;
            var result = FindDescendant<T>(child);
            if (result is not null)
                return result;
        }
        return null;
    }
}


