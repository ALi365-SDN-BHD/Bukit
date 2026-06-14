---
name: bukit-deploy
description: Use when using bukit to deploy a site to GitHub Pages, troubleshooting deploy failures, configuring deploy in site.yaml, setting up CI/CD deployment with bukit, or the user asks about "deploy my site", "publish to GitHub Pages", "bukit deploy", "gh-pages deployment"

status: stable
since: "v3.0.0"
verified_by:
  - "src/Bukit.Cli/Commands/DeployCommand.cs"
  - "src/Bukit.Config/DeployConfig.cs"
source_anchors:
  - "src/Bukit.Cli/Commands/DeployCommand.cs"
  - "src/Bukit.Config/DeployConfig.cs"
guide_chapters:
  - "guide/user/13-deploy-github-pages.md"
---

# Bukit Deployment (GitHub Pages)

## Overview

Bukit supports engine-level deployment via the `bukit deploy` CLI command. It builds the site if needed, then pushes the output to a GitHub Pages `gh-pages` branch using `git`. No external CI/CD template required — a single command handles build + deploy.

**REQUIRED SUB-SKILL:** Configure `site.yaml` `deploy` section using `bukit-config`. CLI details reference `bukit-cli-reference`.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "部署站点"、"发布到 GitHub Pages"、"bukit deploy"、"gh-pages 部署"、"自动部署" |
| English | "deploy my site", "publish to GitHub Pages", "bukit deploy", "gh-pages deployment", "auto deploy" |
| Bahasa Melayu | "deploy laman", "terbit ke GitHub Pages", "bukit deploy", "penempatan gh-pages", "auto deploy" |

## Prerequisites

1. **git CLI** must be installed and available in `PATH`
2. **GITHUB_TOKEN** environment variable must be set (GitHub personal access token with `repo` scope)
3. The current directory must be a **git repository** with a remote `origin` pointing to GitHub
4. The repository's **GitHub Pages** must be enabled in repository Settings → Pages (set source to `gh-pages` branch, root `/`)

## CLI Usage

### Basic Deploy (build + push)

```bash
bukit deploy
```

This runs `bukit build` first, then pushes the output directory (default `dist`) to the `gh-pages` branch.

### Skip Build (deploy existing output)

```bash
bukit deploy --skip-build
```

Use when the site is already built and you only want to push.

### Dry Run (preview without pushing)

```bash
bukit deploy --dry-run
```

Shows what would be deployed without actually pushing to GitHub.

### Custom Branch and Message

```bash
bukit deploy --branch pages --message "Release v2.0.0"
```

### CI Mode

```bash
bukit deploy --ci
```

Reduces log verbosity and uses CI mode suitable for CI systems.

### Full Options Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--config <path>` | string | `site.yaml` | Config file path |
| `--site <name>` | string | — | Multi-site name |
| `--output <dir>` | string | from config | Override output directory |
| `--base-url <url>` | string | from config | Override site.baseUrl |
| `--site-url <url>` | string | from config | Override site.url |
| `--branch <name>` | string | `gh-pages` | Target Git branch |
| `--message <text>` | string | `bukit deploy` | Commit message |
| `--ci` | flag | — | CI mode (quieter logs) |
| `--dry-run` | flag | — | Preview without pushing |
| `--skip-build` | flag | — | Skip build, use existing output |

## Configuration (site.yaml)

The `deploy` section is optional. If it is present, `provider` is required and must be `github-pages`; Bukit 1.0 does not support other deploy providers.

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
  message: "bukit deploy"
  cname: example.com
  keepHistory: false
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `provider` | string | — | Required when `deploy` is present; currently only `github-pages` |
| `branch` | string | `gh-pages` | Target Git branch for deployment |
| `message` | string | `bukit deploy` | Git commit message for each deploy |
| `cname` | string | — | Custom domain (will write a `CNAME` file) |
| `keepHistory` | bool | `false` | Whether to keep full Git history on the deploy branch |

## URL Auto-Detection

When deploying, Bukit automatically detects the repository type and computes the correct URL:

- **User/Org Pages** (`owner.github.io`): site is deployed at `https://owner.github.io`, `baseUrl` should be `/`
- **Project Pages** (`owner/repo`): site is deployed at `https://owner.github.io/repo`, `baseUrl` should be `/<repo>`

The deployed URL is printed after a successful deployment.

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `GITHUB_TOKEN` | Yes | GitHub personal access token with `repo` scope |

## How It Works

1. **Build** (unless `--skip-build`): Runs `bukit build` with the configured options
2. **Prepare Git worktree**: Creates a temporary directory, clones or initializes the target branch
3. **Copy output**: Copies the build output into the temporary directory
4. **Add deployment files**: Creates `.nojekyll` (to disable Jekyll processing) and `CNAME` (if configured)
5. **Commit**: `git add -A && git commit -m "<message>"`
6. **Push**: `git push origin <branch>`

## Common Errors

### `git command not found`
Install git and ensure it is in your `PATH`.

### `GITHUB_TOKEN environment variable is required`
Set `GITHUB_TOKEN` to a GitHub personal access token with `repo` scope:
```bash
export GITHUB_TOKEN=ghp_xxxxxxxxxxxx
```

### `Unable to determine GitHub repository`
Ensure your current directory is a git repository with a remote `origin` pointing to a GitHub repository:
```bash
git remote -v
```

### `Output directory not found` / `Output directory is empty`
The build did not produce any files. Run `bukit build` first to verify the build succeeds, or use `bukit deploy` without `--skip-build`.

### 404 after deployment
Check that:
1. GitHub Pages is enabled in repository Settings → Pages (source: `gh-pages` branch, `/` root)
2. `baseUrl` is correct: `/` for user/org pages, `/<repo>` for project pages
3. `site.url` is set correctly (e.g., `https://owner.github.io/repo`)

### CSS/JS 404 (resources not loading)
This is usually caused by incorrect `baseUrl`. For project pages, set `baseUrl: /<repo>` in `site.yaml` or pass `--base-url /<repo>`.

## Architecture (for extending)

The deployment surface is Core 1.0 stable for GitHub Pages only.
New providers must be introduced and hardened in Labs, then promoted to Core through normal compatibility review.
