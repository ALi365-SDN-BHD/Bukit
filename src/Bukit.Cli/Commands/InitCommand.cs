using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class InitCommand
{
    private static readonly string[] SupportedTemplates = ["minimal", "blog", "docs", "landing", "portfolio", "bare", "none"];

    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var targetDir = command.GetArgument(0);
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            Console.Error.WriteLine("init requires a target directory.");
            return Task.FromResult(2);
        }

        var provider = (command.GetString("--provider") ?? "markdown").Trim().ToLowerInvariant();
        var templateName = (command.GetString("--template") ?? "minimal").Trim().ToLowerInvariant();
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
        WriteDefaultOgImage(root);

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

    private static void WriteDefaultOgImage(string rootDir)
    {
        var path = Path.Combine(rootDir, "themes", "starter", "assets", "og-default.gif");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path,
        [
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, // GIF89a
            0xB0, 0x04, // 1200
            0x76, 0x02, // 630
            0x80, 0x00, 0x00,
            0x1F, 0x29, 0x37,
            0xF8, 0xFA, 0xFC,
            0x2C,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x01, 0x00,
            0x00,
            0x02,
            0x02, 0x44, 0x01,
            0x00,
            0x3B
        ]);
    }

    private static void WriteTheme(string rootDir, string templateName)
    {
        // For "none", skip theme generation entirely.
        if (templateName.Equals("none", StringComparison.OrdinalIgnoreCase))
            return;

        var templateScope = templateName.Equals("bare", StringComparison.OrdinalIgnoreCase)
            ? TemplateScope.Bare
            : TemplateScope.Full;

        var preset = WizardPreset.All.FirstOrDefault(p =>
            p.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));

        if (preset is null)
        {
            if (templateScope == TemplateScope.Bare)
            {
                // Bare mode: use CloneThemeGenerator with bare scope (no StarterThemeScaffold)
                CloneThemeGenerator.WriteTo(rootDir, "starter", CloneTokens.Default, CloneLayoutInfo.Default,
                    brand: "My Site", templateScope: TemplateScope.Bare);
                Directory.CreateDirectory(Path.Combine(rootDir, "themes", "starter", "static"));
                return;
            }
            StarterThemeScaffold.WriteTo(rootDir);
            return;
        }

        var brand = Path.GetFileName(rootDir);
        var profile = GetTemplateProfile(templateName);
        var layout = preset.Layout with
        {
            SiteTitle = profile.Title,
            HeroHeading = profile.HeroHeading,
            HeroSubtext = profile.Description,
            HasFeaturesSection = preset.Layout.HasFeaturesSection || profile.HasFeatureModules,
            HasCTASection = preset.Layout.HasCTASection || profile.HasCallToActionModule,
            HeroCtaText = profile.HeroCtaText ?? preset.Layout.HeroCtaText,
            HeroCtaUrl = profile.HeroCtaUrl ?? preset.Layout.HeroCtaUrl,
        };
        CloneThemeGenerator.WriteTo(rootDir, "starter", preset.Tokens, layout, brand, preset.Behaviors, templateScope: templateScope);
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
                WriteModuleData(rootDir, "features", "Start writing", "Draft posts in Markdown and publish them with clean archive, feed, and taxonomy pages.");
                WriteFile(rootDir, Path.Combine("content", "posts", "welcome.md"), """
---
type: post
title: Welcome to Your Blog
slug: welcome
date: 2026-01-01
author: Bukit Team
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
                WriteModuleData(rootDir, "features", "Start reading", "Use the generated docs index as the entry point for setup, configuration, and reference material.");
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
                WriteModuleData(rootDir, "features", "Clear positioning", "Use focused sections to explain the offer, audience, and next action.");
                WriteModuleData(rootDir, "features", "Fast static delivery", "Ship a lightweight static site with reusable Bukit content and theme primitives.", order: 20);
                WriteModuleData(rootDir, "call_to_action", "Start building today", "Replace this call to action with your signup, booking, or contact link.");
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
                WriteModuleData(rootDir, "features", "Selected work", "Highlight case studies, visual projects, and project notes from one content model.");
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

    private static void WriteModuleData(string rootDir, string type, string title, string desc, int order = 10)
        => WriteFile(rootDir, Path.Combine("data", $"{type}-{order}.md"), $$"""
---
type: {{type}}
title: {{title}}
order: {{order}}
enabled: true
desc: {{desc}}
---

""");

    private static string BuildSiteYaml(string provider, string templateName)
    {
        var collections = BuildCollectionsYaml(templateName);
        var profile = GetTemplateProfile(templateName);
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
  title: {{profile.Title}}
  baseUrl: /
  url: https://example.com
  seo:
    defaultImage: /assets/og-default.gif
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
    brand: {{profile.Brand}}
    footer_text: {{profile.Brand}}
    latest_heading: {{profile.LatestHeading}}

logging:
  level: info
""";
        }

        var contentYaml = BuildMarkdownContentYaml(templateName, defaultType);
        return $$"""
site:
  name: my-site
  title: {{profile.Title}}
  baseUrl: /
  url: https://example.com
  seo:
    defaultImage: /assets/og-default.gif
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
{{collections}}

content:
{{contentYaml}}

build:
  output: dist
  clean: true

theme:
  name: starter
  layouts: layouts
  assets: assets
  static: static
  params:
    brand: {{profile.Brand}}
    footer_text: {{profile.Brand}}
    latest_heading: {{profile.LatestHeading}}

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
            "bare" or "none" => """
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

    private static string BuildMarkdownContentYaml(string templateName, string defaultType)
        => templateName switch
        {
            "blog" => """
  provider: sources
  sources:
    - type: markdown
      name: posts
      mode: content
      collection: post
      markdown:
        dir: content/posts
        defaultType: post
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content/pages
        defaultType: page
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
""",
            "docs" => """
  provider: sources
  sources:
    - type: markdown
      name: docs
      mode: content
      collection: doc
      markdown:
        dir: content/docs
        defaultType: doc
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
""",
            "landing" => """
  provider: sources
  sources:
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content/pages
        defaultType: page
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
""",
            "portfolio" => """
  provider: sources
  sources:
    - type: markdown
      name: work
      mode: content
      collection: work
      markdown:
        dir: content/work
        defaultType: work
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content/pages
        defaultType: page
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
""",
            "bare" or "none" => """
  provider: markdown
  markdown:
    dir: content
    defaultType: page
""",
            _ => $$"""
  provider: markdown
  markdown:
    dir: content
    defaultType: {{defaultType}}
"""
        };

    private static TemplateProfile GetTemplateProfile(string templateName)
        => templateName switch
        {
            "blog" => new TemplateProfile(
                Title: "My Blog",
                Brand: "My Blog",
                Description: "Writing, updates, and field notes from your team.",
                HeroHeading: "Writing, updates, and field notes",
                LatestHeading: "Latest posts",
                HeroCtaText: null,
                HeroCtaUrl: null,
                HasFeatureModules: true,
                HasCallToActionModule: false),
            "docs" => new TemplateProfile(
                Title: "Project Docs",
                Brand: "Project Docs",
                Description: "Practical documentation for your project.",
                HeroHeading: "Practical documentation for your project",
                LatestHeading: "Documentation",
                HeroCtaText: null,
                HeroCtaUrl: null,
                HasFeatureModules: true,
                HasCallToActionModule: false),
            "landing" => new TemplateProfile(
                Title: "Product Landing",
                Brand: "Product Landing",
                Description: "Launch a focused product site with clear sections and a direct call to action.",
                HeroHeading: "Launch a focused product site",
                LatestHeading: "More information",
                HeroCtaText: "Get started",
                HeroCtaUrl: "/overview/",
                HasFeatureModules: true,
                HasCallToActionModule: true),
            "portfolio" => new TemplateProfile(
                Title: "Portfolio",
                Brand: "Portfolio",
                Description: "Selected work and project notes.",
                HeroHeading: "Selected work and project notes",
                LatestHeading: "Selected work",
                HeroCtaText: null,
                HeroCtaUrl: null,
                HasFeatureModules: true,
                HasCallToActionModule: false),
            _ => new TemplateProfile(
                Title: "My Site",
                Brand: "My Site",
                Description: "A clean content-first Bukit site.",
                HeroHeading: null,
                LatestHeading: "Latest content",
                HeroCtaText: null,
                HeroCtaUrl: null,
                HasFeatureModules: false,
                HasCallToActionModule: false)
        };

    private sealed record TemplateProfile(
        string Title,
        string Brand,
        string Description,
        string? HeroHeading,
        string LatestHeading,
        string? HeroCtaText,
        string? HeroCtaUrl,
        bool HasFeatureModules,
        bool HasCallToActionModule);

}
