using Bukit.Config;
using Bukit.Content;

namespace Bukit.Engine;

internal sealed record BuildVariantContext(
    AppConfig Config,
    string RootDir,
    ConfigOverrides Overrides,
    IReadOnlyList<ContentItem> Items,
    IContentBodyStore BodyStore,
    string OutputDir,
    string BaseUrl,
    string LayoutsDir,
    string AssetsDir,
    string StaticDir,
    string MediaDownloadDir,
    string? ManifestSuffix,
    string? DefaultLanguage);
