using System.Text;

namespace Bukit.Cli.Commands;

internal static class StarterThemeResources
{
    internal const string StyleCss = """
:root {
  color-scheme: light;
  --bg: #fbfaf8;
  --surface: #ffffff;
  --surface-muted: #f3f1ed;
  --text: #202124;
  --muted: #66615b;
  --border: #ded9d0;
  --primary: #0b5fff;
  --primary-strong: #0846b8;
  --accent: #0f7b6c;
  --shadow: 0 16px 40px rgba(32, 33, 36, 0.08);
  --radius: 8px;
  --content: 760px;
  --wide: 1080px;
}

* {
  box-sizing: border-box;
}

html {
  background: var(--bg);
}

body {
  margin: 0;
  font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, "Noto Sans", sans-serif;
  color: var(--text);
  background: linear-gradient(180deg, #fff 0, var(--bg) 360px);
  line-height: 1.65;
}

a {
  color: var(--primary);
  text-decoration: none;
}

a:hover {
  color: var(--primary-strong);
  text-decoration: underline;
}

img {
  max-width: 100%;
  height: auto;
}

.site-header {
  border-bottom: 1px solid var(--border);
  background: rgba(255, 255, 255, 0.86);
}

.nav {
  max-width: var(--wide);
  margin: 0 auto;
  padding: 18px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
}

.brand {
  color: var(--text);
  font-weight: 750;
  letter-spacing: 0;
}

.nav-links {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 14px;
}

.nav-links a {
  color: var(--muted);
  font-size: 0.95rem;
}

.container {
  max-width: var(--wide);
  margin: 0 auto;
  padding: 42px 24px 64px;
}

.hero {
  max-width: 860px;
  padding: 28px 0 34px;
}

.eyebrow {
  margin: 0 0 10px;
  color: var(--accent);
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.hero h1,
.page-header h1,
.article-header h1 {
  margin: 0;
  color: var(--text);
  font-size: clamp(2rem, 5vw, 4.2rem);
  line-height: 1.05;
  letter-spacing: 0;
}

.hero p,
.page-header p,
.article-summary {
  max-width: 720px;
  color: var(--muted);
  font-size: 1.08rem;
}

.section-heading {
  margin: 34px 0 16px;
  font-size: 0.88rem;
  font-weight: 750;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
}

.card-list {
  display: grid;
  gap: 14px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.card {
  display: block;
  padding: 20px;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  box-shadow: var(--shadow);
}

.card-title {
  margin: 0 0 6px;
  font-size: 1.18rem;
  line-height: 1.3;
}

.card-title a {
  color: var(--text);
}

.meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin: 0 0 10px;
  color: var(--muted);
  font-size: 0.9rem;
}

.summary {
  margin: 0;
  color: var(--muted);
}

.article {
  max-width: var(--content);
  margin: 0 auto;
}

.article-header,
.page-header {
  margin-bottom: 30px;
}

.content {
  font-size: 1.02rem;
}

.content h1,
.content h2,
.content h3 {
  margin-top: 1.7em;
  line-height: 1.2;
}

.content p,
.content ul,
.content ol {
  margin: 1em 0;
}

.content pre,
pre {
  overflow-x: auto;
  padding: 16px;
  border-radius: var(--radius);
  background: #1f2937;
  color: #f8fafc;
  font-size: 0.92rem;
}

pre code,
code {
  font-family: "SFMono-Regular", Consolas, "Liberation Mono", monospace;
}

pre code {
  display: block;
  padding: 0;
  background: transparent;
  color: inherit;
  white-space: pre;
  tab-size: 2;
}

pre code[class*="language-"] {
  position: relative;
}

pre code[class*="language-"]::before {
  content: "code";
  float: right;
  margin-left: 16px;
  color: #94a3b8;
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

pre code.language-csharp::before { content: "csharp"; }
pre code.language-cs::before { content: "csharp"; }
pre code.language-javascript::before { content: "javascript"; }
pre code.language-js::before { content: "javascript"; }
pre code.language-typescript::before { content: "typescript"; }
pre code.language-ts::before { content: "typescript"; }
pre code.language-css::before { content: "css"; }
pre code.language-html::before { content: "html"; }
pre code.language-bash::before { content: "bash"; }
pre code.language-sh::before { content: "shell"; }
pre code.language-json::before { content: "json"; }
pre code.language-yaml::before { content: "yaml"; }
pre code.language-yml::before { content: "yaml"; }

.token.comment,
.token.prolog,
.token.doctype,
.token.cdata,
.hljs-comment,
.hljs-quote {
  color: #94a3b8;
}

.token.keyword,
.token.operator,
.token.tag,
.hljs-keyword,
.hljs-selector-tag,
.hljs-subst {
  color: #93c5fd;
}

.token.string,
.token.attr-value,
.hljs-string,
.hljs-doctag {
  color: #86efac;
}

.token.number,
.token.boolean,
.token.constant,
.hljs-number,
.hljs-literal {
  color: #fbbf24;
}

.token.function,
.token.class-name,
.hljs-title,
.hljs-section {
  color: #c4b5fd;
}

.token.property,
.token.attr-name,
.token.variable,
.hljs-attr,
.hljs-variable,
.hljs-template-variable {
  color: #f9a8d4;
}

.token.punctuation,
.hljs-punctuation {
  color: #cbd5e1;
}

:not(pre) > code {
  padding: 0.12em 0.35em;
  border-radius: 4px;
  background: var(--surface-muted);
}

blockquote {
  margin: 1.2em 0;
  padding: 0.1em 0 0.1em 18px;
  border-left: 4px solid var(--primary);
  color: var(--muted);
}

table {
  width: 100%;
  border-collapse: collapse;
  margin: 18px 0;
  background: var(--surface);
}

th,
td {
  padding: 10px 12px;
  border: 1px solid var(--border);
  text-align: left;
}

th {
  background: var(--surface-muted);
}

figure {
  margin: 20px 0;
}

figcaption {
  margin-top: 8px;
  color: var(--muted);
  font-size: 0.9rem;
  text-align: center;
}

.callout {
  display: flex;
  gap: 12px;
  margin: 16px 0;
  padding: 16px;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface-muted);
}

.callout-icon {
  flex: 0 0 auto;
  font-size: 1.25rem;
}

.callout-content {
  min-width: 0;
}

.to-do {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 4px 0;
}

.to-do input[type="checkbox"] {
  margin-top: 6px;
}

a.bookmark {
  display: block;
  margin: 12px 0;
  padding: 14px 16px;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  color: inherit;
}

.video-embed {
  position: relative;
  height: 0;
  margin: 18px 0;
  overflow: hidden;
  padding-bottom: 56.25%;
}

.video-embed iframe {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  border: 0;
}

.math-block {
  overflow-x: auto;
  padding: 16px 0;
  text-align: center;
}

.notion-gray { color: #787774; }
.notion-brown { color: #64473a; }
.notion-orange { color: #d9730d; }
.notion-yellow { color: #b38700; }
.notion-green { color: #0f7b6c; }
.notion-blue { color: #0b6e99; }
.notion-purple { color: #6940a5; }
.notion-pink { color: #ad1a72; }
.notion-red { color: #d92d20; }
.notion-gray_background { background-color: #f1f1ef; }
.notion-brown_background { background-color: #f4eeee; }
.notion-orange_background { background-color: #fbecdd; }
.notion-yellow_background { background-color: #fbf3db; }
.notion-green_background { background-color: #edf3ec; }
.notion-blue_background { background-color: #e7f3f8; }
.notion-purple_background { background-color: #f6f3f9; }
.notion-pink_background { background-color: #f9f0f5; }
.notion-red_background { background-color: #fdebec; }

.notion-columns {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 18px;
  margin: 16px 0;
}

.notion-column,
.callout-children,
.to-do-children {
  min-width: 0;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-top: 28px;
  padding-top: 18px;
  border-top: 1px solid var(--border);
}

.search-form {
  display: flex;
  gap: 10px;
  margin: 24px 0;
}

.search-form input {
  flex: 1;
  min-width: 0;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
}

button,
.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 42px;
  padding: 0 16px;
  border: 1px solid var(--primary);
  border-radius: var(--radius);
  background: var(--primary);
  color: #fff;
  font: inherit;
  font-weight: 700;
  cursor: pointer;
}

button:hover,
.button:hover {
  background: var(--primary-strong);
  color: #fff;
  text-decoration: none;
}

.term-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.term-card {
  padding: 16px;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
}

.site-footer {
  border-top: 1px solid var(--border);
  color: var(--muted);
  background: var(--surface);
}

.footer-inner {
  max-width: var(--wide);
  margin: 0 auto;
  padding: 24px;
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 12px;
}

@media (max-width: 680px) {
  .nav,
  .footer-inner,
  .pagination,
  .search-form {
    align-items: stretch;
    flex-direction: column;
  }

  .nav-links {
    justify-content: flex-start;
  }

  .container {
    padding: 30px 18px 48px;
  }

  .card {
    padding: 16px;
  }
}
""";

    internal const string BaseLayout = """
<!DOCTYPE html>
<html lang="{{ site.language }}">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{ if page.seo }}{{ page.seo.title }}{{ else }}{{ page.title }}{{ end }}</title>
  <link rel="alternate" type="application/rss+xml" href="{{ site.base_url }}/rss.xml" />
  <link rel="sitemap" type="application/xml" href="{{ site.base_url }}/sitemap.xml" />
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

    internal const string SeoPartial = """
{{ if page.seo }}
  <link rel="canonical" href="{{ page.seo.canonical | html.escape }}" />
  {{ if page.seo.description }}
    <meta name="description" content="{{ page.seo.description | html.escape }}" />
  {{ end }}
  {{ if page.seo.robots }}
    <meta name="robots" content="{{ page.seo.robots | html.escape }}" />
  {{ end }}

  <meta property="og:title" content="{{ page.seo.og.title | html.escape }}" />
  {{ if page.seo.og.description }}
    <meta property="og:description" content="{{ page.seo.og.description | html.escape }}" />
  {{ end }}
  <meta property="og:url" content="{{ page.seo.og.url | html.escape }}" />
  <meta property="og:type" content="{{ page.seo.og.type | html.escape }}" />
  {{ if page.seo.og.image }}
    <meta property="og:image" content="{{ page.seo.og.image | html.escape }}" />
  {{ end }}
  {{ if page.seo.og.site_name }}
    <meta property="og:site_name" content="{{ page.seo.og.site_name | html.escape }}" />
  {{ end }}
  {{ if page.seo.og.locale }}
    <meta property="og:locale" content="{{ page.seo.og.locale | html.escape }}" />
  {{ end }}

  {{ if page.seo.article.published_time }}
    <meta property="article:published_time" content="{{ page.seo.article.published_time | html.escape }}" />
  {{ end }}
  {{ if page.seo.article.modified_time }}
    <meta property="article:modified_time" content="{{ page.seo.article.modified_time | html.escape }}" />
  {{ end }}
  {{ if page.seo.article.author }}
    <meta property="article:author" content="{{ page.seo.article.author | html.escape }}" />
  {{ end }}
  {{ for tag in page.seo.article.tags }}
    <meta property="article:tag" content="{{ tag | html.escape }}" />
  {{ end }}

  <meta name="twitter:card" content="{{ page.seo.twitter.card | html.escape }}" />
  <meta name="twitter:title" content="{{ page.seo.twitter.title | html.escape }}" />
  {{ if page.seo.twitter.description }}
    <meta name="twitter:description" content="{{ page.seo.twitter.description | html.escape }}" />
  {{ end }}
  {{ if page.seo.twitter.image }}
    <meta name="twitter:image" content="{{ page.seo.twitter.image | html.escape }}" />
  {{ end }}
  {{ if page.seo.twitter.site }}
    <meta name="twitter:site" content="{{ page.seo.twitter.site | html.escape }}" />
  {{ end }}
  {{ if page.seo.twitter.creator }}
    <meta name="twitter:creator" content="{{ page.seo.twitter.creator | html.escape }}" />
  {{ end }}

  {{ for alternate in page.seo.alternates }}
    <link rel="alternate" hreflang="{{ alternate.hreflang | html.escape }}" href="{{ alternate.href | html.escape }}" />
  {{ end }}

  {{ for json in page.seo.json_ld }}
    <script type="application/ld+json">{{ json }}</script>
  {{ end }}
{{ end }}
""";

    internal const string AnalyticsPartial = """
{{ if site.analytics && site.analytics.enabled && site.analytics.google_analytics_id }}
  <script async src="https://www.googletagmanager.com/gtag/js?id={{ site.analytics.google_analytics_id | html.escape }}"></script>
  <script>
    window.dataLayer = window.dataLayer || [];
    function gtag(){dataLayer.push(arguments);}
    gtag('js', new Date());
    gtag('config', '{{ site.analytics.google_analytics_id | html.escape }}');
  </script>
{{ end }}
""";

    internal const string HeaderPartial = """
<header class="site-header">
  <nav class="nav" aria-label="Primary navigation">
    <a class="brand" href="{{ site.base_url }}/">
      {{-- bukit:brand --}}
      {{ if site.params && site.params.brand }}
        {{ site.params.brand }}
      {{ else }}
        {{ site.title }}
      {{ end }}
    </a>
    <div class="nav-links">
      {{ if site.modules && site.modules.navigation }}
        {{ for item in site.modules.navigation }}
          {{ nav_url = "/" }}
          {{ if item.fields && item.fields.link }}
            {{ nav_url = item.fields.link.value }}
          {{ end }}
          <a href="{{ nav_url }}">{{ item.title }}</a>
        {{ end }}
      {{ else }}
        <a href="{{ site.base_url }}/">Home</a>
        <a href="{{ site.base_url }}/blog/">Blog</a>
        <a href="{{ site.base_url }}/pages/">Pages</a>
      {{ end }}
    </div>
  </nav>
</header>
""";

    internal const string FooterPartial = """
<footer class="site-footer">
  <div class="footer-inner">
    <span>
      {{-- bukit:brand --}}
      {{ if site.params && site.params.footer_text }}
        {{ site.params.footer_text }}
      {{ else }}
        {{ site.title }}
      {{ end }}
    </span>
    <small>Powered by <a href="https://github.com/ALi365-SDN-BHD/Bukit" target="_blank" rel="noopener">bukit</a></small>
  </div>
</footer>
""";

    internal const string ListCardPartial = """
<li class="card">
  <h2 class="card-title">
    <a href="{{ site.base_url }}{{ item.url }}">{{ item.title }}</a>
  </h2>
  {{ if item.publish_date }}
    <p class="meta"><time datetime="{{ item.publish_date | date.to_string "%Y-%m-%d" }}">{{ item.publish_date | date.to_string "%Y-%m-%d" }}</time></p>
  {{ end }}
  {{ if item.summary }}
    <p class="summary">{{ item.summary }}</p>
  {{ end }}
</li>
""";

    internal const string PaginationNavPartial = """
{{ page_num = 1 }}
{{ page_total = 1 }}
{{ prev_url = "" }}
{{ next_url = "" }}
{{ if pagination }}
  {{ page_num = pagination.page }}
  {{ page_total = pagination.total_pages }}
  {{ if pagination.has_prev }}
    {{ if page_num == 2 }}
      {{ prev_url = base_url }}
    {{ else }}
      {{ prev_url = base_url + "page/" + (page_num - 1) + "/" }}
    {{ end }}
  {{ end }}
  {{ if pagination.has_next }}
    {{ next_url = base_url + "page/" + (page_num + 1) + "/" }}
  {{ end }}
{{ end }}
{{ if page_total > 1 }}
<nav class="pagination" aria-label="Pagination">
  <span>{{ if prev_url != "" }}<a href="{{ prev_url }}">Previous</a>{{ end }}</span>
  <span>Page {{ page_num }} of {{ page_total }}</span>
  <span>{{ if next_url != "" }}<a href="{{ next_url }}">Next</a>{{ end }}</span>
</nav>
{{ end }}
""";

    internal const string PageTemplate = """
{% layout "layouts/base.html" %}

<article class="article">
  <header class="article-header">
    <p class="eyebrow">Page</p>
    <h1>{{ page.title }}</h1>
    {{ if page.summary }}<p class="article-summary">{{ page.summary }}</p>{{ end }}
  </header>
  <div class="content">
    {{ page.content }}
  </div>
</article>
""";

    internal const string PostTemplate = """
{% layout "layouts/base.html" %}

<article class="article">
  <header class="article-header">
    <p class="eyebrow">Article</p>
    <h1>{{ page.title }}</h1>
    {{ if page.publish_date }}
      <p class="meta"><time datetime="{{ page.publish_date | date.to_string "%Y-%m-%d" }}">{{ page.publish_date | date.to_string "%Y-%m-%d" }}</time></p>
    {{ end }}
    {{ if page.summary }}<p class="article-summary">{{ page.summary }}</p>{{ end }}
  </header>
  <div class="content">
    {{ page.content }}
  </div>
</article>
""";

    internal const string IndexTemplate = """
{% layout "layouts/base.html" %}

<section class="hero">
  <p class="eyebrow">Starter theme</p>
  <h1>{{ site.title }}</h1>
  {{ if site.description }}
    <p>{{ site.description }}</p>
  {{ else }}
    <p>A clean content-first starting point for Markdown, Notion, blog, and documentation sites.</p>
  {{ end }}
</section>

{{ if site.modules && site.modules.features }}
  <section>
    <h2 class="section-heading">Featured</h2>
    <ul class="card-list">
      {{ for feature in site.modules.features }}
        <li class="card">
          <h2 class="card-title">{{ feature.title }}</h2>
          {{ if feature.fields && feature.fields.desc }}<p class="summary">{{ feature.fields.desc.value }}</p>{{ end }}
        </li>
      {{ end }}
    </ul>
  </section>
{{ end }}

<section>
  <h2 class="section-heading">Latest content</h2>
  <ul class="card-list">
  {{ for p in pages }}
    {{ item = p }}
    {{ include "partials/list-card.html" }}
  {{ end }}
  </ul>
</section>
""";

    internal const string ListTemplate = """
{% layout "layouts/base.html" %}

<header class="page-header">
  <p class="eyebrow">Collection</p>
  <h1>{{ page.title }}</h1>
  {{ if page.summary }}<p>{{ page.summary }}</p>{{ end }}
</header>

<ul class="card-list">
{{ for p in pages }}
  {{ item = p }}
  {{ include "partials/list-card.html" }}
{{ end }}
</ul>
""";

    internal const string PaginationTemplate = """
{% layout "layouts/base.html" %}

{{ items = page.fields.items.value }}
{{ pagination = page.fields.pagination.value }}
{{ base_url = site.base_url + "/blog/" }}

<header class="page-header">
  <p class="eyebrow">Archive</p>
  <h1>{{ page.title }}</h1>
</header>

<ul class="card-list">
{{ for item in items }}
  {{ include "partials/list-card.html" }}
{{ end }}
</ul>

{{ include "partials/pagination-nav.html" }}
""";

    internal const string TaxonomyIndexTemplate = """
{% layout "layouts/base.html" %}

{{ terms = page.fields.terms.value }}

<header class="page-header">
  <p class="eyebrow">Browse</p>
  <h1>{{ page.title }}</h1>
</header>

<ul class="term-grid">
{{ for term in terms }}
  <li class="term-card">
    <a href="{{ site.base_url }}/{{ term.kind }}/{{ term.slug }}/">{{ term.title }}</a>
    <span class="meta">{{ term.count }} items</span>
  </li>
{{ end }}
</ul>
""";

    internal const string TaxonomyTermTemplate = """
{% layout "layouts/base.html" %}

{{ items = page.fields.items.value }}
{{ taxonomy = page.fields.taxonomy.value }}
{{ pagination = page.fields.pagination.value }}
{{ base_url = site.base_url + "/" + taxonomy.kind + "/" + taxonomy.slug + "/" }}

<header class="page-header">
  <p class="eyebrow">{{ taxonomy.kind }}</p>
  <h1>{{ page.title }}</h1>
</header>

<ul class="card-list">
{{ for item in items }}
  {{ include "partials/list-card.html" }}
{{ end }}
</ul>

{{ include "partials/pagination-nav.html" }}
""";

    internal const string SearchTemplate = """
{% layout "layouts/base.html" %}

<header class="page-header">
  <p class="eyebrow">Search</p>
  <h1>{{ page.title }}</h1>
  <p>Search across generated titles, summaries, and snippets.</p>
</header>

<div class="search-form">
  <input id="search-query" type="search" placeholder="Search content" />
  <button id="search-button" type="button">Search</button>
</div>
<ul id="search-results" class="card-list"></ul>

<script>
const queryInput = document.getElementById('search-query');
const searchButton = document.getElementById('search-button');
const resultsElement = document.getElementById('search-results');

function appendResult(item) {
  const li = document.createElement('li');
  li.className = 'card';
  const title = document.createElement('h2');
  title.className = 'card-title';
  const link = document.createElement('a');
  link.href = item.url || '#';
  link.textContent = item.title || 'Untitled';
  title.appendChild(link);
  li.appendChild(title);
  const summary = item.snippet || item.summary;
  if (summary) {
    const p = document.createElement('p');
    p.className = 'summary';
    p.textContent = summary;
    li.appendChild(p);
  }
  resultsElement.appendChild(li);
}

async function runSearch() {
  const keyword = (queryInput.value || '').trim().toLowerCase();
  const response = await fetch('{{ site.base_url }}/search.json');
  const items = await response.json();
  resultsElement.innerHTML = '';
  items
    .filter(item => keyword === '' ||
      (item.title || '').toLowerCase().includes(keyword) ||
      (item.summary || '').toLowerCase().includes(keyword) ||
      (item.snippet || '').toLowerCase().includes(keyword))
    .forEach(appendResult);
}

searchButton.addEventListener('click', runSearch);
queryInput.addEventListener('keydown', event => {
  if (event.key === 'Enter') runSearch();
});
</script>
""";

    internal const string TemplateCapabilities = """
templates:
  pages/index.html:
    capabilities:
      needs_page_content: false
      supports_pagination: false
      supports_taxonomy: false
      supports_search_snippets: false
  pages/list.html:
    capabilities:
      needs_page_content: false
      supports_pagination: false
      supports_taxonomy: false
      supports_search_snippets: false
  pages/pagination.html:
    capabilities:
      supports_pagination: true
  pages/taxonomy-index.html:
    capabilities:
      supports_taxonomy: true
  pages/taxonomy-term.html:
    capabilities:
      supports_taxonomy: true
  pages/search.html:
    capabilities:
      supports_search_snippets: true
""";

    internal const string ThemeYaml = """
name: starter
version: 1.0.0
description: Default starter theme for bukit
author: Bukit
license: MIT
tags: [starter, minimal, blog]
templates:
  home:
    template: pages/index.html
    required: true
  page:
    template: pages/page.html
    accepts:
      collection: page
  post:
    template: pages/post.html
    accepts:
      collection: post
  detail:
    template: pages/page.html
    accepts:
      kind: detail
  list:
    template: pages/list.html
    accepts:
      kind: list
  pagination:
    template: pages/pagination.html
    accepts:
      kind: pagination
  archive:
    template: pages/page.html
    accepts:
      kind: archive
  taxonomy_index:
    template: pages/taxonomy-index.html
    accepts:
      kind: taxonomy_index
  taxonomy_term:
    template: pages/taxonomy-term.html
    accepts:
      kind: taxonomy_term
params:
  - key: brand
    label: Site Brand
    type: string
    default: My Site
  - key: primary_color
    label: Primary Color
    type: color
    default: "#0b5fff"
  - key: accent_color
    label: Accent Color
    type: color
    default: "#0f7b6c"
  - key: footer_text
    label: Footer Text
    type: string
    default: My Site
""";
}
