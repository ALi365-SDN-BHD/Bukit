using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine;

internal static class MetricsWriter
{
    internal static void WriteIfRequested(
        string rootDir,
        string? metricsPath,
        AppConfig config,
        string outputDir,
        int contentItemCount,
        IReadOnlyList<BuildVariantResult> variants)
    {
        if (string.IsNullOrWhiteSpace(metricsPath))
        {
            return;
        }

        var fullPath = Path.IsPathRooted(metricsPath) ? metricsPath : Path.Combine(rootDir, metricsPath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(fullPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();

        writer.WriteNumber("version", 2);
        writer.WriteString("ts", DateTimeOffset.UtcNow.ToString("O"));

        writer.WritePropertyName("site");
        writer.WriteStartObject();
        writer.WriteString("name", config.Site.Name);
        writer.WriteString("title", config.Site.Title);
        if (config.Site.Url is null)
        {
            writer.WriteNull("url");
        }
        else
        {
            writer.WriteString("url", config.Site.Url);
        }
        writer.WriteString("baseUrl", config.Site.BaseUrl);
        writer.WriteString("language", config.Site.Language);
        if (config.Site.DefaultLanguage is null)
        {
            writer.WriteNull("defaultLanguage");
        }
        else
        {
            writer.WriteString("defaultLanguage", config.Site.DefaultLanguage);
        }
        writer.WritePropertyName("languages");
        if (config.Site.Languages is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartArray();
            foreach (var lang in config.Site.Languages)
            {
                writer.WriteStringValue(lang);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();

        writer.WriteString("outputDir", Path.GetFullPath(outputDir));
        writer.WriteNumber("contentItems", contentItemCount);

        writer.WritePropertyName("variants");
        writer.WriteStartArray();
        foreach (var v in variants)
        {
            writer.WriteStartObject();
            writer.WriteString("language", v.Language);
            writer.WriteString("baseUrl", v.BaseUrl);
            writer.WriteString("outputDir", Path.GetFullPath(v.OutputDir));
            writer.WriteNumber("routed", v.Routed.Count);
            writer.WriteNumber("derived", v.DerivedRouted.Count);
            writer.WriteNumber("rendered", v.RenderedCount);
            writer.WriteNumber("skipped", v.SkippedCount);

            writer.WritePropertyName("reasons");
            writer.WriteStartObject();
            foreach (var kv in v.RenderReasons)
            {
                writer.WriteNumber(kv.Key, kv.Value);
            }
            writer.WriteEndObject();

            writer.WritePropertyName("plugins");
            writer.WriteStartArray();
            foreach (var p in v.PluginExecutions)
            {
                writer.WriteStartObject();
                writer.WriteString("name", p.Name);
                writer.WriteString("hook", p.Hook);
                writer.WriteNumber("durationMs", p.DurationMs);
                writer.WriteBoolean("success", p.Success);
                if (p.Error is null)
                {
                    writer.WriteNull("error");
                }
                else
                {
                    writer.WriteString("error", p.Error);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WritePropertyName("stages");
            writer.WriteStartObject();

            writer.WritePropertyName("durationsMs");
            writer.WriteStartObject();
            foreach (var kv in v.StageMetrics.DurationsMs.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                writer.WriteNumber(kv.Key, kv.Value);
            }
            writer.WriteEndObject();

            writer.WritePropertyName("counts");
            writer.WriteStartObject();
            foreach (var kv in v.StageMetrics.Counts.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                writer.WriteNumber(kv.Key, kv.Value);
            }
            writer.WriteEndObject();

            writer.WriteEndObject();

            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.Flush();

        WriteHtmlReport(Path.ChangeExtension(fullPath, ".html"), config, contentItemCount, variants);
    }

    private static void WriteHtmlReport(
        string htmlPath,
        AppConfig config,
        int contentItemCount,
        IReadOnlyList<BuildVariantResult> variants)
    {
        var rows = string.Join(Environment.NewLine, variants.Select(v =>
            $"<tr><td>{Escape(v.Language)}</td><td>{v.Routed.Count}</td><td>{v.DerivedRouted.Count}</td><td>{v.RenderedCount}</td><td>{v.SkippedCount}</td><td>{Escape(string.Join(", ", v.RenderReasons.Select(r => $"{r.Key}: {r.Value}")))}</td></tr>"));

        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <title>Bukit Build Report</title>
              <style>
                body{font-family:system-ui,sans-serif;margin:2rem;line-height:1.5;color:#172033}
                table{border-collapse:collapse;width:100%;margin-top:1rem}
                th,td{border:1px solid #d8dee9;padding:.5rem;text-align:left}
                th{background:#f4f6f8}
              </style>
            </head>
            <body>
              <h1>Bukit Build Report</h1>
              <p><strong>{{Escape(config.Site.Title)}}</strong> · {{contentItemCount}} content item(s)</p>
              <table>
                <thead><tr><th>Language</th><th>Routed</th><th>Derived</th><th>Rendered</th><th>Skipped</th><th>Reasons</th></tr></thead>
                <tbody>
            {{rows}}
                </tbody>
              </table>
            </body>
            </html>
            """;
        File.WriteAllText(htmlPath, html);
    }

    private static string Escape(string? value)
        => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
