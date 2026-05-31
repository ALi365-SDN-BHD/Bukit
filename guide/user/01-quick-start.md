# 01 Quick Start: From Zero to Preview (10 Minutes)

This page walks you through the complete pipeline using a "copy-paste" approach: initialize a site →write content →build →local preview →prepare for deployment.

## What You Will Get

- A static site that can be previewed locally (output in `dist/`)
- A minimal, working `site.yaml` (all subsequent features extend from it)
- A set of the most commonly used CLI commands (build/preview/doctor/clean)

## Prerequisites

- .NET installed (the project targets .NET 10; if you run from repository source, the corresponding SDK must be available on your machine)
- Comfortable using the command line (PowerShell / bash)
- Basic understanding of YAML/Markdown syntax

## Path A: Run the In-Repo Example Site Directly (Recommended)

The example site is at: `examples/starter/`, which comes with content, themes, and variant configurations for multilingual/modules, suitable for verifying your environment is set up correctly.

### 1) Build and Self-Check (doctor)

Run from the repository root:

```bash
dotnet build bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
```

If doctor reports errors, check first: [14 Troubleshooting](./14-troubleshooting.md) (and the developer version of the doctor guide: [guide/dev/doctor](../dev/doctor.zh-CN.md); currently available in Chinese and Malay).

### 2) Build the Site (build)

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
```

The build output goes to `build.output` in the example config (default: `examples/starter/dist/`).

### 3) Local Preview (preview)

```bash
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

The console will print a local URL 鈥?open it in your browser.

## Path B: Create Your Own Site (Markdown Mode)

If you are starting a real website project, it is recommended to run `create` in a new directory, which will generate the base directory structure and default configuration.

### 1) Create Site Scaffold

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site
```

You will get a structure similar to this (illustrative; actual output depends on the scaffold):

```text
my-site/
  site.yaml
  content/
  layouts/    # or themes/ (depending on scaffold and theme choice)
  assets/
  static/
```

### 2) Edit the Minimal Config (site.yaml)

A minimal working `site.yaml` (Markdown site) looks like this:

```yaml
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
build:
  output: dist
  clean: true
theme:
  layouts: layouts
  assets: assets
  static: static
  name: alt
logging:
  level: info
```

> **Recommended: Use site.collections to define routing and templates** The config above relies on the post/page compatibility layer for routing (page →`/pages/`, post →`/blog/`). For new projects, we recommend explicitly declaring collections (see [04 Site YAML Config](./04-site-yaml-config.md)). Example:
>
> ```yaml
> site:
>   collections:
>     page:
>       permalink: /pages/{slug}/
>       template: pages/page.html
>       listRoute: /pages/
>     post:
>       permalink: /blog/{slug}/
>       template: pages/post.html
>       listRoute: /blog/
> ```

For a more complete explanation of fields and defaults, see: [04 Site YAML Config](./04-site-yaml-config.md).

### 3) Write Your First Piece of Content (content/hello-world.md)

```markdown
---
type: page
title: Hello World
slug: hello-world
tags: [demo, first]
summary: This is my first page
---

# Hello World

If you can see this text, the build and rendering pipeline has run successfully.
```

### 4) Self-Check (doctor)

Run from the site directory:

```bash
dotnet run --project ../src/Bukit.Cli -c Release -- doctor --config site.yaml
```

### 5) Build and Preview

```bash
dotnet run --project ../src/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
dotnet run --project ../src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

## Next Steps (by Site Type)

- Writing content (Markdown): [05 Content Markdown](./05-markdown-content.md)
- Using Notion: [06 Content Notion](./06-notion-content.md)
- Multi-source composition (pages/posts/modules): [07 Multi Source](./07-multi-source.zh-CN.md) (currently available in Chinese and Malay)
- Company site modules (Modules): [09 Modules Structured Data](./09-modules-data.md)
- Multilingual & SEO: [11 Multilingual & SEO](./11-i18n-seo.md)
- Deploying to GitHub Pages: [13 Deploy GitHub Pages](./13-deploy-github-pages.md)

