# Trae Prompt：Bukit Skills v3.1 Code-Aligned Beta 修复计划

> 适用场景：将本 prompt 直接复制到 Trae，用于修复和优化 Bukit 仓库中的 `src/skills` Agent Knowledge System。\
> 核心原则：**不要全部重写 skills，而是在现有架构基础上做代码事实源对齐重构。**

***

## 一、任务背景

你现在负责修复和优化 Bukit 仓库中的 `src/skills` Agent Knowledge System。

目标不是全部重写 skills，而是在现有架构基础上做一次 **“代码事实源对齐重构”**，将当前 Bukit Skills 从 `v3.0 Beta` 推进到 `v3.1 Code-Aligned Beta`。

请严格基于当前仓库真实代码、CLI 注册表、配置模型、已有 user guide 和现有 skills 修改，不要凭想象补功能，不要把 planned 功能写成 stable。

***

## 二、总体目标

当前 `src/skills` 已经有较好的基础，包括：

- `using-bukit` 网关 skill
- `bukit-cli-reference` CLI 事实源
- `bukit-config` 配置模型说明
- 各领域 skill
- `skills-index.yaml`
- `skills-index.json`
- `plugin.json`
- `README.md`
- `MAINTENANCE.md`
- `QUALITY_REPORT.md`
- `validate-skills.sh`
- `validate-skills-strict.sh`

  <br />

本次不要推倒重写，而是完成以下核心目标：

1. 统一 skills 数量与索引事实源。
2. 将代码中已存在的 CLI 命令完整同步到 `bukit-cli-reference`。
3. 修复 Notion / content 配置模型与 Bukit 1.0 代码不一致的问题。
4. 增强验证脚本，尤其是 CLI 参数级校验、JSON 深度同步、plugin 路径同步。
5. 将 `src/skills` 质量提升到可公开 Beta / Public Preview 水平。
6. 为后续 Stable 做好自动化验证基础。

***

## 三、必须先审计的代码与文档

修改前请完整阅读并理解这些文件。

### 3.1 核心 skills

- `src/skills/README.md`
- `src/skills/using-bukit/SKILL.md`
- `src/skills/bukit-cli-reference/SKILL.md`
- `src/skills/bukit-config/SKILL.md`
- `src/skills/bukit-notion/SKILL.md`
- `src/skills/bukit-import/SKILL.md`
- `src/skills/theme-component-system/SKILL.md`
- `src/skills/skills-index.yaml`
- `src/skills/skills-index.json`
- `src/skills/plugin.json`
- `src/skills/MAINTENANCE.md`
- `src/skills/QUALITY_REPORT.md`

### 3.2 平台入口文件

- `src/skills/AGENTS.md`
- `src/skills/CLAUDE.md`
- `src/skills/GEMINI.md`
- `src/skills/copilot-instructions.md`

  <br />

### 3.3 验证脚本

- `src/skills/scripts/validate-skills.sh`
- `src/skills/scripts/validate-skills-strict.sh`
- `src/skills/scripts/check-cli-commands.py`
- `src/skills/scripts/check-markdown-tables.py`
- `src/skills/scripts/check-yaml-examples.py`
- `src/skills/scripts/check-status-consistency.py`
- `src/skills/scripts/check-status-keywords.py`
- `src/skills/scripts/generate-index-json.sh`

### 3.4 CLI / 配置事实源

- `src/Bukit.Cli/Cli/BukitCliSpecs.cs`
- `src/Bukit.Cli/Cli/BukitCliThemeSpecs.cs`
- `src/Bukit.Cli/Cli/BukitCliDescriptors.cs`
- `src/Bukit.Cli/Commands/ImportCommand.cs`
- `src/Bukit.Cli/Commands/NotionCommand.cs`
- `src/Bukit.Cli/Commands/PublishCommand.cs`
- `src/Bukit.Cli/Commands/ThemeCommand.cs`
- `src/Bukit.Cli/Commands/SeoCommand.cs`
- `src/Bukit.Cli/Commands/GeoCommand.cs`
- `src/Bukit.Config/AppConfig.cs`
- `src/Bukit.Engine/ContentProviderFactory.cs`
- `src/Bukit.Theme/ThemeManifestLoader.cs`
- `src/Bukit.Theme/ThemeComponentRegistry.cs`

### 3.5 User guide

- `guide/user/06-notion-content.md`
- `guide/user/12-cli-reference.md`
- `guide/user/16-parameter-cheatsheet.md`
- `guide/user/17-geo.md`
- `guide/user/18-clone-website.md`
- `guide/user/21-import-html-demo.md`

***

## 四、P0 修复任务：统一 skills 事实源

当前可能存在以下分裂：

- `skills-index.yaml` 仍是 19 skills
- `skills-index.json` 可能是 20 skills
- `plugin.json` 可能仍是 19 skills
- 实际目录中已经有 `src/skills/bukit-import/SKILL.md`
- 代码中已经有 `import` CLI 命令

请正式纳入 `bukit-import`，不要删除它。

### 4.1 更新 `src/skills/skills-index.yaml`

要求：

- `skill_count` 改为 `20`
- `updated` 日期更新为当前修改日期
- 增加 `bukit-import` 条目
- priority 顺序合理
- `requires` 建议为：
  - `bukit-cli-reference`
  - `bukit-theme`
  - `bukit-templating`
- `guide_chapters` 包含：
  - `guide/user/21-import-html-demo.md`
  - `guide/user/12-cli-reference.md`
- `source_anchors` 包含：
  - `src/Bukit.Cli/Commands/ImportCommand.cs`
  - `src/Bukit.Importing/HtmlDemoImporter.cs`
  - `src/Bukit.Importing/ImportReportWriter.cs`

### 4.2 更新 `src/skills/skills-index.json`

要求：

- 必须由 `skills-index.yaml` 重新生成
- 不允许手工编辑造成漂移

### 4.3 更新 `src/skills/plugin.json`

要求：

- `skills` 列表加入：
  - `bukit-import/SKILL.md`
- `description` 从 `19 specialized skills` 改为 `20 specialized skills`
- `version` 与 `skills-index.yaml` 保持一致

### 4.4 更新 `src/skills/README.md`

要求：

- Directory Layout 加入 `bukit-import`
- Skill Responsibilities 加入 `bukit-import`
- Usage Guide 中 `all 19 Bukit skills` 改为 `20`
- Suggested Reading Paths 加入 HTML demo import / import seed 相关路径

### 4.5 更新 `src/skills/using-bukit/SKILL.md`

要求：

- Skill overview 表加入 `bukit-import`
- Workflow routing 加入 `import_html_demo`
- 明确 `bukit-import` 与 `bukit-clone` 的区别：
  - `bukit-clone`：从 live URL / Browser MCP 抽取
  - `bukit-import`：从本地 HTML demo 目录转换

### 4.6 同步平台入口文件

需要同步：

- `src/skills/AGENTS.md`
- `src/skills/CLAUDE.md`
- `src/skills/GEMINI.md`
- `src/skills/copilot-instructions.md`

### 4.7 更新 `QUALITY_REPORT.md`

要求：

- 更新为 20 skills
- 记录 `bukit-import` 已正式纳入
- 移除已经 fixed 的 stale risks
- 新增剩余风险：CLI option-level validation 尚需进一步加强，如果本次已完成则标 fixed

***

## 五、P0 修复任务：重构 `bukit-cli-reference`

`bukit-cli-reference` 是所有命令执行的单一事实源。必须与当前 CLI 代码一致。

请从这些文件核对命令：

- `src/Bukit.Cli/Cli/BukitCliSpecs.cs`
- `src/Bukit.Cli/Cli/BukitCliThemeSpecs.cs`
- `src/Bukit.Cli/Cli/BukitCliDescriptors.cs`

### 5.1 必须补齐 `import` 命令组

在 Command Quick Reference 加入：

- `import html-demo`
- `import seed`

`import html-demo` 关键参数：

- `<demo-dir>`
- `--theme`
- `--force`
- `--use`
- `--verify`
- `--extract-content`
- `--no-extract-content`
- `--generate-seed`
- `--no-seed`
- `--content-source`
- `--build-source`
- `--route-map`
- `--site-path`
- `--language`
- `--dry-run`
- `--strict`
- `--overwrite`
- `--preserve-html`
- `--no-preserve-html`
- `--report`
- `--no-report`
- `--base-url`
- `--push-notion`
- `--notion-database-id`
- `--notion-database-map`
- `--create-missing-notion-databases`
- `--notion-parent-page-id`
- `--notion-generated-database-map`
- `--notion-token-env`
- `--notion-report`
- `--no-validate-notion-schema`
- `--config`
- `--site`

`import seed` 关键参数：

- `<seed-dir>`
- `--output`
- `--force`
- `--config`
- `--site`

### 5.2 必须补齐 `notion` 命令组

加入：

- `notion push`
- `notion validate-schema`

`notion push` 参数至少包含：

- `--input`
- `--database-id`
- `--database-map`
- `--create-missing-databases`
- `--parent-page-id`
- `--generated-database-map`
- `--dry-run`
- `--report`
- `--token-env`
- `--mode`
- `--unique-field`
- `--update-content`
- `--no-validate-schema`

`notion validate-schema` 参数至少包含：

- `--database-id`
- `--token-env`
- `--report`

### 5.3 必须补齐 `publish` 命令组

加入：

- `publish audit`
- `publish diff`

`publish audit` 参数：

- `--dir`
- `--report`
- `--strict`
- `--external`

`publish diff` 参数：

- `--allow-cross-schema`
- `--baseline`
- `--current`
- `--max-new-errors`
- `--max-new-warnings`
- `--max-new-issues`
- `--fail-on-new-code`
- `--fail-on-route-removed`
- `--fail-on-indexable-drop`

### 5.4 修复已有漏项

`theme pack` 必须包含：

- `--output`
- `--config`
- `--site`

`theme install` 必须包含：

- `--registry`
- `--registry-url`
- `--force`
- `--config`
- `--site`

`theme wizard` 必须包含：

- `--template`
- `--preset`
- `--use`
- `--force`
- `--config`
- `--site`

`clone` 必须包含：

- `--template`

`seo diff` 必须包含：

- `--allow-cross-schema`

### 5.5 对 planned / beta / experimental 明确标记

如果某些命令存在 handler 但未在 `BukitCliSpecs` 中注册，例如：

- `theme doctor`
- `theme list-components`
- `theme export-catalog`

则在 `theme-component-system` 中继续标记 planned / not available in CLI，除非你同时把它们注册到 `BukitCliSpecs`。

***

## 六、P0 修复任务：修复 `bukit-notion` 配置模型

当前 Bukit 1.0 代码要求 `content.sources[]`，不再支持 root-level `content.notion` / `content.markdown` / `content.provider`。

请修改 `src/skills/bukit-notion/SKILL.md`：

1. 将所有 `content.notion` 改为 `content.sources[].notion`
2. 将所有 `content.media` 保留为 `content.media`，因为 media 仍在 content 下
3. 增加 Bukit 1.0 正确示例：

```yaml
content:
  sources:
    - type: notion
      mode: content
      collection: post
      notion:
        databaseId: "..."
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
  media:
    downloadToLocal: true
    downloadDir: assets/uploads
    urlBase: /assets/uploads
```

1. 在 Common Issues 增加：
   - 使用旧配置 `content.notion` 导致 build/config check 失败
   - 修复方式：改为 `content.sources[]`
2. Notion skill 中如果出现 `content.provider` 或旧式 notion root config，也要移除或标记为 deprecated。

***

## 七、P1 修复任务：同步 `bukit-config` 到当前 `AppConfig`

检查 `src/skills/bukit-config/SKILL.md` 是否与 `src/Bukit.Config/AppConfig.cs` 一致。

### 7.1 content 节点

当前代码 `ContentConfig` 包含：

- `Sources`
- `Media`
- `ModelSchema`

所以 `bukit-config` 必须说明：

```yaml
content:
  sources:
    - type: markdown|notion
      mode: content|data
      name: ...
      collection: ...
      addToCollections: [...]
      markdown:
        ...
      notion:
        ...
  media:
    ...
  modelSchema:
    ...
```

### 7.2 content.modelSchema

代码已有 `ContentModelSchemaConfig`，包括：

- `contentTypes`
- `statuses`
- `reviewStatuses`
- `syncStatuses`
- `canonicalMappings`
- `customFields`
- `fieldScopes`
- `entityMappings`
- `relationMappings`
- `media`
- `rejectUnknownRawKeys`
- `requireSummary`
- `requireAuthor`
- `requireOrganization`
- `requireUpdatedAt`
- `requireProvenance`
- `requireReviewedAt`
- `requireMediaAlt`
- `requireMediaDescription`
- `requireMediaLicense`
- `requireEntityIds`
- `requireRelationTargets`

请在 `bukit-config` 增加一个简明 reference section，不需要写满长篇，但必须让 Agent 知道这些字段存在。

### 7.3 externalPlugins

代码里 `ExternalPluginConfig` 已增加：

- `templateRequirements`

请在 `bukit-config` externalPlugins 表中补充 `templateRequirements`。

### 7.4 collection schema

如果 `AppConfig` 中 `CollectionConfig` 已不再包含 `Schema` 字段，请不要在 `bukit-config` 中继续描述 `site.collections.<key>.schema` 作为当前代码字段。若其他地方仍有 schema 概念，请基于真实代码重新定位。

***

## 八、P1 修复任务：增强验证脚本

当前 `validate-skills-strict.sh` 已经不错，但仍需增强。

### 8.1 `skills-index.json` 深度同步

当前不能只比较 `skill_count` 和 skill names。必须完整比较 YAML 和 JSON 内容。

要求：

- YAML load 后的数据结构与 JSON load 后的数据结构完全一致
- 不一致时输出 error
- `validate-skills-strict.sh` 必须失败

### 8.2 `plugin.json` 深度同步

当前不能只比较 skill name。

必须校验：

- `plugin.json version == skills-index.yaml version`
- `plugin.json skills 数量 == skill_count`
- `plugin.json skills 路径集合 == skills-index.yaml 中 path 去掉 src/skills/ 前缀后的集合`
- 推荐也校验顺序一致

### 8.3 CLI option-level validation

当前 `check-cli-commands.py` 只检查 command path。请升级为 command path + options 校验。

最低要求：

- 从 `BukitCliSpecs.cs` / `BukitCliThemeSpecs.cs` 提取 command path 和 option names
- 从 `bukit-cli-reference` 的 Command Quick Reference 提取 command path 和 Key Parameters
- 对比每个命令的 option names
- source 有但 reference 缺失 → error
- reference 有但 source 不存在 → error
- 对 `<arg>` positional argument 可单独记录，但不要误判为 option
- planned command 白名单可保留

需要至少能抓出这类问题：

- `theme pack` 缺 `--output`
- `theme install` 缺 `--registry-url`
- `import html-demo` 缺 `--push-notion`
- `notion push` 缺 `--mode`
- `publish diff` 缺 `--allow-cross-schema`

### 8.4 source/ref 文件不存在时必须失败

`check-cli-commands.py` 中：

- `specs_path` 不存在 → exit 1
- `ref_path` 不存在 → exit 1

不要 warning 后 exit 0。

### 8.5 扩展 Markdown/YAML 检查范围

目前 checker 可能只扫描 `*/SKILL.md`。请扩展到：

- `src/skills/README.md`
- `src/skills/QUALITY_REPORT.md`
- `src/skills/MAINTENANCE.md`
- `src/skills/AGENTS.md`
- `src/skills/CLAUDE.md`
- `src/skills/GEMINI.md`
- `src/skills/copilot-instructions.md`
- `src/skills/*/SKILL.md`

### 8.6 保持 PyYAML 缺失时失败

除非显式设置：

```bash
ALLOW_SKIP_YAML_VALIDATION=1
```

否则 `check-yaml-examples.py` 不允许跳过。

***

## 九、P1 修复任务：`theme-component-system` 对齐代码状态

`src/skills/theme-component-system/SKILL.md` 不需要重写，但要增强 source anchors 和能力状态。

### 9.1 Front Matter 增加 source anchors

- `src/Bukit.Cli/Commands/ThemeCommand.cs`
- `src/Bukit.Theme/ThemeManifestLoader.cs`
- `src/Bukit.Theme/ThemeComponentRegistry.cs`

### 9.2 Capability Status 明确状态

- `theme.yaml V2 manifest parsing`: beta
- `section/component registry`: beta
- `theme inheritance chains`: beta
- `theme-catalog.json export`: planned 或 beta，取决于是否注册到 CLI
- `Page Composer`: planned
- `section schema validation`: beta 或 planned，必须根据代码确认

### 9.3 theme component CLI 命令状态

对这些命令：

- `theme doctor`
- `theme list-components`
- `theme export-catalog`

如果不注册到 `BukitCliSpecs`，则保持 planned，并明确：

```text
Handler exists in ThemeCommand, but command is not registered in BukitCliSpecs, so it is not considered public CLI yet.
```

如果注册到 `BukitCliSpecs`，则把 planned 改成 beta，并更新 `bukit-cli-reference`。

***

## 十、P1 修复任务：修正 `bukit-content-to-template` 中 IContentStage 表述

如果 `src/skills/bukit-content-to-template/SKILL.md` 写了：

```text
Plugin developers can inject custom stages by implementing IContentStage
```

请确认是否存在公开插件注册机制能让外部插件注入 `IContentStage`。

如果没有公开机制，请改成更准确的说法：

```text
Bukit internally models content loading as IContentStage. Engine-level contributors can add stages in code. External plugin injection for content stages should only be described if a public registration mechanism exists.
```

这样避免 Agent 误以为用户可以通过 `site.yaml` 或外部 plugin 注入 content stage。

***

## 十一、最终验证命令

修改后必须运行：

```bash
bash src/skills/scripts/generate-index-json.sh
bash src/skills/scripts/validate-skills.sh
bash src/skills/scripts/validate-skills-strict.sh
```

然后运行：

```bash
dotnet build bukit.slnx
```

如果可行，继续运行：

```bash
dotnet test bukit.slnx
```

如果项目质量门禁可运行，则运行：

```bash
bash scripts/quality-gate.sh
```

如果某些测试因为环境缺失无法运行，请在最终报告中说明具体原因，不要假装通过。

***

## 十二、最终交付报告格式

完成后输出：

```markdown
# Bukit Skills v3.1 Code-Aligned Beta 修复报告

## 1. Summary
说明本次修复目标和最终状态。

## 2. Skills Count & Index Sync
说明当前 skill_count、是否纳入 bukit-import、哪些索引已同步。

## 3. CLI Reference Alignment
列出新增/修复的 CLI 命令和参数，包括：
- import html-demo
- import seed
- notion push
- notion validate-schema
- publish audit
- publish diff
- theme pack --output
- theme install --registry-url

## 4. Config / Notion Alignment
说明 content.sources[]、content.modelSchema、externalPlugins.templateRequirements 等是否已同步。

## 5. Validator Improvements
说明新增或增强了哪些验证：
- JSON 深度同步
- plugin.json path/version/order 校验
- CLI option-level validation
- expanded Markdown/YAML scans

## 6. Files Changed
列出修改文件。

## 7. Validation Results
列出实际运行的命令和结果。

## 8. Remaining Risks
列出仍未解决但可接受的风险，例如：
- CLI metadata 仍通过源码解析而非 CLI 自生成 JSON
- theme component system 仍是 beta
- platform entry files 仍为手工维护

## 9. Recommended Next Steps
给出后续进入 Stable 的建议。
```

***

## 十三、严格约束

1. 不要全部重写 skills。
2. 不要删除现有可用结构。
3. 不要把 planned 功能写成 stable。
4. 不要凭想象补 CLI 命令或配置字段。
5. 一切以当前代码为事实源。
6. 修改 skills 后必须同步索引、plugin、README、平台入口。
7. 如果发现代码和 skill 冲突，优先相信代码，并在 `QUALITY_REPORT.md` 记录。
8. 如果需要修改代码才能暴露某个命令，先判断是否真的应该公开；不确定时保留 planned。
9. 所有示例 YAML 必须可解析，并且尽量符合 `AppConfig` 当前模型。
10. 最终目标是让 AI Agent 准确操作 Bukit，而不是让文档看起来更完整。

***

# 修复计划书摘要

## 推荐路线

采用：

```text
保留现有架构 + 关键 skill 重构 + 校验脚本升级
```

不采用：

```text
全部推倒重写
```

***

## 第一阶段：P0 修复

| 任务                               | 文件                                                                    |
| -------------------------------- | --------------------------------------------------------------------- |
| 统一 20 skills，正式纳入 `bukit-import` | `skills-index.yaml`, `skills-index.json`, `plugin.json`, README, 平台入口 |
| 补齐 CLI reference                 | `bukit-cli-reference/SKILL.md`                                        |
| 修复 Notion 配置路径                   | `bukit-notion/SKILL.md`                                               |
| 修复 CLI 参数漏项                      | `bukit-cli-reference/SKILL.md`                                        |
| 更新质量报告                           | `QUALITY_REPORT.md`                                                   |

***

## 第二阶段：P1 校验增强

| 任务                           | 文件                                                     |
| ---------------------------- | ------------------------------------------------------ |
| JSON 深度同步                    | `validate-skills-strict.sh`                            |
| plugin path/version/order 校验 | `validate-skills-strict.sh`                            |
| CLI option-level 校验          | `check-cli-commands.py`                                |
| 扩展 Markdown/YAML 检查范围        | `check-markdown-tables.py`, `check-yaml-examples.py`   |
| 更新 source anchors            | `theme-component-system/SKILL.md`, `skills-index.yaml` |

***

## 第三阶段：Stable 前优化

| 任务                      | 目标                       |
| ----------------------- | ------------------------ |
| CLI 自生成 metadata        | 取代正则解析 C#                |
| 平台入口自动生成                | 减少手工同步                   |
| capability-level status | 精细区分 beta/planned/stable |
| config field 自动校验       | 防止 AppConfig 与 skill 漂移  |

***

## 最终建议

这份 prompt 适合直接交给 Trae 执行。

任务标题可以写成：

```text
Bukit Skills v3.1 Code-Aligned Beta 修复：同步代码事实源、CLI、配置模型和 Agent skills
```

核心原则是：

```text
不要重写全部，按代码事实源修复现有 skills。
```

这样成本最低、风险最小，也最符合当前 Bukit skills 的实际状态。
