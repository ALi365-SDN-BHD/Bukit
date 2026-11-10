---
name: bukit-publish-deploy
description: Use before releasing a Bukit site — `bukit publish audit` / `publish diff` release-readiness checks, `bukit deploy` to GitHub Pages (deploy config, dry run, tokens, other repositories, custom domains, force push), and CI workflows.
---

# Bukit Publish and Deploy

Release flow: build → audits → deploy. Core supports one deploy provider:
GitHub Pages.

## publish audit

`bukit publish audit` reads `<dir>/.bukit/publish-audit-report.json` and checks release
readiness, which `seo audit` does not cover:

- Machine-readable coverage: every route has its JSON and Markdown projection and
  appears in feeds, sitemap, search, llms output, and `agent-manifest.json` as expected
  (`publish.representation_*`, `publish.*_missing_route`, `publish.manifest_*`).
- Semantic HTML: `main`, `article`, `header`, `nav`, `footer`, heading order
  (`publish.semantic_*_missing`, `publish.heading_level_skip`).
- JSON-LD matches visible content (`publish.jsonld_{title,description,date,author}_mismatch`).
- Trust metadata and images (`publish.author_missing`, `publish.summary_missing`,
  `publish.image_alt_missing`, `publish.content_duplicate`, ...).
- AI crawler conflicts (`publish.ai_crawler_policy_conflict`).

```bash
bukit publish audit --dir dist            # exit 1 on errors
bukit publish audit --dir dist --strict   # exit 1 on warnings too
bukit publish diff --baseline base.json --current dist/.bukit/publish-audit-report.json \
  --max-new-errors 0 --fail-on-route-removed
```

Options and exit codes match `seo audit` / `seo diff` (see `bukit-seo`). `--dir`
defaults to `dist`, not `build.output`.

## Deploy configuration

```yaml
deploy:
  provider: github-pages   # required when the section exists; only accepted value
  branch: gh-pages         # default
  message: bukit deploy    # default
  repo: owner/name         # optional; not a URL, no .git
  cname: www.example.com   # optional; writes CNAME
  keepHistory: false       # false: shallow fetch of the target branch
```

Without a `deploy:` section, GitHub Pages defaults are used. Set `site.url` and
`site.baseUrl` to match where the site will live: `baseUrl: /<repo>/` for a project
page, `/` for `<owner>.github.io` or a custom domain.

## Deploy command

```bash
bukit deploy --dry-run                    # show the plan; needs an existing completed build
bukit deploy                              # clean build, then push
bukit deploy --skip-build                 # push the existing output
bukit deploy --repo owner/site --token-env PAGES_TOKEN
bukit deploy --force                      # allow a non-fast-forward push
```

- Repository: `--repo` > `deploy.repo` > the project's `origin`.
- Token: `GITHUB_TOKEN` by default. `--token-env NAME` takes an upper-case variable name
  (`[A-Z_][A-Z0-9_]*`, ≤ 128 chars), never the token value. The token needs Contents read and write on the target repository.
- `--branch`, `--message` override config. `--ci`, `--base-url`, `--site-url`,
  `--output` pass to the internal build.
- `--dry-run` and `--skip-build` require a non-empty output whose
  `.bukit-build-state.json` is `completed`, and skip provider secret checks such as
  `NOTION_TOKEN`.
- Every deploy also needs `<output>/.bukit/publish-audit-report.json`
  (`BKT-DEPLOY-PRIVACY-0002` if missing). Do not delete `.bukit/` before deploying.
- The deploy copies the output except `.bukit/`, `.bukit-build-state.json`,
  `.bukit-output-marker`, and symlinks; it always adds `.nojekyll` and adds `CNAME` when
  configured. A privacy scan blocks publishing Notion provenance
  (`BKT-DEPLOY-PRIVACY-0001`). An existing `CNAME` on the target branch is kept; delete
  it on the branch to drop a custom domain.
- Success prints `Expected site URL (publication not verified): <url>`. Check the
  GitHub Pages settings (source branch) yourself.
- `Non-fast-forward push rejected`: someone else changed the branch; rerun with
  `--force` only if overwriting is intended.

Requires `git` on PATH. All failures exit 1 except a failed internal build, which
returns the build's own code.

## CI (GitHub Actions)

```yaml
name: Deploy
on:
  push:
    branches: [main]
permissions:
  contents: write
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      # install the bukit binary here
      - run: bukit build --ci
      - run: bukit seo audit --dir dist
      - run: bukit publish audit --dir dist
      - run: bukit deploy --ci --skip-build
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

For a different target repository use a fine-grained personal access token stored as a
secret and pass `--token-env`. `--ci` makes the security gate strict (see `bukit-build`).
