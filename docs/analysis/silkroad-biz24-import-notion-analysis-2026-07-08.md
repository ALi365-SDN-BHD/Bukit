# silkroad_biz24 HTML Demo 导入 Notion 分析报告

日期：2026-07-08

范围：只读分析 `examples/silkroad_biz24` 如何通过 Bukit Import 生成 Notion seed，并在后续阶段写入 Notion。本文未调用 Notion API，未创建 Notion database，未保存或回显用户提供的 `NOTION_TOKEN`。

## 结论

`examples/silkroad_biz24` 可以作为 Bukit Import 的 HTML demo 输入，但当前目录自带的 `notion-seed/` 不能直接用于当前 Notion push workflow：

1. 当前示例已有完整 demo、Bukit site、theme、content/data 和 `notion-seed/`，不是纯 HTML demo。
2. 当前 `notion-seed/pages.json`、`posts.json`、`companies.json` 只包含 `file` 引用，例如 `{"file":"page-join.md"}`；而当前 push reader 需要 seed record 里有 `title` 或 `name`，否则会跳过记录。
3. 当前示例的 `notion-database-map.yaml` 只声明 `pages/posts/companies` 三个 database；当前代码生成器默认还会写 `navigation/services`，但不会把 `sections/faqs/media/components` 放进默认 Notion push 主链路。
4. 当前代码支持自动创建 Notion databases：必须同时传 `--create-missing-notion-databases` 与 `--notion-parent-page-id <id>`，并通过环境变量提供 `NOTION_TOKEN`。
5. `examples/silkroad_biz24` 没有 `.bukit/plugins.yaml` 和 `plugins/import` 插件包配置；执行正式 `bukit import`/`bukit notion` 命令前，需要先在该示例项目中安装/注册 Import 插件，或在已经注册 Import 插件的项目根中执行。

因此推荐执行顺序是：先补齐/确认 Import 插件注册，再用 `bukit import html-demo` 重新生成当前格式的 seed，然后先用 `bukit notion push --dry-run` 审核计划，最后再执行真实 Notion 写入。

## 示例结构审计

`examples/silkroad_biz24` 当前包含：

- `demo/`：原始 HTML demo，共 10 个 HTML 页面，包含首页、关于、联系、入驻、资讯列表/详情、企业列表/详情、中马企业筛选页。
- `content/`：已导出的 Markdown 内容，含 3 个 page、3 个 post、3 个 company。
- `data/`：已导出的 data 内容，含 3 个 service、3 个 faq。
- `themes/silkroadbiz/`：已生成主题。
- `notion-seed/`：已有 `pages.json`、`posts.json`、`companies.json`、`notion-database-map.yaml`。
- `site.yaml`：当前站点使用 `content.sources`，实际构建来源仍是 Markdown，不是 Notion provider。

HTML demo 中存在 `data-collection="posts"`、`companies`、`services`、`faqs`、`sections` 等标记；这说明 Import 能识别多个内容集合，但默认 Notion push 主链路不是所有集合都可直接写入。

## 当前 seed 问题

当前 `examples/silkroad_biz24/notion-seed/*.json` 的格式是文件引用：

```json
{ "file": "page-join.md" }
```

而当前 push reader 的逻辑是：

- JSON/YAML item 必须是 object。
- 先读 `title`，没有则读 `name`。
- 如果 `title/name` 为空，直接跳过该 item。
- 只会映射 `title/slug/type/summary/content/language/published/seo_title/seo_description` 和额外标量字段。

所以直接执行 push 会出现高风险结果：database 可能被创建，但 records 数量可能为 0，或实际写入内容不符合预期。必须先用当前 Import 重新生成 seed，或把现有 seed 转换为完整字段格式。

## 当前代码能力

### Import 入口

`ImportCommandWorkflow` 支持 `html-demo` 和 `seed` 子命令。对 `html-demo --push-notion` 有这些硬限制：

- `--push-notion` 不能和 `--dry-run` 同用。
- `--push-notion` 不能和 `--no-seed` 同用。
- `--create-missing-notion-databases` 必须同时提供 `--notion-parent-page-id <id>`。

### Seed 生成

当前 `SeedGenerator` 在 `--content-source notion` 下会写入 `notion-seed/`：

- `pages.json`
- `navigation.json`
- `sections.json`
- `posts.json`
- `companies.json`
- `services.json`
- `faqs.json`
- `media.json`
- `components.json`
- `notion-database-map.yaml`

默认 database map 包含：

- `pages`
- `navigation`
- `posts`
- `companies`
- `services`

默认不包含 `sections/faqs/media/components`。其中 `faqs.json` 当前字段是 `question/answer`，默认 reader 不会把它当作可 push record，除非先转换为带 `title/name` 的 Notion seed 格式。

### Notion 自动建库

`ImportNotionPushWorkflow` 支持两种路径：

1. `import html-demo --push-notion ...`
2. `notion push --input notion-seed ...`

自动创建 database 的关键规则：

- map 中 `databaseId` 为空时，如果没有开启自动创建，命令会失败。
- 开启自动创建需要 parent page id。
- 创建 database 时默认 schema 包含 `Title`、`Slug`、`Type`、`Summary`、`Content`、`Language`、`Published`、`SeoTitle`、`SeoDescription`。
- 额外 seed 字段会被转换为 Notion property，例如 bool -> checkbox、number -> number、`url/link/href` -> url，其余默认 rich_text。
- 创建成功后会写出 generated database map；真实 database id 会写入生成的 map。

## 插件注册前置条件

当前 `examples/silkroad_biz24` 未发现：

- `.bukit/plugins.yaml`
- `plugins/import/plugin.yaml`
- `plugins/import/bin/<rid>/bukit-plugin-import`

而 `examples/silkroad_biz23` 和 `src/Bukit-Plugins/Bukit.Plugin.Import/examples/minimal` 有可参考的插件配置。正式执行前需要确保：

- `bukit plugin list` 能看到 `import` 插件。
- 插件暴露 `import` 和 `notion` commands。
- 插件权限包含 `network: true`。
- 插件权限包含 `environment.read: NOTION_TOKEN`。
- Import 插件包的 `plugin.yaml` 和平台二进制存在，并通过 manifest/hash 校验。

## 推荐执行方案

以下命令中的 token 和 parent page id 用占位符表示。不要把 token 写进报告、仓库文件或 shell history 中；执行时用环境变量注入。

### 阶段 0：确认插件可用

在 `examples/silkroad_biz24` 作为项目根执行前，需要先安装/注册 Import 插件。完成后检查：

```bash
cd /Users/ali/mydev/Git/Github/Bukit/examples/silkroad_biz24
bukit plugin list
```

期望看到 `import` 插件，并且暴露 `import`、`notion` 命令。

### 阶段 1：重新生成当前格式 seed

当前已有 seed 是旧/不兼容格式，必须覆盖：

```bash
cd /Users/ali/mydev/Git/Github/Bukit/examples/silkroad_biz24
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --site-path . \
  --force \
  --overwrite \
  --content-source notion \
  --build-source markdown \
  --language zh-CN \
  --route-map ../demo.routes.yaml
```

关键点：

- `--site-path .`：把 site 产物写回当前示例项目根。
- `--force`：允许覆盖主题草稿。
- `--overwrite`：允许覆盖旧 `notion-seed/*.json` 和 `notion-database-map.yaml`。
- `--route-map ../demo.routes.yaml`：route map 解析基准是 `./demo`，所以需要从 demo 目录向上引用。

生成后先检查：

```bash
jq '.[0]' notion-seed/pages.json
jq '.[0]' notion-seed/posts.json
jq '.[0]' notion-seed/companies.json
sed -n '1,160p' notion-seed/notion-database-map.yaml
```

合格 seed 应该直接包含 `title/slug/content` 等字段，而不是只有 `file`。

### 阶段 2：安全预演，不创建 Notion 数据库

`import html-demo --push-notion` 不能 dry-run；安全预演应使用独立的 `notion push --dry-run`：

```bash
cd /Users/ali/mydev/Git/Github/Bukit/examples/silkroad_biz24
bukit notion push \
  --input notion-seed \
  --create-missing-databases \
  --parent-page-id "<parent-page-id>" \
  --dry-run \
  --generated-database-map notion-seed/notion-database-map.generated.yaml \
  --report notion-push-plan.json
```

这个阶段不会调用 Notion API 创建 database。它用于确认会处理哪些 seed 文件和记录数。

### 阶段 3：真实自动建库并写入 Notion

确认 seed 和 dry-run plan 后，再执行真实写入。推荐用分步 push，便于区分 import 生成和 Notion 副作用：

```bash
cd /Users/ali/mydev/Git/Github/Bukit/examples/silkroad_biz24
export NOTION_TOKEN="<token-from-secure-source>"
bukit notion push \
  --input notion-seed \
  --create-missing-databases \
  --parent-page-id "<parent-page-id>" \
  --mode upsert \
  --update-content replace \
  --generated-database-map notion-seed/notion-database-map.generated.yaml \
  --report notion-push-report.json
```

如果必须一条命令完成 import + push，则使用：

```bash
cd /Users/ali/mydev/Git/Github/Bukit/examples/silkroad_biz24
export NOTION_TOKEN="<token-from-secure-source>"
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --site-path . \
  --force \
  --overwrite \
  --content-source notion \
  --build-source markdown \
  --language zh-CN \
  --route-map ../demo.routes.yaml \
  --push-notion \
  --create-missing-notion-databases \
  --notion-parent-page-id "<parent-page-id>" \
  --notion-generated-database-map notion-seed/notion-database-map.generated.yaml \
  --notion-report notion-push-report.json
```

## 仍需人工确认的 Notion 条件

- `NOTION_TOKEN` 对应的 integration 必须有权限访问 parent page。
- parent page id 必须是可作为 database parent 的 Notion page id。
- 如果 Notion workspace 限制 integration 创建 database，需要先在 Notion 侧授权。
- 自动创建后，生成的 `notion-database-map.generated.yaml` 应保存为后续同步依据；后续同步不应每次都创建新 database。

## 范围外或当前不建议直接推送的集合

- `sections`：当前更像页面结构/组件配置，不在默认 Notion push map 中。
- `faqs`：当前 seed 字段是 `question/answer`，reader 要求 `title/name`，默认不会读取为 push record。
- `media`：当前是资源审核清单，不是 Notion 页面内容。
- `components`：当前是模板组件审核清单，不是 Notion 页面内容。

如果这些集合也必须进入 Notion，需要单独设计 seed schema 和 database map，至少要把 records 转换为带 `title/name` 的格式，并确认 Notion property 类型。

## 风险清单

1. 现有 `notion-seed` 格式不兼容当前 push reader；必须覆盖或转换。
2. `examples/silkroad_biz24` 缺少 Import 插件注册；不能假设 `bukit import` 命令在该目录立即可用。
3. `--push-notion` 是真实副作用操作，不能 dry-run；预演要用独立 `bukit notion push --dry-run`。
4. 使用自动创建时，如果保留旧 map 且未 `--overwrite`，可能只创建 `pages/posts/companies` 三个 database，漏掉 `navigation/services`。
5. 一旦真实 push 创建 database，重复执行自动创建可能产生多套 database；第一次成功后应改用 generated map 中的 database id。
6. 本次分析未使用用户提供的 token 做 live smoke，因此尚未验证 token、parent page 权限和 Notion workspace 策略。

## 证据索引

- `examples/silkroad_biz24/site.yaml`：当前示例使用 Markdown `content.sources`，posts/companies/pages 为内容集合，services/faqs 为 data。
- `examples/silkroad_biz24/notion-seed/*.json`：当前 seed 只有 `file` 引用。
- `examples/silkroad_biz24/notion-seed/notion-database-map.yaml`：当前 map 只有 `pages/posts/companies`。
- `src/Bukit-Plugins/Bukit.Importing/ImportCommandWorkflow.cs`：`--push-notion` 参数组合校验和导入后 push 调用。
- `src/Bukit-Plugins/Bukit.Importing/SeedGenerator.cs`：当前 seed 文件和默认 database map 生成逻辑。
- `src/Bukit-Plugins/Bukit.Importing/ImportSeedRecordReader.cs`：push reader 对 `title/name` 的要求。
- `src/Bukit-Plugins/Bukit.Importing/ImportNotionPushWorkflow.cs`：自动建库、schema、report/generated map 写入逻辑。
- `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginCommandSpecs.cs`：插件命令参数和 `network`/`NOTION_TOKEN` 权限。
- `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginOptionsMapper.cs`：插件层参数映射和环境变量授权检查。
- `tests/Bukit.Importing.Tests/ImportNotionPushWorkflowTests.cs`：missing databaseId、dry-run、自动建库、schema 字段测试。
- `tests/Bukit.Importing.Tests/SeedGeneratorTests.cs`：当前 seed/map 生成测试。
