# Bukit Core 1.0.4 开发修复计划书

> Historical planning record. Do not execute these commands as the current
> coverage or release contract. Use `release-prerelease-template.md` and
> `.github/workflows/ci.yaml` for the active contract.

文档版本：v1.0
目标版本：Bukit Core 1.0.4
版本类型：维护补丁 / Release & CI Hardening
适用分支：`main` / `release/1.0.x`
适用范围：Bukit Core 1.0.x 维护线
不适用范围：Labs 功能扩展、Import/Clone 插件化、Theme Registry、Plugin Marketplace、BukitJalil

---

## 1. 版本定位

Bukit Core 1.0.4 不做新功能，不扩展 Core CLI 命令面，不引入 Labs 能力。

1.0.4 的核心目标是：

```text
把 1.0.3 已经建立的 release-hardening 机制继续做成可测试、可复现、可离线验证、可长期维护的质量闭环。
```

换句话说：

```text
1.0.2 = release pipeline 成型
1.0.3 = release pipeline 补强
1.0.4 = release / CI / fixture / evidence 全面防回退
```

---

## 2. 总目标

1.0.4 需要重点解决以下问题：

1. Release asset validation 的 fixture 覆盖不足。
2. Workflow evidence 逻辑主要依赖真实 GitHub API，缺少离线测试。
3. `checksums.txt` / `checksums.json` / `release-manifest.json` 的交叉校验还可以更严格。
4. Release workflow 中管道命令需要显式 `pipefail`，避免校验失败被隐藏。
5. Coverage baseline 读取逻辑仍依赖 `awk`，长期维护风险较高。
6. Docs / Skills / README / CLI command facts 需要继续防漂移。
7. Dev / Preview / Deploy 的边界测试仍可继续补强。
8. 1.0.x 维护线需要一套明确的 post-release audit checklist。

---

## 3. 不纳入 1.0.4 的内容

以下内容不得进入 1.0.4：

```text
bukit clone
bukit import
theme wizard
theme registry
plugin marketplace
external runtime plugin loading
webhook automation
true HMR
remote theme source
Notion push / migration
BukitJalil GUI
AI 自动生成 skills
```

这些内容应进入：

```text
Labs
1.1.0+
独立设计文档
独立插件化开发计划
```

1.0.4 只做 Core 1.0.x 的稳定性维护。

---

## 4. P0 必须修复项

### P0-1：Release asset validation 增加 fixture 测试

#### 背景

当前 release workflow 已经生成并校验：

```text
checksums.txt
checksums.json
release-manifest.json
release-assets-check.md
```

但 `release-assets.sh` 的主要验证逻辑仍缺少系统化 fixture。1.0.4 必须避免脚本错误只能在真实 release workflow 中暴露。

#### 修复内容

新增目录：

```text
tests/fixtures/release-assets/
```

建议结构：

```text
tests/fixtures/release-assets/
  valid/
  missing-manifest/
  missing-checksums-txt/
  missing-checksums-json/
  wrong-version/
  wrong-commit/
  missing-platform-artifact/
  extra-platform-artifact/
  duplicate-manifest-artifact/
  manifest-unsorted/
  checksum-mismatch/
  size-mismatch/
  missing-rid/
  unexpected-rid/
  invalid-sha-prefix/
```

新增测试脚本：

```text
scripts/checks/test-release-assets-fixtures.sh
```

脚本行为：

1. `valid/` fixture 必须通过。
2. 每个 invalid fixture 必须失败。
3. 每个失败用例必须匹配预期错误关键词。
4. 测试结果写入：

```text
TestResults/release-assets-fixtures/release-assets-fixtures.md
```

#### 验收命令

```bash
bash scripts/checks/test-release-assets-fixtures.sh
```

#### 验收标准

输出必须包含：

```text
valid fixture: passed
missing-manifest: failed as expected
wrong-version: failed as expected
wrong-commit: failed as expected
duplicate-manifest-artifact: failed as expected
release asset fixture tests OK
```

---

### P0-2：Release asset pipeline 显式启用 pipefail

#### 背景

Release workflow 当前通过管道将 `release-assets.sh` 输出写入 `tee`。为了确保校验失败不会被管道掩盖，必须显式启用 `set -euo pipefail`。

#### 修复内容

在 Release workflow 的 `Validate release assets` step 中，将脚本改为：

```bash
set -euo pipefail

mkdir -p TestResults/release-assets
mkdir -p release-assets

cp -f "bukit-linux-x64/bukit-${VERSION}-linux-x64.tar.gz" release-assets/
cp -f "bukit-osx-arm64/bukit-${VERSION}-osx-arm64.tar.gz" release-assets/
cp -f "bukit-win-x64/bukit-${VERSION}-win-x64.zip" release-assets/
cp -f bukit-skills.zip release-assets/
cp -f checksums.txt checksums.json release-manifest.json release-assets/

status=0
bash scripts/checks/release-assets.sh release-assets "$VERSION" "$GITHUB_SHA" \
  | tee TestResults/release-assets/release-assets-check.md || status=$?

if [ "$status" -ne 0 ]; then
  echo "Strict set check: failed"
  exit "$status"
fi

echo "Strict set check: passed"
```

#### 验收标准

人为破坏一个 checksum 后，Release asset validation step 必须失败。

---

### P0-3：重新计算并校验 bundleHash

#### 背景

`checksums.json` 和 `release-manifest.json` 中已有 `bundleHash`。1.0.4 需要确保校验脚本不是只比较两者相等，而是重新按规则计算 bundleHash。

#### 修复内容

在 `scripts/checks/release-assets.sh` 的 Python 校验逻辑中加入：

```python
def recompute_bundle_hash(files):
    normalized = sorted(files, key=lambda item: item["path"])
    hasher = hashlib.sha256()

    for item in normalized:
        line = f"{item['path']}|{item['hash']}|{item['size']}\n"
        hasher.update(line.encode("utf-8"))

    return f"sha256:{hasher.hexdigest()}"
```

校验：

```python
actual_bundle_hash = recompute_bundle_hash(files)
if actual_bundle_hash != checksums_bundle_hash:
    raise SystemExit("ERROR: checksums.json bundleHash mismatch")
```

#### 验收标准

如果手动修改 `checksums.json.bundleHash`，校验必须失败。

---

### P0-4：强校验 checksums.txt header

#### 背景

`checksums.txt` 当前包含：

```text
# schema=...
# version=...
# commit=...
# artifacts=...
```

1.0.4 需要强校验 `commit` 和 `artifacts`，防止 manifest、checksums、实际文件三者不同步。

#### 修复内容

在 `release-assets.sh` 中加入：

```python
if expected_commit and headers["commit"] != expected_commit:
    raise SystemExit(
        f"ERROR: checksums.txt commit {headers['commit']} != expected {expected_commit}"
    )

try:
    artifact_count = int(headers["artifacts"])
except ValueError:
    raise SystemExit("ERROR: checksums.txt artifacts header must be integer")

if artifact_count != len(required_file_set):
    raise SystemExit("ERROR: checksums.txt artifacts count mismatch")
```

#### 验收标准

以下情况必须失败：

```text
checksums.txt commit != expected commit
checksums.txt artifacts != required artifact count
checksums.txt artifacts is not integer
```

---

### P0-5：Workflow evidence 离线 evaluator

#### 背景

当前 workflow evidence 脚本依赖 GitHub API。1.0.4 需要将 “查询 GitHub API” 与 “判断 workflow evidence 是否通过” 拆开。

#### 新增文件

```text
scripts/checks/ci-workflow-evidence-evaluate.py
```

输入参数：

```text
<input-json>
<output-json>
<repo>
<sha>
<workflow>
<require-success>
<query-url>
<report-md>
<required-branches>
```

原 `ci-workflow-evidence.sh` 只负责：

1. curl GitHub API；
2. 把返回 JSON 交给 evaluator；
3. 返回 evaluator 的 exit code。

#### 新增 fixture

```text
tests/fixtures/workflow-evidence/
  success-main.json
  success-master.json
  success-feature-only.json
  failed-main.json
  cancelled-main.json
  no-runs.json
  multiple-runs-latest-failed-older-success.json
```

#### 验收命令

```bash
bash scripts/checks/test-workflow-evidence-fixtures.sh
```

#### 验收标准

必须覆盖：

```text
main success -> pass
master success -> pass
feature-only success with required main/master -> fail
failed main -> fail
cancelled main -> fail
no runs -> fail
```

对于 `latest failed but older success` 需要明确策略：

```text
只要该 commit 在 required branch 上存在 completed success run，即通过。
```

---

## 5. P1 强烈建议修复项

### P1-1：Coverage baseline 读取逻辑 Python 化

#### 背景

当前 coverage baseline 读取逻辑通过 shell/awk 解析 JSON。该方式适合短期，但长期维护不稳。

#### 修复内容

新增：

```text
scripts/checks/read-coverage-baseline.py
```

用法：

```bash
python3 scripts/checks/read-coverage-baseline.py docs/coverage-baselines.json core minimum
python3 scripts/checks/read-coverage-baseline.py docs/coverage-baselines.json importing blocking
```

输出纯文本值。

修改 `scripts/checks/coverage.sh`，用 Python 脚本读取：

```bash
coverage_baseline_value() {
  python3 scripts/checks/read-coverage-baseline.py "$coverage_baseline_file" "$1" "$2"
}
```

#### 验收标准

```bash
bash scripts/checks/coverage.sh Release
```

仍能输出：

```text
Coverage core: xx% (>= 80%)
Coverage cli: xx% (>= 75%)
Coverage importing: xx% (baseline 39.70%)
Coverage labs: xx% (baseline 62.96%)
```

---

### P1-2：Coverage baseline schema 校验

#### 新增文件

```text
docs/schemas/coverage-baselines.v1.json
```

要求字段：

```json
{
  "core": {
    "blocking": true,
    "minimum": 80
  },
  "cli": {
    "blocking": true,
    "minimum": 75
  },
  "importing": {
    "blocking": false,
    "baseline": 39.70
  },
  "labs": {
    "blocking": false,
    "baseline": 62.96
  }
}
```

新增脚本：

```text
scripts/checks/coverage-baseline-schema.sh
```

#### 验收标准

```bash
bash scripts/checks/coverage-baseline-schema.sh
```

必须通过。

错误情况：

```text
core.blocking=false -> fail
cli.blocking=false -> fail
importing.blocking=true without explicit decision -> fail
labs.blocking=true without explicit decision -> fail
missing minimum for core -> fail
missing minimum for cli -> fail
```

---

### P1-3：Core 用户可见文本防回退

#### 背景

当前已经有 Core CLI command whitelist 和 docs consistency。但用户可见文本仍可能在新增文档、错误提示、help text 中回退。

#### 修复内容

新增 Architecture Test：

```text
CoreUserFacingText_DoesNotLeakNonCoreCommands
CoreUserFacingText_UsesLiveReloadNotHmr
```

扫描范围：

```text
src/Bukit.Cli/**
src/Bukit.Config/**
src/Bukit.Engine/**
README*.md
guide/user/**
guide/dev/**
guide/skills/**
```

禁止：

```text
HMR
Hot Module Replacement
bukit clone
bukit import
bukit theme
bukit plugin
bukit webhook
bukit intent
bukit data
bukit visual
bukit theme manifest
--allow-external-plugins
```

允许：

```text
guide/labs/**
guide/archive/**
tests/fixtures/**
scripts/checks/core-cli-contract.sh
```

#### 验收标准

```bash
dotnet test tests/Bukit.Architecture.Tests -c Release
```

必须通过。

---

### P1-4：Release artifact smoke 扩展

#### 修复内容

增强：

```text
scripts/smoke/release-artifacts.sh
```

新增检查：

```bash
"$binary" dev --help | grep -q "LiveReload"
! "$binary" dev --help | grep -q "HMR"

"$binary" --help > "$tmp/help.txt"
! grep -E "clone|import|webhook|theme wizard|plugin marketplace" "$tmp/help.txt"

"$binary" deploy --help | grep -q "github-pages"
```

如有稳定 fixture，也加入：

```bash
"$binary" deploy --dry-run --skip-build --config "$fixture/site.yaml"
```

#### 验收标准

release artifact smoke 能覆盖：

```text
version
help
config schema
config check
doctor
build
seo audit
geo audit
publish audit
dev LiveReload wording
non-Core command absence
deploy help / dry-run
```

---

### P1-5：Deploy 安全与失败路径测试

#### 背景

GitHub Pages deploy provider 涉及 git、token、askpass、临时目录、远程分支、push。需要继续增强失败路径测试。

#### 新增测试

```text
Deploy_ErrorMessage_DoesNotContainGitHubToken
Deploy_AskpassFile_IsDeletedAfterFailure
Deploy_TempDir_IsDeletedAfterFailure
Deploy_OutputDirMissing_ReturnsFriendlyError
Deploy_OutputDirEmpty_ReturnsFriendlyError
Deploy_NonFastForwardRequiresForce
Deploy_GitHubRemoteUrlParser_SupportsHttpsAndSsh
Deploy_GitHubRemoteUrlParser_RejectsNonGitHubRemote
```

#### 验收标准

```bash
dotnet test tests/Bukit.Cli.Tests -c Release --filter Deploy
```

必须通过。

---

## 6. P2 质量增强项

### P2-1：Preview / Dev path fuzz tests

新增测试：

```text
Preview_RejectsEncodedDotDotPath
Preview_RejectsBackslashTraversal
Preview_RejectsDoubleEncodedTraversal
Preview_HandlesVeryLongPath
Preview_HandlesUnicodePathNormalization
Dev_LiveReloadInjection_DoesNotBreakNonHtmlAssets
```

测试路径：

```text
/%2e%2e/
/..%2f
/assets/%2e%2e/secret
/foo/../bar
/%255c../secret
```

---

### P2-2：Docs / Skills / CLI facts 单一事实源

#### 目标

让以下内容自动比对：

```text
BukitCliSpecs.cs
README.md
README.zh-CN.md
README.ms.md
guide/user CLI docs
guide/dev CLI docs
guide/skills/bukit-cli-reference
```

#### 新增脚本

```text
scripts/checks/cli-docs-sync.sh
```

#### 验收标准

任何文档多写、少写、错写 Core 命令都失败。

---

### P2-3：Release checklist 文档化

新增：

```text
guide/dev/release-checklist.md
```

必须包含：

```text
1. Merge to main
2. Wait for ci.yml completed success
3. Confirm workflow evidence
4. Confirm coverage summary
5. Create tag
6. Release workflow
7. Confirm release-assets-check.md
8. Confirm checksums.txt
9. Confirm release-manifest.json
10. Download artifact and run smoke locally
```

---

## 7. 1.0.4 任务拆分建议

### Task 1：Release asset fixture test

目标：

```text
为 release-assets.sh 和 prepare-release-assets.sh 建立完整 fixture 测试。
```

验收：

```bash
bash scripts/checks/test-release-assets-fixtures.sh
```

---

### Task 2：Workflow evidence evaluator extraction

目标：

```text
拆分 GitHub API 查询与 workflow evidence 判断逻辑，加入离线 fixture 测试。
```

验收：

```bash
bash scripts/checks/test-workflow-evidence-fixtures.sh
```

---

### Task 3：Bundle hash / checksums header 强校验

目标：

```text
release-assets.sh 重新计算 bundleHash，并校验 checksums.txt commit/artifacts。
```

验收：

```bash
bash scripts/checks/release-assets.sh tests/fixtures/release-assets/valid 1.0.4 <sha>
```

---

### Task 4：Coverage baseline Python 化

目标：

```text
用 Python 替代 awk 解析 coverage-baselines.json。
```

验收：

```bash
bash scripts/checks/coverage.sh Release
```

---

### Task 5：Core text drift guard

目标：

```text
防止 HMR、旧命令、实验命令出现在 Core 用户可见文本中。
```

验收：

```bash
dotnet test tests/Bukit.Architecture.Tests -c Release
```

---

### Task 6：Deploy failure-path tests

目标：

```text
补充 deploy token、temp cleanup、remote URL、non-fast-forward 失败路径测试。
```

验收：

```bash
dotnet test tests/Bukit.Cli.Tests -c Release --filter Deploy
```

---

### Task 7：Release smoke 扩展

目标：

```text
release artifact smoke 覆盖 dev LiveReload、non-Core command absence、deploy help/dry-run。
```

验收：

```bash
bash scripts/smoke/release-artifacts.sh <publish-dir>
```

---

## 8. 1.0.4 发布门禁

1.0.4 发布前必须通过：

```bash
bash scripts/gates/ci-fast.sh Release
CORE_COVERAGE_THRESHOLD=80 CLI_COVERAGE_THRESHOLD=75 bash scripts/gates/ci-full.sh Release
RELEASE_GATE_RIDS=linux-x64 bash scripts/gates/release.sh Release
```

新增必须通过：

```bash
bash scripts/checks/test-release-assets-fixtures.sh
bash scripts/checks/test-workflow-evidence-fixtures.sh
bash scripts/checks/coverage-baseline-schema.sh
bash scripts/checks/cli-docs-sync.sh
```

GitHub Actions 必须产出：

```text
ci-workflow-evidence.json
ci-workflow-evidence.md
rc-gate-evidence.md
coverage-summary.txt
site.schema.json
release-assets-check.md
checksums.txt
checksums.json
release-manifest.json
```

---

## 9. 1.0.4 Release Notes 草案

```text
Bukit Core 1.0.4 is a release and CI hardening patch for the 1.0 maintenance line.

Highlights:
- Added release asset validation fixtures.
- Added offline workflow evidence evaluator fixtures.
- Strengthened bundleHash validation.
- Strengthened checksums.txt commit and artifact count validation.
- Made release asset validation pipeline failure propagation explicit.
- Replaced fragile JSON baseline parsing with a dedicated parser.
- Added Core user-facing text drift guards.
- Expanded release artifact smoke checks.
- Added deploy failure-path regression coverage.
```

---

## 10. 最终判断

1.0.4 的目标不是继续增加功能，而是把 1.0.x 的质量门禁做到：

```text
可离线验证
可 fixture 回归
可跨平台复现
可发布审计
可长期维护
```

完成 1.0.4 后，Bukit Core 1.0.x 维护线将更加稳固，也更适合作为 1.1.0 插件化开发的基础。
