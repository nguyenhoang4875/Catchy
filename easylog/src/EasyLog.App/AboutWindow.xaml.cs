using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace EasyLog.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        VersionText.Text = $"Version {version}";
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OnCopyEmailClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText("sungyeon22.kim@lge.com");
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

