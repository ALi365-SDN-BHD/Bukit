# 可观测性（日志与 metrics）

本项目的可观测性由两部分组成：

- 日志：控制台输出（text/json）
- metrics：构建结束后输出的结构化 JSON（可用于 CI 采集、增量原因分析、插件耗时分析）

相关入口：
- 日志：`src/Bukit.Shared/Logger.cs`
- 传参：`src/Bukit.Cli/Commands/BuildCommand.cs`
- metrics 输出：`src/Bukit.Engine/SiteEngine.cs`

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

## metrics（--metrics <path>）

### 启用

在 build 时传入：
- `--metrics metrics.json`

引擎会在构建完成后写出 JSON（缩进格式），路径相对 `rootDir` 解析（绝对路径不变）。

### schema（version=1）

顶层字段：
- `version`：固定为 1
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

### 常见用途

- 增量构建为什么变慢：看 `reasons` 是否出现大量 `template_changed/content_changed`
- 插件性能回归：比较 `plugins` 中各插件耗时与错误
- 多语言输出差异：对比不同 `variants[*]` 的 `routed/derived/rendered`
