# 20 Core Safety And Reliability

This chapter describes user-visible behavior introduced by the F-01 through
F-08 Core hardening work. These are current runtime guarantees, not future
roadmap items.

## Behavior Summary

| Area | Current behavior | User action |
|---|---|---|
| Output cleaning | `clean` and build cleanup use the same safety policy. | Keep generated output in a dedicated directory such as `dist`. |
| Search UI | Content titles and snippets are inserted as text nodes, not interpreted as HTML. | Do not rely on HTML embedded in search title or snippet fields. |
| Output ownership | Rendered pages, static files, assets, media, and generated theme tokens are checked for destination conflicts before publication writes begin. | Give each source a unique output destination. |
| Directory symlinks | Default recursive content, static, media, and report inventory walks do not descend through symlink or reparse-point directories. | Store publishable inputs in ordinary directories. |
| Template reload | A running process observes changes to template manifests, root templates, includes, and layout targets on the next build. | Rebuild normally; deleting `.cache` is not required for these changes. |
| Search content cap | `site.search.maxContentLength` applies to document, list, plugin, publish projection, and every multilingual search mode. | Set a positive value or keep the default `8000`. |
| Media concurrency | `content.media.maxConcurrency` limits simultaneous media localization downloads separately for each rewrite operation and each localized body store. | Lower the value when a remote host rate-limits requests. |
| Build health | Build warning/error counts and `generatedFiles` describe the current build and final public output inventory. | Use `.bukit/build-report.json` for build health, not SEO or publish issue totals. |

## Safe Output Cleaning

`bukit clean --dir <path>`, config-based clean, normal build cleanup, and
incomplete-build recovery all use the same output cleaner.

Bukit refuses to recursively delete:

- the project root, user home, or filesystem root;
- a path outside the current project root;
- `.git` or any descendant path containing a `.git` segment;
- a target reached through a symlink or reparse-point segment below the project
  root;
- a non-empty directory without `.bukit-output-marker`.

An empty safe directory can be removed. A successful Bukit build writes the
marker automatically. Do not add a marker to an arbitrary directory to bypass
the review step; move the output to a dedicated directory instead.

## Deterministic Output Ownership

Before rendering or copying publication files, Bukit builds an output plan for
static files, theme assets, localized media, generated theme tokens, content
and list pages, and rendered static templates. Two different categories cannot
own the same destination. A file also cannot own a path that another output
needs as a directory.

The comparison follows the actual output filesystem. On a case-insensitive
volume, `Assets/App.css` and `assets/app.css` conflict even when the operating
system can also use case-sensitive volumes. Conflicts fail with
`BuildAssetOutputCollision` before either competing publication file is
written.

Parent-theme and site-theme files in the same category retain the documented
site override behavior. Output ownership for arbitrary after-build third-party
plugin files is not part of this guarantee.

## Symlink Boundary

With the default `build.followSymlinks: false`, recursive source discovery does
not descend through directory symlinks or Windows reparse points in the content,
static, media, hashing, and report-inventory paths covered by Core.

`build.followSymlinks: true` remains limited to supported copy paths that
perform their own real-path and source-root checks. It is not a global switch
that makes every recursive scanner follow links.

## Live Template Changes

Template capability decisions are derived from current file contents. The next
build in the same `dev` process observes:

- `layouts/bukit.templates.yaml` capability manifest changes;
- a manifest that appears, is deleted, or is corrected after invalid YAML;
- root template changes;
- included template changes;
- a changed layout directive target.

If a change is still not visible, verify that the watcher saw the file and that
the edited path belongs to the resolved theme. Cache deletion is a diagnostic
step, not the normal invalidation contract.

## Search Safety And Size

The default search UI treats `title` and `snippet` as text. Marked query matches
are constructed with text nodes and `<mark>` elements; content is never passed
through an HTML interpretation sink.

`site.search.maxContentLength` limits only the `content` field. It does not
truncate title, summary, or generated snippet fields. The value is measured in
.NET UTF-16 code units, and Bukit avoids splitting a valid surrogate pair. A
zero or negative value is rejected during config validation.

In merged mode, the cap applies to the root merged records. In split and index
modes, it applies to the records in each language's `search.json`.

## Media Download Budget

`content.media.maxConcurrency` is the maximum number of active media
localization downloads. Each rewrite operation has its own gate shared across
its documents, HTML bodies, and media fields. Each localized body store has a
separate store-level gate shared across its concurrent reads. Both are separate
from render jobs and document-transform concurrency.

Retries do not increase the configured permit count. Cancellation and failed
downloads release acquired permits. Separate public rewrite operations have
separate operation budgets; the setting is not a process-wide network governor.

## Build Report Truth

In `.bukit/build-report.json`:

- JSON pointers `/summary/warningCount` and `/summary/errorCount` identify the
  fields that count build diagnostic events
  emitted by the current build, including concurrent language variants;
- counts reset between builds and do not copy SEO, publish, or security issue
  totals;
- `generatedFiles` is a stable, root-relative inventory of final public output;
- the inventory excludes `.bukit/`, `.bukit-build-state.json`,
  `.bukit-output-marker`, and files reachable only through directory symlinks.

The frozen `build-report.v1` shape did not change. Internal report hashes remain
in `.bukit/artifact-manifest.json` and are not duplicated into
`generatedFiles`.

## Related References

- [Site YAML Config](04-site-yaml-config.md)
- [Built-In Outputs](10-built-in-outputs.md)
- [CLI Reference](12-cli-reference.md)
- [Troubleshooting](14-troubleshooting.md)
- [Parameter Cheatsheet](16-parameter-cheatsheet.md)
