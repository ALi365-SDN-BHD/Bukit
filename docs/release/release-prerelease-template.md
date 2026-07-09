# Release Precheck Template（维护者）

## 发布前主干 CI 预检（强制）

1. 先在 `main` 或 `master` 上提交待发布改动，并等待 `.github/workflows/ci.yaml` 全量通过。
2. 在打 tag 前确认同一 commit 的主干 CI 绿灯：
   - `Fast contracts`、`Core tests`、`Core coverage` 都必须成功。
3. 确认无误后再创建并推送 tag：
   - `git tag vX.Y.Z`
   - `git push origin vX.Y.Z`
4. 触发/等待 release，确认 release run 中包含并通过：
   - `Core tests`
   - `Core coverage`
   - `Security check`
   - release asset collection / verification
5. 下载 `core-coverage` artifact，确认包含：
   - `TestResults/coverage/coverage-summary.txt`
   - `TestResults/coverage/coverage-summary.json`
   - `docs/coverage-baselines.json`
   - `docs/coverage-baselines.json` 使用 `version=2.0.0`、`scope=core`、`metric=line`

## Release order（`v$RELEASE_VERSION` 起）

1. Merge to main
2. Wait for `.github/workflows/ci.yaml` completed success
3. Confirm Core coverage artifact
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
