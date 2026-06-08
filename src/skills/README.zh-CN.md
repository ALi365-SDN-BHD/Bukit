# Bukit Agent Skills

`src/skills/` 存放的是面向 AI Agent 的 Bukit 专项知识与操作指引，而不是运行时代码。它把 Bukit 的常见任务拆成一组可组合的 `SKILL.md` 文件，帮助 Agent 在建站、配置、主题开发、内容接入和排障时快速选对知识边界。

如果你在 Trae、Claude Code、Copilot CLI、Codex CLI、Gemini CLI 等支持 skill 的环境中使用 Bukit，建议把这里当作 Agent 侧的"导航层"：

- 明确提到"using bukit / 使用 bukit"时，先进入 `using-bukit`
- 需要执行命令时，统一参考 `bukit-cli-reference`
- 需要改 `site.yaml`、主题、模板、Notion、路由、多语言或插件时，再进入对应子 skill

## 目录结构

```text
src/skills/
  using-bukit/            # 统一入口与路由
  bukit-cli-reference/    # CLI 操作单一知识源
  bukit-config/           # site.yaml 配置模型
  bukit-theme/            # 主题目录、静态资源、创建向导、分发生态
  bukit-templating/       # Scriban 模板开发
  bukit-design-tokens/    # CSS 变量、调色板、排版尺度、间距系统、深色模式
  bukit-content-to-template/  # 从内容 Schema 生成精准模板
  bukit-notion/           # Notion 内容源
  bukit-routing/          # URL 路由与 permalink
  bukit-i18n/             # 多语言站点
  bukit-plugins-debug/    # 插件、增量构建与排障
  bukit-deploy/           # GitHub Pages 部署
  bukit-clone/            # 网站设计克隆 → Bukit 主题
  bukit-import/           # 本地 HTML demo 导入 → Bukit 站点草稿
  bukit-seo/              # 传统搜索引擎优化 (SEO)
  bukit-geo/              # 生成式引擎优化 (GEO)
```

## Skills 分工

| Skill | 主要职责 | 适用场景 |
|---|---|---|
| `using-bukit` | Bukit skill 总入口，识别任务并路由到子 skill | 用户明确说"using bukit / 使用 bukit"，或任务已确定采用 Bukit |
| `bukit-cli-reference` | CLI 检测、安装、命令速查、输出与退出码解读 | 需要执行 `bukit build`、`doctor`、`preview`、`theme`、`webhook` 等命令 |
| `bukit-config` | `site.yaml` 六大顶级节点、场景模板、字段解释 | 创建或修改站点配置、解释字段含义、修复配置校验错误 |
| `bukit-theme` | `layouts/`、`assets/`、`static/` 的分工、wizard 创建、主题分发 (pack/install)、注册表搜索（Experimental）、模板片段 | 通过 wizard/preset 创建主题、列出主题信息/参数、打包分享主题、从 Experimental 注册表安装、浏览模板片段 |
| `bukit-templating` | Scriban 语法、layout 继承、数据访问与常见模板模式 | 编写页面模板、列表页、分页组件、排查模板渲染错误 |
| `bukit-design-tokens` | 主题设计令牌体系：CSS 变量、调色板、排版尺度、间距系统、深色模式 | 建立统一的视觉标识、定义 `:root {}` CSS 变量、配置深色模式、选择配色方案 |
| `bukit-content-to-template` | Schema 驱动模板生成：将 content content model field scope 映射为精准的 Scriban 模板 | 根据 `site.yaml` content model fieldScopes 生成 post/page/list/card 模板，确保每个字段正确渲染 |
| `bukit-notion` | Notion API 接入、字段映射、块渲染、图片本地化 | 用 Notion 做 CMS、排查拉取失败、检查属性映射与图片问题 |
| `bukit-routing` | permalink、集合路由、URL 编码与输出路径 | 自定义 URL 结构、解决路由冲突或 404、配置集合列表页 |
| `bukit-i18n` | 语言检测、独立变体构建、合并 sitemap/RSS/search | 搭建多语言站点、排查语言切换与输出合并问题 |
| `bukit-plugins-debug` | 插件生命周期、增量构建、性能诊断与常见故障排查 | 插件不生效、构建结果异常、构建性能退化 |
| `bukit-deploy` | GitHub Pages 部署，site.yaml deploy 配置、环境变量、CI/CD 集成 | 部署站点、推送 gh-pages、配置 CNAME、排查部署失败 |
| `bukit-clone` | 浏览器 MCP 提取 → `bukit clone` CLI → 验证流水线，将任意网站视觉设计克隆为 Bukit 主题 | 克隆网站外观、复刻设计、从现有网站创建主题 |
| `bukit-import` | 本地 HTML demo 导入、seed 审核、`import-report.md` 与可选 Notion seed 推送 | 把离线 HTML demo 目录转换成 Bukit 主题/站点草稿 |
| `bukit-seo` | 传统 SEO 配置、inject/theme 渲染模式、front matter SEO 字段、6 种 JSON-LD 类型、构建诊断 (11 码)、构建后审计 (~40 码)、CLI seo audit/diff | 配置 SEO、运行 seo audit/diff、解读 SEO 诊断码、设置 OG/Twitter/JSON-LD/sitemap |
| `bukit-geo` | 面向 AI 搜索引擎的优化：llms.txt/llms-full.txt 生成、AI 爬虫 robots.txt 规则、FAQ/HowTo 结构化数据、GEO Score (7 诊断码) | 优化 AI 搜索 (ChatGPT Search/Perplexity/Google AI Overviews)、生成 llms.txt、添加 FAQ/HowTo schema、运行 geo audit |

## 加载与依赖规则

这些 skill 的设计重点是"边界清晰、组合使用"，因此推荐遵循以下顺序：

1. 入口优先：当任务已经明确是 Bukit 任务时，先看 `using-bukit`
2. 命令单一来源：凡是需要执行命令，都以 `bukit-cli-reference` 为准，其他 skill 不重复维护命令说明
3. 配置作为背景知识：`bukit-theme`、`bukit-design-tokens`、`bukit-content-to-template`、`bukit-notion`、`bukit-routing`、`bukit-i18n`、`bukit-plugins-debug`、`bukit-import`、`bukit-seo`、`bukit-geo` 都建立在 `bukit-config` 的配置模型之上
4. 主题先于模板：`bukit-templating` 默认依赖 `bukit-theme` 提供目录结构与资源约定
5. 设计令牌：当目标涉及视觉一致性时加载 `bukit-design-tokens`——提供调色板、排版尺度和深色模式方案
6. Schema 转模板：当需要根据content model fieldScopes 生成模板时加载 `bukit-content-to-template`——桥接 schema 字段定义与 Scriban 代码
7. SEO 分工：传统 SEO 加载 `bukit-seo`，AI 搜索引擎优化加载 `bukit-geo`——它们共享 `site.seo` 配置但面向不同受众

可以把它理解成一条常见工作流：

```text
using-bukit
  -> bukit-cli-reference
  -> bukit-config
  -> bukit-theme / bukit-design-tokens / bukit-notion / bukit-routing / bukit-i18n / bukit-plugins-debug
  -> bukit-templating / bukit-content-to-template
```

## 使用说明

### 文件布局

```
src/skills/
├── CLAUDE.md                    ← Claude Code Agent 完整入口
├── AGENTS.md                    ← Codex CLI Agent 完整入口
├── GEMINI.md                    ← Gemini CLI Agent 完整入口
├── copilot-instructions.md      ← Copilot CLI 完整入口
│
├── plugin.json                  ← Claude Code / Copilot 插件清单
├── skills-index.yaml            ← 机器可读技能目录（单一真源）
├── skills-index.json            ← JSON 版索引（从 YAML 自动生成）
│
├── using-bukit/SKILL.md         ← 网关技能：总入口，路由到子技能
├── bukit-*/SKILL.md             ← 19 个领域技能（CLI、配置、主题、模板、导入……）
│
└── scripts/
    ├── validate-skills.sh       ← CI：验证所有技能文件
    └── generate-index-json.sh   ← CI：YAML → JSON 转换
```

仓库根目录也放置了轻量引用文件（`CLAUDE.md`、`AGENTS.md`、`GEMINI.md`、`.github/copilot-instructions.md`），用于满足各平台在根目录查找入口文件的约定，内容指向 `src/skills/` 中的完整版本。

### 各平台使用方式

#### Trae

Trae 通过 `.trae/rules/project_rules.md` 自动发现技能。无需额外配置——当用户提到 Bukit 时，Agent 会通过 `Skill` 工具找到并加载 `using-bukit` 及其子技能。

```bash
# 无需安装。在 Trae 中打开此仓库，直接说：
"using bukit，帮我建一个博客"
```

#### Claude Code

**方式 A — 项目级加载（自动）：**
根目录的 `CLAUDE.md` 会在会话启动时自动加载，它重定向到 `src/skills/CLAUDE.md`（完整规则）。无需任何操作——在 Claude Code 中打开此仓库即可。

**方式 B — 插件安装（推荐给 Bukit 用户）：**
```bash
# 以 Claude Code 插件方式安装
claude plugins install src/skills

# 或从 GitHub 安装（发布后）
claude plugins install github.com/ALi365-SDN-BHD/Bukit
```

安装后，当你提到任何 Bukit 相关概念时，全部 20 个技能都会通过 `Skill` 工具自动可用。

#### Codex CLI

Codex 原生加载技能文件——没有 `Skill` 工具。根目录的 `AGENTS.md` 会被自动检测，它告诉 Codex 读取 `src/skills/AGENTS.md` 的完整内容。

```bash
# 在 Codex CLI 会话中，直接提到 Bukit：
"帮我配置一个博客的 Bukit site.yaml"

# 子 Agent 分发（需在 ~/.codex/config.toml 中启用 multi_agent = true）：
# Agent 会读取相关 SKILL.md 并作为 spawn_agent 的指令传入。
```

#### Copilot CLI

Copilot 通过 `plugin.json` 发现技能。根目录 `.github/copilot-instructions.md` 重定向到 `src/skills/copilot-instructions.md`。

```bash
# 安装插件
copilot plugin install src/skills

# 然后使用 skill 工具加载
copilot "using bukit，帮我把站点部署到 GitHub Pages"
```

#### Gemini CLI

Gemini CLI 通过 `activate_skill` 激活技能。根目录的 `GEMINI.md` 重定向到 `src/skills/GEMINI.md`，其中列出了所有可用技能和触发关键词。

```bash
# 在 Gemini CLI 会话中，直接提到 Bukit：
"帮我搭建一个中英文双语的 Bukit 站点"
```

### 可编程访问

`skills-index.yaml` 是机器可读的技能目录，可用于：

- **查询技能元数据**：名称、类型、触发条件、依赖关系、用户指南章节对照
- **解析依赖链**：每个技能声明了 `requires` 列表；`workflows` 章节定义了常见任务链
- **生成平台入口**：该目录驱动所有平台入口文件（CLAUDE.md、AGENTS.md 等）

```bash
# 用 yq 解析
yq '.skills[] | select(.type == "gateway") | .name' skills-index.yaml

# 用 python 解析
python3 -c "
import yaml, json
with open('skills-index.yaml') as f:
    data = yaml.safe_load(f)
print(json.dumps(data['workflows'], indent=2))
"
```

### CI 验证

```bash
# 验证所有技能文件
bash src/skills/scripts/validate-skills.sh

# YAML 变更后重新生成 JSON 索引
bash src/skills/scripts/generate-index-json.sh
```

验证脚本检查项：
- Front Matter 完整性（`name` + `description`）
- `description` 以 "Use when…" 开头
- 有 Multilingual Triggers 章节
- 有 Common Errors 章节
- 无硬编码的平台特定工具名
- `plugin.json` 中所有路径指向存在的文件
- `skills-index.yaml` 中的条目与现有 SKILL.md 文件一致

### 快速上手（任意平台）

1. 在你的 AI Agent 中打开此仓库
2. 说：**"using bukit，帮我建一个博客"**
3. Agent 会自动：
   - 读取网关技能（`using-bukit`）
   - 检测 CLI 可用性（`bukit-cli-reference`）
   - 生成 `site.yaml`（`bukit-config`）
   - 创建主题和模板（`bukit-theme` + `bukit-templating`）
   - 构建站点
4. 说：**"bukit dev"** 启动 HMR 开发服务器进行实时预览

---

## 推荐阅读路径

### 场景 1：从零创建站点

1. `using-bukit`
2. `bukit-cli-reference`
3. `bukit-config`
4. `bukit-theme`
5. `bukit-templating`

### 场景 2：接入 Notion 作为内容源

1. `using-bukit`
2. `bukit-notion`
3. `bukit-config`
4. `bukit-cli-reference`

### 场景 3：调整 URL、分类页或列表页

1. `using-bukit`
2. `bukit-routing`
3. `bukit-config`
4. `bukit-templating`

### 场景 4：排查构建异常或插件问题

1. `using-bukit`
2. `bukit-plugins-debug`
3. `bukit-config`
4. `bukit-cli-reference`

### 场景 5：部署到 GitHub Pages

1. `using-bukit`
2. `bukit-deploy`
3. `bukit-config`
4. `bukit-cli-reference`

### 场景 6：配置 SEO 并运行审计

1. `using-bukit`
2. `bukit-seo`
3. `bukit-config`（site.seo 节点）
4. `bukit-cli-reference`（`bukit seo audit` / `bukit seo diff`）

### 场景 7：为 AI 搜索引擎设置 GEO

1. `using-bukit`
2. `bukit-geo`
3. `bukit-config`（site.seo.geo 节点）
4. `bukit-cli-reference`（`bukit geo audit`）

### 场景 8：克隆网站设计

1. `using-bukit`
2. `bukit-clone`
3. `bukit-theme`
4. `bukit-cli-reference`

### 场景 9：创建自定义主题（交互式）

1. `using-bukit`
2. `bukit-theme`（wizard + presets）
3. `bukit-cli-reference`

### 场景 10：从社区注册表安装主题（Experimental）

1. `using-bukit`
2. `bukit-theme`（search + install）
3. `bukit-cli-reference`

### 场景 11：构建设计令牌体系

1. `using-bukit`
2. `bukit-design-tokens`
3. `bukit-theme`
4. `bukit-config`

### 场景 12：从内容 Schema 生成模板

1. `using-bukit`
2. `bukit-content-to-template`
3. `bukit-config`（content model field scope）
4. `bukit-templating`
5. `bukit-design-tokens`（视觉样式）

## 维护约定

为避免 skill 信息和真实实现脱节，维护时建议保持以下规则：

- 每个 skill 固定放在 `src/skills/<skill-name>/SKILL.md`
- `description` 只写"何时触发"，不写泛化介绍
- CLI 指令与执行注意事项只收敛到 `bukit-cli-reference`
- 主题目录、配置字段、CLI 参数要与仓库源码和用户文档保持一致
- 当新增 Bukit 能力时，优先判断应扩充现有 skill，还是新增独立 skill

## 相关文档

- 仓库入口：[`README.zh-CN.md`](../../README.zh-CN.md)
- 用户文档：[`guide/user`](../../guide/user/README.zh-CN.md)
- 开发者文档：[`guide/dev`](../../guide/dev/README.zh-CN.md)
- Skills 设计说明：[`docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md`](../../docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md)
