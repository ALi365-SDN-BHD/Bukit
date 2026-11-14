# Add a site-level default author for SEO

## Why

silkroute.cc 迁移后，4650 个文章页的结构化数据都没有 `author`：Notion 数据库没有作者字段，文章也没有作者关联。站点的所有内容都由同一个机构发布，但目前只能逐篇填写作者。

## What Changes

- 新增 `site.seo.defaultAuthor`（字符串，可选）和 `site.seo.defaultAuthorType`（`Person` | `Organization`，可选，默认 `Person`，大小写不敏感）。
- 作者解析顺序不变，默认作者只作为最后一级回退：作者关联（authoredBy）→ 内容 `author` 字段 → GEO 作者 → `site.seo.defaultAuthor`。
- 回退得到的作者与内容作者一样用于文章 JSON-LD 的 `author` 和 SEO 模型的 `author`/`author_type`。
- 校验：`defaultAuthor` 为空白时报错；只写 `defaultAuthorType` 不写 `defaultAuthor` 时报错；类型只接受 Person、Organization。
- 配置了默认作者时，两个字段进入增量构建的 SEO 哈希，修改后页面会重新渲染；不配置时哈希不变，已有站点不会因升级而全量重渲染。
- YAML 读取、严格字段白名单、JSON Schema、用户与开发文档、CHANGELOG、公共 API 基线同步更新。

## Impact

- 受影响代码：`Bukit.Config`（`SeoConfig`、`SiteDefaultsApplier`、`ConfigStrictFieldValidator`、`I18nValidator`、`ConfigJsonSchemaGenerator`），`Bukit.Engine`（`SeoAuthorResolver`、`SeoModelBuilder`、`SeoJsonLdBuilder`、`Incremental/SeoContributor`）。
- 公共契约：`SeoConfig` 新增两个属性（新增性变更）；公共 API 基线随之更新。
- 兼容性：不配置时输出不变。

## Non-Goals

- 不把默认作者写入内容模型（`Ownership.Author`）、feed 或 llms 输出；只影响 SEO 输出。
- 不支持默认作者的 url、image、sameAs。
