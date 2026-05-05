# 10 Built-in Features & Output: sitemap/rss/search, Tags, Categories & Derived Pages

Beyond generating page HTML, Bukit also generates a set of "site-level artifacts" based on content and configuration, used for SEO, subscriptions, search, and content aggregation.

This page focuses on "what users can control, what files will be generated"; for more detailed plugin contracts and boundaries, see developer docs: [guide/dev/built-in-plugins](../dev/built-in-plugins.md).

## What You Will Get

- What additional files are generated and where
- How these files output in multilingual mode (split/merged/index)
- What "derived pages" like tags/categories/archives/pagination are
- FAQ: why links in sitemap are wrong, why search.json is empty

## Site-Level Artifact List (Common)

In the build output directory (`build.output`, default `dist/`) you will typically see:

- `sitemap.xml`
- `rss.xml`
- `search.json` (browser-facing search data)
- `search.index.json` (optional: aggregated index)
- `tags/`, `categories/` (derived list pages, specific to theme and derivation logic)

See runnable examples:

- `examples/starter/dist/`
- `examples/starter/.bukit_test/dist/` (complete output for testing)

## sitemap.xml: Search Engine Index Entry Point

### What You Can Configure

- `site.url`: Site absolute domain (basis for generating absolute links)
- `site.baseUrl`: Sub-path (common for GitHub Pages)
- `site.sitemapMode`: Multilingual output mode (see next section)

### Common Pitfalls

- `site.url` not set: sitemap may generate relative or incorrect absolute links
- baseUrl misconfigured: sitemap URLs carry wrong prefixes, search engine crawling fails

Deployment details: [13 Deploy GitHub Pages](./13-deploy-github-pages.md).

## rss.xml: Subscription Feed (Blog/Changelog)

RSS typically depends on:

- Site URL (`site.url`)
- Content title/publish date/type (especially post)

If you find RSS content incomplete, check first:

- Whether your content has `publishAt`
- Whether it has been excluded by draft/filter conditions (Notion Published, build.draft, etc.)

## search.json: In-Site Search Data

search.json is typically a list of "title/summary/URL for each page" for frontend JS to implement search.

You typically need:

- A search UI in the theme (reads search.json and filters)
- Or directly use the built-in `search.index.json` (depending on theme/implementation)

If search.json is empty:

- The site may have no content items (content read failed / filtered out)
- Or the theme/config has not enabled the corresponding output (depends on version and mode)

## Tags & Categories (tags / categories)

When your content contains `tags` or `categories`:

- The engine/plugin aggregates this information
- The theme generally renders tags/categories list pages and detail pages

Optional: Enable pinned sorting for content under a certain category/tag:

- Mark content with `pinned: true` (optional `pinOrder` number, smaller = earlier)
- Config items: `taxonomy.pinField` / `taxonomy.pinOrderField` (for multi data sources use `pinFieldBySource` / `pinOrderFieldBySource` for field name mapping)

Markdown examples (tags/categories): [05 Content Markdown](./05-markdown-content.md).

Notion examples: the simulated data table in [06 Content Notion](./06-notion-content.md).

## Derived Pages: What tags/categories/pagination/archives Are

Derived pages are not pages you directly author in your content source, but pages "derived" by the engine from content, for example:

- `/tags/<tag>/`: article list under a certain tag
- `/categories/<category>/`: article list under a certain category
- `/blog/page/2/`: paginated list page
- `/archive/2026/`: archive by year

What users need to care about:

- Whether derived pages are rendered depends on: whether the engine enables the corresponding derivation capability + whether the theme provides the corresponding templates
- Derived pages participate in sitemap/search (so baseUrl and url accuracy is even more important)

## pluginFailMode: Whether to Interrupt Build When Derivation/Output Fails

```yaml
site:
  pluginFailMode: strict  # strict (default) | warn
```

- `strict`: Plugin errors interrupt the build (suitable for production)
- `warn`: Log errors but continue output (suitable for migration/debugging)

## Multilingual Output Modes (sitemap/rss/search)

Under multilingual sites, these artifacts have three common modes (the same meaning applies to the same type of artifact):

- `split`: One per language (e.g., `zh-CN/sitemap.xml` and `en-US/sitemap.xml`)
- `merged`: Aggregated into one (typically outputs one at the root directory)
- `index`: Root directory outputs index file, pointing to each language's files

How to choose: [11 Multilingual & SEO](./11-i18n-seo.md).
