using System.Text.Json.Serialization;
using System.Windows.Media;
using EasyLog.App.Infrastructure;
using EasyLog.Contracts.Enums;
using EasyLog.Contracts.Models;

namespace EasyLog.App.Models;

public sealed class FilterPresetModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _tagFilterText = string.Empty;
    private string _pidFilterText = string.Empty;
    private string _textFilterText = string.Empty;
    private string _excludedTagFilterText = string.Empty;
    private string _excludedPidFilterText = string.Empty;
    private string _excludedTextFilterText = string.Empty;
    private bool _isVerboseEnabled = true;
    private bool _isDebugEnabled = true;
    private bool _isInfoEnabled = true;
    private bool _isWarnEnabled = true;
    private bool _isErrorEnabled = true;
    private bool _isFatalEnabled = true;
    private bool _isEnabled = true;
    private bool _isBatchSelected;
    private string _colorHex = "#1D4ED8";

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                RaisePropertyChanged(nameof(Summary));
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
                RaisePropertyChanged(nameof(Summary));
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
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public string TextFilterText
    {
        get => _textFilterText;
        set
        {
            if (SetProperty(ref _textFilterText, value))
            {
                RaisePropertyChanged(nameof(Summary));
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
                RaisePropertyChanged(nameof(Summary));
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
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public string ExcludedTextFilterText
    {
        get => _excludedTextFilterText;
        set
        {
            if (SetProperty(ref _excludedTextFilterText, value))
            {
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public bool IsVerboseEnabled
    {
        get => _isVerboseEnabled;
        set
        {
            if (SetProperty(ref _isVerboseEnabled, value))
            {
                RaisePropertyChanged(nameof(Summary));
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
                RaisePropertyChanged(nameof(Summary));
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
                RaisePropertyChanged(nameof(Summary));
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
                RaisePropertyChanged(nameof(Summary));
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
                RaisePropertyChanged(nameof(Summary));
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
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    [JsonIgnore]
    public bool IsBatchSelected
    {
        get => _isBatchSelected;
        set => SetProperty(ref _isBatchSelected, value);
    }

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (SetProperty(ref _colorHex, value))
            {
                RaisePropertyChanged(nameof(ColorBrush));
                RaisePropertyChanged(nameof(ForegroundBrush));
            }
        }
    }

    [JsonIgnore]
    public Brush ColorBrush => CreateBrush(ColorHex);

    [JsonIgnore]
    public Brush ForegroundBrush => CreateReadableForeground(ColorHex);

    [JsonIgnore]
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(TextFilterText)) parts.Add($"Text: {TextFilterText}");
            if (!string.IsNullOrWhiteSpace(TagFilterText)) parts.Add($"Tag: {TagFilterText}");
            if (!string.IsNullOrWhiteSpace(PidFilterText)) parts.Add($"PID: {PidFilterText}");
            if (!string.IsNullOrWhiteSpace(ExcludedTextFilterText)) parts.Add($"Exclude Text: {ExcludedTextFilterText}");
            if (!string.IsNullOrWhiteSpace(ExcludedTagFilterText)) parts.Add($"Exclude Tag: {ExcludedTagFilterText}");
            if (!string.IsNullOrWhiteSpace(ExcludedPidFilterText)) parts.Add($"Exclude PID: {ExcludedPidFilterText}");

            var levels = new List<string>(6);
            if (IsVerboseEnabled) levels.Add("V");
            if (IsDebugEnabled) levels.Add("D");
            if (IsInfoEnabled) levels.Add("I");
            if (IsWarnEnabled) levels.Add("W");
            if (IsErrorEnabled) levels.Add("E");
            if (IsFatalEnabled) levels.Add("F");
            if (levels.Count is > 0 and < 6) parts.Add($"Levels: {string.Join('/', levels)}");

            return parts.Count == 0 ? "Match all logs" : string.Join(" | ", parts);
        }
    }

    public FilterQuery ToFilterQuery(Func<string, IReadOnlyCollection<string>?> parseTerms, Func<string, IReadOnlyCollection<int>?> parsePids)
    {
        var levels = GetSelectedLevels();
        return new FilterQuery(
            Levels: levels.Count == 0 ? null : levels,
            TagContains: NullIfWhiteSpace(TagFilterText),
            Pid: null,
            TextContains: NullIfWhiteSpace(TextFilterText),
            TagTerms: parseTerms(TagFilterText),
            Pids: parsePids(PidFilterText),
            TextTerms: parseTerms(TextFilterText),
            ExcludedTagTerms: parseTerms(ExcludedTagFilterText),
            ExcludedPids: parsePids(ExcludedPidFilterText),
            ExcludedTextTerms: parseTerms(ExcludedTextFilterText));
    }

    public void LoadFrom(FilterPresetModel other)
    {
        Name = other.Name;
        TagFilterText = other.TagFilterText;
        PidFilterText = other.PidFilterText;
        TextFilterText = other.TextFilterText;
        ExcludedTagFilterText = other.ExcludedTagFilterText;
        ExcludedPidFilterText = other.ExcludedPidFilterText;
        ExcludedTextFilterText = other.ExcludedTextFilterText;
        IsVerboseEnabled = other.IsVerboseEnabled;
        IsDebugEnabled = other.IsDebugEnabled;
        IsInfoEnabled = other.IsInfoEnabled;
        IsWarnEnabled = other.IsWarnEnabled;
        IsErrorEnabled = other.IsErrorEnabled;
        IsFatalEnabled = other.IsFatalEnabled;
        IsEnabled = other.IsEnabled;
        ColorHex = other.ColorHex;
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

    private static Brush CreateBrush(string colorHex)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(colorHex)!;
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }

    private static Brush CreateReadableForeground(string backgroundHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(backgroundHex);
        var luminance = ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255d;
        var foreground = luminance > 0.6 ? Brushes.Black : Brushes.White;
        if (foreground.CanFreeze)
        {
            foreground.Freeze();
        }

        return foreground;
    }
}

