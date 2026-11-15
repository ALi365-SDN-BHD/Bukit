using System.Text;
using Bukit.Shared;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Bukit.Cli.Commands;

internal static class StarterSiteScaffold
{
    private const string ThemePrefix = "Bukit.Cli.Resources.StarterTheme.";
    private const string SitePrefix = "Bukit.Cli.Resources.StarterSite.";

    private static readonly StarterFile[] DefaultEntries =
    [
        new("BaseLayout.html", "themes/starter/layouts/layouts/base.html"),
        new("SeoPartial.html", "themes/starter/layouts/partials/seo.html"),
        new("HeaderPartial.html", "themes/starter/layouts/partials/header.html"),
        new("FooterPartial.html", "themes/starter/layouts/partials/footer.html"),
        new("ListCardPartial.html", "themes/starter/layouts/partials/list-card.html"),
        new("PaginationNavPartial.html", "themes/starter/layouts/partials/pagination-nav.html"),
        new("PageTemplate.html", "themes/starter/layouts/pages/page.html"),
        new("PostTemplate.html", "themes/starter/layouts/pages/post.html"),
        new("IndexTemplate.html", "themes/starter/layouts/pages/index.html"),
        new("ListTemplate.html", "themes/starter/layouts/pages/list.html"),
        new("PaginationTemplate.html", "themes/starter/layouts/pages/pagination.html"),
        new("TaxonomyIndexTemplate.html", "themes/starter/layouts/pages/taxonomy-index.html"),
        new("TaxonomyTermTemplate.html", "themes/starter/layouts/pages/taxonomy-term.html"),
        new("SearchTemplate.html", "themes/starter/layouts/pages/search.html"),
        new("TemplateCapabilities.yaml", "themes/starter/layouts/bukit.templates.yaml"),
        new("ThemeYaml.yaml", "themes/starter/theme.yaml"),
        new("StyleCss.css", "themes/starter/assets/style.css"),
        new("LICENSE", "themes/starter/LICENSE"),
        new("og-default.png", "themes/starter/assets/og-default.png", Binary: true),
        new("WelcomePost.md", "content/posts/welcome.md", Site: true),
        new("AboutPage.md", "content/pages/about.md", Site: true),
        new("FeatureData.md", "data/features-10.md", Site: true)
    ];

    internal static StarterSiteResult Create(
        string target, string title, string slug, string language)
        => Create(target, title, slug, language, DefaultEntries);

    internal static StarterSiteResult Create(
        string target, string title, string slug, string language,
        IReadOnlyList<StarterFile> entries)
    {
        // Trim the trailing separator so "broken/" is checked as "broken";
        // Path.TrimEndingDirectorySeparator keeps a filesystem root unchanged.
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
        ValidateTarget(root);
        var created = new List<CreatedEntry>();

        try
        {
            WriteText(root, "site.yaml", BuildSiteYaml(title, slug, language), created);
            foreach (var entry in entries)
            {
                if (entry.Binary)
                {
                    WriteBytes(root, entry.Destination,
                        entry.Site ? GetSiteBytes(entry.Resource) : GetThemeBytes(entry.Resource),
                        created);
                }
                else
                {
                    WriteText(root, entry.Destination,
                        entry.Site ? GetSiteText(entry.Resource) : GetThemeText(entry.Resource),
                        created);
                }
            }
            WriteText(root, ".gitignore", "dist/\n.bukit/\n.cache/\n", created);
            WriteText(root, "themes/starter/static/robots.txt",
                "User-agent: *\nAllow: /\n", created);
            return new StarterSiteResult(root);
        }
        catch
        {
            // shortcut: a cleanup failure can mask the original exception; low
            // probability. Do not swallow cleanup errors; a later hardening pass
            // can collect them and rethrow the original.
            for (var i = created.Count - 1; i >= 0; i--)
            {
                var item = created[i];
                if (item.IsDirectory)
                {
                    if (Directory.Exists(item.Path) &&
                        !Directory.EnumerateFileSystemEntries(item.Path).Any())
                    {
                        Directory.Delete(item.Path);
                    }
                }
                else if (File.Exists(item.Path))
                {
                    File.Delete(item.Path);
                }
            }
            throw;
        }
    }

    internal static string GetThemeText(string name) => ReadText(ThemePrefix + name);

    internal static byte[] GetThemeBytes(string name) => ReadBytes(ThemePrefix + name);

    internal static string GetSiteText(string name) => ReadText(SitePrefix + name);

    internal static byte[] GetSiteBytes(string name) => ReadBytes(SitePrefix + name);

    private static void WriteText(
        string root, string relative, string content, List<CreatedEntry> created)
    {
        var path = ResolvePath(root, relative);
        CreateDirectoryTree(Path.GetDirectoryName(path)!, created);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        created.Add(new CreatedEntry(path, IsDirectory: false));
    }

    private static void WriteBytes(
        string root, string relative, byte[] content, List<CreatedEntry> created)
    {
        var path = ResolvePath(root, relative);
        CreateDirectoryTree(Path.GetDirectoryName(path)!, created);
        File.WriteAllBytes(path, content);
        created.Add(new CreatedEntry(path, IsDirectory: false));
    }

    private static string ResolvePath(string root, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"starter path escapes the target: {relative}");
        }

        return path;
    }

    private static void CreateDirectoryTree(string directory, List<CreatedEntry> created)
    {
        var full = Path.GetFullPath(directory);
        var missing = new Stack<string>();
        // current can become null at a drive root such as Z:\ on Windows; stop there,
        // never call Directory.Exists(null).
        string? current = full;
        while (current is not null && !Directory.Exists(current))
        {
            missing.Push(current);
            current = Path.GetDirectoryName(current);
        }

        while (missing.Count > 0)
        {
            var path = missing.Pop();
            Directory.CreateDirectory(path);
            created.Add(new CreatedEntry(path, IsDirectory: true));
        }
    }

    private static string BuildSiteYaml(string title, string slug, string language)
    {
        var text = GetSiteText("SiteYaml.yaml");
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var site = (YamlMappingNode)root.Children[new YamlScalarNode("site")];
        site.Children[new YamlScalarNode("name")] = new YamlScalarNode(slug);
        site.Children[new YamlScalarNode("title")] =
            new YamlScalarNode(title) { Style = ScalarStyle.DoubleQuoted };
        site.Children[new YamlScalarNode("languages")] =
            new YamlSequenceNode(new YamlScalarNode(language));
        var theme = (YamlMappingNode)root.Children[new YamlScalarNode("theme")];
        var parameters = (YamlMappingNode)theme.Children[new YamlScalarNode("params")];
        parameters.Children[new YamlScalarNode("brand")] =
            new YamlScalarNode(title) { Style = ScalarStyle.DoubleQuoted };
        parameters.Children[new YamlScalarNode("footer_text")] =
            new YamlScalarNode(title) { Style = ScalarStyle.DoubleQuoted };
        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    private static void ValidateTarget(string root)
    {
        var info = new DirectoryInfo(root);
        if (info.LinkTarget is not null ||
            (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0))
        {
            throw new CommandArgumentException($"{root} is a symbolic link or reparse point.");
        }

        if (File.Exists(root))
        {
            throw new CommandArgumentException($"{root} is a file.");
        }

        if (Directory.Exists(root) &&
            Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new CommandArgumentException($"{root} must be empty.");
        }
    }

    private static string ReadText(string resourceName)
    {
        using var stream = OpenResource(resourceName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] ReadBytes(string resourceName)
    {
        using var stream = OpenResource(resourceName);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static Stream OpenResource(string resourceName)
    {
        var assembly = typeof(StarterSiteScaffold).Assembly;
        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Bukit installation is missing starter resource '{resourceName}'.");
    }

    private sealed record CreatedEntry(string Path, bool IsDirectory);
}

internal sealed record StarterFile(
    string Resource, string Destination, bool Binary = false, bool Site = false);

internal sealed record StarterSiteResult(string Root);
