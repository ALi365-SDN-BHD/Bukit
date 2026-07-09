# Bukit 文档与脚本深度审计修复复审 - 2026-07-09

## 范围

本复审核对 `docs/analysis/bukit-docs-scripts-deep-audit-2026-07-09.md`
报告后续修复在当前工作区中的真实状态，覆盖：

- 根级公开文档：`README*`、`CONTRIBUTING*`、`SECURITY*`。
- 活跃 guide/script：`guide/`、`scripts/`。
- 活跃 workflow：`.github/workflows/ci.yaml`、`.github/workflows/release.yaml`。
- 公开治理面：`.github/PULL_REQUEST_TEMPLATE.md`、`docs/compatibility-governance*.md`。
- 备份/reference 边界：`guide-0.x/`、`scripts-0.x/`、`.github/workflows-0.1/`。

`guide-0.x/`、`scripts-0.x/`、`.github/workflows-0.1/` 只作为备份和参考，
本复审不把它们作为官方文档、官方脚本或 active gate 来源。

## 结论

原审计报告中的 P0/P1/P2 主问题已经在活跃文档、脚本和 workflow 面闭环。

复审过程中发现并修复了一个新的残留问题：`docs/compatibility-governance*.md`
虽然已清理本机绝对路径，但仍引用不存在的 `ConfigDeprecationScanner.cs`，
并把 `site.plugins.rss`、`site.collections.*.rss`、`site.collection`、
旧 Notion page-root 字段写成 warning-only 语义。当前实现并不是该语义：

- `site.plugins.<name>` 是 Core 内置插件开关，仍为 supported。
- 旧 `site.collections.*.rss`、单数 `site.collection`、旧 Notion page-root 字段
  由当前严格配置契约拒绝。
- 当前 Notion 内容源契约是 `content.sources[].notion.databaseId`。

该残留已通过更新 `docs/compatibility-governance.md` 和
`docs/compatibility-governance.zh-CN.md` 修复，并新增 active Markdown
链接检查防止类似死链回归。

## 原报告逐项复审

| 原问题 | 当前状态 | 当前证据 |
|---|---|---|
| P0 - Mainline `guide/` 和 `scripts/` 未 tracked | 已修复 | `git ls-files guide scripts .github/workflows` 返回 123 个 tracked 文件；`git status --untracked-files=all -- guide scripts .github/workflows` 只剩本次新增/修改脚本，没有整棵 mainline 未跟踪。 |
| P0 - GitHub workflows 未激活且不闭环当前 scripts | 已修复 | `.github/workflows/ci.yaml` 和 `.github/workflows/release.yaml` 存在；YAML 解析通过；workflow 中引用的 `scripts/*.sh` 全部存在；active boundary gate 通过。 |
| P1 - `CONTRIBUTING*` 过期 | 已修复 | 中英马文档均描述 `quality-gate.sh` 是 `ci-fast` 兼容包装器，不再引用 `scripts/smoke.ps1`、旧 guide 文件或旧 AOT 检查脚本；`public-doc-contracts.sh` 通过。 |
| P1 - `SECURITY*` Core/Labs/token 边界漂移 | 已修复 | 中英马文档均改为 Core/Labs 分层、外部进程插件和 generic secrets 口径；不再把 Labs webhook 写成 Core 命令，不再要求 `BUKIT_NOTION_TOKEN`，不再描述 WASM 插件。 |
| P1 - Gate 名称与实际行为不一致 | 已修复 | `README*`、`CONTRIBUTING*`、`guide/dev/release.md`、`guide/dev/testing.md` 均把 fast/core/release gate 分层写清；PR 模板不再把 coverage/format/600 行写成 `quality-gate` 自动覆盖。 |
| P1 - 治理检查覆盖不到根级文档/workflows | 已修复 | `docs-consistency.sh` 现在串联 public docs contract、绝对路径扫描、active links、size policy、Core boundary、local artifact scan。 |
| P2 - `guide-0.2/` / `scripts-0.2/` 边界不清 | 已修复 active 边界 | active workflow/script surface 通过 `active-workflow-boundary.sh`；活跃脚本不调用 backup/reference 树。 |
| P2 - 治理文档包含本机绝对源码链接 | 已修复并加防回归 | `no-absolute-paths.sh` 覆盖公开治理面；`active-links.sh` 覆盖活跃 Markdown 相对链接；两项均通过。 |
| P2 - 本地产物扫描范围过窄 | 已修复 active 范围 | `no-local-artifacts.sh` 递归扫描常见本地产物和敏感文件，并排除 backup/reference 树与 fixture allowlist；当前通过。 |
| P2 - active guide/scripts 以外超限策略不清 | 已修复 active 策略 | `size-policy.sh` 明确 root public docs、`.github` reader docs、`guide/`、compatibility governance 和 active shell scripts 的行数上限；当前通过。 |

## 复审新增修复

### 1. Active Markdown 链接 gate

新增 `scripts/checks/docs/active-links.sh`，由
`scripts/checks/docs-consistency.sh` 调用。它检查以下活跃 Markdown 面中的
相对链接是否仍在仓库内且目标存在：

- `README*.md`
- `CONTRIBUTING*.md`
- `SECURITY*.md`
- `.github/PULL_REQUEST_TEMPLATE.md`
- `guide/**/*.md`
- `docs/compatibility-governance*.md`
- `docs/governance/**/*.md`，如果该目录存在

该 gate 捕获了复审中发现的 `ConfigDeprecationScanner.cs` 死链。

### 2. Compatibility governance 语义修正

`CG-011` 到 `CG-014` 已从旧 warning-only 说法改为当前实现：

- `CG-011`：`site.plugins.<name>` 是 supported 的 Core 内置插件开关。
- `CG-012`：旧 `site.collections.*.rss` shortcut 是 rejected。
- `CG-013`：单数 `site.collection` 是 rejected。
- `CG-014`：旧 Notion page-root 字段是 rejected，当前契约是 `databaseId`。

## 当前保留边界

- `guide-0.2/` 下仍有 `.DS_Store` 等本地产物痕迹，但它属于 backup/reference
  树。按当前仓库规则，本次不修改 backup/reference 树，也不把它纳入 active gate。
- `docs/analysis/bukit-docs-scripts-deep-audit-2026-07-09.md` 是历史审计快照，
  未改写原始结论；本文件记录修复后的当前状态。
- 工作区仍有未提交修改和新增脚本；本复审只判断当前工作区真实状态，不代表
  这些修复已经提交到远端。

## 验证命令

| 命令 | 结果 |
|---|---|
| `python3` 解析 `.github/workflows/ci.yaml` 和 `.github/workflows/release.yaml` | 通过 |
| workflow script 引用存在性扫描 | 通过 |
| `bash scripts/checks/docs/active-links.sh` | 通过 |
| `bash scripts/checks/docs/no-absolute-paths.sh` | 通过 |
| `bash scripts/checks/docs/no-local-artifacts.sh` | 通过 |
| `bash scripts/checks/docs/size-policy.sh` | 通过 |
| `bash scripts/checks/docs-consistency.sh` | 通过 |
| `bash scripts/checks/active-workflow-boundary.sh` | 通过 |
| `bash scripts/quality-gate.sh Release` | 通过 |
| `BUKIT_CI_FULL_SKIP_FAST=1 bash scripts/gates/ci-full.sh Release` | 通过，3281 个 Core 测试 |
| release helper fake archive 验证 | 通过，三 RID 成功，故意缺少 expected RID 时正确失败 |
| `git diff --check` | 通过 |

## 最终判断

截至本复审完成时，原报告中要求修复的 active docs/scripts/workflows 问题没有发现
仍未闭环的 active 阻塞项。剩余需要注意的是流程状态而非代码事实：当前修复仍在
工作区中，需提交后才会成为分支上的正式状态。
