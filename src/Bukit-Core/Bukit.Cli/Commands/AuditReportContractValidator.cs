using System.Text.Json;

namespace Bukit.Cli.Commands;

internal static class AuditReportContractValidator
{
    internal static SeoReportValidator.AuditReportContract ValidateReportContract(
        JsonElement root,
        SeoReportValidator.AuditReportContract contractMode)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("root must be a JSON object.");
        }

        var schema = AuditReportJsonReader.ReadRequiredString(root, "$", "schema");

        if (string.Equals(schema, SeoReportValidator.PublishAuditReportSchema, StringComparison.Ordinal))
        {
            if (contractMode == SeoReportValidator.AuditReportContract.SeoOnly)
            {
                throw new InvalidDataException($"unsupported schema '{schema}'. Expected '{SeoReportValidator.SeoReportSchema}'.");
            }

            PublishAuditReportContractValidator.Validate(root);
            return SeoReportValidator.AuditReportContract.PublishOnly;
        }

        if (string.Equals(schema, SeoReportValidator.SeoReportSchema, StringComparison.Ordinal))
        {
            if (contractMode == SeoReportValidator.AuditReportContract.PublishOnly)
            {
                throw new InvalidDataException($"unsupported schema '{schema}'. Expected '{SeoReportValidator.PublishAuditReportSchema}'.");
            }

            SeoAuditReportContractValidator.Validate(root);
            return SeoReportValidator.AuditReportContract.SeoOnly;
        }

        if (contractMode == SeoReportValidator.AuditReportContract.SeoOrPublish)
        {
            throw new InvalidDataException($"unsupported schema '{schema}'. Expected '{SeoReportValidator.SeoReportSchema}' or '{SeoReportValidator.PublishAuditReportSchema}'.");
        }

        if (contractMode == SeoReportValidator.AuditReportContract.SeoOnly)
        {
            throw new InvalidDataException($"unsupported schema '{schema}'. Expected '{SeoReportValidator.SeoReportSchema}'.");
        }

        throw new InvalidDataException($"unsupported schema '{schema}'. Expected '{SeoReportValidator.PublishAuditReportSchema}'.");
    }

    internal static void ValidateSeoReportContract(JsonElement root)
        => SeoAuditReportContractValidator.Validate(root);

    internal static void ValidatePublishReportContract(JsonElement root)
        => PublishAuditReportContractValidator.Validate(root);

    internal static SeoReportValidator.SeoReportSnapshot ReadDiffSnapshot(JsonElement root)
        => SeoReportDiffSnapshotReader.Read(root);
}
