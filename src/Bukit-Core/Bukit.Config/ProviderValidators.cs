using Bukit.Shared;
using System.Text.RegularExpressions;

namespace Bukit.Config;

internal static partial class ProviderValidators
{
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,62}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CloudBaseIdentifierRegex();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CloudBaseEnvIdRegex();

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,62}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CloudBaseBucketRegex();

    internal static void ValidateCloudBase(
        CloudBaseConfig cloudBase,
        string pathPrefix,
        string mode,
        MediaConfig media)
    {
        if (!CloudBaseEnvIdRegex().IsMatch(cloudBase.EnvId))
        {
            throw CloudBaseInvalid($"{pathPrefix}.envId must match ^[a-z0-9][a-z0-9-]{{0,62}}$.");
        }

        if (cloudBase.Region is not ("cn" or "intl"))
        {
            throw CloudBaseInvalid($"{pathPrefix}.region must be cn or intl.");
        }

        if (!CloudBaseIdentifierRegex().IsMatch(cloudBase.Table))
        {
            throw CloudBaseInvalid($"{pathPrefix}.table must match ^[A-Za-z_][A-Za-z0-9_]{{0,62}}$.");
        }

        if (!CloudBaseIdentifierRegex().IsMatch(cloudBase.IdField))
        {
            throw CloudBaseInvalid($"{pathPrefix}.idField must match ^[A-Za-z_][A-Za-z0-9_]{{0,62}}$.");
        }

        if (CloudBaseFieldRules.IsHardExcluded(cloudBase.IdField))
        {
            throw CloudBaseInvalid($"{pathPrefix}.idField must not use reserved personal field '{cloudBase.IdField}'.");
        }

        if (cloudBase.PageSize is < 1 or > 1000)
        {
            throw CloudBaseInvalid($"{pathPrefix}.pageSize must be between 1 and 1000.");
        }

        if (cloudBase.MaxItems is <= 0)
        {
            throw CloudBaseInvalid($"{pathPrefix}.maxItems must be a positive integer when set.");
        }

        if (cloudBase.SignExpiresSeconds is < 60 or > 3600)
        {
            throw CloudBaseInvalid($"{pathPrefix}.signExpiresSeconds must be between 60 and 3600.");
        }

        if (!string.Equals(cloudBase.CacheMode, "off", StringComparison.Ordinal))
        {
            throw CloudBaseInvalid($"{pathPrefix}.cacheMode must be off.");
        }

        if (!string.Equals(cloudBase.BodyFormat, "markdown", StringComparison.Ordinal))
        {
            throw CloudBaseInvalid($"{pathPrefix}.bodyFormat must be markdown.");
        }

        ValidateCloudBaseMappings(cloudBase, pathPrefix, mode);

        if (cloudBase.Filter is { } filter &&
            (string.IsNullOrWhiteSpace(filter.Field) || string.IsNullOrWhiteSpace(filter.Value)))
        {
            throw CloudBaseInvalid($"{pathPrefix}.filter requires non-empty field and value.");
        }

        if (cloudBase.Filter is { } validFilter && !CloudBaseIdentifierRegex().IsMatch(validFilter.Field))
        {
            throw CloudBaseInvalid($"{pathPrefix}.filter.field must match ^[A-Za-z_][A-Za-z0-9_]{{0,62}}$.");
        }

        if (cloudBase.Sort is { } sort &&
            (string.IsNullOrWhiteSpace(sort.Field) || sort.Direction is not ("asc" or "desc")))
        {
            throw CloudBaseInvalid($"{pathPrefix}.sort requires a field and direction asc or desc.");
        }

        if (cloudBase.Sort is { } validSort && !CloudBaseIdentifierRegex().IsMatch(validSort.Field))
        {
            throw CloudBaseInvalid($"{pathPrefix}.sort.field must match ^[A-Za-z_][A-Za-z0-9_]{{0,62}}$.");
        }

        if (cloudBase.MaxRps is <= 0)
        {
            throw CloudBaseInvalid($"{pathPrefix}.maxRps must be a positive integer when set.");
        }

        if (cloudBase.MaxRetries is < 0)
        {
            throw CloudBaseInvalid($"{pathPrefix}.maxRetries must not be negative.");
        }

        ValidateCloudBaseFileFields(cloudBase, pathPrefix, mode, media);
    }

    private static void ValidateCloudBaseMappings(CloudBaseConfig cloudBase, string pathPrefix, string mode)
    {
        if (mode == "content" && string.IsNullOrWhiteSpace(cloudBase.BodyField))
        {
            throw CloudBaseInvalid($"{pathPrefix}.bodyField is required when mode is content.");
        }

        if (cloudBase.FieldPolicy.Mode is not ("whitelist" or "all"))
        {
            throw CloudBaseInvalid($"{pathPrefix}.fieldPolicy.mode must be whitelist or all.");
        }

        ValidateCloudBaseField("titleField", cloudBase.TitleField);
        ValidateCloudBaseField("slugField", cloudBase.SlugField);
        ValidateCloudBaseField("publishAtField", cloudBase.PublishAtField);
        ValidateCloudBaseField("updatedAtField", cloudBase.UpdatedAtField);
        if (!string.IsNullOrWhiteSpace(cloudBase.BodyField))
        {
            ValidateCloudBaseField("bodyField", cloudBase.BodyField);
        }

        void ValidateCloudBaseField(string name, string value)
        {
            if (CloudBaseFieldRules.IsHardExcluded(value))
            {
                throw CloudBaseInvalid($"{pathPrefix}.{name} must not use reserved personal field '{value}'.");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw CloudBaseInvalid($"{pathPrefix}.{name} must be a non-empty string.");
            }
        }
    }

    private static void ValidateCloudBaseFileFields(CloudBaseConfig cloudBase, string pathPrefix, string mode, MediaConfig media)
    {
        if (cloudBase.FileFields.Count == 0)
        {
            if (cloudBase.StorageBucket is not null && !CloudBaseBucketRegex().IsMatch(cloudBase.StorageBucket))
            {
                throw CloudBaseInvalid($"{pathPrefix}.storageBucket must match ^[a-z0-9][a-z0-9._-]{{0,62}}$.");
            }

            return;
        }

        if (mode == "data")
        {
            throw CloudBaseInvalid($"{pathPrefix}.fileFields is not supported when mode is data.");
        }

        foreach (var field in cloudBase.FileFields)
        {
            if (CloudBaseFieldRules.IsHardExcluded(field))
            {
                throw CloudBaseInvalid($"{pathPrefix}.fileFields entry '{field}' is a reserved personal field.");
            }
        }

        if (cloudBase.FileFields.Any(string.IsNullOrWhiteSpace))
        {
            throw CloudBaseInvalid($"{pathPrefix}.fileFields must not contain empty names.");
        }

        if (string.IsNullOrWhiteSpace(cloudBase.StorageBucket) ||
            !CloudBaseBucketRegex().IsMatch(cloudBase.StorageBucket))
        {
            throw CloudBaseInvalid($"{pathPrefix}.storageBucket is required when fileFields is non-empty and must match ^[a-z0-9][a-z0-9._-]{{0,62}}$.");
        }

        if (!media.DownloadToLocal)
        {
            throw CloudBaseInvalid($"{pathPrefix}.fileFields requires content.media.downloadToLocal to be true.");
        }

        foreach (var field in cloudBase.FileFields)
        {
            if (!media.FieldKeys.Contains(field, StringComparer.Ordinal))
            {
                throw CloudBaseInvalid($"{pathPrefix}.fileFields entry '{field}' is not listed in content.media.fieldKeys.");
            }
        }

        if (!string.Equals(media.OnFailure.Trim(), "placeholder", StringComparison.OrdinalIgnoreCase))
        {
            throw CloudBaseInvalid($"{pathPrefix}.fileFields requires content.media.onFailure to be placeholder.");
        }

        ValidateCloudBaseMediaHosts(cloudBase, pathPrefix, media);
    }

    private static void ValidateCloudBaseMediaHosts(CloudBaseConfig cloudBase, string pathPrefix, MediaConfig media)
    {
        var gateway = cloudBase.Region == "intl"
            ? $"{cloudBase.EnvId}.api.intl.tcloudbasegateway.com"
            : $"{cloudBase.EnvId}.api.tcloudbasegateway.com";
        var storageCdn = $"{cloudBase.EnvId}.storage.tcloudbase.com";
        foreach (var host in new[] { gateway, storageCdn })
        {
            var excluded = MatchesHost(media.SkipHosts, host) ||
                (media.DownloadHosts.Count > 0 && !MatchesHost(media.DownloadHosts, host));
            if (excluded)
            {
                throw new ConfigException(
                    $"{pathPrefix}.fileFields requires content.media.downloadHosts/skipHosts to include {host}.",
                    DiagnosticCode.CloudBaseMediaLocalizationRequired);
            }
        }
    }

    private static bool MatchesHost(IReadOnlyList<string> patterns, string host)
    {
        foreach (var pattern in patterns)
        {
            var normalized = pattern.Trim().TrimEnd('.').ToLowerInvariant();
            if (normalized.StartsWith("*.", StringComparison.Ordinal))
            {
                if (host.Length > normalized.Length - 1 &&
                    host.EndsWith(normalized[1..], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (host.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static ConfigException CloudBaseInvalid(string message)
        => new(message, DiagnosticCode.CloudBaseConfigInvalid);

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

    internal static void ValidateNotion(NotionConfig notion, string pathPrefix = "content.notion", bool validateToken = true)
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

        // Offline builds read the Notion offline snapshot and never need a token.
        if (validateToken && !NotionSnapshotScope.IsOffline)
        {
            var token = EnvironmentHelper.GetNotionToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ConfigException("NOTION_TOKEN is required for notion provider and must come from environment variables.", DiagnosticCode.ConfigRequiredFieldMissing);
            }
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

        if (!MediaLocalizationRules.TryParseOnFailure(media.OnFailure, out _))
        {
            throw new ConfigException(MediaLocalizationRules.InvalidOnFailureMessage, DiagnosticCode.ConfigInvalidValue);
        }

        ValidateMediaHostPatterns("content.media.downloadHosts", media.DownloadHosts);
        ValidateMediaHostPatterns("content.media.skipHosts", media.SkipHosts);
    }

    private static void ValidateMediaHostPatterns(string fieldName, IReadOnlyList<string>? patterns)
    {
        // Name the value, not its index: blank entries are dropped while loading, so indexes can shift.
        if (!MediaLocalizationRules.TryNormalizeHostPatterns(patterns, out _, out var invalid))
        {
            throw new ConfigException(
                MediaLocalizationRules.InvalidHostPatternMessage(fieldName, invalid),
                DiagnosticCode.ConfigInvalidValue);
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

        if (!IsValidBranchConfig(deploy.Branch))
        {
            throw new ConfigException("deploy.branch is not a valid Git branch name for GitHub Pages deployment.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (!string.IsNullOrWhiteSpace(deploy.Message) && deploy.Message.Length > 4096)
        {
            throw new ConfigException("deploy.message must be <= 4096 characters.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        // A present but empty deploy.repo (for example an unset CI variable behind BUKIT_DEPLOY__REPO)
        // must fail instead of silently deploying to origin.
        if (deploy.Repo is not null && string.IsNullOrWhiteSpace(deploy.Repo))
        {
            throw new ConfigException("deploy.repo must not be empty. Set it to 'owner/name', or remove the key to deploy to origin.", DiagnosticCode.ConfigInvalidValue);
        }

        // Trimmed like deploy.branch and deploy.cname, and like deploy --repo; the deploy command trims it too.
        if (deploy.Repo is not null && !GitHubRepoSlug.IsValid(deploy.Repo.Trim()))
        {
            throw new ConfigException("deploy.repo must be 'owner/name' (for example 'ALi365-SDN-BHD/site'), without a URL, '.git' suffix, or whitespace.", DiagnosticCode.ConfigInvalidValue);
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

    private static bool IsValidBranchConfig(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch))
        {
            return true;
        }

        var trimmed = branch.Trim();
        if (trimmed.StartsWith("-", StringComparison.Ordinal) ||
            trimmed.StartsWith("refs/") ||
            trimmed.EndsWith('/') ||
            trimmed.EndsWith(".") ||
            trimmed.EndsWith(".lock") ||
            trimmed.Contains(' '))
        {
            return false;
        }

        if (trimmed.StartsWith('/') ||
            trimmed.Contains("..") ||
            trimmed.Contains("@{") ||
            trimmed.Contains('\\') ||
            trimmed.Contains(':') ||
            trimmed.Contains('?') ||
            trimmed.Contains('*') ||
            trimmed.Contains('[') ||
            trimmed.Any(char.IsWhiteSpace) ||
            trimmed.IndexOfAny(new[] { '\0', '\r', '\n', '\t' }) >= 0)
        {
            return false;
        }

        return true;
    }
}
