# Bukit Environment Variables Specification

## 1. Purpose

This document standardizes the environment variables used in Bukit Notion configuration, build commands, and deployment instructions.

AI must not invent Notion environment variable names.

## 2. Standard Variables

| Variable | Required | Description |
|---|---:|---|
| `NOTION_TOKEN` | yes | Notion integration token |
| `NOTION_DATABASE_ID` | single DB mode | Single Notion database |
| `NOTION_PAGES_DATABASE_ID` | multi DB mode | Pages database |
| `NOTION_POSTS_DATABASE_ID` | multi DB mode | Posts database |
| `NOTION_COMPANIES_DATABASE_ID` | multi DB mode | Companies database |
| `NOTION_SERVICES_DATABASE_ID` | multi DB mode | Services database |

## 3. Single-database Mode

```bash
export NOTION_TOKEN="<notion-token>"
export NOTION_DATABASE_ID="<database-id>"
```

## 4. Multi-database Mode

```bash
export NOTION_TOKEN="<notion-token>"
export NOTION_PAGES_DATABASE_ID="<pages-db-id>"
export NOTION_POSTS_DATABASE_ID="<posts-db-id>"
export NOTION_COMPANIES_DATABASE_ID="<companies-db-id>"
export NOTION_SERVICES_DATABASE_ID="<services-db-id>"
```

## 5. Forbidden Names

AI must not use:

```text
PAGES_DB
POSTS_DB
NOTION_PAGE_DB
NOTION_API_KEY
NOTION_SECRET
NOTION_TOKEN_ENV
```

unless the user explicitly requires compatibility with an existing project.

## 6. Security Rules

- Do not write real tokens into files.
- Do not write tokens into `site.yaml`.
- Use `tokenEnv: NOTION_TOKEN`.
- `.env` files must not be included in Demo, theme, site, or import output.
- Never commit secrets.
