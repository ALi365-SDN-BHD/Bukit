using System.Text.Json;
using Bukit.Config;
using Bukit.Content.Notion.Snapshot;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// <c>--notion-pin &lt;file&gt;</c>: in an offline build, the Notion keys that choose which fields and
/// pages reach templates come from this file (the reviewed version of the site) instead of
/// <c>site.yaml</c>. Without it, an unreviewed change could widen <c>fieldPolicy</c> or drop the
/// filter and expose hidden fields or drafts kept in the snapshot.
/// </summary>
/// <remarks>
/// Format: <c>{"format":1,"sources":{"&lt;source key&gt;":{"databaseId":…,…}}}</c>. Each source holds its
/// reviewed <c>databaseId</c> (a source whose database differs fails, so renaming sources cannot swap
/// pins) and the pinned keys with their <c>site.yaml</c> names; a key it omits takes Bukit's default.
/// Unknown keys are errors.
/// </remarks>
internal sealed class NotionPin
{
    internal const int FormatVersion = 1;

    // The propertyMap keys as site.yaml spells them.
    private static readonly string[] s_propertyMapKeys =
    [
        "Title", "Slug", "Type", "PublishAt", "Language", "I18nKey", "Summary", "Collection",
        "SeoTitle", "SeoDescription", "SeoImage", "Canonical", "OriginalUrl", "References",
        "EntitiesJson", "Cover", "CoverAlt", "CoverCaption"
    ];

    private static readonly string[] s_sourceKeys =
    [
        "databaseId", "renderContent", "fieldPolicy", "propertyMap", "filterProperty", "filterType",
        "filterValue", "sortProperty", "sortDirection", "includeSlugs", "includeSlugProperty", "maxItems"
    ];

    private readonly Dictionary<string, PinnedKeys> _sources;

    private NotionPin(Dictionary<string, PinnedKeys> sources) => _sources = sources;

    /// <summary>The pinned values of one source; null members take Bukit's defaults.</summary>
    internal sealed record PinnedKeys(
        string DatabaseId,
        bool? RenderContent,
        NotionFieldPolicyConfig FieldPolicy,
        NotionPropertyMapConfig? PropertyMap,
        string FilterProperty,
        string FilterType,
        string? FilterValue,
        string? SortProperty,
        string SortDirection,
        IReadOnlyList<string>? IncludeSlugs,
        string IncludeSlugProperty,
        int? MaxItems);

    /// <summary>Reads and validates the pin file. Messages never contain its path.</summary>
    public static NotionPin Load(string path)
    {
        JsonDocument document;
        try
        {
            var bytes = File.ReadAllBytes(path);
            document = JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith("\uFEFF"u8) ? 3 : 0));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new NotionSnapshotException("Notion pin file could not be read as JSON.", ex, DiagnosticCode.NotionPinInvalid);
        }

        using (document)
        {
            return Parse(document.RootElement);
        }
    }

    internal static NotionPin Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("format", out var format)
            || format.ValueKind != JsonValueKind.Number
            || !format.TryGetInt32(out var version)
            || version != FormatVersion)
        {
            throw Invalid($"\"format\" must be {FormatVersion}");
        }

        if (!root.TryGetProperty("sources", out var sources) || sources.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("\"sources\" must be an object");
        }

        var parsed = new Dictionary<string, PinnedKeys>(StringComparer.Ordinal);
        foreach (var source in sources.EnumerateObject())
        {
            parsed[source.Name] = ParseSource(source.Name, source.Value);
        }

        return new NotionPin(parsed);
    }

    /// <summary>
    /// Replaces the pinned keys of every Notion source. Returns the new config and, per source key,
    /// the pinned keys whose <c>site.yaml</c> value differed.
    /// </summary>
    public (AppConfig Config, IReadOnlyList<(string Source, IReadOnlyList<string> Keys)> Changed) Apply(AppConfig config)
    {
        if (config.Content.Sources is not { Count: > 0 } sources)
        {
            return (config, []);
        }

        var keys = ContentSourceKeys.Compute(sources);
        var updated = new ContentSourceConfig[sources.Count];
        var changed = new List<(string, IReadOnlyList<string>)>();
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            if (!source.Type.Trim().Equals("notion", StringComparison.OrdinalIgnoreCase) || source.Notion is not { } notion)
            {
                updated[i] = source;
                continue;
            }

            if (!_sources.TryGetValue(keys[i], out var pin)
                || !string.Equals(pin.DatabaseId.Trim(), notion.DatabaseId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new NotionSnapshotException(
                    $"Notion source '{keys[i]}' with this database is not in the reviewed version. Apply the change and run a build with NOTION_TOKEN and --notion-snapshot.",
                    DiagnosticCode.NotionPinSourceMissing);
            }

            var pinned = notion with
            {
                RenderContent = pin.RenderContent,
                FieldPolicy = pin.FieldPolicy,
                PropertyMap = pin.PropertyMap,
                FilterProperty = pin.FilterProperty,
                FilterType = pin.FilterType,
                FilterValue = pin.FilterValue,
                SortProperty = pin.SortProperty,
                SortDirection = pin.SortDirection,
                IncludeSlugs = pin.IncludeSlugs,
                IncludeSlugProperty = pin.IncludeSlugProperty,
                MaxItems = pin.MaxItems
            };
            var diff = Differences(notion, pinned);
            if (diff.Count > 0)
            {
                changed.Add((keys[i], diff));
            }

            updated[i] = source with { Notion = pinned };
        }

        return (config with { Content = config.Content with { Sources = updated } }, changed);
    }

    private static List<string> Differences(NotionConfig current, NotionConfig pinned)
    {
        var diff = new List<string>();
        if (!string.Equals(current.FieldPolicy.Mode, pinned.FieldPolicy.Mode, StringComparison.Ordinal)
            || !SameList(current.FieldPolicy.Allowed, pinned.FieldPolicy.Allowed))
        {
            diff.Add("fieldPolicy");
        }

        if (current.PropertyMap != pinned.PropertyMap)
        {
            diff.Add("propertyMap");
        }

        AddIf(diff, "filterProperty", current.FilterProperty, pinned.FilterProperty);
        AddIf(diff, "filterType", current.FilterType, pinned.FilterType);
        AddIf(diff, "filterValue", current.FilterValue, pinned.FilterValue);
        AddIf(diff, "sortProperty", current.SortProperty, pinned.SortProperty);
        AddIf(diff, "sortDirection", current.SortDirection, pinned.SortDirection);
        if (!SameList(current.IncludeSlugs, pinned.IncludeSlugs))
        {
            diff.Add("includeSlugs");
        }

        AddIf(diff, "includeSlugProperty", current.IncludeSlugProperty, pinned.IncludeSlugProperty);
        if (current.RenderContent != pinned.RenderContent)
        {
            diff.Add("renderContent");
        }

        if (current.MaxItems != pinned.MaxItems)
        {
            diff.Add("maxItems");
        }

        return diff;

        static void AddIf(List<string> diff, string key, string? a, string? b)
        {
            if (!string.Equals(a, b, StringComparison.Ordinal))
            {
                diff.Add(key);
            }
        }
    }

    private static bool SameList(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
        => (a ?? []).SequenceEqual(b ?? [], StringComparer.Ordinal);

    private static PinnedKeys ParseSource(string name, JsonElement source)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            throw Invalid($"source '{name}' must be an object");
        }

        RejectUnknown(source, s_sourceKeys, name);
        var databaseId = String(source, "databaseId", name);
        if (string.IsNullOrWhiteSpace(databaseId))
        {
            throw Invalid($"'{name}.databaseId' is required");
        }

        bool? renderContent = null;
        if (source.TryGetProperty("renderContent", out var render) && render.ValueKind != JsonValueKind.Null)
        {
            renderContent = render.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw Invalid($"'{name}.renderContent' must be a boolean")
            };
        }

        var defaults = new NotionConfig { DatabaseId = string.Empty };
        var fieldPolicy = defaults.FieldPolicy;
        if (source.TryGetProperty("fieldPolicy", out var policy))
        {
            if (policy.ValueKind != JsonValueKind.Object)
            {
                throw Invalid($"'{name}.fieldPolicy' must be an object");
            }

            RejectUnknown(policy, ["mode", "allowed"], $"{name}.fieldPolicy");

            fieldPolicy = new NotionFieldPolicyConfig
            {
                Mode = String(policy, "mode", $"{name}.fieldPolicy") ?? fieldPolicy.Mode,
                Allowed = Strings(policy, "allowed", $"{name}.fieldPolicy")
            };
        }

        NotionPropertyMapConfig? propertyMap = null;
        if (source.TryGetProperty("propertyMap", out var map) && map.ValueKind != JsonValueKind.Null)
        {
            if (map.ValueKind != JsonValueKind.Object)
            {
                throw Invalid($"'{name}.propertyMap' must be an object");
            }

            RejectUnknown(map, s_propertyMapKeys, $"{name}.propertyMap");

            var where = $"{name}.propertyMap";
            propertyMap = new NotionPropertyMapConfig
            {
                Title = String(map, "Title", where),
                Slug = String(map, "Slug", where),
                Type = String(map, "Type", where),
                PublishAt = String(map, "PublishAt", where),
                Language = String(map, "Language", where),
                I18nKey = String(map, "I18nKey", where),
                Summary = String(map, "Summary", where),
                Collection = String(map, "Collection", where),
                SeoTitle = String(map, "SeoTitle", where),
                SeoDescription = String(map, "SeoDescription", where),
                SeoImage = String(map, "SeoImage", where),
                Canonical = String(map, "Canonical", where),
                OriginalUrl = String(map, "OriginalUrl", where),
                References = String(map, "References", where),
                EntitiesJson = String(map, "EntitiesJson", where),
                Cover = String(map, "Cover", where),
                CoverAlt = String(map, "CoverAlt", where),
                CoverCaption = String(map, "CoverCaption", where)
            };
        }

        int? maxItems = null;
        if (source.TryGetProperty("maxItems", out var max) && max.ValueKind != JsonValueKind.Null)
        {
            if (max.ValueKind != JsonValueKind.Number || !max.TryGetInt32(out var value))
            {
                throw Invalid($"'{name}.maxItems' must be an integer");
            }

            maxItems = value;
        }

        return new PinnedKeys(
            databaseId,
            renderContent,
            fieldPolicy,
            propertyMap,
            String(source, "filterProperty", name) ?? defaults.FilterProperty,
            String(source, "filterType", name) ?? defaults.FilterType,
            String(source, "filterValue", name),
            String(source, "sortProperty", name),
            String(source, "sortDirection", name) ?? defaults.SortDirection,
            Strings(source, "includeSlugs", name),
            String(source, "includeSlugProperty", name) ?? defaults.IncludeSlugProperty,
            maxItems);
    }

    private static void RejectUnknown(JsonElement element, string[] known, string where)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!known.Contains(property.Name, StringComparer.Ordinal))
            {
                throw Invalid($"'{where}.{property.Name}' is not a known key");
            }
        }
    }

    private static string? String(JsonElement parent, string key, string where)
    {
        if (!parent.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw Invalid($"'{where}.{key}' must be a string");
    }

    private static string[]? Strings(JsonElement parent, string key, string where)
    {
        if (!parent.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw Invalid($"'{where}.{key}' must be an array of strings");
        }

        // Trimmed, blanks dropped: how site.yaml lists are read.
        return [.. value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString()!.Trim()
                : throw Invalid($"'{where}.{key}' must be an array of strings"))
            .Where(item => item.Length > 0)];
    }

    private static NotionSnapshotException Invalid(string reason)
        => new($"Notion pin file is invalid: {reason}.", DiagnosticCode.NotionPinInvalid);
}
