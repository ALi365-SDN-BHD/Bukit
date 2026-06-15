# Bukit 1.0.2 Release Checklist（P0-2，硬门槛）

## 目标

1.0.2 发布前必须基于真实 CI 绿灯证据（GitHub Actions workflow run completed success）。
以下条目全部满足后，release 才允许继续。

## 1.0.2 Release Hard Gate

| # | 条目 | 证据文件 | 位置 | 通过标准 |
|---|---|---|---|---|
| P0 | ci.yml completed success evidence | `ci-workflow-evidence.json` | `TestResults/release-gate/ci-workflow-evidence.json` | `workflow_runs` 必须包含至少 1 条 status=`completed` 且 conclusion=`success` 的 `ci.yml` 记录（针对同一 commit SHA）。 |
| P0 | Evidence Markdown | `rc-gate-evidence.md` | `TestResults/release-gate/rc-gate-evidence.md` | 文件存在且非空。 |
| P0 | release artifact smoke report | `release-artifact-smoke.md` | `TestResults/release-gate/native-aot/linux-x64/release-artifact-smoke.md`（release-gate 产物） | 文件存在且包含步骤记录（至少 1 个 `PASS` 条目）。 |
| P1 | config schema artifact | `site.schema.json` | `TestResults/release-gate/site.schema.json` | 文件存在且为有效 JSON（可解析）。 |
| P1 | Coverage summary | `coverage-summary.txt` | `TestResults/release-gate/coverage-summary.txt` | 文件存在且包含覆盖率摘要字段。 |
| P1 | Native AOT 发布构建 | `linux-x64` native-aot 产物 | `TestResults/release-gate/native-aot/linux-x64/` | 目录存在并包含 release smoke 可追溯产物。 |

## 阻断规则

- 缺任何 P0 条目：**Release BLOCKED**（阻断发布）。
- 缺 P1 条目：**Release BLOCKED**（待补齐）。

## 发版前最终验收（人工核对）

- GitHub Actions `release` 的 `release-gate` job 通过。
- 在 release artifact 下载包中能看到以下文件/目录：
  - `TestResults/release-gate/ci-workflow-evidence.json`
  - `TestResults/release-gate/rc-gate-evidence.md`
  - `TestResults/release-gate/site.schema.json`
  - `TestResults/release-gate/coverage-summary.txt`
  - `TestResults/release-gate/native-aot/linux-x64/`
  - `TestResults/release-gate/native-aot/linux-x64/release-artifact-smoke.md`

## P1-2：发布资产验收（强制）

在 `action-gh-release` 上传前后，需逐项确认：

1. 资产列表包含：
   - `bukit-1.0.2-linux-x64.tar.gz`
   - `bukit-1.0.2-osx-arm64.tar.gz`
   - `bukit-1.0.2-win-x64.zip`
   - `bukit-skills.zip`
   - `checksums.txt`
   - `checksums.json`
   - `release-manifest.json`

2. `checksums.txt` 需要满足：
   - 文件开头包含版本与 schema 元信息注释。
   - 每行一条：`<sha256 hex><两个空格><文件名>`。
   - `checksums.txt` 内文件名与 `checksums.json`、`release-manifest.json` 覆盖集合完全一致，且仅包含 4 个必需文件：
    - `bukit-1.0.2-linux-x64.tar.gz`
    - `bukit-1.0.2-osx-arm64.tar.gz`
    - `bukit-1.0.2-win-x64.zip`
    - `bukit-skills.zip`

3. `checksums.json` 需要满足：
   - 可解析 JSON。
   - `schema` 为 `https://bukit.dev/schemas/release-bundle-checksums.v1.json`。
   - `schemaVersion` 为 `1.0`。
   - `fileCount` 与 `files.length` 一致。
   - `files[*]` 中的每项同时包含 `path/hash/size`。
   - `files` 仅允许出现这 4 个必需文件，不允许遗漏或额外文件。

4. `release-manifest.json` 需要满足：
   - 可解析 JSON。
   - `version` 为 tag 版本（不含 `v`）。
   - `commit` 与发布 commit 一致。
   - `artifacts` 数组按 `rid, file` 排序。
   - `artifacts` 数量必须为 4 且文件名集合与 `checksums.json.files` 完全一致（无增删）。

5. `artifacts` 中的 RID 语义核对：
   - `linux-x64`
   - `osx-arm64`
   - `win-x64`
   - `skills`

6. `artifacts[*].sha256` 为带前缀值（`sha256:<hex>`）。

7. 验证执行与归档：
   - 运行：`bash scripts/checks/release-assets.sh <下载目录> <发布版本> <发布 Commit>`
   - `release-assets-check.md` 期望输出应包含：
     - `release asset checks passed`
     - `set_mode=exact-match`
   - 自动验收断言：`set_mode` 必须为 `exact-match`，否则不通过。
   - Workflow Step Summary 中应出现 `Release asset strict check`，并包含完整检查日志（含 `set_mode=exact-match`）
   - 在 CI 证据路径应看到：`TestResults/release-assets/release-assets-check.md`

### 快速验证命令（可执行）

```bash
bash scripts/checks/release-assets.sh <下载目录> <发布版本> <发布 Commit>
```

示例：

```bash
bash scripts/checks/release-assets.sh ./release-assets 1.0.2 0123abc...
```

### 本地 Dry-run 演练（可选）

```bash
tmpdir="$(mktemp -d)"
cp bukit-*/bukit-1.0.2-*.tar.gz "$tmpdir"/ 2>/dev/null || true
cp bukit-win-x64/bukit-1.0.2-win-x64.zip "$tmpdir"/
cp bukit-skills.zip checksums.txt checksums.json release-manifest.json "$tmpdir"/
bash scripts/checks/release-assets.sh "$tmpdir" 1.0.2 <release-commit-sha>
rm -rf "$tmpdir"
```

## 备注

- `GITHUB_ACTIONS` 判定必须以 true/false 兼容语义解析，避免 `true` 被误判为非 CI 环境导致证据检查跳过。
