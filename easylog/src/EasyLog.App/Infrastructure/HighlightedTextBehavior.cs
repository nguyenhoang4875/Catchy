using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace EasyLog.App.Infrastructure;

public static class HighlightedTextBehavior
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(HighlightedTextBehavior),
        new PropertyMetadata(string.Empty, OnHighlightPropertyChanged));

    public static readonly DependencyProperty TermsProperty = DependencyProperty.RegisterAttached(
        "Terms",
        typeof(IEnumerable<string>),
        typeof(HighlightedTextBehavior),
        new PropertyMetadata(null, OnHighlightPropertyChanged));

    public static readonly DependencyProperty HighlightBackgroundProperty = DependencyProperty.RegisterAttached(
        "HighlightBackground",
        typeof(Brush),
        typeof(HighlightedTextBehavior),
        new PropertyMetadata(Brushes.Gold, OnHighlightPropertyChanged));

    public static readonly DependencyProperty HighlightForegroundProperty = DependencyProperty.RegisterAttached(
        "HighlightForeground",
        typeof(Brush),
        typeof(HighlightedTextBehavior),
        new PropertyMetadata(Brushes.Black, OnHighlightPropertyChanged));

    public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);

    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

    public static void SetTerms(DependencyObject element, IEnumerable<string>? value) => element.SetValue(TermsProperty, value);

    public static IEnumerable<string>? GetTerms(DependencyObject element) => (IEnumerable<string>?)element.GetValue(TermsProperty);

    public static void SetHighlightBackground(DependencyObject element, Brush value) => element.SetValue(HighlightBackgroundProperty, value);

    public static Brush GetHighlightBackground(DependencyObject element) => (Brush)element.GetValue(HighlightBackgroundProperty);

    public static void SetHighlightForeground(DependencyObject element, Brush value) => element.SetValue(HighlightForegroundProperty, value);

    public static Brush GetHighlightForeground(DependencyObject element) => (Brush)element.GetValue(HighlightForegroundProperty);

    private static void OnHighlightPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
        {
            return;
        }

        ApplyHighlight(textBlock);
    }

    private static void ApplyHighlight(TextBlock textBlock)
    {
        var text = GetText(textBlock) ?? string.Empty;
        var terms = GetTerms(textBlock)?
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(term => term.Length)
            .ToArray() ?? Array.Empty<string>();

        textBlock.Inlines.Clear();
        if (text.Length == 0)
        {
            return;
        }

        if (terms.Length == 0)
        {
            textBlock.Inlines.Add(new Run(text));
            return;
        }

        var highlightBackground = GetHighlightBackground(textBlock);
        var highlightForeground = GetHighlightForeground(textBlock);
        var index = 0;
        while (index < text.Length)
        {
            var matchedTerm = FindMatchedTerm(text, index, terms);
            if (matchedTerm is null)
            {
                var nextMatchIndex = FindNextMatchIndex(text, index, terms);
                if (nextMatchIndex < 0)
                {
                    textBlock.Inlines.Add(new Run(text[index..]));
                    break;
                }

                textBlock.Inlines.Add(new Run(text[index..nextMatchIndex]));
                index = nextMatchIndex;
                continue;
            }

            var run = new Run(text.Substring(index, matchedTerm.Length))
            {
                Background = highlightBackground,
                Foreground = highlightForeground,
                FontWeight = FontWeights.SemiBold
            };
            textBlock.Inlines.Add(run);
            index += matchedTerm.Length;
        }
    }

    private static string? FindMatchedTerm(string text, int startIndex, IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (startIndex + term.Length > text.Length)
            {
                continue;
            }

            if (text.AsSpan(startIndex, term.Length).Equals(term.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return term;
            }
        }

        return null;
    }

    private static int FindNextMatchIndex(string text, int startIndex, IReadOnlyList<string> terms)
    {
        var nextIndex = -1;
        foreach (var term in terms)
        {
            var matchIndex = text.IndexOf(term, startIndex, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
            {
                continue;
            }

            if (nextIndex < 0 || matchIndex < nextIndex)
            {
                nextIndex = matchIndex;
            }
        }

        return nextIndex;
    }
}

