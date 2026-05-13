namespace Bukit.Cli.Commands;

public static class InitCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var targetDir = reader.GetArg(1);
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            Console.Error.WriteLine("init requires a target directory.");
            return Task.FromResult(2);
        }

        var provider = (reader.GetOption("--provider") ?? "markdown").Trim().ToLowerInvariant();
        var templateName = (reader.GetOption("--template") ?? "minimal").Trim();

        var root = Path.GetFullPath(targetDir);
        Directory.CreateDirectory(root);

        var themeRoot = Path.Combine(root, "themes", "starter");

        Directory.CreateDirectory(Path.Combine(root, "content"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "static"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "partials"));

        WriteFile(root, ".gitignore", "dist/\n.bukit/\n");
        WriteFile(root, "README.md", $"# {Path.GetFileName(root)}\n\nPowered by bukit\n");
        WriteFile(root, Path.Combine("content", "hello-world.md"), "# Hello World\n\n这是一个示例页面。\n");
        StarterThemeScaffold.WriteTo(root);

        WriteFile(root, "site.yaml", BuildSiteYaml(provider, templateName));

        Console.WriteLine($"Initialized: {root}");
        return Task.FromResult(0);
    }

    private static void WriteFile(string rootDir, string relativePath, string content)
    {
        var path = Path.Combine(rootDir, relativePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, content);
    }

    private static string BuildSiteYaml(string provider, string templateName)
    {
        if (provider == "notion")
        {
            return """
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/

content:
  provider: notion
  notion:
    databaseId: xxxxx

build:
  output: dist
  clean: true

theme:
  name: starter
  layouts: layouts
  assets: assets
  static: static
  params:
    brand: My Site
    footer_text: My Site

logging:
  level: info
""";
        }

        return """
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/

content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page

build:
  output: dist
  clean: true

theme:
  name: starter
  layouts: layouts
  assets: assets
  static: static
  params:
    brand: My Site
    footer_text: My Site

logging:
  level: info
""";
    }

}
