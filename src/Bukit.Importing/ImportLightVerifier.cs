using YamlDotNet.RepresentationModel;

namespace Bukit.Importing;

internal static class ImportLightVerifier
{
    internal static IReadOnlyList<ImportDiagnostic> Verify(HtmlDemoImportOptions options)
    {
        var diagnostics = new List<ImportDiagnostic>();
        string siteDir = HtmlDemoImporter.GetSiteDir(options);
        string themeDir = HtmlDemoImporter.GetThemeDir(options);
        string siteYamlPath = Path.Combine(siteDir, "site.yaml");
        string pagesDir = Path.Combine(themeDir, "layouts", "pages");
        string contentDir = Path.Combine(siteDir, "content");

        if (!File.Exists(siteYamlPath))
        {
            diagnostics.Add(Error("import.lightVerifyMissingSiteYaml", "site.yaml was not generated.", siteYamlPath));
        }
        else if (!CanParseYaml(siteYamlPath))
        {
            diagnostics.Add(Error("import.lightVerifyInvalidSiteYaml", "site.yaml could not be parsed.", siteYamlPath));
        }

        if (!Directory.Exists(themeDir))
        {
            diagnostics.Add(Error("import.lightVerifyMissingTheme", "Generated theme directory is missing.", themeDir));
        }

        if (!Directory.Exists(pagesDir) ||
            !Directory.EnumerateFiles(pagesDir, "*.html", SearchOption.TopDirectoryOnly).Any())
        {
            diagnostics.Add(Error("import.lightVerifyMissingTemplates", "Generated page templates are missing.", pagesDir));
        }

        if (options.ExtractContent &&
            (!Directory.Exists(contentDir) || !Directory.EnumerateFiles(contentDir, "*.md", SearchOption.AllDirectories).Any()))
        {
            diagnostics.Add(Error("import.lightVerifyMissingContent", "Generated Markdown content files are missing.", contentDir));
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(new ImportDiagnostic(
                ImportDiagnosticSeverity.Info,
                "import.lightVerifyPassed",
                "Light verification passed.",
                siteYamlPath));
        }

        return diagnostics;
    }

    private static bool CanParseYaml(string path)
    {
        try
        {
            var stream = new YamlStream();
            using var reader = File.OpenText(path);
            stream.Load(reader);
            return stream.Documents.Count > 0;
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static ImportDiagnostic Error(string code, string message, string path)
        => new(ImportDiagnosticSeverity.Error, code, message, path);
}
