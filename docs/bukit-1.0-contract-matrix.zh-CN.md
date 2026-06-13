# Bukit 1.0 契约矩阵

日期：2026-06-08
版本：1.0

## 支持等级定义

| 等级 | 含义 |
|------|------|
| `GA-locked` | 1.0 后只允许对全新 1.0 契约做非破坏性演进；breaking change 必须进入未来 major |
| `GA-limited` | 正式可用，但边界窄、约束明确，不承诺生态完整性 |
| `Experimental` | 可随 public preview 演进，不进入 1.0 稳定承诺 |
| `Out of scope` | 不属于 Bukit 1.0 |

## 能力矩阵

### 配置系统

| 能力 | 1.0 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|----------|----------|----------|----------|
| `site.yaml` 核心字段（site/content/build/theme/taxonomy/logging/deploy） | `GA-locked` | 仅 1.0 新字段 | 旧字段 rejected-with-message + BKT-000x | ConfigValidator 全覆盖 |
| `content.sources`（多源配置） | `GA-locked` | 唯一入口 | 无 sources 时 rejected | provider parity 测试 |
| `content.provider`（旧单源字段） | **Removed** | 不允许 | rejected-with-message，引导迁移到 sources | 拒绝测试 |
| `site.rssMode` | **Removed** | 不允许 | rejected-with-message | 拒绝测试 |
| `site.searchMode` | **Removed** | 不允许 | rejected-with-message | 拒绝测试 |
| `build.report.enabled` | `GA-locked` | 默认 true | N/A | BuildReporter 测试 |
| 环境变量覆盖 | `GA-locked` | 允许 | N/A | ConfigEnvironmentOverrides 测试 |

### 内容模型

| 能力 | 1.0 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|----------|----------|----------|----------|
| Markdown 内容源 | `GA-locked` | 正式网站基础路径 | N/A | MarkdownFolderProvider 测试 |
| Notion 内容源 | `GA-limited` | API/缓存/媒体下载/field policy 边界明确 | 无 Notion 配置时 rejected | NotionContentProvider 测试 |
| Composite content sources | `GA-limited` | 仅 1.0 新语义 | 旧 provider 行为 rejected | Composite 测试 |
| 内容模型 schema | `GA-locked` | reserved meta keys 冻结 | strict 模式 rejected | Schema 测试 |
| `collection` 字段（推荐写法） | `GA-locked` | 唯一推荐 | N/A | RouteGenerator 测试 |
| `type` 字段 | **Removed from starter contract** | 不作为路由/模板选择依据；starter 不声明 | 若内容模型 strict 开启则按未知/移除字段处理 | StarterContentAudit + RouteGenerator 测试 |
| `seo_title`、`cover`、`cover_alt`、`tableOfContents` | `GA-locked` | starter schema 正式字段 | N/A | Schema 测试 |

### 路由系统

| 能力 | 1.0 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|----------|----------|----------|----------|
| 路由优先级（FullOverride > PartialOverride > Collection > Permalink） | `GA-locked` | 冻结 | N/A | Golden tests |
| `route.url`（嵌套路由 URL） | `GA-locked` | 允许 | N/A | Golden tests |
| `route.outputPath`（嵌套路由输出路径） | **Removed** | 不允许 | rejected-with-message + BKT-0209 | 拒绝测试 |
| 顶层 `outputPath` | **Removed** | 不允许 | rejected-with-message + BKT-0209 | 拒绝测试 |
| `route.template`（嵌套路由模板） | `GA-locked` | 允许 | N/A | Golden tests |
| collection/permalink 匹配规则 | `GA-locked` | 冻结 | 无匹配时 rejected | Golden tests |
| 派生路由（list/taxonomy/pagination/archive/alias/static） | `GA-locked` | 冻结 | N/A | Golden tests |
| `outputPathEncoding` | `GA-locked` | none/slug/urlencode/sanitize | 非法值 rejected | 编码测试 |
| 路由安全校验 | `GA-locked` | 冻结 | 不安全路径 rejected + BKT-02xx | RouteSecurityValidator 测试 |
| SEO/GEO publish projection | `GA-limited` | 输出和审计 schema 稳定 | N/A | SEO/GEO 测试 |

### 主题系统

| 能力 | 1.0 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|----------|----------|----------|----------|
| `theme.yaml` 必填字段（name/version/engine） | `GA-locked` | 必填 | 缺失 rejected + BKT-0100 | ValidateThemeYaml 测试 |
| `theme.yaml` version 字段 | `GA-locked` | 必填，semver | 无效 rejected + BKT-0100 | ValidateThemeYaml 测试 |
| `requires_bukit` / engine range | `GA-locked` | 必填 | 不兼容 rejected + BKT-0100 | ValidateThemeYaml 测试 |
| `theme.yaml extends` 本地主题继承 | `GA-locked` | 父主题必须已存在于本地 `themes/<name>` 且含 `theme.yaml` | 非法父主题名/继承链断裂 rejected | ThemeBootstrapper 测试 |
| Starter theme | `GA-locked` | 官方信任样板 | N/A | smoke.sh 通过 |
| `site.yaml theme.source` / `theme.extends` | **Removed** | 不允许 | unknown field rejected | ConfigLoader 测试 |
| Remote theme source/lock | `Experimental` | 仅 Labs/tooling 获取并安装为本地主题 | Core 不 clone/lock/联网 | CoreBoundary 测试 |
| Theme registry/search/install 生态 | `Experimental` | 不承诺生态可用性 | N/A | N/A |
| 无 `theme.yaml` 主题 | **Removed** | 不允许 | rejected + BKT-0100 | ValidateThemeYaml 测试 |

### 插件系统

| 能力 | 1.0 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|----------|----------|----------|----------|
| Built-in plugin lifecycle | `GA-locked` | 内建插件顺序/失败策略/输出归属冻结 | N/A | PluginRunner 测试 |
| External process protocol plugin | `GA-limited` | 协议 v2、capability、env/output 边界稳定 | 无 capability rejected + BKT-0704 | ProtocolEchoPlugin 测试 |
| Protocol handshake v2 | `GA-locked` | 仅 v2 | v1 rejected + BKT-0705 | ProtocolHandshakeNegotiator 测试 |
| Plugin capabilities 声明 | `GA-locked` | 必填 | 缺失 rejected + BKT-0704 | PluginCapabilityEnforcer 测试 |
| Plugin env isolation | `GA-locked` | 冻结 | N/A | ProcessPluginInvoker 测试 |
| Plugin timeout | `GA-locked` | 冻结 | 超时 rejected + BKT-0702 | ProcessPluginInvoker 测试 |
| Plugin stdout/stderr limits | `GA-locked` | 冻结 | 超限 rejected + BKT-0703 | ProcessPluginInvoker 测试 |
| Plugin output traversal | `GA-locked` | 冻结 | 路径逃逸 rejected + BKT-0706 | ProcessPluginInvoker 测试 |
| Plugin stale output cleanup | `GA-locked` | 冻结 | 清理失败 + BKT-0707 | ProcessPluginInvoker 测试 |
| Plugin SHA256 校验 | `GA-locked` | 冻结 | 不匹配 rejected + BKT-0701 | ExternalProtocolPluginSource 测试 |
| Source-generated plugin SDK | `GA-limited` | 若对外暴露需独立版本说明 | N/A | PluginSourceGenerator 测试 |

### 构建与审计

| 能力 | 1.0 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|----------|----------|----------|----------|
| `.bukit/build-report.json` | `GA-locked` | schema 冻结 | N/A | Schema validation |
| `.bukit/routes.json` | `GA-locked` | schema 冻结 | N/A | Schema validation |
| `.bukit/assets.json` | `GA-locked` | 覆盖完整 public output | N/A | Schema validation |
| `.bukit/incremental-manifest.json` | `GA-locked` | schema 冻结 | N/A | Schema validation |
| `.bukit/security-report.json` | `GA-locked` | 真实检查结果 | N/A | Schema validation |
| 确定性构建（clean twice） | `GA-locked` | 冻结 | N/A | build-repro.sh |
| Clean vs incremental 一致性 | `GA-locked` | 冻结 | N/A | CI 验证 |

### 其他能力

| 能力 | 1.0 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|----------|----------|----------|----------|
| AI Intent / samples/intent | `Experimental` | 不纳入核心 1.0 信任链 | N/A | N/A |
| clone/import | `Experimental` | 不进入 1.0 稳定承诺 | N/A | N/A |
| BukitJalil | `Out of scope` | 不进入 Bukit 1.0 | N/A | N/A |

## 诊断码范围

| 范围 | 类别 | 状态 |
|------|------|------|
| BKT-000x | Config | 已分配 0001-0007 |
| BKT-010x | Theme | 已分配 0100-0104 |
| BKT-020x | Route | 已分配 0201-0209 |
| BKT-030x | Render | 已分配 0301-0399 |
| BKT-040x | Schema | 已分配 0401-0402 |
| BKT-050x | Content | 已分配 0501-0503 |
| BKT-060x | Build | 已分配 0601-0603 |
| BKT-070x | Plugin | 已分配 0701-0707 |
| BKT-080x | SEO/GEO | 已分配 0801-0812 |
| BKT-090x | Media | 已分配 0901-0904 |

## 旧行为处理策略

| 旧行为 | 处理方式 | 诊断码 |
|--------|----------|--------|
| `content.provider` | rejected-with-message → 迁移到 `content.sources` | BKT-0007 |
| `site.rssMode` | rejected-with-message | BKT-0005 |
| `site.searchMode` | rejected-with-message | BKT-0005 |
| 顶层 `outputPath` | rejected-with-message → 使用 `route.url` | BKT-0209 |
| nested `route.outputPath` | rejected-with-message → 使用 `route.url` | BKT-0209 |
| 同时声明 `type` + `collection` | 1.0 starter 禁止；路由/模板只读取 `collection` | N/A |
| 仅声明 `type` | 不匹配 1.0 starter 契约；不会驱动路由 | N/A |
| 无 `theme.yaml` 主题 | rejected + BKT-0100 | BKT-0100 |
| Plugin protocol v1 | rejected + BKT-0705 | BKT-0705 |
| 无 `capabilities` 外部插件 | rejected + BKT-0704 | BKT-0704 |
| Warning-only 运行路径 | 全部升级为 rejected-with-message | 各对应 BKT-xxxx |
