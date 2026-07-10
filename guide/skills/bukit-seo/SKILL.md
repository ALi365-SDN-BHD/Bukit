---
name: bukit-seo
description: Use for SEO config, SEO injection, reports, audit, and report diff behavior.
---

# Bukit SEO

SEO config lives under `site.seo`. `SeoPipeline` builds canonical, alternates,
Open Graph, Twitter, article metadata, JSON-LD, final document titles, and
reports.

Treat `page.seo.title` as the semantic title and
`page.seo.document_title` as the final HTML `<title>`. Configure the latter with
`homeTitleTemplate`, `pageTitleTemplate`, and `titleSeparator`. Templates accept
only case-insensitive `{pageTitle}`, `{siteTitle}`, and `{separator}`. The page
template must include `{pageTitle}`; the home template must include
`{pageTitle}` or `{siteTitle}`.

In `inject` mode Core replaces all head titles with one encoded document title.
In `theme` or `off` mode it exposes the SEO model and runs diagnostics without
modifying the HTML. Missing, empty, multiple, long, mismatched, and duplicated actual titles are surfaced as
`seo.document_title_*` issues. A missing output file produces only
`seo.output_file_missing`; a present output without a head also produces
`seo.html_head_missing` and `seo.document_title_missing`.

Use `bukit seo audit --dir dist` after builds and `bukit seo diff` for report
comparison.
