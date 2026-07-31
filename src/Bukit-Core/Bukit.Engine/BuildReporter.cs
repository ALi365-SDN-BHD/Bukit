using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class BuildReporter
{
    internal const string ReportDirectoryName = ".bukit";
    internal const string ArtifactSchemaVersion = "1.0";
    internal const string BuildReportSchema = "https://bukit.dev/schemas/build-report.v1.json";
    internal const string RoutesReportSchema = "https://bukit.dev/schemas/routes.v1.json";
    internal const string AssetsReportSchema = "https://bukit.dev/schemas/assets.v1.json";
    internal const string IncrementalManifestSchema = "https://bukit.dev/schemas/incremental-manifest.v1.json";
    internal const string SecurityReportSchema = "https://bukit.dev/schemas/security-report.v1.json";
    internal const string ArtifactManifestSchema = "https://bukit.dev/schemas/artifact-manifest.v1.json";
    internal const string ReleaseBundleChecksumsSchema = "https://bukit.dev/schemas/release-bundle-checksums.v1.json";
    internal const string BuildManifestDigestSchema = "https://bukit.dev/schemas/build-manifest-digest.v1.json";
    internal const string PublishUrlSnapshotSchema = "https://bukit.dev/schemas/publish-url-snapshot.v1.json";

    internal static async Task WriteIfEnabledAsync(
        AppConfig config,
        string rootDir,
        string outputDir,
        BuildResult result,
        IReadOnlyList<BuildVariantResult> variants,
        ILogger logger,
        SecurityReportData? securityData = null,
        CancellationToken cancellationToken = default)
    {
        var reportDir = Path.Combine(outputDir, ReportDirectoryName);
        Directory.CreateDirectory(reportDir);
        BuildReporterSecurity.WriteSecurityReport(Path.Combine(reportDir, "security-report.json"), config, securityData);

        var context = new BuildReportWriterContext(
            config,
            reportDir,
            outputDir,
            result,
            variants,
            config.Build.Report.Enabled);
        foreach (var writer in BuildReportWriterPlan.Create(config.Build.Report.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync(context, cancellationToken).ConfigureAwait(false);
        }

        logger.Debug(config.Build.Report.Enabled
            ? $"event=build.report.write dir={reportDir} root={rootDir}"
            : $"event=build.security_report.write dir={reportDir} root={rootDir}");
    }

    internal static void EnforceSecurityGate(AppConfig config, SecurityReportData? securityData, bool isCi)
        => BuildReporterSecurity.EnforceSecurityGate(config, securityData, isCi);

    internal static SecurityReportData CreateSecurityReportData(
        AppConfig config,
        string rootDir,
        string outputDir,
        IReadOnlyList<BuildVariantResult> variants)
        => BuildReporterSecurity.CreateSecurityReportData(config, rootDir, outputDir, variants);

    internal static void WriteStringArray(Utf8JsonWriter writer, IReadOnlyList<string>? values)
    {
        writer.WriteStartArray();
        foreach (var value in values ?? Array.Empty<string>())
        {
            writer.WriteStringValue(value);
        }
        writer.WriteEndArray();
    }

    internal static IEnumerable<RouteInfo> EnumerateRoutes(IReadOnlyList<BuildVariantResult> variants)
    {
        foreach (var variant in variants)
        {
            foreach (var route in variant.RoutedDocuments.Select(x => x.Route))
            {
                yield return route;
            }

            foreach (var route in variant.DerivedDocuments.Select(x => x.Route))
            {
                yield return route;
            }

            foreach (var route in variant.StaticRoutes)
            {
                yield return route;
            }
        }
    }

    internal static void WriteArtifactContract(Utf8JsonWriter writer, string schema)
    {
        writer.WriteString("schema", schema);
        writer.WriteString("schemaVersion", ArtifactSchemaVersion);
    }

    internal static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}

internal sealed record SecurityReportData(
    string RouteTraversal,
    string UnsafeSlug,
    string PluginOutputPath,
    string RemoteThemeLock,
    string PublicOutputPrivacy,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);
