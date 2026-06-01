using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Cli.Commands;

internal static class DoctorSchemaChecker
{
    public static bool CheckSchemaFieldCompleteness(DoctorCommand.DoctorContext ctx, IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed)
    {
        var collections = ctx.Config.Site.Collections;
        if (collections is null || collections.Count == 0)
        {
            return false;
        }

        var hasErrors = false;
        var allErrors = new List<string>();
        var allWarnings = new List<string>();

        foreach (var (collectionName, collectionConfig) in collections)
        {
            var schema = collectionConfig.Schema;
            if (schema is null || schema.Count == 0)
            {
                continue;
            }

            var template = collectionConfig.Template?.Trim();
            var collectionItems = string.IsNullOrWhiteSpace(template)
                ? routed
                : routed.Where(r => r.Route.Template?.Trim() == template).ToList();

            foreach (var (item, _) in collectionItems)
            {
                var errors = ContentSchemaValidator.Validate(item.Meta, schema, item.Id);
                foreach (var err in errors)
                {
                    var detail = $"{err.SourcePath ?? item.Id} (collection: {collectionName}): {err.Message}";
                    if (err.Code == "required")
                    {
                        hasErrors = true;
                        allErrors.Add(detail);
                    }
                    else
                    {
                        allWarnings.Add(detail);
                    }
                }
            }
        }

        if (hasErrors)
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

        return hasErrors;
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
            var schemaFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (collectionConfig.Schema is { Count: > 0 })
            {
                foreach (var f in collectionConfig.Schema)
                {
                    if (!string.IsNullOrWhiteSpace(f.Name))
                    {
                        schemaFieldNames.Add(f.Name.Trim());
                    }
                }
            }

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

    public static void CheckExtraContentFields(DoctorCommand.DoctorContext ctx, IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed)
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
            var schemaFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (collectionConfig.Schema is { Count: > 0 })
            {
                foreach (var f in collectionConfig.Schema)
                {
                    if (!string.IsNullOrWhiteSpace(f.Name))
                    {
                        schemaFieldNames.Add(f.Name.Trim());
                    }
                }
            }

            var template = collectionConfig.Template?.Trim();
            var collectionItems = string.IsNullOrWhiteSpace(template)
                ? routed
                : routed.Where(r => r.Route.Template?.Trim() == template).ToList();

            foreach (var (item, _) in collectionItems)
            {
                var fileExtras = new List<string>();
                foreach (var kv in item.Meta)
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
                    var fileId = item.Meta.TryGetValue("sourcePath", out var sp) && sp is string s ? s : item.Id;
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
}
