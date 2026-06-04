# `notion-database-map.yaml` 规范

## 1. 目标

`notion-database-map.yaml` 用于描述 seed 文件与 Notion database 的对应关系。

它服务于：

```text
bukit notion push
自动创建 Notion database
多数据库 Notion CMS
content.sources 生成
```

## 2. 基本结构

```yaml
databases:
  pages:
    title: Pages
    databaseId: ""
    seed: pages.json
    collection: page
    uniqueField: Slug
```

## 3. 字段定义

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `databases` | object | 是 | 数据库映射集合 |
| `<key>.title` | string | 是 | Notion database 名称 |
| `<key>.databaseId` | string | 否 | Notion database ID，空表示可自动创建 |
| `<key>.seed` | string | 是 | seed 文件名 |
| `<key>.collection` | string | 是 | Bukit collection |
| `<key>.uniqueField` | string | 否 | upsert 唯一字段，默认 `Slug` |

## 4. 默认支持集合

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

## 5. review-only seed

以下 seed 默认不进入 Notion push：

```text
sections.json
faqs.json
media.json
components.json
```

## 6. 规则

- `seed` 文件必须存在。
- `collection` 必须与 Bukit collection 一致。
- `databaseId` 为空时必须配合 `--create-missing-databases` 和 `--parent-page-id`。
- `uniqueField` 推荐使用 `Slug`。
- 不要将 `posts` 写成 `articles`。
- 不要将 `companies` 写成 `businesses`。

## 7. 推荐命令

```bash
bukit notion push   --input sites/<site-name>/notion-seed   --database-map sites/<site-name>/notion-seed/notion-database-map.yaml   --create-missing-databases   --parent-page-id <notion-parent-page-id>   --mode upsert   --update-content replace
```
