using System.Text.Json;
using Bukit.CloudBase.Models;
using Bukit.CloudBase.Transport;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal static class DoctorCloudBaseChecker
{
    internal static async Task<int> CheckAsync(
        CloudBaseConfig cloudBase,
        AppConfig config,
        string rootDir,
        ILogger logger,
        Func<CloudBaseClient>? clientFactory = null)
    {
        var errors = 0;
        var key = SecretStore.Get("CLOUDBASE_PUBLISHABLE_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.WriteLine("✖ [BKT-0C02] CLOUDBASE_PUBLISHABLE_KEY is not set");
            return 1;
        }

        var role = CloudBaseCredentials.DecodeRole(key);
        if (role.IsAdminRisk)
        {
            Console.WriteLine("⚠ [BKT-0C0A] CloudBase credential looks like an admin key (service_role or an unreadable role)");
        }

        using var client = CreateClient(cloudBase, key, clientFactory);

        int? total;
        try
        {
            total = await client.HeadCountAsync(cloudBase.Table).ConfigureAwait(false);
        }
        catch (CloudBaseApiException ex)
        {
            Console.WriteLine(Describe(ex, cloudBase.Table));
            return errors + 1;
        }

        if (total == 0)
        {
            Console.WriteLine("⚠ CloudBase table is reachable but returned 0 rows; Row-Level Security may hide rows (no error)");
            return errors;
        }

        JsonElement? row = null;
        try
        {
            var page = await client.QueryAsync(new CloudBaseQueryRequest
            {
                Table = cloudBase.Table,
                IdField = cloudBase.IdField,
                PageSize = 1,
                MaxItems = 1
            }).ConfigureAwait(false);
            row = page.Rows.Count > 0 ? page.Rows[0] : null;
        }
        catch (CloudBaseApiException ex)
        {
            Console.WriteLine(Describe(ex, cloudBase.Table));
            return errors + 1;
        }

        if (row is { } sample)
        {
            foreach (var (name, field) in MappedFields(cloudBase))
            {
                if (!sample.TryGetProperty(field, out _))
                {
                    Console.WriteLine($"⚠ CloudBase sample row is missing mapped {name} '{field}'");
                }
            }

            if (cloudBase.FileFields.Count > 0 && cloudBase.StorageBucket is { } bucket)
            {
                errors += await CheckStorageAsync(client, cloudBase, bucket, sample).ConfigureAwait(false);
            }
        }

        if (cloudBase.FileFields.Count > 0 &&
            MediaPlaceholderProbe.CheckWarning(config, rootDir, logger) is { } placeholder)
        {
            Console.WriteLine($"⚠ {placeholder}");
        }

        if (cloudBase.FieldPolicy.Allowed?.Contains(cloudBase.IdField, StringComparer.OrdinalIgnoreCase) == true)
        {
            Console.WriteLine($"⚠ [BKT-0C01] cloudbase.fieldPolicy.allowed publishes idField '{cloudBase.IdField}'");
        }

        Console.WriteLine("✔ CloudBase checks finished");
        return errors;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000",
        Justification = "CheckAsync owns and disposes the returned client.")]
    private static CloudBaseClient CreateClient(
        CloudBaseConfig cloudBase,
        string key,
        Func<CloudBaseClient>? clientFactory)
        => clientFactory?.Invoke() ??
            new CloudBaseClient(new CloudBaseClientOptions
            {
                EnvId = cloudBase.EnvId,
                Region = cloudBase.Region,
                PublishableKey = key,
                MaxRps = cloudBase.MaxRps is > 0 ? cloudBase.MaxRps.Value : 5,
                MaxRetries = cloudBase.MaxRetries ?? 3
            });

    private static async Task<int> CheckStorageAsync(
        CloudBaseClient client,
        CloudBaseConfig cloudBase,
        string bucket,
        JsonElement sample)
    {
        var value = cloudBase.FileFields
            .Select(field => sample.TryGetProperty(field, out var element) && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        if (value is null)
        {
            return 0;
        }

        var objectName = value.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase)
            ? value["cloud://".Length..] is var rest && rest.Contains('/')
                ? rest[(rest.IndexOf('/') + 1)..]
                : null
            : value;
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return 0;
        }

        try
        {
            var results = await client.SignObjectsAsync(bucket, [objectName], 60).ConfigureAwait(false);
            if (results.Count != 1 || results[0].Url is null)
            {
                Console.WriteLine($"✖ [BKT-0C08] CloudBase storage signing failed for field '{cloudBase.FileFields[0]}'");
                return 1;
            }
        }
        catch (CloudBaseApiException)
        {
            Console.WriteLine($"✖ [BKT-0C08] CloudBase storage signing failed for bucket '{bucket}'");
            return 1;
        }

        return 0;
    }

    private static IEnumerable<(string Name, string Field)> MappedFields(CloudBaseConfig cloudBase)
    {
        yield return ("idField", cloudBase.IdField);
        yield return ("titleField", cloudBase.TitleField);
        yield return ("slugField", cloudBase.SlugField);
        yield return ("publishAtField", cloudBase.PublishAtField);
        yield return ("updatedAtField", cloudBase.UpdatedAtField);
        if (!string.IsNullOrWhiteSpace(cloudBase.BodyField))
        {
            yield return ("bodyField", cloudBase.BodyField);
        }
    }

    private static string Describe(CloudBaseApiException ex, string table)
    {
        var code = ex.Kind switch
        {
            CloudBaseErrorKind.Unauthorized or CloudBaseErrorKind.Forbidden => DiagnosticCode.CloudBaseAuthFailed,
            CloudBaseErrorKind.NotFound => DiagnosticCode.CloudBaseNotFound,
            CloudBaseErrorKind.RateLimited => DiagnosticCode.CloudBaseRateLimited,
            CloudBaseErrorKind.ResponseInvalid => DiagnosticCode.CloudBaseResponseInvalid,
            _ => DiagnosticCode.CloudBaseRequestFailed
        };
        var status = ex.StatusCode is { } value ? $" (HTTP {value})" : string.Empty;
        return $"✖ [{DiagnosticCodeFormatter.Format(code)}] CloudBase table '{table}' check failed: {ex.Kind}{status}";
    }
}
