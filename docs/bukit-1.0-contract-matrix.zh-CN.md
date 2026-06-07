# Bukit 1.0 契约矩阵

日期：2026-06-08
基线：全新项目契约，不保留历史兼容模式

## 支持等级定义

| 等级 | 含义 |
|------|------|
| `GA-locked` | 1.0 后仅允许对全新契约做非破坏性演进；breaking change 必须进入未来 major |
| `GA-limited` | 正式可用，但边界窄、约束明确，不承诺生态完整性 |
| `Experimental` | 可随 public preview 演进，不进入 1.0 稳定承诺 |
| `Out of scope` | 不属于 Bukit 1.0 |

## 能力矩阵

### 配置

| 能力 | 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|------|----------|----------|----------|
| `site.yaml` 七大节点（site/content/build/theme/taxonomy/logging/deploy） | GA-locked | 全部 1.0 字段 | 旧字段拒绝 + BKT-000x + 新写法提示 | ConfigValidator tests, rejection suite |
| `content.provider` 单源入口 | GA-locked | markdown / notion | 缺失或无效值拒绝 | ConfigValidator tests |
| `content.sources` 多源/复合入口 | GA-locked | notion / markdown source 列表 | type 缺失/无效拒绝 | ConfigValidator tests |
| `build.report.enabled` | GA-locked | release/profile 默认 true | N/A | BuildReporter tests |
| ~~`site.rssMode`~~ | **Removed** | — | 拒绝 + BKT-0001 + 迁移到 `site.feed.formats` | rejection suite |
| ~~`site.plugins.rss`~~ | **Removed** | — | 拒绝 + BKT-0001 + 迁移到 `site.plugins.feed` | rejection suite |
| ~~顶层 `outputPath`~~ | **Removed** | — | 拒绝 + BKT-0201 + 迁移到 `route.outputPath` | rejection suite |
| ~~`collections.*.rss`~~ | **Removed** | — | 拒绝 + BKT-0001 + 迁移到 `collections.*.feed` | rejection suite |
| ~~`site.collection`~~ | **Removed** | — | 拒绝 + BKT-0001 + 迁移到 `site.collections` | rejection suite |
| ~~`content.notion.rootPageId`~~ | **Removed** | — | 拒绝 + BKT-0001 + 迁移到 `rootBlockId` | rejection suite |

### 内容

| 能力 | 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|------|----------|----------|----------|
| Markdown 内容源 | GA-locked | dir/includePaths/includeGlobs/defaultType | N/A | Markdown provider tests |
| Notion 内容源 | **GA-limited** | DatabaseId/PropertyMap/FieldPolicy/缓存/媒体下载 | API 错误友好提示 | Notion provider tests |
| Composite 内容源 | GA-limited | sources 列表合并 | source 配置错误拒绝 | Composite provider tests |
| 内容模型 schema（reserved meta/field normalization） | GA-locked | ModelSchema 全部字段 | strict 模式下 schema 违规拒绝 | Schema validator tests |
| ~~type 双声明~~ | **Removed** | 唯一写法：`collection` | `type` 声明产生 warning，推荐 `collection` | starter content audit |

### 路由

| 能力 | 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|------|----------|----------|----------|
| 路由优先级（full override > collection/permalink > partial） | GA-locked | route.url/route.template/route.outputPath | conflict 拒绝 + BKT-0202 | route inventory golden tests |
| nested `route.outputPath` | **GA-locked（正式契约）** | route.outputPath 允许在 collection 下 | 不安全路径拒绝 + BKT-0206 | RouteGenerator tests |
| 顶层 `outputPath` | **Removed** | — | 拒绝 + BKT-0201 | rejection suite |
| collection/type 匹配规则 | GA-locked | collection 匹配优先 | 无匹配拒绝 | RouteGenerator tests |
| 派生路由（list/taxonomy/pagination/archive/static/plugin） | GA-locked | 按 collection output 配置 | conflict 拒绝 + BKT-0202 | route inventory golden tests |
| 派生路由进入 search/rss/sitemap 默认策略 | GA-locked | list/taxonomy 默认进入 | N/A | route inventory golden tests |
| outputPathEncoding（none/slug/urlencode/sanitize） | GA-locked | 四种模式 | 无效值拒绝 | RoutePathBuilder tests |

### 主题

| 能力 | 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|------|----------|----------|----------|
| `theme.yaml` manifest | GA-locked | name/version/engine/min_engine_version 必填 | 无 manifest 拒绝 + BKT-0100 | theme doctor tests |
| `extends` 继承 | GA-locked | 单继承链 | 继承循环/缺失拒绝 | ThemeManifest tests |
| `fallbackDir` 回退目录 | GA-limited | 本地 fallback 目录 | 路径穿越拒绝 | ThemePathResolver tests |
| template capabilities 声明 | GA-locked | bukit.templates.yaml 中声明 | 缺失声明拒绝 | TemplateCapabilities tests |
| Starter theme | GA-locked | 作为官方样板 | N/A | smoke.sh + doctor 通过 |
| Remote theme source/lock | GA-limited | 远程获取 + lock 文件 | 无 lock/无 network 拒绝 | ThemeSourceManager tests |
| Theme registry/search/install | **Experimental** | — | 不承诺生态可用性 | 基础功能测试 |
| ~~无 theme.yaml 的主题兼容~~ | **Removed** | — | 拒绝 + BKT-0100 + 要求生成 manifest | doctor rejection tests |

### 插件

| 能力 | 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|------|----------|----------|----------|
| Built-in plugin lifecycle | GA-locked | derive-pages -> render -> after-build | 失败按 failMode 处理 | PluginPipeline tests |
| External process protocol v2 | GA-limited | handshake v2 + capabilities 必填 | v1 拒绝 + BKT-0700 | ProtocolEchoPlugin tests |
| capabilities 声明 | GA-limited | derive-pages / emit-outputs | 缺失/不匹配拒绝 + BKT-0701 | PluginCapabilityEnforcer tests |
| env isolation / timeout / stdout-stderr limits | GA-limited | timeoutMs/maxStdoutBytes/maxStderrBytes/allowEnvironment | 超时拒绝 + BKT-0702 | ProtocolEchoPlugin tests |
| stale output cleanup | GA-limited | 自动清理 | N/A | ProtocolEchoPlugin tests |
| ~~plugin protocol v1 fallback~~ | **Removed** | — | 拒绝 + BKT-0700 | handshake rejection tests |
| ~~缺失 capabilities 的默认放行~~ | **Removed** | — | 拒绝 + BKT-0701 | PluginCapabilityEnforcer tests |

### 构建与审计

| 能力 | 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|------|----------|----------|----------|
| `.bukit/build-report.json` | GA-locked | 自动生成 | N/A | schema validation |
| `.bukit/routes.json` | GA-locked | 自动生成 | N/A | schema validation + golden tests |
| `.bukit/assets.json` | GA-locked | 自动生成 | N/A | schema validation |
| `.bukit/security-report.json` | GA-locked | 包含真实检查结果 | N/A | schema validation + security-regression 集成 |
| 可复现构建 | GA-locked | clean build 两次一致 | N/A | deterministic build compare |

### AI / 导入 / 其他

| 能力 | 等级 | 允许配置 | 拒绝行为 | 测试要求 |
|------|------|----------|----------|----------|
| AI Intent / samples/intent | Experimental | — | 不纳入核心信任链 | 基础功能测试 |
| clone（项目克隆） | GA-limited | 重新生成 1.0 新项目的工具 | 非 Bukit 项目拒绝 | CloneCommand tests |
| import（HTML 导入） | GA-limited | html-demo / seed 子命令 | 无效输入拒绝 | ImportCommand tests |
| BukitJalil | **Out of scope** | — | 不进入 Bukit 1.0 | 确保不被引用 |
