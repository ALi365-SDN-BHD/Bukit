# 可观测性（日志与 metrics）

本项目的可观测性由两部分组成：

- 日志：控制台输出（text/json）
- metrics：构建结束后输出的结构化 JSON（可用于 CI 采集、增量原因分析、插件耗时分析）

相关入口：
- 日志：`src/Bukit.Shared/Logger.cs`
- 传参：`src/Bukit.Cli/Commands/BuildCommand.cs`
- metrics 输出：`src/Bukit.Engine/SiteEngine.cs`
- 诊断码：`src/Bukit.Shared/DiagnosticCode.cs`

## 日志（--log-format 与日志等级）

### log-format

`bukit build` 支持：
- `--log-format text`（默认）
- `--log-format json`

json 日志格式（每行一条 JSON，输出到 stderr）：

```json
{"ts":"2026-01-01T00:00:00.0000000+00:00","level":"Info","msg":"event=build.start ..."}
```

字段：
- `ts`：UTC 时间戳（ISO 8601）
- `level`：Debug/Info/Warn/Error
- `msg`：原始消息（通常包含 `event=...` 与 key=value）

### 日志等级策略

日志最小等级来自 `site.yaml` 的 `logging.level`，但在 CI 模式下会强制提到 Warn：
- 非 CI：debug/info/warn/error
- CI：`--ci` 会使最小等级固定为 Warn（减少噪音，利于失败定位）

日志来源：`Bukit.Engine`、`Bukit.Content`、`Bukit.Cli`。

## 诊断码（BKT-XXXX）

所有 Bukit 异常携带稳定的 `BKT-XXXX` 十六进制诊断码。实现：`src/Bukit.Shared/DiagnosticCode.cs`、`src/Bukit.Shared/DiagnosticCodeFormatter.cs`、`src/Bukit.Shared/DiagnosticExceptionFormatter.cs`。

| 类别 | 范围 | 示例 |
|---|---|---|
| 配置 | `BKT-0001` – `BKT-00FF` | `BKT-0001` RequiredFieldMissing |
| 主题 | `BKT-0101` – `BKT-01FF` | `BKT-0101` ManifestInvalid |
| 路由 | `BKT-0201` – `BKT-02FF` | `BKT-0201` RouteConflict |
| 渲染 | `BKT-0301` – `BKT-03FF` | `BKT-0301` TemplateNotFound |
| Schema | `BKT-0401` – `BKT-04FF` | `BKT-0402` StrictModeBlocked |
| 内容 | `BKT-0501` – `BKT-05FF` | `BKT-0501` LoadFailed |
| 构建 | `BKT-0601` – `BKT-06FF` | `BKT-0601` OutputUnsafe |
| 插件 | `BKT-0701` – `BKT-07FF` | `BKT-0701` ExecutionFailed |
| SEO | `BKT-0801` – `BKT-0804` | |
| GEO | `BKT-0810` – `BKT-0812` | |
| 媒体 | `BKT-0901` – `BKT-0904` | |

DoctorCommand 通过 `DiagnosticExceptionFormatter.Format()` 以格式化诊断码输出错误。引擎中 13 个关键抛出点携带诊断码；其他抛出保持向后兼容（Code = null）。

## 内容管道阶段日志

每个内容加载阶段在完成时记录名称与耗时：

```
event=content.stage stage=ContentLoad duration_ms=234
event=content.stage stage=ImageLocalize duration_ms=156
event=content.stage stage=DraftFilter duration_ms=1
event=content.stage stage=ContentGraphValidate duration_ms=3
event=content.stage stage=CollectionWarning duration_ms=12
```

阶段顺序：`ContentLoad` → `ImageLocalize` → `DraftFilter` → `ContentGraphValidate` → `CollectionWarning`。实现：`src/Bukit.Engine/ContentPipeline.cs`、`src/Bukit.Engine/Stages/`。

## metrics（--metrics <path>）

### 启用

在 build 时传入：
- `--metrics metrics.json`

引擎会在构建完成后写出 JSON（缩进格式），路径相对 `rootDir` 解析（绝对路径不变）。

### schema（version=2）

顶层字段：
- `version`：固定为 2
- `ts`：生成时间（UTC，ISO 8601）
- `site`：站点基本信息（name/title/url/baseUrl/language/defaultLanguage/languages）
- `outputDir`：构建输出目录（绝对路径）
- `contentItems`：加载到的内容条目数量（ContentItem 总数，含 data 项）
- `variants[]`：每个语言变体的统计（单语言也会有一个变体）

variants 字段：
- `language` / `baseUrl` / `outputDir`
- `routed`：参与路由与渲染的条目数
- `derived`：插件派生页数量
- `rendered` / `skipped`：本次渲染/跳过数量（增量构建相关）
- `reasons`：渲染原因计数（例如 new_page/template_changed/unchanged/full_render）
- `plugins`：插件执行记录（来源于插件执行器的 `PluginExecutionInfo`）
- `stages.durationsMs`：阶段耗时（毫秒），用于拆解构建热点
- `stages.counts`：阶段计数，用于观察热点触发频率

当前内置的阶段指标包括：
- `variantTotal`：单个语言变体的总耗时
- `prepareContent`：data/content 分流与 modules 构建耗时
- `routeGeneration`：路由生成耗时
- `taxonomySetup`：taxonomy 注入耗时
- `derivePages`：derive-pages 插件阶段耗时
- `templateHash`：模板目录 hash 耗时
- `renderPages`：普通页面渲染总耗时
- `renderSpecialLists`：特殊列表页渲染总耗时
- `afterBuildPlugins`：after-build 插件阶段耗时
- `assetsSync` / `mediaCopy`：构建尾部资源同步耗时
- `contentHash`：页面内容 hash 计算次数与耗时
- `metadataHash`：页面轻量元数据 hash 计算次数与耗时
- `stableContentHash`：基于稳定正文指纹计算增量 hash 的次数与耗时
- `contentHash`：回退到读取正文后计算完整内容 hash 的次数与耗时
- `bodyLoad`：普通页面正文读取次数与耗时
- `pageRender`：普通页面模板渲染次数与耗时
- `listHash`：特殊列表增量 hash 计算次数与耗时
- `listBodyLoad`：特殊列表正文读取次数与耗时
- `listBuild`：特殊列表构建次数与耗时

### 常见用途

- 增量构建为什么变慢：看 `reasons` 是否出现大量 `template_changed/content_changed`
- 插件性能回归：比较 `plugins` 中各插件耗时与错误
- 多语言输出差异：对比不同 `variants[*]` 的 `routed/derived/rendered`
- 渲染热点拆解：看 `stages` 中 `metadataHash/stableContentHash/contentHash/bodyLoad/pageRender/listHash/listBuild`
- 构建尾部耗时：看 `stages` 中 `assetsSync/mediaCopy/afterBuildPlugins`

## Notion 统计

当 `maxRps` 激活时，每个内容源结束时输出一行汇总：

```
event=notion.stats requests=1234 throttle_wait_count=56 throttle_wait_ms=7890
```

## 构建报告

当 `build.report.enabled: true`（或使用 `--ci`）时，引擎将结构化构建报告写入 `dist/.bukit/`：

- `build-report.json` — 包含 `schemaErrorCount`（内容 schema 校验错误数）、页面/路由/资产计数、耗时与增量统计。
- `publish-audit-report.json` — 机器可读与可信发布主审计，覆盖语义 HTML、来源、审核状态、representation 覆盖率与聚合输出一致性。
- `seo-report.json` — 每条路由的 SEO schema 审计检查。
- `geo-report.json` — GEO Score 与 LLM 爬虫就绪度评估。

这些报告设计用于 CI/CD 集成、监控看板与 AI agent 消费。

## 渲染指标

统一调度器 `PageRenderDispatcher.DispatchAsync()` 收集按类型分类的渲染指标：

| 指标键 | 类型 | 说明 |
|---|---|---|
| `pageRender` | Page | 每渲染一个页面递增 |
| `listBuild` | List | 每渲染一个列表页递增 |
| `staticRender` | Static | 每渲染一个静态 HTML 递增 |
| `metadataHash` | Page | hash 计算次数 |
| `bodyLoad` | Page | 正文加载次数 |
| `listBodyLoad` | List | 列表条目正文加载次数 |
| `listHash` | List | 列表内容 hash 计算次数 |
