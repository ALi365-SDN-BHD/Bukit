# Bukit 文档与脚本深度审计 - 2026-07-09

## 范围

- 审计对象为当前主线文档与脚本：仓库根级文档、`docs/`、`guide/`、`scripts/`、`.github/`，以及备份/参考目录 `guide-0.1/`、`scripts-0.1/`、`guide-0.2/`、`scripts-0.2/`。
- 按 `AGENTS.md:5-24`，`guide-0.1/`、`scripts-0.1/`、`guide-0.2/` 与 `scripts-0.2/` 均仅作为备份/参考处理；这些目录不得作为官方文档、CI 门禁、发布脚本或运行时行为来源。
- 本报告只做只读审计，检查内容缺口、文档漂移、脚本/门禁漂移、超限文件与治理覆盖缺口。

## 验证运行

| 检查 | 结果 |
| --- | --- |
| `git status --short --untracked-files=all` | 当前仓库存在未跟踪的主线 `guide/` 与 `scripts/` 目录以及本报告；见 P0。 |
| `find docs guide scripts guide-0.1 scripts-0.1 guide-0.2 scripts-0.2 .github -type f` | 文档/脚本表面已枚举。 |
| `bash scripts/check-links.sh` | 通过；仅检查 docs/guide README/CODE_OF_CONDUCT/SECURITY/LICENSE。 |
| `bash scripts/check-governance.sh` | 通过；但不会覆盖根级文档、`.github/` 或 `guide-0.2/`。 |
| `bash scripts/check-doc-size.sh` | 通过；根级文档与 `.github/` 未纳入。 |
| `bash scripts/check-local-artifacts.sh` | 通过；只扫描根目录常见输出文件名。 |
| `bash scripts/check-tabs.sh` | 通过；当前只检查 cs/csproj/sln/yml/md/sh/json。 |
| `bash scripts/ci-fast.sh` | 通过；包含格式、build、unit/integration 测试、治理检查与 starter smoke。 |
| `bash scripts/quality-gate.sh` | 通过；当前是 ci-fast + trust + release readiness + governance + smoke 的组合，不再是“仅 CI 质量门”。 |
| `python3 scripts/scan_config_removed_fields.py` | 通过；未发现已移除配置字段。 |

## 执行摘要

仓库现在有一套可以执行且通过的主线 `scripts/` 门禁，当前 `guide/` 主线文档也比旧 `guide-0.1/` 更接近真实实现。但审计发现两个发布级阻塞项：

1. 真实主线 `guide/` 和 `scripts/` 目前未被 Git 跟踪，因此 CI/发布不能依赖它们，现有报告和本地通过结果也不会自动成为仓库事实。
2. `.github/workflows/` 缺失，已有 workflow 仍停留在 `.github/workflows-0.1/` 备份目录，并且引用旧 `scripts-0.1/`；这意味着 GitHub Actions 不会执行当前主线质量门禁。

此外，根级治理文档仍存在多处漂移：`CONTRIBUTING.md` 指向不存在的 workflow，`SECURITY.md` 仍描述旧的 hook/API-token 假设，门禁命名与脚本实际行为不一致，治理脚本也没有覆盖这些漂移区域。因此当前状态更像“本地门禁健康，但仓库发布治理尚未闭环”。

## 发现

### P0 - 主线 guide/scripts 未被 Git 跟踪

**证据**

- `git status --short --untracked-files=all` 显示 `guide/`、`scripts/` 和本审计报告均为未跟踪文件。
- 主线规则要求优先修改 `guide/` 与 `scripts/`，见 `AGENTS.md:16-19`。
- 当前通过的所有主线门禁都来自未跟踪的 `scripts/`，包括 `scripts/ci-fast.sh` 与 `scripts/quality-gate.sh`。

**影响**

- 本地审计通过不等于仓库发布事实；克隆仓库、CI 或发布分支可能完全看不到这些脚本和文档。
- 备份目录仍是仓库里唯一稳定可见的脚本/指南来源，这与 `AGENTS.md` 的主线偏好冲突。
- 后续所有关于 `guide/` / `scripts/` 的修复若不先纳入版本控制，都可能继续停留在工作区状态。

**建议**

先将 `guide/` 与 `scripts/` 作为真实主线资产纳入 Git，然后再基于它们修复治理与 CI。不要把 `guide-0.1/` 或 `scripts-0.1/` 当作修复目标。

### P0 - GitHub workflows 未激活，且没有与当前 scripts 闭环

**证据**

- 当前没有 `.github/workflows/` 目录；只有 `.github/workflows-0.1/ci.yml` 与 `.github/workflows-0.1/docs.yml`。
- `README.md:163-174` 将 `scripts/quality-gate.sh` 描述为推荐验证路径。
- `.github/workflows-0.1/ci.yml:57` 运行 `bash scripts-0.1/quality-gate.sh`。
- `.github/workflows-0.1/docs.yml:40-44` 运行 `scripts-0.1/check-links.sh`、`scripts-0.1/check-doc-size.sh`、`scripts-0.1/check-local-artifacts.sh`、`scripts-0.1/check-governance.sh`。
- `CONTRIBUTING.md:120-122` 仍称 CI workflow 定义在 `.github/workflows/ci.yml`，但该路径不存在。

**影响**

- GitHub Actions 不会自动运行当前主线 quality gate。
- 即使把 `workflows-0.1` 手动恢复到 `.github/workflows`，它仍会执行备份脚本，而不是当前 `scripts/`。
- 文档、CI 与实际本地验证路径三者之间存在发布阻塞级漂移。

**建议**

创建或恢复 `.github/workflows/ci.yml` 与 docs workflow，并让它们调用 `scripts/` 中的当前脚本。若 `workflows-0.1` 只是备份，应在文档中明确其非活跃状态。

### P1 - CONTRIBUTING 文档陈旧且存在断链

**证据**

- `CONTRIBUTING.md:101-107` 说明发布前使用 `scripts/quality-gate.sh`，但文件仍称其为“quality gate”，未提到当前脚本已扩展为 `ci-fast` + trust + release readiness + governance + smoke。
- `CONTRIBUTING.md:120-122` 指向不存在的 `.github/workflows/ci.yml`。
- `scripts/check-links.sh` 未扫描 `CONTRIBUTING.md`，因此该断链不会被当前链接检查发现。

**影响**

- 贡献者会以为 CI 文件存在且处于激活状态。
- 贡献流程没有解释当前门禁组合的真实成本、范围和失败归属。
- 治理脚本对贡献流程文档失明。

**建议**

更新 `CONTRIBUTING.md` 的 CI 与门禁章节，并将该文件加入链接检查。

### P1 - SECURITY 文档与 Core/Labs 边界和 token 契约冲突

**证据**

- `SECURITY.md:87` 称 `src/Bukit.Core` 的改动包含 hook 或扩展 API。
- `SECURITY.md:90-91` 要求扩展 API 保持兼容，并避免可变全局状态。
- 当前主线文档把插件隔离边界放在 `Bukit.PluginHost`、plugin manifest 与独立插件包中，例如 `guide/42-plugin-development.md:10-12`、`guide/42-plugin-development.md:184-193`。
- `SECURITY.md:35-48` 建议通过环境变量配置 API tokens，但当前 guide 强调 secrets 与 secrets provider，例如 `guide/45-external-publishing.md:30-55`。
- `scripts/check-governance.sh` 没有覆盖 `SECURITY.md`。

**影响**

- 安全审查入口仍在描述旧的 Core/hook 架构，可能把审查指向错误模块。
- secrets 使用建议与当前 guide 的强制边界不一致。
- 安全文档不是当前治理脚本的受检对象。

**建议**

重写 `SECURITY.md` 中的扩展/API-token 章节，使其匹配当前 `Bukit.PluginHost`、plugin manifest、secrets provider、外部发布与 deploy 边界。

### P1 - Gate 名称与实际行为不再一致

**证据**

- `scripts/ci-fast.sh:11-27` 运行格式、build、unit/integration 测试、治理检查与 starter smoke。
- `scripts/quality-gate.sh:13-19` 运行 `ci-fast`、trust hardening、release readiness、governance、starter smoke。
- `README.md:163-174` 把 quality gate 描述为“CI quality gate”。
- `docs/1.0-release-readiness.md:81-100` 将 `ci-fast` 与 `quality-gate` 分开列出，但没有说明 `quality-gate` 已重复调用 `ci-fast` 与 starter smoke。

**影响**

- 用户无法从名称判断脚本成本和覆盖范围。
- 失败 triage 边界模糊：同一 smoke 会在 `ci-fast` 和 `quality-gate` 中重复出现。
- 发布文档没有明确“开发快速门禁”与“发布完整门禁”的分层。

**建议**

统一命名与文档：将 `ci-fast` 定义为开发/PR 快速门禁，将 `quality-gate` 定义为发布候选完整门禁，并说明它包含/重复哪些子检查，或重构脚本避免重复运行。

### P1 - 治理检查覆盖不到已漂移的根级文档和 workflows

**证据**

- `scripts/check-governance.sh` 扫描 `guide/`、`docs/`、`guide-0.1/`、`docs/legacy/`、`guide-0.2/`、`scripts-0.2/`。
- 它没有扫描 `README.md`、`CONTRIBUTING.md`、`SECURITY.md`、`.github/`。
- `CONTRIBUTING.md` 和 `SECURITY.md` 的漂移没有被该脚本发现。
- `scripts/check-links.sh` 也没有扫描 `CONTRIBUTING.md`。

**影响**

- 治理脚本给出“通过”时，关键根级治理文档仍可能不可信。
- workflow 漂移不会被任何当前脚本捕获。

**建议**

扩展 governance/link 检查范围，至少覆盖根级治理文档与 `.github/workflows*`。同时明确哪些备份目录只做参考扫描，哪些目录是发布阻塞扫描。

### P2 - guide-0.2/ 和 scripts-0.2/ 必须保持备份/参考身份

**证据**

- `AGENTS.md` 已将 `guide-0.2/` 与 `scripts-0.2/` 明确纳入备份/参考目录。
- `guide/` 与 `scripts/` 才是官方文档和脚本入口。
- `guide-0.2/` 与 `scripts-0.2/` 仍然存在，且内容规模大于当前主线目录。
- `scripts/check-governance.sh` 会扫描 `guide-0.2/` 与 `scripts-0.2/` 中的配置漂移。
- `scripts/check-links.sh` 不扫描 `guide-0.2/` 或 `scripts-0.2/`。
- `scripts/check-doc-size.sh` 扫描 `guide-0.2/`，但不扫描 `scripts-0.2/`。

**影响**

- 生命周期规则已明确：0.2 目录只能作为备份/参考，严禁作为官方文档和脚本使用。
- 残余风险是 CI、发布、README 或治理脚本仍可能误引用、误执行或误扫描 0.2 目录。
- 不同检查脚本对 0.2 目录的覆盖不一致，容易让备份目录看起来像活跃主线。

**建议**

保持 `guide-0.2/` 与 `scripts-0.2/` 只读参考身份。若需要其中的内容，应移植到 `guide/` 或 `scripts/` 并在主线验证；不要从 CI、发布脚本、README、贡献指南或安全文档直接链接/执行 0.2 目录。

### P2 - 治理文档仍包含陈旧的本机绝对源码链接

**证据**

- `docs/governance/archive/0.1.1-update-plan.md:48` 指向 `/Users/ali/.../src/Bukit.Cli/Commands/BuildCommand.cs:185`。
- `scripts/check-governance.sh` 有 `scan_abs_paths`，但 archive 目录未纳入扫描集合。

**影响**

- 归档文档泄露本机路径，并且链接不可移植。
- 当前治理脚本无法阻止类似路径残留在 archive 中。

**建议**

要么从 archive 中清理本机绝对路径，要么在治理脚本中显式说明 archive 可包含历史原始路径。若目标是发布文档整洁，建议扫描 `docs/governance/archive/` 的绝对路径。

### P2 - 本地产物扫描范围过窄

**证据**

- `scripts/check-local-artifacts.sh` 只在仓库根目录检查固定文件名，例如 `bukit.db`、`openapi.json`、`.coverage`、`.env`。
- 它不扫描子目录中的 `.env`、数据库、coverage、trx/binlog、临时输出等。
- 当前仓库已有多个生成/临时风险模式，但脚本只覆盖极少数根级路径。

**影响**

- 本地敏感或生成产物可能落在子目录而不被检测。
- “local artifacts” gate 的名称暗示范围大于实际范围。

**建议**

将脚本改名为 root-artifact check，或扩展为有白名单的递归扫描，覆盖常见输出与 secret 文件模式。

### P2 - active guide/scripts 以外的超限策略不清

**证据**

- `scripts/check-doc-size.sh` 当前扫描 `docs`、`guide`、`guide-0.1`、`guide-0.2` 下的 Markdown。
- 它不扫描 `README.md`、`CONTRIBUTING.md`、`SECURITY.md`、`.github`、`scripts`。
- 当前运行结果通过，但这只证明被扫描的 Markdown 未超过 1000 行。

**影响**

- 根级文档可以无限增长而不触发“超限”检查。
- 脚本文件体积/复杂度没有任何大小或复杂度门禁。

**建议**

明确“文档大小”门禁只覆盖 guide/docs，或扩展到根级治理文档。脚本复杂度若重要，应单独增加 shellcheck/复杂度/行数策略，而不是混入 Markdown size gate。

## 当前健康的部分

- `guide/` 中当前主线 guide 的链接检查、文档大小检查、治理检查均通过。
- `scripts/ci-fast.sh` 和 `scripts/quality-gate.sh` 在本地均可运行并通过。
- `scripts/scan_config_removed_fields.py` 没有发现已移除配置字段残留。
- `guide/42-plugin-development.md`、`guide/45-external-publishing.md`、`guide/49-publishing-edge-cases.md` 与当前插件/发布边界整体一致。
- `scripts-0.1/`、`guide-0.1/`、`scripts-0.2/` 和 `guide-0.2/` 按 AGENTS 规则可继续作为参考，但不应作为主线修复目标或官方入口。

## 建议修复顺序

1. 将 `guide/` 与 `scripts/` 纳入版本控制，确认它们是当前主线资产。
2. 创建/恢复 `.github/workflows/`，并让 workflow 调用当前 `scripts/`，而不是 `scripts-0.1/`。
3. 更新 `CONTRIBUTING.md` 与 `SECURITY.md`，修复 CI 路径、门禁分层、Core/Labs/plugin/secrets 边界。
4. 扩展 governance/link 检查，使根级治理文档与 active workflows 进入发布阻塞范围。
5. 统一各脚本扫描策略，确保 `guide-0.2/` 与 `scripts-0.2/` 只作为备份/参考，不被官方 workflow、发布脚本或文档入口使用。
6. 决定 archive 绝对路径、本地产物递归扫描、根级文档大小与脚本复杂度是否纳入发布门禁。
