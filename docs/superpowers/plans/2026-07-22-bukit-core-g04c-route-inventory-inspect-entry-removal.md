# Bukit Core G-04C `RouteInventoryInspectEntry` Single-Type Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove only `Bukit.Engine.RouteInventoryInspectEntry` from the Bukit 2.0 CLR surface while preserving routing behavior, the closed 136-candidate history, and every unrelated public contract.

**Architecture:** Establish a source-verifiable `2.0.0-alpha.1` development line first, then use one assembly-level RED test to drive the source deletion. Capture the resulting single breaking API diagnostic before generating a reviewed 539-type baseline, and record the decision in active governance without mutating the closed candidate manifest.

**Tech Stack:** .NET 10, C# 13, xUnit, `System.Reflection`, `System.Text.Json`, MSBuild, Bash, `jq`, Bukit's public API drift tool, Native AOT.

## Global Constraints

- Work only in `codex/g04c-route-inventory-inspect-entry-removal`, based on local integration branch `2.0` at `88a31b5eba2e52219ec3d1a107b703acdf9a3467`.
- This branch may merge only into local `2.0`; do not merge or cherry-pick any implementation commit into the 1.x `main` line.
- Do not push either branch, publish an artifact, create a Release, or alter GitHub Issue #60 without separate explicit authorization.
- Change the Bukit project version from exactly `1.0.10` to exactly `2.0.0-alpha.1` in a dedicated commit before deleting the type.
- Remove only `Bukit.Engine.RouteInventoryInspectEntry`; do not rename it, make it `internal`, add an `Obsolete` facade, or expose the private `RouteInventoryEntry` implementation.
- Do not change routing behavior, configuration schema, plugin protocol, persistence formats, asset URLs, output paths, HTTP/TLS behavior, or global path tools.
- Preserve `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` byte-for-byte as the closed 136-candidate historical cohort.
- The governed public API baseline must change from 540 to 539 types and from 136 to 135 `2.0-candidate` entries, with no other semantic change.
- If any new direct CLR, reflection, serializer, inheritance, signature, Native AOT, private, or public consumer evidence appears, stop and restore the type and old baseline; compatibility design belongs in a separate task.
- After each code/documentation subtask, run only the focused affected check. Run the aggregate targeted gate exactly once at the end with base `88a31b5eba2e52219ec3d1a107b703acdf9a3467`.
- Do not run `ci-full`, the release gate, `scripts/test-all.sh`, `scripts/smoke-all.sh`, or whole-solution tests.
- Environment, permission, NuGet, or infrastructure failures are blockers to classify; they do not authorize unrelated repairs or suppression.

## File Map

- Modify `Directory.Build.props`: establish the 2.0 alpha version line; no other build property changes.
- Modify `src/Bukit-Core/Bukit.Engine/RouteInventoryValidator.cs`: delete only the unused top-level public record.
- Create `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs`: prevent type reintroduction and govern version, current baseline, historical manifest, and active documentation state.
- Modify `docs/governance/bukit-core-public-api-baseline.v1.json`: replace with the reviewed snapshot containing exactly one type removal.
- Do not modify `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`: tests read it as immutable historical evidence.
- Modify `docs/governance/bukit-core-2.0-consumer-declaration.md`: replace the old no-authorization sentence with the single-type decision and link the ledger.
- Modify `guide/dev/public-api-governance.md`: document the 2.0 pilot and separate historical cohort from the live baseline.
- Create `docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md`: permanent decision, migration, evidence, verification, and review ledger.
- Existing approved design and this plan remain part of the aggregate diff; do not rewrite their scope during implementation.

---

### Task 1: Establish the isolated Bukit 2.0 alpha version line

**Files:**
- Modify: `Directory.Build.props:9-12`

**Interfaces:**
- Consumes: the existing conditional `<Version>1.0.10</Version>` applied to projects whose names start with `Bukit.`.
- Produces: the exact MSBuild product version `2.0.0-alpha.1` used by every later compile and Native AOT command.

- [ ] **Step 1: Reconfirm branch isolation and a clean implementation start**

Run:

```bash
test "$(git branch --show-current)" = "codex/g04c-route-inventory-inspect-entry-removal"
test "$(git merge-base HEAD 88a31b5eba2e52219ec3d1a107b703acdf9a3467)" = "88a31b5eba2e52219ec3d1a107b703acdf9a3467"
test -z "$(git status --short)"
git branch --list 2.0
```

Expected: the first three commands exit 0 and the last command prints local branch `2.0`. Stop if the task branch is not based on the approved 1.x closure point.

- [ ] **Step 2: Change only the project version**

Apply this exact replacement in `Directory.Build.props`:

```xml
  <PropertyGroup Condition="$([System.String]::Copy('$(MSBuildProjectName)').StartsWith('Bukit.'))">
    <Version>2.0.0-alpha.1</Version>
  </PropertyGroup>
```

Do not add `VersionPrefix`, `PackageVersion`, release notes, or a second version property.

- [ ] **Step 3: Verify the version diff and focused owners**

Run:

```bash
git diff --check -- Directory.Build.props
git diff --word-diff=plain -- Directory.Build.props
bash scripts/checks/post-change-focused.sh -- Directory.Build.props
```

Expected: the diff contains only `1.0.10 -> 2.0.0-alpha.1`; the focused check runs the format and code-analysis owner self-tests and exits 0.

- [ ] **Step 4: Commit the version boundary separately**

```bash
git add Directory.Build.props
git commit -m "build: start Bukit 2.0 alpha line"
```

Expected: one commit contains only `Directory.Build.props`.

---

### Task 2: Drive the single type removal with an assembly-level RED test

**Files:**
- Create: `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/RouteInventoryValidator.cs:10-18`

**Interfaces:**
- Consumes: `Bukit.Engine.RouteInventoryValidator` only as a stable assembly marker.
- Produces: an architecture guard that resolves the exact, case-sensitive CLR full name and requires it to be absent.

- [ ] **Step 1: Add only the initial failing type-absence test**

Create `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs` with:

```csharp
using Bukit.Engine;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04CPublicSurfacePilotTests
{
    private const string RemovedTypeName = "Bukit.Engine.RouteInventoryInspectEntry";

    [Fact]
    public void EngineAssembly_DoesNotExposeRemovedRouteInventoryInspectEntry()
    {
        var engineAssembly = typeof(RouteInventoryValidator).Assembly;

        Assert.Null(engineAssembly.GetType(RemovedTypeName, throwOnError: false, ignoreCase: false));
    }
}
```

- [ ] **Step 2: Run the test and prove the intended RED state**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --no-restore \
  --filter FullyQualifiedName~G04CPublicSurfacePilotTests.EngineAssembly_DoesNotExposeRemovedRouteInventoryInspectEntry
```

Expected: exactly this test fails because `Assembly.GetType` returns `Bukit.Engine.RouteInventoryInspectEntry`. A restore/assets error is not valid RED evidence; restore the test project and rerun if necessary.

- [ ] **Step 3: Delete only the unused top-level public record**

Remove exactly this block from `src/Bukit-Core/Bukit.Engine/RouteInventoryValidator.cs`:

```csharp
public sealed record RouteInventoryInspectEntry(
    string Url,
    string OutputPath,
    string Template,
    string? Collection,
    string? Type,
    string? Language,
    string RouteSource);
```

Leave all `using` directives, `RouteInventoryValidator`, and its private nested `RouteInventoryEntry` unchanged unless the compiler proves an existing `using` became unused. Do not mechanically format unrelated code.

- [ ] **Step 4: Run the GREEN test and the affected Engine regression**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --no-restore \
  --filter FullyQualifiedName~G04CPublicSurfacePilotTests.EngineAssembly_DoesNotExposeRemovedRouteInventoryInspectEntry
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore
```

Expected: the architecture test passes and all Engine tests pass. Stop on any routing, conflict, template, content-loading, or compile regression.

- [ ] **Step 5: Prove repository-local source usage is gone without rewriting historical evidence**

Run:

```bash
rg -n "RouteInventoryInspectEntry" src tests \
  --glob '!tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs'
git diff --check -- \
  src/Bukit-Core/Bukit.Engine/RouteInventoryValidator.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs
bash scripts/checks/post-change-focused.sh -- \
  src/Bukit-Core/Bukit.Engine/RouteInventoryValidator.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs
```

Expected: `rg` exits 1 with no matches; focused verification exits 0 and maps the source change to `Bukit.Engine.Tests` plus the new test to `Bukit.Architecture.Tests`.

- [ ] **Step 6: Commit the TDD guard and source deletion**

```bash
git add \
  src/Bukit-Core/Bukit.Engine/RouteInventoryValidator.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs
git commit -m "breaking(engine): remove route inventory inspect entry"
```

Expected: the commit does not contain baseline, manifest, schema, protocol, or unrelated source changes.

---

### Task 3: Capture the one-item breaking drift and deliberately update the governed baseline

**Files:**
- Modify: `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs`
- Modify: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Read only: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`

**Interfaces:**
- Consumes: the source-deleted Engine assembly, the 540-type governed baseline, and the closed 136-candidate manifest.
- Produces: a 539-type current baseline with 135 live `2.0-candidate` entries, plus tests that keep the closed manifest at 136 historical entries.

- [ ] **Step 1: Capture the pre-approval drift and require exactly one diagnostic**

Run:

```bash
set +e
bash scripts/checks/public-api-drift.sh check Release \
  > /tmp/bukit-g04c-route-inventory-drift.txt 2>&1
status=$?
set -e
test "$status" -eq 1
grep -Fx \
  'breaking: Bukit.Engine::Bukit.Engine.RouteInventoryInspectEntry: exported type removed' \
  /tmp/bukit-g04c-route-inventory-drift.txt
test "$(grep -c '^breaking:' /tmp/bukit-g04c-route-inventory-drift.txt)" -eq 1
test "$(grep -Ec '^(review-required|protected-review|type-shape-review|contract-shape-review|aot-review|unclassified|gate-error):' /tmp/bukit-g04c-route-inventory-drift.txt)" -eq 0
```

Expected: the check exits 1, and the captured file contains exactly one breaking diagnostic: the target exported type removal. Any second drift category stops the task before baseline generation.

- [ ] **Step 2: Generate a new snapshot and prove its semantic delta**

Run:

```bash
snapshot_root="$(mktemp -d "${TMPDIR:-/tmp}/bukit-g04c-snapshot.XXXXXX")"
snapshot="$snapshot_root/bukit-core-public-api-baseline.v1.json"
bash scripts/checks/public-api-drift.sh snapshot "$snapshot" Release

test "$(jq '.types | length' "$snapshot")" -eq 539
test "$(jq '[.types[] | select(.compatibility == "2.0-candidate")] | length' "$snapshot")" -eq 135
test "$(jq '[.types[] | select(.assembly == "Bukit.Engine" and .name == "Bukit.Engine.RouteInventoryInspectEntry")] | length' "$snapshot")" -eq 0

jq -S 'del(.types[] | select(.assembly == "Bukit.Engine" and .name == "Bukit.Engine.RouteInventoryInspectEntry"))' \
  docs/governance/bukit-core-public-api-baseline.v1.json > "$snapshot_root/expected.json"
jq -S '.' "$snapshot" > "$snapshot_root/actual.json"
diff -u "$snapshot_root/expected.json" "$snapshot_root/actual.json"
```

Expected: all count checks pass and `diff` emits no output. This semantic comparison proves assembly mappings, schema metadata, SDK policy, target framework, all other types, and their members/classifications are unchanged.

- [ ] **Step 3: Replace the governed baseline with the reviewed generated snapshot**

Run:

```bash
/bin/cp "$snapshot" docs/governance/bukit-core-public-api-baseline.v1.json
```

This is the approved generated-snapshot operation. Do not hand-edit surrounding entries or change the closed candidate manifest.

- [ ] **Step 4: Extend the architecture guard for version, live baseline, and historical cohort**

Replace `G04CPublicSurfacePilotTests.cs` with:

```csharp
using System.Text.Json;
using System.Xml.Linq;
using Bukit.Engine;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04CPublicSurfacePilotTests
{
    private const string RemovedTypeName = "Bukit.Engine.RouteInventoryInspectEntry";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void EngineAssembly_DoesNotExposeRemovedRouteInventoryInspectEntry()
    {
        var engineAssembly = typeof(RouteInventoryValidator).Assembly;

        Assert.Null(engineAssembly.GetType(RemovedTypeName, throwOnError: false, ignoreCase: false));
    }

    [Fact]
    public void ProductVersion_IsTheApprovedTwoPointZeroAlpha()
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, "Directory.Build.props"));
        var versions = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Version")
            .Select(element => element.Value)
            .ToArray();

        Assert.Equal(["2.0.0-alpha.1"], versions);
    }

    [Fact]
    public void CurrentPublicApiBaseline_ContainsOnlyTheApprovedRemoval()
    {
        using var document = ReadJson("docs", "governance", "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal("bukit-core-public-api-baseline-v1", root.GetProperty("schema").GetString());
        Assert.Equal("net10.0", root.GetProperty("targetFramework").GetString());
        Assert.Equal("no-general-clr-sdk", root.GetProperty("sdkPolicy").GetString());
        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(539, types.Length);
        Assert.Equal(135, types.Count(type =>
            type.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.DoesNotContain(types, type =>
            type.GetProperty("assembly").GetString() == "Bukit.Engine" &&
            type.GetProperty("name").GetString() == RemovedTypeName);
    }

    [Fact]
    public void ClosedCandidateManifest_PreservesTheHistoricalPilotEvidence()
    {
        using var document = ReadJson("docs", "governance", "bukit-core-2.0-public-surface-candidates.v1.json");
        var root = document.RootElement;
        var candidates = root.GetProperty("candidates").EnumerateArray().ToArray();
        var target = Assert.Single(candidates, candidate =>
            candidate.GetProperty("fullName").GetString() == RemovedTypeName);

        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);
        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal("consumer-declaration-pending", target.GetProperty("declarationStatus").GetString());
        Assert.Equal("unknown-until-voluntary-declaration", target.GetProperty("privateConsumerStatus").GetString());
        Assert.Equal("no-public-match-found", target.GetProperty("externalEvidence").GetProperty("searchStatus").GetString());

        var queries = target.GetProperty("externalEvidence").GetProperty("queries").EnumerateArray().ToArray();
        Assert.Equal(2, queries.Length);
        Assert.All(queries, query =>
        {
            Assert.Equal(0, query.GetProperty("returned").GetInt32());
            Assert.False(query.GetProperty("truncated").GetBoolean());
        });
    }

    private static JsonDocument ReadJson(params string[] relativeSegments)
    {
        var path = Path.Combine([RepoRoot, .. relativeSegments]);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
```

- [ ] **Step 5: Run baseline and historical-evidence tests**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --no-restore \
  --filter FullyQualifiedName~G04CPublicSurfacePilotTests
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
git diff --exit-code 88a31b5eba2e52219ec3d1a107b703acdf9a3467 -- \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
bash scripts/checks/post-change-focused.sh -- \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  docs/governance/bukit-core-public-api-baseline.v1.json
```

Expected: all four pilot tests pass; drift self-test and real check exit 0; the manifest diff is empty; focused verification exits 0.

- [ ] **Step 6: Commit the deliberate baseline update and its guards**

```bash
git add \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  docs/governance/bukit-core-public-api-baseline.v1.json
git commit -m "test(governance): approve G-04C baseline removal"
```

Expected: this commit modifies only the current baseline and architecture guard; it does not contain the closed manifest.

---

### Task 4: Record the single-type decision and migration boundary

**Files:**
- Create: `docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md`
- Modify: `docs/governance/bukit-core-2.0-consumer-declaration.md:87-92`
- Modify: `guide/dev/public-api-governance.md:127-151`
- Modify: `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs`

**Interfaces:**
- Consumes: the exact one-item drift evidence, 539-type baseline, immutable 136-entry manifest, and approved 2.0-only decision.
- Produces: one permanent Chinese ledger and two active governance links that distinguish the current public surface from the historical declaration cohort.

- [ ] **Step 1: Create the permanent G-04C decision ledger**

Create `docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md` with this complete structure and wording:

```markdown
# Bukit Core G-04C `RouteInventoryInspectEntry` 单类型删除关闭台账

日期：2026-07-22

状态：实施记录已建立 / 跨边界验证与独立复审待执行

基线：`main@88a31b5eba2e52219ec3d1a107b703acdf9a3467`

目标版本线：`2.0.0-alpha.1`

## 1. 决策

G-04C 本次只批准从 Bukit 2.0 CLR 公共面删除
`Bukit.Engine.RouteInventoryInspectEntry`。其余 135 项候选没有获得批量变更授权；
1.x `main` 的 CLR 可见性不受本任务影响。

该类型是未被生产代码消费的实现型 DTO。`RouteInventoryValidator` 的实际业务逻辑使用
私有嵌套 `RouteInventoryEntry`，因此本次删除不改变内容路由生成、模板解析、路径安全、
冲突检测或构建输出。

## 2. 消费者证据边界

仓库语义检索没有发现 Core、Labs、官方插件或测试消费者；G-04B3 的认证公开搜索结果为
`no-public-match-found`。这只能证明已审阅的公开证据没有命中，不能证明私人、未索引或
未自愿声明的消费者不存在。

关闭的 136 项 manifest 保留窗口关闭时的原始 candidate identity、搜索结果和
`unknown-until-voluntary-declaration` 状态。它是历史 cohort，不是删除后的当前公共面
枚举，因此本任务没有删除或重写其中的目标条目。

## 3. 公共面变化

- 产品版本：`1.0.10 -> 2.0.0-alpha.1`；
- 当前 baseline 类型：`540 -> 539`；
- 当前 baseline 的 `2.0-candidate`：`136 -> 135`；
- 删除项：`Bukit.Engine::Bukit.Engine.RouteInventoryInspectEntry`；
- schema、target framework、SDK policy、assembly mapping 和其余 539 项保持不变。

baseline 更新前，真实 drift check 只产生一条诊断：

```text
breaking: Bukit.Engine::Bukit.Engine.RouteInventoryInspectEntry: exported type removed
```

## 4. 兼容性与迁移

这是 Bukit 2.0 的 source/binary breaking change，没有替代 API。若私人消费者直接构造
该记录，应删除引用并使用消费者自己的数据结构；不得引用或要求 Bukit 暴露内部
`RouteInventoryEntry`。

若后续出现新的直接 CLR、反射、序列化、继承、公共签名或 Native AOT 消费证据，
必须重新开启独立兼容性任务。本台账不授权临时 facade、兼容 shim 或另外 135 项变更。

## 5. 当前证据与待验收项

- 删除前架构测试按预期 RED，删除后同一测试 GREEN；
- 目标 G-04C 架构测试与 `Bukit.Engine.Tests` 通过；
- public API drift self-test 及更新后的真实 check 通过；
- Core、Labs、`bukit-plugins.slnx`、`osx-arm64` Native AOT、release-artifact
  smoke、aggregate targeted gate 和独立只读复审尚未执行。

未执行项必须在最终关闭提交中根据真实结果改为通过或明确阻塞；不得预先声称通过。

## 6. 复审结论

最终关闭需独立只读复审确认 diff 只包含已批准的 2.0 版本线、单类型源码删除、
当前 baseline 精确更新、架构守卫和治理文档。路由行为、配置 schema、插件协议、
持久化格式、asset URL、输出路径、HTTP/TLS 策略及全局路径工具必须保持未改变。

本台账关闭的只是一个单类型试点，不代表 G-04C 批量收窄已经获批或完成。
```

Do not change the provisional status to a closure claim in this task. Task 6 performs that update only after the cross-boundary proof and first independent review have actually passed.

- [ ] **Step 2: Update the active consumer declaration**

In `docs/governance/bukit-core-2.0-consumer-declaration.md`, replace the bullet saying the target remains unapproved with this paragraph, linking the ledger relatively:

```markdown
## G-04C Single-Type Decision

G-04C single-type decision: only `Bukit.Engine.RouteInventoryInspectEntry` is
approved for removal in 2.0; the other 135 candidates are not batch-approved.
The [G-04C decision ledger](../analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md)
records the exact drift, migration boundary, verification, and independent review.

The closed 136-entry candidate manifest remains the immutable historical cohort
captured at declaration-window closure. The current public API baseline is the
source of truth for the post-removal CLR surface.
```

Keep all statements about private-consumer uncertainty, Issue #60 history, and 1.x isolation.

- [ ] **Step 3: Update the public API governance guide**

Append this subsection after `## 2.0 Consumer Declaration Window` and before `## Historical Feedback Channel` in `guide/dev/public-api-governance.md`:

```markdown
### G-04C Single-Type Pilot

G-04C single-type decision: only `Bukit.Engine.RouteInventoryInspectEntry` is
approved for removal in 2.0; the other 135 candidates are not batch-approved.
See the [decision ledger](../../docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md)
for the breaking-change evidence, migration boundary, targeted verification,
and independent review.

The closed 136-entry candidate manifest is an immutable declaration-window
snapshot. It intentionally retains the removed type and its original search
evidence. The governed public API baseline, not that historical cohort, is the
current CLR surface inventory.
```

- [ ] **Step 4: Add exact documentation assertions to the architecture guard**

Add this test above `ReadJson` in `G04CPublicSurfacePilotTests.cs`:

```csharp
    [Fact]
    public void ActiveGovernance_RecordsOnlyTheApprovedSingleTypeDecision()
    {
        const string decision = "G-04C single-type decision: only `Bukit.Engine.RouteInventoryInspectEntry` is";
        const string remainder = "the other 135 candidates are not batch-approved.";
        var declaration = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-consumer-declaration.md"));
        var guide = File.ReadAllText(Path.Combine(RepoRoot, "guide", "dev", "public-api-governance.md"));
        var ledgerPath = Path.Combine(
            RepoRoot,
            "docs",
            "analysis",
            "bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md");

        Assert.Contains(decision, declaration, StringComparison.Ordinal);
        Assert.Contains(remainder, declaration, StringComparison.Ordinal);
        Assert.Contains(decision, guide, StringComparison.Ordinal);
        Assert.Contains(remainder, guide, StringComparison.Ordinal);
        Assert.True(File.Exists(ledgerPath), $"Missing G-04C decision ledger: {ledgerPath}");

        var ledger = File.ReadAllText(ledgerPath);
        Assert.Contains("其余 135 项候选没有获得批量变更授权", ledger, StringComparison.Ordinal);
        Assert.Contains("历史 cohort", ledger, StringComparison.Ordinal);
        Assert.Contains("没有替代 API", ledger, StringComparison.Ordinal);
    }
```

- [ ] **Step 5: Validate links, wording, immutable history, and affected tests**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --no-restore \
  --filter FullyQualifiedName~G04CPublicSurfacePilotTests
bash scripts/checks/docs-consistency.sh
git diff --exit-code 88a31b5eba2e52219ec3d1a107b703acdf9a3467 -- \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
rg -n "G-04C single-type decision|other 135 candidates|historical cohort|current public API baseline" \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md
bash scripts/checks/post-change-focused.sh -- \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md
```

Expected: five pilot tests pass, documentation checks and links pass, the closed manifest remains byte-identical to the base, and focused verification exits 0.

- [ ] **Step 6: Commit the governance closure record**

```bash
git add \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md
git commit -m "docs(governance): record G-04C single-type decision"
```

Expected: documentation and its executable guard are committed together.

---

### Task 5: Run cross-boundary compile and Native AOT proof

**Files:**
- Verify only: `bukit-core.slnx`
- Verify only: `bukit-labs.slnx`
- Verify only: `bukit-plugins.slnx`
- Verify only: `scripts/build/native-aot.sh`
- Verify only: `scripts/smoke/release-artifacts.sh`

**Interfaces:**
- Consumes: the complete implementation commits from Tasks 1-4.
- Produces: evidence that no hidden Core, Labs, official plugin, trimming, or Native AOT consumer depends on the removed type.

- [ ] **Step 1: Run the complete affected test projects**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore
```

Expected: all tests pass with 0 failures. Record actual totals in the task handoff; do not infer success from an empty or restore-blocked invocation.

- [ ] **Step 2: Build Core, Labs, and the complete plugin solution**

Run:

```bash
dotnet build bukit-core.slnx -c Release --no-restore --nologo
dotnet build bukit-labs.slnx -c Release --no-restore --nologo
dotnet build bukit-plugins.slnx -c Release --no-restore --nologo
```

Expected: all three builds exit 0 with no errors. A project-assets failure requires an explicit restore and rerun; it is not compile evidence.

- [ ] **Step 3: Produce and smoke one real `osx-arm64` Native AOT archive**

Run outside the restricted sandbox if the real Native AOT toolchain requires it:

```bash
aot_root="$(mktemp -d "${TMPDIR:-/tmp}/bukit-g04c-aot.XXXXXX")"
archive="$(bash scripts/build/native-aot.sh 2.0.0-alpha.1 osx-arm64 "$aot_root" Release)"
test -s "$archive"
bash scripts/smoke/release-artifacts.sh "$archive" osx-arm64
```

Expected: Native AOT publish and archive creation succeed, the archive is non-empty, and the basic Markdown release smoke completes config check, build, and publish audit. This artifact remains temporary and is not uploaded or released.

- [ ] **Step 4: Reconfirm the final real public API check**

Run:

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
```

Expected: both commands exit 0 and the real check reports no drift.

---

### Task 6: Complete independent review, close the ledger, and run the one aggregate gate

**Files:**
- Review: every path changed from `88a31b5eba2e52219ec3d1a107b703acdf9a3467`
- Do not modify: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`

**Interfaces:**
- Consumes: all committed design, plan, version, source, test, baseline, and governance changes.
- Produces: two independent read-only review verdicts, a truthful final ledger, and one aggregate targeted gate result for merge consideration into local `2.0` only.

- [ ] **Step 1: Audit the complete path and commit scope before independent review**

Run:

```bash
git status --short
git diff --check 88a31b5eba2e52219ec3d1a107b703acdf9a3467
git diff --name-status 88a31b5eba2e52219ec3d1a107b703acdf9a3467
git log --oneline --decorate 88a31b5eba2e52219ec3d1a107b703acdf9a3467..HEAD
git diff --exit-code 88a31b5eba2e52219ec3d1a107b703acdf9a3467 -- \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Expected: the worktree is clean; only the approved files are listed; version, deletion, baseline, and provisional governance remain separate commits; the candidate manifest has no diff.

- [ ] **Step 2: Request the first independent read-only implementation review**

Give a fresh reviewer this exact scope:

```text
Read-only review the G-04C implementation diff from
88a31b5eba2e52219ec3d1a107b703acdf9a3467 through HEAD. Verify: only
Bukit.Engine.RouteInventoryInspectEntry was removed; no route behavior changed;
Directory.Build.props is exactly 2.0.0-alpha.1; the public API baseline semantic
delta is exactly that one type; the closed 136-candidate manifest is
byte-identical; the tests independently inspect the compiled assembly and
governed files; Core/Labs/plugins/AOT evidence is real and honest. Report
Critical, Important, and Minor findings with file and line evidence. Do not edit
files.
```

Expected: no unresolved Critical or Important finding. Resolve any in-scope finding with the relevant focused check before continuing; do not broaden the source, baseline, manifest, schema, protocol, or path-tool scope.

- [ ] **Step 3: Convert the provisional ledger to a truthful closure record**

Only after Task 5 and Step 2 have passed, make these exact changes in
`docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md`:

Replace the status with:

```markdown
状态：已实施并通过跨边界验证与独立只读复审
```

Replace `## 5. 当前证据与待验收项` and its body with:

```markdown
## 5. 验证证据

- 删除前架构测试按预期 RED，删除后同一测试 GREEN；
- `Bukit.Engine.Tests` 与完整 `Bukit.Architecture.Tests` 通过；
- public API drift self-test 及更新后的真实 check 通过；
- Core、Labs 与 `bukit-plugins.slnx` Release 编译通过；
- `osx-arm64` Native AOT 归档构建及 release-artifact smoke 通过；
- 第一次独立只读实施复审未发现未关闭的 Critical 或 Important finding。

环境或基础设施阻塞必须保留为未取得证据，不得记录为通过。父任务的
aggregate targeted gate 和最终 aggregate diff 复审在本关闭提交后执行，并以任务
最终交接记录为准。
```

Replace the first paragraph under `## 6. 复审结论` with:

```markdown
第一次独立只读复审确认 diff 只包含已批准的 2.0 版本线、单类型源码删除、
当前 baseline 精确更新、架构守卫和治理文档。路由行为、配置 schema、插件协议、
持久化格式、asset URL、输出路径、HTTP/TLS 策略及全局路径工具均未改变。
```

Then run and commit:

```bash
bash scripts/checks/post-change-focused.sh -- \
  docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md
git add docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md
git commit -m "docs(governance): close G-04C pilot ledger"
```

Expected: only evidence already obtained in Task 5 and Step 2 is promoted to `通过`; the aggregate gate is not prematurely claimed inside the committed ledger.

- [ ] **Step 4: Request the final independent aggregate diff review**

Give a different fresh reviewer this exact scope:

```text
Read-only final aggregate review from
88a31b5eba2e52219ec3d1a107b703acdf9a3467 through HEAD for G-04C. Verify the
approved design and implementation plan are followed; only
Bukit.Engine.RouteInventoryInspectEntry is removed; no route behavior changed;
the 2.0.0-alpha.1 version commit is isolated; the baseline semantic delta is
exactly one exported type; the closed 136-entry manifest is byte-identical;
active docs distinguish the 539-type current baseline from historical evidence;
there is no replacement API; the other 135 candidates are not authorized; all
test/build/AOT claims match recorded commands; and no schema, plugin protocol,
persistence, asset URL, output path, HTTP/TLS, or global path-tool drift exists.
Report Critical, Important, and Minor findings with exact file and line evidence.
Do not edit files.
```

Expected: no unresolved Critical or Important finding. A finding blocks the aggregate completion gate until repaired and re-reviewed with focused evidence.

- [ ] **Step 5: Run the parent task's only aggregate targeted gate**

After both read-only reviews are clean and the worktree is unchanged, run exactly once:

```bash
bash scripts/checks/post-change-targeted.sh \
  --base 88a31b5eba2e52219ec3d1a107b703acdf9a3467 -- \
  Directory.Build.props \
  src/Bukit-Core/Bukit.Engine/RouteInventoryValidator.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md \
  docs/superpowers/specs/2026-07-22-bukit-core-g04c-route-inventory-inspect-entry-removal-design.zh-CN.md \
  docs/superpowers/plans/2026-07-22-bukit-core-g04c-route-inventory-inspect-entry-removal.md
```

Expected: focused affected checks and `ci-fast Release` both pass. Do not rerun to hide a failure; classify and fix only a failure causally connected to an approved changed path, then disclose that a new aggregate completion attempt was required.

- [ ] **Step 6: Perform the final local merge-readiness audit**

Run:

```bash
test -z "$(git status --short)"
test "$(git merge-base HEAD 2.0)" = "$(git rev-parse 2.0)"
test "$(git rev-parse main)" = "88a31b5eba2e52219ec3d1a107b703acdf9a3467"
git diff --stat 2.0...HEAD
git log --oneline 2.0..HEAD
```

Expected: task branch is clean and based on local `2.0`; 1.x `main` remains at the approved closure commit; the feature branch is ready for a separate explicit local merge request into `2.0`. Do not merge, push, or publish automatically.

## Rollback Boundary

If removal rollback is required before merge, revert Tasks 2-4 as a unit: source deletion, G-04C tests, current baseline, ledger, declaration, and guide. The dedicated `2.0.0-alpha.1` version commit from Task 1 may remain on `2.0` only if the user separately decides to retain the 2.0 development line. Never resolve rollback by changing 1.x `main`, rewriting the closed manifest, or inventing a compatibility facade inside this task.
