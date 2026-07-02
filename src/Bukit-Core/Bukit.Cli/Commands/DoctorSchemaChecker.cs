using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Cli.Commands;

internal static class DoctorSchemaChecker
{
    public static bool CheckSchemaFieldCompleteness(DoctorCommand.DoctorContext ctx, IReadOnlyList<RoutedContentDocument> routed)
    {
        var documents = routed.Select(x => x.Document).ToArray();
        if (documents.Length == 0)
        {
            return false;
        }

        var allErrors = new List<string>();
        var allWarnings = new List<string>();
        foreach (var err in ContentModelSchemaProjection.ValidateDocuments(ctx.Config, documents))
        {
            var detail = $"{err.SourcePath}: [{err.Code}] {err.Message}";
            if (IsBlockingSchemaError(err.Code))
            {
                allErrors.Add(detail);
            }
            else
            {
                allWarnings.Add(detail);
            }
        }

        if (allErrors.Count > 0)
        {
            Console.WriteLine($"✖ {allErrors.Count} schema validation error(s):");
            foreach (var e in allErrors)
            {
                Console.WriteLine($"  - {e}");
            }
        }

        if (allWarnings.Count > 0)
        {
            Console.WriteLine($"⚠ {allWarnings.Count} schema validation warning(s):");
            foreach (var w in allWarnings)
            {
                Console.WriteLine($"  - {w}");
            }
        }

        return allErrors.Count > 0;
    }

    public static void CheckTemplateFieldsVsSchema(DoctorCommand.DoctorContext ctx)
    {
        var collections = ctx.Config.Site.Collections;
        if (collections is null || collections.Count == 0)
        {
            return;
        }

        var mismatches = new List<string>();
        foreach (var (collectionName, collectionConfig) in collections)
        {
            var schemaFieldNames = ContentModelSchemaProjection.GetFieldNames(ctx.Config, collectionName);

            var templates = new List<string>();
            if (!string.IsNullOrWhiteSpace(collectionConfig.Template))
                templates.Add(collectionConfig.Template.Trim());
            if (!string.IsNullOrWhiteSpace(collectionConfig.ListTemplate))
                templates.Add(collectionConfig.ListTemplate.Trim());

            foreach (var templatePath in templates)
            {
                var capabilities = TemplateCapabilitiesResolver.GetCapabilities(templatePath, ctx.LayoutsDir);
                if (capabilities?.Fields is null || capabilities.Fields.Count == 0)
                {
                    continue;
                }

                foreach (var field in capabilities.Fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Key) || field.Key == "tags" || field.Key == "categories")
                    {
                        continue;
                    }

                    if (!schemaFieldNames.Contains(field.Key))
                    {
                        mismatches.Add($"{collectionName} → {templatePath}: template declares field '{field.Key}' but collection schema has no such field");
                    }
                }
            }
        }

        if (mismatches.Count > 0)
        {
            Console.WriteLine($"⚠ Template fields vs schema mismatch:");
            foreach (var m in mismatches)
            {
                Console.WriteLine($"  - {m}");
            }
        }
    }

    public static void CheckExtraContentFields(DoctorCommand.DoctorContext ctx, IReadOnlyList<RoutedContentDocument> routed)
    {
        var collections = ctx.Config.Site.Collections;
        if (collections is null || collections.Count == 0)
        {
            return;
        }

        var reservedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "title", "slug", "type", "collection", "publishat", "published", "language", "tags", "categories",
            "summary", "route", "url", "outputpath", "template", "source", "sourcepath",
            "bodyfingerprint", "draft"
        };

        var extraFields = new List<string>();
        var totalExtras = 0;
        var filesWithExtras = 0;

        foreach (var (collectionName, collectionConfig) in collections)
        {
            var schemaFieldNames = ContentModelSchemaProjection.GetFieldNames(ctx.Config, collectionName);

            var template = collectionConfig.Template?.Trim();
            var collectionItems = string.IsNullOrWhiteSpace(template)
                ? routed
                : routed.Where(r => r.Route.Template?.Trim() == template).ToList();

            foreach (var routedDocument in collectionItems)
            {
                var document = routedDocument.Document;
                var fileExtras = new List<string>();
                foreach (var kv in document.CustomFields ?? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase))
                {
                    if (reservedKeys.Contains(kv.Key))
                    {
                        continue;
                    }

                    if (!schemaFieldNames.Contains(kv.Key))
                    {
                        fileExtras.Add(kv.Key);
                    }
                }

                if (fileExtras.Count > 0)
                {
                    filesWithExtras++;
                    totalExtras += fileExtras.Count;
                    var fileId = ContentFieldReader.GetText(document.CustomFields, "sourcePath") ?? document.Id;
                    extraFields.Add($"{fileId}: field(s) [{string.Join(", ", fileExtras)}] not in collection schema");
                }
            }
        }

        if (extraFields.Count > 0)
        {
            Console.WriteLine($"ℹ Extra fields in content not declared in schema:");
            foreach (var e in extraFields)
            {
                Console.WriteLine($"  - {e}");
            }

            Console.WriteLine($"  ({totalExtras} extra field(s) total across {filesWithExtras} file(s))");
        }
    }

    private static bool IsBlockingSchemaError(string code)
        => code.Contains("required", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("missing", StringComparison.OrdinalIgnoreCase);
}
