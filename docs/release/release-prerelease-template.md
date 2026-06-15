# Release Precheck Template（维护者）

## 发布前主干 CI 预检（强制）

1. 先在 `main` 或 `master` 上提交待发布改动，并等待 `ci.yml` 全量通过。
2. 在打 tag 前确认同一 commit 的主干 CI 绿灯：
   - 目标 commit 的 `workflow_runs` 至少存在 1 条 `ci.yml` 记录，且 `status=completed`、`conclusion=success`，`head_branch` 为 `main` 或 `master`。
3. 确认无误后再创建并推送 tag：
   - `git tag vX.Y.Z`
   - `git push origin vX.Y.Z`
4. 触发/等待 release，确认 `release-gate` 产物中包含：
   - `TestResults/release-gate/ci-workflow-evidence.json`
   - `TestResults/release-gate/rc-gate-evidence.md`
   - `TestResults/release-gate/coverage-summary.txt`
   - `docs/coverage-baselines.json`
   - `docs/coverage-baselines.json` 包含 `core/cli/importing/labs` 条目；`core`/`cli` 使用 `blocking: true + minimum`，`importing`/`labs` 使用 `blocking: false + baseline`

## Release order（`v$RELEASE_VERSION` 起）

1. Merge to main
2. Wait for ci.yml completed success
3. Confirm workflow evidence
4. Create tag `v$RELEASE_VERSION`
5. Release workflow runs
6. 下载 release 资产后执行一致性复核（release-manifest/release-assets）：
   ```bash
   RELEASE_VERSION="<version>" # 例如 1.0.2
   RELEASE_COMMIT="${GITHUB_SHA}"
   bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" ./release-assets
   ```

示例（可直接复制）：

```bash
RELEASE_VERSION="1.0.2"
RELEASE_COMMIT="${GITHUB_SHA}"
bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" ./release-assets
```

## 版本复用模板（任意版本）

```bash
export RELEASE_VERSION="x.y.z"      # e.g. 1.0.3
export RELEASE_COMMIT="<release_commit_sha>"
bash scripts/release/verify-release-assets.sh \
  "$RELEASE_VERSION" \
  "$RELEASE_COMMIT" \
  ./release-assets
```
