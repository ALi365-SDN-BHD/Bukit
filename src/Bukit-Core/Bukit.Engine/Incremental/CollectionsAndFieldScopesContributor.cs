using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.Incremental;

internal sealed class CollectionsAndFieldScopesContributor : IRenderDependencyContributor
{
    public string Name => "collections-and-field-scopes";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var collections = context.Config.Site.Collections;
        if (collections is null || collections.Count == 0)
        {
            return;
        }

        foreach (var collection in collections.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            writer.AppendNewline();
            writer.AppendUtf8(collection.Key);
            writer.AppendNewline();
            writer.AppendUtf8(collection.Value.Permalink);
            writer.AppendNewline();
            writer.AppendUtf8(collection.Value.Template);
            writer.AppendNewline();
            writer.AppendUtf8(collection.Value.ListRoute);
            writer.AppendNewline();
            writer.AppendUtf8(collection.Value.ListTitle);
            writer.AppendNewline();
            writer.AppendUtf8(collection.Value.ListDescription);
            writer.AppendNewline();
            writer.AppendUtf8(collection.Value.ListTemplate);
            writer.AppendNewline();
            writer.AppendUtf8(collection.Value.SchemaFailMode);

            var pagination = collection.Value.Pagination;
            writer.AppendNewline();
            writer.AppendUtf8(pagination.Enabled.ToString());
            writer.AppendNewline();
            writer.AppendUtf8(pagination.PageSize.ToString());
            writer.AppendNewline();
            writer.AppendUtf8(pagination.UrlPattern);
            writer.AppendNewline();
            writer.AppendUtf8(pagination.FirstPageUsesListRoute.ToString());

            var output = collection.Value.Output;
            writer.AppendNewline();
            writer.AppendUtf8(output.Rss.ToString());
            writer.AppendNewline();
            writer.AppendUtf8(output.Sitemap.ToString());
            writer.AppendNewline();
            writer.AppendUtf8(output.Archive.ToString());
            writer.AppendNewline();
            writer.AppendUtf8(output.FeedPath);
            writer.AppendNewline();
            writer.AppendUtf8(output.FeedTitle);
            writer.AppendNewline();
            writer.AppendUtf8(output.FeedDescription);
            if (output.ArchiveDetail is not null)
            {
                writer.AppendNewline();
                writer.AppendUtf8(output.ArchiveDetail.Depth);
                writer.AppendNewline();
                writer.AppendUtf8(output.ArchiveDetail.Template);
                writer.AppendNewline();
                writer.AppendUtf8(output.ArchiveDetail.RoutePrefix);
            }

            if (collection.Value.FilteredLists is { Count: > 0 })
            {
                foreach (var filteredList in collection.Value.FilteredLists.OrderBy(x => x.Field, StringComparer.Ordinal))
                {
                    writer.AppendNewline();
                    writer.AppendUtf8(filteredList.Field);
                    writer.AppendNewline();
                    writer.AppendUtf8(filteredList.Operator);
                    writer.AppendNewline();
                    writer.AppendUtf8(filteredList.Value);
                    if (filteredList.Values is { Count: > 0 })
                    {
                        foreach (var value in filteredList.Values.OrderBy(x => x, StringComparer.Ordinal))
                        {
                            writer.AppendNewline();
                            writer.AppendUtf8(value);
                        }
                    }

                    writer.AppendNewline();
                    writer.AppendUtf8(filteredList.ListRoute);
                    writer.AppendNewline();
                    writer.AppendUtf8(filteredList.Title);
                    writer.AppendNewline();
                    writer.AppendUtf8(filteredList.Description);
                    writer.AppendNewline();
                    writer.AppendUtf8(filteredList.ListTemplate);
                    writer.AppendNewline();
                    writer.AppendUtf8(filteredList.PageSize?.ToString());
                    writer.AppendNewline();
                    writer.AppendUtf8(filteredList.UrlPattern);
                    writer.AppendNewline();
                    writer.AppendUtf8(filteredList.EmptyBehavior);
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
            writer.AppendNewline();
            writer.AppendUtf8(scope.Key);
            foreach (var field in scope.Value.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.AppendNewline();
                writer.AppendUtf8(field.Name);
                writer.AppendNewline();
                writer.AppendUtf8(field.FieldType);
                writer.AppendNewline();
                writer.AppendUtf8(field.Label);
                writer.AppendNewline();
                writer.AppendUtf8(field.Format);
                if (field.Enum is { Count: > 0 })
                {
                    foreach (var value in field.Enum.OrderBy(x => x, StringComparer.Ordinal))
                    {
                        writer.AppendNewline();
                        writer.AppendUtf8(value);
                    }
                }

                writer.AppendNewline();
                writer.AppendUtf8(field.Min?.ToString());
                writer.AppendNewline();
                writer.AppendUtf8(field.Max?.ToString());
                writer.AppendNewline();
                writer.AppendUtf8(field.Required.ToString());
                writer.AppendNewline();
                writer.AppendUtf8(field.Default?.ToString());
            }
        }
    }
}
