# Bukit Notion 错误码与快照格式校验（2.2.0）

- 日期：2026-11-14
- 分支：`feat/notion-2.1.1`
- 提交：`dd05f872`（第 1 层）、`49512437`（第 2 层）、`e4a84e1a`（审计小项）
- 来源方案：BukitJalil `docs/notion-error-codes-and-snapshot-version.md` 第 3、4 节（D1–D8）
- 版本：**2.2.0**，取代方案 D2 的 2.1.1。理由：公共枚举新增 + CLI 行为变化，按语义化版本用一个 minor 信号；不升 3.0.0，因为这次是把 CLI 输出改回文档约定的 `BKT-xxxx`，且已知消费者 BukitJalil 不读旧 `code` 或退出码。

## D1–D8

| | 决定 |
|---|---|
| D1 | cli-error 的 `code` 从枚举名改为 `BKT-xxxx`；`bukit-cli-reference` 的示例同步。 |
| D2 | 版本 2.2.0（见上）。 |
| D3 | 单语言 `build` 的快照错误退出 2；`config check` 和 `doctor` 仍返回 1；多语言 `build` 仍返回 1。 |
| D4 | Notion 码段 `0x0Bxx`：接口 0B01–0B08，快照 0B10–0B17。 |
| D5 | 多语言父进程解析子进程 stderr 的 `Error: [BKT-xxxx] …`；退出码 1；找不到时回退 `i18n-worktree-worker-failed`。 |
| D6 | 新 catch 只接 `NotionSnapshotException`；I18n、资源错误仍是退出 1 / `cli-error`。 |
| D7 | `(kind, status) → code` 在 `Bukit.Content` internal；`Bukit.Engine` internal 桥接；不改 Core 依赖边界。 |
| D8 | BukitJalil 先读 cli-error 的 `code`，再看行首 `[BKT-0Bxx]` 或 `✖ [BKT-0Bxx]`；不认 `⚠` 和行中间出现的码。 |

## 对外可见变化

- `DiagnosticCode` 新增 16 个公共成员（0B01–0B08、0B10–0B17）；public API 基线已更新。
- 顶层配置、内容、渲染错误的 cli-error `code` 改用 `BKT-xxxx`。
- 单语言 `build` 的快照错误退出码从 1 改为 2；多语言保持 1。
- 快照只校验 `FormatVersion`；`bukitVersion` 只用于诊断，升级 Bukit 不再让快照失效。
- 多语言 worker 失败时，父进程的 cli-error 带上原来的 `BKT-xxxx` 码和原因。

## 验证

- 最终代码（`e4a84e1a`）：CLI 1,324 passed / 5 skipped（1,329 total）；Engine 3,584；Architecture 422。Shared 545、Content 694、Content.Notion 108 来自同一内容提交 `49512437`（`e4a84e1a` 未改这些项目/测试）。
- `dotnet format`、analyzer ratchet 419/421、public-api-drift、docs、skills、`openspec validate --all` 29/29 通过。
- 独立审查：0 Critical / 0 Important；3 个 Important 已闭环；3 个 Minor 记录（多语言 stdout/stderr 未拆分、D6 测试走反射、`LastWorkerFailure` 只测一行）。
- 审计复核：9 条中 8 条成立；#3 修正为内联 `CodeFor`（已做）；#2/#5/#7 已做；#4/#8/#9 未做。

## 未纳入

- **快照提交崩溃窗口**（`NotionSnapshotSession.RemoveAbandonedSiblings` / `Swap`）：`origin/main` 已存在，不是这两个提交引入。按 SOP 作为独立 Core 任务，发版前修：读路径不重命名；`OpenOffline` 发现目标缺失且有 `.old-*` 时报告具体原因；测试模拟两次重命名之间被杀。
- 审计 #4/#8/#9 是可选测试或健壮性项，未纳入本次。
- 方案文档的 fixture 措辞（#6）在 BukitJalil 仓库单独改。
