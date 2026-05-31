# guide/ 文档产品化治理实施方案（完整版）

## 概述

对 `guide/` 目录进行全面治理：入口 README 重构、多语言同步、详细页面链接修复、新增治理文档、AI Prompt 安全增强。

**范围（全部 guide/ 文件）**：
- `guide/user/README.*`（3 文件）— 入口重构
- `guide/user/` 详细页面 01-20（多文件）— 链接修复、语言 fallback 标注
- `guide/dev/README.*`（3 文件）— 入口重构 + 新文档链接
- `guide/dev/` 详细页面（多文件）— 链接修复、语言 fallback 标注
- `guide/ai/chatgpt/README.*`（3 文件）— 安全闭环增强
- `guide/ai/chatgpt/` prompt 文件（15 文件）— `dosc` 路径修正、安全增强
- 新增治理文档（9 文件）

**不作修改**：
- `src/skills/**`（严格不碰）

---

## 审计结果总览

### 语言覆盖率缺口

| 目录 | 齐全 | 仅 .zh-CN.md | .md+.zh-CN 缺 .ms | .ms+.zh-CN 缺 .md | 仅 .md |
|---|---|---|---|---|---|
| `ai/chatgpt` | 6 文件 | 0 | 0 | 0 | 0 |
| `dev` | 35 文件 | 8 | 0 | 1 | 6(scriban/) |
| `user` | 19 文件 | 0 | 2 | 1 | 0 |

### 具体缺口清单

**`guide/dev/` 仅中文的 8 个文件**：
- `component-utilities.zh-CN.md`
- `page-composer.zh-CN.md`
- `performance-benchmarks.zh-CN.md`
- `section-plugin.zh-CN.md`
- `section-schema.zh-CN.md`
- `theme-component-system.zh-CN.md`
- `theme-doctor.zh-CN.md`
- `theme-manifest.zh-CN.md`

**`guide/dev/` 仅英文的 6 个文件（scriban/ 子目录）**：
- `scriban/builtins.md`、`language.md`、`liquid-support.md`、`readme.md`、`runtime.md`、`scriban.md`

**`guide/dev/` 缺英文**：`doctor.md`

**`guide/user/` 缺马来文**：`05-markdown-content`、`20-external-plugins`

**`guide/user/` 缺英文**：`07-multi-source`

### 已发现的链接/措辞问题

| # | 文件 | 行 | 当前 | 问题 |
|---|---|---|---|---|
| 1 | `guide/user/README.md` | L30 | `[07 Multi-Source (Chinese)]` | 不统一 fallback 标注 |
| 2 | `guide/user/README.md` | L104 | `Full Chinese source:` | 对英文用户不中性 |
| 3 | `guide/user/README.ms.md` | L103 | `Sumber penuh bahasa Cina:` | 对马来用户不中性 |
| 4 | `guide/user/README.ms.md` | L15 | `[05 Kandungan Markdown](./05-markdown-content.md)` | 马来标签链英文文件，无 fallback 标注 |
| 5 | `guide/dev/README.md` | L49 | `[Doctor checks (Chinese)]` | 应统一 fallback 措辞 |
| 6 | `guide/dev/README.md` | L74 | `Full Chinese source:` | 同上 |
| 7 | `guide/dev/README.ms.md` | L74 | `Rujukan bahasa Inggeris:` | 应改为中性 "Rujukan kanonik" |
| 8 | `guide/ai/chatgpt/README.md` | L37 | `Full Chinese source:` | 同上 |
| 9 | `guide/ai/chatgpt/README.ms.md` | L37 | `Sumber penuh bahasa Cina:` | 同上 |
| 10 | `guide/user/14-troubleshooting.md` | L9 | `../dev/cache-clean.zh-CN.md` | **cache-clean.md 英文版存在！** 应链 `.md` 而非 `.zh-CN.md` |
| 11 | `guide/user/14-troubleshooting.md` | L9 | `../dev/doctor.zh-CN.md` | doctor.md 不存在，需添加 fallback 标注 |
| 12 | `guide/user/01-quick-start.md` | L30 | `../dev/doctor.zh-CN.md` | 同上 |
| 13 | `guide/user/01-quick-start.md` | L149 | `./07-multi-source.zh-CN.md` | 07-multi-source.md 不存在，需添加 fallback 标注 |
| 14 | `guide/ai/chatgpt/knowledge_manifest.md` | L7 | `dosc/intent.md` | **疑似 typo**，应为 `docs/intent.md` |
| 15 | `guide/ai/chatgpt/system_instructions.md` | L17 | `dosc/intent.md` | 同上 |
| 16 | `guide/ai/chatgpt/prompt_fix_config.md` | L12 | `dosc/intent.md` | 同上 |
| 17 | `guide/ai/chatgpt/knowledge_manifest.md` | L19 | `guide/user/07-multi-source.md` | **文件不存在**（仅 `.zh-CN.md` 和 `.ms.md`） |
| 18 | `guide/ai/chatgpt/knowledge_manifest.md` | L20 | `dosc/ai_guide.md` | **疑似 typo**，应为 `docs/ai_guide.md` |

### 无安全问题
- 所有 `NOTION_TOKEN` 引用均为合规的文档说明（使用环境变量）
- 无真实 token/secret 出现在文档中

---

## 执行计划（5 个 Phase）

### Phase 1: guide/ 入口 README 修复（9 个文件）

> 与之前计划相同，不再重复展开。见原计划 Phase 1 的 1.1-1.9。

**核心变更**：
- `guide/user/README.*`：增加 "Choose Your Path" 决策导航表格 + 修复链接 fallback 措辞
- `guide/dev/README.*`：增加 "Maintainer Task Map" 任务导航表格 + 链接修复 + 新增治理文档链接
- `guide/ai/chatgpt/README.*`：增加 "Safety and Validation" 安全闭环区块 + 措辞修复

---

### Phase 2: guide/user/ 详细页面修复（01-20 系列）

> **策略**：不对详细页面做全面重写。只修复已发现的链接问题、添加语言 fallback 标注、并让 3 个有语言缺口的文件添加标准化的 fallback header。

#### 2.1 `guide/user/01-quick-start.md` — 2 处链接修复

**修复 L30**：
```
# 当前
If doctor reports errors, check first: [14 Troubleshooting](./14-troubleshooting.md) (and the developer version of the doctor guide: [guide/dev/doctor](../dev/doctor.zh-CN.md)).
# 改为
If doctor reports errors, check first: [14 Troubleshooting](./14-troubleshooting.md) (and the developer version of the doctor guide: [guide/dev/doctor](../dev/doctor.zh-CN.md); currently available in Chinese and Malay).
```

**修复 L149**：
```
# 当前
- Multi-source composition (pages/posts/modules): [07 Multi Source](./07-multi-source.zh-CN.md)
# 改为
- Multi-source composition (pages/posts/modules): [07 Multi Source](./07-multi-source.zh-CN.md) (currently available in Chinese and Malay)
```

#### 2.2 `guide/user/14-troubleshooting.md` — 2 处链接修复

**修复 L9 — cache-clean 链接**（cache-clean.md 英文版存在！）：
```
# 当前
Developer-oriented troubleshooting docs: [guide/dev/doctor](../dev/doctor.zh-CN.md), [guide/dev/cache-clean](../dev/cache-clean.zh-CN.md).
# 改为
Developer-oriented troubleshooting docs: [guide/dev/doctor](../dev/doctor.zh-CN.md) (Chinese/Malay), [guide/dev/cache-clean](../dev/cache-clean.md).
```

#### 2.3 添加语言 fallback header（3 个文件）

以下文件缺少某语言版本，在文件顶部语言声明后添加 fallback 说明：

**`guide/user/05-markdown-content.md`**（缺 .ms.md）：
在文件顶部（标题之后）添加：
```
> Bahasa Melayu: pada masa ini hanya tersedia dalam bahasa Inggeris dan Cina.
```

**`guide/user/07-multi-source.zh-CN.md`**（缺 .md）：
在文件顶部添加：
```
> English version pending. Versi Bahasa Melayu: [07-multi-source.ms.md](./07-multi-source.ms.md)
```

**`guide/user/20-external-plugins.md`**（缺 .ms.md）：
在文件顶部添加：
```
> Bahasa Melayu: pada masa ini hanya tersedia dalam bahasa Inggeris dan Cina.
```

**`guide/user/20-external-plugins.zh-CN.md`**（缺 .ms.md）：
在文件顶部添加：
```
> Bahasa Melayu: pada masa ini hanya tersedia dalam bahasa Inggeris dan Cina.
```

---

### Phase 3: guide/dev/ 详细页面修复

#### 3.1 `guide/dev/doctor.md` — 创建英文 stub

`doctor.md` 仅存在 `.zh-CN.md` 和 `.ms.md`。创建英文 stub 而不是空文件，指向前两者：

```markdown
# Doctor Checks

Language versions: English (current) | [简体中文](./doctor.zh-CN.md) | [Bahasa Melayu](./doctor.ms.md)

> **Note**: The authoritative doctor documentation is currently available in Chinese and Malay. This English stub provides a high-level overview. For detailed check lists, error codes, and troubleshooting, please refer to the [Chinese version](./doctor.zh-CN.md).

## Overview

`doctor` runs self-checks on your site configuration and environment:

1. `site.yaml` parse and validation
2. Content provider connectivity (Markdown dirs, Notion API with `NOTION_TOKEN`)
3. Theme and template presence
4. Output directory readiness
5. Environment variable checks

## Quick Run

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
```

Exit code 0 = all checks passed. Exit code 1 = errors found.

## Detailed Reference

For the full check list, error codes (`BKT-*`), and troubleshooting flow, see:
- [doctor.zh-CN.md](./doctor.zh-CN.md) (Chinese — authoritative)
- [doctor.ms.md](./doctor.ms.md) (Malay)
```

#### 3.2 8 个仅中文文件 — 添加语言 fallback header

以下文件仅 `.zh-CN.md` 存在，在文件顶部添加标准化 header：

```markdown
> **语言说明**：本页目前仅有中文版本。English version pending. Versi Bahasa Melayu belum tersedia.
```

影响的文件：
- `component-utilities.zh-CN.md`
- `page-composer.zh-CN.md`
- `performance-benchmarks.zh-CN.md`
- `section-plugin.zh-CN.md`
- `section-schema.zh-CN.md`
- `theme-component-system.zh-CN.md`
- `theme-doctor.zh-CN.md`
- `theme-manifest.zh-CN.md`

#### 3.3 `guide/dev/scriban/` — 6 个仅英文文件添加 fallback header

以下文件仅 `.md` 存在，添加标准化 header：

```markdown
> **Language note**: This page is currently available in English only. 中文版本待补充。Versi Bahasa Melayu belum tersedia.
```

影响的文件：
- `scriban/builtins.md`
- `scriban/language.md`
- `scriban/liquid-support.md`
- `scriban/readme.md`
- `scriban/runtime.md`
- `scriban/scriban.md`

---

### Phase 4: guide/ai/chatgpt/ prompt 文件修复

#### 4.1 修复 `dosc` → `docs` 路径（4 个文件，共 4 处）

| 文件 | 行 | 当前 | 改为 |
|---|---|---|---|
| `knowledge_manifest.md` | L7 | `dosc/intent.md` | `docs/intent.md` |
| `knowledge_manifest.md` | L20 | `dosc/ai_guide.md` | `docs/ai_guide.md` |
| `system_instructions.md` | L17 | `dosc/intent.md` | `docs/intent.md` |
| `prompt_fix_config.md` | L12 | `dosc/intent.md` | `docs/intent.md` |

同样的修复应用到对应的 `.zh-CN.md` 和 `.ms.md` 版本（共 12 个文件，每个 prompt 文件 3 种语言）。

#### 4.2 修复 `knowledge_manifest.md` 不存在的文件引用

**L19** `guide/user/07-multi-source.md` → 该文件不存在，改为：
```
- `guide/user/07-multi-source.zh-CN.md`: Composite sources and mode semantics (Chinese/Malay)
```

或者如果 `examples/starter/site.modules.yaml` 已覆盖多源，可考虑删除此行。

#### 4.3 增强 prompt 文件的安全边界（3 个文件）

**`system_instructions.md`** — 在 "Important Rules" 区块增加一条：

```markdown
- Safety: If the user asks you to generate shell commands, deployment scripts, or absolute file paths, refuse and direct them to the Bukit CLI reference (`guide/user/12-cli-reference.md`). Never suggest `curl | bash` or similar patterns.
```

**`prompt_intent.md`** — 在模板后增加：

```markdown
## Safety Constraints (Must Follow)

- Never output shell commands, deployment scripts, or absolute file paths.
- Never ask for or accept tokens, keys, or secrets. Notion access must always use the `NOTION_TOKEN` environment variable.
- If the user asks for commands, direct them to: `guide/user/12-cli-reference.md`
```

**`prompt_site_yaml.md`** — 增加同样的 Safety Constraints 区块。

同样更新 `.zh-CN.md` 和 `.ms.md` 版本。

---

### Phase 5: 新增治理文档（9 个文件）

> 与之前计划相同，不再重复展开。见原计划 Phase 2 的 2.1-2.9。

---

### Phase 6: 链接与语言一致性最终审计

执行以下检查验证所有修复：

```bash
# 确认无残留临时标注
grep -Rn "(Chinese)" guide/          # 期望：0 结果
grep -Rn "Full Chinese source" guide/ # 期望：0 结果
grep -Rn "Sumber penuh bahasa" guide/ # 期望：0 结果

# 确认英文文件不再错误引用 zh-CN 文件（允许有 fallback 标注的情况）
grep -Rn "zh-CN\.md" guide/user/01-quick-start.md guide/user/14-troubleshooting.md

# 确认新增治理文档可读
ls guide/dev/documentation-governance.* guide/dev/release-checklist.* guide/dev/public-preview-scope.*

# 确认 src/skills/** 未被修改
git diff --name-only | grep src/skills || echo "PASS: src/skills not modified"
```

---

## 文件变更汇总

| Phase | 类别 | 文件数 |
|---|---|---|
| Phase 1 | guide/*/README.* 修改 | 9 |
| Phase 2 | guide/user/ 详细页面修改 | 6 (2 链接修复 + 4 fallback header) |
| Phase 3 | guide/dev/ 详细页面修改 | 15 (1 新 doctor.md + 8 header + 6 header) |
| Phase 4 | guide/ai/chatgpt/ prompt 修复 | ~18 (12 dosc 修复 + 6 safety 增强) |
| Phase 5 | 新增治理文档 | 9 |
| **总计** | | **~57 个文件** |

---

## 验证标准

1. `src/skills/**` 未被修改
2. 无残留 `(Chinese)`、`Full Chinese source`、`Sumber penuh bahasa Cina`
3. 所有英文文件不再错误链接到 `.zh-CN.md`（仅保留有标注的必要 fallback）
4. 所有语言缺口文件均有标准化 fallback header
5. `dosc/` 路径全部修正为 `docs/`
6. AI prompt 文件新增安全约束
7. 新增治理文档已从 `guide/dev/README.*` 链接
8. `NOTION_TOKEN` 合规（无真实 token 泄露）
