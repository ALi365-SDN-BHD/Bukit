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

## Diagnostic Codes

When Bukit encounters an error, it outputs a stable `BKT-XXXX` diagnostic code. Common user-facing codes:

| Code | Meaning | Quick Fix |
|---|---|---|
| `BKT-0002` | Config invalid value | A config field has an unrecognized value — e.g. `externalPluginPolicy: alow` (should be `allow`). Check spelling. |
| `BKT-0004` | Path traversal detected | A path value escapes the project boundary — e.g. `--site ../../../etc/passwd`. Use a valid relative path. |
| `BKT-0201` | Route conflict | Two pages have same URL/path — rename slugs or adjust permalinks |
| `BKT-0301` | Template not found | Check `site.collections` template paths exist under `layouts/` |
| `BKT-0302` | Template parse error | Scriban syntax error — check `{{ }}` matching |
| `BKT-0303` | Layout nesting exceeded | Circular `{% layout %}` reference — max depth is 10 |
| `BKT-0402` | Schema strict mode blocked | A required schema field is missing — fix content or set `build.schemaFailMode: warn` |
| `BKT-0601` | Output unsafe | `build.output` points to a protected directory — use a dedicated output directory |
| `BKT-0701` | Plugin execution failed | Plugin crashed or permission denied — check `capabilities` if declared |

Run `bukit doctor` first to get a formatted diagnostic output.

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

## Symptom 7: Clean Refuses to Delete Output Directory

Symptoms:

- `build --clean` fails with "output directory clean refused"
- The output directory is not deleted

Cause: Bukit now requires a `.bukit-output-marker` file in the output directory before cleaning. This prevents accidental deletion of non-Bukit directories (e.g., project root, home directory, `.git`).

Fix:

- If the directory was created by Bukit: run a full build first (it writes the marker), then clean.
- If the directory is not a Bukit output: manually delete it or choose a different output directory.
- If you're pointing `build.output` to an existing non-Bukit directory: change `build.output` to a dedicated directory.

## Symptom 8: Plugin stdout/stderr Limit Exceeded

Symptoms:

- Build fails with "stdout limit exceeded" or "stderr limit exceeded"
- An external plugin process is killed during build

Cause: The external plugin produced more output than the configured `maxStdoutBytes` or `maxStderrBytes` limit.

Fix:

- Increase the limit in `site.externalPlugins.<name>.maxStdoutBytes` / `maxStderrBytes`.
- Or remove the limit (delete the config field) to allow unlimited output.
- Investigate why the plugin is producing excessive output — it may indicate a bug.

## Symptom 9: Theme Lock Commit Mismatch

Symptoms:

- Build fails with "Theme lock mismatch for ... locked commit ..., current commit ..."
- A remote theme that worked before now fails

Cause: A remote theme (`theme.source`) was previously built and locked to a specific Git commit. The cached theme now has a different commit than the one recorded in `bukit-theme.lock.json`.

Fix:

- Delete the theme's local cache directory and the lock file, then rebuild to re-clone.
- Or delete just the lock file to force re-verification.
- If you intentionally updated the theme, the lock file needs to be regenerated.

## Symptom 10: Template Variables Render Empty

Symptoms:

- A Scriban variable like `{{ page.title }}` works, but `{{ page.auther }}` renders blank without any build error.

Cause: Bukit's Scriban engine has `EnableRelaxedMemberAccess` enabled — typos in variable names silently return `null` instead of throwing an error.

Fix:

- Run `bukit doctor` to perform the **template variable spell check** section. It scans all `.html` templates for unknown variable references.
- Check the Known Fields Whitelist in the [bukit-templating skill](../../src/skills/bukit-templating/SKILL.md) for the correct field names.

## Symptom 11: Plugin Permission Denied (Capability Enforcement)

Symptoms:

- Build fails with `[BKT-0701] Plugin './tools/plugin' is missing required capability 'derive-pages' for hook 'derive-pages'.`

Cause: An external plugin declared `capabilities` but the capability list doesn't cover all hooks the plugin is registered for.

Fix:

- Add the missing capability to the plugin's `capabilities` list in `site.yaml`:
  ```yaml
  site:
    externalPlugins:
      my-plugin:
        capabilities:
          - derive-pages   # Added: matches hooks list
          - emit-outputs
  ```
- Or remove the `capabilities` field entirely to allow all hooks (backward compatible).

