# init/create (Scaffold Initialization)

`bukit init <dir>` (synonym `create <dir>`) creates a minimal runnable site project.

Implementation: `src/Bukit.Cli/Commands/InitCommand.cs`

## Basic Usage

```bash
bukit init my-site
bukit create my-site
```

Parameters:
- `--provider <markdown|notion>` (default markdown)
- `--template <minimal|blog|docs|landing|portfolio>` (default minimal)

`minimal` keeps the default starter scaffold. The other templates reuse the
same preset system as `bukit theme wizard --preset ...`, so a new project can
start with a blog, docs, landing, or portfolio visual direction without running
a separate theme command.

Template content skeletons:
- `minimal`: `content/hello-world.md`, explicit `collection: page`
- `blog`: `content/posts/welcome.md` plus `content/pages/about.md`, data modules for the homepage, explicit `collection: post/page`, dated blog permalinks, pagination, RSS/archive output
- `docs`: `content/docs/getting-started.md` plus `content/docs/configuration.md`, data modules for the homepage, explicit `collection: doc`, `/docs/{slug}/` routes
- `landing`: `content/pages/overview.md` plus `content/pages/contact.md`, homepage feature/CTA modules, explicit `collection: page`, flat page routes
- `portfolio`: `content/work/sample-project.md` plus `content/pages/about.md`, data modules for the homepage, explicit `collection: work/page`, `/work/{slug}/` routes

Non-minimal Markdown templates use `content.sources[]`: content sources are
assigned to their collection, while `data/` is loaded with
`mode: data` and injected into `site.modules` for the first screen.

Generated `site.yaml` includes `site.url: https://example.com` as a safe
placeholder so the first build can produce absolute canonical, sitemap, RSS,
and schema URLs. Replace it with the production URL before publishing.
Generated config also sets `site.seo.defaultImage: /assets/og-default.gif`,
backed by a local starter asset, and the blog starter post includes an author
to avoid first-run BlogPosting schema warnings.

## Generated Structure

```text
<dir>/
  site.yaml
  .gitignore
  content/hello-world.md
  themes/starter/
    assets/style.css
    assets/og-default.gif
    static/
    layouts/
      layouts/base.html
      pages/index.html, list.html, page.html, post.html
      partials/header.html, footer.html
```

## Verification

```bash
cd my-site
bukit doctor
bukit build --clean
bukit preview --dir dist
```

## Known Limitations

- `--provider notion` writes Notion-oriented config, but local sample content
  is still Markdown-only reference material until the user connects a database
