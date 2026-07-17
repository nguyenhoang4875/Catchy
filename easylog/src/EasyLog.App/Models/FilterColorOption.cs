using System.Windows.Media;

namespace EasyLog.App.Models;

public sealed class FilterColorOption
{
    public FilterColorOption(string name, string backgroundHex)
    {
        Name = name;
        BackgroundHex = backgroundHex;
        BackgroundBrush = CreateBrush(backgroundHex);
        ForegroundBrush = CreateReadableForeground(backgroundHex);
    }

    public string Name { get; }

    public string BackgroundHex { get; }

    public Brush BackgroundBrush { get; }

    public Brush ForegroundBrush { get; }

    public Brush BorderBrush { get; } = Brushes.White;

    public string DisplayLabel => $"{Name} ({BackgroundHex})";

    public override string ToString() => Name;

    private static Brush CreateBrush(string colorHex)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(colorHex)!;
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }

    private static Brush CreateReadableForeground(string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        var luminance = ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255d;
        var brush = luminance > 0.6 ? Brushes.Black : Brushes.White;
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }
}

