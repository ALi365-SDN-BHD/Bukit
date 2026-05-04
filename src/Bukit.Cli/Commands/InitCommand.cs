using System.Text;

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
        WriteFile(root, Path.Combine("themes", "starter", "assets", "style.css"), DefaultStyleCss);

        WriteFile(root, Path.Combine("themes", "starter", "layouts", "layouts", "base.html"), BaseLayout);
        WriteFile(root, Path.Combine("themes", "starter", "layouts", "partials", "header.html"), HeaderPartial);
        WriteFile(root, Path.Combine("themes", "starter", "layouts", "partials", "footer.html"), FooterPartial);
        WriteFile(root, Path.Combine("themes", "starter", "layouts", "pages", "page.html"), PageTemplate);
        WriteFile(root, Path.Combine("themes", "starter", "layouts", "pages", "post.html"), PostTemplate);
        WriteFile(root, Path.Combine("themes", "starter", "layouts", "pages", "index.html"), IndexTemplate);
        WriteFile(root, Path.Combine("themes", "starter", "layouts", "pages", "list.html"), ListTemplate);

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

        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

logging:
  level: info
""";
    }

    private const string DefaultStyleCss = """
body {
  margin: 0;
  font-family: system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, "Noto Sans", "PingFang SC", "Microsoft YaHei", sans-serif;
  line-height: 1.6;
}

.container {
  max-width: 860px;
  margin: 0 auto;
  padding: 24px;
}

a {
  color: #0b5fff;
  text-decoration: none;
}

a:hover {
  text-decoration: underline;
}

nav a {
  margin-right: 12px;
}

footer {
  margin-top: 32px;
  opacity: 0.8;
}

.content h1, .content h2, .content h3 {
  margin-top: 1.2em;
}

/* ── Callout ─── */
.callout {
  display: flex;
  padding: 16px;
  border-radius: 4px;
  background: #f7f6f3;
  margin: 8px 0;
}
.callout-icon {
  margin-right: 8px;
  font-size: 1.3em;
  flex-shrink: 0;
}
.callout-content {
  flex: 1;
  min-width: 0;
}

/* ── To-do ─── */
.to-do {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 3px 0;
}
.to-do input[type="checkbox"] {
  margin-top: 4px;
}

/* ── Bookmark ─── */
a.bookmark {
  display: block;
  padding: 12px 14px;
  border: 1px solid #e3e2e0;
  border-radius: 4px;
  color: inherit;
  text-decoration: none;
  margin: 4px 0;
}
a.bookmark:hover {
  background: #f7f6f3;
}

/* ── Video embed ─── */
.video-embed {
  position: relative;
  padding-bottom: 56.25%;
  height: 0;
  overflow: hidden;
  margin: 8px 0;
}
.video-embed iframe {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  border: none;
}

/* ── Math block ─── */
.math-block {
  text-align: center;
  padding: 16px 0;
  overflow-x: auto;
}

/* ── Inline math ─── */
.math-inline {
  font-family: 'KaTeX_Main', serif;
}

/* ── Code block ─── */
pre {
  background: #f7f6f3;
  padding: 16px;
  border-radius: 4px;
  overflow-x: auto;
  font-size: 0.9em;
}
pre code {
  font-family: 'SFMono-Regular', Menlo, Consolas, monospace;
}

/* ── Table ─── */
table {
  border-collapse: collapse;
  width: 100%;
  margin: 8px 0;
}
th, td {
  border: 1px solid #e3e2e0;
  padding: 6px 10px;
  text-align: left;
}
th {
  background: #f7f6f3;
  font-weight: 600;
}

/* ── Toggle ─── */
details {
  margin: 4px 0;
}
details summary {
  cursor: pointer;
  font-weight: 500;
}
details summary::marker {
  color: #9b9a97;
}

/* ── Blockquote ─── */
blockquote {
  border-left: 3px solid #e3e2e0;
  padding-left: 14px;
  margin: 4px 0;
  color: inherit;
}

/* ── Divider ─── */
hr {
  border: none;
  border-top: 1px solid #e3e2e0;
  margin: 16px 0;
}

/* ── Figure/Image ─── */
figure {
  margin: 8px 0;
  text-align: center;
}
figcaption {
  color: #9b9a97;
  font-size: 0.85em;
  margin-top: 4px;
}
img {
  max-width: 100%;
  height: auto;
}

/* ── Notion colors (foreground) ─── */
.notion-gray { color: #787774; }
.notion-brown { color: #64473A; }
.notion-orange { color: #D9730D; }
.notion-yellow { color: #DFAB01; }
.notion-green { color: #0F7B6C; }
.notion-blue { color: #0B6E99; }
.notion-purple { color: #6940A5; }
.notion-pink { color: #AD1A72; }
.notion-red { color: #E03E3E; }

/* ── Notion colors (background) ─── */
.notion-gray_background { background-color: #F1F1EF; }
.notion-brown_background { background-color: #F4EEEE; }
.notion-orange_background { background-color: #FBECDD; }
.notion-yellow_background { background-color: #FBF3DB; }
.notion-green_background { background-color: #EDF3EC; }
.notion-blue_background { background-color: #E7F3F8; }
.notion-purple_background { background-color: #F6F3F9; }
.notion-pink_background { background-color: #F9F0F5; }
.notion-red_background { background-color: #FDEBEC; }

/* ── Children containers / extra Notion blocks ─── */
.callout-children {
  margin-top: 8px;
}
.to-do-children {
  margin: 4px 0 4px 24px;
}
.notion-columns {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 16px;
  margin: 8px 0;
}
.notion-column {
  min-width: 0;
}
.notion-file,
.notion-pdf,
.notion-child-page,
.notion-child-database {
  margin: 6px 0;
}
""";

    private const string BaseLayout = """
<!DOCTYPE html>
<html lang="{{ site.language }}">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{ page.title }} - {{ site.title }}</title>
  <link rel="stylesheet" href="{{ site.base_url }}/assets/style.css" />
</head>
<body>
  {{ include "partials/header.html" }}
  <main class="container">
    {{ content }}
  </main>
  {{ include "partials/footer.html" }}
</body>
</html>
""";

    private const string HeaderPartial = """
<header>
  <nav>
    <a href="{{ site.base_url }}/">首页</a>
    <a href="{{ site.base_url }}/blog/">博客</a>
    <a href="{{ site.base_url }}/pages/">页面</a>
  </nav>
</header>
""";

    private const string FooterPartial = """
<footer>
  <small>Powered by bukit</small>
</footer>
""";

    private const string PageTemplate = """
{% layout "layouts/base.html" %}

<article>
  <h1>{{ page.title }}</h1>
  <div class="content">
    {{ page.content }}
  </div>
</article>
""";

    private const string PostTemplate = """
{% layout "layouts/base.html" %}

<article>
  <h1>{{ page.title }}</h1>
  {{ if page.publish_date }}
    <small>{{ page.publish_date | date.to_string "%Y-%m-%d" }}</small>
  {{ end }}
  <div class="content">
    {{ page.content }}
  </div>
</article>
""";

    private const string IndexTemplate = """
{% layout "layouts/base.html" %}

<h1>{{ site.title }}</h1>

<ul>
{{ for p in pages }}
  <li>
    <a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a>
    {{ if p.publish_date }}
      <small>{{ p.publish_date | date.to_string "%Y-%m-%d" }}</small>
    {{ end }}
  </li>
{{ end }}
</ul>
""";

    private const string ListTemplate = """
{% layout "layouts/base.html" %}

<ul>
{{ for p in pages }}
  <li>
    <a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a>
    {{ if p.publish_date }}
      <small>{{ p.publish_date | date.to_string "%Y-%m-%d" }}</small>
    {{ end }}
  </li>
{{ end }}
</ul>
""";
}
