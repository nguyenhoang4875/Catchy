using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NUnit.Framework;

namespace EasyLog.UiTests;

/// <summary>
/// Verifies that ON/OFF badges in the filter rules ListBox are rendered at
/// the same horizontal position regardless of filter name or summary length.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class FilterRuleBadgeAlignmentTests
{
    [SetUp]
    public void SetUp()
    {
        if (Application.Current is null)
        {
            new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        }
    }

    [Test]
    public void OnOffBadges_ShouldRenderAtSameHorizontalPosition()
    {
        // Build a ListBox that mirrors the production filter rules layout
        var listBox = new ListBox
        {
            Width = 400,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };

        // Use a DataTemplate identical to the production one (simplified for the
        // alignment-critical parts: DockPanel → Badge(Dock=Right) → Name)
        var template = new DataTemplate();
        var dockPanelFactory = new FrameworkElementFactory(typeof(DockPanel));

        // Badge border (Dock=Right, first child)
        var badgeBorderFactory = new FrameworkElementFactory(typeof(Border));
        badgeBorderFactory.SetValue(DockPanel.DockProperty, Dock.Right);
        badgeBorderFactory.SetValue(Border.MinWidthProperty, 36.0);
        badgeBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        badgeBorderFactory.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
        badgeBorderFactory.SetValue(Border.BackgroundProperty, Brushes.Green);
        badgeBorderFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        badgeBorderFactory.SetValue(FrameworkElement.NameProperty, "Badge");

        var badgeTextFactory = new FrameworkElementFactory(typeof(TextBlock));
        badgeTextFactory.SetValue(TextBlock.TextProperty, "ON");
        badgeTextFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        badgeBorderFactory.AppendChild(badgeTextFactory);
        dockPanelFactory.AppendChild(badgeBorderFactory);

        // Name (last child, fills remaining space)
        var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
        nameFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
        nameFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        dockPanelFactory.AppendChild(nameFactory);

        template.VisualTree = dockPanelFactory;
        listBox.ItemTemplate = template;

        // Items with very different name lengths
        var items = new ObservableCollection<TestFilterItem>
        {
            new("A"),
            new("Rule With A Moderately Long Name Here"),
            new("X"),
            new("Another Very Very Long Filter Rule Name For Testing"),
        };
        listBox.ItemsSource = items;

        // Host in a Window to trigger layout
        var window = new Window
        {
            Width = 450,
            Height = 400,
            Content = listBox,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        // Force layout
        listBox.UpdateLayout();

        // Collect badge right-edge X positions
        var badgeRightEdges = new List<double>();
        for (int i = 0; i < items.Count; i++)
        {
            var container = (ListBoxItem)listBox.ItemContainerGenerator.ContainerFromIndex(i);
            Assert.That(container, Is.Not.Null, $"Container for item {i} is null");

            var badge = FindDescendantByName<Border>(container, "Badge");
            Assert.That(badge, Is.Not.Null, $"Badge border not found for item {i}");

            // Get the badge's right edge relative to the ListBox
            var transform = badge!.TransformToAncestor(listBox);
            var topLeft = transform.Transform(new Point(0, 0));
            var rightEdge = topLeft.X + badge.ActualWidth;
            badgeRightEdges.Add(rightEdge);
        }

        window.Close();

        // All badge right edges should be at the same X position (within 1px tolerance)
        var firstEdge = badgeRightEdges[0];
        for (int i = 1; i < badgeRightEdges.Count; i++)
        {
            Assert.That(badgeRightEdges[i], Is.EqualTo(firstEdge).Within(1.0),
                $"Badge right edge for item {i} ({badgeRightEdges[i]:F1}) differs from item 0 ({firstEdge:F1}). " +
                $"All edges: [{string.Join(", ", badgeRightEdges.Select(e => e.ToString("F1")))}]");
        }
    }

    private sealed class TestFilterItem
    {
        public string Name { get; }
        public TestFilterItem(string name) => Name = name;
    }

    private static T? FindDescendantByName<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name)
                return fe;
            var result = FindDescendantByName<T>(child, name);
            if (result is not null)
                return result;
        }
        return null;
    }
}


