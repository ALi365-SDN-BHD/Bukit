using System.Text;
using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

internal static class CloneFidelityRunner
{
    internal static async Task<int> RunAsync(string rootDir, string themeName, string htmlDir, bool force, bool use, ArgReader? reader)
    {
        var fullHtmlDir = Path.GetFullPath(htmlDir);
        if (!Directory.Exists(fullHtmlDir))
        {
            Console.Error.WriteLine($"HTML directory not found: {fullHtmlDir}");
            return 2;
        }

        var themeDir = Path.Combine(rootDir, "themes", themeName);
        if (Directory.Exists(themeDir))
        {
            if (!force)
            {
                Console.Error.WriteLine($"Theme already exists: {themeName}. Use --force to overwrite.");
                return 2;
            }

            Directory.Delete(themeDir, recursive: true);
        }

        Console.WriteLine($"Fidelity clone from: {fullHtmlDir}");

        CloneFidelityGenerator.FidelityResult result;
        try
        {
            result = CloneFidelityGenerator.Generate(rootDir, fullHtmlDir, themeName);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fidelity clone failed: {ex.Message}");
            return 1;
        }

        WriteFidelitySiteYaml(rootDir, themeName);
        TransferAssetsToStatic(rootDir, themeName);

        Console.WriteLine($"Theme cloned (fidelity mode): {themeName}");
        Console.WriteLine($"  Templates: {result.TemplateCount}");
        Console.WriteLine($"  Partials: {result.PartialCount}");
        Console.WriteLine($"  Assets: {result.AssetCount}");
        Console.WriteLine($"  Pages detected: {result.PageCount}");
        foreach (var w in result.Warnings)
            Console.WriteLine($"  Warning: {w}");

        if (use && reader is not null)
        {
            var useResult = await ThemeCommand.SetThemeAsync(themeName, reader, brand: null, primaryColor: null, accentColor: null);
            if (useResult != 0)
                return useResult;
        }

        return 0;
    }

    private static void WriteFidelitySiteYaml(string rootDir, string themeName)
    {
        var htmlFiles = Directory.GetFiles(
            Path.Combine(rootDir, "themes", themeName, "layouts", "pages"), "*.html")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not "index" and not "list")
            .Select(n => n!.Replace("-", " "))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("site:");
        sb.AppendLine($"  name: {themeName}");
        sb.AppendLine($"  title: {themeName}");
        sb.AppendLine("  baseUrl: /");
        sb.AppendLine("  language: zh-CN");
        sb.AppendLine("  seo:");
        sb.AppendLine("    renderMode: 'off'");
        sb.AppendLine("  collections:");
        sb.AppendLine("    page:");
        sb.AppendLine("      permalink: '/{slug}/'");
        sb.AppendLine("      template: 'pages/page.html'");
        sb.AppendLine("      listRoute: '/'");
        sb.AppendLine("content:");
        sb.AppendLine("  provider: markdown");
        sb.AppendLine("  contentDir: content");
        sb.AppendLine("theme:");
        sb.AppendLine($"  name: {themeName}");

        var path = Path.Combine(rootDir, "site.yaml");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, sb.ToString());
        }
        else
        {
            Console.WriteLine("  site.yaml already exists, skipping. Generated config would be:");
            Console.WriteLine(sb.ToString());
        }
    }

    private static void TransferAssetsToStatic(string rootDir, string themeName)
    {
        var themeBase = Path.Combine(rootDir, "themes", themeName);

        var themeAssetsDir = Path.Combine(themeBase, "assets");
        var themeStaticDir = Path.Combine(themeBase, "static");

        if (Directory.Exists(themeAssetsDir))
        {
            if (!Directory.Exists(themeStaticDir))
                Directory.CreateDirectory(themeStaticDir);

            foreach (var file in Directory.GetFiles(themeAssetsDir, "*.*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(themeAssetsDir, file);
                var dest = Path.Combine(themeStaticDir, rel);
                if (!File.Exists(dest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Move(file, dest);
                }
            }
        }
    }
}
