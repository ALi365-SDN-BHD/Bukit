using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Notion;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;
namespace Bukit.Engine;

internal static class TaxonomyTermsInjector
{
    internal static void InjectFromDataDocuments(BuildContext context, IReadOnlyList<ContentDocument> dataDocuments)
    {
        if (dataDocuments.Count == 0)
        {
            return;
        }

        var ensure = GetOrCreateEnsureTermsMap(context);

        foreach (var document in dataDocuments)
        {
            var kind = ContentFieldReader.GetText(document.CustomFields, "sourceKey") ?? string.Empty;
            if (!kind.Equals("categories", StringComparison.OrdinalIgnoreCase) &&
                !kind.Equals("tags", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var title = (document.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var slug = (document.Slug ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = SlugHelper.Slugify(title);
            }

            if (string.IsNullOrWhiteSpace(slug))
            {
                continue;
            }

            if (!ensure.TryGetValue(kind, out var list))
            {
                list = new List<Dictionary<string, object>>(capacity: 16);
                ensure[kind] = list;
            }

            if (list.Any(x => x.TryGetValue("slug", out var s) && s is not null && string.Equals(s.ToString(), slug, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            list.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = title,
                ["slug"] = slug
            });
        }
    }

    internal static async Task InjectFromNotionDatabaseOptionsAsync(
        BuildContext context,
        AppConfig config,
        CancellationToken cancellationToken)
    {
        var snapshot = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.Current;
        var offline = snapshot is { Offline: true };
        var token = offline ? string.Empty : EnvironmentHelper.GetNotionToken();
        if (!offline && string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var keyToKind = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = "tags",
            ["categories"] = "categories"
        };

        if (config.Taxonomy.Kinds is { Count: > 0 } kinds)
        {
            foreach (var kindConfig in kinds)
            {
                var key = (kindConfig.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var kind = string.IsNullOrWhiteSpace(kindConfig.Kind) ? key : kindConfig.Kind.Trim();
                keyToKind[key] = kind;
            }
        }

        if (keyToKind.Count == 0)
        {
            return;
        }

        var notionConfigs = new List<NotionConfig>();
        if (config.Content.Sources is { Count: > 0 } sources)
        {
            foreach (var source in sources)
            {
                if (source.Type.Equals("notion", StringComparison.OrdinalIgnoreCase) && source.Notion is not null)
                {
                    notionConfigs.Add(source.Notion);
                }
            }
        }

        if (notionConfigs.Count == 0)
        {
            return;
        }

        var seenDb = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var notion in notionConfigs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var databaseId = (notion.DatabaseId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(databaseId) || !seenDb.Add(databaseId))
            {
                continue;
            }

            var options = new NotionContentSourceOptions
            {
                DatabaseId = databaseId,
                Token = (token ?? string.Empty).Trim(),
                MaxRetries = notion.MaxRetries ?? 5,
                MaxRps = notion.MaxRps ?? 3,
                Snapshot = snapshot
            };

            try
            {
                using var client = NotionContentClient.Create(options, context.Logger);
                var schemaOptions = await NotionDatabaseOptionReader.ReadAsync(
                    client,
                    databaseId,
                    cancellationToken);
                InjectNotionSchemaOptions(schemaOptions, keyToKind, context);
            }
            catch (Bukit.Shared.NotionSnapshotException)
            {
                // Offline: missing options are an error, not a silently smaller term list.
                throw;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                context.Logger.Warn($"event=taxonomy.ensure_terms.notion_options_failed databaseId={databaseId} error={ex.GetType().Name}");
            }
        }
    }

    private static void InjectNotionSchemaOptions(
        IReadOnlyDictionary<string, IReadOnlyList<string>> schemaOptions,
        IReadOnlyDictionary<string, string> keyToKind,
        BuildContext context)
    {
        var ensure = GetOrCreateEnsureTermsMap(context);

        foreach (var property in schemaOptions)
        {
            if (!keyToKind.TryGetValue(property.Key, out var kind) || string.IsNullOrWhiteSpace(kind))
            {
                continue;
            }

            var list = ensure.TryGetValue(kind, out var existing) ? existing : null;
            if (list is null)
            {
                list = new List<Dictionary<string, object>>(capacity: 16);
                ensure[kind] = list;
            }

            foreach (var option in property.Value)
            {
                var title = Plugins.BuiltIn.TaxonomyTermValueNormalizer.Normalize(option, out var decoded);
                if (decoded)
                {
                    context.Logger.Warn($"event=taxonomy.term.decoded key={property.Key} raw=\"{option}\" normalized=\"{title}\" source=notion_schema_option hint=fix_option_in_notion");
                }
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var slug = SlugHelper.Slugify(title);
                if (string.IsNullOrWhiteSpace(slug))
                {
                    continue;
                }

                if (list.Any(x => x.TryGetValue("slug", out var s) && s is not null && string.Equals(s.ToString(), slug, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                list.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = title,
                    ["slug"] = slug
                });
            }
        }
    }

    internal static Dictionary<string, List<Dictionary<string, object>>> GetOrCreateEnsureTermsMap(BuildContext context)
        => context.GetOrAdd(
            BuildDataKeys.TaxonomyEnsureTerms,
            static () => new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase));

    internal static string NormalizeNotionFieldKey(string text)
        => NotionDatabaseOptionReader.NormalizeFieldKey(text);


}
