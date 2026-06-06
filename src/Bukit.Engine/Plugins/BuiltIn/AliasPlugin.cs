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

    public IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> DerivePages(BuildContext context)
    {
        var derived = new List<(ContentItem, RouteInfo, DateTimeOffset)>();
        var baseUrl = context.BaseUrl == "/" ? "" : context.BaseUrl;

        foreach (var (item, route) in context.Routed)
        {
            var aliases = ContentFieldReader.GetTextList(item.Fields, "aliases");
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

                var aliasItem = new ContentItem(
                    Id: $"alias-{item.Id}-{EscapePath(alias)}",
                    Title: $"[Redirect] {item.Title}",
                    Slug: $"alias-{item.Slug}",
                    PublishAt: item.PublishAt,
                    ContentHtml: html,
                    Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = "redirect",
                        ["sitemap"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["exclude"] = true
                        }
                    }));

                derived.Add((aliasItem, aliasRoute, item.PublishAt));
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
