using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;

namespace Bukit.Engine;

internal sealed record BuildVariantContext(
    AppConfig Config,
    string RootDir,
    ConfigOverrides Overrides,
    IReadOnlyList<ContentItem> Items,
    CanonicalContentGraph ContentGraph,
    IContentBodyStore BodyStore,
    string OutputDir,
    string BaseUrl,
    string LayoutsDir,
    string AssetsDir,
    string StaticDir,
    string MediaDownloadDir,
    IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> SeoAlternates,
    string? RootBaseUrl,
    string? ManifestSuffix,
    string? DefaultLanguage,
    string? ParentLayoutsDir = null,
    string? ParentAssetsDir = null,
    string? ParentStaticDir = null,
    string? UserLayoutsDir = null);
