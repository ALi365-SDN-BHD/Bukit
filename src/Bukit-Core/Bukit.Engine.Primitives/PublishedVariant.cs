using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;

namespace Bukit.Engine;

/// <summary>
/// The published content of one language variant, as the publish audits read it.
/// Engine builds it from its build result so audit code never depends on Engine types.
/// </summary>
internal sealed record PublishedVariant(
    string Language,
    string BaseUrl,
    IContentBodyStore BodyStore,
    IReadOnlyDictionary<string, SeoIndexEntry> SeoIndex,
    IReadOnlyDictionary<string, SeoModel> SeoModels,
    IReadOnlyList<RoutedContentDocument> RoutedDocuments,
    IReadOnlyList<RoutedContentDocument> DerivedDocuments,
    CanonicalContentGraph? ContentGraph,
    IReadOnlyList<PublishProjectionResult> ProjectionResults);
