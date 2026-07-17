using System.Reflection;

namespace EasyLog.App.Infrastructure;

public static class AppMetadata
{
    public const string ProductName = "LogPilot - AAOS Log Viewer";
    public const string ShortProductName = "LogPilot";
    public const string ProductFolderName = "LogPilot";

    public static string VersionDisplay { get; } = ResolveVersionDisplay();

    public static string FormatWindowTitle(string? pageTitle = null) =>
        string.IsNullOrWhiteSpace(pageTitle)
            ? $"{ProductName} {VersionDisplay}"
            : $"{ProductName} {VersionDisplay} - {pageTitle}";

    private static string ResolveVersionDisplay()
    {
        var assembly = typeof(AppMetadata).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+', StringSplitOptions.RemoveEmptyEntries)[0];

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        var version = assembly.GetName().Version;
        if (version is null)
        {
            return "v0.0.0";
        }

        var build = version.Build >= 0 ? version.Build : 0;
        return $"v{version.Major}.{version.Minor}.{build}";
    }
}

