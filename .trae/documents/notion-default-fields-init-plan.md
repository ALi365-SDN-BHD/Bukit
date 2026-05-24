# Notion 初始化默认字段写入计划

## 背景与当前状态

用户问题：当用户在初始化时选择 Notion 作为数据源时，哪些字段会自动作为默认字段写入数据库。

当前代码状态：

1. `bukit init --provider notion` 只会生成 Notion-oriented 的 `site.yaml` 配置。
2. 生成位置在 `src/Bukit.Cli/Commands/InitCommand.cs` 的 `BuildSiteYaml(provider, templateName)` 分支中。
3. 当前生成的 Notion 配置只有：

   * `content.provider: notion`

   * `content.notion.databaseId: xxxxx`
4. 交互式选择 Notion 数据源的流程不在 `bukit init`，而在 `bukit intent init`。
5. `bukit intent init` 选择 Notion 后会询问：

   * `content.notion.database_id`

   * `content.notion.field_policy.mode`

   * `content.notion.field_policy.allowed`
6. 当前仓库没有通过 Notion API 创建或更新 database schema/properties 的代码。
7. 当前 Notion 集成是只读 CMS 集成：读取 database、query pages、读取 page properties、读取 blocks，并映射为 Bukit 内容字段。

因此，当前准确答案是：现在不会自动向 Notion 数据库写入任何默认字段。初始化只写本地配置，不写远程 Notion database schema。

## 需要明确的产品语义

如果要实现“用户初始化时选择 Notion 后自动写入默认字段到数据库”，需要把这个能力定义为一个显式初始化辅助功能，而不是隐式副作用。

推荐语义：

* `bukit init --provider notion` 仍然只生成本地站点骨架和配置。

* 新增可选能力：当用户明确提供 databaseId 并开启 schema 初始化时，Bukit 才会尝试更新 Notion 数据库字段。

* 不应默认静默修改用户的 Notion 数据库。

* 对远程 Notion database schema 的修改必须可预览、可确认、可跳过。

## 默认写入字段建议

### 第一层：最小可运行字段

这些字段用于让 Bukit 能稳定拉取、过滤和生成内容：

| 字段名         | Notion 类型   | 用途                                  | 推荐级别 |
| ----------- | ----------- | ----------------------------------- | ---- |
| `Published` | `checkbox`  | 发布过滤，配合 `filterProperty: Published` | 必需   |
| `Title`     | `title`     | 内容标题                                | 必需   |
| `Slug`      | `rich_text` | URL slug                            | 必需   |
| `Type`      | `select`    | 内容类型，例如 `post` / `page` / `doc`     | 必需   |
| `PublishAt` | `date`      | 发布时间                                | 必需   |

注意：Notion database 必须有且只能有一个 title 类型字段。如果数据库已有其他 title 字段，不能再创建 `Title`，需要识别并复用现有 title 字段。

### 第二层：推荐内容字段

这些字段用于常见内容站点、列表页、模板展示：

| 字段名          | Notion 类型       | 用途  | 推荐级别 |
| ------------ | --------------- | --- | ---- |
| `Summary`    | `rich_text`     | 摘要  | 推荐   |
| `Tags`       | `multi_select`  | 标签  | 推荐   |
| `Categories` | `multi_select`  | 分类  | 推荐   |
| `Cover`      | `files` 或 `url` | 封面图 | 推荐   |

建议默认使用 `Cover` 为 `files`。如果要兼容外链图片，也可以采用 `Cover` 为 `url`，但 Notion 页面自身的 cover 也会被 Bukit 注入为 `cover` 字段，因此需要文档说明优先级。

### 第三层：SEO 与展示字段

这些字段用于主题和 SEO 模板：

| 字段名            | Notion 类型              | 用途     | 推荐级别 |
| -------------- | ---------------------- | ------ | ---- |
| `SEO Title`    | `rich_text`            | SEO 标题 | 可选   |
| `SEO Desc`     | `rich_text`            | SEO 描述 | 可选   |
| `OG Image`     | `files` 或 `url`        | 社交分享图  | 可选   |
| `Author`       | `rich_text` 或 `people` | 作者     | 可选   |
| `Reading Time` | `number`               | 阅读时间   | 可选   |
| `Featured`     | `checkbox`             | 精选内容   | 可选   |
| `Weight`       | `number`               | 排序权重   | 可选   |

### 第四层：多语言字段

仅在站点启用多语言时写入：

| 字段名        | Notion 类型   | 用途                     | 推荐级别  |
| ---------- | ----------- | ---------------------- | ----- |
| `Language` | `select`    | 内容语言，例如 `zh-CN` / `en` | 多语言必需 |
| `i18n_key` | `rich_text` | 多语言内容关联 key            | 多语言必需 |

## 推荐默认字段集合

默认集合建议采用“基础 + 内容 + SEO”的平衡方案：

1. `Published` checkbox
2. `Title` title
3. `Slug` rich\_text
4. `Type` select
5. `PublishAt` date
6. `Summary` rich\_text
7. `Tags` multi\_select
8. `Categories` multi\_select
9. `Cover` files
10. `SEO Title` rich\_text
11. `SEO Desc` rich\_text
12. `OG Image` files
13. `Author` rich\_text
14. `Featured` checkbox
15. `Weight` number

多语言启用时追加：

1. `Language` select
2. `i18n_key` rich\_text

不建议自动写入：

* `source`

* `sourcePath`

* `sourceKey`

* `sourceMode`

* `sourceId`

* `notionPageId`

* `bodyFingerprint`

* `tableOfContents`

* `ContentHtml`

* `Fields`

* `Meta`

* `url`

* `outputPath`

* `template`

这些字段要么由 Bukit 自动生成，要么是内部结构，要么是路由/模板控制字段，不适合作为默认 Notion database 字段自动写入。

## 实现方案

### 步骤 1：新增默认字段模型

新增一个内部模型表达 Notion database property 规格。

建议位置：

* `src/Bukit.Content/Notion/NotionDatabasePropertySpec.cs`

模型职责：

* 字段显示名

* Notion property 类型

* 是否必需

* 是否多语言专用

* 默认 select/multi\_select 选项

* 与 Bukit 语义的说明

建议包含字段：

* `Name`

* `Type`

* `Required`

* `I18nOnly`

* `Options`

### 步骤 2：新增默认字段清单提供器

建议位置：

* `src/Bukit.Content/Notion/NotionDefaultDatabaseSchema.cs`

职责：

* 返回基础默认字段集合

* 根据模板类型补充字段

* 根据是否启用 i18n 补充 `Language` 和 `i18n_key`

* 避免把内部字段加入 schema

建议方法：

* `GetDefaultFields(templateName, includeI18n)`

* `GetMinimalFields()`

* `GetRecommendedFields()`

### 步骤 3：扩展 Notion API client 支持 PATCH

当前 `NotionApiClient` 只有 GET 和 POST。更新 database properties 需要调用 Notion 的 database update API，HTTP 方法是 PATCH。

需要新增：

* `PatchAsync(string url, string json, CancellationToken cancellationToken)`

注意事项：

* 继续复用已有 retry、rate limit、错误处理逻辑。

* 不要在日志中输出 `NOTION_TOKEN`。

* 错误信息可以包含 Notion 返回的安全错误消息，但不得包含 Authorization header。

### 步骤 4：新增 database schema 读取与 diff 逻辑

当前已有 `NotionDatabaseSchemaResolver` 读取 database properties。应复用 GET database 能力，但新增专门的 schema diff 组件。

建议新增：

* `src/Bukit.Content/Notion/NotionDatabaseSchemaPlanner.cs`

职责：

* 读取现有 database properties

* 判断已有字段

* 判断 title 字段是否存在

* 生成待新增字段列表

* 检测类型冲突

* 输出 dry-run 计划

关键规则：

1. 如果数据库已有 title 类型字段，不创建 `Title`，而是记录复用现有 title 字段。
2. 如果同名字段已存在且类型一致，跳过。
3. 如果同名字段已存在但类型不一致，默认不覆盖，报告冲突。
4. 不删除任何现有字段。
5. 不修改现有字段类型。
6. 对 select/multi\_select，可以只在字段不存在时创建默认选项；字段已存在时不强制覆盖选项。

### 步骤 5：新增 schema 应用器

建议新增：

* `src/Bukit.Content/Notion/NotionDatabaseSchemaApplier.cs`

职责：

* 接收 schema diff 结果

* 生成 Notion database update payload

* 调用 `PATCH /v1/databases/{database_id}`

* 返回应用结果

Payload 结构示意：

```json
{
  "properties": {
    "Published": { "checkbox": {} },
    "Slug": { "rich_text": {} },
    "Type": {
      "select": {
        "options": [
          { "name": "post" },
          { "name": "page" },
          { "name": "doc" }
        ]
      }
    },
    "PublishAt": { "date": {} }
  }
}
```

不能为已有 title 字段创建第二个 title 字段。

### 步骤 6：设计 CLI 入口

不建议让普通 `bukit init --provider notion` 默认写远程数据库。

推荐新增显式选项：

```bash
bukit init my-site --provider notion --notion-database-id xxx --init-notion-schema
```

或新增独立命令：

```bash
bukit notion schema init --database-id xxx
bukit notion schema plan --database-id xxx
```

更推荐独立命令，因为它把“创建本地站点”和“修改远程 Notion 数据库”分离。

建议第一阶段实现独立命令：

1. `bukit notion schema plan --database-id xxx`
2. `bukit notion schema apply --database-id xxx`

可选参数：

* `--template minimal|blog|docs|landing|portfolio`

* `--i18n`

* `--recommended`

* `--minimal`

* `--yes`

### 步骤 7：连接 init / intent 流程

在实现独立命令后，再考虑把提示接入初始化流程。

`bukit init --provider notion` 完成后输出下一步提示：

```text
Next for Notion:
  export NOTION_TOKEN=secret_xxx
  bukit notion schema plan --database-id xxxxx
  bukit notion schema apply --database-id xxxxx --yes
```

`bukit intent init` 中选择 Notion 后，可以增加一个问题：

* 是否生成 Notion schema 初始化建议

但不应直接写数据库，除非用户明确确认。

### 步骤 8：更新 site.yaml 默认 Notion 配置

`InitCommand.BuildSiteYaml(provider: notion)` 当前只写 `databaseId`。建议补充：

```yaml
content:
  provider: notion
  notion:
    databaseId: xxxxx
    filterProperty: Published
    filterType: checkbox_true
    fieldPolicy:
      mode: whitelist
      allowed:
        - Summary
        - Tags
        - Categories
        - Cover
        - SEO Title
        - SEO Desc
        - OG Image
        - Author
        - Featured
        - Weight
```

如果使用归一化字段名作为 allowed，则需要先确认当前 `fieldPolicy.allowed` 比较的是原始 Notion 名还是归一化后的 key。代码实现前必须用测试确认。根据现有交互式默认值，当前 intent 默认 allowed 使用归一化字段：`cover`, `seo_title`, `seo_desc`, `tags`, `categories`, `og_image`, `i18n_key`, `language`。因此应优先保持归一化形式，避免破坏现有行为。

推荐写入：

```yaml
fieldPolicy:
  mode: whitelist
  allowed:
    - summary
    - tags
    - categories
    - cover
    - seo_title
    - seo_desc
    - og_image
    - author
    - featured
    - weight
```

多语言时追加：

```yaml
    - language
    - i18n_key
```

### 步骤 9：更新测试

需要新增/更新测试：

1. `NotionApiClient` PATCH 方法测试。
2. 默认 schema 字段清单测试。
3. schema planner：空 database 时计划创建默认字段。
4. schema planner：已有同名同类型字段时跳过。
5. schema planner：已有同名不同类型字段时报冲突。
6. schema planner：已有 title 字段但不叫 `Title` 时复用，不创建第二个 title。
7. schema applier：生成正确 PATCH payload。
8. CLI `notion schema plan` 输出测试。
9. CLI `notion schema apply` dry-run/yes 行为测试。
10. `init --provider notion` 生成更完整 `site.yaml` 的回归测试。
11. `intent init` 默认 allowed 字段测试。

### 步骤 10：更新文档

需要更新：

* `guide/user/06-notion-content.zh-CN.md`

* `guide/dev/content.zh-CN.md`

* `guide/user/12-cli-reference.md`

* `guide/dev/init-create.md`

文档应说明：

1. 初始化选择 Notion 默认不会修改远程数据库。
2. 推荐的 Notion database 字段列表。
3. 如何使用 schema plan/apply 命令。
4. 字段名如何归一化为 `page.fields.*`。
5. `fieldPolicy.allowed` 应使用哪些字段名。
6. 哪些字段不应该写入 Notion。

## 验证计划

实现后需要运行：

1. CLI 测试。
2. Content Notion 测试。
3. Config 测试。
4. 全量测试。
5. lint/typecheck/build 命令按项目规则执行。

执行前需要读取项目现有测试命令和项目规则，避免假设命令名称。

## 阶段性落地建议

### 第一阶段：不写远程，只完善初始化配置与文档

目标：低风险快速改进。

内容：

1. `init --provider notion` 生成更完整的 `site.yaml`。
2. `intent init` 的默认 allowed 字段与推荐字段统一。
3. 文档明确推荐字段和当前不会自动写数据库。
4. 更新测试。

### 第二阶段：新增 schema plan，只读预览

目标：让用户知道数据库缺哪些字段，但不修改远程。

内容：

1. 读取 Notion database properties。
2. 生成缺失字段和冲突字段报告。
3. CLI 输出 plan。
4. 测试覆盖。

### 第三阶段：新增 schema apply，显式写入

目标：在用户明确执行 apply 时创建缺失字段。

内容：

1. `PatchAsync`。
2. schema applier。
3. `--yes` 确认机制。
4. 不覆盖、不删除、不改类型。
5. 测试和文档。

## 推荐先实施的最小变更

如果本次只想解决“初始化选择 Notion 时默认有哪些字段”的问题，建议只做第一阶段：

1. 明确当前行为：不会写数据库。
2. 在 Notion 初始化生成的 `site.yaml` 中补充推荐 `fieldPolicy.allowed`。
3. 在文档中新增“推荐 Notion 数据库字段”表。
4. CLI 初始化输出提示用户手动创建这些字段。

推荐默认字段为：

* `Published` checkbox

* `Title` title

* `Slug` rich\_text

* `Type` select

* `PublishAt` date

* `Summary` rich\_text

* `Tags` multi\_select

* `Categories` multi\_select

* `Cover` files

* `SEO Title` rich\_text

* `SEO Desc` rich\_text

* `OG Image` files

* `Author` rich\_text

* `Featured` checkbox

* `Weight` number

多语言站点追加：

* `Language` select

* `i18n_key` rich\_text

