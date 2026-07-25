using Bukit.Config;

namespace Bukit.Engine;

internal interface IBuildReportWriter
{
    string Name { get; }

    void Write(BuildReportWriterContext context);
}

internal sealed record BuildReportWriterContext(
    AppConfig Config,
    string ReportDir,
    string OutputDir,
    BuildResult Result,
    IReadOnlyList<BuildVariantResult> Variants,
    bool ReportsEnabled);

internal static class BuildReportWriterPlan
{
    private static readonly IReadOnlyList<IBuildReportWriter> EnabledWriters =
    [
        new BuildSummaryReportWriter(),
        new RoutesReportWriter(),
        new AssetsReportWriter(),
        new IncrementalManifestReportWriter(),
        new PublishUrlSnapshotReportWriter(),
        new ReleaseBundleChecksumsReportWriter(),
        new ArtifactManifestReportWriter(),
        new BuildManifestDigestReportWriter()
    ];

    private static readonly IReadOnlyList<IBuildReportWriter> DisabledWriters =
    [
        new ReleaseBundleChecksumsReportWriter(),
        new ArtifactManifestReportWriter(),
        new BuildManifestDigestReportWriter()
    ];

    internal static IReadOnlyList<IBuildReportWriter> Create(bool enabled)
        => enabled ? EnabledWriters : DisabledWriters;
}
