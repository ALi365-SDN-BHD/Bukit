# `notion-database-map.yaml` Specification

## 1. Purpose

`notion-database-map.yaml` maps seed files to Notion databases.

It supports:

```text
bukit notion push
automatic Notion database creation
multi-database Notion CMS
content.sources generation
```

## 2. Structure

```yaml
databases:
  pages:
    title: Pages
    databaseId: ""
    seed: pages.json
    collection: page
    uniqueField: Slug
```

## 3. Fields

| Field | Type | Required | Description |
|---|---|---:|---|
| `databases` | object | yes | Database mappings |
| `<key>.title` | string | yes | Notion database title |
| `<key>.databaseId` | string | no | Empty means auto-create is allowed |
| `<key>.seed` | string | yes | Seed file |
| `<key>.collection` | string | yes | Bukit collection |
| `<key>.uniqueField` | string | no | Upsert key, default `Slug` |

## 4. Default Collections

```yaml
databases:
  pages:
    title: Pages
    databaseId: ""
    seed: pages.json
    collection: page
    uniqueField: Slug

  posts:
    title: Posts
    databaseId: ""
    seed: posts.json
    collection: post
    uniqueField: Slug

  companies:
    title: Companies
    databaseId: ""
    seed: companies.json
    collection: company
    uniqueField: Slug

  services:
    title: Services
    databaseId: ""
    seed: services.json
    collection: service
    uniqueField: Slug
```

## 5. Review-only Seed Files

The following seed files are not pushed by default:

```text
sections.json
faqs.json
media.json
components.json
```

If they need to be pushed, define a dedicated schema and update this map explicitly.

## 6. Rules

- `seed` must exist.
- `collection` must match a Bukit collection.
- Empty `databaseId` requires `--create-missing-databases` and `--parent-page-id`.
- `uniqueField` should usually be `Slug`.
- Do not rename `posts` to `articles`.
- Do not rename `companies` to `businesses`.

## 7. Recommended Command

```bash
bukit notion push   --input sites/<site-name>/notion-seed   --database-map sites/<site-name>/notion-seed/notion-database-map.yaml   --create-missing-databases   --parent-page-id <notion-parent-page-id>   --mode upsert   --update-content replace
```
