using System.Text;
using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Content.Notion;
using Bukit.Engine.Plugins;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class TaxonomyTermsInjector
{
    internal static void InjectFromDataItems(BuildContext context, IReadOnlyList<ContentItem> dataItems)
    {
        if (dataItems.Count == 0)
        {
            return;
        }

        var ensure = GetOrCreateEnsureTermsMap(context.Data);

        foreach (var item in dataItems)
        {
            if (!item.Meta.TryGetValue("sourceKey", out var sourceKeyObj) || sourceKeyObj is null)
            {
                continue;
            }

            var kind = (sourceKeyObj.ToString() ?? string.Empty).Trim();
            if (!kind.Equals("categories", StringComparison.OrdinalIgnoreCase) &&
                !kind.Equals("tags", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var title = (item.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var slug = (item.Slug ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = SlugifyTerm(title);
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

    internal static async Task InjectFromNotionDatabaseOptionsAsync(BuildContext context, CancellationToken cancellationToken)
    {
        var token = EnvironmentHelper.GetNotionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var keyToKind = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = "tags",
            ["categories"] = "categories"
        };

        if (context.Config.Taxonomy.Kinds is { Count: > 0 } kinds)
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
        if (context.Config.Content.Sources is { Count: > 0 } sources)
        {
            foreach (var source in sources)
            {
                if (source.Type.Equals("notion", StringComparison.OrdinalIgnoreCase) && source.Notion is not null)
                {
                    notionConfigs.Add(source.Notion);
                }
            }
        }
        else if (context.Config.Content.Provider.Equals("notion", StringComparison.OrdinalIgnoreCase) && context.Config.Content.Notion is not null)
        {
            notionConfigs.Add(context.Config.Content.Notion);
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

            var options = new NotionProviderOptions
            {
                DatabaseId = databaseId,
                Token = token.Trim(),
                MaxRetries = notion.MaxRetries ?? 5,
                MaxRps = notion.MaxRps ?? 3
            };

            try
            {
                using var client = new NotionApiClient(options);
                using var doc = await client.GetAsync(NotionApiUrls.Database(databaseId), cancellationToken);
                InjectNotionSchemaOptions(doc, keyToKind, context.Data);
            }
            catch (Exception ex)
            {
                context.Logger.Warn($"event=taxonomy.ensure_terms.notion_options_failed databaseId={databaseId} error={ex.GetType().Name}");
            }
        }
    }

    private static void InjectNotionSchemaOptions(
        JsonDocument databaseDoc,
        IReadOnlyDictionary<string, string> keyToKind,
        Dictionary<string, object> data)
    {
        if (!databaseDoc.RootElement.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var ensure = GetOrCreateEnsureTermsMap(data);

        foreach (var prop in props.EnumerateObject())
        {
            var normalizedKey = NormalizeNotionFieldKey(prop.Name);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                continue;
            }

            if (!keyToKind.TryGetValue(normalizedKey, out var kind) || string.IsNullOrWhiteSpace(kind))
            {
                continue;
            }

            if (!prop.Value.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var type = (typeEl.GetString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            if (!TryGetNotionPropertyOptions(prop.Value, type, out var optionsEl))
            {
                continue;
            }

            var list = ensure.TryGetValue(kind, out var existing) ? existing : null;
            if (list is null)
            {
                list = new List<Dictionary<string, object>>(capacity: 16);
                ensure[kind] = list;
            }

            foreach (var opt in optionsEl.EnumerateArray())
            {
                if (!opt.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var title = (nameEl.GetString() ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var slug = SlugifyTerm(title);
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

    private static bool TryGetNotionPropertyOptions(JsonElement property, string type, out JsonElement options)
    {
        options = default;

        if (type is "select" or "multi_select" or "status")
        {
            if (!property.TryGetProperty(type, out var inner) || inner.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!inner.TryGetProperty("options", out options) || options.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static Dictionary<string, List<Dictionary<string, object>>> GetOrCreateEnsureTermsMap(Dictionary<string, object> data)
    {
        if (data.TryGetValue("taxonomy_ensure_terms", out var existing) &&
            existing is Dictionary<string, List<Dictionary<string, object>>> typed)
        {
            return typed;
        }

        var map = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);
        data["taxonomy_ensure_terms"] = map;
        return map;
    }

    private static string NormalizeNotionFieldKey(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(trimmed.Length);
        var underscore = false;

        foreach (var ch in trimmed)
        {
            var lower = char.ToLowerInvariant(ch);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(lower);
                underscore = false;
                continue;
            }

            if (!underscore)
            {
                sb.Append('_');
                underscore = true;
            }
        }

        return sb.ToString().Trim('_');
    }

    internal static string SlugifyTerm(string text)
    {
        var trimmed = (text ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(trimmed.Length);
        var dash = false;

        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                dash = false;
                continue;
            }

            if (ch is ' ' or '-' or '_' or '.')
            {
                if (!dash && sb.Length > 0)
                {
                    sb.Append('-');
                    dash = true;
                }
            }
        }

        return sb.ToString().Trim('-');
    }
}
