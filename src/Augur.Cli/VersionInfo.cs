using System.Reflection;
using Augur.Core.Catalog;

namespace Augur.Cli;

/// <summary>The tool version and the catalog version, as printed by <c>augur --version</c>.</summary>
public static class VersionInfo
{
    public static string ToolVersion { get; } =
        typeof(VersionInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(VersionInfo).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    public static int CatalogVersion => CatalogVersion2.Version;

    public static string Render() => $"augur {ToolVersion} (catalog {CatalogVersion})";
}
