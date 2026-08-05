# Bukit SEO/GEO Next Master Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 按严格顺序把 Bukit 从“生成机器可读内容”推进到“问题覆盖、来源可信、引用可观测、结果可验证”的离线闭环，同时保持 Core 确定性、无网络和无排名承诺。

**Architecture:** 本计划是总控程序，不是单次实现批次。八个工作包分别修复文章发布者、建立文章信任图、策展 llms 输出、统一薄弱集合索引、增加文章图片诊断、建立问题覆盖合同、记录生成式引用、记录外部权威引用；每个工作包独立建立 verification closure、TDD、提交和专项复审，前一工作包完成并集成后才能开始后一工作包。

**Tech Stack:** .NET 10, Native AOT, `System.Text.Json` source generation, JSON Schema Draft 2020-12, xUnit, Bukit build/audit projection pipeline, external-process plugin protocol, Codex verification-closure workflow.

## Global Constraints

- 严格执行 `WP0 -> WP1-A -> WP1-B -> WP1-C -> WP1-D -> WP2-A -> WP2-B -> WP2-C`；不得并行实施或把后续字段预埋进前序工作包。
- Core 不访问 Google、OpenAI、Perplexity、Reddit 或其他外部服务，不持有 OAuth、API key、cookie、账号、调度和通知状态。
- 外部采集器只写版本化本地 observation 文件；Core 只验证、关联、聚合和报告。
- 新 JSON 合同使用 Draft 2020-12、`additionalProperties: false`、固定 `$id`/`schemaVersion` 和 `System.Text.Json` source generation。
- 不改变现有 `seo-observation.v1`、`seo-route-map.v1`、`seo-insights-report.v1` 的字段语义；新维度使用独立 schema/report。
- 所有诊断只表述为缺失、冲突、观测或候选假设，不把相关性写成原因，不承诺排名、AI 展示或引用。
- `noindex` 的安全/索引边界优先于任何 llms 显式 include；任何策展配置都不能重新暴露非 indexable 页面。
- `citation` 与 `isBasedOn` 不可互换；只有显式 `based-on` 关系才生成 `isBasedOn`。
- `alt=""` 允许作为装饰图语义，只能触发人工复核 warning；不得自动生成 alt。
- `llms.priority` 只控制 Bukit 内部稳定排序，不代表外部 AI 优先级。
- 问题文本、AI 原始答案、用户名、账号标识和凭据不得写入公开输出；`.bukit` 报告也不得包含凭据。
- `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/` 只作历史备份，不读取、不修改、不纳入验证。
- 每个工作包实施前重新读取根 `AGENTS.md`，对实际变更文件运行 `python3 scripts/checks/codex-workflow.py closure`；`unmappedFiles` 非空立即停止。
- 每个工作包使用单写者队列，覆盖 `writing -> testing -> review_wait -> done|blocked`；Bukit fixture、插件锁、manifest 和 dotnet 测试串行。
- 只运行 closure 返回的专项命令；不得运行 whole-solution、`scripts/test-all.sh`、`scripts/smoke-all.sh`、full、release、`ci-fast` 或推断出的 gate。
- 每个工作包只安排一次专项复审；仅 Critical/Important 发现允许回到实现和定向复审，Minor 记录但不扩展范围。
- 每个工作包一个独立提交；总控完成后只做一次 delta-only 统一复审，不重复已缓存的专项证据。
- 本总控计划不授权实施、提交、合并、推送、发布或外部 API 操作；每个工作包仍需单独用户批准。

---

## Work-Package Sequence

| 顺序 | 工作包 | 交付物 | 前置条件 | 主要专项测试 |
|---|---|---|---|---|
| WP0 | [NewsArticle Publisher](2026-08-05-bukit-newsarticle-publisher.md) | 三类 Article publisher 一致性和审计 | 当前 `main` | Engine |
| WP1-A | [Article Trust Graph](2026-08-05-bukit-article-trust-graph.md) | `mainEntityOfPage`、`citation`、显式 `isBasedOn` | WP0 已集成 | Engine + Architecture |
| WP1-B | [LLMS Curation Contract](2026-08-05-bukit-llms-curation-contract.md) | 页面级 visibility/tier/priority | WP1-A 已集成 | Engine + Architecture |
| WP1-C | [Minimum Collection Index Policy](2026-08-05-bukit-minimum-collection-index-policy.md) | 薄弱集合共享索引策略 | WP1-B 已集成 | Config + Engine + Architecture |
| WP1-D | [Article Image Diagnostics](2026-08-05-bukit-article-image-diagnostics.md) | 空 alt 复核和默认图诊断 | WP1-C 已集成 | Engine |
| WP2-A | [Question Coverage Contracts](2026-08-05-bukit-question-coverage-contracts.md) | 目标问题和搜索问题观测 | WP1-D 已集成 | CLI + Architecture |
| WP2-B | [Generative Citation Observation](2026-08-05-bukit-generative-citation-observation.md) | 多运行 AI 提及/引用报告 | WP2-A 已集成 | CLI + Architecture |
| WP2-C | [External Authority Observation](2026-08-05-bukit-external-authority-observation.md) | 通用外部引用报告；无 Reddit adapter | WP2-B 已集成 | CLI + Architecture |

## Cross-Package Interfaces

```text
ContentDocument
  -> SeoModel / SeoIndexEntry
  -> deterministic HTML + JSON-LD + sitemap/search/llms projections
  -> .bukit/seo-route-map.json

question-target-map + external observations
  -> local readers
  -> route/question/citation matchers
  -> separate versioned reports
  -> human-reviewed hypotheses
```

- WP0/WP1-A 只修改文章结构化语义，不修改索引选择。
- WP1-B 只修改 llms 投影选择；不改变 robots、sitemap、search、feed。
- WP1-C 只通过共享 `SeoIndexEntry.Indexable` 修改 robots/sitemap/search/llms；RSS 继续按内容条目资格生成。
- WP1-D 只新增 warning，不修改 HTML、图片 URL、Schema 或构建退出码。
- WP2-A/B/C 复用现有 route map 和 URL 归一化；不改现有 URL 观测报告。

## Controller Workflow For Every Package

- [ ] **Step 1: Refresh baseline**

```bash
git status --short --branch
git rev-parse HEAD
git log -5 --oneline --decorate
```

Expected: 明确当前分支、HEAD 和用户已有修改；不得 stash、reset 或覆盖用户文件。

- [ ] **Step 2: Create isolated execution worktree**

使用 `superpowers:using-git-worktrees`。八个分支名依次固定为
`codex/seo-geo-next-wp0-newsarticle-publisher`、
`codex/seo-geo-next-wp1a-article-trust-graph`、
`codex/seo-geo-next-wp1b-llms-curation`、
`codex/seo-geo-next-wp1c-minimum-collection-index`、
`codex/seo-geo-next-wp1d-article-image-diagnostics`、
`codex/seo-geo-next-wp2a-question-coverage`、
`codex/seo-geo-next-wp2b-generative-citation`、
`codex/seo-geo-next-wp2c-external-authority`。计划所在 `main` 工作区不作为实现写入目标。

创建前先运行 `git branch -a` 预检，确认无同名或同前缀 `codex/seo-geo-next-*` 残留分支；若存在残留，先向用户确认处置方式，不得擅自删除或复用。

- [ ] **Step 3: Initialize and acquire the single-writer queue**

```bash
python3 scripts/checks/codex-workflow.py queue init --state /tmp/codex-reports/bukit-seo-geo-next-writer.json
python3 scripts/checks/codex-workflow.py queue acquire --state /tmp/codex-reports/bukit-seo-geo-next-writer.json --task wp0-newsarticle-publisher
```

Expected: WP0 独占 writer slot。后续工作包依次使用精确 task ID
`wp1a-article-trust-graph`、`wp1b-llms-curation`、
`wp1c-minimum-collection-index`、`wp1d-article-image-diagnostics`、
`wp2a-question-coverage`、`wp2b-generative-citation`、
`wp2c-external-authority`，不得复用已完成 ID。

若 `/tmp/codex-reports/` 状态文件因系统清理丢失：禁止盲目 `queue init` 重建（会丢失已完成 task ID 历史）。恢复方式为根据 `git log --oneline --decorate` 与 `codex/seo-geo-next-*` 分支/提交记录重建已完成工作包清单，再重新 `queue init` 并将已完成 task ID 的状态补齐；无法确认时向用户报告而非继续。

- [ ] **Step 4: Generate closure before the first code edit**

对该子计划 `Files` 中全部 Create/Modify/Test 路径逐个传入 `--changed`，并必须提供 `--policy` 参数指向 `scripts/checks/codex-workflow-policy.v1.json`：

```bash
python3 scripts/checks/codex-workflow.py closure --policy scripts/checks/codex-workflow-policy.v1.json --changed <path1> --changed <path2> ...
```

Expected: `unmappedFiles: []`，并记录 direct/contract consumers、public contract files、精确 specialty commands 和资源分类。

随后可用 `python3 scripts/checks/codex-workflow.py classify --policy scripts/checks/codex-workflow-policy.v1.json --path <path> ...` 核对分类；本总控全部工作包严格串行，dotnet 测试按 `dotnet-serial` 执行，Bukit fixture 构建、插件锁、manifest 与缓存操作按 `fixture-exclusive` 独占执行，不安排 `static-parallel` 并行写入。

- [ ] **Step 5: Execute the child plan test-first**

开始实现前迁移队列状态：

```bash
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/codex-reports/bukit-seo-geo-next-writer.json --task <wp-task-id> --to writing
```

每个 task 都遵循 RED -> minimal GREEN -> scoped refactor -> exact specialty test。禁止在 RED 阶段修改生产代码。进入测试阶段时迁移 `--to testing`。

- [ ] **Step 6: Cache and review evidence**

使用 `cache record` 保存匹配当前 HEAD、closure、命令、环境状态和 SDK 的 GREEN 证据；随后显式迁移状态：

```bash
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/codex-reports/bukit-seo-geo-next-writer.json --task <wp-task-id> --to review_wait
```

执行一次专项复审。

- [ ] **Step 7: Commit only the package paths**

先运行 `git diff --cached --name-only`，确认没有跨包文件，再使用子计划指定的提交信息。不得 push 或 merge，除非用户另行授权。

- [ ] **Step 8: Record metrics and release writer**

使用固定 metrics 状态路径记录 task phase duration、cache、rerun、writer conflict 和 completion status，例如：

```bash
python3 scripts/checks/codex-workflow.py metrics add --state /tmp/codex-reports/bukit-seo-geo-next-metrics.json --task <wp-task-id> --phase <phase> --duration-ms <n> --cache-status <hit|miss|none> --status <completed|blocked>
```

该 `--state` 路径必须与 Program Completion 中 `metrics report` 使用的 `/tmp/codex-reports/bukit-seo-geo-next-metrics.json` 保持一致。Critical/Important 为零后迁移 `--to done`（必要时先经 `blocked`）释放 writer slot。

## Package Acceptance Matrix

每个工作包只有在以下证据全部存在时才算完成：

1. 当前 HEAD 和变更文件与该包 closure 一致。
2. 所有新增/修改合同有直接消费者测试。
3. closure 中每条专项命令在相同 HEAD 上 GREEN，或命中严格匹配的缓存。
4. 未知字段、缺失字段、边界值、稳定排序、隐私和 Native AOT 路径均有测试。
5. 一次专项复审无 Critical/Important。
6. `git diff --cached --name-only` 只包含该包文件。
7. 文档明确“能力存在”与“外部结果得到证明”的区别。

## Program Completion

- [ ] 生成跨包 `review-scope`，只检查交叉点：Article JSON-LD、共享 indexability、llms representation inventory、route/question/citation keys、报告 schema 和未关闭的 Critical/Important。
- [ ] 复用各包未失效的 GREEN 证据，不重跑历史审计。
- [ ] 运行 `python3 scripts/checks/codex-workflow.py metrics report --state /tmp/codex-reports/bukit-seo-geo-next-metrics.json`，识别重复测试和队列延迟。
- [ ] 更新用户指南索引和最终能力矩阵，但不加入排名或 AI 引用承诺。
- [ ] 只有用户明确要求时才合并到 `main`；合并授权不包含 push、发布或外部采集。
