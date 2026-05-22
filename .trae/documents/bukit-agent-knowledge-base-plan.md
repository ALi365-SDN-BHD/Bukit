# Bukit Agent 知识库实现计划

> 目标：将 `src/skills/` 打造为 Codex / Trae / Claude Code / Copilot CLI / Gemini CLI 可直接消费的 Bukit Agent 知识库

***

## 一、现状分析

### 1.1 已有资产

| 资产                                                                           | 状态    | 说明                                                                                |
| ---------------------------------------------------------------------------- | ----- | --------------------------------------------------------------------------------- |
| `src/skills/` 18 个 SKILL.md                                                  | ✅ 已完成 | 覆盖 CLI、配置、主题、模板、Notion、路由、i18n、插件、部署、克隆、SEO、GEO、预览、开发、Webhook、设计令牌、Schema→模板、入口路由 |
| `.trae/rules/project_rules.md`                                               | ✅ 已完成 | 声明 `src/skills/<skill-name>/SKILL.md` 为 agent skill，映射到 CLI 子系统                   |
| `.trae/skills/` 13 个开发流程 skill                                               | ✅ 已完成 | 开发工作流技能（brainstorming、TDD、debugging 等），非 Bukit 专属                                 |
| 设计文档 `docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md` | ✅ 已完成 | 原始 9 技能蒸馏设计，现已扩展为 18 技能                                                           |
| 跨平台工具映射参考 `using-superpowers/references/*.md`                                | ✅ 已完成 | Codex、Copilot、Gemini 三平台工具映射表                                                     |

### 1.2 当前技能加载路径

```
用户说 "using bukit" / "使用 bukit"
   → Trae: Skill 工具 → 加载 .trae/skills/.../SKILL.md 或 src/skills/.../SKILL.md
   → Claude Code: Skill 工具 → 需插件或项目配置
   → Codex CLI: 原生读取 SKILL.md → 直接遵循指令
   → Copilot CLI: skill 工具 → 需插件安装
   → Gemini CLI: activate_skill → 需 GEMINI.md 配置
```

### 1.3 核心缺口

| 缺口                                                         | 影响平台                       | 严重程度 |
| ---------------------------------------------------------- | -------------------------- | ---- |
| 缺少 **技能索引/清单文件**（机器可读）                                     | 全部                         | 🔴 高 |
| 缺少 **Claude Code 插件配置**（`plugin.json`）                     | Claude Code                | 🔴 高 |
| 缺少 **根目录入口文件**（`CLAUDE.md` / `AGENTS.md` / `GEMINI.md`）    | Claude Code, Codex, Gemini | 🔴 高 |
| 缺少 **Copilot CLI 指令文件**（`.github/copilot-instructions.md`） | Copilot CLI                | 🟡 中 |
| 技能文件仅中文编写（部分有英文 `description`）                             | Codex, Gemini（ENG 优先）      | 🟡 中 |
| 触发条件仅设计给 Trae `Skill` 工具，未适配其他平台                           | 全部（除 Trae）                 | 🟡 中 |
| 缺少 **知识库版本号/变更日志**                                         | 全部                         | 🟢 低 |
| `using-bukit` 设计为 Trae `Skill` 工具的路由器，其他平台无等效机制            | Codex, Gemini              | 🟡 中 |

***

## 二、目标架构

### 2.1 三层知识库模型

```
┌──────────────────────────────────────────────────────────────┐
│  第三层：平台适配入口 (Platform Entry Points)                   │
│  CLAUDE.md / AGENTS.md / GEMINI.md / copilot-instructions.md  │
│  每个平台一个入口文件，告诉 Agent 何时加载、加载哪个技能           │
├──────────────────────────────────────────────────────────────┤
│  第二层：知识索引层 (Knowledge Index)                           │
│  skills-index.json / skills-index.yaml                        │
│  机器可读的技能清单：名称、描述、触发条件、前置依赖、平台映射      │
├──────────────────────────────────────────────────────────────┤
│  第一层：知识核心层 (Knowledge Core) — src/skills/              │
│  18 个 SKILL.md，单一真源，平台无关，纯 Markdown                 │
└──────────────────────────────────────────────────────────────┘
```

### 2.2 各平台消费路径

```
                    ┌─────────────────────┐
                    │   src/skills/        │
                    │   (Knowledge Core)   │
                    └─────────┬───────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    │ skills-      │  │ 平台入口     │  │ 插件清单     │
    │ index.json   │  │ CLAUDE.md   │  │ plugin.json  │
    │ (可编程查询)  │  │ AGENTS.md   │  │ (发布用)      │
    └──────┬───────┘  │ GEMINI.md   │  └──────┬───────┘
           │          └──────┬───────┘         │
           ▼                 ▼                 ▼
    ┌──────────────────────────────────────────────┐
    │              各 Agent 运行时                    │
    │  Trae: Skill 工具 + project_rules.md           │
    │  Claude Code: Skill 工具 + CLAUDE.md           │
    │  Codex CLI: 原生读取 AGENTS.md + spawn_agent   │
    │  Copilot CLI: skill 工具 + copilot-instructions│
    │  Gemini CLI: activate_skill + GEMINI.md        │
    └──────────────────────────────────────────────┘
```

### 2.3 入口消费时机

```
用户输入 ──→ Agent 启动
               │
               ├─ 读取 CLAUDE.md / AGENTS.md / GEMINI.md（根目录）
               ├─ 检查关键词是否匹配 Bukit（bukit、site.yaml、Scriban、.csproj）
               ├─ 匹配 → 加载 using-bukit（网关技能）
               │         │
               │         └─ 按任务类型路由到子技能
               │
               └─ 不匹配 → 不影响正常运行（零侵入）
```

***

## 三、实施计划

### Phase 0：知识库标准化审计（1 天）

**目标**：确保 18 个 SKILL.md 满足跨平台消费的最低质量标准

#### 0.1 审计清单

对每个 `SKILL.md` 逐一审查：

| # | 检查项                | 标准                                                                  |
| - | ------------------ | ------------------------------------------------------------------- |
| 1 | Front Matter 完整性   | `name` + `description`（英文）必有；`description_zh` / `description_ms` 推荐 |
| 2 | description 触发条件语义 | 以 "Use when..." 开头，描述具体症状/场景，不泛化                                    |
| 3 | 平台无关引用             | 无硬编码工具名（不说 "用 Bash 工具"），命令用 `bukit`（非 `bukit.exe`）                  |
| 4 | CLI 收敛             | 除 `bukit-cli-reference` 外，不包含 CLI 执行指令                              |
| 5 | 多语言触发短语表           | "Multilingual Triggers" 章节，覆盖 zh-CN / en / ms                       |
| 6 | 常见错误章节             | 至少 3 条 Symptom → Cause → Fix 排查项                                    |
| 7 | 示例驱动               | 至少 2 个可复制执行的示例（代码/配置/命令）                                            |

#### 0.2 审计输出

* 每个技能文件标记：PASS / FIX\_NEEDED / MISSING

* 生成 `skills-audit-report.md`（含问题和修正建议）

* 不匹配项纳入 Phase 3 修正

***

### Phase 1：知识索引清单（2 天）

**目标**：创建机器可读的技能索引文件，支持编程式查询和路由

#### 1.1 `src/skills/skills-index.yaml` — 知识清单（单文件）

该文件作为所有平台适配入口的**单一数据源**：

```yaml
# Bukit Agent Skills Index
# Version: 2.0.0
# This file is the machine-readable catalog of all Bukit agent skills.
# It drives platform entry points (CLAUDE.md, AGENTS.md, GEMINI.md, etc.)

version: "2.0.0"
generated: "2026-05-22"

skills:
  - name: using-bukit
    type: gateway
    path: src/skills/using-bukit/SKILL.md
    description: |
      Gateway skill - load FIRST when user mentions bukit. Routes to sub-skills.
      Prevents other SSG skills from loading.
    triggers:
      - pattern: "using bukit|使用 bukit|guna bukit"
        languages: [zh-CN, en, ms]
      - pattern: "bukit.*static site|bukit.*SSG|static site.*bukit"
        languages: [en]
      - pattern: "用 bukit 建站|bukit 静态站点"
        languages: [zh-CN]
      - pattern: "bina laman.*bukit|bukit.*penjana laman"
        languages: [ms]
      - signals: [site.yaml, .csproj, Scriban, bukit init, bukit build]
        type: file_or_command
    requires: []
    conflicts_with: [hugo, jekyll, astro, gatsby, nextra, vitepress, mkdocs]
    platform_loading:
      claude_code: "Skill tool, loaded via CLAUDE.md trigger detection"
      codex: "Native read, triggered by AGENTS.md keyword matching"
      copilot: "skill tool, triggered by copilot-instructions.md"
      gemini: "activate_skill, triggered by GEMINI.md keyword detection"
      trae: "Skill tool, auto-discovered via project_rules.md"

  - name: bukit-cli-reference
    type: reference
    path: src/skills/bukit-cli-reference/SKILL.md
    # ... (每个技能有相同结构)
```

#### 1.2 各技能在索引中的关键字段

| 字段                 | 用途                                                    | 必需 |
| ------------------ | ----------------------------------------------------- | -- |
| `name`             | 唯一标识，对应目录名                                            | ✅  |
| `type`             | gateway / reference / technique / pattern / operation | ✅  |
| `path`             | 相对仓库根路径                                               | ✅  |
| `description`      | 英文简短描述（跨平台一致）                                         | ✅  |
| `triggers`         | 多语言触发模式 + 文件/命令信号                                     | ✅  |
| `requires`         | 前置技能列表                                                | ✅  |
| `conflicts_with`   | 冲突技能（避免同时加载）                                          | 如有 |
| `platform_loading` | 各平台加载方式说明                                             | ✅  |
| `guide_chapter`    | 对应用户指南章节                                              | 推荐 |
| `load_priority`    | 在 required 列表中的相对优先级                                  | 推荐 |

#### 1.3 `src/skills/skills-index.json` — 机器可查询（自动生成）

从 YAML 自动生成的 JSON 版本，供脚本/CI 消费：

```bash
# 生成 JSON 索引（由 CI 或 git pre-commit hook 自动运行）
yq -o json src/skills/skills-index.yaml > src/skills/skills-index.json
```

***

### Phase 2：平台适配入口（3 天）

**目标**：为每个 Agent 平台创建根目录入口文件，告诉 Agent 何时、如何发现并使用 Bukit 技能

#### 2.1 平台入口架构

```
仓库根目录
├── CLAUDE.md              ← Claude Code 入口（原生支持）
├── AGENTS.md              ← Codex CLI 入口（原生支持）
├── GEMINI.md              ← Gemini CLI 入口（原生支持）
├── .github/
│   └── copilot-instructions.md  ← Copilot CLI 入口
├── .trae/
│   └── rules/
│       └── project_rules.md     ← Trae 入口（已存在，需增强）
└── .codex/
    └── config.toml              ← Codex 配置（可选增强）
```

#### 2.2 CLAUDE.md — Claude Code 入口

Claude Code 原生支持 `CLAUDE.md` 作为项目上下文文件，在每次会话启动时自动加载。利用这一机制，我们只需要一个轻量入口：

```markdown
# Bukit Agent Knowledge Base

## Skill Loading Rules

When the user mentions bukit, Bukit site generation, Scriban templates,
site.yaml files, or any related Bukit concepts:

1. **LOAD `src/skills/using-bukit/SKILL.md` FIRST**
   - This is the gateway skill that routes to the correct sub-skill
   - It prevents conflicts with other SSG skills (Hugo, Jekyll, Astro, etc.)

2. **THEN load sub-skills as needed via Skill tool:**
   - `bukit-cli-reference` — for any CLI command execution
   - `bukit-config` — for site.yaml configuration
   - `bukit-theme` — for theme structure and static assets
   - `bukit-templating` — for Scriban template development
   - `bukit-design-tokens` — for CSS variables and design systems
   - `bukit-content-to-template` — for schema-driven template generation
   - `bukit-notion` — for Notion content integration
   - `bukit-routing` — for URL routing and permalinks
   - `bukit-i18n` — for multilingual sites
   - `bukit-plugins-debug` — for plugin and build debugging
   - `bukit-deploy` — for GitHub Pages deployment
   - `bukit-clone` — for website design cloning
   - `bukit-seo` — for traditional SEO
   - `bukit-geo` — for generative engine optimization
   - `bukit-preview` — for local preview server
   - `bukit-dev` — for HMR development server
   - `bukit-webhook` — for webhook automated deployment

3. **Trigger keywords** — Load when user mentions ANY of:
   - "bukit", "site.yaml", "Scriban", "scriban"
   - "static site generator", "SSG", "blog generator"
   - ".csproj" (in context of static site)
   - Bukit-specific concepts: "permalink", "content collection", "Notion API"

4. **Skill file paths**: `src/skills/<skill-name>/SKILL.md`

5. **CLI command policy**: ALL CLI operations reference `bukit-cli-reference`;
   other skills only provide knowledge, not command execution instructions.

6. **Platform**: Shell commands use `bukit` (cross-platform). CLIs adapt for
   OS-specific suffixes (e.g., `bukit.exe` on Windows).

## Quick Reference

| Task | Load Skill |
|------|-----------|
| Create new site | using-bukit → cli-reference → config → theme → templating |
| Configure Notion | using-bukit → notion → config → cli-reference |
| Fix template error | using-bukit → templating → theme |
| Debug build failure | using-bukit → plugins-debug → config → cli-reference |
| Deploy to GitHub Pages | using-bukit → deploy → config → cli-reference |
| Clone website design | using-bukit → clone → theme → cli-reference |
| Set up SEO | using-bukit → seo → config → cli-reference |
| Set up GEO (AI search) | using-bukit → geo → config → cli-reference |
| Create design system | using-bukit → design-tokens → theme → config |
| Generate templates from schema | using-bukit → content-to-template → config → templating → design-tokens |
```

#### 2.3 AGENTS.md — Codex CLI 入口

````markdown
# Bukit Agent Knowledge Base

## When to Load Bukit Skills

Load Bukit skills when the user mentions ANY of these signals:

### Language Triggers
- English: "bukit", "using bukit", "Bukit static site", "Bukit SSG"
- Chinese: "使用 bukit", "用 bukit 建站", "bukit 静态站点"
- Malay: "guna bukit", "bina laman bukit"

### File/Context Signals
- User is editing `site.yaml`
- User is editing `.scriban` or Scriban template files
- User is working with Notion-as-CMS
- User mentions `bukit build`, `bukit init`, etc.

## How to Load

1. **Read the gateway skill**: `src/skills/using-bukit/SKILL.md`
2. **Follow routing instructions** in the gateway skill
3. **Read sub-skills directly** (Codex loads skills natively, no Skill tool needed)
4. **Load `bukit-cli-reference` first** for any command execution

## Sub-Agent Dispatch

When spawning agents for Bukit tasks (requires `multi_agent = true` in `~/.codex/config.toml`):

- Pass the relevant SKILL.md content as task instructions
- Formulate as "Your task is to..." with the skill content in `<agent-instructions>` tags
- Use `spawn_agent(message=...)` with filled skill content

## Environment Detection

Before operating on Bukit sites, verify:
```bash
test -f site.yaml && echo "Bukit site detected" || echo "Not a Bukit site"
````

````

#### 2.4 GEMINI.md — Gemini CLI 入口

```markdown
# Bukit Agent Knowledge Base

## Skill Activation

When the user mentions Bukit or Bukit-related concepts:

1. Activate `using-bukit` skill first: `activate_skill("using-bukit")`
2. Follow the gateway routing to activate sub-skills as needed
3. Commands use `run_shell_command` with `bukit` prefix

## Trigger Keywords

bukit, site.yaml, Scriban, scriban, static site generator, Notion CMS,
permalink, content collection, SSG

## Skill Paths

All skills: `src/skills/<skill-name>/SKILL.md`

## Platform Notes

- Gemini CLI has no subagent support; run tasks in single session
- Use `run_shell_command` for CLI, `read_file`/`write_file` for file ops
- Use `save_memory` to persist key Bukit patterns across sessions
````

#### 2.5 `.github/copilot-instructions.md` — Copilot CLI 入口

```markdown
# Bukit Agent Knowledge Base

When the user's task involves Bukit (static site generator):

1. Use the `skill` tool to load Bukit skills from `src/skills/<skill-name>/SKILL.md`
2. Start with `using-bukit` as the gateway skill
3. Route to sub-skills per the gateway's routing table

Bukit trigger signals:
- Mentions "bukit", "site.yaml", "Scriban"
- Working in a repo with .csproj files and site.yaml
- Notion-as-CMS conversations
```

#### 2.6 `.trae/rules/project_rules.md` — Trae 入口（增强）

当前 `.trae/rules/project_rules.md` 已包含：

```
- Agent skills in `src/skills/<skill-name>/SKILL.md` — each maps to a CLI subsystem
```

需增强为：

```
- Agent skills in `src/skills/<skill-name>/SKILL.md` — 18 skills covering all Bukit subsystems
- Gateway: `using-bukit` routes to sub-skills via Skill tool
- CLI operations: always load `bukit-cli-reference` first
- See `src/skills/skills-index.yaml` for complete skill catalog
```

***

### Phase 3：技能文件质量提升（2 天）

**目标**：根据 Phase 0 审计结果修正所有不达标的 SKILL.md

#### 3.1 修正优先级

| 优先级 | 修正项                            | 影响技能数（预估）                 |
| --- | ------------------------------ | ------------------------- |
| P0  | 补齐英文 `description`（所有平台通用）     | \~5 个（部分仅有中文 description） |
| P0  | 确保 "Multilingual Triggers" 表完整 | \~8 个                     |
| P0  | 补齐 "常见错误排查" 表（至少 3 条）          | \~3 个                     |
| P1  | 移除硬编码工具引用                      | \~2 个                     |
| P1  | 补齐至少 2 个可执行示例                  | \~2 个                     |
| P2  | 统一章节结构模板                       | \~5 个                     |

#### 3.2 统一章节模板

每个 SKILL.md 遵循此结构（已大部分遵守，仅需微调）：

```markdown
---
name: <skill-name>
description: Use when...（英文，触发条件）
description_zh: 当...（中文触发条件）
description_ms: Gunakan apabila...（马来文触发条件）
---

# <Skill Title>

## Overview
<一句话概述技能边界>

## Multilingual Triggers
| 语言 | 触发短语 |

## <核心知识 1>
## <核心知识 2>

## Common Errors / 常见错误排查
| Symptom | Cause | Fix |

## Reference
- Cross-reference: `bukit-cli-reference`（仅引用，不重复命令）
```

***

### Phase 4：Claude Code 插件打包（2 天）

**目标**：创建 Claude Code 插件清单，使 Bukit 技能可作为插件被安装和自动发现

#### 4.1 `src/skills/plugin.json` — 插件清单

```json
{
  "name": "bukit-agent-skills",
  "version": "2.0.0",
  "description": "Bukit static site generator skills for AI agents — configuration, theming, templating, Notion integration, SEO, GEO, deployment, and more.",
  "author": "Bukit",
  "homepage": "https://github.com/ALi365-SDN-BHD/Bukit",
  "skills": [
    "src/skills/using-bukit/SKILL.md",
    "src/skills/bukit-cli-reference/SKILL.md",
    "src/skills/bukit-config/SKILL.md",
    "src/skills/bukit-theme/SKILL.md",
    "src/skills/bukit-templating/SKILL.md",
    "src/skills/bukit-design-tokens/SKILL.md",
    "src/skills/bukit-content-to-template/SKILL.md",
    "src/skills/bukit-notion/SKILL.md",
    "src/skills/bukit-routing/SKILL.md",
    "src/skills/bukit-i18n/SKILL.md",
    "src/skills/bukit-plugins-debug/SKILL.md",
    "src/skills/bukit-deploy/SKILL.md",
    "src/skills/bukit-clone/SKILL.md",
    "src/skills/bukit-seo/SKILL.md",
    "src/skills/bukit-geo/SKILL.md",
    "src/skills/bukit-preview/SKILL.md",
    "src/skills/bukit-dev/SKILL.md",
    "src/skills/bukit-webhook/SKILL.md"
  ],
  "agents": [],
  "mcpServers": []
}
```

#### 4.2 安装方式（用户侧）

```bash
# Claude Code: 本地安装
claude plugins install src/skills

# 或通过 Git URL（未来发布后）
claude plugins install github.com/ALi365-SDN-BHD/Bukit
```

***

### Phase 5：CI 自动化验证（1 天）

**目标**：确保技能相关知识在每次变更后自动验证

#### 5.1 CI 检查项

| 检查项                                  | 工具       | 触发条件                          |
| ------------------------------------ | -------- | ----------------------------- |
| `skills-index.yaml` ↔ `SKILL.md` 一致性 | 自定义脚本    | PR 到 `src/skills/**/*.md`     |
| `plugin.json` 中的路径全部有效               | Shell 脚本 | PR 到 `src/skills/`            |
| 所有 SKILL.md 有必需 Front Matter 字段      | 自定义脚本    | PR 到 `src/skills/**/SKILL.md` |
| `skills-index.json` 与 YAML 同步        | diff 比较  | PR 到 `skills-index.yaml`      |

#### 5.2 建议的目录结构

```
src/skills/
├── skills-index.yaml              # 知识索引（单一真源）
├── skills-index.json              # JSON 版（自动生成）
├── plugin.json                    # Claude Code / Copilot 插件清单
├── README.md / README.zh-CN.md    # 用户说明
├── using-bukit/SKILL.md           # 网关技能
├── bukit-cli-reference/SKILL.md   # CLI 参考
├── bukit-config/SKILL.md          # 配置
├── bukit-theme/SKILL.md           # 主题
├── bukit-templating/SKILL.md      # 模板
├── bukit-design-tokens/SKILL.md   # 设计令牌
├── bukit-content-to-template/SKILL.md  # Schema→模板
├── bukit-notion/SKILL.md          # Notion
├── bukit-routing/SKILL.md         # 路由
├── bukit-i18n/SKILL.md            # 多语言
├── bukit-plugins-debug/SKILL.md   # 插件
├── bukit-deploy/SKILL.md          # 部署
├── bukit-clone/SKILL.md           # 克隆
├── bukit-seo/SKILL.md             # SEO
├── bukit-geo/SKILL.md             # GEO
├── bukit-preview/SKILL.md         # 预览
├── bukit-dev/SKILL.md             # 开发
├── bukit-webhook/SKILL.md         # Webhook
└── scripts/
    ├── validate-skills.sh         # 技能文件验证脚本
    ├── generate-index-json.sh     # YAML → JSON 转换
    └── audit-triggers.sh          # 触发条件审计
```

#### 5.3 仓库根目录文件

```
仓库根目录
├── CLAUDE.md              ← Claude Code 入口
├── AGENTS.md              ← Codex CLI 入口
├── GEMINI.md              ← Gemini CLI 入口
├── .github/
│   └── copilot-instructions.md  ← Copilot CLI 入口
├── .trae/
│   └── rules/
│       └── project_rules.md     ← Trae 入口（增强）
└── .codex/
    └── config.toml              ← Codex 可选配置
```

***

## 四、实施优先级

| Phase   | 描述                             | 工作量 | 优先级   | 依赖        |
| ------- | ------------------------------ | --- | ----- | --------- |
| Phase 0 | 技能文件审计                         | 1 天 | 🔴 P0 | 无         |
| Phase 1 | 知识索引清单（skills-index.yaml/json） | 2 天 | 🔴 P0 | Phase 0   |
| Phase 2 | 平台适配入口（CLAUDE.md 等 5 个入口）      | 3 天 | 🔴 P0 | Phase 1   |
| Phase 3 | 技能文件质量提升（修正审计问题）               | 2 天 | 🟡 P1 | Phase 0   |
| Phase 4 | Claude Code 插件打包（plugin.json）  | 2 天 | 🟡 P1 | Phase 1   |
| Phase 5 | CI 自动化验证                       | 1 天 | 🟢 P2 | Phase 1-3 |

**总工作量：约 9-11 天**（可并行部分约 6-7 天）

***

## 五、设计与决策要点

### 5.1 为什么不在根目录放 18 个 SKILL.md

**拒绝方案**：将 `src/skills/` 扁平化到根目录 `.claude/skills/`

**理由**：

* `src/skills/` 已是"非运行时源码"的准确位置（与 `src/Bukit.Cli/` 等运行时源码目录对应）

* 各平台原生支持从任意路径加载 skill 文件

* 根目录放 18 个文件会造成杂乱

* 少数不支持自定义路径的平台（如有），用符号链接解决

### 5.2 为什么需要 skills-index.yaml（而非仅 CLAUDE.md）

* `CLAUDE.md` 是**文档**，面向人类阅读，Agent 也能理解但不可编程查询

* `skills-index.yaml` 是**数据**，可被脚本解析、CI 验证、工具生成

* 两者互补：`CLAUDE.md` 告诉 Agent"做什么"，`skills-index.yaml` 告诉工具"有什么"

### 5.3 为什么不复制 SKILL.md 到各平台目录

* **单一真源原则**：`src/skills/` 是唯一位置

* 平台入口文件只做触发和路由，不复制技能内容

* 更新一处即可，无需同步多份

### 5.4 为什么 plugin.json 放在 `src/skills/` 而非根目录

* Claude Code 插件指向技能文件目录，而非仓库根目录

* `plugin.json` 描述的是"这个技能集合"，自然与技能文件放在一起

* 未来如果拆分技能包（如 `bukit-core-skills` vs `bukit-advanced-skills`），可独立配置

### 5.5 平台兼容性与限制

| 特性          | Trae              | Claude Code | Codex CLI      | Copilot CLI            | Gemini CLI          |
| ----------- | ----------------- | ----------- | -------------- | ---------------------- | ------------------- |
| 原生 Skill 工具 | ✅ Skill           | ✅ Skill     | ❌ 原生读取         | ✅ skill                | ✅ activate\_skill   |
| 多语言触发       | ✅ description\_zh | ✅ CLAUDE.md | ✅ AGENTS.md    | ✅ copilot-instructions | ✅ GEMINI.md         |
| 子 Agent 分发  | ✅ Task            | ✅ Task      | ✅ spawn\_agent | ✅ task                 | ❌ 不支持               |
| 插件系统        | ❌                 | ✅ plugins   | ❌              | ✅ plugins              | ❌                   |
| 跨平台命令       | Bash              | Bash        | 原生 shell       | bash                   | run\_shell\_command |

**关键适配点**：

* Codex 不支持 `Skill` 工具 → 入口文件中有明确指令"直接 Read 文件"

* Gemini 不支持子 Agent → 技能中不依赖并行分发模式

* Trae 已原生支持 → 仅需增强 `project_rules.md`

***

## 六、验收标准

### 6.1 功能验收

* [ ] `CLAUDE.md` / `AGENTS.md` / `GEMINI.md` / `copilot-instructions.md` 四个入口文件创建完毕

* [ ] `skills-index.yaml` + `skills-index.json` 创建完毕，包含全部 18 个技能

* [ ] `plugin.json` 创建完毕，路径全部有效

* [ ] `.trae/rules/project_rules.md` 已增强

### 6.2 质量验收

* [ ] 所有 18 个 SKILL.md 通过 Phase 0 审计（100% PASS）

* [ ] `skills-index.yaml` 中每个技能的 `path` 可解析到存在的文件

* [ ] `plugin.json` 中每个技能路径文件存在

* [ ] 入口文件中无过期或错误的技能名称

### 6.3 CI 验收

* [ ] `validate-skills.sh` 在 CI 中运行通过

* [ ] `skills-index.yaml` 与 `skills-index.json` 同步检查通过

### 6.4 兼容性验收

* [ ] Trae 环境：加载 `using-bukit` 后正确路由到子技能

* [ ] Claude Code 环境：`CLAUDE.md` 被正确读取，Skill 工具可加载各技能

* [ ] Codex CLI 环境：`AGENTS.md` 关键词触发正确，技能文件可直接 Read

* [ ] 新文件不影响现有构建、测试、lint 流程（`dotnet build` / `dotnet test` 正常）

***

## 七、未来扩展方向

| 扩展           | 说明                                  | 时机        |
| ------------ | ----------------------------------- | --------- |
| 英文版 SKILL.md | 为 Codex/Gemini 等 ENG-first 平台提供英文版本 | 用户反馈后     |
| 技能热更新机制      | 通过 GitHub API 拉取最新技能，无需更新 Bukit CLI | Phase 5 后 |
| 技能分析仪表盘      | 统计各技能加载频率、常见错误触达率                   | 有数据后      |
| 技能市场/注册表     | 第三方可贡献 Bukit 技能（如特定模板/集成）           | 社区需求      |
| 多语言技能自动翻译    | CI 自动将中文 SKILL.md 翻译为 en/ms         | 维护成本高时    |

