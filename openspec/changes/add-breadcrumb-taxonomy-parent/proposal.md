# Add breadcrumb home and taxonomy parent

## Why

silkroute.cc 的文章网址是 `/2596/` 这种一级结构。Bukit 的 BreadcrumbList 只按网址层级找上级，所以 3528 个文章页的结构化面包屑只有文章自己一项，和页面上显示的“首页 › 分类 › 文章”不一致；所有页面也都不含首页。只有一项的面包屑基本不会被搜索引擎采用。

## What Changes

- 新增 `site.seo.schema.breadcrumbHome`（布尔，默认 `false`）：开启后每个 BreadcrumbList 以首页开头，名称取首页路由的标题，没有首页路由时取 `site.title`。
- 新增 `site.seo.schema.breadcrumbParent`（分类法 kind，可选）：内容页如果出现在该分类法的某个分类页（含分页）里，就以该分类的第一页作为上级。文档自身字段里的分类顺序优先，否则取第一个列出它的分类页。分类名称沿用分类页标题（可被 `content.routeMetadata` 覆盖）。
- 网址层级上的上级照旧保留；如果分类页同时也是网址上级，不重复出现。
- 校验：`breadcrumbParent` 为空白时报错。
- 两项任一开启时进入增量构建 SEO 哈希；都不配置时哈希和输出不变。
- YAML 读取、严格字段白名单、JSON Schema、用户文档、CHANGELOG、公共 API 基线同步更新。

## Impact

- 受影响代码：`Bukit.Config`（`SeoSchemaConfig`、`SiteDefaultsApplier`、`ConfigStrictFieldValidator`、`I18nValidator`、`ConfigJsonSchemaGenerator`），`Bukit.Engine`（`BreadcrumbDescriptor`、新增 `BreadcrumbTaxonomyParentResolver`、`Incremental/SeoContributor`）。
- 公共契约：`SeoSchemaConfig` 新增两个属性（新增性变更）。
- 兼容性：默认关闭，不配置时输出不变。

## Non-Goals

- 不改变主题里可见面包屑的渲染。
- 不支持多级分类层级链（只取一个分类页作为上级）。
