# Bukit 2.0 Public API Drift Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不接受非预期二进制破坏的前提下，收回意外公开类型、恢复六个 record 的旧 CLR 契约、建立真实的 2.x API 分类并审阅更新正式 baseline，使最终冻结提交的 public API drift 与唯一一次 `ci-fast Release` 门禁恢复绿色。

**Architecture:** 六个修复任务严格串行并遵守单写者规则。每个任务采用 RED→GREEN，只运行该任务列出的专项测试，不运行 `ci-fast`、release/full gate、whole-solution tests 或 Native AOT；全部修复任务完成后，最终任务一次性重跑所有任务专项测试、public API owner checks、一次真实 Native AOT 和一次 `ci-fast Release`。

**Tech Stack:** .NET 10、C# records、xUnit、System.Text.Json source generation、JSON Schema Draft 2020-12、Bukit.PublicApiDrift、Bash、Bukit codex-workflow。

## Global Constraints

- 实施前冻结一个发布候选 SHA；任务执行期间如 HEAD 被外部提交推进，立即停止并重新生成 verification closure。
- 六个修复任务严格串行；任一时刻只有一个 writer，任务必须从 `writing` 转到 `testing`、`review_wait`，最后才能转到 `done` 或 `blocked`。
- 每个任务只运行“Task test”列出的测试；不得在 Task 1–6 运行 `scripts/gates/ci-fast.sh`、`scripts/test-all.sh`、`scripts/smoke-all.sh`、whole-solution tests、release gate 或 Native AOT。
- Task 1–4 不得修改正式 baseline；Task 5 是唯一允许修改 `docs/governance/bukit-core-public-api-baseline.v1.json` 的任务。
- 不直接用未审阅 candidate 覆盖 baseline；baseline 不得含 `unresolved-owner-review`、`review-required` 或未批准 metadata。
- 不修改 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/`。
- 每个任务完成一次专项复审；仅 Critical/Important finding 允许回到实现并进行一次 scoped re-review。
- 每个任务独立提交，不 push、不创建 PR；最终验证必须基于最后一个修复提交的完整 SHA，旧 SHA 结果不可复用。

## Execution Preflight

- [ ] 记录 `git rev-parse HEAD` 和 `git status --short`；工作树不干净时停止，不覆盖用户改动。
- [ ] 将本计划 Task 1–6 的全部 changed files 逐个作为 `--changed` 传给：

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed docs/superpowers/plans/2026-08-06-bukit-public-api-drift-remediation.md \
  --changed src/Bukit-Core/Bukit.Engine.Abstractions/ContentDocumentFactory.cs \
  --changed tests/Bukit.Engine.Abstractions.Tests/ContentDocumentFactoryTests.cs \
  --changed src/Bukit-Core/Bukit.Plugin.Abstractions/Manifest/PluginManifest.cs \
  --changed tests/Bukit.Plugin.Abstractions.Tests/PluginManifestBinaryCompatibilityTests.cs \
  --changed src/Bukit-Core/Bukit.PluginHost/PluginProcessRequest.cs \
  --changed src/Bukit-Core/Bukit.PluginHost/PluginProcessResult.cs \
  --changed src/Bukit-Core/Bukit.PluginHost/ProcessRunRequest.cs \
  --changed src/Bukit-Core/Bukit.PluginHost/ProcessRunResult.cs \
  --changed src/Bukit-Core/Bukit.PluginHost/ResolvedPlugin.cs \
  --changed tests/Bukit.PluginHost.Tests/PluginHostRecordBinaryCompatibilityTests.cs \
  --changed tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs \
  --changed scripts/checks/public-api-drift-self-test.sh \
  --changed docs/schemas/bukit-core-public-api-baseline.v1.schema.json \
  --changed guide/dev/public-api-governance.md \
  --changed docs/governance/bukit-core-public-api-baseline.v1.json \
  --changed scripts/checks/post-change-focused-owner-checks.sh \
  --changed scripts/checks/post-change-focused-owner-checks-self-test.sh
```

- [ ] 处理 closure 返回的每一个 `unmappedFiles`，并将实际 `specialtyTests` 与本计划对照；不允许静默删除 owner test。
- [ ] 初始化唯一 queue state，并按 Task 1→6 顺序 acquire；state 文件放在 `/tmp`，不得提交进仓库。

---

### Task 1: Internalize `ContentDocumentFactory`

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine.Abstractions/ContentDocumentFactory.cs`
- Modify/Test: `tests/Bukit.Engine.Abstractions.Tests/ContentDocumentFactoryTests.cs`

**Interfaces:**
- Consumes: `IContentBodyStore` 在同程序集调用 `MergeFields` 和 `CreateDocument`。
- Produces: `internal static class ContentDocumentFactory`；行为和四个方法签名保持不变，但不再出现在导出 CLR surface。

- [ ] **Step 1: Write the failing visibility test**

```csharp
[Fact]
public void Type_IsInternalImplementationDetail()
{
    var type = typeof(IContentBodyStore).Assembly.GetType(
        "Bukit.Engine.Abstractions.Content.ContentDocumentFactory",
        throwOnError: true)!;

    Assert.False(type.IsPublic);
    Assert.True(type.IsNotPublic);
}
```

- [ ] **Step 2: Run RED task test**

```bash
dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj \
  --filter 'FullyQualifiedName~ContentDocumentFactoryTests'
```

Expected: visibility assertion fails because the type is currently public.

- [ ] **Step 3: Make the minimal implementation**

Change only `public static class ContentDocumentFactory` to `internal static class ContentDocumentFactory`. Do not rename methods or move logic back into the interface.

- [ ] **Step 4: Run GREEN task test and review**

Run the same filtered command. Confirm all factory behavior tests and visibility assertion pass; search production consumers and confirm every remaining caller is inside `Bukit.Engine.Abstractions`.

- [ ] **Step 5: Commit**

```bash
git add src/Bukit-Core/Bukit.Engine.Abstractions/ContentDocumentFactory.cs \
  tests/Bukit.Engine.Abstractions.Tests/ContentDocumentFactoryTests.cs
git commit -m "fix(core): internalize content document factory"
```

**Task test:** only the filtered `ContentDocumentFactoryTests` command above. No gate.

---

### Task 2: Restore `PluginManifest` binary compatibility

**Files:**
- Modify: `src/Bukit-Core/Bukit.Plugin.Abstractions/Manifest/PluginManifest.cs`
- Modify direct constructor consumers that pass `ManifestVersion`: `src/Bukit-Core/Bukit.PluginHost/PluginManifestLoader.cs`
- Modify: `tests/Bukit.Plugin.Abstractions.Tests/PluginDtoSerializationTests.cs`
- Create/Test: `tests/Bukit.Plugin.Abstractions.Tests/PluginManifestBinaryCompatibilityTests.cs`

**Interfaces:**
- Produces: the old nine-value constructor and nine-value `Deconstruct`; additive `public int ManifestVersion { get; init; } = 1`.

- [ ] **Step 1: Add a compile-time compatibility test**

```csharp
[Fact]
public void LegacyConstructorAndDeconstruct_KeepNineValues()
{
    var manifest = new PluginManifest(
        "example", "Example", "1.0.0", "bukit-plugin-v1", "process",
        "self-contained", null, null, null);

    var (id, name, version, protocol, kind, distribution,
        platforms, commands, permissions) = manifest;

    Assert.Equal("example", id);
    Assert.Equal("Example", name);
    Assert.Equal("1.0.0", version);
    Assert.Equal("bukit-plugin-v1", protocol);
    Assert.Equal("process", kind);
    Assert.Equal("self-contained", distribution);
    Assert.Empty(platforms);
    Assert.Empty(commands);
    Assert.NotNull(permissions);
    Assert.Equal(1, manifest.ManifestVersion);
    Assert.Equal(2, (manifest with { ManifestVersion = 2 }).ManifestVersion);
}
```
- [ ] **Step 2: Run RED task test**

```bash
dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj \
  --filter 'FullyQualifiedName~PluginManifestBinaryCompatibilityTests'
```

Expected: compilation fails because current generated `Deconstruct` has ten outputs.

- [ ] **Step 3: Restore the primary record shape**

Remove `ManifestVersion` from the positional parameter list and add this member inside the record body:

```csharp
public int ManifestVersion { get; init; } = 1;
```

Change every intentional non-default construction to an object initializer: `new PluginManifest(/* old arguments */) { ManifestVersion = manifestVersion }`.

- [ ] **Step 4: Run GREEN task test** using the same filter. Then run the existing manifest JSON class only:

```bash
dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj \
  --filter 'FullyQualifiedName~PluginDtoSerializationTests'
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  --filter 'FullyQualifiedName~PluginProtocolCompatibilityTests|FullyQualifiedName~PluginManifestLoaderTests'
```

- [ ] **Step 5: Commit** with message `fix(plugin): preserve manifest record binary contract`.

**Task tests:** the two filtered Plugin.Abstractions commands and the filtered PluginHost consumer command above. No gate.

---

### Task 3: Restore five PluginHost record contracts

**Files:**
- Modify: `src/Bukit-Core/Bukit.PluginHost/PluginProcessRequest.cs`
- Modify: `src/Bukit-Core/Bukit.PluginHost/PluginProcessResult.cs`
- Modify: `src/Bukit-Core/Bukit.PluginHost/ProcessRunRequest.cs`
- Modify: `src/Bukit-Core/Bukit.PluginHost/ProcessRunResult.cs`
- Modify: `src/Bukit-Core/Bukit.PluginHost/ResolvedPlugin.cs`
- Modify direct consumers: `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs`, `PluginProcessInvoker.cs`, `SystemProcessRunner.cs`, and `src/Bukit-Core/Bukit.Cli/Cli/PluginCliLoader.cs`
- Modify existing affected PluginHost/CLI tests that pass the new values positionally or by named constructor arguments.
- Create/Test: `tests/Bukit.PluginHost.Tests/PluginHostRecordBinaryCompatibilityTests.cs`

**Interfaces:**
- `PluginProcessRequest` and `ProcessRunRequest`: preserve old eight-value constructor/deconstruction; add `MaxCpuTime` and `MaxMemoryBytes` as nullable init properties.
- `PluginProcessResult` and `ProcessRunResult`: preserve old six-value constructor/deconstruction; add `ResourceLimitExceeded` as a nullable init property.
- `ResolvedPlugin`: preserve old thirteen-value constructor/deconstruction; add `Resources` as a nullable init property.

- [ ] **Step 1: Add five RED compile-time tests.** Use the exact old constructor and deconstruction arities below; each object also proves the new property is assignable without changing the positional contract.

```csharp
[Fact]
public void PluginProcessRequest_KeepsEightValues()
{
    var value = new PluginProcessRequest("tool", null, "{}", "/tmp",
        TimeSpan.FromSeconds(1), 10, 20, null)
    { MaxCpuTime = TimeSpan.FromMilliseconds(50), MaxMemoryBytes = 100 };
    var (path, arguments, input, directory, timeout, stdout, stderr, environment) = value;
    Assert.Equal("tool", path);
    Assert.Empty(arguments);
    Assert.Equal("{}", input);
    Assert.Equal("/tmp", directory);
    Assert.Equal(TimeSpan.FromSeconds(1), timeout);
    Assert.Equal(10, stdout);
    Assert.Equal(20, stderr);
    Assert.Empty(environment);
    Assert.Equal(100, value.MaxMemoryBytes);
}

[Fact]
public void PluginProcessResult_KeepsSixValues()
{
    var value = new PluginProcessResult(0, "{}", "", false, false, null)
    { ResourceLimitExceeded = "cpu" };
    var (exitCode, stdout, stderr, timedOut, outputExceeded, stream) = value;
    Assert.Equal(0, exitCode);
    Assert.Equal("{}", stdout);
    Assert.Equal("", stderr);
    Assert.False(timedOut);
    Assert.False(outputExceeded);
    Assert.Null(stream);
    Assert.Equal("cpu", value.ResourceLimitExceeded);
}

[Fact]
public void ProcessRunRequest_KeepsEightValues()
{
    var value = new ProcessRunRequest("tool", null, "input", "/tmp",
        TimeSpan.FromSeconds(1), 10, 20, null)
    { MaxCpuTime = TimeSpan.FromMilliseconds(50), MaxMemoryBytes = 100 };
    var (path, arguments, input, directory, timeout, stdout, stderr, environment) = value;
    Assert.Equal("tool", path);
    Assert.Empty(arguments);
    Assert.Equal("input", input);
    Assert.Equal("/tmp", directory);
    Assert.Equal(TimeSpan.FromSeconds(1), timeout);
    Assert.Equal(10, stdout);
    Assert.Equal(20, stderr);
    Assert.Empty(environment);
    Assert.Equal(TimeSpan.FromMilliseconds(50), value.MaxCpuTime);
}

[Fact]
public void ProcessRunResult_KeepsSixValues()
{
    var value = new ProcessRunResult(0, "ok", "", false, false, null)
    { ResourceLimitExceeded = "memory" };
    var (exitCode, stdout, stderr, timedOut, outputExceeded, stream) = value;
    Assert.Equal(0, exitCode);
    Assert.Equal("ok", stdout);
    Assert.Equal("", stderr);
    Assert.False(timedOut);
    Assert.False(outputExceeded);
    Assert.Null(stream);
    Assert.Equal("memory", value.ResourceLimitExceeded);
}

[Fact]
public void ResolvedPlugin_KeepsThirteenValues()
{
    var host = new PluginHostInfo("Bukit", "2.0.0", "osx-arm64");
    var value = new ResolvedPlugin("id", "1", "osx-arm64", "tool", "/tmp",
        host, null, null, null, null, null, null, null)
    { Resources = new PluginResourceLimitOptions(50, 100) };
    var (id, version, platform, executable, workingDirectory, resolvedHost,
        projectRoot, arguments, timeout, output, permissions, environment,
        sha256Verified) = value;
    Assert.Equal("id", id);
    Assert.Equal("1", version);
    Assert.Equal("osx-arm64", platform);
    Assert.Equal("tool", executable);
    Assert.Equal("/tmp", workingDirectory);
    Assert.Same(host, resolvedHost);
    Assert.Null(projectRoot);
    Assert.Empty(arguments);
    Assert.NotNull(timeout);
    Assert.NotNull(output);
    Assert.NotNull(permissions);
    Assert.Empty(environment);
    Assert.Null(sha256Verified);
    Assert.Equal(100, value.Resources!.MaxMemoryBytes);
}
```
- [ ] **Step 2: Run RED task test**

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  --filter 'FullyQualifiedName~PluginHostRecordBinaryCompatibilityTests'
```

Expected: compilation fails on the current expanded `Deconstruct` signatures.

- [ ] **Step 3: Move new positional parameters to properties**

```csharp
public TimeSpan? MaxCpuTime { get; init; }
public long? MaxMemoryBytes { get; init; }
public string? ResourceLimitExceeded { get; init; }
public PluginResourceLimitOptions? Resources { get; init; }
```

Place only the applicable properties on each record. Update all internal callers to object initializers; do not add compatibility constructors with duplicate optional overloads because they can create overload ambiguity.

- [ ] **Step 4: Run GREEN compatibility test**, followed by existing behavior classes only:

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  --filter 'FullyQualifiedName~PluginProtocolCompatibilityTests|FullyQualifiedName~PluginProtocolClientTests|FullyQualifiedName~PluginProcessInvokerTests|FullyQualifiedName~SystemProcessRunnerTests'
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj \
  --filter 'FullyQualifiedName~PluginCliIntegrationTests'
```

- [ ] **Step 5: Commit** with message `fix(plugin): preserve process record binary contracts`.

**Task tests:** the two filtered PluginHost commands and the filtered CLI consumer command above. No project-wide test and no gate.

---

### Task 4: Add truthful 2.x API policy vocabulary

**Files:**
- Modify: `tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs`
- Modify/Test: `scripts/checks/public-api-drift-self-test.sh`
- Modify: `docs/schemas/bukit-core-public-api-baseline.v1.schema.json`
- Modify: `guide/dev/public-api-governance.md`

**Interfaces:**
- Produces approved compatibility values `2.x-do-not-narrow`, `2.x-migration-safe`, and `2.x-shape-stable`.
- Produces documented horizons `retain-2.x` and `3.0-review`; horizon remains a required non-empty string.

- [ ] **Step 1: Add RED fixture coverage.** Derive canonical scratch fixtures from the existing fixture so no permanent duplicate baseline is introduced:

```bash
for compatibility in 2.x-do-not-narrow 2.x-migration-safe 2.x-shape-stable; do
  output="$scratch/${compatibility}.json"
  sed "s/\"compatibility\": \"2.0-candidate\"/\"compatibility\": \"${compatibility}\"/" \
    "$fixtures/baseline.json" >"$output"
  assert_exit 0 "$scratch/${compatibility}.txt" \
    "${tool[@]}" "$output" "$output"
done
sed 's/"compatibility": "2.0-candidate"/"compatibility": "2.x-unknown"/' \
  "$fixtures/baseline.json" >"$scratch/compatibility-unknown.json"
assert_exit 2 "$scratch/compatibility-unknown.txt" \
  "${tool[@]}" "$scratch/compatibility-unknown.json" "$fixtures/unchanged.json"
grep -Fq 'gate-error:' "$scratch/compatibility-unknown.txt" || \
  fail "unknown 2.x compatibility lacks gate-error"
```
- [ ] **Step 2: Run RED task test**

```bash
bash scripts/checks/public-api-drift-self-test.sh
```

- [ ] **Step 3: Implement policy and documentation.** Add the same three values to `ApiPolicy.Compatibility` and the JSON Schema enum. Add exact review-policy rows to the guide and document `retain-2.x`/`3.0-review`; do not rewrite historical 1.x baseline entries.
- [ ] **Step 4: Run GREEN task test** using the same self-test command and inspect the fixture/schema/guide diff for exact vocabulary agreement.
- [ ] **Step 5: Commit** with message `fix(governance): define 2.x public API stability policy`.

**Task test:** only `public-api-drift-self-test.sh`. It is the direct owner self-test, not the real Core drift gate.

---

### Task 5: Review and replace the governed API baseline

**Files:**
- Modify: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Modify only if the review decision text needs synchronization: `guide/dev/public-api-governance.md`

**Interfaces:**
- Adds `Bukit.Config.CollectionIndexPolicyConfig` as `Configuration / serialized-contract / 2.x-shape-stable / retain-2.x`.
- Adds `Bukit.Plugin.Abstractions.Config.PluginResourceLimitOptions` as `External plugin protocol / serialized-contract / 2.x-shape-stable / retain-2.x`.
- Does not add `ContentDocumentFactory`.
- Retains existing metadata for the eight approved additive surfaces: `CollectionConfig`, `BodyCacheDecorator`, `NotionClient`, `NotionClientOptions`, `PluginConfigEntry`, `PluginJsonSerializerContext`, `GeoCitationModel`, and `SsrfGuard`.

- [ ] **Step 1: Generate a new candidate outside the repository**

```bash
candidate_dir="$(mktemp -d /tmp/bukit-public-api-reviewed.XXXXXX)"
TMPDIR=/tmp bash scripts/checks/public-api-drift.sh snapshot "$candidate_dir/candidate.json" Release
```

This is a focused snapshot/build, not a gate and not a test suite.

- [ ] **Step 2: Review the candidate.** Capture the expected drift and prove the breaking category is empty:

```bash
status=0
dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj \
  -c Release --no-build --no-restore -- \
  compare docs/governance/bukit-core-public-api-baseline.v1.json \
  "$candidate_dir/candidate.json" 2>"$candidate_dir/drift.txt" || status=$?
test "$status" -eq 1
test "$(grep -Fc 'exported type added' "$candidate_dir/drift.txt")" -eq 2
if grep -Fq 'breaking:' "$candidate_dir/drift.txt"; then exit 1; fi
if grep -Fq 'ContentDocumentFactory' "$candidate_dir/candidate.json"; then exit 1; fi
```

Require exactly two added types, zero removed types, zero removed public members, and no `ContentDocumentFactory`. Stop if any other breaking change remains.
- [ ] **Step 3: Assign the two exact metadata tuples above, retain all approved captured signatures/members, preserve canonical ordering/UTF-8/no-BOM formatting, and replace only the governed baseline.** Save the reviewed form as `$candidate_dir/reviewed.json` before replacing the governed file so the review artifact is not confused with the unresolved raw candidate.
- [ ] **Step 4: Prove the accepted additive surfaces with their owner tests**

```bash
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj \
  --filter 'FullyQualifiedName~EmptyCollectionSeoConfigTests'
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj \
  --filter 'FullyQualifiedName~BodyCacheDecoratorTests'
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj \
  --filter 'FullyQualifiedName~NotionClientTests'
dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj \
  --filter 'FullyQualifiedName~PluginConfigDtoTests|FullyQualifiedName~PluginDtoSerializationTests'
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter 'FullyQualifiedName~GeoSeoModelBuilderTests'
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj \
  --filter 'FullyQualifiedName~SecurityFuzzingTests'
```

- [ ] **Step 5: Generate a fresh post-acceptance candidate and compare without invoking the real gate wrapper**

```bash
TMPDIR=/tmp bash scripts/checks/public-api-drift.sh snapshot \
  "$candidate_dir/post-acceptance.json" Release

dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj \
  -c Release --no-build --no-restore -- \
  compare docs/governance/bukit-core-public-api-baseline.v1.json \
  "$candidate_dir/post-acceptance.json"
```

Expected: exit `0`, no diagnostics. Also assert the governed baseline contains neither `unresolved-owner-review` nor `review-required`.

```bash
if grep -Eq '"(owner|classification|compatibility|migrationHorizon)": "(unresolved-owner-review|review-required)"' \
  docs/governance/bukit-core-public-api-baseline.v1.json; then
  exit 1
fi
```

- [ ] **Step 6: Commit** with message `fix(governance): accept reviewed 2.0 public API surface`.

**Task tests:** the six filtered owner-test commands, post-acceptance candidate comparison, and unresolved-metadata scan only. Do not run `public-api-drift.sh check` or `ci-fast` here.

---

### Task 6: Route Core API-affecting changes to the focused owner check

**Files:**
- Modify: `scripts/checks/post-change-focused-owner-checks.sh`
- Modify/Test: `scripts/checks/post-change-focused-owner-checks-self-test.sh`
- Modify: `guide/dev/public-api-governance.md`

**Interfaces:**
- Produces a deduplicated `public-api-drift` owner check for governed Core C# source changes.
- Dry-run prints `bash scripts/checks/public-api-drift.sh check Release`; ordinary execution runs that command only when the focused workflow is explicitly invoked.

- [ ] **Step 1: Add RED dry-run assertions.** Use one Config public source and one PluginHost public source, require exactly one public API check, and prove unrelated docs-only input does not schedule it.
- [ ] **Step 2: Run RED task test**

```bash
bash scripts/checks/post-change-focused-owner-checks-self-test.sh
```

- [ ] **Step 3: Add the owner mapping and execution branch.** Deduplicate multiple Core paths through the existing `add_owner_check` mechanism:

```bash
src/Bukit-Core/*.cs)
  add_owner_check public-api-drift ;;
```

and in the execution dispatch:

```bash
public-api-drift)
  run_or_print "public API drift" \
    bash scripts/checks/public-api-drift.sh check Release ;;
```

Update the guide to state that focused routing detects drift early but does not authorize baseline replacement.
- [ ] **Step 4: Run GREEN task test** using the same self-test command. Do not invoke the real mapped check during this task.
- [ ] **Step 5: Commit** with message `fix(workflow): route Core changes to public API review`.

**Task test:** only the owner-checks self-test. No gate.

---

### Task 7: One final combined verification and one gate

**Files:**
- Verify only; no implementation edits are allowed after the final verification starts.

- [ ] **Step 1: Freeze final SHA and generate `review-scope`.** Include all Task 1–6 commits and reuse no evidence whose HEAD, closure, command, environment state, or SDK differs.
- [ ] **Step 2: Re-run every repair-task test once, in resource-safe order**

```bash
dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj \
  --filter 'FullyQualifiedName~ContentDocumentFactoryTests'
dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj \
  --filter 'FullyQualifiedName~PluginManifestBinaryCompatibilityTests|FullyQualifiedName~PluginDtoSerializationTests|FullyQualifiedName~PluginConfigDtoTests'
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  --filter 'FullyQualifiedName~PluginHostRecordBinaryCompatibilityTests|FullyQualifiedName~PluginProtocolCompatibilityTests|FullyQualifiedName~PluginManifestLoaderTests|FullyQualifiedName~PluginProtocolClientTests|FullyQualifiedName~PluginProcessInvokerTests|FullyQualifiedName~SystemProcessRunnerTests'
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj \
  --filter 'FullyQualifiedName~PluginCliIntegrationTests'
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj \
  --filter 'FullyQualifiedName~EmptyCollectionSeoConfigTests'
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj \
  --filter 'FullyQualifiedName~BodyCacheDecoratorTests'
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj \
  --filter 'FullyQualifiedName~NotionClientTests'
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter 'FullyQualifiedName~GeoSeoModelBuilderTests'
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj \
  --filter 'FullyQualifiedName~SecurityFuzzingTests'
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/post-change-focused-owner-checks-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
```

- [ ] **Step 3: Run one fresh Native AOT proof**

```bash
aot_root="$(mktemp -d /tmp/bukit-public-api-aot.XXXXXX)"
bash scripts/build/native-aot.sh 2.0.0 osx-arm64 "$aot_root" Release
```

Require a non-empty archive and successful exit. This proof is mandatory because `PluginJsonSerializerContext` and serialized plugin configuration changed.

- [ ] **Step 4: Run the only aggregate gate in the entire plan**

```bash
bash scripts/gates/ci-fast.sh Release
```

Do not rerun it just to refresh evidence. If it fails, diagnose the exact failed owner and return only to the owning task; after a repair commit, invalidate final evidence and restart Task 7 from Step 1.

- [ ] **Step 5: Run final hygiene and unified review**

```bash
git diff --check
git status --short
```

Require no Critical/Important findings, no uncommitted changes, public API drift exit `0`, Native AOT success, and `ci-fast Release` success all at the same final SHA.

## Final Acceptance Matrix

| Contract | Required result |
|---|---|
| Exported types | exactly the reviewed surface; `ContentDocumentFactory` absent |
| Binary compatibility | six old constructors and six old `Deconstruct` signatures preserved |
| New contracts | two new serialized types carry approved 2.x metadata |
| Baseline hygiene | no unresolved/review-required metadata and canonical JSON |
| AOT | fresh `osx-arm64` Native AOT archive succeeds |
| Task evidence | every Task 1–6 specialty test rerun at final SHA |
| Aggregate gate | exactly one `bash scripts/gates/ci-fast.sh Release`, green at final SHA |
| Publication | no push, PR, tag, package publication, or deployment is authorized by this plan |
