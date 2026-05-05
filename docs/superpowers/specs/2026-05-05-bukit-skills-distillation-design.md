# Bukit → Agent Skills 蒸馏设计

## 目标

将 Bukit 静态站点生成器的专业知识蒸馏为 **8 个** Trae IDE `.skill` 文件，让任何兼容的 AI Agent（Trae/Claude Code、Codex CLI、Copilot CLI、Gemini CLI）都能成为"Bukit 专家"，覆盖配置、主题、模板、内容源、路由、国际化、插件调试、CLI 执行八大领域。

## 范围

- 在 `src/skills/` 目录下新增 8 个技能文件（每个对应一个 Bukit 子系统或操作域）
- 每个技能遵循 `writing-skills` 中定义的 SKILL.md 标准结构
- 纯知识蒸馏 + CLI 操作指引，不包含可执行代码或 MCP 服务器
- 全部使用中文编写（与用户语言一致）
- 采用平台无关的工具引用方式，兼容 Codex/Copilot/Gemini（遵循 `using-superpowers` 的跨平台适配模式）

## 不做的事

- 不创建 MCP 服务器
- 不修改 Bukit 源码
- 不新增 CLI 命令或工具脚本
- 不覆盖 Bukit 的测试体系

## 跨平台 Agent 兼容性设计

### 已有的兼容基础设施

`using-superpowers` 技能已建立跨平台工具映射体系：

| Agent 平台 | Skill 加载方式 | Shell 工具 | 文件工具 | 搜索工具 |
|-----------|---------------|-----------|---------|---------|
| **Trae/Claude Code** | `Skill` 工具 | `Bash` | `Read`/`Write`/`Edit` | `Grep`/`Glob` |
| **Codex CLI** | 原生加载，直接遵循指令 | 原生 shell 工具 | 原生文件工具 | 原生搜索 |
| **Copilot CLI** | `skill` 工具 | `bash` | `view`/`create`/`edit` | `grep`/`glob` |
| **Gemini CLI** | `activate_skill` 工具 | `run_shell_command` | `read_file`/`write_file` | `grep_search`/`glob` |

### Bukit Skills 的兼容策略

1. **不硬编码工具名**：不说"用 Bash 工具执行"，说"执行以下命令"
2. **不假设平台特性**：命令示例给出 `bukit`（跨平台统一），Agent 自行适配后缀（Windows 下 `bukit.exe`）
3. **Skill 文件为纯 Markdown**：Codex 原生加载，Copilot 通过 `skill` 工具发现，Gemini 通过 `activate_skill` 激活
4. **CLI 操作用独立 skill 封装**：`bukit-cli-reference` 作为所有命令操作的单一知识源

### 为什么这 8 个技能不需 MCP

Agent 调用 Bukit CLI 不需要 MCP 中间层，因为：
- Bukit 是单文件可执行文件（NativeAOT 编译），Agent 可直接执行
- 所有命令都是短生命周期（`build` / `init` / `preview`）或一次性操作
- Agent 的原生 Shell 工具（Bash / run_shell_command / bash）已足够
- MCP 的增量价值在于**长时间会话状态管理**，Bukit 不需要

## 背景：为什么选 .skill 文件而非 MCP

| 维度 | .skill 文件方案 | MCP 服务器方案 |
|------|----------------|---------------|
| Bukit CLI 已有极简接口 | AI 直接执行 CLI，skill 提供操作指引 | 通过 AI 中转反而增加延迟 |
| 核心痛点 | 配置编排、模板 DSL、排错、CLI 用法 | 命令执行本身不复杂 |
| 维护成本 | 纯 Markdown，随文档更新 | 需同步 CLI 版本、跨平台兼容 |
| 知识密度 | 高（site.yaml 250+ 行模型、Scriban DSL、i18n 合并等） | 低（CLI 就几个命令） |
| 跨 Agent 移植 | Codex/Copilot/Gemini 原生支持 .skill | MCP 需各平台各自适配 |

**结论：** Bukit 的核心价值在知识层面（如何配置、如何排错）+ 操作指引（CLI 何时执行）。`.skill` 文件是最佳载体。

## 技能拆分策略

### 拆分原则

1. **一个子系统一个技能** — 对应 Bukit 架构中的 1 个模块
2. **职责边界清晰** — 每个技能独立闭环，减少交叉引用
3. **触发条件明确** — `description` 用"Use when..."描述用户会遇到的具体症状
4. **示例驱动** — 每个技能包含至少 2 个真实场景示例

### 8 个技能的职责映射

```
Bukit 架构层               →  Skill 文件              类型
────────────────────────────────────────────────────────────
Bukit.Cli (CLI 入口)        →  bukit-cli-reference     Reference（操作指引）
Bukit.Config + Theme        →  bukit-theme             Pattern（主题模式）
Bukit.Config (配置模型)     →  bukit-config            Technique（配置技法）
Bukit.Rendering (模板)      →  bukit-templating        Technique（模板技法）
Bukit.Content/Notion        →  bukit-notion            Reference（内容源指南）
Bukit.Routing (路由)        →  bukit-routing           Technique（路由技法）
Bukit.Engine (i18n)         →  bukit-i18n              Pattern（多语言模式）
Bukit.Engine/Plugins (插件) →  bukit-plugins-debug     Technique（调试技法）
```

## 各技能详细设计

### 1. `bukit-config` — 站点配置专家

**目录：** `src/skills/bukit-config/SKILL.md`

**触发条件：**
- 用户创建或修改 `site.yaml`
- 用户询问某个配置字段的含义
- 配置验证报错
- 用户想实现某个功能但不知道如何配置

**核心内容：**

| 章节 | 内容 |
|------|------|
| Overview | site.yaml 是 Bukit 的唯一配置入口，采用约定优于配置哲学 |
| 配置模型速查 | Site / Content / Build / Theme / Taxonomy / Logging 六大顶级节点速查表 |
| 常见场景模板 | 个人博客、文档站、多语言站点、知识库 四种完整配置模板 |
| 字段详解 | 关键字段的含义、类型、默认值、示例（重点：permalink 模式、集合定义、i18n 配置） |
| 配置验证 | 常见配置错误及修正方法 |
| CLI 覆盖 | `--override` 参数的用法 |

**关键示例：**
- 最小可用的 site.yaml
- 带分类法和分页的博客配置
- 多语言站点配置

---

### 2. `bukit-theme` — 主题目录结构与静态资源

**目录：** `src/skills/bukit-theme/SKILL.md`

**Skill 类型：** Pattern（主题模式）

**与 `bukit-templating` 的分工：**
- `bukit-theme` → "主题怎么组织？目录结构是什么？CSS 放哪？静态资源怎么引用？"
- `bukit-templating` → "Scriban 语法怎么写？layout 怎么继承？变量怎么访问？"

**触发条件：**
- 用户从零搭建或迁移主题
- 用户询问 layouts/、assets/、static/ 目录的职责
- 静态资源（CSS/JS/图片）引用 404
- 用户询问主题参数（theme.params）的用法
- `bukit init` 生成的默认主题需要自定义

**核心内容：**

| 章节 | 内容 |
|------|------|
| Overview | Bukit 主题 = layouts/ + assets/ + static/ + theme 配置节，四者协同构成完整视觉层 |
| 目录结构 | `layouts/`（模板）、`assets/`（需处理的资源，如 SCSS）、`static/`（直接复制的静态文件）三个目录的职责、约定和输出行为 |
| 主题配置 | site.yaml 中 `theme` 节点的完整用法：name、layouts_dir、assets_dir、static_dir、params 五个字段 |
| 主题参数 | `theme.params` 的定义语法、类型支持、在 Scriban 模板中通过 `site.theme.params.xxx` 访问 |
| 静态资源 | CSS/JS/图片/字体的组织方式，构建时的复制策略，引用路径规则 |
| 从零搭建 | 完整主题创建流程：创建目录 → 配置 theme 节 → 编写 base layout → 创建页面模板 → 添加 CSS → 验证构建 |
| 默认主题解剖 | `bukit init` 生成的主题结构解读，如何在其基础上修改 |
| 主题迁移 | 从默认主题逐步迁移到自定义主题的策略 |
| 常见错误 | 资源路径错误（绝对 vs 相对）、static 与 assets 混淆、主题参数未传递、layouts_dir 配置后模板找不到 |

**关键示例：**
- 最小自定义主题的完整目录树 + site.yaml theme 配置
- 带 SCSS 编译的自定义主题
- 主题参数（如 `primary_color`、`footer_text`）定义和模板引用

---

### 3. `bukit-templating` — Scriban 模板开发

**目录：** `src/skills/bukit-templating/SKILL.md`

**触发条件：**
- 用户编写或修改 Scriban 模板
- 模板渲染报错或输出不符合预期
- 用户询问如何在模板中访问数据、循环、条件判断
- layout 继承不生效

**核心内容：**

| 章节 | 内容 |
|------|------|
| Overview | Bukit 使用 Scriban 模板引擎，支持 `{% layout %}` 继承和 `include` 局部模板 |
| 模板文件布局 | layouts/ 目录结构约定，模板命名规范 |
| 数据模型 | `page`、`site`、`data` 三大数据对象的字段速查 |
| Layout 继承 | `{% layout "base" %}` 指令用法，内容块 `{% block %}` 机制 |
| 常用模式 | 列表页模板、单页模板、首页模板、分类页模板、分页模板 |
| 自定义函数 | Bukit 注入的 Scriban 扩展函数 |
| 常见错误 | 模板路径错误、变量未定义、循环引用、编码问题 |

**关键示例：**
- 带 layout 继承的完整页面模板
- 列表页 + 分页组件模板
- 多语言模板的条件渲染

---

### 4. `bukit-notion` — Notion 内容源配置

**目录：** `src/skills/bukit-notion/SKILL.md`

**触发条件：**
- 用户用 Notion 作为内容源
- Notion 内容拉取失败或数据不完整
- 用户询问属性映射规则
- 图片未下载到本地

**核心内容：**

| 章节 | 内容 |
|------|------|
| Overview | Bukit 通过 Notion API 将数据库页面转换为 ContentItem |
| 前置准备 | Notion Integration 创建、API Key 获取、数据库 ID 获取、连接授权 |
| 属性映射规则 | title / rich_text / url / email / number / checkbox / date / select / multi_select / relation / rollup / formula / files 的映射行为 |
| 块渲染支持 | 20+ 种 Notion 块类型的渲染说明（Code、Callout、Table、Toggle、Column 等） |
| 关联关系处理 | relation 字段的解析与引用 |
| 图片本地化 | 远程图片自动下载的行为和配置 |
| 常见问题 | API 限流、权限不足、属性类型不匹配、图片下载失败 |

**关键示例：**
- 完整的 Notion 数据库 site.yaml 配置
- 属性映射对照表
- 常见错误排查流程

---

### 5. `bukit-routing` — URL 路由与永久链接

**目录：** `src/skills/bukit-routing/SKILL.md`

**触发条件：**
- 用户自定义 URL 结构
- URL 生成不符合预期
- 用户询问 permalink 模式
- 集合路由配置

**核心内容：**

| 章节 | 内容 |
|------|------|
| Overview | Bukit 通过 permalink 模式和集合路由规则生成 URL 和输出路径 |
| Permalink 模式 | `{slug}` / `{year}` / `{month}` / `{day}` / `{type}` 占位符说明 |
| 集合路由 | 每个集合可定义独立的 permalink 和 template |
| URL 编码策略 | none / urlencode / slug / sanitize 四种模式 |
| 路由覆盖 | 通过在 content 元数据中设置 `route` 字段手动指定 URL |
| 输出路径 | 从 RouteInfo 到磁盘路径的映射逻辑 |
| 常见错误 | 路由冲突、permalink 占位符拼写错误、编码导致 404 |

**关键示例：**
- 博客按年月日组织的 permalink 配置
- 文档站扁平化 permalink 配置
- 集合级别覆盖路由的配置

---

### 6. `bukit-i18n` — 多语言站点搭建

**目录：** `src/skills/bukit-i18n/SKILL.md`

**触发条件：**
- 用户创建多语言站点
- 语言切换不生效
- 多语言内容未正确分离
- sitemap/RSS/搜索索引合并问题

**核心内容：**

| 章节 | 内容 |
|------|------|
| Overview | Bukit 通过语言检测和独立变体构建实现多语言站点 |
| 配置模型 | i18n 配置节：语言列表、默认语言、内容策略 |
| 内容组织 | 如何在 Notion/Markdown 中标记内容语言 |
| 构建流程 | 语言检测 → 独立变体构建 → 输出合并 |
| 输出结构 | 按语言分目录 vs 根级别混合的策略 |
| 合并机制 | Sitemap 合并、RSS 合并、搜索索引合并 |
| 模板适配 | 多语言模板的条件渲染、语言切换器组件 |
| 常见问题 | 语言检测失败、内容未正确归属、合并冲突 |

**关键示例：**
- 中英文双语站完整配置
- 语言切换器模板片段
- 根页面语言重定向配置

---

### 7. `bukit-plugins-debug` — 插件系统与构建排错

**目录：** `src/skills/bukit-plugins-debug/SKILL.md`

**触发条件：**
- 插件未生效或行为异常
- 构建输出不符合预期
- 增量构建行为异常
- 用户想开发自定义插件
- 构建性能问题

**核心内容：**

| 章节 | 内容 |
|------|------|
| Overview | Bukit 内置 7 个核心插件 + 支持外部程序集和协议插件 |
| 内置插件速查 | Taxonomy / Sitemap / RSS / SearchIndex / Pagination / Archive / PagesIndex 的功能和配置 |
| 插件注册来源 | BuiltIn / Generated / ExternalAssembly (.dll + SHA256) / ExternalProtocol (WASM/进程) |
| 插件执行顺序 | derivePages → 渲染 → afterBuild 的生命周期 |
| 路由冲突策略 | fail / warn / last-wins 的含义和选择 |
| 增量构建 | SHA256 哈希机制、跳过条件、构建清单格式 |
| 构建排错 | 页面未输出、数据缺失、模板未找到、并发写入冲突 |
| 自定义插件开发 | 实现 IBukitPlugin / IDerivePagesPlugin / IAfterBuildPlugin |
| 性能诊断 | 并行渲染瓶颈、插件耗时分析 |

**关键示例：**
- 自定义分类法配置
- 增量构建未触发的排查流程
- 自定义外部插件最小实现

---

### 8. `bukit-cli-reference` — CLI 命令操作指引（Agent 执行层）

**目录：** `src/skills/bukit-cli-reference/SKILL.md`

**Skill 类型：** Reference（操作指引）

**这是唯一包含 CLI 执行指令的 skill，其他 7 个 skill 引用它而非重复命令。**

**触发条件：**
- Agent 需要执行 Bukit CLI 命令（`build` / `init` / `preview` 等）
- Agent 需要检测 Bukit CLI 是否已安装
- Agent 需要安装或升级 Bukit CLI
- 构建报错需要解读 CLI 输出

**核心内容：**

| 章节 | 内容 |
|------|------|
| Overview | Bukit 是单文件 CLI 工具，Agent 通过原生 Shell 直接执行 |
| CLI 检测 | 如何判断 `bukit` 是否可用（`bukit version` / `bukit --version`），Windows/Linux/macOS 差异 |
| 安装指引 | dotnet tool install、直接下载 release 二进制、从源码构建 三种方式 |
| 命令速查表 | 全部 11 个命令的名称、参数、用途一览表 |
| 关键命令详解 | `init`、`build`、`preview`、`clean`、`doctor` 的完整参数和输出解读 |
| 构建输出解读 | 成功/失败时的典型输出，常见错误信息的含义 |
| 退出码 | 各命令的退出码含义 |
| 预览服务器 | `preview` 的端口选择逻辑、如何打开浏览器预览 |

**Agent 调用流程（平台无关）：**

```
用户说"帮我建一个Bukit博客" 
  → Agent 加载 bukit-cli-reference skill
  → Agent 执行: bukit version  （检测 CLI 是否可用）
  → CLI 不可用 → Agent 引导用户安装
  → CLI 可用   → Agent 执行: bukit init --name "my-blog"
  → Agent 加载 bukit-config skill  → 生成 site.yaml
  → Agent 加载 bukit-templating skill → 编写模板
  → Agent 执行: bukit build
  → 构建成功 → Agent 执行: bukit preview (可选)
```

**跨平台执行注意事项：**

| 场景 | 指引 |
|------|------|
| Windows | 可能需要 `.\bukit.exe` 或 `./bukit.exe`，PowerShell 下建议用 `&` 调用 |
| Linux/macOS | `./bukit`，可能需要 `chmod +x` |
| dotnet tool | 全局安装后可直接 `bukit`（已加入 PATH） |
| 工作目录 | 始终在站点根目录（包含 `site.yaml` 的目录）执行 |
| 输出编码 | 非英语 Windows 下可能有编码问题，建议 `[Console]::OutputEncoding` 设为 UTF-8 |
| 首次构建 | `build` 会创建 `dist/` 目录，首次为全量构建（无增量跳过） |

**关键示例：**
- 完整的新站点初始化 → 配置 → 构建 → 预览流程
- `doctor` 诊断输出示例和解读
- `build` 失败时的典型错误输出和修复对应表

---

## 实现顺序

按用户需求和依赖关系排序：

| 优先级 | Skill | 理由 |
|--------|-------|------|
| P0 | `bukit-cli-reference` | Agent 执行任何操作前必须知道如何调用 CLI |
| P0 | `bukit-config` | 一切配置的入口，其他技能都依赖它 |
| P0 | `bukit-theme` | 主题是站点视觉的骨架，与模板强相关但独立 |
| P0 | `bukit-templating` | 最常用的用户操作，但依赖 theme 提供目录上下文 |
| P1 | `bukit-notion` | Notion 是主要内容源，问题频率高 |
| P1 | `bukit-routing` | URL 结构是站点的骨架 |
| P2 | `bukit-i18n` | 多语言是进阶需求 |
| P2 | `bukit-plugins-debug` | 排错和高级定制 |

## 技能文件质量标准

每个 SKILL.md 必须满足：

1. **Frontmatter 正确：** `description` 以 "Use when..." 开头，仅描述触发条件
2. **示例驱动的：** 至少 2 个完整的配置/代码/命令示例
3. **有常见错误章节：** 3 个以上常见错误及修复方法
4. **可独立使用：** 不强制要求阅读其他技能文件（可交叉引用但不依赖）
5. **遵循 TDD 流程：** 写前先构思"这个技能要解决的 3 个用户问题"
6. **CLI 操作收敛：** 除 `bukit-cli-reference` 外，其他 7 个 skill 不包含 CLI 执行指令，改为引用 `bukit-cli-reference`
7. **平台无关引用：** 说"执行以下命令"而非"用 Bash 工具执行"，命令示例用 `bukit` 而非 `bukit.exe`

## 各 Skill 间的引用关系

```
bukit-cli-reference  ←── 所有其他 skill 引用它（CLI 操作单一知识源）
bukit-config         ←── bukit-theme, bukit-notion, bukit-i18n, bukit-routing 引用它（配置是基础设施）
bukit-theme          ←── bukit-templating 引用它（模板是主题的子集，theme 提供目录上下文）
bukit-templating     ←── 独立（Scriban 语法自成一派，但依赖 theme 理解目录布局）
bukit-plugins-debug  ←── 独立（但引用 bukit-config 的插件配置节）
```

## 验收标准

- 8 个 SKILL.md 文件全部创建在 `src/skills/bukit-*/` 目录下
- 每个文件的结构符合 `writing-skills` 中定义的标准
- 每个文件的 `description` 通过触发条件语义检查（不只概括内容）
- 所有示例代码和配置片段与当前 Bukit 源码一致
- `bukit-cli-reference` 中的命令和参数与 Bukit.Cli 源码一致
- 跨平台执行注意事项覆盖 Windows/Linux/macOS 三大平台
- 除 CLI 技能外，其余技能不包含命令执行指令
