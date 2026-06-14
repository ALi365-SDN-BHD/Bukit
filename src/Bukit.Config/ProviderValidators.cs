using Bukit.Shared;
using System.Text.RegularExpressions;

namespace Bukit.Config;

internal static class ProviderValidators
{
    internal static IReadOnlyDictionary<string, object>? AsObjectMap(object value)
    {
        if (value is IReadOnlyDictionary<string, object> readOnlyMap)
        {
            return readOnlyMap;
        }

        if (value is IDictionary<string, object> map)
        {
            return new Dictionary<string, object>(map, StringComparer.OrdinalIgnoreCase);
        }

        return null;
    }

    internal static void ValidateNotion(NotionConfig notion, string pathPrefix = "content.notion")
    {
        if (string.IsNullOrWhiteSpace(notion.DatabaseId))
        {
            throw new ConfigException($"{pathPrefix}.databaseId is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (notion.PageSize is < 1 or > 100)
        {
            throw new ConfigException($"{pathPrefix}.pageSize must be between 1 and 100.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (notion.MaxItems is not null && notion.MaxItems.Value <= 0)
        {
            throw new ConfigException($"{pathPrefix}.maxItems must be a positive integer when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (notion.RenderConcurrency is not null && notion.RenderConcurrency.Value <= 0)
        {
            throw new ConfigException($"{pathPrefix}.renderConcurrency must be a positive integer when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (notion.MaxRps is not null && notion.MaxRps.Value <= 0)
        {
            throw new ConfigException($"{pathPrefix}.maxRps must be a positive integer when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (notion.MaxRetries is not null && notion.MaxRetries.Value < 0)
        {
            throw new ConfigException($"{pathPrefix}.maxRetries must be a non-negative integer when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        var mode = (notion.FieldPolicy.Mode ?? "whitelist").Trim().ToLowerInvariant();
        if (mode is not ("whitelist" or "all"))
        {
            throw new ConfigException($"{pathPrefix}.fieldPolicy.mode must be whitelist|all.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        var filterType = (notion.FilterType ?? "checkbox_true").Trim().ToLowerInvariant();
        if (filterType is not ("checkbox_true" or "checkbox_false" or "select_equals" or "status_equals" or "rich_text_equals" or "none"))
        {
            throw new ConfigException($"{pathPrefix}.filterType must be checkbox_true|checkbox_false|select_equals|status_equals|rich_text_equals|none.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (filterType != "none" && string.IsNullOrWhiteSpace(notion.FilterProperty))
        {
            throw new ConfigException($"{pathPrefix}.filterProperty is required when filterType is not none.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (filterType is "select_equals" or "status_equals" or "rich_text_equals" &&
            string.IsNullOrWhiteSpace(notion.FilterValue))
        {
            throw new ConfigException($"{pathPrefix}.filterValue is required for select_equals|status_equals|rich_text_equals filters.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (!string.IsNullOrWhiteSpace(notion.SortProperty))
        {
            var dir = (notion.SortDirection ?? "ascending").Trim().ToLowerInvariant();
            if (dir is not ("ascending" or "descending"))
            {
                throw new ConfigException($"{pathPrefix}.sortDirection must be ascending|descending.", DiagnosticCode.ConfigRequiredFieldMissing);
            }
        }

        if (notion.IncludeSlugs is { Count: > 0 })
        {
            if (string.IsNullOrWhiteSpace(notion.IncludeSlugProperty))
            {
                throw new ConfigException($"{pathPrefix}.includeSlugProperty is required when includeSlugs is set.", DiagnosticCode.ConfigRequiredFieldMissing);
            }
        }

        var cacheMode = (notion.CacheMode ?? "off").Trim().ToLowerInvariant();
        if (cacheMode is not ("off" or "readwrite" or "readonly"))
        {
            throw new ConfigException($"{pathPrefix}.cacheMode must be off|readwrite|readonly.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (notion.CacheDir is not null && string.IsNullOrWhiteSpace(notion.CacheDir))
        {
            throw new ConfigException($"{pathPrefix}.cacheDir must be a non-empty string when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        var token = EnvironmentHelper.GetNotionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ConfigException("NOTION_TOKEN is required for notion provider and must come from environment variables.", DiagnosticCode.ConfigRequiredFieldMissing);
        }
    }

    internal static void ValidateMedia(MediaConfig media)
    {
        if (string.IsNullOrWhiteSpace(media.DownloadDir))
        {
            throw new ConfigException("content.media.downloadDir must be a non-empty string.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        RejectPathTraversal("content.media.downloadDir", media.DownloadDir);

        if (string.IsNullOrWhiteSpace(media.UrlBase))
        {
            throw new ConfigException("content.media.urlBase must be a non-empty string.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (string.IsNullOrWhiteSpace(media.DefaultImageUrl))
        {
            throw new ConfigException("content.media.defaultImageUrl must be a non-empty string.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (media.FieldKeys is null)
        {
            throw new ConfigException("content.media.fieldKeys is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (media.MaxConcurrency is <= 0)
        {
            throw new ConfigException("content.media.maxConcurrency must be a positive integer when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (media.MaxRetries is < 0)
        {
            throw new ConfigException("content.media.maxRetries must be a non-negative integer when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (media.TimeoutMs is <= 0)
        {
            throw new ConfigException("content.media.timeoutMs must be a positive integer when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (media.MaxFileSizeBytes is <= 0)
        {
            throw new ConfigException("content.media.maxFileSizeBytes must be a positive integer when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (media.RetryBaseDelayMs is < 0)
        {
            throw new ConfigException("content.media.retryBaseDelayMs must be a non-negative integer when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }
    }

    internal static void RejectPathTraversal(string fieldName, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        if (Path.IsPathRooted(value))
        {
            throw new ConfigException($"{fieldName} must be a relative path.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        var normalized = value.Replace('\\', '/');
        if (normalized.StartsWith('/'))
        {
            throw new ConfigException($"{fieldName} must be a relative path.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (normalized.Length >= 3 &&
            char.IsLetter(normalized[0]) &&
            normalized[1] == ':' &&
            normalized[2] == '/')
        {
            throw new ConfigException($"{fieldName} must be a relative path.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        var segments = normalized.Split('/');
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                throw new ConfigException($"{fieldName} must not contain '..' path traversal segments.", DiagnosticCode.ConfigPathTraversal);
            }
        }
    }

    internal static void ValidateMarkdown(MarkdownConfig markdown, string pathPrefix = "content.markdown")
    {
        if (string.IsNullOrWhiteSpace(markdown.Dir))
        {
            throw new ConfigException($"{pathPrefix}.dir is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        RejectPathTraversal($"{pathPrefix}.dir", markdown.Dir);

        if (markdown.MaxItems is not null && markdown.MaxItems.Value <= 0)
        {
            throw new ConfigException($"{pathPrefix}.maxItems must be a positive integer when set.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (markdown.IncludePaths is { Count: > 0 } includePaths)
        {
            for (var i = 0; i < includePaths.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(includePaths[i]))
                {
                    throw new ConfigException($"{pathPrefix}.includePaths[{i}] must be a non-empty string.", DiagnosticCode.ConfigRequiredFieldMissing);
                }

                RejectPathTraversal($"{pathPrefix}.includePaths[{i}]", includePaths[i]);
            }
        }

        if (markdown.IncludeGlobs is { Count: > 0 } includeGlobs)
        {
            for (var i = 0; i < includeGlobs.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(includeGlobs[i]))
                {
                    throw new ConfigException($"{pathPrefix}.includeGlobs[{i}] must be a non-empty string.", DiagnosticCode.ConfigRequiredFieldMissing);
                }

                RejectPathTraversal($"{pathPrefix}.includeGlobs[{i}]", includeGlobs[i]);
            }
        }
    }

    internal static void ValidateDeployConfig(DeployConfig deploy)
    {
        if (string.IsNullOrWhiteSpace(deploy.Provider))
        {
            throw new ConfigException("deploy.provider is required when deploy section is present. Bukit 1.0 supports only 'github-pages'.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        var provider = deploy.Provider.Trim().ToLowerInvariant();
        if (provider is not ("github-pages"))
        {
            throw new ConfigException("deploy.provider must be 'github-pages' in Bukit 1.0.", DiagnosticCode.ConfigInvalidValue);
        }

        if (!string.IsNullOrWhiteSpace(deploy.Branch) && deploy.Branch.Contains('/'))
        {
            throw new ConfigException("deploy.branch must not contain '/'.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (!string.IsNullOrWhiteSpace(deploy.Message) && deploy.Message.Length > 4096)
        {
            throw new ConfigException("deploy.message must be <= 4096 characters.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (!string.IsNullOrWhiteSpace(deploy.Cname))
        {
            var cname = deploy.Cname.Trim();
            if (!IsValidDomain(cname))
            {
                throw new ConfigException($"deploy.cname '{cname}' is not a valid domain name.", DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    internal static bool IsValidDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        if (domain.Length > 253)
        {
            return false;
        }

        return Regex.IsMatch(domain, @"^[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)*$", RegexOptions.CultureInvariant);
    }
}
