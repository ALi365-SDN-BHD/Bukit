# Bukit AI Demo-to-CMS Workflow

## Overview

This directory defines the engineering workflow for AI-assisted Bukit site creation.

The goal is not to generate disposable HTML. The goal is to guide AI tools through a stable, reviewable, and buildable pipeline:

```text
User requirements
-> AI-generated visual HTML Demo
-> User confirmation
-> Bukit theme + content seed + configuration
-> Notion CMS when required
-> Static build
-> Deployment
```

The workflow is designed for ChatGPT, Codex, Claude Code, Cursor, Trae, and any agent that can read repository instructions.

## Files

| File | Purpose |
|---|---|
| `README.md` | Workflow entry point |
| `engineering-spec.md` | Full engineering specification |
| `prompt-template.md` | Prompt template for AI tools |
| `checklist.md` | Stage-by-stage validation checklist |
| `config/` | Configuration contracts and schema references |

## Core Principles

### 1. Demo First, Engineering Second

The AI should generate a visual HTML Demo before producing a final Bukit project. This gives the user a chance to review the style, content direction, page structure, and functionality.

### 2. The Demo Must Be Migratable

The Demo is not a throwaway artifact. It must be designed so Bukit or an AI agent can later convert it into:

- Theme templates
- Partials
- Components
- Content seed
- Notion seed
- Route map
- Site configuration

### 3. Content Must Be Structured

Business copy, page body content, posts, company data, services, SEO information, and metadata should eventually move into structured data files or Notion.

### 4. Themes Should Hold Structure, Not Long-term Business Copy

Theme files should contain layout, components, style hooks, loops, and variables. They should not permanently contain long business text, article bodies, company profiles, FAQ content, or SEO metadata.

### 5. Bukit Is the Quality Gate

Every conversion must be validated with:

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

## Standard Import Command

```bash
bukit import html-demo ./demo   --theme <theme-name>   --content-source notion   --build-source markdown   --route-map demo.routes.yaml   --strict warn   --force   --verify
```

## Standard Notion Push Command

```bash
bukit notion push   --input sites/<site-name>/notion-seed   --database-map sites/<site-name>/notion-seed/notion-database-map.yaml   --create-missing-databases   --parent-page-id <notion-parent-page-id>   --mode upsert   --update-content replace
```

## Standard Notion-only Build Mode

```bash
bukit import html-demo ./demo   --theme <theme-name>   --content-source notion   --build-source notion   --route-map demo.routes.yaml   --force
```

## Default Notion Push Scope

By default, the following collections are pushed to Notion:

```text
pages
posts
companies
services
```

The following seed files are review-only unless dedicated schemas are introduced:

```text
sections
faqs
media
components
```

## Configuration Contracts

Configuration contracts are located in:

```text
docs/ai-demo-to-bukit/config/
```

Machine-readable schemas are located in:

```text
schemas/
```

AI agents must consult these files before generating or modifying configuration.
