# Bukit 1.0 决策记录

日期：2026-06-08  
来源计划书：[2026-06-07-bukit-1-0-ga-trust-plan.zh-CN.md](./2026-06-07-bukit-1-0-ga-trust-plan.zh-CN.md)  
适用范围：Bukit 1.0 GA 收口。本文记录已经拍板的产品与工程契约，供实现、测试、文档和 release gate 对齐。

## 总前提

Bukit 1.0 按全新项目实施。各模块不采用兼容模式，不承诺历史文件、历史网站、旧配置、旧主题或旧插件协议可继续运行。旧行为只能删除、拒绝，或给出新项目重写提示；不能静默兼容，也不能 warning 后继续运行。

## 当前执行状态

截至 2026-06-08 本轮收口：

- 核心实现已对齐：`content.provider` 在 `site.yaml` 中被拒绝；`content.sources[]` 是唯一内容源入口；顶层 `outputPath` 和 nested `route.outputPath` 都被 `BKT-0209` fail fast；`type` 不再驱动 routing；`build.report.enabled` 默认开启；external process plugin 必须声明 `capabilities`。
- 默认构建入口已对齐：`BuildOptionsMapper` 不再生成 `Provider=markdown`，而是生成默认 markdown source。
- 已通过回归：`Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Cli.Tests`、`Bukit.Rendering.Tests`、`Bukit.Importing.Tests`、`dotnet test bukit.slnx -c Release --no-restore`。
- 已通过非沙箱 gate：`bash scripts/smoke.sh Release`、`bash scripts/security-regression.sh Release`、`bash scripts/check-doc-asset-consistency.sh`、`bash scripts/build-repro.sh Release`。
- 公开 guide、AI prompt、demo/import 资料和 skills 已完成 1.0 契约 sweep；可复制旧 `provider:` 配置已清零，`content.provider` 仅保留在“已移除/禁止生成”说明中。
- AI Intent 当前仍是 Experimental DSL：直接生成 `site.yaml` 时必须生成 1.0 `content.sources[]`。若要把 Intent 也纳入 1.0 GA 配置契约，下一步应迁移 Intent DSL 字段名。

## 决策 1：nested `route.outputPath`

决策：1.0 拒绝 nested `route.outputPath`，只允许 `route.url` 和可选 `route.template`。

契约：

- `route.url`：允许，作为唯一公开 URL 控制面。
- `route.template`：允许，用于覆盖模板。
- `route.outputPath`：拒绝。
- 顶层 `outputPath`：拒绝。
- output path 由最终 URL 派生，并统一经过 `outputPathEncoding` 与 route security validation。

原因：

- URL 是用户真正需要控制的公开路由语义。
- output path 是引擎内部写入语义，允许手写会制造 URL/磁盘路径分叉。
- 拒绝手写 output path 能简化审计、rollback、sitemap/search/rss/publish projection 一致性。

实现要求：

- `RouteGenerator` 对 nested `route.outputPath` 和顶层 `outputPath` 都必须 fail fast。
- 错误必须包含稳定 `BKT-02xx` 诊断码。
- 文档、skills、troubleshooting 不得再教用户通过 `route.outputPath` 解决冲突。

测试要求：

- nested `route.outputPath` 拒绝测试。
- 顶层 `outputPath` 拒绝测试。
- `route.url` 自动派生 output path 的 golden route inventory 测试。

## 决策 2：`content.provider` vs `content.sources`

决策：1.0 只支持 `content.sources`，拒绝 `content.provider`。

契约：

- `content.sources[]`：唯一合法内容源入口。
- `content.provider`：拒绝。
- 顶层 `content.markdown` / `content.notion`：不作为 1.0 内容源入口；只能作为 `sources[].markdown` / `sources[].notion` 的子配置。
- 单源项目也必须写成 `content.sources[]`。
- `sources[].name` 建议作为审计/debug 标识；是否必填由配置契约实现统一决定。

原因：

- `sources` 能表达多源、data source、collection 归属和 source identity。
- 单一入口避免 provider/sources 双轨优先级与静默升级。
- 1.0 不做历史兼容，因此不应保留 provider auto-upgrade。

实现要求：

- `ConfigLoader` 发现 `content.provider` 时必须拒绝，不得 auto-upgrade。
- `ConfigValidator` 必须要求 `content.sources` 非空。
- examples、fixtures、skills、guide、AI prompt 都必须改为 `content.sources[]`。

测试要求：

- `content.provider` rejected-with-message 测试。
- 无 `content.sources` 拒绝测试。
- markdown / notion / composite sources 新语义测试。

## 决策 3：`type` 与 `collection`

决策：1.0 starter 只写 `collection`，不写 `type`。

契约：

- `collection`：starter 的唯一公开写法，用于路由、列表、分页、taxonomy、feed/search/sitemap 归属。
- `type`：starter 不暴露。
- engine 内部可保留 `ContentType`，但不应让 starter 教用户同时声明 `type` 与 `collection`。

原因：

- `collection` 更清楚地表达内容归属与构建行为。
- `type + collection` 双声明会让用户无法判断哪个字段生效。
- starter 是 1.0 官方样板，必须去掉这个认知噪音。

实现要求：

- starter content 和 fixtures 移除默认 `type` 字段。
- route/content 文档以 `collection` 为唯一新项目写法。
- 如果仍支持 `type` 作为高级语义字段，必须从 routing contract 中切开，不作为 starter 路由入口。

测试要求：

- starter content audit 测试。
- route matching 以 `collection` 为主的 golden tests。

## 决策 4：`build.report.enabled`

决策：1.0 默认开启 build report。

契约：

- 默认生成 `dist/.bukit/` 报告。
- 用户可显式设置 `build.report.enabled: false` 关闭，但 release/CI profile 应要求开启。
- 可审计报告保留完整字段；确定性比较使用 normalized report，忽略时间、耗时、本机绝对路径等字段。

原因：

- 1.0 核心目标包含可审计、可回滚、可复现。
- 默认关闭报告会让用户出问题后才知道应该开启审计。
- `.bukit/` 是 release artifact 和 rollback 证据的基础。

实现要求：

- record 默认值、ConfigLoader 默认值、schema、示例、测试必须一致。
- smoke/release gate 应确认 `.bukit/` 关键报告存在。

测试要求：

- 默认构建生成 `.bukit/build-report.json` 等报告。
- 显式关闭时不生成报告，并给 info 级提示。

## 决策 5：Notion 内容源等级

决策：Notion 内容源在 1.0 定为 `GA-limited`。

支持范围：

- `content.sources[].type: notion`
- database 内容读取
- 基础 property mapping
- Published/filter、pageSize/maxItems
- 基础 block 渲染
- 媒体下载安全边界
- rate limit / retry / cacheMode 的明确行为
- 稳定诊断码和定位信息

不承诺：

- Notion API 外部行为稳定性
- 高级 relation graph 自动解析
- 任意 property 类型完美映射
- 复杂 block 像素级一致性
- workspace 权限、限流、第三方状态问题

## 决策 6：clone/import 定位

决策：clone/import 在 1.0 定为 `Experimental`。

契约：

- 仅作为重新生成 Bukit 1.0 新项目草稿的辅助工具。
- 不属于核心建站契约。
- 不承诺历史站点原地升级。
- 不承诺任意 HTML/CSS/JS 输入可成功转换。
- 生成结果必须人工 review。

实现要求：

- 文档和 CLI 输出必须标明 Experimental。
- import/clone 输出必须符合 1.0 新项目结构，或明确失败。

## 决策 7：External process plugin 等级

决策：External process plugin 在 1.0 定为 `GA-limited`。

支持范围：

- `runtime: process`
- protocol schema v2-only
- hook：`derive-pages`、`after-build`
- 必填 `capabilities`
- env isolation、allowEnvironment、timeout、stdout/stderr limits
- output path safety、stale output cleanup
- handshake/bad JSON/empty stdout/ok=false/timeout 的稳定失败语义

不支持：

- protocol v1 fallback
- 未声明 `capabilities` 默认放行
- dynamic assembly plugin
- WASM plugin
- plugin registry / marketplace
- 插件自身逻辑稳定性
- 任意语言运行时可用性

## 决策 8：Theme registry/search/install 等级

决策：Theme registry/search/install 生态在 1.0 定为 `Experimental`。

分层：

- 本地 theme contract：`GA-locked`
- Remote theme source / lock：`GA-limited`
- Theme registry/search/install：`Experimental`

原因：

- registry 可用性、社区主题质量、版本纪律、远程下载可靠性和供应链安全不能由核心引擎完全控制。
- 1.0 信任基础应落在本地主题契约和 remote source lock，不落在生态发现层。

## Release Gate 对齐项

- `dotnet test bukit.slnx -c Release --no-restore`
- `bash scripts/smoke.sh Release`
- `bash scripts/security-regression.sh Release`
- `bash scripts/check-doc-asset-consistency.sh`
- `bash scripts/build-repro.sh Release`
- `.bukit/*.json` schema validation
- 旧写法 rejection suite
