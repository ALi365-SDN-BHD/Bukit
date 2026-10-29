using Bukit.Config;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine.Tests.Infrastructure;

internal sealed class SiteBuilder : IDisposable
{
    public static readonly DateTimeOffset BuildTime = new(2024, 7, 1, 0, 0, 0, TimeSpan.Zero);
    public SiteSandbox Sandbox { get; } = new();
    public string Root => Sandbox.Root;
    public string Output => Sandbox.PathFor("dist");
    public string Cache => Sandbox.PathFor(".cache");
    public AppConfig Config => ConfigLoader.Load(Sandbox.PathFor("site.yaml"));

    public SiteBuilder(string? fixture = null)
    {
        if (fixture is null)
        {
            Sandbox.Write("site.yaml", """
                site:
                  name: phase0
                  title: Phase 0
                  language: en
                  collections:
                    page:
                      permalink: /{slug}/
                      template: pages/page.html
                content:
                  sources:
                    - type: markdown
                      name: pages
                      collection: page
                      markdown:
                        dir: content
                build:
                  output: dist
                theme:
                  layouts: layouts
                """);
            Directory.CreateDirectory(Sandbox.PathFor("content"));
            Sandbox.Write("layouts/pages/index.html", "<html lang=\"{{ site.language }}\"><head><title>{{ site.title }}</title></head><body>{{ site.title }}</body></html>");
            Sandbox.Write("layouts/pages/page.html", "<html lang=\"{{ site.language }}\"><head><title>{{ page.title }}</title></head><body>{{ page.title }}{{ page.content }}</body></html>");
        }
        else
        {
            string[] files = fixture switch
            {
                "basic-markdown-site" => ["content/index.md"],
                "i18n-site" => ["content/en/index.md", "content/zh/index.md"],
                "taxonomy-site" => ["content/index.md", "content/another-post.md", "layouts/theme.yaml",
                    "layouts/pages/taxonomy-index.html", "layouts/pages/taxonomy-term.html"],
                _ => throw new ArgumentException("Unknown phase 0 fixture.", nameof(fixture))
            };
            foreach (var relative in files.Concat(new[] { "site.yaml", "layouts/layouts/base.html", "layouts/pages/index.html", "layouts/pages/page.html" }))
            {
                var source = Path.Combine(SiteSandbox.RepositoryRoot, "tests/fixtures", fixture, relative);
                Sandbox.Write(relative, File.ReadAllText(source));
            }
            foreach (var markdown in files.Where(path => path.EndsWith(".md", StringComparison.Ordinal)))
            {
                var text = File.ReadAllText(Sandbox.PathFor(markdown));
                var date = markdown.EndsWith("another-post.md", StringComparison.Ordinal) ? "2024-06-02" : "2024-06-01";
                Sandbox.Write(markdown, text.Insert(text.IndexOf('\n') + 1, $"publishAt: {date}T00:00:00Z\n"));
            }
            if (fixture == "taxonomy-site")
            {
                Replace("site.yaml", "taxonomy:", "taxonomy:\n  pageSize: 1");
                Replace("content/another-post.md", "  - news", "  - news\n  - tech");
                Sandbox.Write("layouts/pages/taxonomy-index.html", """
                    {% layout "layouts/base.html" %}
                    {{ for term in page.fields.terms.value }}<a href="{{ term.url }}">[{{ term.title }}:{{ term.count }}]</a>{{ end }}
                    """);
                Sandbox.Write("layouts/pages/taxonomy-term.html", """
                    {% layout "layouts/base.html" %}
                    category={{ taxonomy.slug }} page={{ pagination.page }} total={{ pagination.total_pages }}
                    {{ for item in pages }}[{{ item.title }}]{{ end }}
                    prev={{ pagination.prev_url }} next={{ pagination.next_url }}
                    """);
            }
        }
        Replace("site.yaml", "site:", "site:\n  url: https://example.test");
        Replace("site.yaml", "content:", "content:\n  media:\n    downloadToLocal: false");
        Replace("site.yaml", "build:", "build:\n  report:\n    enabled: true\n  languageJobs: 1");
    }

    private void Replace(string relative, string oldValue, string newValue)
        => Sandbox.Write(relative, File.ReadAllText(Sandbox.PathFor(relative)).Replace(oldValue, newValue, StringComparison.Ordinal));

    public void Markdown(string slug, string title, string body)
        => Sandbox.Write($"content/{slug}.md", $"---\nslug: {slug}\ntitle: {title}\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\n---\n{body}\n");

    public Task<BuildResult> BuildAsync(bool clean = true, bool incremental = false, int jobs = 1,
        Func<string, ITemplateRenderer>? rendererFactory = null)
    {
        var config = Config;
        if (Path.GetFullPath(config.Build.Output, Root) != Output || config.Content.Sources is not { Count: > 0 } sources ||
            sources.Any(source => source.Type != "markdown" || Path.GetFullPath(source.Markdown?.Dir ?? "content", Root) != Sandbox.PathFor("content")) ||
            config.Content.Media.DownloadToLocal || config.Content.Media.DownloadDir != "assets/uploads")
            throw new InvalidOperationException("Phase 0 build must keep output/cache and content in its sandbox.");
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error), new DefaultContentProviderFactory(),
            new DefaultSearchIndexBuilder(), rendererFactory, new FixedClock());
        return engine.BuildAsync(config, Root, new ConfigOverrides { Clean = clean, Incremental = incremental, Jobs = jobs });
    }

    public void Dispose() => Sandbox.Dispose();

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => BuildTime;
    }
}
