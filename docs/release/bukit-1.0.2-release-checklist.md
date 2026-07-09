# Bukit 1.0.2 Release Checklist（P0-2，硬门槛）

## 目标

1.0.2 发布前必须基于真实 CI 绿灯证据（GitHub Actions workflow run completed success）。
以下条目全部满足后，release 才允许继续。

## 本轮验收执行序列（快速）

- `bash scripts/gates/ci-fast.sh Release`
- `bash scripts/gates/ci-full.sh Release`
- `bash scripts/checks/coverage-baseline-schema.sh`
- `bash scripts/checks/coverage.sh Release`
- `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~DevFileWatcher_RebuildFailure_DoesNotDisposeWatcher|FullyQualifiedName~DevFileWatcher_RapidChanges_DebouncedToSingleRebuild|FullyQualifiedName~DevRequestHandler_LiveReloadScript_UsesSameOriginWebSocket"`
- `bash scripts/smoke/release-artifacts.sh TestResults/release-gate/native-aot/linux-x64`
- `bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" <下载目录>`

## 一键验收脚本（可直接使用）

```bash
export RELEASE_VERSION="1.0.2"
export RELEASE_COMMIT="${GITHUB_SHA}"
```

```bash
bash scripts/release/check-p1-2-1.0.2.sh
```

可选参数：`release_dir`、`release_version`、`release_commit`、`release_download_dir`

示例：

```bash
bash scripts/release/check-p1-2-1.0.2.sh \
  TestResults/release-gate/native-aot/linux-x64 \
  "$RELEASE_VERSION" \
  "$RELEASE_COMMIT" \
  ./release-assets
```

若你手头是已下载的 release 产物目录，可直接走资产复核一键脚本：

```bash
bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" ./release-assets
```

## 发布操作模板（维护者）

- 先执行统一模板： [Release Precheck Template](release-prerelease-template.md)

## Release order（v1.0.2）

1. Merge to main
2. Wait for `.github/workflows/ci.yaml` completed success
3. Confirm Core coverage artifact
4. Create tag `v$RELEASE_VERSION`
5. Release workflow runs
6. 下载 release 资产后执行一致性复核：
   ```bash
   bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" ./release-assets
   ```

## 1.0.2 Release Hard Gate

| # | 条目 | 证据文件 | 位置 | 通过标准 |
|---|---|---|---|---|
| P0 | CI workflow success | `.github/workflows/ci.yaml` | GitHub Actions | `Fast contracts`、`Core tests`、`Core coverage` 都必须成功。 |
| P0 | Core coverage artifact | `coverage-summary.txt` / `coverage-summary.json` | `TestResults/coverage/` | 文件存在且包含 Core-only coverage 摘要。 |
| P0 | release artifact smoke report | `release-artifact-smoke.md` | `TestResults/release-gate/native-aot/linux-x64/release-artifact-smoke.md`（release-gate 产物） | 文件存在且包含步骤记录（至少 1 个 `PASS` 条目）。 |
| P1 | config schema artifact | `site.schema.json` | `TestResults/release-gate/site.schema.json` | 文件存在且为有效 JSON（可解析）。 |
| P1 | Coverage baseline | `coverage-baselines.json` | `docs/coverage-baselines.json` | 必须通过 `bash scripts/checks/coverage-baseline-schema.sh`。文件使用 Core-only v2 policy。 |
| P1 | Native AOT 发布构建 | `linux-x64` native-aot 产物 | `TestResults/release-gate/native-aot/linux-x64/` | 目录存在并包含 release smoke 可追溯产物。 |

## 覆盖率基线维护说明（1.0.2）

- 基线文件：`docs/coverage-baselines.json`。
- 修改策略：
  - policy 使用 `version=2.0.0`、`scope=core`、`metric=line`。
  - `minimums.overall` 是 Core 整体 line coverage 下限。
  - `minimums.projectFloor` 是每个 `src/Bukit-Core/*` 项目的最低 line coverage 下限。
- 约束：本 gate 不包含 Plugins 或 Labs。若未来要加入，必须作为独立任务同步更新脚本、workflow、文档和 Architecture.Tests。
- 更新后请确保 release checklist 的 P1 条目与 `TestResults/coverage/coverage-summary.txt` 同步可读。

## 阻断规则

- 缺任何 P0 条目：**Release BLOCKED**（阻断发布）。
- 缺 P1 条目：**Release BLOCKED**（待补齐）。

## 发版前最终验收（人工核对）

- GitHub Actions `release` workflow 的 Core tests、Core coverage、Security check 和 release asset collection jobs 通过。
- 该 commit 在 `main` 或 `master` 分支上的 `.github/workflows/ci.yaml` 通过。
- 在 release artifact 下载包中能看到以下文件/目录：
  - `TestResults/release-gate/site.schema.json`
  - `TestResults/coverage/coverage-summary.txt`
  - `TestResults/coverage/coverage-summary.json`
  - `docs/coverage-baselines.json`
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
   - 运行：`bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" <下载目录>`
   - `release-assets-check.md` 期望输出应包含：
     - `release asset checks passed`
     - `set_mode=exact-match`
   - 自动验收断言：`set_mode` 必须为 `exact-match`，否则不通过。
   - Workflow Step Summary 中应出现 `Release asset strict check`，并包含完整检查日志（含 `set_mode=exact-match`）
 - 在 CI 证据路径应看到：`TestResults/release-assets/release-assets-check.md`

## P1-3：release artifact smoke 增强验收（待确认）

在 `TestResults/release-gate/native-aot/<rid>/release-artifact-smoke.md`（或对应 publish-dir）中要求新增步骤项：

- help text
- version text
- schema
- build
- quality audits（seo / geo / publish）
- deploy dry-run
- LiveReload wording（`bukit dev --help`）
- non-Core command absence（help 中不得出现 Core 外命令）
- Dev 重建回归测试（`DevFileWatcher_RebuildFailure_DoesNotDisposeWatcher`、`DevFileWatcher_RapidChanges_DebouncedToSingleRebuild`、`DevRequestHandler_LiveReloadScript_UsesSameOriginWebSocket`）

验收命令（脚本层）：

```bash
bash scripts/smoke/release-artifacts.sh TestResults/release-gate/native-aot/linux-x64
```

人工核对时应同时确认报告中至少包含以下 PASS 项：

- `Version command returns version text`
- `CLI help includes core commands`
- `CLI help excludes non-Core command family`
- `CLI dev help includes LiveReload wording`
- `CLI dev help excludes HMR wording`
- `Deploy dry-run fixture site`
- `Dev server rebuild regression tests`

对应执行命令（含 release artifact + release-level 统一复核）：

```bash
bash scripts/smoke/release-artifacts.sh TestResults/release-gate/native-aot/linux-x64
bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" ./release-assets
```

## P2-2：dev 服务器回归测试补充（本轮建议）

- 新增 `tests/Bukit.Cli.Tests/DevCommandTests.cs` 回归项（已落盘）：
  - `DevFileWatcher_RebuildFailure_DoesNotDisposeWatcher`
  - `DevFileWatcher_RapidChanges_DebouncedToSingleRebuild`
  - `DevRequestHandler_LiveReloadScript_UsesSameOriginWebSocket`
- 本地验收命令（建议）：
  - `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~DevFileWatcher_RebuildFailure_DoesNotDisposeWatcher|FullyQualifiedName~DevFileWatcher_RapidChanges_DebouncedToSingleRebuild|FullyQualifiedName~DevRequestHandler_LiveReloadScript_UsesSameOriginWebSocket"`

### 快速验证命令（可执行）

```bash
bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" <下载目录>
```

示例：

```bash
bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" ./release-assets
```

参数化（可直接复制）：

```bash
RELEASE_VERSION=1.0.2
RELEASE_COMMIT="${GITHUB_SHA}"
bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" ./release-assets
```

### 本地 Dry-run 演练（可选）

```bash
tmpdir="$(mktemp -d)"
cp bukit-*/bukit-"$RELEASE_VERSION"-*.tar.gz "$tmpdir"/ 2>/dev/null || true
cp bukit-win-x64/bukit-"$RELEASE_VERSION"-win-x64.zip "$tmpdir"/
cp bukit-skills.zip "$tmpdir"/
bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" "$tmpdir"
rm -rf "$tmpdir"
```

## 备注

- `GITHUB_ACTIONS` 判定必须以 true/false 兼容语义解析，避免 `true` 被误判为非 CI 环境导致证据检查跳过。
