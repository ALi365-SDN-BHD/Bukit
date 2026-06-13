namespace Bukit.Labs.Cli.Commands;

internal static class TemplateSnippets
{
    public static readonly Dictionary<string, string> ScribanSnippets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["post-card"] = """
<article class="post-card">
  <h2><a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a></h2>
  {{ if p.summary }}<p>{{ p.summary }}</p>{{ end }}
  {{ if p.publish_date }}
    <time>{{ p.publish_date | date.to_string "%Y-%m-%d" }}</time>
  {{ end }}
</article>
""",
        ["tag-cloud"] = """
{{ if site.data.tags }}
<div class="tag-cloud">
  {{ for tag in site.data.tags }}
    <a href="{{ site.base_url }}/tags/{{ tag.name }}/" class="tag-item">{{ tag.name }}</a>
  {{ end }}
</div>
{{ end }}
""",
        ["toc"] = """
<div class="toc">
  <h3>目录</h3>
  <ul>
  {{ for h in page.fields.headings.value }}
    <li class="toc-level-{{ h.level }}"><a href="#{{ h.id }}">{{ h.text }}</a></li>
  {{ end }}
  </ul>
</div>
""",
        ["share-buttons"] = """
<div class="share-buttons">
  <span>分享：</span>
  <a href="https://twitter.com/intent/tweet?url={{ site.url }}{{ page.url }}&text={{ page.title }}" target="_blank" rel="noopener">Twitter</a>
  <a href="https://www.facebook.com/sharer/sharer.php?u={{ site.url }}{{ page.url }}" target="_blank" rel="noopener">Facebook</a>
</div>
""",
        ["comments-placeholder"] = """
<section class="comments" aria-label="评论">
  <h3>评论</h3>
  {{ if site.params.comments_provider }}
    <p>评论系统加载中...</p>
  {{ else }}
    <p>评论功能暂未开启。</p>
  {{ end }}
</section>
""",
        ["breadcrumb"] = """
<nav class="nav-breadcrumb" aria-label="面包屑导航">
  <a href="{{ site.base_url }}/">Home</a>
  {{ if page.url contains '/blog/' }}
    <span>/</span> <a href="{{ site.base_url }}/blog/">Blog</a>
  {{ end }}
  <span>/</span> <span>{{ page.title }}</span>
</nav>
""",
        ["related-posts"] = """
{{ if site.data.related_posts }}
<aside class="related-posts">
  <h3>相关文章</h3>
  <ul>
  {{ for rp in site.data.related_posts }}
    <li><a href="{{ site.base_url }}{{ rp.url }}">{{ rp.title }}</a></li>
  {{ end }}
  </ul>
</aside>
{{ end }}
""",
        ["author-bio"] = """
{{ if page.fields.author.value }}
<section class="author-bio">
  <h3>关于作者</h3>
  <p>{{ page.fields.author_bio.value }}</p>
</section>
{{ end }}
""",
    };

    public static readonly Dictionary<string, string> CssSnippets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["post-card"] = """
.post-card { padding: 1.5rem; border: 1px solid var(--border); border-radius: var(--radius); margin-bottom: 1rem; transition: box-shadow 0.2s; }
.post-card:hover { box-shadow: var(--card-shadow); }
.post-card h2 { margin: 0 0 0.5rem; font-size: 1.25rem; }
.post-card h2 a { color: var(--text); text-decoration: none; }
.post-card h2 a:hover { color: var(--primary); }
.post-card time { color: var(--muted); font-size: 0.875rem; }
.post-card p { color: var(--muted); margin: 0.5rem 0 0; }
""",
        ["tag-cloud"] = """
.tag-cloud { display: flex; flex-wrap: wrap; gap: 0.5rem; padding: 1rem 0; }
.tag-item { display: inline-block; padding: 0.25rem 0.75rem; background: var(--surface-muted); border-radius: 999px; font-size: 0.875rem; color: var(--text); text-decoration: none; transition: background 0.2s, color 0.2s; }
.tag-item:hover { background: var(--primary); color: #fff; }
""",
        ["toc"] = """
.toc { background: var(--surface-muted); padding: 1rem 1.5rem; border-radius: var(--radius); margin-bottom: 2rem; }
.toc h3 { margin: 0 0 0.75rem; font-size: 1rem; }
.toc ul { list-style: none; padding: 0; margin: 0; }
.toc li { margin-bottom: 0.25rem; }
.toc li a { color: var(--muted); text-decoration: none; font-size: 0.9rem; }
.toc li a:hover { color: var(--primary); }
.toc-level-2 { padding-left: 0; }
.toc-level-3 { padding-left: 1rem; }
""",
        ["btn"] = """
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.625rem 1.5rem; border: none; border-radius: var(--radius); font-size: 0.9375rem; font-weight: 500; cursor: pointer; text-decoration: none; transition: background 0.2s, color 0.2s, box-shadow 0.2s; }
.btn-primary { background: var(--primary); color: #fff; }
.btn-primary:hover { background: var(--primary-strong); box-shadow: 0 4px 12px rgba(11, 95, 255, 0.3); }
.btn-outline { background: transparent; color: var(--primary); border: 1.5px solid var(--primary); }
.btn-outline:hover { background: var(--primary); color: #fff; }
.btn-sm { padding: 0.375rem 1rem; font-size: 0.8125rem; }
.btn-lg { padding: 0.875rem 2rem; font-size: 1.0625rem; }
""",
        ["nav-breadcrumb"] = """
.nav-breadcrumb { padding: 0.5rem 0; font-size: 0.875rem; color: var(--muted); }
.nav-breadcrumb a { color: var(--muted); text-decoration: none; }
.nav-breadcrumb a:hover { color: var(--primary); }
.nav-breadcrumb span { margin: 0 0.25rem; }
""",
        ["share-buttons"] = """
.share-buttons { display: flex; align-items: center; gap: 0.5rem; padding: 1rem 0; border-top: 1px solid var(--border); margin-top: 2rem; font-size: 0.875rem; }
.share-buttons span { color: var(--muted); }
.share-buttons a { color: var(--primary); text-decoration: none; padding: 0.25rem 0.75rem; border: 1px solid var(--border); border-radius: var(--radius); }
.share-buttons a:hover { background: var(--surface-muted); }
""",
        ["callout"] = """
.callout { padding: 1rem 1.25rem; border-radius: var(--radius); margin: 1.5rem 0; border-left: 4px solid; }
.callout-info { background: #e8f0fe; border-color: var(--primary); color: #174ea6; }
.callout-warning { background: #fef7e0; border-color: #f9ab00; color: #6a4a00; }
.callout-danger { background: #fce8e6; border-color: #ea4335; color: #8b1a15; }
.callout-success { background: #e6f4ea; border-color: #34a853; color: #0d652d; }
""",
        ["responsive-table"] = """
.responsive-table { width: 100%; overflow-x: auto; }
.responsive-table table { width: 100%; border-collapse: collapse; font-size: 0.9375rem; }
.responsive-table th, .responsive-table td { padding: 0.625rem 0.875rem; text-align: left; border-bottom: 1px solid var(--border); }
.responsive-table th { font-weight: 600; background: var(--surface-muted); }
.responsive-table tr:hover td { background: var(--surface-muted); }
""",
        ["code-block"] = """
.code-block { background: #1e1e1e; color: #d4d4d4; padding: 1.25rem; border-radius: var(--radius); overflow-x: auto; font-size: 0.875rem; line-height: 1.6; margin: 1.5rem 0; }
.code-block code { font-family: var(--code-font, "SFMono-Regular", Consolas, "Liberation Mono", monospace); }
.code-inline { background: var(--surface-muted); padding: 0.15rem 0.4rem; border-radius: 4px; font-size: 0.875em; font-family: var(--code-font, "SFMono-Regular", Consolas, monospace); }
""",
    };
}
