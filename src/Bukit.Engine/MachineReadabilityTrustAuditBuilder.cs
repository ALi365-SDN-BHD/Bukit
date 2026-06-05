using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;

namespace Bukit.Engine;

internal sealed record MachineReadabilityTrustAuditResult(
    SeoAuditReport SeoReport,
    PublishAuditReport PublishReport);

internal static class MachineReadabilityTrustAuditBuilder
{
    internal static MachineReadabilityTrustAuditResult Build(
        AppConfig config,
        string outputDir,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyDictionary<string, SeoModel> seoModels,
        CanonicalContentGraph? contentGraph = null,
        bool requireHreflangTargets = true)
        => SeoAuditReportWriter.BuildMachineReadabilityTrustAudit(
            config,
            outputDir,
            seoIndex,
            seoModels,
            contentGraph,
            requireHreflangTargets);
}
