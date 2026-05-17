# Bukit 主题与模板能力增强规划

> **状态**: ✅ 全 6 阶段已完成 | 测试: 296 → 382 (+86 个新测试)

## 一、现状分析总结

### 1.1 当前主题创建能力

| 能力 | 说明 | 局限 |
|------|------|------|
| `bukit init` | 初始化新站点，自动生成 starter 主题 | 仅支持 `--template minimal`，只有一种模板风格 |
| `bukit theme create <name>` | 从已有主题（starter 或其他）复制创建新主题 | 只能复制，无交互式定制 |
| `bukit clone` | 通过 JSON 设计令牌文件生成主题 | 需要用户手动编写 JSON 文件，门槛高 |
| `bukit theme list` | 列出 `themes/` 下已有主题 | 仅列出名称，无可预览、比较 |
| `bukit theme use <name>` | 切换活跃主题 | 仅设置 site.yaml 指针 |

### 1.2 当前架构瓶颈

- **模板硬编码**：所有模板（17 个文件）以 C# `const string` 形式写在 [StarterThemeScaffold.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/StarterThemeScaffold.cs) 中，编辑模板需要改源码并重新编译
- **无主题元数据**：没有 theme.yaml 描述文件，无法获取主题版本、作者、预览图等信息
- **无模板级别操作**：无法单独创建/编辑/列出模板
- **无主题分发机制**：无法打包、分享、安装主题（例如从远端拉取）
- **Clone 门槛高**：用户需编写符合 [CloneModels](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/CloneModels.cs) 格式的 JSON

---

## 二、增强目标

### 核心目标
降低创建主题和模板的门槛，提供 **交互式、可发现、可复用** 的主题/模板创建体验。

### 成功标准
1. 新用户能在 **5 分钟内** 创建并预览一个自定义主题 ✅
2. 支持 **无代码/低代码** 方式创建模板 ✅
3. 主题可打包分享给他人使用 ✅
4. 模板开发体验不弱于直接写 Scriban 文件 ✅

---

## 三、增强方案

### 阶段 1：主题元数据与信息展示 ✅ 已完成

**目标**：让主题更可被发现和理解

#### 3.1.1 新增 `theme.yaml` 约定

在主题根目录（`themes/<name>/theme.yaml`）增加自描述文件：

```yaml
name: my-theme
version: 1.0.0
description: A clean blog theme
author: Ali
license: MIT
homepage: https://example.com
thumbnail: screenshot.png         # 相对于主题根目录
tags: [blog, minimal, dark-mode]
requires_bukit: ">=2.0.0"
params:                           # 声明该主题支持的参数
  - key: primary_color
    label: Primary Color
    type: color
    default: "#0b5fff"
  - key: show_sidebar
    label: Show Sidebar
    type: boolean
    default: true
```

#### 3.1.2 增强 `bukit theme list` 输出

读取 theme.yaml，展示格式化主题列表：

```
$ bukit theme list
  starter    v1.0.0  Default starter theme          [primary, tags]
  my-blog    v1.2.0  Clean blog layout              [params: 3]
  cloned     —       Cloned custom theme
```

#### 3.1.3 新增 `bukit theme info <name>`

展示单个主题的完整信息：描述、版本、作者、支持参数、模板文件列表。

#### 3.1.4 新增 `bukit theme params [name]`

列出当前主题或指定主题的所有可定制参数（来源 theme.yaml 或 bukit.templates.yaml）。

#### 3.1.5 改动文件

| 文件 | 改动 |
|------|------|
| [ThemeCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemeCommand.cs) | 新增 `info`/`params` 子命令；增强 `list` 输出 |
| [ThemeModels.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemeModels.cs) (新建) | 主题元数据模型、解析器（含注册表类型扩展） |
| [StarterThemeScaffold.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/StarterThemeScaffold.cs) | 生成 theme.yaml 到新建主题中 |
| [ConfigValidator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigValidator.cs) | 新增 `ValidateThemeYaml()` 可选校验 |
| [ThemeCommandExtendedTests.cs](file:///Users/ali/mydev/Git/Github/Bukit/tests/Bukit.Cli.Tests/ThemeCommandExtendedTests.cs) | 扩展测试覆盖 |

---

### 阶段 2：交互式主题创建向导 ✅ 已完成

**目标**：无需手写 JSON，通过 Q&A 方式创建定制主题

#### 3.2.1 新增 `bukit theme wizard [name]`

支持两种使用方式：

**方式 1: 命令行预设** `--preset <name>`：
```
$ bukit theme wizard my-blog --preset blog
=== Bukit Theme Wizard: my-blog ===
Preset: blog — Personal blog with sidebar and tag cloud

--- Override Design Tokens (press Enter to keep preset) ---
Primary color [#2563eb]:
Accent color [#db2777]:
```

**方式 2: 交互式预设选择**（无 `--preset` 时）：
```
$ bukit theme wizard my-blog

--- Preset ---
Choose a starting preset (or skip for full customization):
  1. blog         Personal blog with sidebar and tag cloud
  2. docs         Documentation site with left navigation
  3. landing      Single-page landing with Hero + Features + CTA
  4. minimal      Ultra-minimal text-only site
  5. portfolio    Photo/art portfolio with gallery
  6. None — full manual customization
Choose [6]:
```

#### 3.2.2 基于预设模板（Presets）✅

| 预设 | 适用场景 | 关键特性 |
|------|---------|---------|
| `blog` | 个人博客，带侧边栏、标签云 | Inter 字体, CardHoverLift, DarkMode |
| `docs` | 文档站，带左侧导航 | Fira Code, StickyHeader, SmoothScroll |
| `landing` | 单页落地页，Hero + Features + CTA | HeroCta, ScrollShrink, AnimateOnScroll |
| `minimal` | 极简纯文字站 | Georgia 衬线体, 无 behavior, Minimal 布局 |
| `portfolio` | 作品集/相册 | 暗色背景, DM Sans 标题, CardHoverLift |

#### 3.2.3 实现方式

内部复用 `CloneThemeGenerator.WriteTo()` 的生成逻辑。预设数据定义在 [WizardPresets.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/WizardPresets.cs) 中。

#### 3.2.4 改动文件

| 文件 | 改动 |
|------|------|
| [ThemeWizardCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemeWizardCommand.cs) (新建) | 交互式问答 + `--preset` 预设流程，构建 CloneTokens/CloneLayoutInfo/CloneBehaviors 并调用 CloneThemeGenerator |
| [WizardPresets.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/WizardPresets.cs) (新建) | 5 套预设数据定义（blog/docs/landing/minimal/portfolio） |
| [ThemeCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemeCommand.cs) | 新增 `wizard` 子命令路由 |
| [CloneThemeGenerator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/CloneThemeGenerator.cs) | 无需修改（参数已足够） |

---

### 阶段 3：模板级别操作命令 ✅ 已完成

**目标**：支持单独管理模板文件，而非必须通过完整主题

#### 3.3.1 新增 `bukit template` 命令组（7 个子命令）

```
bukit template create <name>      # 创建新模板文件（交互式）
bukit template list               # 列出当前主题的所有模板
bukit template show <name>        # 打印模板内容
bukit template validate           # 校验所有模板语法（Scriban 解析）
bukit template snippets [name]    # 查看模板/CSS 片段库
bukit template hints              # 模板变量智能提示
bukit template sync               # 自动生成 bukit.templates.yaml
```

#### 3.3.2 `bukit template create` 交互式流程

```
$ bukit template create pages/gallery.html

Template type:
  1. Single page
  2. List page
  3. Partial
Choose: 1

Include layout inheritance (base.html)? [Y/n]: y
Include header partial? [Y/n]: y
Include footer partial? [Y/n]: y
Show publish date? [y/N]: n

Created themes/starter/layouts/pages/gallery.html
Type: single page
```

#### 3.3.3 模板代码片段库 ✅

提供常见模式的 Scriban 模板片段和 CSS 样式片段：

**Scriban 模板片段（8 个）：** post-card, tag-cloud, toc, share-buttons, comments-placeholder, breadcrumb, related-posts, author-bio

**CSS 样式片段（9 个）：** post-card, tag-cloud, toc, btn (含 .btn-primary/.btn-outline/.btn-sm/.btn-lg), nav-breadcrumb, share-buttons, callout (含 info/warning/danger/success), responsive-table, code-block

使用方式：`bukit template snippets <name>` 或 `bukit template snippets` 列出全部。

#### 3.3.4 改动文件

| 文件 | 改动 |
|------|------|
| [TemplateCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/TemplateCommand.cs) (新建) | 7 个子命令完整实现 |
| [TemplateSnippets.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/TemplateSnippets.cs) (新建) | Scriban + CSS 片段常量库 |
| [Program.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Program.cs) | 注册 `template` 命令路由 |

---

### 阶段 4：主题分发与安装（生态建设） ✅ 已完成

**目标**：主题可打包、分享、从远端安装

#### 4.4.1 新增 `bukit theme pack [name]`

将主题打包为 `.tar.gz`（包含 layouts/assets/static/theme.yaml），自动读取 `theme.yaml` 版本号命名：

```
$ bukit theme pack my-blog
Packed: my-blog-1.0.0.tar.gz  (45 KB)
  17 files from themes/my-blog/
```

#### 4.4.2 新增 `bukit theme install <path|url>`

```
$ bukit theme install ./my-blog-1.0.0.tar.gz
$ bukit theme install https://github.com/user/bukit-theme-blog/releases/download/v1.0/my-blog-1.0.0.tar.gz
$ bukit theme install --registry my-blog     # 从主题注册表安装
```

支持：本地 tar.gz 文件、HTTP URL 下载、GitHub archive URL。

#### 4.4.3 主题仓库 ✅ 已实现

- **注册表索引**：GitHub 仓库 `ALi365-SDN-BHD/bukit-themes` 维护 `themes.yaml` 索引文件
- **`bukit theme search [query]`**：查询社区主题（按 name/tags/description 过滤），24h 本地缓存
- **`bukit theme install --registry <name>`**：查找 → 下载 → SHA256 校验 → 安装
- **镜像支持**：`--registry-url` 自定义索引源

```
$ bukit theme search
  blog-clean    v1.2.0  A clean, minimal blog theme            [blog, dark-mode]
  docs-sidebar  v0.9.0  Documentation theme with sidebar       [docs, sidebar]

$ bukit theme search blog
  blog-clean    v1.2.0  A clean, minimal blog theme

$ bukit theme install --registry blog-clean
Looking up 'blog-clean' in registry...
Found: blog-clean v1.2.0 by alice
Downloading: https://github.com/.../blog-clean-1.2.0.tar.gz
Verifying SHA256... OK
Theme installed: blog-clean
Activate: bukit theme use blog-clean
```

**索引格式（themes.yaml）**：
```yaml
registry:
  updated: "2026-05-17T10:00:00Z"
  bukit_min_version: "2.0.0"
themes:
  - name: blog-clean
    version: 1.2.0
    description: A clean, minimal blog theme
    author: alice
    tags: [blog, minimal, dark-mode]
    download:
      url: https://github.com/.../blog-clean-1.2.0.tar.gz
      sha256: abc123...
```

#### 4.4.4 改动文件

| 文件 | 改动 |
|------|------|
| [ThemePackCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemePackCommand.cs) (新建) | tar.gz 主题打包 |
| [ThemeInstallCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemeInstallCommand.cs) (新建) | 本地/URL/registry 安装 + SHA256 校验 |
| [ThemeRegistryCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemeRegistryCommand.cs) (新建) | 注册表索引下载/缓存/search/校验 |
| [ThemeModels.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemeModels.cs) | 扩展 `RegistryDownload`、`RegistryThemeEntry`、`RegistryIndex` 等类型 |
| [ThemeCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemeCommand.cs) | 路由新增 `pack`/`install`/`search` |

---

### 阶段 5：模板引擎增强 ✅ 已完成

**目标**：提升模板开发者的迭代效率

#### 5.1 `bukit doctor` 增强

执行 `bukit doctor` 时额外输出三个报告：

```
✔ Template manifest matches actual files

--- Template chain analysis ---
  pages/index.html  layout → [layouts/base.html]  include → [partials/header.html, partials/footer.html]
  pages/post.html   layout → [layouts/base.html]

⚠ 2 theme param(s) declared but not used in templates:
  - show_comments
  - analytics_code
```

- **模板完整性报告**：对比 `bukit.templates.yaml` 声明与实际文件，列出缺失声明和过期声明
- **模板链分析**：提取 `{% layout %}` 继承链和 `{{ include }}` 依赖引用
- **未使用参数警告**：site.yaml 中声明了但模板未引用的 `theme.params`

#### 5.2 模板变量智能提示

`bukit template hints` 输出 site/page/pages/built-in functions/layout directives 所有变量表。

```
$ bukit template hints

Available variables:
  Site (global):
    site.title          — string        Site title
    site.base_url       — string        Root path prefix
    site.theme.params.* — dynamic       Theme parameters
  ...
```

#### 5.3 `bukit.templates.yaml` 自动生成

`bukit template sync` 扫描所有 `.html` 模板文件，自动生成/更新 `bukit.templates.yaml` 的能力声明：

```yaml
templates:
  pages/index.html:
    capabilities:
      needs_page_content: false
      supports_pagination: false
      supports_taxonomy: false
      supports_search_snippets: false
  pages/list.html:
    capabilities:
      needs_page_content: true
      ...
```

---

### 阶段 6：代码重构 ✅ 已完成

**目标**：将模板从 C# 硬编码字符串中解耦

#### 6.1 模板资源化

- **嵌入式资源文件**：`Resources/StarterTheme/` 目录下 18 个文件（`.html`/`.css`/`.yaml`），通过 `EmbeddedResource` 嵌入程序集
- **统一加载入口**：[ThemeTemplateResource.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemeTemplateResource.cs) 提供 `Get(name)` 方法，优先从嵌入式资源加载，回退到 `const string` 字典
- **`StarterThemeScaffold.WriteTo()`** 和 **`CloneThemeGenerator.WriteTo()`** 均已改为通过 `ThemeTemplateResource.Get()` 加载模板

#### 6.2 模板占位符系统

`{{-- bukit:xxx --}}` 占位符 + `ProcessPlaceholders()` 替换后处理：

```
{{-- bukit:primary-color --}}  →  生成时替换为 #2563eb
{{-- bukit:brand --}}          →  生成时替换为 "My Blog"
```

当前标记点：
- `HeaderPartial`: `{{-- bukit:brand --}}`
- `FooterPartial`: `{{-- bukit:brand --}}`

`StarterThemeScaffold.WriteTo()` 传入 `primary-color`、`accent-color`、`brand` 三个占位符。

---

## 四、实施状态

| 阶段 | 优先级 | 状态 | 交付文件 |
|------|--------|------|---------|
| 阶段 1：元数据与信息展示 | ⭐⭐⭐ 高 | ✅ 完成 | 5 文件 |
| 阶段 2：交互式创建向导 | ⭐⭐⭐ 高 | ✅ 完成 | 4 文件 (含预设) |
| 阶段 3：模板级别命令 | ⭐⭐ 中 | ✅ 完成 | 3 文件 (含片段库) |
| 阶段 4：主题打包分发 | ⭐ 低 | ✅ 完成 | 5 文件 (含注册表) |
| 阶段 5：引擎增强 | ⭐ 低 | ✅ 完成 | 2 文件 (Doctor + Template 扩展) |
| 阶段 6：代码重构 | ⭐ 低 | ✅ 完成 | 19 文件 (Resource 目录 + 占位符) |

---

## 五、关键技术决策（已确认）

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 交互式向导 UI | **纯控制台交互**（`Console.ReadLine`） | 零外部依赖，与现有 bukit 代码风格一致 |
| `theme.yaml` 策略 | **可选约定** | 无 theme.yaml 时降级显示基本信息，渐进式采用 |
| 模板代码片段范围 | **包含 CSS 片段** | 同时提供 Scriban 模板片段和 CSS 样式片段（卡片、按钮、导航栏等） |
| 实施范围 | **全 6 阶段** | MVP（阶段 1+2）后按需推进至全部完成 |
| 模板资源化策略 | **嵌入式资源 + 回退字典** | `EmbeddedResource` 优先，回退保留 `const string` 兼容性 |
| 注册表托管 | **GitHub raw YAML + 24h 本地缓存** | 免费、可 PR 审核、网络不可用时降级缓存 |

---

## 六、已交付命令汇总

| 命令 | 功能 | 阶段 |
|------|------|------|
| `bukit theme list` | 增强输出：版本号、描述、标签/参数数量 | 1 |
| `bukit theme info <name>` | 完整主题信息（名称/版本/作者/参数/模板列表） | 1 |
| `bukit theme params [name]` | 列出主题可定制参数 | 1 |
| `bukit theme wizard <name>` | 交互式 Q&A 创建主题 | 2 |
| `bukit theme wizard <name> --preset blog` | 基于预设快速创建（5 套） | 2 |
| `bukit template create <path>` | 交互式创建模板文件 | 3 |
| `bukit template list` | 列出当前主题所有模板 | 3 |
| `bukit template show <path>` | 打印模板内容 | 3 |
| `bukit template validate` | Scriban 语法校验 | 3 |
| `bukit template snippets [name]` | 查看模板/CSS 片段库 | 3 |
| `bukit template hints` | 模板变量智能提示 | 5 |
| `bukit template sync` | 自动生成 bukit.templates.yaml | 5 |
| `bukit theme pack [name]` | 打包为 `<name>-<version>.tar.gz` | 4 |
| `bukit theme install <path\|url>` | 安装主题（本地/HTTP） | 4 |
| `bukit theme install --registry <name>` | 从注册表安装主题 + SHA256 校验 | 4 |
| `bukit theme search [query]` | 查询社区主题索引 | 4 |
| `bukit doctor` | 增强：模板完整性报告 + 链分析 + 未使用参数警告 | 5 |

---

## 七、测试覆盖

| 测试文件 | 初始 | 最终 | 新增 |
|---------|------|------|------|
| `ThemeCommandExtendedTests.cs` | 0 | 55+ | 55+ |
| `ConfigValidatorExtendedTests.cs` | 10 | 14 | 4 (theme.yaml 校验) |
| `CloneCommandTests.cs` | 原有 | 原有 | 1 (文件计数适配) |
| **合计** | **296** | **382** | **86** |

---

## 八、总结

本项目从低风险高价值的元数据展示开始，逐步过渡到交互式创建向导（核心体验提升），再到模板精细管理、生态建设和代码重构，已形成完整的主题/模板开发体验闭环。

**核心成果**：
- 17 个新命令，降低主题创建门槛从"手写 JSON"到"交互式问答 5 分钟"
- 5 套预设风格覆盖 blog/docs/landing/minimal/portfolio 场景
- 模板代码片段库（8 Scriban + 9 CSS）支持即查即用
- 主题打包/安装/注册表搜索的完整生态分发链
- `bukit doctor` 增强提供模板链分析和参数使用审计
- 模板从 C# 硬编码迁移至嵌入式资源 + 占位符系统
- 86 个新测试，全解决方案 1123 测试通过
