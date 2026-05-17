# Bukit 主题与模板能力增强规划

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
1. 新用户能在 **5 分钟内** 创建并预览一个自定义主题
2. 支持 **无代码/低代码** 方式创建模板
3. 主题可打包分享给他人使用
4. 模板开发体验不弱于直接写 Scriban 文件

---

## 三、增强方案

### 阶段 1：主题元数据与信息展示（低风险、高价值）

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
| `src/Bukit.Cli/Commands/ThemeCommand.cs` | 新增 `info`/`params` 子命令；增强 `list` 输出 |
| `src/Bukit.Cli/Commands/ThemeModels.cs` (新建) | 主题元数据模型、解析器 |
| `src/Bukit.Cli/Commands/StarterThemeScaffold.cs` | 生成 theme.yaml 到新建主题中 |
| `src/Bukit.Config/ConfigValidator.cs` | 可选的 theme.yaml 校验 |
| `tests/Bukit.Cli.Tests/ThemeCommandExtendedTests.cs` | 扩展测试覆盖 |

---

### 阶段 2：交互式主题创建向导（核心体验提升）

**目标**：无需手写 JSON，通过 Q&A 方式创建定制主题

#### 3.2.1 新增 `bukit theme wizard [name]`

交互式问答流程：

```
$ bukit theme wizard my-blog
Theme Name: my-blog
Description: My personal blog theme
Author: Ali

>> Design Tokens
Primary color (hex): #2563eb
Accent color (hex): #059669
Background color (hex): [#ffffff]
Text color (hex): [#1a1a1a]
Font family: [system-ui]
Border radius (px): [8]

>> Layout
Include hero section? [y/N]: y
Include sidebar? [y/N]: n
Include dark mode toggle? [y/N]: y
Sticky header? [Y/n]: y

>> Features
Include search page? [Y/n]: y
Include taxonomy pages? [Y/n]: y
Include pagination? [Y/n]: y

>> Templates
Select layout style:
  1. Standard (header + content + footer)
  2. Sidebar layout
  3. Minimal (no header)
Choose [1]: 1

Generating theme...
Created themes/my-blog/ — 17 files written.
Use it: bukit theme use my-blog
Preview: bukit preview
```

#### 3.2.2 基于预设模板（Presets）

在 Wizard 中内置几套预设风格（复用 CloneThemeGenerator 生成逻辑）：

| 预设 | 适用场景 |
|------|---------|
| `blog` | 个人博客，带侧边栏、标签云 |
| `docs` | 文档站，带左侧导航 |
| `landing` | 单页落地页，Hero + Features + CTA |
| `minimal` | 极简纯文字站 |
| `portfolio` | 作品集/相册 |

#### 3.2.3 实现方式

内部复用 `CloneThemeGenerator.WriteTo()` 的生成逻辑，用向导收集参数替代 JSON 文件。`CloneTokens`、`CloneLayoutInfo`、`CloneBehaviors` 的字段通过控制台交互逐一填充。

#### 3.2.4 改动文件

| 文件 | 改动 |
|------|------|
| `src/Bukit.Cli/Commands/ThemeWizardCommand.cs` (新建) | 交互式问答流程，构建 CloneTokens/CloneLayoutInfo/CloneBehaviors 并调用 CloneThemeGenerator |
| `src/Bukit.Cli/Commands/ThemeCommand.cs` | 新增 `wizard` 子命令路由 |
| `src/Bukit.Cli/Commands/CloneThemeGenerator.cs` | 可能需要暴露更多参数（已足够） |

---

### 阶段 3：模板级别操作命令

**目标**：支持单独管理模板文件，而非必须通过完整主题

#### 3.3.1 新增 `bukit template` 命令组

```
bukit template create <name>      # 创建新模板文件
bukit template list               # 列出当前主题的所有模板
bukit template show <name>        # 打印模板内容
bukit template validate           # 校验所有模板语法
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
Show tags? [y/N]: n

Created themes/starter/layouts/pages/gallery.html
```

#### 3.3.3 模板代码片段库

提供常见模式的模板片段和 CSS 样式片段：

**Scriban 模板片段：**
- 文章卡片列表
- 标签云
- 文章目录 (TOC)
- 社交分享按钮
- 评论区域占位
- 面包屑导航
- 相关文章推荐

**CSS 样式片段：**
- `.post-card` — 文章卡片样式
- `.tag-cloud` — 标签云样式
- `.toc` — 文章目录样式
- `.btn` / `.btn-primary` — 按钮样式
- `.nav-breadcrumb` — 面包屑导航
- `.share-buttons` — 分享按钮栏
- `.callout` — 提示框样式
- `.responsive-table` — 响应式表格
- `.code-block` — 代码块美化

```html
<!-- snippet: 文章卡片（模板 + CSS） -->
{{-- 模板部分 --}}
<article class="post-card">
  <h2><a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a></h2>
  {{ if p.summary }}<p>{{ p.summary }}</p>{{ end }}
  {{ if p.publish_date }}
    <time>{{ p.publish_date | date.to_string "%Y-%m-%d" }}</time>
  {{ end }}
</article>

{{-- CSS 部分 --}}
/* post-card */
.post-card { padding: 1.5rem; border: 1px solid var(--border); border-radius: var(--radius); }
.post-card h2 { margin: 0 0 0.5rem; }
.post-card time { color: var(--muted); font-size: 0.875rem; }
```

#### 3.3.4 改动文件

| 文件 | 改动 |
|------|------|
| `src/Bukit.Cli/Commands/TemplateCommand.cs` (新建) | 模板命令组实现 |
| `src/Bukit.Cli/Commands/TemplateSnippets.cs` (新建) | 模板代码片段常量 |
| `src/Bukit.Cli/Program.cs` | 注册 `template` 命令路由 |

---

### 阶段 4：主题分发与安装（生态建设）

**目标**：主题可打包、分享、从远端安装

#### 4.4.1 新增 `bukit theme pack [name]`

将主题打包为 `.tar.gz`（包含 layouts/assets/static/theme.yaml）：

```
$ bukit theme pack my-blog
Packed: my-blog-1.0.0.tar.gz  (45 KB)
```

#### 4.4.2 新增 `bukit theme install <path|url>`

从本地打包文件或 GitHub URL 安装主题：

```
$ bukit theme install ./my-blog-1.0.0.tar.gz
$ bukit theme install https://github.com/user/bukit-theme-blog
$ bukit theme install --registry my-blog     # 从主题仓库安装（后续）
```

#### 4.4.3 主题仓库（可选、远期）

在 GitHub 上维护一个 `bukit-themes` 仓库索引，列出社区主题。`bukit theme search` 查询可用主题。

#### 4.4.4 改动文件

| 文件 | 改动 |
|------|------|
| `src/Bukit.Cli/Commands/ThemePackCommand.cs` (新建) | 主题打包 |
| `src/Bukit.Cli/Commands/ThemeInstallCommand.cs` (新建) | 主题安装 |
| `src/Bukit.Cli/Commands/ThemeCommand.cs` | 路由新增 `pack`/`install` |

---

### 阶段 5：模板引擎增强（开发者体验）

**目标**：提升模板开发者的迭代效率

#### 5.1 `bukit doctor` 增强

- **模板完整性报告**：对比 bukit.templates.yaml 声明与实际文件
- **模板链分析**：显示 `{% layout %}` 继承链和 `{{ include }}` 依赖图
- **未使用参数警告**：site.yaml 中声明了但模板未引用的 `theme.params`

#### 5.2 模板变量智能提示（远期）

通过 `bukit template hints` 输出当前模板可用的变量：

```
$ bukit template hints pages/post.html

Available variables:
  page.title          — string
  page.url            — string
  page.content        — string (HTML)
  page.summary        — string | null
  page.publish_date   — DateTime | null
  page.fields.*       — dynamic
  site.title          — string
  site.base_url       — string
  site.theme.params.* — dynamic
```

#### 5.3 `bukit.templates.yaml` 自动生成

`bukit template sync` 扫描所有模板文件，自动生成/更新 bukit.templates.yaml 的能力声明。

---

### 阶段 6：代码重构（长期工程）

**目标**：将模板从 C# 硬编码字符串中解耦

#### 6.1 模板文件化

将 `StarterThemeScaffold.cs` 和 `CloneThemeGenerator.cs` 中的模板字符串提取为嵌入式资源文件 (`.html` 文件放在 `Resources/` 目录，编译时嵌入为 `EmbeddedResource`)。

或者直接维护一套 `.template` 文件，构建时复制到输出。

#### 6.2 模板变量占位

在工具生成的模板中引入占位符，支持后处理替换（类似现有 `ApplyColorOverrides` 但更通用）：

```
{{-- bukit:primary-color --}}  →  生成时替换为实际颜色
{{-- bukit:brand --}}          →  生成时替换为品牌名
```

---

## 四、实施优先级与顺序

| 阶段 | 优先级 | 工作量 | 依赖 | 价值 |
|------|--------|--------|------|------|
| 阶段 1：元数据与信息展示 | ⭐⭐⭐ 高 | 小（~3 天） | 无 | 立即提升主题可发现性 |
| 阶段 2：交互式创建向导 | ⭐⭐⭐ 高 | 中（~5 天） | 阶段 1 | 核心体验飞跃 |
| 阶段 3：模板级别命令 | ⭐⭐ 中 | 中（~4 天） | 阶段 2 | 精细化模板管理 |
| 阶段 4：主题打包分发 | ⭐ 低 | 中（~4 天） | 阶段 1 | 生态建设 |
| 阶段 5：引擎增强 | ⭐ 低 | 大（~6 天） | 阶段 3 | 高级开发者体验 |
| 阶段 6：代码重构 | ⭐ 低 | 大（~8 天） | 阶段 3+5 | 长期维护性 |

**MVP 执行范围：阶段 1 + 阶段 2**，这是最小可行增强路径，约 8 个工作日。

---

## 五、关键技术决策（已确认）

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 交互式向导 UI | **纯控制台交互**（`Console.ReadLine`） | 零外部依赖，与现有 bukit 代码风格一致 |
| `theme.yaml` 策略 | **可选约定** | 无 theme.yaml 时降级显示基本信息，渐进式采用 |
| 模板代码片段范围 | **包含 CSS 片段** | 同时提供 Scriban 模板片段和 CSS 样式片段（卡片、按钮、导航栏等） |
| 实施范围 | **MVP：阶段 1 + 阶段 2** | 优先交付核心价值，后续阶段按需推进 |

---

## 六、验证计划

每个阶段完成后验证：

1. **阶段 1**：
   - `bukit theme list` 展示带元数据的主题列表
   - `bukit theme info starter` 输出完整主题信息
   - `bukit theme params` 列出可用参数
   - 新建主题包含 theme.yaml 文件

2. **阶段 2**：
   - `bukit theme wizard test-theme` 完整 Q&A 流程无报错
   - 生成的主题通过 `bukit doctor` 检查
   - `bukit build` 构建成功，页面正常显示
   - 测试各种输入组合（默认值、非法值、边界值）

3. **阶段 3**：
   - `bukit template create` 生成模板通过 Scriban 语法检查
   - `bukit template list` 正确列出所有模板
   - 模板包含正确的 layout 继承声明

4. **阶段 4**：
   - `bukit theme pack` 生成可解压的 tar.gz
   - `bukit theme install` 安装后可用

---

## 七、总结

本规划从 **低风险高价值的元数据展示** 开始，逐步过渡到 **交互式创建向导**（核心体验提升），再到 **模板精细管理** 和 **生态建设**，形成完整的主题/模板开发体验闭环。

最小可行路径（MVP）：阶段 1 + 阶段 2，约 8 个工作日，即可让用户通过交互式问答在 5 分钟内创建自定义主题。
