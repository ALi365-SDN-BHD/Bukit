using System.Text.Json;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal sealed class RoutesReportWriter : IBuildReportWriter
{
    public string Name => "routes";

    public void Write(BuildReportWriterContext context)
    {
        var entries = BuildRouteEntries(context.Variants);
        using var stream = File.Create(Path.Combine(context.ReportDir, "routes.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        BuildReporter.WriteArtifactContract(writer, BuildReporter.RoutesReportSchema);
        writer.WritePropertyName("routes");
        writer.WriteStartArray();
        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("url", entry.Url);
            writer.WriteString("outputPath", BuildReporter.NormalizePath(entry.OutputPath));
            writer.WriteString("template", BuildReporter.NormalizePath(entry.Template));
            writer.WriteString("source", entry.Source);
            writer.WriteString("kind", entry.Kind);
            writer.WriteString("language", entry.Language);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static IReadOnlyList<RouteReportEntry> BuildRouteEntries(IReadOnlyList<BuildVariantResult> variants)
    {
        return variants
            .SelectMany(variant => variant.RoutedDocuments.Concat(variant.DerivedDocuments).Select(route => new RouteReportEntry(
                route.Route.Url,
                route.Route.OutputPath,
                route.Route.Template,
                GetSource(route.Document),
                GetKind(route.Document),
                variant.Language)))
            .Concat(variants.SelectMany(variant => variant.StaticRoutes.Select(route => new RouteReportEntry(
                route.Url,
                route.OutputPath,
                route.Template,
                null,
                "static",
                variant.Language))))
            .Concat(variants.SelectMany(variant => variant.PluginOutputs.Select(plugin => new RouteReportEntry(
                BuildPluginRouteUrl(plugin.Path),
                BuildReporter.NormalizePath(plugin.Path),
                string.Empty,
                plugin.Plugin,
                "plugin",
                variant.Language))))
            .OrderBy(entry => entry.Url, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Language, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildPluginRouteUrl(string outputPath)
    {
        var normalizedPath = BuildReporter.NormalizePath(outputPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        return "/" + normalizedPath.TrimStart('/');
    }

    private static string? GetSource(ContentDocument document)
    {
        foreach (var key in new[] { "source", "sourcePath", "path", "file" })
        {
            var value = ContentFieldReader.GetText(document.CustomFields, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string GetKind(ContentDocument document)
    {
        return ContentFieldReader.GetCollection(document);
    }

    private sealed record RouteReportEntry(
        string Url,
        string OutputPath,
        string Template,
        string? Source,
        string Kind,
        string Language);
}
