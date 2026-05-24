# Bukit 其他模块提升计划

> 目的：梳理 Bukit 非插件模块的能力现状，对比 Hugo/Astro/11ty/Zola，给出提升方向与优先级。

---

## 一、模块全景图

Bukit 有 **10 个模块**，排除插件系统（已在 P0/P1/P2 完成）后，分析范围为 **9 个**：

| 模块 | 路径 | 职责 | 当前等级 |
|------|------|------|---------|
| **Bukit.Config** | `src/Bukit.Config/` | YAML 配置加载、验证、覆盖 | 🟡 中等 |
| **Bukit.Content** | `src/Bukit.Content/` | Markdown/Notion 内容处理、图片本地化 | 🟡 中等 |
| **Bukit.Routing** | `src/Bukit.Routing/` | URL 生成、路径编码、冲突检测 | 🟡 中等 |
| **Bukit.Rendering** | `src/Bukit.Rendering/` | Scriban 模板引擎（布局/组件/短代码/SEO） | 🟢 较好 |
| **Bukit.Engine** | `src/Bukit.Engine/` | 构建编排（多语言/增量/SEO/指标） | 🟢 较好 |
| **Bukit.Theme** | `src/Bukit.Theme/` | 主题清单/Section/组件/令牌/目录 | 🟡 中等 |
| **Bukit.Cli** | `src/Bukit.Cli/` | 16 个命令（build/init/deploy/seo/geo/theme等） | 🟢 较好 |
| **Bukit.Shared** | `src/Bukit.Shared/` | 日志/slug/短代码/layout 解析 | 🟡 中等 |
| **Bukit.Engine.Abstractions** | `src/Bukit.Engine.Abstractions/` | 核心数据模型、插件接口 | 🟡 中等 |

---

## 二、逐模块对比与提升方向

### 2.1 Bukit.Config（配置加载与验证）🟡 → 🟢

**现状：**
- YamlDotNet 反序列化 + 强类型 record 模型
- `ConfigValidator` 包含 50+ 条规则（必填字段、URL 格式、路径穿越防护等）
- `ConfigOverrides` CLI 开关覆盖
- 无 JSON Schema 支持，无 IDE 智能感知

**对标：**
| SSG | Schema 校验 | 类型检查 | IDE 支持 | 诊断命令 |
|-----|-----------|---------|---------|---------|
| Astro | Zod + TS | 完整 TS | 一等公民 | `astro check` |
| Hugo | 无 | 无 | 社区 JSON Schema | `hugo config` |
| Zola | Serde | Rust 类型 | 无 | `zola check` |
| **Bukit 当前** | 自定义 50+ 规则 | C# record 类型 | ❌ 无 | ❌ 无 YAML Schema |

**提升方向：**

| 编号 | 提升项 | 严重程度 | 说明 |
|------|--------|---------|------|
| **C-1** | 生成 JSON Schema | 🟡 P1 | 从 `AppConfig` record 反射生成 `schema.json`，VSCode/YAML LSP 自动补全+校验 |
| **C-2** | 配置诊断命令 | 🟡 P1 | `bukit config check` — 验证但不构建；复用/抽取 `doctor` 中已有配置校验逻辑，输出完整配置问题 |
| **C-3** | 环境变量注入 | 🟡 P1 | `BUKIT_*` 前缀环境变量覆盖任意配置字段，方便 CI/CD |
| **C-4** | 废弃配置项警告 | 🟢 P2 | 迁移提示（如 `rss` → `feed`），`warn` 模式自动检测 |

---

### 2.2 Bukit.Content（内容处理）🟡 → 🟢

**现状：**
- Markdown 提供者：`BasicMarkdownToHtml` 仅处理 h1/h2/h3/p/img
- Notion 提供者：完整 Notion Blocks 渲染（25+ 块类型）
- 图片本地化管道（SSRF 安全，增量缓存）
- **无** 语法高亮、**无** 脚注、**无** 数学公式、**无** TOC、**无** Emoji

**对标：**
| Markdown 功能 | Hugo | Astro | 11ty | Bukit 当前 |
|---|---|---|---|---|
| 渲染引擎 | Goldmark | remark/rehype | markdown-it | BasicMarkdownToHtml |
| 语法高亮 | Chroma | Shiki | Prism.js | ❌ |
| 脚注 | ✅ | 需插件 | 需插件 | ❌ |
| 数学公式 | ✅ | 需插件 | 需插件 | ❌ |
| TOC 自动生成 | ✅ | 需插件 | 需插件 | ❌ |
| Emoji | ✅ | 需插件 | 需插件 | ❌ |
| 自定义 Hook | renderHooks | remark 管线 | markdown-it 插件 | ❌ |

**提升方向：**

| 编号 | 提升项 | 严重程度 | 说明 |
|------|--------|---------|------|
| **CT-1** | 代码语法高亮 | 🔴 P0 | 集成 Markdig + Prism.js/Highlight.js，或调用 Scriban `markdownify` 过滤器 |
| **CT-2** | GFM 扩展 | 🔴 P0 | 表格、任务列表、删除线、自动链接（通过 Markdig 或扩展 BasicMarkdownToHtml） |
| **CT-3** | 自动 TOC 生成 | 🟡 P1 | 从标题提取生成 `.TableOfContents`，模板可访问 |
| **CT-4** | Emoji 快捷方式 | 🟢 P2 | `:smile:` → Unicode，可配置 |
| **CT-5** | 脚注支持 | 🟢 P2 | `[^1]` 脚注语法 |
| **CT-6** | 内容 Schema 校验增强 | 🟡 P1 | 已有 `collection.schema` 基础校验（required/type）与 doctor 检查；下一步增强格式、枚举、范围、默认值应用和更清晰诊断 |

---

### 2.3 Bukit.Routing（路由与 URL 生成）🟡

**现状：**
- 5 层路由解析优先级（FullOverride → PartialOverride → Collection → Permalink → BuiltinFallback）
- 变量扩展：`{slug}`、`{title}`、`{year}`、`{month}`、`{day}`、`{type}`
- 路径编码：`none`/`slug`/`urlencode`/`sanitize`

**对标：**
| SSG | URL 模式 | 重定向 | 别名 |
|-----|---------|--------|------|
| Hugo | `:year/:month/:slug/` | `aliases` front matter | 自动 HTML redirect |
| Astro | 动态路由 `[...slug]` | 手动/中间件 | 需服务端配置 |
| **Bukit** | `{slug}` 变量 | ✅ AliasPlugin | ✅ 已实现 |

**分析：** 路由系统已较完善，AliasPlugin 已实现 URL 重定向。**暂无高优先级提升项。**

---

### 2.4 Bukit.Rendering（模板引擎）🟢

**现状：**
- Scriban 模板引擎（Jinja2 方言）
- 布局继承（`{% layout %}`，10 层深度）
- 短代码 + 组件系统
- Section 渲染 + 响应式图片辅助函数
- 完整的 SEO 模型注入
- 模板缓存（基于文件签名）

**对标：** Scriban 功能接近 Hugo Go Templates 和 Zola Tera。**暂无高优先级提升项。**

---

### 2.5 Bukit.Engine（构建引擎）🟢

**现状：**
- 并行多语言构建
- 增量构建（基于哈希的智能跳过）
- SEO 诊断（off/warn/strict，40+ 检查项）
- SEO HTML 注入（OG/Twitter/JSON-LD/hreflang）
- SCSS 编译 + 图片优化
- 插件运行器（derive + after-build 钩子）

**对标：** Bukit 的 SEO 注入 + 诊断系统在所有 SSG 中领先。Hugo/Astro 均无内置 SEO audit。

**提升方向：**

| 编号 | 提升项 | 严重程度 | 说明 |
|------|--------|---------|------|
| **E-1** | 构建报告面板 | 🟢 P2 | 对标 Astro Dev Toolbar — 生成 HTML 构建报告，可视化各阶段耗时、页面数、渲染原因 |
| **E-2** | 不完整构建恢复 | 🟢 P2 | 构建中断后自动恢复，不丢失已完成页面 |

---

### 2.6 Bukit.Theme（主题系统）🟡

**现状：**
- `theme.yaml` 清单（`ThemeManifestV2`）
- Section/Component 注册表 + 继承链
- 设计令牌（tokens → CSS 变量）
- `PageComposer` + `SectionDataResolver`（JSON → Section 编排）
- `SectionSchemaValidator`（off/warn/strict）

**对标：** 这是 Bukit 的自有创新（Hugo/Astro 均无内置主题组件化系统）。

**提升方向：**

| 编号 | 提升项 | 严重程度 | 说明 |
|------|--------|---------|------|
| **T-1** | 主题注册表生态增强 | 🟡 P1 | `bukit theme search/install --registry` 已有基础实现；下一步完善注册表服务端、索引治理、预览元数据、签名/校验策略 |
| **T-2** | 主题预览 | 🟢 P2 | 安装前可预览主题（截图 + 描述） |
| **T-3** | 设计令牌深层合并 | 🟢 P2 | 当前为浅合并，支持深层嵌套 token 合并 |

---

### 2.7 Bukit.Cli（命令行）🟢

**现状：**
- 16 个命令（build/init/deploy/seo/geo/theme/dev/preview/doctor/clean/plugin/template/intent/webhook/version/help）
- 结构化 CLI 解析（`CliParser`）
- 帮助/错误渲染
- SEO/GEO 审计（`seo audit/diff`、`geo`）

**对标：** Bukit CLI 命令丰富度超越 Hugo（Hugo 无内置 seo/geo audit 命令）。

**提升方向：**

| 编号 | 提升项 | 严重程度 | 说明 |
|------|--------|---------|------|
| **CLI-1** | `bukit config check` | 🟡 P1 | 配置验证命令（不构建），应复用/抽取 `doctor` 中已有配置校验逻辑，避免重复实现 |
| **CLI-2** | `bukit lint` | 🟢 P2 | Markdown 风格检查、链接死链检测、模板语法检查 |
| **CLI-3** | Shell 自动补全 | 🟢 P2 | 生成 bash/zsh/fish 补全脚本 |

---

### 2.8 Bukit.Shared（共享工具类）🟡

**现状：**
- `ILogger` + `ConsoleLogger`（text/json 格式）
- `SlugHelper`（Unicode 规范化的 slug 生成）
- `ScribanLayoutDirectiveParser`（布局指令提取）
- `ShortcodeProcessor`（`{% name args %}` 正则解析）

**提升方向：**

| 编号 | 提升项 | 严重程度 | 说明 |
|------|--------|---------|------|
| **S-1** | Markdown 升级 | 🔴 P0 | 用 Markdig 替换 `BasicMarkdownToHtml`，或增强其为完整 GFM 支持 |
| **S-2** | 结构化日志 | 🟢 P2 | JSON 日志添加 `spanId`/`traceId` 支持分布式追踪 |

---

### 2.9 Bukit.Engine.Abstractions（核心抽象）🟡

**现状：**
- `ContentItem`（不可变记录，延迟 body 加载）
- `RouteInfo`/`BuildContext`/`SeoIndexEntry`
- 插件接口（`IDerivePagesPlugin`/`IAfterBuildPlugin`/`ISectionPlugin`）

**分析：** 核心抽象已稳定，暂无高优先级提升项。

---

## 三、优先级汇总

### 🔴 P0 — 必须补齐

| 编号 | 模块 | 提升项 | 对标 | 工作量 |
|------|------|--------|------|-------|
| **P0-CT1** | Content | 代码语法高亮（Markdig + 高亮库集成） | Hugo Chroma / Astro Shiki | 3-4 天 |
| **P0-CT2** | Content | GFM 扩展（表格/任务列表/删除线/自动链接） | Hugo Goldmark | 2-3 天 |

### 🟡 P1 — 强烈建议

| 编号 | 模块 | 提升项 | 对标 | 工作量 |
|------|------|--------|------|-------|
| **P1-C1** | Config | 生成 JSON Schema（IDE 自动补全+校验） | Astro Zod schema | 2-3 天 |
| **P1-C2** | Config | `bukit config check` 诊断命令（复用 doctor 配置校验） | Hugo `hugo config` | 1-2 天 |
| **P1-C3** | Config | 环境变量注入（`BUKIT_*` 前缀） | 12-Factor App | 1-2 天 |
| **P1-CT3** | Content | 自动 TOC 生成 | Hugo `.TableOfContents` | 1-2 天 |
| **P1-CT6** | Content | 内容 Schema 校验增强（已有基础校验） | Astro `defineCollection` | 2-3 天 |
| **P1-T1** | Theme | 主题注册表生态增强（命令已有基础实现） | Hugo Themes | 2-3 天 |
| **P1-CLI1** | CLI | `bukit config check`（复用 doctor 配置校验） | Hugo `hugo config` | 1-2 天 |

### 🟢 P2 — 锦上添花

| 编号 | 模块 | 提升项 | 工作量 |
|------|------|--------|-------|
| **P2-C4** | Config | 废弃配置项迁移警告 | 1 天 |
| **P2-CT4** | Content | Emoji 快捷方式 | 1 天 |
| **P2-CT5** | Content | 脚注支持 | 1 天 |
| **P2-E1** | Engine | 构建报告面板 | 2-3 天 |
| **P2-E2** | Engine | 不完整构建恢复 | 1-2 天 |
| **P2-T2** | Theme | 主题预览 | 1-2 天 |
| **P2-T3** | Theme | 设计令牌深层合并 | 1 天 |
| **P2-CLI2** | CLI | `bukit lint` 命令 | 2-3 天 |
| **P2-CLI3** | CLI | Shell 自动补全 | 1 天 |
| **P2-S2** | Shared | JSON 日志分布式追踪 | 1 天 |

---

## 四、实施路线图

### Phase 1：Markdown 现代化（P0，v2.9 目标）

| 任务 | 说明 | 工作量 |
|------|------|-------|
| 代码语法高亮 | 引入 Markdig + Prism.js/Highlight.js 输出 | 3-4 天 |
| GFM 扩展 | 表格、任务列表、删除线、自动链接 | 2-3 天 |

### Phase 2：配置与内容增强（P1，v2.10 目标）

| 任务 | 说明 | 工作量 |
|------|------|-------|
| JSON Schema 生成 | 从 record 类型反射生成 schema.json | 2-3 天 |
| `bukit config check` | 复用/抽取 `doctor` 的配置校验子集，提供不构建的配置验证诊断命令 | 1-2 天 |
| 环境变量注入 | `BUKIT_*` 环境变量覆盖配置 | 1-2 天 |
| 自动 TOC 生成 | 从标题提取生成目录 | 1-2 天 |
| 内容 Schema 校验增强 | 在已有 required/type 校验基础上补格式、枚举、范围、默认值应用和诊断输出 | 2-3 天 |
| 主题注册表生态增强 | 在已有 `theme search/install --registry` 基础上完善服务端、索引治理、预览元数据、签名/校验策略 | 2-3 天 |

### Phase 3：DX 与锦上添花（P2，v2.11 目标）

| 任务 | 说明 | 工作量 |
|------|------|-------|
| Emoji + 脚注 + 废弃配置警告 | Markdown 扩展 | 2-3 天 |
| 构建报告 + 中断恢复 | DX 提升 | 3-4 天 |
| `bukit lint` + Shell 补全 | DX 提升 | 3-4 天 |
| 主题预览 + 令牌深层合并 | 主题增强 | 2-3 天 |
| JSON 日志追踪 | 运维增强 | 1 天 |

---

## 五、与其他 SSG 的最终对比

| 维度 | Bukit 当前 | Phase 1 后 | Phase 2 后 | Phase 3 后 | Hugo | Astro |
|------|-----------|-----------|-----------|-----------|------|-------|
| 配置校验 | 50+ 规则 | - | JSON Schema + IDE | +环境变量 | 基础 | 先进 |
| Markdown | 基础 | GFM + 高亮 | +TOC | +Emoji/脚注 | 先进 | 先进 |
| 内容建模 | 无 | - | Schema 校验 | - | 基础 | 先进 |
| 资源管线 | SCSS + 图片 | - | - | - | 先进 | 先进 |
| HMR | dotnet watch | - | - | - | 中等 | 先进 |
| SEO | 40+ 诊断 + 注入 | - | - | - | 中等 | 中等 |
| GEO/AI | llms.txt + 8 bot 规则 | - | - | - | 基础 | 基础 |
| I18N | 多语言并行 | - | - | - | 先进 | 基础 |
| 增量构建 | 哈希跳过 | - | - | +构建面板 | 先进 | 中等 |
| DX | doctor + theme | - | config check | lint + 补全 | 中等 | 先进 |

---

## 六、Bukit 独特优势（保持/强化）

1. **SEO 诊断系统**：40+ 检查项 + audit/diff CLI，全行业唯一
2. **GEO/AI 搜索**：llms.txt + llms-full.txt + 8 种 AI Bot 规则，全行业唯一
3. **主题组件化**：Section/Component 注册表 + 设计令牌，全行业唯一
4. **意图配置**：`bukit intent` 基于 YAML 意图文件的配置向导
5. **网站克隆**：`bukit clone` 抓取+主题生成
6. **Notion 集成**：25+ 块类型渲染 + 关系解析 + 图片本地化
