# Bukit src/skills Agent 知识体系升级计划

## 1. 审计阶段：读取并理解全部文件（只读，不修改）

### 1.1 已完成的初始审计

* [x] 读取 `src/skills/plugin.json` — 19 个 skill 路径

* [x] 读取 `src/skills/skills-index.yaml` — 单事实源，19 个 skill，含 workflows

* [x] 读取 `src/skills/skills-index.json` — 由 YAML 生成，版本 3.0.0

* [x] 读取 `src/skills/README.md` — 发现目录布局缺失 4 个 skill

* [x] 读取 `src/skills/AGENTS.md` — 已列出所有 skill（含 theme-component-system）

* [x] 读取 `src/skills/CLAUDE.md` — 发现缺失 theme-component-system

* [x] 读取 `src/skills/GEMINI.md` — 发现缺失 theme-component-system，写 "18 total"

* [x] 读取 `src/skills/copilot-instructions.md` — 发现缺失 theme-component-system，写 "18"

* [x] 读取 `src/skills/using-bukit/SKILL.md` — 18 个 skill 表（缺失 0 项，含 theme-component-system 为 #18）

* [x] 读取 `src/skills/bukit-cli-reference/SKILL.md` — CLI 命令表含重复 `geo`/`geo audit`

* [x] 读取 `src/skills/bukit-config/SKILL.md` — 顶级节点说 6 个但实际列出 7 个

* [x] 读取 `src/skills/bukit-theme/SKILL.md` — front matter 检查

* [x] 读取 `src/skills/bukit-seo/SKILL.md` — front matter 检查

* [x] 读取 `src/skills/bukit-geo/SKILL.md` — front matter 检查

* [x] 读取 `src/skills/theme-component-system/SKILL.md` — front matter 检查

* [x] 读取 `src/skills/scripts/validate-skills.sh` — 偏格式检查，缺少语义验证

* [x] 读取 `src/skills/scripts/generate-index-json.sh` — JSON 生成脚本

* [x] 检查 `guide/user/` 目录 — 确认 guide chapter 文件存在

### 1.2 待完成的审计（并行的 search agents）

* [ ] 审计 CLI 源码：核对 `bukit --help` 真实输出与 `bukit-cli-reference` 命令表

* [ ] 审计配置模型源码：核对 `site.yaml` 所有顶级节点与 `bukit-config`

* [ ] 审计 SEO/GEO 源码：核对诊断码、SEO report 路径、`seo-report.json` 结构

* [ ] 审计 webhook 源码：核对 token 验证方式（shared token vs HMAC）

* [ ] 审计 plugin 源码：核对真实 plugin 数量和 lifecycle

* [ ] 审计 theme V2 源码：评估组件化主题稳定性

* [ ] 读取剩余的 12 个 SKILL.md 文件，检查内容一致性

***

## 2. 已发现的 P0 问题（数量/索引不一致）

| #    | 问题                                                                                                                                     | 影响文件                                          | 修复方式                      |
| ---- | -------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------- | ------------------------- |
| P0-1 | **skill 数量不一致**：plugin.json 有 19 个，skills-index.yaml 有 19 个，但 GEMINI.md 写 "18 total"，copilot-instructions.md 写 "18"，README.md 多处写 "18" | GEMINI.md, copilot-instructions.md, README.md | 统一为 19                    |
| P0-2 | **CLAUDE.md 缺失 theme-component-system**：CLAUDE.md 的 bullet list 只有 18 个 skill（缺 theme-component-system）                                | CLAUDE.md                                     | 添加 theme-component-system |
| P0-3 | **GEMINI.md 缺失 theme-component-system**：skill 表格只有 18 行                                                                                | GEMINI.md                                     | 添加 theme-component-system |
| P0-4 | **copilot-instructions.md 缺失 theme-component-system**：列出 18 个名称                                                                        | copilot-instructions.md                       | 添加 theme-component-system |
| P0-5 | **README.md Directory Layout 缺失 4 个 skill**：缺 bukit-preview, bukit-dev, bukit-webhook, theme-component-system                          | README.md                                     | 补全目录布局                    |
| P0-6 | **bukit-config 顶级节点数量矛盾**：Overview 说 "Six top-level nodes"，但表列出 7 个（含 deploy）                                                          | bukit-config/SKILL.md                         | 修正为 "seven"               |
| P0-7 | **CLI 命令表重复**：`geo audit` 和 `geo` 两条同时存在                                                                                               | bukit-cli-reference/SKILL.md                  | 合并为一条 `geo audit`         |

***

## 3. 已发现的 P1 问题（语义/对齐）

| #    | 问题                                                                                                                                                                                | 影响文件                                  | 修复方式                                                     |
| ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------- | -------------------------------------------------------- |
| P1-1 | **所有 SKILL.md 缺失 status 元数据**：无 status/since/verified\_by/source\_anchors 字段                                                                                                      | 全部 19 个 SKILL.md                      | 添加 Front Matter 元数据                                      |
| P1-2 | **skills-index.yaml 类型体系 vs 用户要求的 5 层结构不匹配**：现有类型为 gateway/reference/technique/pattern/operation，用户要求为 Gateway/Core Reference/Build Authoring/Data Site Features/Operations Debug | skills-index.yaml, README.md, 各平台入口文件 | 在 README 中明确 5 层映射，skills-index.yaml 可保留现有类型或增加 layer 字段 |
| P1-3 | **theme-component-system 可能未稳定**：V2 组件化主题需确认稳定状态，若未完全稳定应标记 experimental/beta                                                                                                      | theme-component-system/SKILL.md       | 核实后标记合适状态                                                |
| P1-4 | **validate-skills.sh 缺少语义校验**：无 status 检查、source\_anchors 路径检查、guide\_chapters 路径检查、本地绝对路径检查、YAML 验证、skill 数量一致性检查                                                                | scripts/validate-skills.sh            | 增强或新增 validate-skills-strict.sh                          |
| P1-5 | **C**LI `plugin list` 命令表写 "14 built-in"，但 skills-index 说 "13 built-in"                                                                                                           | bukit-cli-reference/SKILL.md          | 核对源码后统一                                                  |
| P1-6 | **缺少 MAINTENANCE.md**                                                                                                                                                             | src/skills/                           | 创建维护文档                                                   |
| P1-7 | **缺少 QUALITY\_REPORT.md**                                                                                                                                                         | src/skills/                           | 创建质量报告                                                   |

***

## 4. 执行计划（分步实施）

### Phase 1: P0 数量/索引修复（不涉及源码审计）

**任务 1.1**: 修复 CLAUDE.md

* 在 bullet list 中添加 `theme-component-system`

* 更新 Quick Reference 表格添加 componentized theme workflow

**任务 1.2**: 修复 GEMINI.md

* 将 "18 total" 改为 "19 total"

* 在 skill 表格中添加 theme-component-system

* 在 Quick Reference 表格添加 componentized theme entry

**任务 1.3**: 修复 copilot-instructions.md

* 用 19 替换 18 的计数

* 名称列表中添加 theme-component-system

**任务 1.4**: 修复 README.md

* Directory Layout 补全 bukit-preview, bukit-dev, bukit-webhook, theme-component-system

* File Layout 中的 "18 domain skills" 改为 "19 domain skills"

* Per-Platform Usage 中的 "18 Bukit skills" 改为 "19 Bukit skills"

* Skill Responsibilities 表格补全缺失的 4 个 skill（若表不完整）

* Loading Rules 补充 theme-component-system 规则

* Suggested Reading Paths 添加 componentized theme entry

* 明确 5 层结构：Gateway, Core Reference, Build Authoring, Data/Site Features, Operations/Debug

**任务 1.5**: 修复 bukit-config/SKILL.md

* 将 "Six top-level nodes" 改为 "Seven top-level nodes"

**任务 1.6**: 修复 bukit-cli-reference/SKILL.md 命令表

* 合并重复的 `geo audit` / `geo` 为一条

* 核对 "14 built-in" plugin 数量改为与源码一致的正确值

**任务 1.7**: 修复 using-bukit/SKILL.md 中的 skill 表

* 确认 18 个 skill 编号正确，theme-component-system 为 #18（当前已正确）

### Phase 2: P1 语义对齐修复（需要源码审计后确认）

**任务 2.1**: 用 search agents 审计源码

* search agent 1: 审计 CLI 源码 — 核对所有命令、参数、exit codes

* search agent 2: 审计 config 模型源码 — 核对顶级节点、字段名、默认值

* search agent 3: 审计 SEO/GEO 源码 — 核对诊断码、report 路径、log路径

* search agent 4: 审计 webhook/deploy/dev/preview 源码 — 核对实际行为

* search agent 5: 审计 plugin 源码 — 核对数量、生命周期、hook 顺序

* search agent 6: 读取剩余 12 个 SKILL.md 文件

**任务 2.2**: 基于审计结果修复各 SKILL.md 内容

重点关注：

* `bukit-cli-reference`: 所有命令/参数与真实 CLI 对齐

* `bukit-config`: 所有字段与 Config 模型对齐，Notion 配置字段准确

* `bukit-seo` / `bukit-geo`: 诊断码、报告路径准确

* `bukit-webhook`: token 验证方式准确（shared token vs HMAC）

* `bukit-plugins-debug`: plugin 数量、lifecycle 准确

* `bukit-dev` / `bukit-preview`: 端口、WebSocket、HMR 行为准确

* `theme-component-system`: 稳定性评估

### Phase 3: 为所有 SKILL.md 添加 status 元数据

**任务 3.1**: 评估每个 skill 的状态

| Skill                     | 预估 Status         | 理由                       |
| ------------------------- | ----------------- | ------------------------ |
| using-bukit               | stable            | gateway，长期稳定             |
| bukit-cli-reference       | stable            | CLI 命令集稳定                |
| bukit-config              | stable            | 配置模型已稳定                  |
| bukit-theme               | stable            | V1 theme 系统稳定            |
| bukit-templating          | stable            | Scriban 集成稳定             |
| bukit-design-tokens       | stable            | CSS 变量系统稳定               |
| bukit-content-to-template | beta              | schema→template 生成可能调整   |
| bukit-notion              | stable            | Notion 集成稳定              |
| bukit-routing             | stable            | 路由系统稳定                   |
| bukit-i18n                | stable            | 多语言系统稳定                  |
| bukit-plugins-debug       | stable            | 插件生命周期稳定                 |
| bukit-deploy              | stable            | GitHub Pages 部署稳定        |
| bukit-clone               | beta              | 网站克隆依赖 Browser MCP，仍可能调整 |
| bukit-seo                 | stable            | SEO 功能稳定                 |
| bukit-geo                 | beta              | GEO 评估标准仍在演进             |
| bukit-preview             | stable            | 预览服务器稳定                  |
| bukit-dev                 | stable            | HMR 开发服务器稳定              |
| bukit-webhook             | stable            | webhook 功能稳定             |
| theme-component-system    | experimental/beta | V2 组件化主题需确认              |

**注意**: 以上 status 需在源码审计后确认，不可凭想象标记。

**任务 3.2**: 为每个 SKILL.md Front Matter 添加字段

```yaml
status: stable|beta|experimental|planned
since: "v3.0.0"
verified_by:
  - "src/Bukit.Cli/..."
source_anchors:
  - "src/Bukit.Engine/..."
guide_chapters:
  - "guide/user/XX-xxx.md"
```

### Phase 4: 强化 gateway skill (using-bukit)

**任务 4.1**: 更新 using-bukit/SKILL.md

* 明确 5 层结构引用

* 明确 experimental/planned skill 使用限制

* 补全 bukit-design-tokens 和 bukit-content-to-template 的 guide chapter 引用

* 确保所有 19 个 subskill 在路由表中

### Phase 5: 强化验证脚本

**任务 5.1**: 创建 `validate-skills-strict.sh`

新增检查项：

1. `skills-index.yaml` 中的 `skill_count` == 实际 skill 数量
2. `plugin.json` 中的 skills 与 `skills-index.yaml` 完全一致
3. 所有 `SKILL.md` 都有必需 Front Matter：`name`, `description`, `status`, `since`, `verified_by`, `source_anchors`
4. `status` 值合法（stable|beta|experimental|planned）
5. 所有 `source_anchors` 路径存在
6. 所有 `guide_chapters` 路径存在（支持逗号分隔多条）
7. 所有 Related Skills 不使用 `file://` 协议
8. 不出现本地绝对路径（如 `/Users/...`）
9. 不出现平台特定工具名（如 "Bash tool"、"TodoWrite"）
10. YAML 代码块基本可解析
11. `skills-index.json` 与 `skills-index.yaml` 同步
12. README 中列出的 skill 与索引一致
13. 所有 `requires` 指向存在的 skill
14. 所有 workflow chain 中的 skill 存在
15. 无重复 command 说明
16. guide\_chapter 引用文件存在

**任务 5.2**: 更新现有 `validate-skills.sh`

* 添加 skill 数量检查

* 添加 plugin.json 与 skills-index.yaml 一致性检查

### Phase 6: 创建/更新文档

**任务 6.1**: 创建 `src/skills/MAINTENANCE.md`
内容：

* 维护流程：源码修改 → skill 同步步骤

* CLI 修改后如何更新 bukit-cli-reference

* 配置模型修改后如何更新 bukit-config

* Guide 修改后如何更新 guide chapter 引用

* 新增 skill 的标准和步骤

* 删除/合并 skill 的标准

* 发布前 checklist

* 验证命令

**任务 6.2**: 创建 `src/skills/QUALITY_REPORT.md`
内容：

* 本次审计摘要

* 已修复问题列表

* 待人工确认问题列表

* experimental/planned 能力清单

* 验证结果

**任务 6.3**: 更新 `src/skills/README.md`

* 添加 5 层结构说明

* 添加维护规则

* 添加如何新增 skill 的说明

* 添加 status 判断标准

* 添加如何从 YAML 生成 JSON 和平台入口文件

### Phase 7: 重新生成并验证

**任务 7.1**: 运行 `generate-index-json.sh` 重新生成 `skills-index.json`

**任务 7.2**: 运行 `validate-skills.sh` 检查通过

**任务 7.3**: 运行 `validate-skills-strict.sh` 检查通过

**任务 7.4**: 运行 `dotnet build` 和 `dotnet test`（如项目支持）

***

## 5. 关键决策点（需用户确认）

1. **theme-component-system 状态**：标记为 `beta` 还是 `experimental`？需确认 V2 组件化主题的源码实现状态。
2. **5 层结构 vs 现有 type 字段**：是否保留 skills-index.yaml 中的 type 字段（gateway/reference/technique/pattern/operation），还是替换为新分层？建议保留现有 type 作为细粒度分类，另加 `layer` 字段映射到 5 层。
3. **source\_anchors 的粒度**：引用到目录级还是文件级？建议目录级避免过度维护。
4. **guide\_chapters 的引用格式**：当前已在 skills-index.yaml 中使用简单字符串（如 "04 Site YAML Config"），是否需要改为精确的相对路径格式？建议在 SKILL.md Front Matter 中使用相对路径，在 skills-index.yaml 中保留人类可读格式。

***

## 6. 执行顺序总结

```
Phase 1 (P0 修复) → 立即执行，无需等待审计
├── 1.1 CLAUDE.md
├── 1.2 GEMINI.md
├── 1.3 copilot-instructions.md
├── 1.4 README.md
├── 1.5 bukit-config/SKILL.md
├── 1.6 bukit-cli-reference/SKILL.md
└── 1.7 using-bukit/SKILL.md

Phase 2 (P1 语义对齐) → 需源码审计后执行
├── 2.1 并行 search agents 审计源码
└── 2.2 基于审计结果修复各 SKILL.md

Phase 3 (Status 元数据) → 审计后执行
├── 3.1 评估每个 skill 状态
└── 3.2 添加 Front Matter 字段

Phase 4-7 → 顺序执行
├── Phase 4: 强化 using-bukit
├── Phase 5: 增强验证脚本
├── Phase 6: 创建/更新文档
└── Phase 7: 重新生成 + 验证
```

