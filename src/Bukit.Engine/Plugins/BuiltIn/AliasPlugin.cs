using System.Text;
using Bukit.Content;
using Bukit.Routing;

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
            var aliases = GetAliases(item.Meta);
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
                    Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = "redirect",
                        ["sitemap"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["exclude"] = true
                        }
                    });

                derived.Add((aliasItem, aliasRoute, item.PublishAt));
            }
        }

        return derived;
    }

    private static IReadOnlyList<string>? GetAliases(IReadOnlyDictionary<string, object> meta)
    {
        if (!meta.TryGetValue("aliases", out var value) || value is null)
        {
            return null;
        }

        if (value is IEnumerable<object> seq)
        {
            var list = seq.Select(x => x?.ToString() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return list.Count == 0 ? null : list;
        }

        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            return new[] { s };
        }

        return null;
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
