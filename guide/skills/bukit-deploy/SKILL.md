---
name: bukit-deploy
description: Use when configuring or troubleshooting Bukit GitHub Pages deployment, `deploy` options, dry runs, skip-build behavior, branch/message/CNAME settings, or deploy validation errors.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Cli.Tests/DeployCommandTests.cs"
  - "tests/Bukit.Engine.Tests/DeployConfigTests.cs"
source_anchors:
  - "src/Bukit.Cli/Commands/DeployCommand.cs"
  - "src/Bukit.Cli/Deploy/GitHubPagesDeployProvider.cs"
  - "src/Bukit.Config/DeployConfig.cs"
  - "src/Bukit.Config/ProviderValidators.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit Deploy

Bukit Core 1.0 deploys to GitHub Pages.

## Config

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
  message: "Deploy site"
  cname: example.com
  keepHistory: true
```

`deploy.provider` must be `github-pages` when a deploy section is present.

## Commands

```bash
bukit deploy --dry-run
bukit deploy --config site.yaml
bukit deploy --skip-build --output dist
bukit deploy --branch gh-pages --message "Deploy site"
```

By default, deploy runs `bukit build` first. `--skip-build` still validates config, but provider-secret validation is relaxed for Notion sources.

## Options

| Option | Meaning |
|---|---|
| `--dry-run` | Print deployment plan without pushing |
| `--skip-build` | Deploy existing output |
| `--base-url` | Override `site.baseUrl` |
| `--site-url` | Override `site.url` |
| `--output` | Override output directory |
| `--branch` | Target GitHub Pages branch |
| `--message` | Commit message |
| `--ci` | CI logging mode |
| `--force` | Permit forced branch update |

## Pre-Deploy Gate

```bash
bukit build
bukit seo audit --dir dist
bukit geo audit --dir dist
bukit publish audit --dir dist
bukit deploy --dry-run
```
