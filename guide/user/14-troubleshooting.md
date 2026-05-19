# 14 Troubleshooting: Doctor First, Diagnose by Symptom

When you encounter a problem, do not guess first. Follow this order for diagnosis:

1. `doctor` (config/environment self-check)
2. `build --clean` (eliminate incremental cache effects)
3. Compare against `examples/starter/` (find a "working baseline")

Developer-oriented troubleshooting docs: [guide/dev/doctor](../dev/doctor.zh-CN.md), [guide/dev/cache-clean](../dev/cache-clean.zh-CN.md).

## Quick Command Reference

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- clean --dir dist
dotnet run --project src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

## Symptom 1: doctor Fails Immediately (Config Validation)

### A) Notion token missing

Symptom: Prompts that `NOTION_TOKEN` is missing or Notion-related config is unavailable.

Fix:

- Local: set the environment variable `NOTION_TOKEN`
- CI: inject via GitHub Actions Secrets (see: [13 Deploy GitHub Pages](./13-deploy-github-pages.md))

### B) Path does not exist (content/theme/build output)

Symptom: Prompts that a directory does not exist (e.g., `content`, `layouts`, `assets`).

Fix checklist:

- Confirm the directory actually exists
- Confirm you understand "relative path base" (relative to the directory containing `site.yaml`), see: [03 Project Structure](./03-project-structure.md)
- If you use `--config path/to/site.yaml`, ensure the corresponding directories are also under that config directory

### C) Field type is wrong (YAML structure does not match)

Typical errors:

- Writing a list as a string (e.g., `languages: zh-CN` instead of `languages: [zh-CN]`)
- Indentation errors causing structural misalignment

Fix:

- First compare against `examples/starter/site.yaml`, `examples/starter/site.i18n.yaml`
- Then correct according to [04 Site YAML Config](./04-site-yaml-config.md)

### D) Route conflict detected

Symptom: `doctor` or `build` fails with `Route conflict on url` or `Route conflict on outputPath`.

Fix checklist:
- Two content pages have the same slug → rename slugs or use different collection routes
- Two content pages have the same `route.outputPath` override → ensure uniqueness
- A content page URL collides with a derived page (pagination/archive/taxonomy) → change `deriveConflictPolicy` to `warn` or `last-wins`, or adjust the conflicting URL

Run `bukit doctor` first to detect conflicts without a full build.

## Symptom 2: build Succeeds, but Pages Are Missing / URLs Are Wrong

### A) slug/type changes cause path changes

Symptom: You think a page is at `/pages/about/`, but it actually outputs elsewhere.

Fix:

- Confirm the content's `type` and `slug`
- Do not casually use `route/url/outputPath/template` override fields (unless you clearly know the output path)

### B) Multilingual filtering excludes content

Symptom: After enabling `languages` on the site, certain content "disappears" in a given language.

Fix:

- Add `language` to every piece of content
- Check that the language values are exactly consistent (`en-US` should not be written as `en`)

See: [11 Multilingual & SEO](./11-i18n-seo.md).

## Symptom 3: 404 After Deployment (Local Preview Works Fine)

### A) baseUrl misconfigured (most common for project repos)

Symptoms:

- Homepage loads, but CSS/images 404
- Or internal site links 404 after clicking

Fix:

- Project repos must set `baseUrl: /<repo>`
- During build, it is recommended to override via CLI: `--base-url /<repo> --site-url https://<owner>.github.io/<repo>`

See: [13 Deploy GitHub Pages](./13-deploy-github-pages.md).

### B) Upload directory is wrong

Symptom: GitHub Pages deployment succeeds, but content is empty.

Fix:

- Confirm the workflow's `upload-pages-artifact` `path` points to the actual output directory (e.g., `_site`)

## Symptom 4: Preview Port Occupied or Cannot Be Opened

Fix:

- Use `--port auto` to auto-select a port
- Or switch to a different port: `--port 4174`
- If you need a fixed port but it is occupied, stop the process occupying that port first

## Symptom 5: Changed Content/Templates, but Output Did Not Change

Prioritize the "elimination method":

1. `build --clean` (ensure the output directory is cleaned)
2. Temporarily disable incremental: `--no-incremental`
3. Clean the cache directory: the directory pointed to by `--cache-dir` (default `.cache`) or run `clean`

If you truly rely on incremental builds for speed, it is recommended to get the site working first, then gradually enable incremental.

## Symptom 6: Modules (data) Not Taking Effect

Symptoms:

- `site.modules.*` is empty
- The homepage does not render banner/faq etc. modules

Diagnosis checklist:

- In sources, is modules set to `mode: data`?
- Does the module data include `type` (determines the grouping key)?
- Does the theme template read `site.modules` (compare against example themes)?

See: [09 Modules Structured Data](./09-modules-data.md).

