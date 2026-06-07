using System.Text;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class AliasPlugin : IBukitPlugin, IDerivePagesPlugin
{
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
                var aliasUrl = alias.StartsWith('/') ? alias : "/" + alias;
                if (!aliasUrl.EndsWith('/'))
                {
                    aliasUrl += "/";
                }

                var targetUrl = route.Url.StartsWith('/') ? route.Url : "/" + route.Url;

                var html = BuildRedirectHtml($"{baseUrl}{targetUrl}");
                var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(aliasUrl, context.Config.Site.OutputPathEncoding);
                var aliasRoute = new RouteInfo(aliasUrl, outputPath, null!);

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

    private static string BuildRedirectHtml(string targetUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine($"<meta http-equiv=\"refresh\" content=\"0; url={EscapeAttr(targetUrl)}\">");
        sb.AppendLine($"<link rel=\"canonical\" href=\"{EscapeAttr(targetUrl)}\">");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"<p>Redirecting to <a href=\"{EscapeAttr(targetUrl)}\">{EscapeHtml(targetUrl)}</a></p>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string EscapePath(string path)
    {
        return path.Replace("/", "-", StringComparison.Ordinal)
            .Replace("\\", "-", StringComparison.Ordinal)
            .TrimStart('-');
    }

    private static string EscapeHtml(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string EscapeAttr(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
