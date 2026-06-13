using System.Text;
using Bukit.Cli;
using Bukit.Cli.Cli.Binding;
using Scriban;

namespace Bukit.Labs.Cli.Commands;

public static class TemplateCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var sub = command.GetArgument(0);
        if (string.IsNullOrWhiteSpace(sub))
        {
            Console.Error.WriteLine("Usage: bukit template <create|list|show|validate|snippets|hints|sync>");
            return Task.FromResult(2);
        }

        return sub switch
        {
            "create" => CreateAsync(command),
            "list" => ListAsync(command),
            "show" => ShowAsync(command),
            "validate" => ValidateAsync(command),
            "snippets" => SnippetsAsync(command),
            "hints" => HintsAsync(command),
            "sync" => SyncAsync(command),
            _ => Task.FromResult(Unknown(sub))
        };
    }

    private static Task<int> CreateAsync(CliBoundCommand command)
    {
        var name = command.GetArgument(1);
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('-'))
        {
            Console.Error.WriteLine("Missing template name. Usage: bukit template create <path>");
            return Task.FromResult(2);
        }

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;

        var activeThemeName = ResolveActiveThemeName(resolved, rootDir);
        if (string.IsNullOrWhiteSpace(activeThemeName))
        {
            Console.Error.WriteLine("No active theme found. Set one with: bukit theme use <name>");
            return Task.FromResult(2);
        }

        var layoutsDir = Path.Combine(rootDir, "themes", activeThemeName, "layouts");
        if (!Directory.Exists(layoutsDir))
        {
            Console.Error.WriteLine($"Layouts directory not found for theme '{activeThemeName}'.");
            return Task.FromResult(2);
        }

        string templatePath;
        try
        {
            templatePath = ResolveTemplatePath(layoutsDir, name);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return Task.FromResult(2);
        }

        if (File.Exists(templatePath))
        {
            var force = command.GetBool("--force");
            if (!force)
            {
                Console.Error.WriteLine($"Template already exists: {name}. Use --force to overwrite.");
                return Task.FromResult(2);
            }
        }

        var dir = Path.GetDirectoryName(templatePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Console.WriteLine("Template type:");
        Console.WriteLine("  1. Single page (page template)");
        Console.WriteLine("  2. List page (collection list)");
        Console.WriteLine("  3. Partial (reusable component)");
        var typeChoice = Prompt("Choose", "1");

        var includeLayout = typeChoice != "3" && PromptBool("Include layout inheritance (base.html)", true);
        var includeHeader = typeChoice != "3" && PromptBool("Include header partial", true);
        var includeFooter = typeChoice != "3" && PromptBool("Include footer partial", true);

        var showDate = typeChoice == "1" && PromptBool("Show publish date", false);
        var showTags = typeChoice == "2" && PromptBool("Show tags loop", false);

        var sb = new StringBuilder();

        if (includeLayout)
        {
            sb.AppendLine("{% layout \"layouts/base.html\" %}");
            sb.AppendLine();
        }

        if (includeHeader)
        {
            sb.AppendLine("{{ include \"partials/header.html\" }}");
        }

        if (typeChoice == "1")
        {
            sb.AppendLine();
            sb.AppendLine("<article>");
            sb.AppendLine("  <h1>{{ page.title }}</h1>");
            if (showDate)
            {
                sb.AppendLine("  {{ if page.publish_date }}");
                sb.AppendLine("    <time>{{ page.publish_date | date.to_string \"%Y-%m-%d\" }}</time>");
                sb.AppendLine("  {{ end }}");
            }
            sb.AppendLine("  <div class=\"content\">");
            sb.AppendLine("    {{ page.content }}");
            sb.AppendLine("  </div>");
            sb.AppendLine("</article>");
        }
        else if (typeChoice == "2")
        {
            sb.AppendLine();
            sb.AppendLine("<h1>{{ page.title }}</h1>");
            sb.AppendLine();
            sb.AppendLine("{{ for p in pages }}");
            sb.AppendLine("  <article>");
            sb.AppendLine("    <h2><a href=\"{{ site.base_url }}{{ p.url }}\">{{ p.title }}</a></h2>");
            if (showDate)
            {
                sb.AppendLine("    {{ if p.publish_date }}");
                sb.AppendLine("      <time>{{ p.publish_date | date.to_string \"%Y-%m-%d\" }}</time>");
                sb.AppendLine("    {{ end }}");
            }
            sb.AppendLine("    {{ if p.summary }}<p>{{ p.summary }}</p>{{ end }}");
            if (showTags)
            {
                sb.AppendLine("    {{ if p.fields.tags }}");
                sb.AppendLine("      <div class=\"tags\">");
                sb.AppendLine("        {{ for tag in p.fields.tags.value }}");
                sb.AppendLine("          <span class=\"tag\">{{ tag }}</span>");
                sb.AppendLine("        {{ end }}");
                sb.AppendLine("      </div>");
                sb.AppendLine("    {{ end }}");
            }
            sb.AppendLine("  </article>");
            sb.AppendLine("{{ end }}");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("{{-- partial: " + Path.GetFileNameWithoutExtension(name) + " --}}");
            sb.AppendLine("{{ if site.params.show_sidebar }}");
            sb.AppendLine("  <aside class=\"sidebar\">");
            sb.AppendLine("  </aside>");
            sb.AppendLine("{{ end }}");
        }

        if (includeFooter)
        {
            sb.AppendLine();
            sb.AppendLine("{{ include \"partials/footer.html\" }}");
        }

        var content = sb.ToString();
        File.WriteAllText(templatePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Console.WriteLine($"Created: themes/{activeThemeName}/layouts/{name}");

        var tag = typeChoice switch
        {
            "1" => "single page",
            "2" => "list page",
            _ => "partial"
        };
        Console.WriteLine($"Type: {tag}");

        return Task.FromResult(0);
    }

    private static Task<int> ListAsync(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;

        var activeThemeName = ResolveActiveThemeName(resolved, rootDir);
        if (string.IsNullOrWhiteSpace(activeThemeName))
        {
            Console.Error.WriteLine("No active theme found. Set one with: bukit theme use <name>");
            return Task.FromResult(2);
        }

        var layoutsDir = Path.Combine(rootDir, "themes", activeThemeName, "layouts");
        if (!Directory.Exists(layoutsDir))
        {
            Console.WriteLine($"No templates found for theme '{activeThemeName}'.");
            return Task.FromResult(0);
        }

        var files = Directory.GetFiles(layoutsDir, "*.html", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine($"No templates found for theme '{activeThemeName}'.");
            return Task.FromResult(0);
        }

        Console.WriteLine($"Templates for theme '{activeThemeName}':");

        var grouped = files.GroupBy(f =>
        {
            var relative = Path.GetRelativePath(layoutsDir, f);
            var dirPart = Path.GetDirectoryName(relative);
            return dirPart ?? "root";
        });

        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            Console.WriteLine($"  [{group.Key}/]");
            foreach (var file in group)
            {
                var name = Path.GetFileName(file);
                var size = new FileInfo(file).Length;
                Console.WriteLine($"    {name,-30} {size,6} bytes");
            }
        }

        return Task.FromResult(0);
    }

    private static async Task<int> ShowAsync(CliBoundCommand command)
    {
        var name = command.GetArgument(1);
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('-'))
        {
            Console.Error.WriteLine("Missing template name. Usage: bukit template show <path>");
            return 2;
        }

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;

        var activeThemeName = ResolveActiveThemeName(resolved, rootDir);
        if (string.IsNullOrWhiteSpace(activeThemeName))
        {
            Console.Error.WriteLine("No active theme found.");
            return 2;
        }

        var layoutsDir = Path.Combine(rootDir, "themes", activeThemeName, "layouts");
        string templatePath;
        try
        {
            templatePath = ResolveTemplatePath(layoutsDir, name);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        if (!File.Exists(templatePath))
        {
            Console.Error.WriteLine($"Template not found: {name}");
            return 2;
        }

        var content = await File.ReadAllTextAsync(templatePath);
        Console.WriteLine($"=== themes/{activeThemeName}/layouts/{name} ===");
        Console.WriteLine();
        Console.WriteLine(content);

        return 0;
    }

    private static async Task<int> ValidateAsync(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;

        var activeThemeName = ResolveActiveThemeName(resolved, rootDir);
        if (string.IsNullOrWhiteSpace(activeThemeName))
        {
            Console.Error.WriteLine("No active theme found.");
            return 2;
        }

        var layoutsDir = Path.Combine(rootDir, "themes", activeThemeName, "layouts");
        if (!Directory.Exists(layoutsDir))
        {
            Console.Error.WriteLine("No layouts directory found.");
            return 2;
        }

        var files = Directory.GetFiles(layoutsDir, "*.html", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine("No template files to validate.");
            return 0;
        }

        var errorCount = 0;
        var okCount = 0;

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(layoutsDir, file);
            var text = await File.ReadAllTextAsync(file);
            var template = Template.Parse(text, relative);

            if (template.HasErrors)
            {
                errorCount++;
                Console.WriteLine($"✖ {relative}");
                foreach (var msg in template.Messages)
                {
                    Console.WriteLine($"  - {msg}");
                }
            }
            else
            {
                okCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Validated: {okCount} OK, {errorCount} errors in {files.Count} files.");

        return errorCount > 0 ? 1 : 0;
    }

    private static Task<int> HintsAsync(CliBoundCommand command)
    {
        Console.WriteLine("Available template variables:");
        Console.WriteLine();
        Console.WriteLine("  Site (global):");
        Console.WriteLine("    site.name           — string        Site identifier");
        Console.WriteLine("    site.title          — string        Site title");
        Console.WriteLine("    site.url            — string|null   Full site URL");
        Console.WriteLine("    site.description    — string|null   Site description");
        Console.WriteLine("    site.base_url       — string        Root path prefix");
        Console.WriteLine("    site.language       — string        Current language code");
        Console.WriteLine("    site.theme.params.* — dynamic       Theme parameters from site.yaml");
        Console.WriteLine("    site.data.*         — dynamic       Data modules (mode: data)");
        Console.WriteLine();
        Console.WriteLine("  Page (single page templates):");
        Console.WriteLine("    page.title          — string        Page title");
        Console.WriteLine("    page.url            — string        Relative page URL");
        Console.WriteLine("    page.content        — string        HTML content");
        Console.WriteLine("    page.summary        — string|null   Auto-generated summary");
        Console.WriteLine("    page.publish_date   — DateTime|null Publish date");
        Console.WriteLine("    page.fields.*       — dynamic       Custom fields (tags, author, etc.)");
        Console.WriteLine();
        Console.WriteLine("  Pages (list/index templates):");
        Console.WriteLine("    pages[]             — array         Sorted descending by publish date");
        Console.WriteLine("      p.title           — string        Page title");
        Console.WriteLine("      p.url             — string        Relative URL");
        Console.WriteLine("      p.content         — string        HTML content");
        Console.WriteLine("      p.summary         — string|null   Summary");
        Console.WriteLine("      p.publish_date    — DateTime|null Publish date");
        Console.WriteLine("      p.fields.*        — dynamic       Custom fields");
        Console.WriteLine();
        Console.WriteLine("  Scriban built-in functions:");
        Console.WriteLine("    date.now, date.parse, date.to_string");
        Console.WriteLine("    string.downcase, string.upcase, string.slice");
        Console.WriteLine("    array.size, array.limit, array.offset");
        Console.WriteLine("    math.round, math.ceil, math.floor");
        Console.WriteLine();
        Console.WriteLine("  Layout directives:");
        Console.WriteLine("    {%% layout \"layouts/base.html\" %%}");
        Console.WriteLine("    {{ include \"partials/header.html\" }}");
        Console.WriteLine("    {{ content }}  — child template content placeholder");
        return Task.FromResult(0);
    }

    private static string ResolveTemplatePath(string layoutsDir, string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            throw new ArgumentException("Template path must be non-empty.");
        }

        if (Path.IsPathRooted(templateName))
        {
            throw new ArgumentException("Template path must be relative to the layouts directory.");
        }

        var normalized = templateName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(layoutsDir, normalized));
        var safeRoot = Path.GetFullPath(layoutsDir) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(safeRoot, Bukit.Shared.PlatformPathHelper.PathComparison))
        {
            throw new ArgumentException("Template path must stay inside the layouts directory.");
        }

        return resolved;
    }

    private static Task<int> SyncAsync(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;

        var activeThemeName = ResolveActiveThemeName(resolved, rootDir);
        if (string.IsNullOrWhiteSpace(activeThemeName))
        {
            Console.Error.WriteLine("No active theme found. Set one with: bukit theme use <name>");
            return Task.FromResult(2);
        }

        var layoutsDir = Path.Combine(rootDir, "themes", activeThemeName, "layouts");
        if (!Directory.Exists(layoutsDir))
        {
            Console.Error.WriteLine($"Layouts directory not found for theme '{activeThemeName}'.");
            return Task.FromResult(2);
        }

        var force = command.GetBool("--force");
        var manifestPath = Path.Combine(layoutsDir, "bukit.templates.yaml");

        if (File.Exists(manifestPath) && !force)
        {
            Console.WriteLine("bukit.templates.yaml already exists. Use --force to override.");
            return Task.FromResult(0);
        }

        var htmlFiles = Directory.GetFiles(layoutsDir, "*.html", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("templates:");

        foreach (var file in htmlFiles)
        {
            var relative = Path.GetRelativePath(layoutsDir, file).Replace('\\', '/');
            var text = File.ReadAllText(file);

            var needsPageContent = text.Contains("p.content", StringComparison.Ordinal) ||
                                   text.Contains("item.content", StringComparison.Ordinal);
            var supportsPagination = relative.StartsWith("pages/pagination", StringComparison.OrdinalIgnoreCase);
            var supportsTaxonomy = relative.StartsWith("pages/taxonomy", StringComparison.OrdinalIgnoreCase);
            var supportsSearch = relative.StartsWith("pages/search", StringComparison.OrdinalIgnoreCase);

            sb.AppendLine($"  {relative}:");
            sb.AppendLine("    capabilities:");
            sb.AppendLine($"      needs_page_content: {needsPageContent.ToString().ToLowerInvariant()}");
            sb.AppendLine($"      supports_pagination: {supportsPagination.ToString().ToLowerInvariant()}");
            sb.AppendLine($"      supports_taxonomy: {supportsTaxonomy.ToString().ToLowerInvariant()}");
            sb.AppendLine($"      supports_search_snippets: {supportsSearch.ToString().ToLowerInvariant()}");
        }

        File.WriteAllText(manifestPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine($"Generated: themes/{activeThemeName}/layouts/bukit.templates.yaml");
        Console.WriteLine($"  {htmlFiles.Count} template(s) declared.");
        return Task.FromResult(0);
    }

    private static Task<int> SnippetsAsync(CliBoundCommand command)
    {
        var filter = command.GetArgument(1);

        if (!string.IsNullOrWhiteSpace(filter) && !filter.StartsWith('-'))
        {
            if (TemplateSnippets.ScribanSnippets.TryGetValue(filter, out var scribanSnippet))
            {
                Console.WriteLine($"=== Scriban snippet: {filter} ===");
                Console.WriteLine();
                Console.WriteLine(scribanSnippet);
            }
            else
            {
                Console.WriteLine($"Snippet '{filter}' not found.");
            }

            if (TemplateSnippets.CssSnippets.TryGetValue(filter, out var cssSnippet))
            {
                Console.WriteLine();
                Console.WriteLine($"=== CSS snippet: {filter} ===");
                Console.WriteLine();
                Console.WriteLine(cssSnippet);
            }

            return Task.FromResult(0);
        }

        Console.WriteLine("Available template snippets:");
        Console.WriteLine();
        Console.WriteLine("  Scriban templates:");
        foreach (var key in TemplateSnippets.ScribanSnippets.Keys.OrderBy(k => k))
        {
            Console.WriteLine($"    {key,-22}");
        }

        Console.WriteLine();
        Console.WriteLine("  CSS styles:");
        foreach (var key in TemplateSnippets.CssSnippets.Keys.OrderBy(k => k))
        {
            Console.WriteLine($"    {key,-22}");
        }

        Console.WriteLine();
        Console.WriteLine("Usage: bukit template snippets <name>");
        return Task.FromResult(0);
    }

    private static string? ResolveActiveThemeName(ResolvedConfigPath resolved, string rootDir)
    {
        if (!File.Exists(resolved.FullConfigPath))
        {
            return null;
        }

        try
        {
            var yaml = File.ReadAllText(resolved.FullConfigPath);
            var stream = new YamlDotNet.RepresentationModel.YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count > 0 &&
                stream.Documents[0].RootNode is YamlDotNet.RepresentationModel.YamlMappingNode root &&
                root.Children.TryGetValue(new YamlDotNet.RepresentationModel.YamlScalarNode("theme"), out var themeNode) &&
                themeNode is YamlDotNet.RepresentationModel.YamlMappingNode themeMap &&
                themeMap.Children.TryGetValue(new YamlDotNet.RepresentationModel.YamlScalarNode("name"), out var nameNode) &&
                nameNode is YamlDotNet.RepresentationModel.YamlScalarNode nameScalar)
            {
                return CloneModels.IsSafeThemeName(nameScalar.Value) ? nameScalar.Value : null;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string Prompt(string prompt, string defaultValue)
    {
        Console.Write($"{prompt} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }

    private static bool PromptBool(string prompt, bool defaultValue)
    {
        var yn = defaultValue ? "[Y/n]" : "[y/N]";
        Console.Write($"{prompt}? {yn}: ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return input is "y" or "yes";
    }

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine($"Unknown template subcommand: {sub}");
        return 2;
    }
}
