using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class AliasPlugin : IBukitPlugin, IDerivePagesPlugin
{
    private readonly AppConfig _config;

    internal AliasPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "alias";
    public string Version => "1.0.0";

    public IReadOnlyList<RoutedContentDocument> DerivePages(BuildContext context)
    {
        var derived = new List<RoutedContentDocument>();
        var baseUrl = context.BaseUrl == "/" ? "" : context.BaseUrl;

        foreach (var routedDocument in context.RoutedDocuments)
        {
            var document = routedDocument.Document;
            var route = routedDocument.Route;
            var aliases = ContentFieldReader.GetTextList(document.CustomFields, "aliases");
            if (aliases is null)
            {
                continue;
            }

            foreach (var alias in aliases)
            {
                var decodedAlias = RoutePathBuilder.DecodePercentEncodedUrl(alias);
                if (!string.Equals(decodedAlias, alias, StringComparison.Ordinal))
                {
                    context.Logger.Warn($"event=route.alias.decoded id={document.Id} raw=\"{alias}\" normalized=\"{decodedAlias}\" hint=fix_alias_in_source");
                }

                var aliasUrl = decodedAlias.StartsWith('/') ? decodedAlias : "/" + decodedAlias;
                if (!aliasUrl.EndsWith('/'))
                {
                    aliasUrl += "/";
                }

                var targetUrl = route.Url.StartsWith('/') ? route.Url : "/" + route.Url;

                var html = RedirectPageHtml.Build($"{baseUrl}{targetUrl}");
                var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(aliasUrl, _config.Site.OutputPathEncoding);
                var aliasRoute = new RouteInfo(aliasUrl, outputPath, RedirectPageHtml.RouteTemplate);
                var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["type"] = new("text", "redirect"),
                    ["collection"] = new("text", "redirect"),
                    ["excludeFromSitemap"] = new("bool", true)
                };

                var aliasDocument = DerivedContentDocumentFactory.Create(
                    id: $"alias-{document.Id}-{EscapePath(alias)}",
                    title: $"[Redirect] {document.Title}",
                    slug: $"alias-{document.Slug}",
                    publishAt: document.PublishAt,
                    body: new ContentBodyRef(Html: html),
                    customFields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = "redirect",
                        ["sitemap"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["exclude"] = true
                        }
                    }));

                derived.Add(new RoutedContentDocument(aliasDocument, aliasRoute, document.PublishAt));
            }
        }

        return derived;
    }

    private static string EscapePath(string path)
    {
        return path.Replace("/", "-", StringComparison.Ordinal)
            .Replace("\\", "-", StringComparison.Ordinal)
            .TrimStart('-');
    }

}
