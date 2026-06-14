# Bukit `site.yaml` Standard Profiles

AI must not freely assemble `site.yaml`. It must choose one of the following Profiles.

## Profile A: Markdown Local Preview

Use this after Demo conversion for local validation.

```yaml
site:
  title: <site-title>
  baseUrl: https://example.com
  language: en

content:
  sources:
    - name: pages
      mode: content
      collection: page
      markdown:
        dir: content

build:
  output: dist
  clean: true

theme:
  name: <theme-name>
```

## Profile B: Notion Single-database Mode

Use this for small websites or early CMS integration.

```yaml
site:
  title: <site-title>
  baseUrl: https://example.com
  language: en

content:
  sources:
    - name: pages
      mode: content
      collection: page
      notion:
        databaseId: ${NOTION_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

build:
  output: dist
  clean: true

theme:
  name: <theme-name>
```

## Profile C: Notion Multi-database Mode

Use this for production CMS websites.

```yaml
site:
  title: <site-title>
  baseUrl: https://example.com
  language: en

content:
  sources:
    - name: pages
      mode: content
      collection: page
      notion:
        databaseId: ${NOTION_PAGES_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

    - name: posts
      mode: content
      collection: post
      notion:
        databaseId: ${NOTION_POSTS_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

    - name: companies
      mode: content
      collection: company
      notion:
        databaseId: ${NOTION_COMPANIES_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

    - name: services
      mode: content
      collection: service
      notion:
        databaseId: ${NOTION_SERVICES_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

build:
  output: dist
  clean: true

theme:
  name: <theme-name>
```

## Profile D: JSON/YAML Seed + Markdown Build Mode

Use this when the project exports JSON/YAML data but still builds from Markdown.

```yaml
site:
  title: <site-title>
  baseUrl: https://example.com
  language: en

content:
  sources:
    - name: pages
      mode: content
      collection: page
      markdown:
        dir: content

build:
  output: dist
  clean: true

theme:
  name: <theme-name>
```

## Selection Rules

| User Need | Profile |
|---|---|
| Local preview first | Profile A |
| Small site, one Notion database | Profile B |
| Production CMS, multiple content collections | Profile C |
| No Notion, data export only | Profile D |

## AI Must Declare Before Output

```text
Selected Profile:
Reason:
content-source:
build-source:
Uses Notion:
Uses multiple databases:
Expected configuration type:
```
