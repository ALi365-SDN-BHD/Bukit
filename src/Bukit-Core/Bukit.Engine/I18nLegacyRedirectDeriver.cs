using System.Runtime.CompilerServices;
using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// With the default language at the root, its old prefixed URLs get redirect pages back to the
/// unprefixed pages unless user content already owns those paths: the home page through
/// <c>site.defaultLanguageRedirect</c>, every other HTML page through
/// <c>site.defaultLanguageLegacyRedirects</c>.
/// </summary>
internal static class I18nLegacyRedirectDeriver
{
    private const int MaxReportedSkippedPaths = 10;

    /// <summary>Test probe counting occupancy-set builds, so tests can assert one build per derive.</summary>
    internal static readonly AsyncLocal<StrongBox<int>?> OccupancyBuildsProbe = new();

    internal static void Derive(
        BuildVariantContext context,
        BuildContext pluginContext,
        IReadOnlyList<RouteInfo> listRoutes)
    {
        var site = context.Config.Site;
        if ((!site.DefaultLanguageRedirect && !site.DefaultLanguageLegacyRedirects) ||
            !I18nOutputLayout.IsRootLanguageVariant(context.Config))
        {
            return;
        }

        var language = site.Language.Trim();
        var occupancy = new Occupancy(context, pluginContext, listRoutes);
        var fallbackPublishAt = pluginContext.RoutedDocuments.Count > 0
            ? pluginContext.RoutedDocuments[0].Document.PublishAt
            : DateTimeOffset.UnixEpoch;
        // Sources are read before any redirect is added, so redirects never feed themselves.
        var candidates = site.DefaultLanguageLegacyRedirects
            ? CollectCandidates(pluginContext, listRoutes, language, fallbackPublishAt)
            : [];

        if (site.DefaultLanguageRedirect)
        {
            DeriveHome(context, pluginContext, occupancy, language, fallbackPublishAt);
        }
        else if (site.DefaultLanguageLegacyRedirects)
        {
            pluginContext.Logger.Info(
                $"event=i18n.legacy_redirects.home_skipped path={language}/index.html reason=defaultLanguageRedirect-disabled");
        }

        if (site.DefaultLanguageLegacyRedirects)
        {
            DeriveLegacy(context, pluginContext, occupancy, language, candidates);
        }
    }

    private static void DeriveHome(
        BuildVariantContext context,
        BuildContext pluginContext,
        Occupancy occupancy,
        string language,
        DateTimeOffset publishAt)
    {
        var outputPath = BuildPathUtils.NormalizeRelPath(Path.Combine(language, "index.html"));
        if (occupancy.IsOccupied(outputPath))
        {
            pluginContext.Logger.Info(
                $"event=i18n.legacy_root_redirect.skipped path={outputPath} reason=user-output");
            return;
        }

        var target = SeoModelBuilder.BuildAbsoluteUrl(context.Config.Site.Url, context.BaseUrl, "/");
        var derived = RedirectPageDocument.Create(
            id: $"i18n-legacy-home-{language}",
            title: "Redirect",
            slug: $"i18n-legacy-home-{language}",
            publishAt: publishAt,
            targetUrl: target,
            routeUrl: "/" + language + "/",
            outputPath: outputPath);
        Add(pluginContext, occupancy, derived);
    }

    private static void DeriveLegacy(
        BuildVariantContext context,
        BuildContext pluginContext,
        Occupancy occupancy,
        string language,
        IReadOnlyList<Candidate> candidates)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skippedPaths = new List<string>();
        var count = 0;
        var skipped = 0;
        long bytes = 0;
        foreach (var candidate in candidates)
        {
            if (!seen.Add(candidate.OutputPath))
            {
                continue;
            }

            if (occupancy.IsOccupied(candidate.OutputPath))
            {
                skipped++;
                if (skippedPaths.Count < MaxReportedSkippedPaths)
                {
                    skippedPaths.Add(candidate.OutputPath);
                }

                continue;
            }

            // Staging keeps internal links from the previous output, e.g. en/blog -> zh-CN/blog (analysis 4.8 a).
            BuildTransaction.RejectLinkSegments(context.OutputDir, candidate.OutputPath);
            var target = SeoModelBuilder.BuildAbsoluteUrl(context.Config.Site.Url, context.BaseUrl, candidate.TargetRouteUrl);
            var key = $"i18n-legacy-{language}-{HashUtil.Sha256Hex(candidate.OutputPath.ToLowerInvariant())[..16]}";
            var derived = RedirectPageDocument.Create(
                id: key,
                title: "Redirect",
                slug: key,
                publishAt: candidate.PublishAt,
                targetUrl: target,
                routeUrl: candidate.RouteUrl,
                outputPath: candidate.OutputPath);
            Add(pluginContext, occupancy, derived);
            count++;
            bytes += Encoding.UTF8.GetByteCount(RedirectPageHtml.Build(target));
        }

        var message = $"event=i18n.legacy_redirects.derived count={count} skipped={skipped} bytes={bytes}";
        if (skippedPaths.Count > 0)
        {
            message += $" skippedPaths={string.Join(",", skippedPaths)}";
        }

        pluginContext.Logger.Info(message);
    }

    private static List<Candidate> CollectCandidates(
        BuildContext pluginContext,
        IReadOnlyList<RouteInfo> listRoutes,
        string language,
        DateTimeOffset fallbackPublishAt)
    {
        var candidates = new List<Candidate>();
        foreach (var routed in pluginContext.RoutedDocuments)
        {
            AddPage(candidates, routed.Route, routed.Document.PublishAt, language);
        }

        foreach (var route in listRoutes)
        {
            AddPage(candidates, route, fallbackPublishAt, language);
        }

        foreach (var derived in pluginContext.DerivedDocuments)
        {
            if (!IsRedirect(derived))
            {
                AddPage(candidates, derived.Route, derived.Document.PublishAt, language);
                continue;
            }

            // Only alias redirects carry a target; the legacy home and other redirects are not migrated.
            var aliasTarget = ContentFieldReader.GetText(derived.Document.CustomFields, RedirectPageDocument.RedirectTargetField);
            if (string.IsNullOrWhiteSpace(aliasTarget) ||
                string.Equals(FirstSegment(derived.Route.OutputPath), language, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddCandidate(candidates, derived.Route, aliasTarget, derived.Document.PublishAt, language);
        }

        foreach (var route in pluginContext.StaticHtmlRoutes)
        {
            AddPage(candidates, route, fallbackPublishAt, language);
        }

        return candidates;
    }

    private static void AddPage(List<Candidate> candidates, RouteInfo route, DateTimeOffset publishAt, string language)
        => AddCandidate(candidates, route, route.Url, publishAt, language);

    private static void AddCandidate(
        List<Candidate> candidates,
        RouteInfo route,
        string targetRouteUrl,
        DateTimeOffset publishAt,
        string language)
    {
        var outputPath = BuildPathUtils.NormalizeRelPath(route.OutputPath).TrimStart('/');
        var extension = Path.GetExtension(outputPath);
        if (outputPath.Length == 0 ||
            string.Equals(outputPath, "index.html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(outputPath, "404.html", StringComparison.OrdinalIgnoreCase) ||
            !(extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
              extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var routeUrl = route.Url.StartsWith('/') ? route.Url : "/" + route.Url;
        var target = targetRouteUrl.StartsWith('/') ? targetRouteUrl : "/" + targetRouteUrl;
        candidates.Add(new Candidate(
            language + "/" + outputPath,
            "/" + language + routeUrl,
            target,
            publishAt));
    }

    private static bool IsRedirect(RoutedContentDocument document)
        => string.Equals(document.Route.Template, RedirectPageHtml.RouteTemplate, StringComparison.Ordinal) ||
           string.Equals(ContentFieldReader.GetText(document.Document.CustomFields, "type"), "redirect", StringComparison.OrdinalIgnoreCase);

    private static string FirstSegment(string path)
        => BuildPathUtils.NormalizeRelPath(path).TrimStart('/').Split('/')[0];

    private static void Add(BuildContext pluginContext, Occupancy occupancy, RoutedContentDocument derived)
    {
        pluginContext.DerivedDocuments.Add(derived);
        pluginContext.DerivedRoutes.Add((derived.Route, derived.Document.PublishAt));
        occupancy.Claim(derived.Route.OutputPath);
    }

    private sealed record Candidate(string OutputPath, string RouteUrl, string TargetRouteUrl, DateTimeOffset PublishAt);

    /// <summary>Every path already produced by user content, built once so each lookup is constant time.</summary>
    private sealed class Occupancy
    {
        private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
        private readonly BuildVariantContext _context;
        private readonly DirectoryCopyOptions _staticOptions;

        internal Occupancy(BuildVariantContext context, BuildContext pluginContext, IReadOnlyList<RouteInfo> listRoutes)
        {
            if (OccupancyBuildsProbe.Value is { } probe)
            {
                probe.Value++;
            }

            _context = context;
            _staticOptions = I18nOutputLayout.StaticCopyOptions(context.Config.Build);
            foreach (var document in pluginContext.RoutedDocuments) Claim(document.Route.OutputPath);
            foreach (var document in pluginContext.DerivedDocuments) Claim(document.Route.OutputPath);
            foreach (var route in listRoutes) Claim(route.OutputPath);
            foreach (var route in pluginContext.StaticHtmlRoutes) Claim(route.OutputPath);
        }

        internal void Claim(string path) => _paths.Add(Normalize(path));

        internal bool IsOccupied(string path)
            => _paths.Contains(Normalize(path)) ||
               DirectoryCopy.StaticSourceExists(_context.StaticDir, path, _staticOptions) ||
               (_context.ParentStaticDir is not null &&
                DirectoryCopy.StaticSourceExists(_context.ParentStaticDir, path, _staticOptions));

        private static string Normalize(string path) => BuildPathUtils.NormalizeRelPath(path).TrimStart('/');
    }
}
