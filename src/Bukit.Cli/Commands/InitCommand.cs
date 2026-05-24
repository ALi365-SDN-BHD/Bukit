namespace Bukit.Cli.Commands;

public static class InitCommand
{
    private static readonly string[] SupportedTemplates = ["minimal", "blog", "docs", "landing", "portfolio"];

    public static Task<int> RunAsync(ArgReader reader)
    {
        var targetDir = reader.GetArg(1);
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            Console.Error.WriteLine("init requires a target directory.");
            return Task.FromResult(2);
        }

        var provider = (reader.GetOption("--provider") ?? "markdown").Trim().ToLowerInvariant();
        var templateName = (reader.GetOption("--template") ?? "minimal").Trim().ToLowerInvariant();
        if (!SupportedTemplates.Contains(templateName, StringComparer.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown template: {templateName}. Available: {string.Join(", ", SupportedTemplates)}.");
            return Task.FromResult(2);
        }

        var root = Path.GetFullPath(targetDir);
        Directory.CreateDirectory(root);

        var themeRoot = Path.Combine(root, "themes", "starter");

        Directory.CreateDirectory(Path.Combine(root, "content"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "static"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "partials"));

        WriteFile(root, ".gitignore", "dist/\n.cache/\n.bukit/\n");
        WriteFile(root, "README.md", BuildReadme(Path.GetFileName(root), templateName));
        WriteContentSkeleton(root, templateName);
        WriteTheme(root, templateName);

        WriteFile(root, "site.yaml", BuildSiteYaml(provider, templateName));

        Console.WriteLine($"Initialized: {root}");
        Console.WriteLine($"Template: {templateName}");
        return Task.FromResult(0);
    }

    private static void WriteFile(string rootDir, string relativePath, string content)
    {
        var path = Path.Combine(rootDir, relativePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, content);
    }

    private static void WriteTheme(string rootDir, string templateName)
    {
        var preset = WizardPreset.All.FirstOrDefault(p =>
            p.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));

        if (preset is null)
        {
            StarterThemeScaffold.WriteTo(rootDir);
            return;
        }

        var brand = Path.GetFileName(rootDir);
        CloneThemeGenerator.WriteTo(rootDir, "starter", preset.Tokens, preset.Layout, brand, preset.Behaviors);
        Directory.CreateDirectory(Path.Combine(rootDir, "themes", "starter", "static"));
    }

    private static string BuildReadme(string siteName, string templateName)
        => $"""
# {siteName}

Powered by bukit.

Template: {templateName}

## Local workflow

```bash
bukit doctor
bukit build --clean
bukit preview --dir dist --port auto
```
""";

    private static void WriteContentSkeleton(string rootDir, string templateName)
    {
        switch (templateName)
        {
            case "blog":
                WriteFile(rootDir, Path.Combine("content", "posts", "welcome.md"), """
---
type: post
title: Welcome to Your Blog
slug: welcome
date: 2026-01-01
summary: A first post you can replace with your own writing.
tags: [intro, bukit]
categories: [news]
---

# Welcome to Your Blog

Use this post as the first entry in your new Bukit blog.
""");
                WriteFile(rootDir, Path.Combine("content", "pages", "about.md"), """
---
type: page
title: About
slug: about
summary: A short page about this site.
---

# About

Replace this page with your story, profile, or project background.
""");
                break;

            case "docs":
                WriteFile(rootDir, Path.Combine("content", "docs", "getting-started.md"), """
---
type: doc
title: Getting Started
slug: getting-started
summary: Start here to learn how this documentation site is organized.
weight: 1
---

# Getting Started

This page is the first documentation article in your Bukit docs site.
""");
                WriteFile(rootDir, Path.Combine("content", "docs", "configuration.md"), """
---
type: doc
title: Configuration
slug: configuration
summary: Document the most important settings for your project.
weight: 2
---

# Configuration

Use this page to describe setup, options, and common workflows.
""");
                break;

            case "landing":
                WriteFile(rootDir, Path.Combine("content", "pages", "overview.md"), """
---
type: page
title: Product Overview
slug: overview
summary: Explain your offer, audience, and value proposition.
---

# Product Overview

Use this page to describe what you are launching and who it helps.
""");
                WriteFile(rootDir, Path.Combine("content", "pages", "contact.md"), """
---
type: page
title: Contact
slug: contact
summary: Tell visitors how to reach you.
---

# Contact

Add your email, booking link, or contact form instructions here.
""");
                break;

            case "portfolio":
                WriteFile(rootDir, Path.Combine("content", "work", "sample-project.md"), """
---
type: work
title: Sample Project
slug: sample-project
summary: A starter portfolio item for your selected work.
tags: [featured]
---

# Sample Project

Describe the project, your role, the outcome, and any relevant links.
""");
                WriteFile(rootDir, Path.Combine("content", "pages", "about.md"), """
---
type: page
title: About
slug: about
summary: Introduce the person or studio behind the portfolio.
---

# About

Share your background, services, and contact details here.
""");
                break;

            default:
                WriteFile(rootDir, Path.Combine("content", "hello-world.md"), "# Hello World\n\n这是一个示例页面。\n");
                break;
        }
    }

    private static string BuildSiteYaml(string provider, string templateName)
    {
        var collections = BuildCollectionsYaml(templateName);
        var defaultType = templateName switch
        {
            "blog" => "post",
            "docs" => "doc",
            "portfolio" => "work",
            _ => "page"
        };

        if (provider == "notion")
        {
            return $$"""
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
{{collections}}

content:
  provider: notion
  notion:
    databaseId: xxxxx

build:
  output: dist
  clean: true

theme:
  name: starter
  layouts: layouts
  assets: assets
  static: static
  params:
    brand: My Site
    footer_text: My Site

logging:
  level: info
""";
        }

        return $$"""
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
{{collections}}

content:
  provider: markdown
  markdown:
    dir: content
    defaultType: {{defaultType}}

build:
  output: dist
  clean: true

theme:
  name: starter
  layouts: layouts
  assets: assets
  static: static
  params:
    brand: My Site
    footer_text: My Site

logging:
  level: info
""";
    }

    private static string BuildCollectionsYaml(string templateName)
        => templateName switch
        {
            "blog" => """
    post:
      permalink: /blog/{year}/{month}/{slug}/
      template: pages/post.html
      listRoute: /blog/
      pagination:
        enabled: true
        pageSize: 10
        urlPattern: page/:num/
      output:
        rss: true
        archive:
          enabled: true
          depth: monthly
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/
""",
            "docs" => """
    doc:
      permalink: /docs/{slug}/
      template: pages/page.html
      listRoute: /docs/
    page:
      permalink: /{slug}/
      template: pages/page.html
""",
            "landing" => """
    page:
      permalink: /{slug}/
      template: pages/page.html
      listRoute: /pages/
""",
            "portfolio" => """
    work:
      permalink: /work/{slug}/
      template: pages/page.html
      listRoute: /work/
    page:
      permalink: /{slug}/
      template: pages/page.html
""",
            _ => """
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/
"""
        };

}
