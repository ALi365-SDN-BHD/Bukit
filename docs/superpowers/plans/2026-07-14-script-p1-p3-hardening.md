# Script P1-P3 Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 逐项关闭脚本全量审计的 F1-F11，使安全、发布、打包、扫描和辅助脚本只能在获得直接证据时返回成功。

**Architecture:** 保留现有公开 Shell 入口，把 TRX、发布资产、归档和文件树等结构化验证放入单一职责 Python 辅助脚本。每项修复先用注入式自测证明旧行为为红，再实现最小严格契约，运行归属门禁，并在进入下一项前完成高风险只读审计。

**Tech Stack:** Bash 3 兼容脚本、Python 3 标准库、.NET 10/VSTest TRX、YamlDotNet/xUnit、GitHub Actions YAML、Native AOT、tar/ZIP。

## Global Constraints

- 只实现已批准规格中的 F1-F11；不得扩展到无关重构。
- 不修改或执行 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/`。
- 不修改 `src/Bukit-Core/` 运行时代码。
- 公开入口路径保持不变；新增辅助脚本必须单一职责。
- 活跃 `.sh`、`.py` 及已审计 `.trae` 辅助脚本均不得超过 200 行。
- 用法或畸形输入返回 2；契约违规返回 1；可诊断的工具错误保留其非零状态。
- 证据生产和验证命令周围不得使用 `|| true`；只允许在保存主状态后忽略尽力清理错误。
- 每个子任务先观察 RED，再实现 GREEN，然后运行 `bash scripts/checks/post-change-targeted.sh -- <该子任务路径>`。
- CI、release、gate、安全进程控制属于高风险；每项 GREEN 后立即执行有界只读审计。
- 未经用户另行明确授权，不运行 `ci-full`、`scripts/gates/release.sh`、`test-all`、`smoke-all` 或整个 solution 的 `.slnx` 测试。
- 执行阶段开始时先用 `superpowers:using-git-worktrees` 创建隔离工作树；不得直接在当前 `main` 实施。
- 进入隔离工作树后立即执行 `git branch codex/script-p1-p3-hardening-base HEAD`；如果该引用已存在则先核对它恰好指向当前 HEAD，否则停止。最终范围审计固定使用该引用，不依赖跨命令环境变量。

---

## File Structure

### Security

- Create `scripts/security/verify-trx.py`: 验证 TRX counters 和每个安全选择器的执行证据。
- Create `scripts/security/security-regression-self-test.sh`: 用假的 `dotnet` 覆盖有效、零测试、缺选择器、缺 TRX、失败结果。
- Modify `scripts/security/security-regression.sh`: 生成唯一 TRX 并调用验证器。

### Release assets and workflow

- Create `scripts/release/release-assets.py`: `prepare`/`verify` 的唯一资产契约实现。
- Create `scripts/release/release-assets-self-test.sh`: 覆盖重复、额外、陈旧、符号链接和精确 RID 集合。
- Modify `scripts/release/prepare-release-assets.sh`: 参数校验后委托 Python `prepare`。
- Modify `scripts/release/verify-release-assets.sh`: 参数校验后委托 Python `verify`。
- Create `tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs`: 结构化验证 RID 选择和上传前冒烟。
- Modify `.github/workflows/release.yaml`: 始终传入选定 RID，并在上传前冒烟最终归档。

### Native AOT and reproducibility

- Modify `scripts/build/package-native-aot.sh`: 干净发布目录、安全 PowerShell 数据边界、确定性属性和双输出。
- Modify `scripts/build/native-aot.sh`: 真实兼容入口。
- Modify `scripts/build/build-repro.sh`: 两次隔离 Native AOT 打包并比较发布树。
- Create `scripts/build/compare-publish-trees.py`: 精确比较相对路径、类型、大小和 SHA-256。
- Create `scripts/build/native-aot-self-test.sh`: 注入假 dotnet/PowerShell 验证清理、路径和委托。
- Create `scripts/build/build-repro-self-test.sh`: 验证相同和漂移构建分类。

### Release smoke

- Create `scripts/smoke/extract-release-artifact.py`: 安全解压 `.tar.gz`/`.zip`。
- Modify `scripts/smoke/release-artifacts.sh`: 运行归档内 CLI 的真实 Core 冒烟。
- Create `scripts/smoke/release-artifacts-self-test.sh`: 覆盖空归档、穿越、缺失/重复二进制和成功命令链。
- Modify `scripts/smoke/core.sh`: 修正示例夹具路径。

### Active checks

- Modify `scripts/checks/active-workflow-boundary.sh`: 区分 grep 无匹配和工具失败。
- Modify `guide/skills/scripts/validate-skills-strict.sh`: 移除 rg 并区分 grep 状态。
- Modify `scripts/checks/ci-fast-portability-self-test.sh`: 覆盖两个遗漏扫描器和 Core CLI 搜索参数。
- Modify `scripts/checks/core-cli-contract.sh`: 只扫描 `scripts/` 和 `.github/workflows/`。
- Modify `scripts/checks/docs/size-policy.sh`: 纳入 Shell、Python 和已审计辅助脚本。
- Create `scripts/checks/docs/size-policy-self-test.sh`: 注入超限 Python 文件。
- Modify `scripts/checks/docs-consistency.sh`: 运行尺寸策略自测。

### Auxiliary helpers

- Create `.trae/skills/brainstorming/scripts/session-state.sh`: 会话路径、状态和进程身份验证函数。
- Modify `.trae/skills/brainstorming/scripts/start-server.sh`: 严格参数和可验证状态。
- Modify `.trae/skills/brainstorming/scripts/stop-server.sh`: 验证后才发信号或删除。
- Create `scripts/checks/brainstorm-server-self-test.sh`: 使用假 Node 进程验证生命周期。
- Modify `.trae/skills/systematic-debugging/find-polluter.sh`: NUL 安全枚举和失败分类。
- Create `scripts/checks/find-polluter-self-test.sh`: 路径和退出状态回归。
- Modify `scripts/gates/ci-fast.sh`: 接入两个快速辅助自测。

### Documentation

- Modify `guide/dev/testing.md`: 记录安全、自测和最终归档冒烟入口。
- Modify `guide/dev/release.md`: 记录 Native AOT、可重复构建、资产准备/验证顺序。
- Modify `docs/bukit-1.0-contract-matrix.zh-CN.md`: 用完整命令替换 `build-repro.sh` 占位引用。

---

### Task 1: F1 Security TRX Evidence

**Files:**
- Create: `scripts/security/verify-trx.py`
- Create: `scripts/security/security-regression-self-test.sh`
- Modify: `scripts/security/security-regression.sh`

**Interfaces:**
- Consumes: `verify-trx.py <trx-path> <FullyQualifiedName~selector>...`。
- Produces: 仅当 counters 全通过且每个选择器至少命中一个已执行结果时退出 0。

- [ ] **Step 1: 写入口失败自测**

在 `security-regression-self-test.sh` 中创建临时 `bin/dotnet`。假命令解析
`--filter`、`--logger`、`--results-directory`，并依据 `FAKE_TRX_MODE` 写包含
`ResultSummary/Counters`、`UnitTestResult`、`UnitTest/TestMethod` 的 TRX。

```bash
FAKE_TRX_MODE=valid PATH="$scratch/bin:$PATH" \
  BUKIT_SECURITY_SKIP_RESTORE=1 bash "$script" Release
for mode in zero missing-selector missing failed; do
  if FAKE_TRX_MODE="$mode" PATH="$scratch/bin:$PATH" \
    BUKIT_SECURITY_SKIP_RESTORE=1 bash "$script" Release >"$output" 2>&1; then
    fail "$mode unexpectedly passed"
  fi
done
```

`zero` 写全零 counters；`missing-selector` 省略过滤器中的首个 selector；`missing`
不写文件；`failed` 写 `failed=1` 和 `outcome="Failed"`；`valid` 为每个 selector 写
一个 Passed result。

- [ ] **Step 2: 运行并确认 RED**

Run: `bash scripts/security/security-regression-self-test.sh`

Expected: FAIL with `zero unexpectedly passed`，证明旧入口没有 TRX 证据门。

- [ ] **Step 3: 实现 TRX 验证器**

`verify-trx.py` 使用以下完整核心函数；CLI 捕获 `ET.ParseError`、`OSError`、
`ValueError`，打印 `security TRX validation failed:` 并返回 1，参数不足返回 2。

```python
def verify_trx(path: Path, selectors: list[str]) -> None:
    root = ET.parse(path).getroot()
    counters = root.find(".//{*}ResultSummary/{*}Counters")
    if counters is None:
        raise ValueError("TRX counters are missing")
    names = ("total", "executed", "passed", "failed", "notExecuted")
    values = {name: int(counters.attrib.get(name, "0")) for name in names}
    if values["total"] <= 0:
        raise ValueError("TRX contains zero tests")
    if not (values["executed"] == values["passed"] == values["total"]
            and values["failed"] == values["notExecuted"] == 0):
        raise ValueError(f"TRX tests were not all executed and passed: {values}")
    methods: dict[str, str] = {}
    for unit in root.findall(".//{*}UnitTest"):
        method = unit.find("./{*}TestMethod")
        if method is not None:
            methods[unit.attrib["id"]] = (
                f'{method.attrib.get("className", "")}.{method.attrib.get("name", "")}'
            )
    executed = [
        methods.get(result.attrib.get("testId", ""), result.attrib.get("testName", ""))
        for result in root.findall(".//{*}UnitTestResult")
        if result.attrib.get("outcome") == "Passed"
    ]
    missing = [
        selector for selector in selectors
        if selector.removeprefix("FullyQualifiedName~") not in "\n".join(executed)
    ]
    if missing:
        raise ValueError(f"security selectors have no executed result: {missing}")
```

- [ ] **Step 4: 让安全入口生成并验证唯一 TRX**

每条 `project|selector...` 拆成数组；同一数组同时构造 filter 和验证参数：

```bash
results="$(mktemp -d "${TMPDIR:-/tmp}/bukit-security-results.XXXXXX")"
trap 'rm -rf "$results"' EXIT
IFS='|' read -r -a fields <<< "$entry"
project="${fields[0]}"; selectors=("${fields[@]:1}")
filter="$(IFS='|'; printf '%s' "${selectors[*]}")"
name="$(basename "$(dirname "$project")")"
trx="$results/$name.trx"
args=(test "$project" -c "$configuration" --filter "$filter"
  --logger "trx;LogFileName=$name.trx" --results-directory "$results")
[[ "${BUKIT_SECURITY_SKIP_RESTORE:-0}" != 1 ]] || args+=(--no-restore)
run_step "$name security" dotnet "${args[@]}"
[[ -f "$trx" ]] || { echo "missing security TRX: $trx" >&2; exit 1; }
python3 scripts/security/verify-trx.py "$trx" "${selectors[@]}"
```

- [ ] **Step 5: 验证 GREEN 和真实证据**

Run:

```bash
bash scripts/security/security-regression-self-test.sh
python3 -c 'import sys; from pathlib import Path; p=Path(sys.argv[1]); compile(p.read_text(), str(p), "exec")' scripts/security/verify-trx.py
bash -n scripts/security/security-regression.sh scripts/security/security-regression-self-test.sh
bash scripts/security/security-regression.sh Release
bash scripts/checks/post-change-targeted.sh -- \
  scripts/security/verify-trx.py scripts/security/security-regression.sh \
  scripts/security/security-regression-self-test.sh
```

Expected: 自测打印 `security regression self-test OK`；真实五个项目均打印非零 TRX
验证成功；定向门禁通过。

- [ ] **Step 6: 即时审计并提交**

审计 selector 单一事实源、零测试、跳过、缺文件和清理路径；确认无备份变更。

```bash
git diff --check
git diff -- scripts/security
git add scripts/security/verify-trx.py scripts/security/security-regression.sh \
  scripts/security/security-regression-self-test.sh
git commit -m "fix(security): require executed TRX evidence"
```

### Task 2: F2 Exact Release Assets and RID Wiring

**Files:**
- Create: `scripts/release/release-assets.py`
- Create: `scripts/release/release-assets-self-test.sh`
- Modify: `scripts/release/prepare-release-assets.sh`
- Modify: `scripts/release/verify-release-assets.sh`
- Create: `tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs`
- Modify: `.github/workflows/release.yaml`

**Interfaces:**
- Consumes: `release-assets.py prepare VERSION COMMIT OUTPUT ARCHIVE...` 或
  `release-assets.py verify VERSION COMMIT DIR [RID...]`。
- Produces: assets、`checksums.txt`、`checksums.json`、`release-manifest.json` 的精确双射。

- [ ] **Step 1: 写重复、额外和陈旧资产 RED 自测**

创建合法 `bukit-1.2.3-linux-x64.tar.gz`，并断言：

```bash
if bash "$prepare" 1.2.3 abc "$out" "$archive" "$archive"; then
  fail "duplicate archive unexpectedly passed"
fi
bash "$prepare" 1.2.3 abc "$out" "$archive"
printf '%064d  extra.tar.gz\n' 0 >> "$out/checksums.txt"
if bash "$verify" 1.2.3 abc "$out" linux-x64; then
  fail "extra checksum unexpectedly passed"
fi
printf 'stale\n' > "$out/stale-debug.zip"
if bash "$verify" 1.2.3 abc "$out" linux-x64; then
  fail "stale disk asset unexpectedly passed"
fi
```

再覆盖重复 basename、保留元数据名、输入符号链接、重复 RID、错误扩展名和正向三 RID。

- [ ] **Step 2: 运行并确认 RED**

Run: `bash scripts/release/release-assets-self-test.sh`

Expected: FAIL with `duplicate archive unexpectedly passed`。

- [ ] **Step 3: 实现单一 Python 资产契约**

固定常量和接口：

```python
RID_SUFFIX = {"linux-x64": ".tar.gz", "osx-arm64": ".tar.gz", "win-x64": ".zip"}
METADATA = {"checksums.txt", "checksums.json", "release-manifest.json"}

def expected_name(version: str, rid: str) -> str:
    if rid not in RID_SUFFIX:
        raise ContractError(f"unsupported release RID: {rid}")
    return f"bukit-{version}-{rid}{RID_SUFFIX[rid]}"

def asset_record(path: Path) -> dict[str, object]:
    return {"name": path.name,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "bytes": path.stat().st_size}
```

`prepare()` 必须依次：校验 version/commit；拒绝缺失、非普通、符号链接、重复路径、
重复 basename、保留名和错误 RID 文件名；通过 `resolve_output()` 拒绝根目录、仓库根、
`.`/`..`、输出符号链接和解析后改变的父目录；在同级 `tempfile.mkdtemp()` 复制资产并
生成三份元数据；调用 `verify()` 自证；删除经过验证的旧输出目录；`os.replace()` 暂存。

`verify()` 必须构造五个集合并要求完全相等：expected RID names、disk regular files、
manifest names、checksums JSON names、checksums text names。JSON root 和资产对象只允许
精确键；checksum 行必须完整匹配 `([0-9a-f]{64})  ([^/\\\x00-\x1f]+)`；逐文件复算
SHA-256 和 bytes。每次差异打印排序后的 `missing=`、`extra=`。

- [ ] **Step 4: 把 Shell 入口收敛为严格委托**

```bash
[[ -n "$version" && -n "$commit" && -n "$output_dir" && $# -gt 3 ]] || {
  echo "usage: bash scripts/release/prepare-release-assets.sh <version> <commit> <output-dir> <archive>..." >&2
  exit 2
}
shift 3
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
exec python3 "$script_dir/release-assets.py" prepare \
  "$version" "$commit" "$output_dir" "$@"
```

verify wrapper 在 `shift 3` 后执行 `verify` 子命令。Python CLI 对用法错误返回 2，
对 `ContractError` 返回 1。

- [ ] **Step 5: 写结构化 RID 工作流测试并确认 RED**

创建 `ReleaseWorkflowContractTests.cs`，用 `YamlStream` 解析 release YAML；定义
`Job(name)`、`Steps(job)`、`RunOfStep(job, name)`，并加入：

```csharp
[Fact]
public void CollectAssets_VerifiesTheSelectedRidSet()
{
    var run = RunOfStep(Job("collect-assets"), "Verify assets");
    Assert.Contains("case \"$RIDS\" in", run, StringComparison.Ordinal);
    Assert.Contains("linux-x64) expected_rids=(linux-x64)", run, StringComparison.Ordinal);
    Assert.Contains("osx-arm64) expected_rids=(osx-arm64)", run, StringComparison.Ordinal);
    Assert.Contains("win-x64) expected_rids=(win-x64)", run, StringComparison.Ordinal);
    Assert.Contains("all) expected_rids=(linux-x64 osx-arm64 win-x64)", run, StringComparison.Ordinal);
    Assert.Contains("verify-release-assets.sh", run, StringComparison.Ordinal);
}
```

Run: `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --filter FullyQualifiedName~ReleaseWorkflowContractTests`

Expected: FAIL，因为当前只在 `PUBLISH=true` 时传三个 RID。

- [ ] **Step 6: 修复工作流 RID 映射并验证 GREEN**

Verify assets step 增加 `RIDS: ${{ inputs.rids }}`，run 使用：

```bash
case "$RIDS" in
  linux-x64) expected_rids=(linux-x64) ;;
  osx-arm64) expected_rids=(osx-arm64) ;;
  win-x64) expected_rids=(win-x64) ;;
  all) expected_rids=(linux-x64 osx-arm64 win-x64) ;;
  *) echo "unsupported RID selection: $RIDS" >&2; exit 2 ;;
esac
bash scripts/release/verify-release-assets.sh \
  "$VERSION" "$GITHUB_SHA" release-assets "${expected_rids[@]}"
```

Run:

```bash
bash scripts/release/release-assets-self-test.sh
python3 -c 'import sys; from pathlib import Path; p=Path(sys.argv[1]); compile(p.read_text(), str(p), "exec")' scripts/release/release-assets.py
bash -n scripts/release/prepare-release-assets.sh scripts/release/verify-release-assets.sh \
  scripts/release/release-assets-self-test.sh
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --filter FullyQualifiedName~ReleaseWorkflowContractTests
bash scripts/checks/post-change-targeted.sh -- scripts/release/release-assets.py \
  scripts/release/prepare-release-assets.sh scripts/release/verify-release-assets.sh \
  scripts/release/release-assets-self-test.sh \
  tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs \
  .github/workflows/release.yaml
```

Expected: 自测、Architecture 定向测试和 post-change gate 全通过。

- [ ] **Step 7: 即时审计并提交**

审计 staging 删除边界、所有集合唯一性、每种 workflow 输入的精确 RID；确认上传通配符
无法包含未列出文件。

```bash
git diff --check
git diff -- scripts/release .github/workflows/release.yaml \
  tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs
git add scripts/release tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs \
  .github/workflows/release.yaml
git commit -m "fix(release): enforce exact asset sets"
```

### Task 3: F6/F8 Native AOT Hygiene and Reproducibility

**Files:**
- Modify: `scripts/build/package-native-aot.sh`
- Modify: `scripts/build/native-aot.sh`
- Modify: `scripts/build/build-repro.sh`
- Create: `scripts/build/compare-publish-trees.py`
- Create: `scripts/build/native-aot-self-test.sh`
- Create: `scripts/build/build-repro-self-test.sh`

**Interfaces:**
- Produces: `native-aot.sh VERSION RID OUTPUT [CONFIG]` 返回真实 archive 路径。
- Produces: `build-repro.sh VERSION RID [CONFIG]` 仅在两棵发布树完全一致时成功。
- Consumes: `compare-publish-trees.py LEFT RIGHT`。

- [ ] **Step 1: 写打包和兼容入口 RED 自测**

假 `dotnet` 解析 `-o` 并写 `bukit`；运行前在 publish dir 写 `stale.txt`，运行后断言
它消失。假 `pwsh` 断言 PowerShell 源码不含输出路径，并通过环境变量创建归档：

```bash
case "$*" in
  *"$BUKIT_EXPECTED_ARCHIVE"*) exit 91 ;;
esac
printf 'zip\n' > "$BUKIT_ARCHIVE_PATH"
```

使用含单引号的 `output_root="$scratch/it's-safe"`。另断言 `native-aot.sh` 缺参数失败，
参数完整时委托 package 并产生非空归档。

- [ ] **Step 2: 运行并确认 RED**

Run: `bash scripts/build/native-aot-self-test.sh`

Expected: FAIL，陈旧文件仍在或 PowerShell 路径出现在 `-Command` 文本中。

- [ ] **Step 3: 加固 package-native-aot.sh**

在 RID 白名单后建立受保护发布路径：

```bash
mkdir -p "$output_root"
output_root="$(cd "$output_root" && pwd -P)"
publish_root="$output_root/publish"
[[ ! -L "$publish_root" ]] || { echo "publish root must not be a symlink" >&2; exit 1; }
mkdir -p "$publish_root"
[[ "$(cd "$publish_root" && pwd -P)" == "$output_root/publish" ]] || {
  echo "publish root escaped output root" >&2; exit 1;
}
publish_dir="$publish_root/$rid"
rm -rf -- "$publish_dir"
mkdir -p "$publish_dir"
```

给 `dotnet publish` 增加：

```bash
-p:ContinuousIntegrationBuild=true \
-p:Deterministic=true \
-p:PathMap="$(pwd -P)=/_/src"
```

写归档前 `rm -f -- "$archive"`。PowerShell 使用静态命令文本：

```bash
BUKIT_ARCHIVE_PATH="$archive_for_pwsh" "$pwsh_cmd" -NoProfile -Command \
  '$source=(Get-Location).Path; $dest=$env:BUKIT_ARCHIVE_PATH; [IO.Compression.ZipFile]::CreateFromDirectory($source,$dest)'
```

发布目录和归档都必须非空。`GITHUB_OUTPUT` 写：

```bash
printf 'archive=%s\npublish_dir=%s\n' "$archive" "$publish_dir" >> "$GITHUB_OUTPUT"
```

- [ ] **Step 4: 实现真实 native-aot.sh**

```bash
#!/usr/bin/env bash
set -euo pipefail
[[ $# -ge 3 && $# -le 4 ]] || {
  echo "usage: bash scripts/build/native-aot.sh <version> <rid> <output-root> [configuration]" >&2
  exit 2
}
version="$1"; rid="$2"; output_root="$3"; configuration="${4:-Release}"
printf 'Native AOT package: version=%s rid=%s configuration=%s\n' \
  "$version" "$rid" "$configuration" >&2
exec bash "$(dirname "${BASH_SOURCE[0]}")/package-native-aot.sh" \
  "$version" "$rid" "$output_root" "$configuration"
```

- [ ] **Step 5: 写 publish tree 比较 RED 自测**

假 `dotnet` 使用 `FAKE_BUILD_STATE` 计数；`stable` 两次写相同内容，`drift` 第二次写
不同内容。断言 stable 成功、drift 失败且输出 `changed=['bukit']`。

Run: `bash scripts/build/build-repro-self-test.sh`

Expected: FAIL，因为当前 `build-repro.sh` 仍为空操作。

- [ ] **Step 6: 实现比较器和 build-repro.sh**

`compare-publish-trees.py`：

```python
def manifest(root: Path) -> dict[str, tuple[str, int, str]]:
    result: dict[str, tuple[str, int, str]] = {}
    for path in sorted(root.rglob("*")):
        rel = path.relative_to(root).as_posix()
        if path.is_symlink() or not (path.is_dir() or path.is_file()):
            raise ValueError(f"unsupported publish entry: {rel}")
        if path.is_dir():
            result[rel] = ("dir", 0, "")
        else:
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            result[rel] = ("file", path.stat().st_size, digest)
    return result

def compare(left: Path, right: Path) -> None:
    left_items, right_items = manifest(left), manifest(right)
    missing = sorted(left_items.keys() - right_items.keys())
    extra = sorted(right_items.keys() - left_items.keys())
    changed = sorted(name for name in left_items.keys() & right_items.keys()
                     if left_items[name] != right_items[name])
    if missing or extra or changed:
        raise ValueError(f"missing={missing} extra={extra} changed={changed}")
```

CLI 参数不为 2 返回 2；`ValueError`/`OSError` 打印 `publish trees differ:` 并返回 1。

`build-repro.sh` 严格解析 `VERSION RID [CONFIG]`，创建 Bukit 临时根并固定：

```bash
export GITHUB_SHA="${GITHUB_SHA:-$(git rev-parse HEAD)}"
export SOURCE_DATE_EPOCH="${SOURCE_DATE_EPOCH:-$(git show -s --format=%ct HEAD)}"
for run in first second; do
  bash scripts/build/package-native-aot.sh "$version" "$rid" \
    "$scratch/$run" "$configuration" > "$scratch/$run.archive"
done
python3 scripts/build/compare-publish-trees.py \
  "$scratch/first/publish/$rid" "$scratch/second/publish/$rid"
```

- [ ] **Step 7: 验证 GREEN 和真实主机可重复构建**

Run:

```bash
bash scripts/build/native-aot-self-test.sh
bash scripts/build/build-repro-self-test.sh
python3 -c 'import sys; from pathlib import Path; p=Path(sys.argv[1]); compile(p.read_text(), str(p), "exec")' scripts/build/compare-publish-trees.py
bash -n scripts/build/package-native-aot.sh scripts/build/native-aot.sh \
  scripts/build/build-repro.sh scripts/build/native-aot-self-test.sh \
  scripts/build/build-repro-self-test.sh
rid="$(case "$(uname -s)-$(uname -m)" in Darwin-arm64) echo osx-arm64;; Linux-x86_64) echo linux-x64;; *) exit 2;; esac)"
bash scripts/build/build-repro.sh 0.0.0-ci "$rid" Release
bash scripts/checks/post-change-targeted.sh -- scripts/build/package-native-aot.sh \
  scripts/build/native-aot.sh scripts/build/build-repro.sh \
  scripts/build/compare-publish-trees.py scripts/build/native-aot-self-test.sh \
  scripts/build/build-repro-self-test.sh
```

Expected: 注入自测通过；真实 AOT 两棵发布树完全一致；定向门禁通过。若真实比较失败，
本任务保持未完成，依据 changed path 修正确定性输入后重跑，不得降级为警告。

- [ ] **Step 8: 即时审计并提交**

审计 `rm -rf` 只能命中白名单 RID 的严格派生目录、PowerShell 文本不含调用者路径、
两个构建共享 commit/time/property。

```bash
git diff --check
git diff -- scripts/build
git add scripts/build
git commit -m "fix(build): prove Native AOT reproducibility"
```

### Task 4: F3 Final Archive Smoke and Upload Ordering

**Files:**
- Create: `scripts/smoke/extract-release-artifact.py`
- Modify: `scripts/smoke/release-artifacts.sh`
- Create: `scripts/smoke/release-artifacts-self-test.sh`
- Modify: `scripts/smoke/core.sh`
- Modify: `.github/workflows/release.yaml`
- Modify: `tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs`

**Interfaces:**
- Consumes: `release-artifacts.sh <archive-or-publish-dir> <rid>`。
- Produces: 归档中唯一 CLI 完成 `config check -> build --clean -> publish audit`。

- [ ] **Step 1: 写最终归档 RED 自测**

创建可执行假 `bukit`，记录 `$*`，并分别打成 tar.gz 和 zip。断言成功日志依次含：

```text
config check --config
build --config
publish audit --dir
```

再断言空目录、无二进制、两个 `bukit`、含 `../escape` 成员和 RID/扩展名不匹配失败。

- [ ] **Step 2: 运行并确认 RED**

Run: `bash scripts/smoke/release-artifacts-self-test.sh`

Expected: FAIL，因为当前脚本对空目录返回成功且不接受 RID。

- [ ] **Step 3: 实现安全归档解压器**

定义 `safe_relative(name: str) -> PurePosixPath`，拒绝绝对路径、空路径和 `..`。ZIP
使用 `ZipInfo.external_attr >> 16` 拒绝符号链接；tar 只允许 `isdir()`/`isreg()`。
逐成员复制，不调用 `extractall()`：

```python
def safe_relative(name: str) -> PurePosixPath:
    relative = PurePosixPath(name)
    if relative.is_absolute() or not relative.parts or ".." in relative.parts:
        raise ValueError(f"unsafe archive member: {name}")
    return relative

target = destination.joinpath(*relative.parts)
target.parent.mkdir(parents=True, exist_ok=True)
with archive.open(member) as source, target.open("wb") as output:
    shutil.copyfileobj(source, output)
target.chmod(mode & 0o777)
```

CLI 为 `extract-release-artifact.py ARCHIVE RID DEST`；按 RID 验证 `.zip` 或 `.tar.gz`，
契约错误退出 1，用法错误退出 2。

- [ ] **Step 4: 实现真实 release-artifacts.sh**

严格要求两个参数并校验 RID。归档解压到 scratch；目录直接作为 publish root。用
Bash 3 兼容循环收集唯一 basename：

```bash
matches=()
while IFS= read -r path; do
  matches+=("$path")
done < <(find "$publish_root" -type f -name "$exe" -print)
[[ ${#matches[@]} -eq 1 ]] || { echo "expected exactly one $exe" >&2; exit 1; }
cp -R tests/fixtures/basic-markdown-site "$scratch/site"
BUKIT_BIN="${matches[0]}" BUKIT_SMOKE_ROOT="$scratch/site" \
  BUKIT_SMOKE_OUTPUT="$scratch/output" bash scripts/smoke/core.sh
```

POSIX RID 还要求 `-x`；所有临时内容由 trap 清理。把 `core.sh` 示例改成
`tests/fixtures/basic-markdown-site`。

- [ ] **Step 5: 写工作流顺序测试并确认 RED**

在 `ReleaseWorkflowContractTests` 增加三个 package job 的 theory。通过 step 数组索引
断言 `id: package` < `name: Smoke packaged archive` < `upload-artifact`，并断言 smoke
run 精确包含：

```csharp
var expected = "bash scripts/smoke/release-artifacts.sh \"${{ steps.package.outputs.archive }}\" " + rid;
Assert.Contains(expected, smokeRun, StringComparison.Ordinal);
```

Run: 定向 Architecture 测试。

Expected: FAIL，当前没有 smoke step。

- [ ] **Step 6: 在三个 package job 上传前接入归档冒烟**

每个 job 在 package 后增加：

```yaml
- name: Smoke packaged archive
  run: bash scripts/smoke/release-artifacts.sh "${{ steps.package.outputs.archive }}" linux-x64
```

macOS/Windows 分别使用 `osx-arm64`/`win-x64`，不得传 `publish_dir`。

- [ ] **Step 7: 验证 GREEN**

Run:

```bash
bash scripts/smoke/release-artifacts-self-test.sh
python3 -c 'import sys; from pathlib import Path; p=Path(sys.argv[1]); compile(p.read_text(), str(p), "exec")' scripts/smoke/extract-release-artifact.py
bash -n scripts/smoke/core.sh scripts/smoke/release-artifacts.sh \
  scripts/smoke/release-artifacts-self-test.sh
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --filter FullyQualifiedName~ReleaseWorkflowContractTests
bash scripts/checks/post-change-targeted.sh -- scripts/smoke/core.sh \
  scripts/smoke/release-artifacts.sh scripts/smoke/extract-release-artifact.py \
  scripts/smoke/release-artifacts-self-test.sh .github/workflows/release.yaml \
  tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs
```

Expected: 归档自测、结构化工作流测试和定向门禁通过。

- [ ] **Step 8: 即时审计并提交**

审计归档路径规范化、特殊成员拒绝、唯一二进制和 workflow step 顺序。

```bash
git diff --check
git diff -- scripts/smoke .github/workflows/release.yaml \
  tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs
git add scripts/smoke .github/workflows/release.yaml \
  tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs
git commit -m "fix(release): smoke final packaged archives"
```

### Task 5: F4/F7/F9 Active Scanner Contracts

**Files:**
- Modify: `scripts/checks/active-workflow-boundary.sh`
- Modify: `guide/skills/scripts/validate-skills-strict.sh`
- Modify: `scripts/checks/ci-fast-portability-self-test.sh`
- Modify: `scripts/checks/core-cli-contract.sh`
- Modify: `scripts/checks/docs/size-policy.sh`
- Create: `scripts/checks/docs/size-policy-self-test.sh`
- Modify: `scripts/checks/docs-consistency.sh`

**Interfaces:** `grep` 状态 0=有匹配、1=无匹配、>1=工具错误；尺寸自测注入 201 行 `.py`。

- [ ] **Step 1: 扩展自测并确认 RED**

把两个遗漏入口加入 portability `checks`。增加 recording grep，运行 Core CLI contract 后
断言参数含 `.github/workflows` 且不存在独立 `.github`。尺寸自测创建并 trap 删除
`scripts/.size-policy-self-test.$$.py`，写 201 行，要求 size policy 失败并报告该路径。

```bash
checks+=(scripts/checks/active-workflow-boundary.sh
  guide/skills/scripts/validate-skills-strict.sh)
mkdir -p "$scratch/recording-grep"
printf '%s\n' '#!/usr/bin/env bash' \
  'printf "%s\n" "$@" > "$BUKIT_GREP_ARGS"' 'exit 1' > "$scratch/recording-grep/grep"
chmod +x "$scratch/recording-grep/grep"
BUKIT_GREP_ARGS="$scratch/grep.args" PATH="$scratch/recording-grep:$PATH" \
  bash scripts/checks/core-cli-contract.sh
grep -Fx '.github/workflows' "$scratch/grep.args"
if grep -Fx '.github' "$scratch/grep.args"; then fail "Core CLI scanned all of .github"; fi
i=0
while ((i < 201)); do printf 'pass\n'; i=$((i + 1)); done > "$probe"
if bash scripts/checks/docs/size-policy.sh >"$output" 2>&1; then
  fail "oversized Python script unexpectedly passed"
fi
```

Run: `bash scripts/checks/ci-fast-portability-self-test.sh`，然后
`bash scripts/checks/docs/size-policy-self-test.sh`。

Expected: 前者因 strict skill 调用 rg 或 injected grep 被隐藏而失败；后者因 Python
未进入策略而失败。

- [ ] **Step 2: 实现扫描状态、活跃范围和尺寸集合**

两个遗漏扫描器统一采用：

```bash
grep_status=0
matches="$(grep -RInE -- "$pattern" "${targets[@]}")" || grep_status=$?
if ((grep_status > 1)); then echo "<specific> text search failed" >&2; exit "$grep_status"; fi
```

strict skill 使用 `grep -RInE`，不再出现 `rg`。Core CLI 设置
`targets=(scripts .github/workflows)`。size policy 的 find predicate 改为
`\( -name '*.sh' -o -name '*.py' \)`，并追加 `.trae/skills`；docs consistency 在正式
size policy 前运行 `size-policy-self-test.sh`。

- [ ] **Step 3: 验证、即时审计并提交**

```bash
bash scripts/checks/ci-fast-portability-self-test.sh
bash scripts/checks/docs/size-policy-self-test.sh
bash scripts/checks/docs/size-policy.sh
bash scripts/checks/post-change-targeted.sh -- scripts/checks/active-workflow-boundary.sh \
  guide/skills/scripts/validate-skills-strict.sh scripts/checks/ci-fast-portability-self-test.sh \
  scripts/checks/core-cli-contract.sh scripts/checks/docs/size-policy.sh \
  scripts/checks/docs/size-policy-self-test.sh scripts/checks/docs-consistency.sh
git diff --check
git add scripts/checks guide/skills/scripts/validate-skills-strict.sh
git commit -m "fix(checks): fail closed across active scanners"
```

审计 fake grep >1 是否均非零、backup workflow 未进入参数、Python probe 必定被发现。

### Task 6: F5/F10 Brainstorm Lifecycle Safety

**Files:**
- Create: `.trae/skills/brainstorming/scripts/session-state.sh`
- Modify: `.trae/skills/brainstorming/scripts/start-server.sh`
- Modify: `.trae/skills/brainstorming/scripts/stop-server.sh`
- Create: `scripts/checks/brainstorm-server-self-test.sh`
- Modify: `scripts/gates/ci-fast.sh`

**Interfaces:** `write_session_state STATE PID TOKEN SERVER`；
`validate_session_process STATE` 打印已验证 PID；`classify_session_dir PATH` 打印
`ephemeral` 或 `persistent`。

- [ ] **Step 1: 写生命周期 RED 自测**

用后台 PID 加 20 次 `sleep 0.05` 实现 `run_with_deadline`；超时则 TERM/KILL 并返回
124，避免旧 parser 让自测挂起。断言 `start-server.sh --host`、前后台冲突在期限内立即
以 2 失败。临时 PATH 中的假 `node`
打印带 `BRAINSTORM_DIR` 的 `server-started` JSON 后循环，并在 TERM 时退出；用它完成
一次 ephemeral 和一次 project-dir 启停。断言前者被删除、后者保留。构造任意
`/tmp/not-brainstorm-*` 和错误 token 状态，使用独立 `sleep 60` 作为受保护 PID，断言
stop 不杀该进程且不删除目录；trap 负责清理 sleep。

```bash
run_with_deadline() {
  "$@" >"$output" 2>&1 & local pid=$! i
  for i in {1..20}; do
    if ! kill -0 "$pid" 2>/dev/null; then wait "$pid"; return $?; fi
    sleep 0.05
  done
  kill "$pid" 2>/dev/null || true; sleep 0.05
  kill -9 "$pid" 2>/dev/null || true; wait "$pid" 2>/dev/null || true
  return 124
}
```

Run: `bash scripts/checks/brainstorm-server-self-test.sh`

Expected: FAIL；旧 parser 挂起或旧 stop 接受不可信状态。

- [ ] **Step 2: 实现不可 source 的状态和进程验证**

`session-state.sh` 只读取独立单行文件 `server.pid`、`owner.uid`、`server.path`、
`server.token`。校验 PID/UID 为数字，token 匹配 `[A-Za-z0-9._-]+`；使用：

```bash
live_uid="$(ps -o uid= -p "$pid" | tr -d ' ')"
command="$(ps -ww -o command= -p "$pid")"
[[ "$live_uid" == "$owner_uid" && "$command" == *"$server_path"* \
  && "$command" == *"--session-token=$token"* ]] || return 1
```

`classify_session_dir` 用 `pwd -P` 规范化 `/tmp`，只接受其直接子目录
`brainstorm-[0-9]+-[0-9]+-[0-9]+`，或后缀
`/.superpowers/brainstorm/<同格式 ID>`。

- [ ] **Step 3: 加固 start/stop 并接入 ci-fast**

start 增加 strict mode、`require_value`、冲突检查、`umask 077`；SESSION_ID 为
`$$-$(date +%s)-$RANDOM`。以绝对 `server.cjs --session-token=$token` 启动，前台使用
`exec`，后台记录 `$!`，两者调用 `write_session_state`。stop 先 classify，再
`validate_session_process`，只对返回 PID 执行 TERM/等待；发 SIGKILL 前必须再次调用
`validate_session_process` 并确认仍是同一 PID；只删除 ephemeral。

在 ci-fast 增加 `run_step "brainstorm server self-test" bash scripts/checks/brainstorm-server-self-test.sh`。

- [ ] **Step 4: 验证、即时审计并提交**

```bash
bash scripts/checks/brainstorm-server-self-test.sh
bash -n .trae/skills/brainstorming/scripts/session-state.sh \
  .trae/skills/brainstorming/scripts/start-server.sh \
  .trae/skills/brainstorming/scripts/stop-server.sh scripts/checks/brainstorm-server-self-test.sh
bash scripts/checks/post-change-targeted.sh -- .trae/skills/brainstorming/scripts/session-state.sh \
  .trae/skills/brainstorming/scripts/start-server.sh \
  .trae/skills/brainstorming/scripts/stop-server.sh \
  scripts/checks/brainstorm-server-self-test.sh scripts/gates/ci-fast.sh
git diff --check
git add .trae/skills/brainstorming/scripts scripts/checks/brainstorm-server-self-test.sh \
  scripts/gates/ci-fast.sh
git commit -m "fix(skills): validate brainstorm server identity"
```

审计所有 kill/rm 路径均支配于 path+UID+command+token 验证，且 persistent 永不递归删。

### Task 7: F11 Reliable Polluter Search

**Files:**
- Modify: `.trae/skills/systematic-debugging/find-polluter.sh`
- Create: `scripts/checks/find-polluter-self-test.sh`
- Modify: `scripts/gates/ci-fast.sh`

**Interfaces:** 找到污染返回 1；零匹配、预存污染或测试失败但无污染返回 2；全部成功且
无污染才返回 0。

- [ ] **Step 1: 写路径与失败分类 RED 自测**

临时项目含 `tests/with space.test.ts`。假 npm 记录第三个参数，并按文件名选择成功、
失败或创建污染。分别断言 exact path 未拆分、polluter=1、failed-only=2、zero=2、
pre-existing=2、clean=0。

Run: `bash scripts/checks/find-polluter-self-test.sh`

Expected: FAIL；旧循环拆分空格且 failed-only 误报 clean。

- [ ] **Step 2: 实现 NUL 数组和四态分类**

```bash
TEST_FILES=()
test_list="$scratch/test-files.list"
find . -path "$TEST_PATTERN" -print0 > "$test_list" || {
  echo "Test discovery failed" >&2; exit 2;
}
while IFS= read -r -d '' test_file; do TEST_FILES+=("$test_file"); done < "$test_list"
[[ ${#TEST_FILES[@]} -gt 0 ]] || { echo "No tests matched" >&2; exit 2; }
[[ ! -e "$POLLUTION_CHECK" ]] || { echo "Pollution already exists" >&2; exit 2; }
failed_tests=()
for test_file in "${TEST_FILES[@]}"; do
  status=0; npm test -- "$test_file" >"$log" 2>&1 || status=$?
  if [[ -e "$POLLUTION_CHECK" ]]; then echo "FOUND POLLUTER: $test_file"; exit 1; fi
  [[ $status -eq 0 ]] || failed_tests+=("$test_file")
done
[[ ${#failed_tests[@]} -eq 0 ]] || { printf 'Test failed without pollution: %s\n' "${failed_tests[@]}" >&2; exit 2; }
```

增加 Bukit 前缀临时日志目录和 trap。在 ci-fast 增加独立 self-test run_step。

- [ ] **Step 3: 验证、审计并提交**

```bash
bash scripts/checks/find-polluter-self-test.sh
bash -n .trae/skills/systematic-debugging/find-polluter.sh scripts/checks/find-polluter-self-test.sh
bash scripts/checks/post-change-targeted.sh -- \
  .trae/skills/systematic-debugging/find-polluter.sh \
  scripts/checks/find-polluter-self-test.sh scripts/gates/ci-fast.sh
git add .trae/skills/systematic-debugging/find-polluter.sh \
  scripts/checks/find-polluter-self-test.sh scripts/gates/ci-fast.sh
git commit -m "fix(skills): preserve polluter test evidence"
```

### Task 8: Documentation and Completion Audit

**Files:**
- Modify: `guide/dev/testing.md`
- Modify: `guide/dev/release.md`
- Modify: `docs/bukit-1.0-contract-matrix.zh-CN.md`
- Verify: every F1-F11 implementation file from Tasks 1-7

**Interfaces:** 文档命令与实现精确一致；最终证据矩阵逐项证明 F1-F11。

- [ ] **Step 1: 同步活跃文档**

testing 文档加入 security self-test/真实 gate、archive smoke 接口和两个辅助自测；release
文档加入 `native-aot.sh VERSION RID OUTPUT [CONFIG]`、
`build-repro.sh VERSION RID [CONFIG]`、prepare/verify 与 selected RID 顺序。契约矩阵把
`build-repro.sh` 单名改成 `bash scripts/build/build-repro.sh <version> <rid> Release`。

- [ ] **Step 2: 运行文档定向门禁并提交**

```bash
bash scripts/checks/post-change-targeted.sh -- guide/dev/testing.md guide/dev/release.md \
  docs/bukit-1.0-contract-matrix.zh-CN.md
git diff --check
git add guide/dev/testing.md guide/dev/release.md docs/bukit-1.0-contract-matrix.zh-CN.md
git commit -m "docs: document strict script proof paths"
```

- [ ] **Step 3: 运行父任务最终证据集**

```bash
bash scripts/security/security-regression-self-test.sh
bash scripts/security/security-regression.sh Release
bash scripts/release/release-assets-self-test.sh
bash scripts/build/native-aot-self-test.sh
bash scripts/build/build-repro-self-test.sh
bash scripts/smoke/release-artifacts-self-test.sh
bash scripts/checks/ci-fast-portability-self-test.sh
bash scripts/checks/docs/size-policy-self-test.sh
bash scripts/checks/brainstorm-server-self-test.sh
bash scripts/checks/find-polluter-self-test.sh
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release
bash scripts/gates/ci-fast.sh Release
```

重新执行当前主机 RID 的真实 `build-repro.sh`。Expected: 全部退出 0；每个负向场景已在
对应 self-test 内被观察为预期非零。

Run: `bash scripts/build/build-repro.sh 0.0.0-ci "$(test "$(uname -s)-$(uname -m)" = Darwin-arm64 && echo osx-arm64 || echo linux-x64)" Release`

- [ ] **Step 4: 完成聚合只读审计**

从固定引用 `codex/script-p1-p3-hardening-base` 到 HEAD 逐项建立 F1-F11 -> 文件 -> RED ->
GREEN -> owning gate 证据表。运行：

```bash
git diff --check codex/script-p1-p3-hardening-base..HEAD
git diff --name-only codex/script-p1-p3-hardening-base..HEAD
git diff --name-only codex/script-p1-p3-hardening-base..HEAD -- guide-0.1 guide-0.2 scripts-0.1 scripts-0.2
while IFS= read -r -d '' path; do
  case "$path" in
    *.sh) bash -n "$path" ;;
    *.py) python3 -c 'import sys; from pathlib import Path; p=Path(sys.argv[1]); compile(p.read_text(), str(p), "exec")' "$path" ;;
  esac
done < <(find scripts guide/skills/scripts .trae/skills -type f \
  \( -name '*.sh' -o -name '*.py' \) -print0)
git status --short
```

Expected: backup 查询无输出；所有 F1-F11 有直接证据；没有无关路径或未解释 diff；
Shell/Python 语法全部通过。发现任何问题时回到所属 Task，修复、重跑定向门禁并只重审
受影响范围，不能以最终审计替代修复。
