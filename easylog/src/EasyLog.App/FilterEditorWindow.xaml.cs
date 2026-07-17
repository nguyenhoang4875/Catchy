using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using EasyLog.App.Infrastructure;
using EasyLog.App.Models;

namespace EasyLog.App;

public partial class FilterEditorWindow : Window
{
    public FilterEditorWindow(
        string dialogTitle,
        FilterPresetModel source,
        IReadOnlyCollection<FilterColorOption> colorOptions,
        bool showNameField,
        bool showColorField)
    {
        InitializeComponent();
        DialogTitle = dialogTitle;
        ShowNameField = showNameField;
        ShowColorField = showColorField;
        WorkingPreset = new FilterPresetModel();
        WorkingPreset.LoadFrom(source);
        ColorOptions = new ObservableCollection<FilterColorOption>(colorOptions);
        SelectedColor = ColorOptions.FirstOrDefault(x => string.Equals(x.BackgroundHex, WorkingPreset.ColorHex, StringComparison.OrdinalIgnoreCase))
            ?? ColorOptions.FirstOrDefault();
        DataContext = this;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public string DialogTitle { get; }

    public bool ShowNameField { get; }

    public bool ShowColorField { get; }

    public ObservableCollection<FilterColorOption> ColorOptions { get; }

    public FilterPresetModel WorkingPreset { get; }

    public FilterColorOption? SelectedColor { get; set; }

    public FilterPresetModel GetResult()
    {
        WorkingPreset.ColorHex = SelectedColor?.BackgroundHex ?? WorkingPreset.ColorHex;
        return WorkingPreset;
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        if (ShowNameField && string.IsNullOrWhiteSpace(WorkingPreset.Name))
        {
            MessageBox.Show(this, "Rule name is required.", AppMetadata.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        CloseAsCancelled();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Owner is not null)
        {
            FontFamily = Owner.FontFamily;
        }

        if (Owner is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 960;
            Height = 760;
            return;
        }

        Owner.LocationChanged += OnOwnerBoundsChanged;
        Owner.SizeChanged += OnOwnerBoundsChanged;
        Owner.StateChanged += OnOwnerBoundsChanged;
        SyncToOwnerBounds();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        Closed -= OnClosed;
        if (Owner is null)
        {
            return;
        }

        Owner.LocationChanged -= OnOwnerBoundsChanged;
        Owner.SizeChanged -= OnOwnerBoundsChanged;
        Owner.StateChanged -= OnOwnerBoundsChanged;
    }

    private void OnOwnerBoundsChanged(object? sender, EventArgs e) => SyncToOwnerBounds();

    private void SyncToOwnerBounds()
    {
        if (Owner is null)
        {
            return;
        }

        Left = Owner.Left;
        Top = Owner.Top;
        Width = Math.Max(Owner.Width, 1);
        Height = Math.Max(Owner.Height, 1);
        DialogCard.MaxWidth = Math.Max(360, Width - 48);
        DialogCard.MaxHeight = Math.Max(360, Height - 48);
        DialogCard.Width = Math.Min(620, DialogCard.MaxWidth);
        DialogCard.Height = Math.Min(700, DialogCard.MaxHeight);
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Escape)
        {
            return;
        }

        CloseAsCancelled();
        e.Handled = true;
    }

    private void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsClickInsideDialog(e.OriginalSource as DependencyObject))
        {
            return;
        }

        CloseAsCancelled();
        e.Handled = true;
    }

    private bool IsClickInsideDialog(DependencyObject? source)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, DialogCard))
            {
                return true;
            }

            source = GetParentObject(source);
        }

        return false;
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

    private void CloseAsCancelled()
    {
        DialogResult = false;
        Close();
    }
}

