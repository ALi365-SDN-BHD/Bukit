# Bukit Core Whole Re-review Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 闭合全量复审确认的 13 项 Important、8 项 Conditional Important 和 9 项 Minor，使 Bukit Core 在确定性、正文所有权、图片与进程安全、Notion、文件系统边界和 Native AOT 上达到可验证的 CLOSED 状态。

**Architecture:** 按已批准规格执行 Batch 0–7 串行闭包；每个实现 batch 内按根因建立独立 RED/GREEN checkpoint，但只在 batch 结束时运行一次完整 owner specialty、fresh Native AOT 和专项复审。生产修改仅位于 `src/Bukit-Core/`；测试、probe 与消费者项目只用于证明 Core 合同。

**Tech Stack:** .NET 10、C#、xUnit、YamlDotNet、ImageSharp 3.1.12、System.Text.Json source generation、Windows Job Object、POSIX process groups/no-follow file handles、Bukit codex-workflow、Native AOT (`osx-arm64`)。

## Global Constraints

- 设计规格：`docs/superpowers/specs/2026-08-04-bukit-core-whole-rereview-remediation-design.md`。
- 生产代码只修改 `src/Bukit-Core/`；不得修改 `src/Bukit-Labs/`、`src/Bukit-Plugins/` 或第三方插件生产代码。
- 不修改 CI、release、gate、workflow policy、public release 文档、`guide-0.1/`、`guide-0.2/`、`scripts-0.1/` 或 `scripts-0.2/`。
- 测试只修改本计划明确列出的 `tests/Bukit.*.Tests/` 与受控 process probe；不得以 Labs/Plugin consumer 生产改动修复 Core。
- 不运行 whole-solution、`scripts/test-all.sh`、`scripts/smoke-all.sh`、full/release gate、`post-change-*` 或未列名矩阵。
- 任一时刻只有一个 writer；queue task 从 `writing` 到 `testing`、`review_wait`、`done|blocked` 全程持有 writer slot。
- 每批写代码前执行 `closure` 和 `classify`；`unmapped` 必须为零。fixture-exclusive、Bukit locks、plugin locks、manifest、cache、AOT publish 串行执行。
- 每个 finding 先 RED。编译错误、夹具错误、平台权限错误或不相关异常不是有效 RED。
- Conditional Important 必须先得到可重复 RED；当前平台无法构造时标记 `unverified` 并停止该 finding，不允许无证据扩展修复。
- 每批完整 owner tests 和 Native AOT 后只做一次专项复审；只有 Critical/Important 可回到 implementation 并进行 scoped re-review。
- 每批 AOT 必须 publish 成功，且 native `bukit version` 与 `bukit --help` 均 exit 0。
- 不 push、不部署、不发布包。本地 merge 需要用户单独授权。

## Shared Execution State

```bash
queue_state=/tmp/bukit-core-whole-remediation-queue.json
metrics_state=/tmp/bukit-core-whole-remediation-metrics.json
findings_state=/tmp/codex-reports/bukit-core-whole-remediation-findings.json
mkdir -p /tmp/codex-reports
```

每个 batch 固定使用：

```text
/tmp/codex-reports/bukit-core-whole-remediation-batch-N-closure.json
/tmp/codex-reports/bukit-core-whole-remediation-batch-N-evidence.json
/tmp/codex-reports/bukit-core-whole-remediation-batch-N-review.md
```

Native AOT 固定命令：

```bash
aot_dir="$(mktemp -d /tmp/bukit-core-whole-remediation-aot.XXXXXX)"
dotnet publish src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj \
  -c Release -r osx-arm64 --self-contained true \
  -p:PublishAot=true -o "$aot_dir/publish"
"$aot_dir/publish/bukit" version
"$aot_dir/publish/bukit" --help
```

每个实现 batch 的 queue 与 metrics 固定协议如下；`batch_task`、`batch_phase` 和 `batch_label` 使用对应 Task 明确列出的值：

```bash
phase_started_seconds=$SECONDS
# execute the phase
phase_duration_ms="$(( (SECONDS - phase_started_seconds) * 1000 ))"
python3 scripts/checks/codex-workflow.py metrics add \
  --state /tmp/bukit-core-whole-remediation-metrics.json \
  --task "$batch_task" --phase "$batch_phase" \
  --duration-ms "$phase_duration_ms" \
  --command-label "$batch_label" --status completed
```

生产实现完成后从 `writing` 转 `testing`；完整 owner tests 与 AOT 完成后转 `review_wait`；专项复审 C0/I0 后转 `done`。任何一步失败转 `blocked` 前必须满足仓库规定的重复阻塞阈值，普通 RED 或首次环境失败不得标 blocked。

```bash
python3 scripts/checks/codex-workflow.py queue transition \
  --state /tmp/bukit-core-whole-remediation-queue.json \
  --task "$batch_task" --to testing
python3 scripts/checks/codex-workflow.py queue transition \
  --state /tmp/bukit-core-whole-remediation-queue.json \
  --task "$batch_task" --to review_wait
python3 scripts/checks/codex-workflow.py queue transition \
  --state /tmp/bukit-core-whole-remediation-queue.json \
  --task "$batch_task" --to done
```

每批 closure 生成后使用同一 closure JSON 做资源分类；字段名固定为 `changedFiles` 和 `exactCommands`：

```bash
classify_args=(python3 scripts/checks/codex-workflow.py classify \
  --policy scripts/checks/codex-workflow-policy.v1.json)
while IFS= read -r changed_file; do
  classify_args+=(--path "$changed_file")
done < <(jq -r '.changedFiles[]' "$batch_closure")
while IFS= read -r exact_command; do
  classify_args+=(--test-command "$exact_command")
done < <(jq -r '.exactCommands[]' "$batch_closure")
"${classify_args[@]}"
```

每个完整 owner GREEN 和 AOT command 都写入当前 batch cache record。`--base` 使用 batch closure JSON，`--command` 必须与实际命令逐字一致，`--sdk-version` 使用 `dotnet --version`；`--env` 只传环境变量名称和是否设置的状态，不传值或秘密。cache check 只有命中才允许跳过未失效命令。

---

## Task 0: Normalize the Trusted Baseline

**Closes:** execution precondition only; no finding closes here.

**Files:**

- Read: all current dirty paths and prior `/tmp/codex-reports/bukit-rereview-closure-*` evidence
- Create outside repository: batch-0 closure/evidence/review files and findings ledger
- No production edits

**Interfaces:**

- Consumes: approved dirty working tree at `main@a1f57b24` plus prior remediation evidence
- Produces: clean, attributable local baseline; queue; metrics state; 30-record finding ledger

- [ ] **Step 1: Capture the live baseline**

```bash
git rev-parse HEAD
git status --short
git diff --check HEAD -- src/Bukit-Core tests scripts/checks
phase_started_seconds=$SECONDS
```

Expected: HEAD is at or after `a1f57b24`; diff check exits 0. Save HEAD, dirty paths and SDK information in batch-0 evidence without environment values or secrets.

- [ ] **Step 2: Attribute every existing dirty path**

Build a table with columns `path`, `priorTask`, `finding`, `evidence`, `action`. Every path must be one of:

```text
prior-approved-keep
new-plan-owner-batch-1..6
unrelated-preserve
unmapped-stop
```

Expected: no `unmapped-stop`. If any path cannot be attributed, stop before queue initialization and request direction.

Scope override: every existing production path under `src/Bukit-Labs/` or `src/Bukit-Plugins/` and every existing CI/release/gate/workflow-policy path is always `unrelated-preserve` for this plan, even if it came from an earlier approved task. Batch 0 must not stage it; integrating those paths requires their own task authority.

- [ ] **Step 3: Normalize prior approved changes into a baseline commit**

Stage only paths classified `prior-approved-keep`; verify the staged list against the table, then create one local checkpoint commit. Do not stage `unrelated-preserve`.

```bash
git diff --cached --name-status
git diff --cached --check
git commit -m "fix(core): checkpoint approved audit closures"
```

Expected: current checkout has no uncommitted plan-owned path. If an overlapping file contains unrelated hunks that cannot be separated safely, stop instead of using broad staging.

After the checkpoint commit, store its SHA as `baselineHead` in batch-0 evidence. Task 7 uses this exact SHA rather than assuming a commit count.

- [ ] **Step 4: Initialize queue, metrics and finding ledger**

```bash
python3 scripts/checks/codex-workflow.py queue init --state /tmp/bukit-core-whole-remediation-queue.json
```

Create `/tmp/codex-reports/bukit-core-whole-remediation-findings.json` with exactly `I-01..I-13`, `CI-01..CI-08`, `M-01..M-09`; each record contains `ownerBatch`, `status: open`, and empty `redEvidence`, `greenEvidence`, `reviewEvidence` arrays.

- [ ] **Step 5: Generate all six implementation closures**

Run `python3 scripts/checks/codex-workflow.py closure` once per Batch 1–6 with the exact paths listed below. Save JSON to the fixed closure path. Resolve every `unmapped` before Task 1.

- [ ] **Step 6: Record baseline metrics and commit only external evidence state**

```bash
batch_task=batch-0-baseline
batch_phase=review
batch_label=batch0-review
phase_duration_ms="$(( (SECONDS - phase_started_seconds) * 1000 ))"
python3 scripts/checks/codex-workflow.py metrics add \
  --state /tmp/bukit-core-whole-remediation-metrics.json \
  --task "$batch_task" --phase "$batch_phase" \
  --duration-ms "$phase_duration_ms" \
  --command-label "$batch_label" --status completed
```

Do not create a second repository commit for `/tmp` evidence.

---

## Task 1: Deterministic Rendering, Recovery, Feeds, and Route Identity

**Closes:** I-01, I-02, I-03, I-04, I-05, CI-06, M-03, M-04, M-06.

**Files:**

- Modify: `src/Bukit-Core/Bukit.Engine/Incremental/RenderDependencyHashWriter.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Incremental/SiteModelDataContributor.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/BuildRecoveryTracker.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/AtomFeedGenerator.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/FeedWindowSelector.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/TaxonomyFeedWriter.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/CollectionRouteIndex.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/ArchivePlugin.cs`
- Modify: `src/Bukit-Core/Bukit.Routing/RouteSecurityValidator.cs`
- Modify: `src/Bukit-Core/Bukit.Routing/RoutePathBuilder.cs`
- Modify: `src/Bukit-Core/Bukit.Theme/SectionDataResolver.cs`
- Test: `tests/Bukit.Engine.Tests/RenderDependencyHasherTests.cs`
- Test: `tests/Bukit.Engine.Tests/BuildRecoveryTrackerTests.cs`
- Test: `tests/Bukit.Engine.Tests/RssGeneratorTests.cs`
- Test: `tests/Bukit.Engine.Tests/TaxonomyFeedWriterTests.cs`
- Test: `tests/Bukit.Engine.Tests/CollectionRouteIndexTests.cs`
- Test: `tests/Bukit.Engine.Tests/LlmsTxtPluginTests.cs`
- Test: `tests/Bukit.Engine.Tests/ArchivePluginTests.cs`
- Test: `tests/Bukit.Routing.Tests/RouteSecurityValidatorTests.cs`
- Test: `tests/Bukit.Routing.Tests/RoutePathBuilderTests.cs`
- Test: `tests/Bukit.Theme.Tests/SectionDataResolverTests.cs`

**Interfaces:**

- Consumes: `SiteModel.Data`, `SiteModel.Modules`, public normalized content values, route URLs
- Produces: `RenderDependencyHashWriter.AppendCanonicalValue(object?)`; stable route rejection and deterministic selectors
- Metrics labels: `batch1-implementation`, `batch1-specialty`, `batch1-review`

- [ ] **Step 1: Acquire writer and classify commands**

```bash
batch_task=batch-1-determinism
phase_started_seconds=$SECONDS
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/bukit-core-whole-remediation-queue.json \
  --task "$batch_task"
```

Classify the Engine, Routing, Theme, Architecture tests and AOT command. Expected: test commands are `dotnet-serial`; AOT is `fixture-exclusive`.

- [ ] **Step 2: Add I-01/I-02 RED tests**

Add these exact facts to `RenderDependencyHasherTests`:

```csharp
[Fact] public void Compute_DifferentSiteDataValue_ProducesDifferentHash();
[Fact] public void Compute_DifferentModuleField_ProducesDifferentHash();
[Fact] public void Compute_DifferentSequenceElements_ProducesDifferentHash();
[Fact] public void Compute_StringAndNumberWithSameText_ProducesDifferentHash();
[Fact] public void Compute_NumericValue_IsCultureInvariant();
[Fact] public void Compute_CyclicValue_FailsWithStableDiagnostic();
```

The first five compare hashes with one changed semantic input. The cycle test builds a self-referential dictionary and asserts an `InvalidOperationException` containing `render dependency value cycle`.

- [ ] **Step 3: Run the render RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter 'FullyQualifiedName~RenderDependencyHasherTests.Compute_DifferentSiteDataValue|FullyQualifiedName~RenderDependencyHasherTests.Compute_DifferentModuleField|FullyQualifiedName~RenderDependencyHasherTests.Compute_DifferentSequenceElements|FullyQualifiedName~RenderDependencyHasherTests.Compute_StringAndNumberWithSameText|FullyQualifiedName~RenderDependencyHasherTests.Compute_NumericValue_IsCultureInvariant|FullyQualifiedName~RenderDependencyHasherTests.Compute_CyclicValue_FailsWithStableDiagnostic'
```

Expected: value/module/list/type tests fail because current hashes collide; culture test shows differing hashes or the cycle test shows fallback behavior.

- [ ] **Step 4: Implement the canonical render encoder**

Add an internal method with this contract:

```csharp
internal void AppendCanonicalValue(object? value)
```

Use framed type tags (`null`, `string`, `bool`, `int64`, `uint64`, `decimal`, `double`, `date`, `map`, `seq`, `content-field`, `toc-entry`), invariant scalar formatting, Ordinal map ordering, active-reference cycle detection, max depth 64 and max nodes 100_000. Unsupported values throw `InvalidOperationException`; do not call fallback `ToString()`.

Update `SiteModelDataContributor` to call the encoder for every Data value and explicit ModuleInfo projection:

```csharp
writer.AppendCanonicalValue(new Dictionary<string, object?>
{
    ["id"] = item.Id,
    ["title"] = item.Title,
    ["slug"] = item.Slug,
    ["content"] = item.Content,
    ["fields"] = item.Fields
});
```

- [ ] **Step 5: Add I-03 RED tests and implement atomic recovery state**

Add:

```csharp
[Fact] public void HasIncompleteBuild_MalformedExistingState_ReturnsTrue();
[Fact] public void HasIncompleteBuild_UnknownState_ReturnsTrue();
[Fact] public void MarkStarted_WriteFailure_PreservesPreviousState();
```

Run the three tests and verify current malformed/unknown cases fail. Implement same-directory temp write, `Flush(true)`, then `File.Replace`/atomic move. Any existing unreadable, unsupported-version or unknown state returns `true`.

- [ ] **Step 6: Add I-04/I-05/CI-06/M-03/M-04/M-06 RED tests**

Add exact tests:

```csharp
[Fact] public void Generate_AtomTimestamps_AreInvariantUnderNonGregorianCulture();
[Theory] [InlineData("/docs/?view=all")] [InlineData("/docs/#intro")]
public void Validate_RouteWithQueryOrFragment_Throws(string route);
[Theory] [InlineData("/con./")] [InlineData("/name. /")] [InlineData("/CON.foo.bar/")]
public void Validate_WindowsAlias_ThrowsOnEveryPlatform(string route);
[Fact] public void Select_CaseCollisionWinner_IsInputOrderIndependent();
[Fact] public void WriteFeeds_EqualTimestampWindow_IsInputOrderIndependent();
[Fact] public void CollectionRouteIndex_EqualPublishAt_IsInputOrderIndependent();
[Fact] public void LlmsTxt_EqualPublishAtLimit_IsInputOrderIndependent();
[Fact] public void Archive_EqualPublishAt_IsInputOrderIndependent();
[Fact] public void Resolve_EqualPrimaryKeys_UsesCanonicalIdentityTieBreak();
```

Run the exact filters and verify RED is the reported instability or missing rejection.

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter 'FullyQualifiedName~Generate_AtomTimestamps_AreInvariantUnderNonGregorianCulture|FullyQualifiedName~Select_CaseCollisionWinner_IsInputOrderIndependent|FullyQualifiedName~WriteFeeds_EqualTimestampWindow_IsInputOrderIndependent|FullyQualifiedName~CollectionRouteIndex_EqualPublishAt_IsInputOrderIndependent|FullyQualifiedName~LlmsTxt_EqualPublishAtLimit_IsInputOrderIndependent|FullyQualifiedName~Archive_EqualPublishAt_IsInputOrderIndependent'
dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj \
  --filter 'FullyQualifiedName~Validate_RouteWithQueryOrFragment|FullyQualifiedName~Validate_WindowsAlias'
dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj \
  --filter 'FullyQualifiedName~Resolve_EqualPrimaryKeys_UsesCanonicalIdentityTieBreak'
```

- [ ] **Step 7: Implement invariant feed and route contracts**

Use:

```csharp
utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
```

Reject query, fragment, NUL, trailing dot/space and any segment whose prefix before the first dot is a Windows reserved device name. Add Ordinal URL/ID tie-breaks before `Take`, grouping or winner selection in every listed selector.

- [ ] **Step 8: Run focused GREEN**

Run the exact RED filters again. Expected: all pass; reverse-input assertions produce byte-identical outputs.

- [ ] **Step 9: Run Batch 1 owner suites and AOT**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj
dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

Then run the fixed AOT command. Record exact counts, exits and SDK in batch-1 evidence.

- [ ] **Step 10: Specialty review and commit**

Review only framing/type coverage, stale incremental skip, atomic recovery, route identity and selector boundaries. Critical/Important return to the owning step; Minor is recorded. Then:

```bash
git add \
  src/Bukit-Core/Bukit.Engine/Incremental/RenderDependencyHashWriter.cs \
  src/Bukit-Core/Bukit.Engine/Incremental/SiteModelDataContributor.cs \
  src/Bukit-Core/Bukit.Engine/BuildRecoveryTracker.cs \
  src/Bukit-Core/Bukit.Engine/AtomFeedGenerator.cs \
  src/Bukit-Core/Bukit.Engine/FeedWindowSelector.cs \
  src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/TaxonomyFeedWriter.cs \
  src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/CollectionRouteIndex.cs \
  src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs \
  src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/ArchivePlugin.cs \
  src/Bukit-Core/Bukit.Routing/RouteSecurityValidator.cs \
  src/Bukit-Core/Bukit.Routing/RoutePathBuilder.cs \
  src/Bukit-Core/Bukit.Theme/SectionDataResolver.cs \
  tests/Bukit.Engine.Tests/RenderDependencyHasherTests.cs \
  tests/Bukit.Engine.Tests/BuildRecoveryTrackerTests.cs \
  tests/Bukit.Engine.Tests/RssGeneratorTests.cs \
  tests/Bukit.Engine.Tests/TaxonomyFeedWriterTests.cs \
  tests/Bukit.Engine.Tests/CollectionRouteIndexTests.cs \
  tests/Bukit.Engine.Tests/LlmsTxtPluginTests.cs \
  tests/Bukit.Engine.Tests/ArchivePluginTests.cs \
  tests/Bukit.Routing.Tests/RouteSecurityValidatorTests.cs \
  tests/Bukit.Routing.Tests/RoutePathBuilderTests.cs \
  tests/Bukit.Theme.Tests/SectionDataResolverTests.cs
git commit -m "fix(core): make render and route outputs deterministic"
```

Transition queue to `done` only after review has Critical 0 / Important 0.

---

## Task 2: Fully Validate Image and Media Artifacts

**Closes:** I-06, M-02, M-05, M-09.

**Files:**

- Modify: `src/Bukit-Core/Bukit.Content/Media/ImageContentValidator.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Media/MediaIndexManager.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/ImageOptimizer.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/ImageProcessingPlugin.cs`
- Modify: `src/Bukit-Core/Bukit.Shared/UrlRedactor.cs`
- Test: `tests/Bukit.Content.Tests/ImageAssetLocalizerTests.cs`
- Test: `tests/Bukit.Content.Tests/MediaIndexManagerTests.cs`
- Test: `tests/Bukit.Engine.Tests/ImageOptimizerTests.cs`
- Test: `tests/Bukit.Engine.Tests/ImageProcessingPluginTests.cs`
- Test: `tests/Bukit.Shared.Tests/UrlRedactorTests.cs`

**Interfaces:**

- Consumes: declared MIME, expected converter format, ownership manifest
- Produces: `ImageContentValidator.ValidateAsync(path, contentType, cancellationToken)` as the only image publish gate
- Metrics labels: `batch2-implementation`, `batch2-specialty`, `batch2-review`

- [ ] **Step 1: Acquire Batch 2 and add image RED tests**

```bash
batch_task=batch-2-image-media
phase_started_seconds=$SECONDS
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/bukit-core-whole-remediation-queue.json \
  --task "$batch_task"
```

Add:

```csharp
[Fact] public async Task ValidateAsync_ValidHeaderWithTruncatedPixels_ReturnsFalse();
[Fact] public async Task ValidateAsync_TotalDecodedPixelsOverBudget_ReturnsFalse();
[Fact] public async Task ValidateAsync_MoreThan256Frames_ReturnsFalse();
[Fact] public async Task OptimizeAsync_ExitZeroWithInvalidOutput_FailsWithoutPublishing();
[Fact] public async Task Resize_InvalidTempOutput_IsNotTrackedOrPublished();
```

Use real generated ImageSharp fixtures for valid/truncated/budget cases; fake external tools may write invalid bytes only for the converter-negative test.

- [ ] **Step 2: Run image RED**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj \
  --filter 'FullyQualifiedName~ValidateAsync_ValidHeaderWithTruncatedPixels|FullyQualifiedName~ValidateAsync_TotalDecodedPixelsOverBudget|FullyQualifiedName~ValidateAsync_MoreThan256Frames'
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter 'FullyQualifiedName~OptimizeAsync_ExitZeroWithInvalidOutput|FullyQualifiedName~Resize_InvalidTempOutput'
```

Expected: truncated or fake output is currently accepted, or budget behavior is absent.

- [ ] **Step 3: Implement bounded full decode and wire every consumer**

Use `Image.IdentifyAsync` to validate expected format and checked `width * height * max(frameMetadataCount, 1) <= 100_000_000`; reject more than 256 frame metadata entries. Reset/reopen the stream and `Image.LoadAsync` with `DecoderOptions.MaxFrames = 257`; verify actual format, frames and total pixels, then dispose.

Call the validator before any cache reuse, collision winner acceptance, atomic move, resize tracking or ImageOptimizer publish. AVIF/ICO remain rejected because the pinned decoder set cannot prove validity.

- [ ] **Step 4: Add and close media ownership/lock/log RED tests**

Add:

```csharp
[Fact] public async Task Process_UserOwnedWidthSuffixSource_IsNotSkipped();
[Fact] public void PathGate_LastLeaseReleased_RemovesOnlySameGate();
[Theory] [InlineData("https://user:pass@example.test/secret/token.png?key=x")]
public void Redact_RemovesUserInfoPathQueryAndFragment(string value);
```

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter 'FullyQualifiedName~Process_UserOwnedWidthSuffixSource_IsNotSkipped'
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj \
  --filter 'FullyQualifiedName~PathGate_LastLeaseReleased_RemovesOnlySameGate'
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj \
  --filter 'FullyQualifiedName~Redact_RemovesUserInfoPathQueryAndFragment'
```

Implement manifest-owned generated detection, reference-counted path-gate leases with key/value conditional removal, and log URLs formatted only as `scheme://host[:port]/<redacted-path>`.

- [ ] **Step 5: Run focused and full owner GREEN**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

Run AOT. Review decoder coverage, memory budgets, publish-before-validate ordering, ownership false positives and lock reclamation.

- [ ] **Step 6: Commit Batch 2**

```bash
git add \
  src/Bukit-Core/Bukit.Content/Media/ImageContentValidator.cs \
  src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs \
  src/Bukit-Core/Bukit.Content/Media/MediaIndexManager.cs \
  src/Bukit-Core/Bukit.Engine/ImageOptimizer.cs \
  src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/ImageProcessingPlugin.cs \
  src/Bukit-Core/Bukit.Shared/UrlRedactor.cs \
  tests/Bukit.Content.Tests/ImageAssetLocalizerTests.cs \
  tests/Bukit.Content.Tests/MediaIndexManagerTests.cs \
  tests/Bukit.Engine.Tests/ImageOptimizerTests.cs \
  tests/Bukit.Engine.Tests/ImageProcessingPluginTests.cs \
  tests/Bukit.Shared.Tests/UrlRedactorTests.cs
git commit -m "fix(media): validate decoded artifacts before publication"
```

---

## Task 3: Enforce Plugin Process and Dev Lifecycle Semantics

**Closes:** I-07, CI-01, CI-02, CI-03, M-07.

**Files:**

- Modify: `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs`
- Modify: `src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs`
- Modify: `src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs`
- Create: `src/Bukit-Core/Bukit.PluginHost/ProcessTree/IProcessTreeLimiter.cs`
- Create: `src/Bukit-Core/Bukit.PluginHost/ProcessTree/ProcessTreeUsage.cs`
- Create: `src/Bukit-Core/Bukit.PluginHost/ProcessTree/PlatformProcessTreeLimiter.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/ExternalToolProcessRunner.cs`
- Create: `src/Bukit-Core/Bukit.Engine/ExternalToolProcessTree.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/Dev/DevWebSocketHub.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/Dev/IDevWebSocketHub.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/Dev/DevFileWatcher.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/DevCommand.cs`
- Test: `tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs`
- Test: `tests/Bukit.PluginHost.Tests/PluginProtocolCompatibilityTests.cs`
- Test: `tests/Bukit.PluginHost.Tests/SystemProcessRunnerTests.cs`
- Test: `tests/Bukit.Engine.Tests/ExternalToolProcessRunnerTests.cs`
- Test: `tests/Bukit.Cli.Tests/DevCommandTests.cs`
- Modify only as probe: `tests/PluginProcessProbe/Program.cs`

**Interfaces:**

- Produces: terminal-state gate; `IProcessTreeLimiter`; async watcher shutdown
- Preserves: unconfigured plugin resource-limit behavior and plugin JSON protocol
- Metrics labels: `batch3-implementation`, `batch3-specialty`, `batch3-review`

- [ ] **Step 1: Add I-07 RED**

```bash
batch_task=batch-3-runtime
phase_started_seconds=$SECONDS
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/bukit-core-whole-remediation-queue.json \
  --task "$batch_task"
```

```csharp
[Fact] public async Task InvokeAsync_ValidSuccessJsonAfterResourceLimit_ThrowsResourceLimitExceeded();
```

Fake `PluginProcessResult` with valid success stdout, nonzero exit and `ResourceLimitExceeded = "CPU time exceeded"`. Expected current result: success response; valid RED.

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  --filter 'FullyQualifiedName~InvokeAsync_ValidSuccessJsonAfterResourceLimit_ThrowsResourceLimitExceeded'
```

- [ ] **Step 2: Implement one terminal-state gate**

Replace invoke-specific partial checks with a shared method that rejects caller cancellation, timeout, output limit and resource limit before deserialize. Preserve the documented nonzero-exit/valid-response compatibility only when no safety terminal state exists.

- [ ] **Step 3: Add CI-01/CI-02 process-tree RED**

Extend `PluginProcessProbe` with `spawn-cpu-child`, `spawn-memory-child`, and `exit-parent-keep-pipe-child` modes. Add:

```csharp
[Fact] public async Task RunAsync_ChildCpuExceedsTreeLimit_ReturnsResourceLimitExceeded();
[Fact] public async Task RunAsync_ChildMemoryExceedsTreeLimit_ReturnsResourceLimitExceeded();
[Fact] public async Task RunAsync_ParentExitWithPipeChild_TerminatesTreeAndReaders();
```

Every test records child PID and in `finally` verifies it no longer exists. A platform unable to create the required process-group/job fixture records `unverified` and blocks overall CLOSED.

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  --filter 'FullyQualifiedName~RunAsync_ChildCpuExceedsTreeLimit|FullyQualifiedName~RunAsync_ChildMemoryExceedsTreeLimit'
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter 'FullyQualifiedName~RunAsync_ParentExitWithPipeChild_TerminatesTreeAndReaders'
```

- [ ] **Step 4: Implement platform process-tree ownership**

Define:

```csharp
internal interface IProcessTreeLimiter : IAsyncDisposable
{
    void Attach(Process process);
    ValueTask<ProcessTreeUsage> SampleAsync(CancellationToken cancellationToken);
    void Terminate();
}

internal readonly record struct ProcessTreeUsage(
    TimeSpan CpuTime,
    long PeakMemoryBytes);
```

Windows uses a kill-on-close Job Object and job accounting. Unix starts the child in a dedicated process group, samples the group/descendants using platform-supported APIs and sends group termination. Add `PluginHostErrorCodes.ResourceLimitUnsupported = "plugin.resourceLimitUnsupported"` and assert it in `PluginProtocolCompatibilityTests`. If configured limits cannot be proven on the runtime platform, fail before plugin execution with that stable code.

Engine does not reference PluginHost, so `ExternalToolProcessTree` is a separate internal ownership helper limited to group/tree creation and termination; it does not duplicate resource accounting. ExternalToolProcessRunner owns that helper for the whole invocation; drain timeout terminates the tree, awaits readers and returns failure.

- [ ] **Step 5: Add CI-03/M-07 dev RED**

Add deterministic fake WebSockets and rebuild barriers:

```csharp
[Fact] public async Task BroadcastReloadAsync_StalledClient_TimesOutAndOthersComplete();
[Fact] public async Task DisposeAsync_WaitsForAcceptedRebuildBeforeDisposingGate();
[Fact] public async Task DisposeAsync_CancelsDebounceWithoutUnobservedFault();
```

Do not use sleep as the pass condition. The stalled socket completes only when its token cancels; the watcher test pauses after admission and releases via TCS.

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj \
  --filter 'FullyQualifiedName~BroadcastReloadAsync_StalledClient|FullyQualifiedName~DisposeAsync_WaitsForAcceptedRebuild|FullyQualifiedName~DisposeAsync_CancelsDebounceWithoutUnobservedFault'
```

- [ ] **Step 6: Implement bounded broadcast and async watcher disposal**

Use linked shutdown tokens with a two-second timeout per send and `Task.WhenAll` across a snapshot of clients. Remove only failed/timed-out clients. Track scheduled rebuild tasks in a concurrent set; async dispose disables events, cancels, awaits tracked tasks, then disposes semaphore. `DevCommand` awaits disposal.

- [ ] **Step 7: Full owner GREEN, AOT, review and commit**

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

Run AOT; inspect P/Invoke AOT compatibility, process cleanup and shutdown fault observation. Commit:

```bash
git add \
  src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs \
  src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs \
  src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs \
  src/Bukit-Core/Bukit.PluginHost/ProcessTree/IProcessTreeLimiter.cs \
  src/Bukit-Core/Bukit.PluginHost/ProcessTree/ProcessTreeUsage.cs \
  src/Bukit-Core/Bukit.PluginHost/ProcessTree/PlatformProcessTreeLimiter.cs \
  src/Bukit-Core/Bukit.Engine/ExternalToolProcessRunner.cs \
  src/Bukit-Core/Bukit.Engine/ExternalToolProcessTree.cs \
  src/Bukit-Core/Bukit.Cli/Commands/Dev/DevWebSocketHub.cs \
  src/Bukit-Core/Bukit.Cli/Commands/Dev/IDevWebSocketHub.cs \
  src/Bukit-Core/Bukit.Cli/Commands/Dev/DevFileWatcher.cs \
  src/Bukit-Core/Bukit.Cli/Commands/DevCommand.cs \
  tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs \
  tests/Bukit.PluginHost.Tests/PluginProtocolCompatibilityTests.cs \
  tests/Bukit.PluginHost.Tests/SystemProcessRunnerTests.cs \
  tests/Bukit.Engine.Tests/ExternalToolProcessRunnerTests.cs \
  tests/Bukit.Cli.Tests/DevCommandTests.cs \
  tests/PluginProcessProbe/Program.cs
git commit -m "fix(runtime): enforce process-tree and dev lifecycles"
```

---

## Task 4: Enforce Strict Config and Body-store Ownership

**Closes:** I-08, I-09, I-10, I-11, I-13, M-01.

**Files:**

- Modify: `src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs`
- Modify: `src/Bukit-Core/Bukit.Config/ConfigYamlHelpers.cs`
- Modify: `src/Bukit-Core/Bukit.Content/CompositeContentProvider.cs`
- Modify: `src/Bukit-Core/Bukit.Content/CompositeContentBodyStore.cs`
- Modify: `src/Bukit-Core/Bukit.Content/BodyCacheDecorator.cs`
- Modify: `src/Bukit-Core/Bukit.Content.Notion/NotionBodyStore.cs`
- Modify: `src/Bukit-Core/Bukit.Engine.Abstractions/ContentDocumentFactory.cs`
- Test: `tests/Bukit.Config.Tests/ConfigLoaderTests.cs`
- Test: `tests/Bukit.Config.Tests/ConfigLoaderFullCoverageTests.cs`
- Test: `tests/Bukit.Content.Tests/CompositeContentProviderTests.cs`
- Test: `tests/Bukit.Content.Tests/CompositeContentBodyStoreTests.cs`
- Test: `tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs`
- Test: `tests/Bukit.Content.Tests/NotionBodyStoreTests.cs`
- Create: `tests/Bukit.Engine.Abstractions.Tests/ContentDocumentFactoryTests.cs`

**Interfaces:**

- Produces: node-kind-aware YAML helpers; opaque per-provider BodyKey route token; admission-safe async disposal
- Preserves: duplicate public SourceKey document IDs and public config field names
- Metrics labels: `batch4-implementation`, `batch4-specialty`, `batch4-review`

- [ ] **Step 1: Add I-08 strict-node RED**

```bash
batch_task=batch-4-config-body
phase_started_seconds=$SECONDS
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/bukit-core-whole-remediation-queue.json \
  --task "$batch_task"
```

Add theories covering mapping, sequence and scalar mismatches:

```csharp
[Theory]
[InlineData("build: []", "build", "mapping", "sequence")]
[InlineData("build: { clean: [] }", "build.clean", "scalar", "sequence")]
[InlineData("content: { sources: {} }", "content.sources", "sequence", "mapping")]
public void Load_WrongYamlNodeKind_ThrowsStableConfigInvalidValue(
    string yaml, string path, string expected, string actual);
```

Assert diagnostic code and all three strings. Current silent default or missing-section behavior is RED.

```bash
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj \
  --filter 'FullyQualifiedName~Load_WrongYamlNodeKind_ThrowsStableConfigInvalidValue'
```

- [ ] **Step 2: Implement node-kind-aware accessors**

Add `GetOptionalMapping/Sequence/Scalar` behavior that returns null only when key is absent; present wrong kind throws `ConfigException(ConfigInvalidValue)`. Make strict validator Map/Seq and sequence-child traversal use the same contract.

- [ ] **Step 3: Add I-09/I-10 composite RED**

```csharp
[Fact] public async Task LoadRawAsync_DuplicateSourceKeys_RoutesEachBodyToItsOwnStore();
[Fact] public async Task DisposeAsync_DisposesEachDistinctChildStoreExactlyOnce();
[Fact] public async Task LoadRawAsync_SecondProviderFails_DisposesFirstSuccessfulStore();
[Fact] public async Task LoadRawAsync_RelationProjectionFails_DisposesAllStores();
```

Use two stores returning distinct HTML for the same SourceKey; assert document IDs remain `notion:a` and `notion:b` while bodies remain A/B.

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj \
  --filter 'FullyQualifiedName~LoadRawAsync_DuplicateSourceKeys_RoutesEachBody|FullyQualifiedName~DisposeAsync_DisposesEachDistinctChildStore|FullyQualifiedName~LoadRawAsync_SecondProviderFails|FullyQualifiedName~LoadRawAsync_RelationProjectionFails'
```

- [ ] **Step 4: Implement composite route token and ownership**

Assign provider ordinal tokens independent of SourceKey. Prefix only BodyKey with an opaque internal token; strip it before child delegation. Implement `IAsyncDisposable`, distinct-reference disposal and failure cleanup for completed provider tasks.

- [ ] **Step 5: Add I-11/M-01 lifecycle RED**

```csharp
[Fact] public async Task GetAsync_AdmittedBeforeDispose_IsAwaitedBeforeInnerDisposal();
[Fact] public async Task NotionGetAsync_AdmittedBeforeDispose_CompletesBeforeCtsDisposal();
[Fact] public async Task Trim_OneOverCapacity_RemovesExactlyOneEntry();
```

Pause between admission and `Lazy.Value` with deterministic TCS seams. Reuse BodyCacheDecorator's existing publish seam; add an internal `Action? onCacheEntryPublished` constructor seam to NotionBodyStore and invoke it immediately after `GetOrAdd` and before `lazy.Value`. Dispose must not finish until the accepted Get completes.

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj \
  --filter 'FullyQualifiedName~GetAsync_AdmittedBeforeDispose|FullyQualifiedName~NotionGetAsync_AdmittedBeforeDispose|FullyQualifiedName~Trim_OneOverCapacity_RemovesExactlyOneEntry'
```

- [ ] **Step 6: Implement admission gates and exact trim**

Both stores close admission atomically, count active accepted operations and complete a drain TCS at zero. Dispose waits drain before cancellation/inner disposal. Trim recalculates excess under the LRU lock and removes only that count.

- [ ] **Step 7: Add I-13 RED and normalize merge**

```csharp
[Fact] public void MergeFields_CaseSensitiveMutableInput_CustomFieldWinsCaseInsensitively();
[Fact] public void MergeFields_DoesNotMutateCallerDictionary();
```

Always create an OrdinalIgnoreCase dictionary; add raw properties first and custom fields second.

```bash
dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj \
  --filter 'FullyQualifiedName~MergeFields_CaseSensitiveMutableInput|FullyQualifiedName~MergeFields_DoesNotMutateCallerDictionary'
```

- [ ] **Step 8: Full owner GREEN, AOT, review and commit**

```bash
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj
dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

Run AOT; review public/config compatibility, exact-once disposal and no post-dispose factory start. Commit only listed paths with message:

```bash
git add \
  src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs \
  src/Bukit-Core/Bukit.Config/ConfigYamlHelpers.cs \
  src/Bukit-Core/Bukit.Content/CompositeContentProvider.cs \
  src/Bukit-Core/Bukit.Content/CompositeContentBodyStore.cs \
  src/Bukit-Core/Bukit.Content/BodyCacheDecorator.cs \
  src/Bukit-Core/Bukit.Content.Notion/NotionBodyStore.cs \
  src/Bukit-Core/Bukit.Engine.Abstractions/ContentDocumentFactory.cs \
  tests/Bukit.Config.Tests/ConfigLoaderTests.cs \
  tests/Bukit.Config.Tests/ConfigLoaderFullCoverageTests.cs \
  tests/Bukit.Content.Tests/CompositeContentProviderTests.cs \
  tests/Bukit.Content.Tests/CompositeContentBodyStoreTests.cs \
  tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs \
  tests/Bukit.Content.Tests/NotionBodyStoreTests.cs \
  tests/Bukit.Engine.Abstractions.Tests/ContentDocumentFactoryTests.cs
git commit -m "fix(content): enforce strict config and body ownership"
```

---

## Task 5: Make Notion Summary, Cache, and Pagination Deterministic

**Closes:** I-12, CI-07, CI-08.

**Files:**

- Modify: `src/Bukit-Core/Bukit.Content.Notion/NotionContentSource.cs`
- Modify: `src/Bukit-Core/Bukit.Content.Notion/NotionBodyStore.cs`
- Modify: `src/Bukit-Core/Bukit.Content.Notion/NotionCacheManager.cs`
- Modify: `src/Bukit-Core/Bukit.Content.Notion/NotionRelationTargetCache.cs`
- Create: `src/Bukit-Core/Bukit.Content.Notion/AtomicNotionCacheWriter.cs`
- Create: `src/Bukit-Core/Bukit.Notion/Rendering/NotionPaginationGuard.cs`
- Create: `src/Bukit-Core/Bukit.Notion/Rendering/NotionPaginationException.cs`
- Modify: `src/Bukit-Core/Bukit.Notion/Rendering/NotionBlocksRenderer.cs`
- Modify: `src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/TableBlockRenderer.cs`
- Test: `tests/Bukit.Content.Notion.Tests/NotionContentSourceTests.cs`
- Test: `tests/Bukit.Notion.Tests/NotionBlocksRendererPaginationTests.cs`
- Test: `tests/Bukit.Notion.Tests/NotionRenderingTests.cs`

**Interfaces:**

- Produces: immutable precomputed summary/body result; atomic cache writer; shared pagination guard
- Preserves: Notion cache schema, valid cursor semantics and lazy body fetch when AutoSummary is off
- Metrics labels: `batch5-implementation`, `batch5-specialty`, `batch5-review`

- [ ] **Step 1: Add I-12 RED**

```bash
batch_task=batch-5-notion
phase_started_seconds=$SECONDS
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/bukit-core-whole-remediation-queue.json \
  --task "$batch_task"
```

```csharp
[Fact] public async Task LoadRawAsync_AutoSummary_IsPresentBeforeCanonicalConversion();
[Fact] public async Task LoadRawAsync_AutoSummary_CollectionCopiesShareImmutableValue();
[Fact] public async Task LoadRawAsync_AutoSummary_PrefetchedBodyIsFetchedOnce();
[Fact] public async Task LoadRawAsync_RenderContentFalse_DoesNotPrefetchOrSummarize();
```

Assert fields before any BodyStore.GetAsync call. Current summary absence and later mutation are RED.

```bash
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj \
  --filter 'FullyQualifiedName~LoadRawAsync_AutoSummary_IsPresentBeforeCanonical|FullyQualifiedName~LoadRawAsync_AutoSummary_CollectionCopies|FullyQualifiedName~LoadRawAsync_AutoSummary_PrefetchedBody|FullyQualifiedName~LoadRawAsync_RenderContentFalse'
```

- [ ] **Step 2: Implement bounded summary prefetch**

When RenderContent and AutoSummary are true, render missing-summary pages before RawContentDocument creation using `Parallel.ForEachAsync` with `RenderConcurrency ?? 4`. Build new OrdinalIgnoreCase fields and seed NotionBodyStore with completed ContentBody tasks keyed by page ID. AutoSummary off remains lazy.

- [ ] **Step 3: Add CI-07 atomic-cache RED**

```csharp
[Fact] public async Task PageCache_CancelDuringWrite_PreservesPreviousValidJson();
[Fact] public async Task RelationCache_ConcurrentWriters_LeaveOneCompleteDocument();
[Fact] public async Task CacheWriteFailure_RemovesTemporaryFile();
```

Use a before-replace seam and two manager instances. Assert live JSON is always parseable and version-valid.

```bash
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj \
  --filter 'FullyQualifiedName~PageCache_CancelDuringWrite|FullyQualifiedName~RelationCache_ConcurrentWriters|FullyQualifiedName~CacheWriteFailure_RemovesTemporaryFile'
```

- [ ] **Step 4: Implement atomic cache replacement**

Serialize to unique same-directory temp, flush-to-disk, hold cross-process `.lock`, then replace/move atomically. Clean temp in `finally`. Do not use a process-lifetime static lock dictionary.

- [ ] **Step 5: Add CI-08 pagination RED**

```csharp
[Fact] public async Task ContentSource_RepeatedCursor_ThrowsStablePaginationException();
[Fact] public async Task BlocksRenderer_RepeatedCursor_ThrowsStablePaginationException();
[Fact] public async Task TableRenderer_RepeatedCursor_ThrowsStablePaginationException();
[Fact] public async Task Pagination_MoreThan10000Requests_ThrowsBudgetException();
```

Use local fake handlers only. Assert request count stops at the repeated cursor or budget.

```bash
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj \
  --filter 'FullyQualifiedName~ContentSource_RepeatedCursor'
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj \
  --filter 'FullyQualifiedName~BlocksRenderer_RepeatedCursor|FullyQualifiedName~TableRenderer_RepeatedCursor|FullyQualifiedName~Pagination_MoreThan10000Requests'
```

- [ ] **Step 6: Implement shared pagination guard**

`NotionPaginationGuard.Advance(string? nextCursor)` records Ordinal cursor values, rejects null when `has_more=true`, rejects repeats and rejects request 10,001. It throws internal `NotionPaginationException` with stable reason values `missing_cursor`, `repeated_cursor`, or `request_budget_exceeded`. `Bukit.Notion.csproj` already grants `InternalsVisibleTo` to `Bukit.Content.Notion`, so all three loops use these exact internal types and one error contract.

- [ ] **Step 7: Full owner GREEN, AOT, review and commit**

```bash
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

Run AOT; review summary ordering, cache crash safety, cursor bounds and source-generated serialization. Commit:

```bash
git add \
  src/Bukit-Core/Bukit.Content.Notion/NotionContentSource.cs \
  src/Bukit-Core/Bukit.Content.Notion/NotionBodyStore.cs \
  src/Bukit-Core/Bukit.Content.Notion/NotionCacheManager.cs \
  src/Bukit-Core/Bukit.Content.Notion/NotionRelationTargetCache.cs \
  src/Bukit-Core/Bukit.Content.Notion/AtomicNotionCacheWriter.cs \
  src/Bukit-Core/Bukit.Notion/Rendering/NotionPaginationGuard.cs \
  src/Bukit-Core/Bukit.Notion/Rendering/NotionPaginationException.cs \
  src/Bukit-Core/Bukit.Notion/Rendering/NotionBlocksRenderer.cs \
  src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/TableBlockRenderer.cs \
  tests/Bukit.Content.Notion.Tests/NotionContentSourceTests.cs \
  tests/Bukit.Notion.Tests/NotionBlocksRendererPaginationTests.cs \
  tests/Bukit.Notion.Tests/NotionRenderingTests.cs
git commit -m "fix(notion): make summaries caches and pagination deterministic"
```

---

## Task 6: Establish Handle-based Template and Markdown Reads

**Closes:** CI-04, CI-05, M-08.

**Files:**

- Create: `src/Bukit-Core/Bukit.Shared/IO/ISafeSourceFileOpener.cs`
- Create: `src/Bukit-Core/Bukit.Shared/IO/VerifiedSourceFile.cs`
- Create: `src/Bukit-Core/Bukit.Shared/IO/PlatformSafeSourceFileOpener.cs`
- Modify: `src/Bukit-Core/Bukit.Shared/InternalsVisibleTo.cs`
- Delete after consumers migrate: `src/Bukit-Core/Bukit.Engine/IO/ISafeSourceFileOpener.cs`
- Delete after consumers migrate: `src/Bukit-Core/Bukit.Engine/IO/VerifiedSourceFile.cs`
- Delete after consumers migrate: `src/Bukit-Core/Bukit.Engine/IO/PlatformSafeSourceFileOpener.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/DirectoryCopy.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Incremental/BuildManifestTracker.cs`
- Modify: `src/Bukit-Core/Bukit.Rendering/Scriban/FileTemplateLoader.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Markdown/MarkdownFolderProvider.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Markdown/MarkdownBodyStore.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Markdown/MarkdownFieldBuilder.cs`
- Create: `tests/Bukit.Shared.Tests/PlatformSafeSourceFileOpenerTests.cs`
- Test: `tests/Bukit.Rendering.Tests/FileTemplateLoaderTests.cs`
- Test: `tests/Bukit.Content.Tests/MarkdownFolderProviderTests.cs`
- Test: `tests/Bukit.Content.Tests/MarkdownBodyStoreTests.cs`
- Test: `tests/Bukit.Content.Tests/MarkdownFieldBuilderTests.cs`
- Test: `tests/Bukit.Engine.Tests/DirectoryCopyFollowSymlinksTests.cs`
- Test: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`
- Test: `tests/Bukit.Engine.Tests/BuildManifestTests.cs`

**Interfaces:**

- Produces: internal `Bukit.Shared.IO` no-follow opener accessible only to Engine, Rendering and Content via friend assemblies
- Preserves: ordinary-file sync/async read behavior and current Markdown dates
- Metrics labels: `batch6-implementation`, `batch6-specialty`, `batch6-review`

- [ ] **Step 1: Add CI-04/CI-05/M-08 RED**

```bash
batch_task=batch-6-safe-reads
phase_started_seconds=$SECONDS
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/bukit-core-whole-remediation-queue.json \
  --task "$batch_task"
```

```csharp
[Fact] public void Load_LayoutSymlinkOutsideRoot_Throws();
[Fact] public async Task LoadAsync_LayoutSymlinkOutsideRoot_Throws();
[Fact] public async Task GetAsync_FileReplacedBySymlinkAfterEnumeration_Throws();
[Theory] [InlineData("1,25", "fr-FR")] [InlineData("1.25", "en-US")]
public void Build_NumberParsing_IsInvariant(string input, string culture);
```

Create real temporary symlinks where supported. Unsupported platform/permission records CI finding as unverified and blocks overall CLOSED.

```bash
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj \
  --filter 'FullyQualifiedName~Load_LayoutSymlinkOutsideRoot|FullyQualifiedName~LoadAsync_LayoutSymlinkOutsideRoot'
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj \
  --filter 'FullyQualifiedName~GetAsync_FileReplacedBySymlinkAfterEnumeration|FullyQualifiedName~Build_NumberParsing_IsInvariant'
```

- [ ] **Step 2: Extract the shared internal opener**

Move the existing handle implementation without weakening its contract. `Bukit.Shared/InternalsVisibleTo.cs` already grants access to `Bukit.Engine` and `Bukit.Content`; preserve those entries and add only:

```csharp
[assembly: InternalsVisibleTo("Bukit.Rendering")]
```

Update Engine consumers and tests first; run their focused safe-copy tests before deleting old files.

- [ ] **Step 3: Convert template and Markdown reads**

FileTemplateLoader sync/async reads use `VerifiedSourceFile.Stream`. MarkdownBodyStore receives source root plus candidate relative identity and opens/validates on every GetAsync; it never trusts a previously validated pathname.

Use `long.TryParse(..., NumberStyles.Integer, CultureInfo.InvariantCulture, ...)` and `double.TryParse(..., NumberStyles.Float, CultureInfo.InvariantCulture, ...)`.

- [ ] **Step 4: Focused then full owner GREEN**

```bash
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

Run AOT; review same-handle validation, SafeHandle ownership, sync/async parity, macOS/Linux/Windows P/Invoke and no public API expansion.

- [ ] **Step 5: Commit Batch 6**

```bash
git add \
  src/Bukit-Core/Bukit.Shared/IO/ISafeSourceFileOpener.cs \
  src/Bukit-Core/Bukit.Shared/IO/VerifiedSourceFile.cs \
  src/Bukit-Core/Bukit.Shared/IO/PlatformSafeSourceFileOpener.cs \
  src/Bukit-Core/Bukit.Shared/InternalsVisibleTo.cs \
  src/Bukit-Core/Bukit.Engine/IO/ISafeSourceFileOpener.cs \
  src/Bukit-Core/Bukit.Engine/IO/VerifiedSourceFile.cs \
  src/Bukit-Core/Bukit.Engine/IO/PlatformSafeSourceFileOpener.cs \
  src/Bukit-Core/Bukit.Engine/DirectoryCopy.cs \
  src/Bukit-Core/Bukit.Engine/Incremental/BuildManifestTracker.cs \
  src/Bukit-Core/Bukit.Rendering/Scriban/FileTemplateLoader.cs \
  src/Bukit-Core/Bukit.Content/Markdown/MarkdownFolderProvider.cs \
  src/Bukit-Core/Bukit.Content/Markdown/MarkdownBodyStore.cs \
  src/Bukit-Core/Bukit.Content/Markdown/MarkdownFieldBuilder.cs \
  tests/Bukit.Shared.Tests/PlatformSafeSourceFileOpenerTests.cs \
  tests/Bukit.Rendering.Tests/FileTemplateLoaderTests.cs \
  tests/Bukit.Content.Tests/MarkdownFolderProviderTests.cs \
  tests/Bukit.Content.Tests/MarkdownBodyStoreTests.cs \
  tests/Bukit.Content.Tests/MarkdownFieldBuilderTests.cs \
  tests/Bukit.Engine.Tests/DirectoryCopyFollowSymlinksTests.cs \
  tests/Bukit.Engine.Tests/DirectoryCopyTests.cs \
  tests/Bukit.Engine.Tests/BuildManifestTests.cs
git commit -m "fix(io): enforce handle-based Core reads"
```

---

## Task 7: Delta-only Unified Review and Native AOT Acceptance

**Closes:** final evidence, cross-batch intersections and overall status.

**Files:**

- Review: changed files from Task 0 baseline through Task 6 HEAD
- Create outside repository: `/tmp/codex-reports/bukit-core-whole-remediation-final.md`
- No production edits unless unified review returns Critical/Important within this plan

**Interfaces:**

- Consumes: six closure JSON files, six evidence JSON files, six specialty reports, 30 finding records
- Produces: final CLOSED/PARTIAL/BLOCKED determination
- Metrics labels: `batch7-specialty`, `batch7-review`

- [ ] **Step 1: Verify ledger completeness**

```bash
batch_task=batch-7-final-review
phase_started_seconds=$SECONDS
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/bukit-core-whole-remediation-queue.json \
  --task "$batch_task"
```

Every finding must contain RED, GREEN and review evidence. `CI-* unverified` is not closed and forces final PARTIAL. No record may be closed only because a full project passed.

- [ ] **Step 2: Generate final review scope**

```bash
review_scope=(python3 scripts/checks/codex-workflow.py review-scope \
  --findings /tmp/codex-reports/bukit-core-whole-remediation-findings.json)
for evidence in /tmp/codex-reports/bukit-core-whole-remediation-batch-{1..6}-evidence.json; do
  review_scope+=(--evidence "$evidence")
done
baseline_head="$(jq -r .baselineHead /tmp/codex-reports/bukit-core-whole-remediation-batch-0-evidence.json)"
while IFS= read -r changed_file; do
  review_scope+=(--changed "$changed_file")
done < <(git diff --name-only "$baseline_head"..HEAD -- src/Bukit-Core tests)
"${review_scope[@]}"
```

Expected: only cross-batch intersections, invalidated evidence, uncovered changed files, public/serialized contracts and open Critical/Important.

- [ ] **Step 3: Run the exact 14-project final matrix**

```bash
dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj
dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj
dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj
dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

No whole-solution substitute is allowed.

- [ ] **Step 4: Run final fresh Native AOT**

Run the fixed AOT command. Record publish exit, warnings, native runtime identification and both smoke exits separately.

- [ ] **Step 5: Run one unified delta review**

Review only:

```text
render encoder <-> canonical body values
process terminal states <-> dev shutdown
image validation <-> ownership/media index
Notion prefetch <-> body-store admission/cache
shared opener <-> Engine/Rendering/Content consumers
public/config/serialized contracts <-> Native AOT rooting
```

Critical/Important returns only to the owning Task and then receives one scoped re-review. Do not reopen clean historical findings.

- [ ] **Step 6: Report final status and metrics**

```bash
python3 scripts/checks/codex-workflow.py metrics report \
  --state /tmp/bukit-core-whole-remediation-metrics.json
baseline_head="$(jq -r .baselineHead /tmp/codex-reports/bukit-core-whole-remediation-batch-0-evidence.json)"
git diff --check "$baseline_head"..HEAD -- src/Bukit-Core tests
```

Write `/tmp/codex-reports/bukit-core-whole-remediation-final.md` with separate fields for 13 Important, 8 Conditional Important, 9 Minor, 14 specialty projects, AOT publish, two native smokes, unified review and environment conditions.

Overall status rules:

```text
CLOSED  = all 30 findings closed + 14 suites GREEN + AOT/smokes GREEN + review C0/I0
PARTIAL = any CI unverified, Minor explicitly deferred, or required evidence missing
BLOCKED = a required platform/tool/permission condition prevents progress after the repository retry boundary
```

- [ ] **Step 7: Final local commit if review produced report-only repository changes**

The final report remains under `/tmp`; normally no repository commit is created. Do not amend or squash Batch 1–6 commits without explicit authorization.

## Execution Stop Conditions

Stop the current task and report instead of widening scope when:

- a repair requires changing a public required parameter, config default, cache schema or serialized manifest beyond the compatibility changes explicitly approved in the design;
- a Conditional Important cannot produce a valid RED on the available platform;
- a process or filesystem test cannot guarantee cleanup of child processes, handles or temporary paths;
- a required Core consumer would need production edits under Labs/Plugins;
- closure reports an unmapped path, queue ownership is contested, or the same environment failure repeats twice;
- AOT requires reflection suppression or dynamic fallback rather than an analyzable implementation;
- final review finds an issue outside I/CI/M ledger.
