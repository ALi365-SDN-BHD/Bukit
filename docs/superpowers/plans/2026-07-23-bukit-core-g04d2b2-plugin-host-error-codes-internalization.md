# Bukit Core G-04D2B2 PluginHost Error Codes Internalization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Narrow only `Bukit.PluginHost.PluginHostErrorCodes` from public to
internal in 2.0 while preserving the six diagnostic strings, all
`PluginProtocolClient` behavior, the closed consumer manifest, and real Native
AOT/process-plugin operation.

**Architecture:** Keep `PluginHostErrorCodes` as the same-assembly constant
owner and change only the containing class access modifier. Migrate the
governed public API snapshot from 14/508/104 to 14/507/103, update only live
current-state assertions, and retain G-04D2B1's independent runtime/wire
contracts. Qualify the final implementation with a published osx-arm64 Native
AOT CLI, release-artifact smoke, and a real Echo process-plugin path before the
single aggregate gate.

**Tech Stack:** C# 14 / .NET 10, xUnit, JSON public API snapshots, Bash owner
checks, Native AOT, `bukit-plugin-v1`.

## Global Constraints

- Base is exactly
  `757fb14976ad7337edc2a6fbf925b986222dea6f`; implementation occurs only on
  `codex/g04d2b2-error-codes-internalization`.
- The only production code change is
  `public static class PluginHostErrorCodes` to
  `internal static class PluginHostErrorCodes`.
- Keep all six const member names, declaration order, types, values and const
  semantics byte-for-byte unchanged.
- Do not modify `PluginProtocolClient`, `PluginPermissionEvaluator`,
  `PluginProtocolClientTests`, the diagnostic vocabulary fixture, plugin
  protocol or security documents, DTO/schema/config semantics, CLI behavior,
  official plugins, or any other PluginHost access level.
- Do not add `InternalsVisibleTo`, replacement constants, facade, enum, type
  forwarding, trim roots, reflection annotations, or a new public contract.
- The generated public API baseline must be exactly 14 assemblies / 507 types /
  103 `2.0-candidate` entries and may remove only the target type record and
  its six public const members.
- `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` must remain
  byte-identical with Git blob
  `7b07d6890562387010b52301e9f8716e9bf10ed1`.
- Keep `unknown-until-voluntary-declaration`; do not claim zero private,
  unindexed, binary, copied-source or reflection consumers.
- Preserve historical decision sentences and historical remainder counts.
  Update only facts explicitly describing the live current baseline.
- Do not touch `guide-0.1/`, `guide-0.2/`, `scripts-0.1/` or `scripts-0.2/`.
- Do not modify CI, release, gate, build or smoke scripts.
- Do not run full/release gates, `test-all`, `smoke-all`, or whole-solution
  tests.
- Run one code-subtask `post-change-focused.sh`; run
  `post-change-targeted.sh` exactly once, only after AOT/process qualification
  passes and the final diff is frozen.
- Use non-sandbox execution for real builds, tests and Git writes, as explicitly
  authorized by the user.
- Any environment or infrastructure blocker must be recorded as
  `qualification-blocked`; it does not count as task completion.

---

### Task 1: TDD internalization, governed baseline and active governance

**Files:**

- Modify:
  `src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs`
- Modify:
  `tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs`
- Modify:
  `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs`
- Modify:
  `tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs`
- Modify:
  `tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs`
- Modify:
  `tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs`
- Modify:
  `tests/Bukit.Architecture.Tests/G04D2APluginSecretMaskerInternalizationTests.cs`
- Modify:
  `docs/governance/bukit-core-public-api-baseline.v1.json`
- Modify:
  `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Modify:
  `guide/dev/public-api-governance.md`
- Create:
  `docs/analysis/bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md`

**Interfaces:**

- Consumes: G-04D2B1's public-entry behavior tests and
  `plugin-host-error-vocabulary.v1.json`.
- Produces: internal same-assembly `PluginHostErrorCodes`, governed baseline
  14/507/103, exact active-governance decision, and a provisional execution
  ledger that Task 2 will complete with observed proof.

- [ ] **Step 0: Reconfirm and record the clean implementation baseline**

Before editing any test or production file, run:

```bash
dotnet test \
  tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off

dotnet test \
  tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off

git hash-object \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Expected:

- PluginHost: 170 passed, 0 failed;
- Architecture: 130 passed, 0 failed;
- manifest blob:
  `7b07d6890562387010b52301e9f8716e9bf10ed1`.

Record the three outputs in the Task 1 report. Any failed test or different
manifest blob is a stop condition, not authorization to edit unrelated code.

- [ ] **Step 1: Replace the B1 public-surface assertion with separate B2
  visibility and baseline facts**

In
`tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs`:

1. Remove the `BaselineMembers` array.
2. Add these constants after `CandidateManifestBlob`:

```csharp
private const string Decision =
    "G-04D2B2 single-type internalization decision: only `Bukit.PluginHost.PluginHostErrorCodes` is narrowed from public to internal in 2.0; the other 103 candidates are not batch-approved.";
private const string CurrentBaseline =
    "The current public API baseline contains 507 types, including 103 `2.0-candidate` entries.";
```

3. Replace `CurrentPublicSurface_KeepsErrorCodeTypeAndExactBaseline` with:

```csharp
[Fact]
public void PluginHostAssembly_KeepsErrorCodeTypeInternalAndDoesNotExportIt()
{
    var assembly = typeof(Bukit.PluginHost.PluginConfigLoader).Assembly;
    var type = assembly.GetType(
        TargetTypeName,
        throwOnError: false,
        ignoreCase: false);

    Assert.NotNull(type);
    Assert.False(type.IsPublic);
    Assert.DoesNotContain(
        assembly.GetExportedTypes(),
        exported => exported.FullName == TargetTypeName);
}

[Fact]
public void CurrentBaseline_ContainsFourteenAssemblies507TypesAnd103Candidates()
{
    using var document = ReadJson(
        "docs",
        "governance",
        "bukit-core-public-api-baseline.v1.json");
    var root = document.RootElement;
    var types = root.GetProperty("types").EnumerateArray().ToArray();

    Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
    Assert.Equal(507, types.Length);
    Assert.Equal(103, types.Count(entry =>
        entry.GetProperty("compatibility").GetString() == "2.0-candidate"));
    Assert.DoesNotContain(types, entry =>
        entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
        entry.GetProperty("name").GetString() == TargetTypeName);
}
```

Keep the B1 zero-direct-reference, six-value vocabulary and immutable
closed-manifest facts unchanged.

- [ ] **Step 2: Run the visibility fact and observe the required RED**

Run:

```bash
dotnet test \
  tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  FullyQualifiedName~PluginHostAssembly_KeepsErrorCodeTypeInternalAndDoesNotExportIt
```

Expected: one failed test. The failure must be `Assert.False()` because the
type at the approved base is still public. A compilation error, missing
assembly, restore error, or different assertion is not valid TDD RED.

- [ ] **Step 3: Apply the one-token production change**

In `src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs`, change only:

```csharp
public static class PluginHostErrorCodes
```

to:

```csharp
internal static class PluginHostErrorCodes
```

Do not change the six field declarations.

- [ ] **Step 4: Re-run the visibility fact and observe GREEN**

Run the Step 2 command again.

Expected: `Passed: 1, Failed: 0`. Output must contain no compilation warning or
test failure.

- [ ] **Step 5: Run the governed-baseline fact and observe the second RED**

Run:

```bash
dotnet test \
  tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  FullyQualifiedName~CurrentBaseline_ContainsFourteenAssemblies507TypesAnd103Candidates
```

Expected: one failed test because the governed baseline still contains 508
types and the target record. The failure must come from current snapshot state,
not compilation.

- [ ] **Step 6: Generate and audit a temporary public API snapshot**

Use a new `/tmp` directory so the snapshot output does not already exist:

```bash
snapshot_root="$(mktemp -d "${TMPDIR%/}/bukit-g04d2b2-snapshot.XXXXXX")"
candidate="$snapshot_root/bukit-core-public-api-baseline.v1.json"

bash scripts/checks/public-api-drift.sh snapshot "$candidate" Release

jq '{
  assemblies: (.assemblies | length),
  types: (.types | length),
  candidates: ([.types[] |
    select(.compatibility == "2.0-candidate")] | length),
  target: [.types[] |
    select(.assembly == "Bukit.PluginHost" and
           .name == "Bukit.PluginHost.PluginHostErrorCodes")]
}' "$candidate"
```

Expected JSON:

```json
{
  "assemblies": 14,
  "types": 507,
  "candidates": 103,
  "target": []
}
```

Capture sorted type identities from the old and candidate snapshots:

```bash
jq -r '.types[] | [.assembly, .name] | @tsv' \
  docs/governance/bukit-core-public-api-baseline.v1.json |
  sort >"$snapshot_root/old-types.txt"

jq -r '.types[] | [.assembly, .name] | @tsv' "$candidate" |
  sort >"$snapshot_root/new-types.txt"

comm -23 "$snapshot_root/old-types.txt" "$snapshot_root/new-types.txt"
comm -13 "$snapshot_root/old-types.txt" "$snapshot_root/new-types.txt"
```

Expected first command output is exactly:

```text
Bukit.PluginHost	Bukit.PluginHost.PluginHostErrorCodes
```

Expected second command output is empty.

Before replacing the baseline, assert the old target has the exact six B1
members:

```bash
jq -e '
  [.types[] |
    select(.assembly == "Bukit.PluginHost" and
           .name == "Bukit.PluginHost.PluginHostErrorCodes") |
    .publicMembers] ==
  [[
    "public const System.String! ExecutionFailed = \"plugin.executionFailed\"",
    "public const System.String! InvalidResponse = \"plugin.invalidResponse\"",
    "public const System.String! OutputTooLarge = \"plugin.outputTooLarge\"",
    "public const System.String! PermissionDenied = \"plugin.permissionDenied\"",
    "public const System.String! Timeout = \"plugin.timeout\"",
    "public const System.String! UnsupportedProtocol = \"plugin.unsupportedProtocol\""
  ]]
' docs/governance/bukit-core-public-api-baseline.v1.json
```

Expected: exit 0 and output `true`.

Prove the entire generated document equals the old governed baseline with only
that one target type record removed:

```bash
jq -S '
  del(.types[] |
    select(.assembly == "Bukit.PluginHost" and
           .name == "Bukit.PluginHost.PluginHostErrorCodes"))
' docs/governance/bukit-core-public-api-baseline.v1.json \
  >"$snapshot_root/expected.json"

jq -S . "$candidate" >"$snapshot_root/actual.json"

cmp "$snapshot_root/expected.json" "$snapshot_root/actual.json"
```

Expected: `cmp` exits 0 with no output. This comparison binds the complete
assembly mapping and every retained type's owner, classification,
compatibility, migration horizon, signature, public members, protected members
and ordering. A type-identity-only comparison is insufficient.

Copy the generated candidate over the governed baseline as one mechanical
snapshot replacement. Do not hand-edit the generated JSON:

```bash
cp "$candidate" \
  docs/governance/bukit-core-public-api-baseline.v1.json
```

- [ ] **Step 7: Update only live current-state Architecture assertions**

Change 508 → 507 and 104 → 103 only in assertions/constants that explicitly
describe the live current baseline:

- `G04CPublicSurfacePilotTests.cs`
  - baseline counts;
  - `currentRemainder`;
  - `currentBaseline`.
- `G04D1AStaticNotionFacadeRemovalTests.cs`
  - baseline counts;
  - `currentBaseline`.
- `G04D1BBlockRendererFacadeRemovalTests.cs`
  - baseline counts.
- `G04D1CM2AtomicRemovalTests.cs`
  - `CurrentBaseline`;
  - baseline counts;
  - rename the current-baseline test to
    `CurrentBaseline_ContainsFourteenAssembliesFiveHundredSevenTypesAndOneHundredThreeCandidates`.
- `G04D2APluginSecretMaskerInternalizationTests.cs`
  - `CurrentBaseline`;
  - baseline counts;
  - rename the current-baseline test to
    `CurrentBaseline_ContainsFourteenAssemblies507TypesAnd103Candidates`.

Keep these historical decision strings unchanged:

```text
G-04D1C-M2 ... the other 105 candidates are not batch-approved.
G-04D2A ... the other 104 candidates are not batch-approved.
```

- [ ] **Step 8: Add the B2 governance fact**

Append this fact to
`G04D2B1PluginHostErrorCodeContractTests.cs` before `ReadJson`:

```csharp
[Fact]
public void ActiveGovernance_RecordsExactG04D2B2DecisionAndCurrentBaseline()
{
    var declaration = File.ReadAllText(Path.Combine(
        RepoRoot,
        "docs",
        "governance",
        "bukit-core-2.0-consumer-declaration.md"));
    var guide = File.ReadAllText(Path.Combine(
        RepoRoot,
        "guide",
        "dev",
        "public-api-governance.md"));
    var ledgerPath = Path.Combine(
        RepoRoot,
        "docs",
        "analysis",
        "bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md");

    Assert.Contains(Decision, declaration, StringComparison.Ordinal);
    Assert.Contains(Decision, guide, StringComparison.Ordinal);
    Assert.Contains(CurrentBaseline, declaration, StringComparison.Ordinal);
    Assert.Contains(CurrentBaseline, guide, StringComparison.Ordinal);
    Assert.True(File.Exists(ledgerPath), $"Missing G-04D2B2 decision ledger: {ledgerPath}");
}
```

- [ ] **Step 9: Synchronize active governance without rewriting history**

In both `docs/governance/bukit-core-2.0-consumer-declaration.md` and
`guide/dev/public-api-governance.md`:

1. Rephrase the G-04D2A line that currently says
   `The current public API baseline contains 508 types...` as its historical
   decision-time state:

```text
At the G-04D2A decision, the public API baseline contained 508 types,
including 104 `2.0-candidate` entries.
```

2. Add a G-04D2B2 section containing the exact decision:

```text
G-04D2B2 single-type internalization decision: only `Bukit.PluginHost.PluginHostErrorCodes` is narrowed from public to internal in 2.0; the other 103 candidates are not batch-approved.

The current public API baseline contains 507 types, including 103 `2.0-candidate` entries.
```

3. State that:
   - the closed 136-entry manifest and blob remain immutable;
   - private consumers remain `unknown-until-voluntary-declaration`;
   - the 2026-07-22 authenticated public search found no public match;
   - no new governance-grade GitHub Code Search was available on 2026-07-23;
   - ordinary const-consuming binaries may retain inlined values, but source
     recompilation and public metadata/reflection consumers are breaking;
   - six vocabulary strings and five runtime Host behaviors are unchanged;
   - no other PluginHost candidate is approved.
4. Link the new B2 ledger with the same relative-link style as G-04D2A.

- [ ] **Step 10: Create the provisional Chinese decision ledger**

Create
`docs/analysis/bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md`
with these sections and facts:

```markdown
# Bukit Core G-04D2B2 `PluginHostErrorCodes` 单类型 internalization 决策账本

日期：2026-07-23

基线：`2.0@757fb14976ad7337edc2a6fbf925b986222dea6f`

状态：实施中；最终资格由本任务最新 handoff 决定

## 决策

只将 `Bukit.PluginHost.PluginHostErrorCodes` 的 containing type 从 public
收窄为 internal。六个 const 成员和值、五个 Host 实际诊断行为及
`plugin.permissionDenied` 保留词汇保持不变。

## 兼容边界

这是 2.0-only source/public-metadata/reflection breaking change。普通已编译
const consumer 可能继续使用内联字符串，但这不构成全面 binary compatibility
承诺。私有消费者继续为 `unknown-until-voluntary-declaration`。

## Governed delta

目标是 14 assemblies / 507 types / 103 candidates。closed 136-entry manifest
必须保持 blob `7b07d6890562387010b52301e9f8716e9bf10ed1`。

## 搜索证据限制

2026-07-22 认证公开搜索未发现目标匹配；2026-07-23 环境没有可校准的治理级
GitHub Code Search，因此没有把本轮连接器结果写成新的认证快照。

## 验证状态

RED、GREEN、focused、Native AOT、release smoke、published CLI process-plugin
proof、唯一 aggregate 和独立复审的最终状态，必须以本任务最新 handoff 的实测结果
为准；本文不提前宣称通过。

## 排除项

不修改 schema、插件协议、配置语义、CLI 行为、错误字符串、权限语义、
`PluginProtocolClient`、其他 PluginHost 类型、CI/release/gate 或 protected
reference areas。
```

Task 2 will append observed commands, counts and final qualification status;
Task 1 must not pre-claim them.

- [ ] **Step 11: Verify targeted GREEN and owner contracts**

Run the B2 Architecture class:

```bash
dotnet test \
  tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  FullyQualifiedName~G04D2B1PluginHostErrorCodeContractTests
```

Expected: all facts in that class pass.

Run the selected current-state and plugin-boundary tests:

```bash
dotnet test \
  tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  "FullyQualifiedName~G04D2B1PluginHostErrorCodeContractTests|FullyQualifiedName~G04D2APluginSecretMaskerInternalizationTests|FullyQualifiedName~G04D1CM2AtomicRemovalTests|FullyQualifiedName~G04D1BBlockRendererFacadeRemovalTests|FullyQualifiedName~G04D1AStaticNotionFacadeRemovalTests|FullyQualifiedName~G04CPublicSurfacePilotTests|FullyQualifiedName~PluginBoundaryTests"
```

Expected: exit 0, no failed tests.

Run both complete affected test projects:

```bash
dotnet test \
  tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off

dotnet test \
  tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off
```

Expected:

- PluginHost: 170 passed, 0 failed;
- Architecture: 132 passed, 0 failed.

If the Architecture total differs only because another already-merged test
changed the count, record the actual clean total and inspect the test list
before proceeding; do not rewrite assertions to match blindly.

- [ ] **Step 12: Verify public API, docs and immutable manifest**

Run:

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
git hash-object \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
bash scripts/checks/docs/active-links.sh
bash scripts/checks/docs/no-absolute-paths.sh
git diff --check
```

Expected:

- public API self-test and real check exit 0;
- closed manifest hash is exactly
  `7b07d6890562387010b52301e9f8716e9bf10ed1`;
- both docs checks and `git diff --check` exit 0.

- [ ] **Step 13: Run the one code-subtask focused check**

Run exactly once:

```bash
bash scripts/checks/post-change-focused.sh -- \
  docs/superpowers/specs/2026-07-23-bukit-core-g04d2b2-plugin-host-error-codes-internalization-design.md \
  docs/superpowers/plans/2026-07-23-bukit-core-g04d2b2-plugin-host-error-codes-internalization.md \
  src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs \
  tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D2APluginSecretMaskerInternalizationTests.cs \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md
```

Expected: exit 0. Do not run the aggregate in this task.

- [ ] **Step 14: Self-review and commit Task 1**

Verify:

```bash
git diff -- \
  src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs
git diff --name-only \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
git status --short
```

Expected:

- production diff contains only `public` → `internal`;
- closed manifest command prints nothing;
- status contains only the approved Task 1 paths.

Commit:

```bash
git add \
  src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs \
  tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D2APluginSecretMaskerInternalizationTests.cs \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md
git commit -m "refactor(pluginhost): internalize error codes"
```

The implementation plan is committed by the controller before Task 1 starts,
so it is intentionally absent from this implementation commit while remaining
part of the final 13-path parent diff.

The controller must generate a task review package from the Task 1 starting
SHA to this commit, dispatch an independent read-only task reviewer, and close
all Critical/Important findings before Task 2.

---

### Task 2: Real Native AOT and published process-plugin qualification

**Files:**

- Modify:
  `docs/analysis/bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md`
- Controller acceptance correction only: Modify this implementation plan to
  replace two stale lexical assertions with canonical-path identity and the
  exact existing Echo line output.
- Verify only: `scripts/build/native-aot.sh`
- Verify only: `scripts/smoke/release-artifacts.sh`
- Verify only: `src/Bukit-Plugins/Bukit.Plugin.Echo/`

**Interfaces:**

- Consumes: Task 1 implementation and provisional ledger.
- Produces: real osx-arm64 archive/smoke/process-plugin evidence in `/tmp` and
  a committed final qualification ledger. It must not change production,
  tests, fixtures, scripts or governance snapshots.

- [ ] **Step 1: Establish clean proof roots and platform**

From the worktree root, run:

```bash
set -euo pipefail
test -f bukit-core.slnx
test "$(uname -s)" = "Darwin"
test "$(uname -m)" = "arm64"
test ! -e /tmp/bukit-g04d2b2-aot
test ! -e /tmp/bukit-g04d2b2-plugin-proof
mkdir -p /tmp/bukit-g04d2b2-aot
mkdir -p /tmp/bukit-g04d2b2-plugin-proof/site
```

Expected: exit 0. If either proof root already exists, stop and report the
collision; do not delete an unknown directory.

- [ ] **Step 2: Build the bounded Native AOT archive**

Run:

```bash
bash scripts/build/native-aot.sh \
  2.0.0-g04d2b2 \
  osx-arm64 \
  /tmp/bukit-g04d2b2-aot \
  Release
```

Resolve the physical proof root before checking owner output:

```bash
AOT_PROOF_ROOT="$(cd /tmp/bukit-g04d2b2-aot && pwd -P)"
```

Expected final stdout:

```text
${AOT_PROOF_ROOT}/bukit-2.0.0-g04d2b2-osx-arm64.tar.gz
```

Compare the observed final stdout to that expanded canonical path, then assert:

```bash
test -s \
  /tmp/bukit-g04d2b2-aot/bukit-2.0.0-g04d2b2-osx-arm64.tar.gz
test -x /tmp/bukit-g04d2b2-aot/publish/osx-arm64/bukit
```

- [ ] **Step 3: Run release-artifact smoke**

Run:

```bash
bash scripts/smoke/release-artifacts.sh \
  /tmp/bukit-g04d2b2-aot/bukit-2.0.0-g04d2b2-osx-arm64.tar.gz \
  osx-arm64
```

Expected: exit 0 with `Config check passed`, `Build completed:` and
`Publish audit: routes=2 errors=0`.

- [ ] **Step 4: Publish Echo into an isolated temporary site**

Copy the existing basic site fixture into the proof root:

```bash
cp tests/fixtures/basic-markdown-site/site.yaml \
  /tmp/bukit-g04d2b2-plugin-proof/site/site.yaml
cp -R tests/fixtures/basic-markdown-site/content \
  /tmp/bukit-g04d2b2-plugin-proof/site/
cp -R tests/fixtures/basic-markdown-site/layouts \
  /tmp/bukit-g04d2b2-plugin-proof/site/
mkdir -p \
  /tmp/bukit-g04d2b2-plugin-proof/site/plugins/echo/bin/osx-arm64
mkdir -p /tmp/bukit-g04d2b2-plugin-proof/site/.bukit
```

Publish the existing Echo project:

```bash
dotnet publish \
  src/Bukit-Plugins/Bukit.Plugin.Echo/Bukit.Plugin.Echo.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  --artifacts-path /tmp/bukit-g04d2b2-plugin-proof/echo-artifacts \
  -o /tmp/bukit-g04d2b2-plugin-proof/site/plugins/echo/bin/osx-arm64
```

Expected: exit 0 and executable:

```text
/tmp/bukit-g04d2b2-plugin-proof/site/plugins/echo/bin/osx-arm64/bukit-plugin-echo
```

Compute its SHA-256 with `shasum -a 256`; require exactly 64 lower-case hex
characters.

- [ ] **Step 5: Create the exact temporary plugin configs with apply_patch**

Use `apply_patch`, not `cat`, shell redirection or a repo change, to create:

`/tmp/bukit-g04d2b2-plugin-proof/site/plugins/echo/plugin.yaml`

```yaml
id: echo
name: Bukit Echo Plugin
version: 1.0.0
protocol: bukit-plugin-v1
kind: process
distribution: self-contained
platforms:
  osx-arm64:
    entry: bin/osx-arm64/bukit-plugin-echo
    sha256: <the exact 64-character value observed in Step 4>
commands:
  - name: echo
    description: Echo command arguments and context.
```

and `/tmp/bukit-g04d2b2-plugin-proof/site/.bukit/plugins.yaml`:

```yaml
version: 1
plugins:
  echo:
    enabled: true
    source: plugins/echo
    exposeCommands:
      - echo
    allowInCi: true
    permissions:
      network: false
      fileSystem:
        read: []
        write: []
      environment:
        read: []
```

The SHA value is runtime evidence, not a checked-in placeholder. The created
file must contain the actual observed value before any CLI invocation.

- [ ] **Step 6: Validate static config and manifest with the published CLI**

From `/tmp/bukit-g04d2b2-plugin-proof/site`, run:

```bash
/tmp/bukit-g04d2b2-aot/publish/osx-arm64/bukit \
  plugin validate-config

/tmp/bukit-g04d2b2-aot/publish/osx-arm64/bukit \
  plugin validate-manifest plugins/echo
```

Resolve the physical site root before checking owner output:

```bash
SITE_PROOF_ROOT="$(cd /tmp/bukit-g04d2b2-plugin-proof/site && pwd -P)"
```

Expected exact success lines after expanding `SITE_PROOF_ROOT`:

```text
Plugin config OK: ${SITE_PROOF_ROOT}/.bukit/plugins.yaml
Plugin manifest OK: ${SITE_PROOF_ROOT}/plugins/echo/plugin.yaml
```

Both commands must exit 0 without stderr.

- [ ] **Step 7: Prove handshake and runtime manifest through `plugin list`**

From the temporary site, run:

```bash
/tmp/bukit-g04d2b2-aot/publish/osx-arm64/bukit plugin list
```

Expected output contains:

```text
Plugins:
  echo@1.0.0 enabled=true status=ok platform=osx-arm64 commands=echo
```

Why this is binding evidence:

- `echo@1.0.0` is taken from the successful process handshake identity;
- `commands=echo` is taken from the successful runtime manifest and exposed
  command selection;
- `.bukit/plugins.lock.yaml` must exist and contain
  `protocol: bukit-plugin-v1`, `platform: osx-arm64`,
  `entry: plugins/echo/bin/osx-arm64/bukit-plugin-echo`, the exact SHA and
  `sha256Verified: true`.

The CLI does not expose raw handshake/runtime-manifest envelopes. Do not claim
that it does.

- [ ] **Step 8: Prove invoke and execution report with the published CLI**

From the temporary site, run:

```bash
/tmp/bukit-g04d2b2-aot/publish/osx-arm64/bukit echo hello
```

Expected exit 0, empty stderr, and stdout JSON satisfying:

```text
arguments == ["hello"]
options == {}
context.rootDir == "${SITE_PROOF_ROOT}"
context.workingDir == "${SITE_PROOF_ROOT}"
```

Use `jq -e` to validate stdout. Both context values must exactly equal the
expanded `SITE_PROOF_ROOT`; this verifies the same physical site identity even
when macOS reports `/private/tmp` for the `/tmp` alias. Do not accept a loose
`/tmp|/private/tmp` regular expression. Do not use Python.

Require exactly one
`.bukit/reports/plugin-executions/echo-invoke-*.json`. Validate it with
`jq -e`:

```text
pluginId == "echo"
pluginVersion == "1.0.0"
operation == "invoke"
protocol == "bukit-plugin-v1"
platform == "osx-arm64"
command == "echo"
commandPath == ["echo"]
entry == "plugins/echo/bin/osx-arm64/bukit-plugin-echo"
processExitCode == 0
responseExitCode == 0
sha256Verified == true
success == true
timedOut == false
outputLimitExceeded == false
stdoutBytes > 0
stderrBytes == 33
stderr == "bukit-plugin-echo handled invoke\n"
responseSummary.success == true
responseSummary.exitCode == 0
responseSummary.diagnosticCodes == []
responseSummary.artifactCount == 0
```

- [ ] **Step 9: Update the ledger with only observed evidence**

Append to the B2 ledger:

- Task 1 RED and GREEN commands with actual passed/failed counts;
- full PluginHost and Architecture totals;
- focused check result;
- public API 14/507/103 and exact one-type delta;
- closed manifest blob;
- exact Native AOT archive path and successful exit;
- release-artifact smoke observations;
- handshake/runtime-manifest inference boundary;
- published `echo hello` result and execution-report assertions;
- task review findings and resolutions;
- final state `qualification-complete` only if every required proof passed.

Controller-approved acceptance correction: the initial Task 2 brief expected
the lexical `/tmp` alias and omitted Echo's `WriteLine` line terminator. Preserve
the failed original assertions in the ledger, record the correction and its
independent read-only review, then rerun the corrected canonical-path and exact
33-byte stderr assertions. Do not modify the AOT scripts, Echo, process runner
or reporter to satisfy the stale text.

If a proof is blocked, write `qualification-blocked`, the command, exit code and
exact blocker. Do not mark the task complete.

- [ ] **Step 10: Verify docs and worktree cleanliness, then commit Task 2**

Run:

```bash
bash scripts/checks/docs/active-links.sh
bash scripts/checks/docs/no-absolute-paths.sh
git diff --check
git status --short
```

Expected: docs checks and diff check exit 0; only the B2 ledger and the
controller-approved acceptance correction in this plan are modified since the
Task 1 commit. `/tmp` proof artifacts must not appear in Git status.

Commit:

```bash
git add \
  docs/analysis/bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md \
  docs/superpowers/plans/2026-07-23-bukit-core-g04d2b2-plugin-host-error-codes-internalization.md
git commit -m "docs(architecture): qualify g04d2b2"
```

The controller must generate a Task 2 review package, dispatch an independent
read-only reviewer, and close all Critical/Important findings before freezing
the branch.

---

### Task 3: Freeze, single aggregate and final independent review

**Files:**

- Verify only: complete branch diff from
  `757fb14976ad7337edc2a6fbf925b986222dea6f`
- Modify: none

**Interfaces:**

- Consumes: reviewed Task 1 and Task 2 commits with all owner proofs passing.
- Produces: one aggregate result and an independent whole-branch merge
  readiness verdict. No file may change after this task begins.

- [ ] **Step 1: Freeze and audit the exact final path set**

Run:

```bash
git status --short
git diff --name-only \
  757fb14976ad7337edc2a6fbf925b986222dea6f..HEAD
git diff --check \
  757fb14976ad7337edc2a6fbf925b986222dea6f..HEAD
git hash-object \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Expected:

- worktree clean;
- path set is exactly the 13 paths listed in Step 2 below;
- diff check exits 0;
- manifest blob is
  `7b07d6890562387010b52301e9f8716e9bf10ed1`.

- [ ] **Step 2: Run the parent aggregate exactly once**

Run once and only once:

```bash
bash scripts/checks/post-change-targeted.sh \
  --base 757fb14976ad7337edc2a6fbf925b986222dea6f -- \
  docs/superpowers/specs/2026-07-23-bukit-core-g04d2b2-plugin-host-error-codes-internalization-design.md \
  docs/superpowers/plans/2026-07-23-bukit-core-g04d2b2-plugin-host-error-codes-internalization.md \
  src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs \
  tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D2APluginSecretMaskerInternalizationTests.cs \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md
```

Expected: exit 0.

If this command fails, record the exact failure and stop. Do not modify files,
do not rerun the aggregate, and do not claim G-04D2B2 complete. Any replacement
qualification requires a new explicit task.

- [ ] **Step 3: Dispatch the final whole-branch read-only review**

Generate a review package for:

```text
757fb14976ad7337edc2a6fbf925b986222dea6f..HEAD
```

The most capable reviewer must check:

- exact one-token production diff;
- six const members unchanged;
- B1 behavior/vocabulary contracts preserved;
- exact baseline delta and immutable closed manifest;
- historical/current governance wording;
- TDD evidence, focused result, AOT/smoke/process proof and unique aggregate;
- no friendship, replacement API, schema/protocol/CLI/gate drift;
- no protected reference area changes.

Expected verdict: Critical 0, Important 0, ready to merge.

If the reviewer finds a Critical or Important issue, stop. Since aggregate has
already run, do not modify and reuse the old result.

- [ ] **Step 4: Present the four branch integration options**

After fresh verification and a clean final review, use
`superpowers:finishing-a-development-branch` and present exactly:

```text
1. Merge back to 2.0 locally
2. Push and create a Pull Request
3. Keep the branch as-is
4. Discard this work
```

Do not merge, push, clean up or discard until the user selects an option.
