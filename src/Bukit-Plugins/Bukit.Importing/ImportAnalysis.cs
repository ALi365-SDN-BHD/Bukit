namespace Bukit.Importing;

/// <summary>
/// Holds all analysis results from scanning and extracting an HTML demo directory.
/// This is a pure data object — no side effects, no file I/O.
/// </summary>
internal sealed record ImportAnalysis(
    List<DiscoveredPage> Pages,
    LayoutExtractor.LayoutInfo Layout,
    List<string> Warnings,
    List<ImportDiagnostic> Diagnostics,
    List<DiscoveredComponent> Components,
    ExtractedContent Content,
    RouteMapConfig? RouteMap,
    AssetImporter.AssetImportResult AssetResult);
