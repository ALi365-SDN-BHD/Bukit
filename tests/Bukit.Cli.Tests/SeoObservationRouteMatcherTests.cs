using System.Reflection;
using Bukit.Cli.Commands.SeoInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoObservationRouteMatcherTests
{
    [Fact]
    public void Match_ExactRelativeCanonicalReturnsRouteIdentity()
    {
        var (matcher, _) = CreateMatcher(
            new RouteDefinition(
                "route:article",
                "content:article",
                "/articles/example/",
                "/articles/example/"));

        var result = matcher.Match("https://example.com/articles/example/");

        Assert.Equal(SeoObservationMatchKind.Matched, result.Kind);
        Assert.Equal("https://example.com/articles/example/", result.ObservedUrl);
        Assert.Equal("https://example.com/articles/example/", result.NormalizedUrl);
        Assert.Equal("route:article", result.RouteKey);
        Assert.Equal("content:article", result.ContentKey);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("route:article", candidate.RouteKey);
        Assert.Equal("content:article", candidate.ContentKey);
        Assert.Equal("/articles/example/", candidate.Route);
        Assert.Equal("/articles/example/", candidate.Canonical);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Match_TrackingAndDeclaredAliasVariantUsesSameMatchKey()
    {
        var (matcher, _) = CreateMatcher(
            new RouteDefinition(
                "route:article",
                null,
                "/articles/example/",
                "https://canonical.example/articles/example/?lang=zh"));

        var result = matcher.Match(
            "HTTPS://WWW.EXAMPLE.COM:443/articles/example?utm_source=newsletter&lang=zh#top");

        Assert.Equal(SeoObservationMatchKind.Matched, result.Kind);
        Assert.Equal("https://www.example.com/articles/example/?lang=zh", result.NormalizedUrl);
        Assert.Equal("route:article", result.RouteKey);
    }

    [Fact]
    public void Match_NoCandidateReturnsUnmatched()
    {
        var (matcher, _) = CreateMatcher(
            new RouteDefinition("route:known", null, "/known/", "/known/"));

        var result = matcher.Match("https://example.com/missing/");

        Assert.Equal(SeoObservationMatchKind.Unmatched, result.Kind);
        Assert.Equal("https://example.com/missing/", result.NormalizedUrl);
        Assert.Null(result.RouteKey);
        Assert.Null(result.ContentKey);
        Assert.Empty(result.Candidates);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Match_NormalizationFailureReturnsUnmatchedWithErrorCode()
    {
        var (matcher, _) = CreateMatcher(
            new RouteDefinition("route:known", null, "/known/", "/known/"));

        var result = matcher.Match("https://undeclared.example/known/");

        Assert.Equal(SeoObservationMatchKind.Unmatched, result.Kind);
        Assert.Equal("https://undeclared.example/known/", result.ObservedUrl);
        Assert.Null(result.NormalizedUrl);
        Assert.Null(result.RouteKey);
        Assert.Null(result.ContentKey);
        Assert.Empty(result.Candidates);
        Assert.Equal("host_not_allowed", result.ErrorCode);
    }

    [Fact]
    public void Match_DuplicateMatchKeyReturnsAllCandidatesInDeterministicOrder()
    {
        var (matcher, _) = CreateMatcher(
            new RouteDefinition("route:b", null, "/legacy/", "/shared/"),
            new RouteDefinition("route:a", "content:a", "/current/", "https://canonical.example/shared"));

        var result = matcher.Match("https://example.com/shared/");

        Assert.Equal(SeoObservationMatchKind.Ambiguous, result.Kind);
        Assert.Null(result.RouteKey);
        Assert.Null(result.ContentKey);
        Assert.Null(result.ErrorCode);
        Assert.Equal(["route:a", "route:b"], result.Candidates.Select(candidate => candidate.RouteKey));
        Assert.Equal("content:a", result.Candidates[0].ContentKey);
        Assert.Null(result.Candidates[1].ContentKey);
        Assert.Equal(["/current/", "/legacy/"], result.Candidates.Select(candidate => candidate.Route));
        Assert.Equal(
            ["https://canonical.example/shared", "/shared/"],
            result.Candidates.Select(candidate => candidate.Canonical));
    }

    [Fact]
    public void Construction_DoesNotMutateRouteMapEntryOrderOrValues()
    {
        var definitions = new[]
        {
            new RouteDefinition("route:z", null, "/z/", "/z/"),
            new RouteDefinition("route:a", "content:a", "/a/", "/a/")
        };
        var routeMap = CreateRouteMap(definitions);
        var before = ReadRouteMapEntries(routeMap);

        _ = CreateMatcherFromMap(routeMap);

        Assert.Equal(before, ReadRouteMapEntries(routeMap));
    }

    [Theory]
    [InlineData("/bad%ZZ/")]
    [InlineData("https://canonical.example/bad%ZZ/")]
    public void Construction_MalformedCanonicalFailsAsInvalidRouteMapData(string canonical)
    {
        var routeMap = CreateRouteMap([
            new RouteDefinition("route:bad", null, "/bad/", canonical)
        ]);

        var exception = Assert.Throws<TargetInvocationException>(() => CreateMatcherFromMap(routeMap));

        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    private static (SeoObservationRouteMatcher Matcher, object RouteMap) CreateMatcher(
        params RouteDefinition[] routes)
    {
        var routeMap = CreateRouteMap(routes);
        return (CreateMatcherFromMap(routeMap), routeMap);
    }

    private static SeoObservationRouteMatcher CreateMatcherFromMap(object routeMap)
    {
        var constructor = typeof(SeoObservationRouteMatcher)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return (SeoObservationRouteMatcher)constructor.Invoke([routeMap, Options()]);
    }

    private static object CreateRouteMap(IReadOnlyList<RouteDefinition> routes)
    {
        var engineAssembly = Assembly.Load("Bukit.Engine");
        var entryType = engineAssembly.GetType("Bukit.Engine.SeoRouteMapEntry", throwOnError: true)!;
        var entryConstructor = entryType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 10);
        var entries = Array.CreateInstance(entryType, routes.Count);
        for (var index = 0; index < routes.Count; index++)
        {
            var route = routes[index];
            entries.SetValue(entryConstructor.Invoke([
                route.RouteKey,
                route.ContentKey,
                route.Route,
                route.Canonical,
                null,
                null,
                null,
                true,
                null,
                null
            ]), index);
        }

        var mapType = engineAssembly.GetType("Bukit.Engine.SeoRouteMap", throwOnError: true)!;
        var mapConstructor = mapType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 6);
        return mapConstructor.Invoke([
            "https://bukit.dev/schemas/seo-route-map.v1.json",
            "1.0",
            DateTimeOffset.Parse("2026-08-03T00:00:00Z"),
            "https://example.com",
            "/",
            entries
        ]);
    }

    private static string[] ReadRouteMapEntries(object routeMap)
    {
        var routes = (System.Collections.IEnumerable)routeMap.GetType().GetProperty("Routes")!.GetValue(routeMap)!;
        return routes.Cast<object>()
            .Select(route => string.Join(
                "\u001f",
                route.GetType().GetProperty("RouteKey")!.GetValue(route),
                route.GetType().GetProperty("ContentKey")!.GetValue(route),
                route.GetType().GetProperty("Route")!.GetValue(route),
                route.GetType().GetProperty("Canonical")!.GetValue(route)))
            .ToArray();
    }

    private static SeoObservationUrlOptions Options()
        => new(
            "example.com",
            new HashSet<string>(["www.example.com"], StringComparer.Ordinal),
            new HashSet<string>(["utm_source"], StringComparer.Ordinal));

    private sealed record RouteDefinition(
        string RouteKey,
        string? ContentKey,
        string Route,
        string Canonical);
}
