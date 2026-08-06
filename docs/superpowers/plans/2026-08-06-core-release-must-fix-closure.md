# Bukit Core Release Must-Fix Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完整修复并验证 C-1、C-3、I-1、I-2、I-8，使五项原发布阻断问题在实现、直接消费者和回归测试层面全部闭合。

**Architecture:** 五项修复严格串行，每项独立执行 RED→GREEN→专项复审。HTML 属性统一使用现有编码器；输出写入必须使用路径策略返回的规范化目标；绝对路径统一返回 `Path.GetFullPath`；默认策略通过原子发布保持单例；SSRF 连接在固定 DNS 结果中依次尝试所有公网地址，同时保持私网阻断和取消语义。

**Tech Stack:** .NET 10、C#、xUnit、`System.Net.Sockets`、Bukit codex-workflow。

## Global Constraints

- 仅修改五项明确批准的问题、直接回归测试和本计划，不顺带处理其他审计项。
- 不修改 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/`。
- 五项任务严格串行；每项通过 writer queue 的 `writing → testing → review_wait → done` 生命周期。
- 每项先写失败测试并确认因目标缺陷失败，再写最小实现。
- Task 1–5 只运行列出的专项测试，不运行 `ci-fast`、`ci-full`、whole-solution、release gate 或 Native AOT。
- 最终只运行 closure 要求的专项项目测试和一次 delta-only unified review；任何更广 gate 仍需单独授权。
- 每项独立提交；不 push、不创建 PR、不合并到 `main`。

## Execution Preflight

- [x] 确认 worktree 位于 `codex/core-release-must-fix`，HEAD 为 `2558ec668e94aead347bbfc870e9c514f0f050f4`，初始状态干净。
- [x] 对本计划列出的全部预期变更文件生成 verification closure，解决全部 `unmappedFiles`。
- [x] 用 `classify` 确认四条 `dotnet test` 命令属于 `dotnet-serial`。
- [x] 初始化 `/tmp/codex-reports/bukit-core-release-must-fix-queue.json` 和 metrics state。
- [ ] 运行四个专项测试类作为隔离 worktree 基线。

---

### Task 0: 补齐发布必修计划的 codex-workflow owner 映射

**Files:**
- Modify/Test: `scripts/checks/codex-workflow-self-test.d/closure-basic.sh`
- Modify: `scripts/checks/codex-workflow-policy.v1.json`

**Interfaces:**
- Produces: `docs/superpowers/plans/*core-release-must-fix*.md` 由 `codex-workflow` owner 管理，并要求 `bash scripts/checks/codex-workflow-self-test.sh`。

- [x] **Step 1: 在 closure self-test 中新增计划路径映射断言。**
- [x] **Step 2: 运行 `bash scripts/checks/codex-workflow-self-test.sh`，确认因计划路径仍 unmapped 而 RED。**
- [x] **Step 3: 仅扩展 `codex-workflow-plans.matches`，加入 `*core-release-must-fix*.md` 顶层和递归模式。**
- [x] **Step 4: 重跑同一 self-test 确认 GREEN，并重新生成本计划 closure，确认 `unmappedFiles=[]`。**
- [ ] **Step 5: 提交 `fix(workflow): map release must-fix plans`。**

---

### Task 1: C-1 HTML URL 属性编码

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine/BuildPathUtils.cs:213-263`
- Modify/Test: `tests/Bukit.Engine.Tests/BuildPathUtilsTests.cs`

**Interfaces:**
- Consumes: `BuildPathUtils.EscapeHtml(string)`。
- Produces: `RenderSimplePage` 和 `RenderSimpleIndex` 对 `href` 属性中的 `&`、`"`、`'`、`<`、`>` 做 HTML 编码；正文 HTML 继续按既有契约原样渲染。

- [ ] **Step 1: 写失败测试**

新增两个测试，分别传入包含 `"` 和 `&` 的 canonical URL 与 route URL，并断言输出只包含：

```csharp
Assert.Contains("href=\"/post/?q=&quot;quoted&quot;&amp;page=1\"", result);
Assert.DoesNotContain("href=\"/post/?q=\"quoted\"&page=1\"", result);
```

- [ ] **Step 2: 运行 RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~BuildPathUtilsTests'
```

预期：新增的 page/index 属性编码断言失败，正常渲染测试保持通过。

- [ ] **Step 3: 最小实现**

将三个属性插值改为：

```csharp
href="{EscapeHtml(cssHref)}"
href="{EscapeHtml(canonical)}"
href="{EscapeHtml(href)}"
```

- [ ] **Step 4: 运行同一命令确认 GREEN 并复审消费者。**
- [ ] **Step 5: 提交 `fix(engine): escape simple page URL attributes`。**

---

### Task 2: C-3 使用安全路径解析结果

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine/DirectoryCopy.cs:26-74,617-680`
- Modify/Test: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`

**Interfaces:**
- Consumes: `IOutputPathPolicy.ResolveSafePath(string, string)` 通过 `FileWriter.GetSafeFullPath` 返回的最终物理路径。
- Produces: `Copy`、`SyncFileToPath`、`SyncVerifiedFileToPath` 的全部检查、写入和元数据更新均使用解析后的目标路径。

- [ ] **Step 1: 写失败测试**

添加测试用 `RedirectingOutputPathPolicy` 将目标重映射到另一个临时目录，分别验证：

```csharp
DirectoryCopy.Copy(source, originalOutput, originalOutput, policy);
DirectoryCopy.SyncFiles(source, originalOutput, outputRoot: originalOutput, pathPolicy: policy);
DirectoryCopy.SyncPlannedFile(sourceFile, originalDestination, "size-time",
    originalOutput, planned.PhysicalSourceRoot, options, policy);
```

每个断言都要求文件只写入策略返回路径，原始目标文件不存在。

- [ ] **Step 2: 运行 RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~DirectoryCopyTests'
```

预期：三个重映射测试均发现文件仍写入原始目标。

- [ ] **Step 3: 最小实现**

在三处把目标赋值为 `GetSafeFullPath` 的返回值，并在写入前创建解析后目标的父目录：

```csharp
destinationFile = FileWriter.GetSafeFullPath(
    outputRoot,
    Path.GetRelativePath(outputRoot, destinationFile),
    pathPolicy);
Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
```

- [ ] **Step 4: 运行同一命令确认 GREEN；复核 `AssetPipeline` 和 `BuildManifestTracker` 直接消费者。**
- [ ] **Step 5: 提交 `fix(engine): honor resolved output copy paths`。**

---

### Task 3: I-1 规范化绝对路径

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine/BuildPathUtils.cs:18-47`
- Modify/Test: `tests/Bukit.Engine.Tests/BuildPathUtilsTests.cs`

**Interfaces:**
- Produces: 两个 `MakeAbsolute` 重载无论输入是否 rooted，均返回 `Path.GetFullPath` 的规范化结果；`enforceWithinRoot=true` 的边界检查保持不变。

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public void MakeAbsolute_RootedPath_NormalizesParentSegments()
{
    var root = Path.Combine(Path.GetTempPath(), "bukit-root");
    var input = Path.Combine(root, "nested", "..", "target");

    Assert.Equal(Path.Combine(root, "target"), BuildPathUtils.MakeAbsolute(root, input));
}
```

- [ ] **Step 2: 运行 Task 1 的 `BuildPathUtilsTests` 命令确认 RED。**
- [ ] **Step 3: 将 `!enforceWithinRoot` 分支改为 `return resolved;`。**
- [ ] **Step 4: 运行同一命令确认 GREEN；复核 BuildPlanner、ContentProviderFactory、DevCommand 消费者。**
- [ ] **Step 5: 提交 `fix(engine): normalize rooted absolute paths`。**

---

### Task 4: I-2 原子发布默认路径策略

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine/FileWriter.cs:6-19`
- Modify/Test: `tests/Bukit.Engine.Tests/FileWriterTests.cs`

**Interfaces:**
- Produces: 并发首次读取 `FileWriter.DefaultPolicy` 时所有调用者获得同一个已发布实例；测试或内部覆盖仍可通过 setter 原子替换非空策略。

- [ ] **Step 1: 写失败并发测试**

使用 16 个长期线程、`Barrier` 和多轮首次读取；每轮通过反射只把私有字段恢复为未初始化状态，然后同步读取并记录引用：

```csharp
Assert.False(observedMultipleInstances,
    "Concurrent first reads published more than one default policy instance.");
```

测试结束必须 join 全部线程，并在 `finally` 恢复原策略。

- [ ] **Step 2: 运行 RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~FileWriterTests'
```

预期：现有 `??=` 在至少一轮并发首次读取中返回多个实例。

- [ ] **Step 3: 最小实现**

```csharp
get
{
    var current = Volatile.Read(ref s_defaultPolicy);
    if (current is not null) return current;
    var created = new SafePathResolver();
    return Interlocked.CompareExchange(ref s_defaultPolicy, created, null) ?? created;
}
set
{
    ArgumentNullException.ThrowIfNull(value);
    Volatile.Write(ref s_defaultPolicy, value);
}
```

- [ ] **Step 4: 运行同一命令确认 GREEN。**
- [ ] **Step 5: 提交 `fix(engine): publish default path policy atomically`。**

---

### Task 5: I-8 依次尝试全部公网 DNS 地址

**Files:**
- Modify: `src/Bukit-Core/Bukit.Shared/SsrfGuard.cs:8-30`
- Create/Test: `tests/Bukit.Shared.Tests/SsrfGuardConnectTests.cs`

**Interfaces:**
- 保留: `public static ValueTask<Stream> SsrfSafeConnectAsync(SocketsHttpConnectionContext, CancellationToken)`。
- 新增 internal seam: 接收 host、port、地址解析委托和单地址连接委托，供确定性回归测试使用。
- Produces: 跳过全部私网/保留地址；公网地址按 DNS 顺序尝试；网络连接失败时继续；取消立即停止；所有公网地址失败后抛出带最后失败原因的 `HttpRequestException`。

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public async Task SafeConnect_FirstPublicAddressFails_TriesNextPublicAddress()
{
    var attempted = new List<IPAddress>();
    using var expected = new MemoryStream([1, 2, 3]);
    var stream = await SsrfGuard.SsrfSafeConnectAsync(
        "example.com", 443, CancellationToken.None,
        (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("1.1.1.1") }),
        (address, _, _) =>
        {
            attempted.Add(address);
            return attempted.Count == 1
                ? ValueTask.FromException<Stream>(new SocketException((int)SocketError.HostUnreachable))
                : ValueTask.FromResult<Stream>(expected);
        });

    Assert.Same(expected, stream);
    Assert.Equal(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("1.1.1.1") }, attempted);
}
```

另加私网地址不会传给 connector、取消不继续重试、全部公网失败保留 inner exception 三个边界测试。

- [ ] **Step 2: 运行 RED**

```bash
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~SsrfGuardConnectTests'
```

预期：测试因缺少可注入的多地址连接入口而无法通过编译。

- [ ] **Step 3: 最小实现**

公共入口委托给 internal seam；seam 解析一次 DNS、过滤私网地址并逐个调用 connector。生产 connector 为每个地址创建独立 `Socket`，失败即 dispose；仅捕获可重试的 `SocketException`、`IOException`、`HttpRequestException`，取消和程序错误原样传播。

- [ ] **Step 4: 运行同一命令确认 GREEN，再运行 `SecurityFuzzingTests` 过滤命令确认现有地址分类不回归。**
- [ ] **Step 5: 提交 `fix(shared): retry public SSRF connection addresses`。**

---

### Task 6: Final Verification and Unified Review

**Files:** 全部 Task 1–5 diff。

- [ ] 重新生成实际 changed-file closure 和 `review-scope`，确认无 unmapped、无未覆盖文件。
- [ ] 串行运行：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~BuildPathUtilsTests|FullyQualifiedName~DirectoryCopyTests|FullyQualifiedName~FileWriterTests'
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~SsrfGuardConnectTests|FullyQualifiedName~SecurityFuzzingTests'
```

- [ ] 执行一次 delta-only unified review：重点检查 HTML 编码、解析路径实际使用、路径兼容性、并发发布、地址重试/取消/资源释放。
- [ ] 仅当没有 Critical/Important finding，记录 metrics report 并提交本计划最终勾选状态。
- [ ] 不执行合并、push、PR 或发布 gate；向用户报告分支、提交、测试和剩余集成边界。
