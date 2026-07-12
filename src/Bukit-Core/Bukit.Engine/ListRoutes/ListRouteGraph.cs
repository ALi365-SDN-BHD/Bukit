using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;
using Bukit.Engine.RouteMetadata;

namespace Bukit.Engine;

internal sealed class ListRouteGraph
{
    private ListRouteGraph(IReadOnlyList<ListRoutePlan> routes)
    {
        Routes = routes;
    }

    public static ListRouteGraph Empty { get; } = new(Array.Empty<ListRoutePlan>());

    public IReadOnlyList<ListRoutePlan> Routes { get; }

    public static ListRouteGraph Create(IEnumerable<ListRoutePlan> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var materialized = routes.ToArray();
        foreach (var route in materialized)
        {
            ValidateRoute(route);
        }

        ValidateUnique(materialized, route => route.RouteId.Trim(), "routeId");
        ValidateUnique(materialized, route => NormalizeUrlForComparison(route.Url), "url");
        ValidateUnique(materialized, route => RoutePathBuilder.NormalizeOutputPath(route.OutputPath), "outputPath");
        return materialized.Length == 0 ? Empty : new ListRouteGraph(materialized);
    }

    public ListRoutePlan? FindByRouteId(string routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId))
        {
            return null;
        }

        return Routes.FirstOrDefault(route => string.Equals(route.RouteId, routeId, StringComparison.OrdinalIgnoreCase));
    }

    public ListRoutePlan? FindByOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return null;
        }

        var normalized = RoutePathBuilder.NormalizeOutputPath(outputPath);
        return Routes.FirstOrDefault(route => string.Equals(
            RoutePathBuilder.NormalizeOutputPath(route.OutputPath),
            normalized,
            StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateUnique(IReadOnlyList<ListRoutePlan> routes, Func<ListRoutePlan, string> selector, string fieldName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in routes)
        {
            var value = selector(route);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"List route {fieldName} is required.", nameof(routes));
            }

            if (!seen.Add(value))
            {
                throw new ArgumentException($"Duplicate list route {fieldName}: {value}", nameof(routes));
            }
        }
    }

    private static void ValidateRoute(ListRoutePlan route)
    {
        if (string.IsNullOrWhiteSpace(route.RouteId))
        {
            throw new ArgumentException("List route routeId is required.", nameof(route));
        }

        if (string.IsNullOrWhiteSpace(route.Template))
        {
            throw new ArgumentException($"List route template is required for {route.RouteId}.", nameof(route));
        }

        if (string.IsNullOrWhiteSpace(route.CanonicalUrl))
        {
            throw new ArgumentException($"List route canonicalUrl is required for {route.RouteId}.", nameof(route));
        }

        if (route.TotalItems < 0)
        {
            throw new ArgumentException($"List route totalItems must be non-negative for {route.RouteId}.", nameof(route));
        }

        if (route.TotalItems < route.Items.Count)
        {
            throw new ArgumentException($"List route totalItems must be greater than or equal to item count for {route.RouteId}.", nameof(route));
        }

        var description = Describe(route);
        RouteSecurityValidator.ValidateInternalUrl(route.Url, description);
        RouteSecurityValidator.ValidateInternalUrl(route.CanonicalUrl, description);
        if (!string.IsNullOrWhiteSpace(route.MetadataRouteUrl))
        {
            RouteSecurityValidator.ValidateInternalUrl(route.MetadataRouteUrl, description);
        }
        RouteSecurityValidator.ValidateOutputPath(route.OutputPath, description);
    }

    private static string NormalizeUrlForComparison(string url)
    {
        var normalized = RoutePathBuilder.NormalizeUrl(url);
        return normalized == "/" ? normalized : normalized.TrimEnd('/');
    }

    private static string Describe(ListRoutePlan route)
        => $"list route id={route.RouteId}, url={route.Url}, outputPath={route.OutputPath}";
}

internal sealed record ListRoutePlan
{
    public required string RouteId { get; init; }
    public required ListRouteKind Kind { get; init; }
    public required string Url { get; init; }
    public required string OutputPath { get; init; }
    public required string Template { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public string? MetadataRouteUrl { get; init; }
    public bool RouteMetadataApplied { get; init; }
    public string? Collection { get; init; }
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    public required int TotalItems { get; init; }
    public IReadOnlyList<ListRouteItem> Items { get; init; } = Array.Empty<ListRouteItem>();
    public required string CanonicalUrl { get; init; }
    public string? PrevUrl { get; init; }
    public string? NextUrl { get; init; }
    public ListRouteFilterContext? FilterContext { get; init; }
    public ListRouteTaxonomyContext? TaxonomyContext { get; init; }

    public int? TotalPages => CalculateTotalPages(PageSize, TotalItems);

    public RouteInfo ToRouteInfo()
    {
        return new RouteInfo(Url, OutputPath, Template);
    }

    private static int? CalculateTotalPages(int? pageSize, int totalItems)
    {
        if (pageSize.GetValueOrDefault() <= 0)
        {
            return null;
        }

        return Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize.GetValueOrDefault()));
    }
}

internal enum ListRouteKind
{
    Home,
    CollectionList,
    CollectionPage,
    TaxonomyIndex,
    TaxonomyTermPage,
    FilteredListPage
}

internal sealed record ListRouteItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? OutputPath { get; init; }
    public string? Template { get; init; }
    public string? Summary { get; init; }
    public DateTimeOffset? PublishDate { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public IReadOnlyDictionary<string, ContentField>? Fields { get; init; }
    public ContentRecord? ContentRecord { get; init; }
    public ContentRoutePolicy? RoutePolicy { get; init; }
    public ContentPublishPolicy? PublishPolicy { get; init; }
    public IReadOnlyList<EntityRecord>? Entities { get; init; }
    public ProvenanceRecord? Provenance { get; init; }
    public TrustMetadata? Trust { get; init; }
    public IReadOnlyList<string>? Representations { get; init; }

    public static ListRouteItem FromRoutedContentDocument(RoutedContentDocument routed)
    {
        ArgumentNullException.ThrowIfNull(routed);

        var document = routed.Document;
        var record = document.Record;
        return new ListRouteItem
        {
            Id = document.Id,
            Title = document.Title,
            Url = routed.Route.Url,
            OutputPath = routed.Route.OutputPath,
            Template = routed.Route.Template,
            Summary = record.Presentation.Summary ?? ContentFieldReader.GetSummary(document),
            PublishDate = document.PublishAt,
            UpdatedAt = record.Lifecycle.UpdatedAt,
            Fields = document.CustomFields,
            ContentRecord = record,
            RoutePolicy = document.Route,
            PublishPolicy = document.Publish,
            Entities = record.Entities,
            Provenance = record.Provenance,
            Trust = record.Trust,
            Representations = PublishRepresentationRegistry.DocumentKinds()
        };
    }

    public PageInfo ToPageInfo(
        string content = "",
        SeoModel? seo = null,
        IReadOnlyList<TableOfContentsEntry>? tableOfContents = null)
    {
        return new PageInfo
        {
            Title = Title,
            Url = Url,
            Content = content,
            Summary = Summary,
            TableOfContents = tableOfContents,
            PublishDate = PublishDate,
            UpdatedAt = UpdatedAt,
            Fields = Fields,
            Seo = seo,
            ContentRecord = ContentRecord,
            Route = RoutePolicy,
            Publish = PublishPolicy,
            Entities = Entities,
            Provenance = Provenance,
            Trust = Trust,
            Representations = Representations
        };
    }
}

internal sealed record ListRouteFilterContext
{
    public required string Field { get; init; }
    public string Operator { get; init; } = "equals";
    public string? Value { get; init; }
    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();
}

internal sealed record ListRouteTaxonomyContext
{
    public required string Kind { get; init; }
    public string? Term { get; init; }
    public string? Slug { get; init; }
    public string? RoutePrefix { get; init; }
    public string? Url { get; init; }
    public bool IsIndex { get; init; }
}

internal sealed record ListRouteGraphSnapshot
{
    public string Schema { get; init; } = "bukit.list-route-graph";
    public string SchemaVersion { get; init; } = "1";
    public required IReadOnlyList<ListRoutePlanSnapshot> Routes { get; init; }

    public static ListRouteGraphSnapshot FromGraph(ListRouteGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        return new ListRouteGraphSnapshot
        {
            Routes = graph.Routes.Select(ListRoutePlanSnapshot.FromPlan).ToArray()
        };
    }
}

internal sealed record ListRoutePlanSnapshot
{
    public required string RouteId { get; init; }
    public required string Kind { get; init; }
    public required string Url { get; init; }
    public required string OutputPath { get; init; }
    public required string Template { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Collection { get; init; }
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    public int TotalItems { get; init; }
    public required IReadOnlyList<string> ItemIds { get; init; }
    public required string CanonicalUrl { get; init; }
    public string? PrevUrl { get; init; }
    public string? NextUrl { get; init; }
    public ListRouteFilterContext? FilterContext { get; init; }
    public ListRouteTaxonomyContext? TaxonomyContext { get; init; }

    public static ListRoutePlanSnapshot FromPlan(ListRoutePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new ListRoutePlanSnapshot
        {
            RouteId = plan.RouteId,
            Kind = ToSnapshotKind(plan.Kind),
            Url = plan.Url,
            OutputPath = plan.OutputPath,
            Template = plan.Template,
            Title = plan.Title,
            Summary = plan.Summary,
            Collection = plan.Collection,
            PageNumber = plan.PageNumber,
            PageSize = plan.PageSize,
            TotalItems = plan.TotalItems,
            ItemIds = plan.Items.Select(item => item.Id).ToArray(),
            CanonicalUrl = plan.CanonicalUrl,
            PrevUrl = plan.PrevUrl,
            NextUrl = plan.NextUrl,
            FilterContext = plan.FilterContext,
            TaxonomyContext = plan.TaxonomyContext
        };
    }

    private static string ToSnapshotKind(ListRouteKind kind)
        => kind switch
        {
            ListRouteKind.Home => "home",
            ListRouteKind.CollectionList => "collectionList",
            ListRouteKind.CollectionPage => "collectionPage",
            ListRouteKind.TaxonomyIndex => "taxonomyIndex",
            ListRouteKind.TaxonomyTermPage => "taxonomyTermPage",
            ListRouteKind.FilteredListPage => "filteredListPage",
            _ => kind.ToString()
        };
}
