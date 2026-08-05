using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.Incremental;

internal sealed class CollectionsAndFieldScopesContributor : IRenderDependencyContributor
{
    public string Name => "collections-and-field-scopes";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var collections = context.Config.Site.Collections;
        if (collections is { Count: > 0 })
        {
            foreach (var collection in collections.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.AppendLabeledCanonicalValue("collection.key", collection.Key);
                writer.AppendLabeledCanonicalValue("collection.permalink", collection.Value.Permalink);
                writer.AppendLabeledCanonicalValue("collection.template", collection.Value.Template);
                writer.AppendLabeledCanonicalValue("collection.listRoute", collection.Value.ListRoute);
                writer.AppendLabeledCanonicalValue("collection.listTitle", collection.Value.ListTitle);
                writer.AppendLabeledCanonicalValue("collection.listDescription", collection.Value.ListDescription);
                writer.AppendLabeledCanonicalValue("collection.listTemplate", collection.Value.ListTemplate);
                writer.AppendLabeledCanonicalValue("collection.schemaFailMode", collection.Value.SchemaFailMode);

                var pagination = collection.Value.Pagination;
                writer.AppendLabeledCanonicalValue("collection.pagination.enabled", pagination.Enabled);
                writer.AppendLabeledCanonicalValue("collection.pagination.pageSize", pagination.PageSize);
                writer.AppendLabeledCanonicalValue("collection.pagination.urlPattern", pagination.UrlPattern);
                writer.AppendLabeledCanonicalValue("collection.pagination.firstPageUsesListRoute", pagination.FirstPageUsesListRoute);

                var output = collection.Value.Output;
                writer.AppendLabeledCanonicalValue("collection.output.rss", output.Rss);
                writer.AppendLabeledCanonicalValue("collection.output.sitemap", output.Sitemap);
                writer.AppendLabeledCanonicalValue("collection.output.archive", output.Archive);
                writer.AppendLabeledCanonicalValue("collection.output.feedPath", output.FeedPath);
                writer.AppendLabeledCanonicalValue("collection.output.feedTitle", output.FeedTitle);
                writer.AppendLabeledCanonicalValue("collection.output.feedDescription", output.FeedDescription);
                writer.AppendLabeledCanonicalValue("collection.output.archiveDetail.present", output.ArchiveDetail is not null);
                if (output.ArchiveDetail is not null)
                {
                    writer.AppendLabeledCanonicalValue("collection.output.archiveDetail.depth", output.ArchiveDetail.Depth);
                    writer.AppendLabeledCanonicalValue("collection.output.archiveDetail.template", output.ArchiveDetail.Template);
                    writer.AppendLabeledCanonicalValue("collection.output.archiveDetail.routePrefix", output.ArchiveDetail.RoutePrefix);
                }

                if (collection.Value.FilteredLists is { Count: > 0 })
                {
                    foreach (var filteredList in collection.Value.FilteredLists.OrderBy(x => x.Field, StringComparer.Ordinal))
                    {
                        writer.AppendLabeledCanonicalValue("collection.filteredList.field", filteredList.Field);
                        writer.AppendLabeledCanonicalValue("collection.filteredList.operator", filteredList.Operator);
                        writer.AppendLabeledCanonicalValue("collection.filteredList.value", filteredList.Value);
                        writer.AppendLabeledCanonicalValue(
                            "collection.filteredList.values",
                            filteredList.Values?.OrderBy(x => x, StringComparer.Ordinal).ToList());
                        writer.AppendLabeledCanonicalValue("collection.filteredList.listRoute", filteredList.ListRoute);
                        writer.AppendLabeledCanonicalValue("collection.filteredList.title", filteredList.Title);
                        writer.AppendLabeledCanonicalValue("collection.filteredList.description", filteredList.Description);
                        writer.AppendLabeledCanonicalValue("collection.filteredList.listTemplate", filteredList.ListTemplate);
                        writer.AppendLabeledCanonicalValue("collection.filteredList.pageSize", filteredList.PageSize);
                        writer.AppendLabeledCanonicalValue("collection.filteredList.urlPattern", filteredList.UrlPattern);
                        writer.AppendLabeledCanonicalValue("collection.filteredList.emptyBehavior", filteredList.EmptyBehavior);
                    }
                }
            }
        }

        AppendFieldScopes(writer, ContentModelSchemaFactory.FromConfig(context.Config).FieldScopes);
    }

    private static void AppendFieldScopes(
        RenderDependencyHashWriter writer,
        IReadOnlyDictionary<string, IReadOnlyList<CustomFieldDefinition>>? fieldScopes)
    {
        if (fieldScopes is null || fieldScopes.Count == 0)
        {
            return;
        }

        foreach (var scope in fieldScopes.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            writer.AppendLabeledCanonicalValue("fieldScope.key", scope.Key);
            foreach (var field in scope.Value.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.AppendLabeledCanonicalValue("fieldScope.field.name", field.Name);
                writer.AppendLabeledCanonicalValue("fieldScope.field.type", field.FieldType);
                writer.AppendLabeledCanonicalValue("fieldScope.field.label", field.Label);
                writer.AppendLabeledCanonicalValue("fieldScope.field.format", field.Format);
                writer.AppendLabeledCanonicalValue(
                    "fieldScope.field.enum",
                    field.Enum?.OrderBy(x => x, StringComparer.Ordinal).ToList());
                writer.AppendLabeledCanonicalValue("fieldScope.field.min", field.Min);
                writer.AppendLabeledCanonicalValue("fieldScope.field.max", field.Max);
                writer.AppendLabeledCanonicalValue("fieldScope.field.required", field.Required);
                writer.AppendLabeledCanonicalValue("fieldScope.field.default", field.Default);
            }
        }
    }
}
