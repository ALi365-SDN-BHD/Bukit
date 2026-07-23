# Bukit Core G-04D2B1 PluginHost Error Code Contract Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 `PluginHostErrorCodes` 的测试契约从同源 public const 引用迁移到
`PluginProtocolClient` public 入口和独立协议词汇 fixture，同时保持所有生产代码、
CLR 可见性和治理基线不变。

**Architecture:** 一个 implementation task 完成两个相互依赖的证据层：PluginHost
测试用固定协议字符串验证五个实际 Host 输出，Architecture fixture 验证六个稳定/
保留协议词汇以及 B1 的 public-surface 当前态。任务不增加新运行时抽象，不修改
`PluginHostErrorCodes`、`PluginProtocolClient`、baseline 或 closed manifest。

**Tech Stack:** C# 14、.NET 10、xUnit、System.Text.Json、JSON fixture、Markdown、
Bukit repository checks。

## Global Constraints

- 基线固定为 `2.0@2272156f054cb308028b57ba50cc65268a454e30`。
- 本任务只迁移 diagnostic-contract 测试和证据；不得修改任何 production source。
- 不得修改 `PluginHostErrorCodes` 类型、六个成员、六个字符串或访问级别。
- 不得修改 `PluginProtocolClient`、异常格式、`DiagnosticCode`、权限、timeout、
  output-limit、artifact path 或 invoke business-failure 行为。
- 五个 Host 实际错误码必须通过 public 入口精确锁定：
  `plugin.unsupportedProtocol`、`plugin.invalidResponse`、`plugin.timeout`、
  `plugin.executionFailed`、`plugin.outputTooLarge`。
- `plugin.permissionDenied` 只能记录为 inbound/reserved protocol vocabulary；
  不得宣称或实现 Host 当前会发出该值。
- 不得新增 public/internal replacement constants、enum、facade、contract assembly
  或 `InternalsVisibleTo`。
- 不得修改 `bukit-plugin-v1` DTO/schema、配置、CLI schema 或官方插件。
- public API baseline 必须保持 14 assemblies / 508 types / 104 candidates，文件
  byte-for-byte 不变。
- closed candidate manifest 必须保持 136 entries，Git blob 必须是
  `7b07d6890562387010b52301e9f8716e9bf10ed1`，文件 byte-for-byte 不变。
- 不得修改 consumer declaration 或 public API governance guide；B1 不授权 B2。
- 不得修改 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/` 或 `scripts-0.2/`。
- 代码子任务完成后只运行一次 `post-change-focused.sh`；父任务完成时只运行一次
  `post-change-targeted.sh --base 2272156f...`。
- 不运行 full、release、Native AOT、`test-all`、`smoke-all` 或 whole-solution gate。

---

### Task 1: Migrate the PluginHost diagnostic contract without changing production

**Files:**
- Create: `tests/fixtures/plugin-contracts/plugin-host-error-vocabulary.v1.json`
- Create: `tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs`
- Modify: `tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs`
- Create: `docs/analysis/bukit-core-g04d2b1-plugin-host-error-code-contract-migration-2026-07-23.zh-CN.md`
- Use unchanged: `src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs`
- Use unchanged: `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs`
- Use unchanged: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Use unchanged: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- Use unchanged: `docs/plugins/Bukit 插件协议 v1 规范.md`
- Use unchanged: `docs/plugins/Bukit 插件安全模型 ADR.md`

**Interfaces:**
- Consumes:
  - `PluginProtocolClient.HandshakeAsync(ResolvedPlugin, CancellationToken)`
  - `PluginProtocolClient.GetManifestAsync(ResolvedPlugin, CancellationToken)`
  - `PluginProtocolClient.InvokeAsync(ResolvedPlugin, PluginInvokeRequest, CancellationToken)`
  - `ConfigException.Code`
  - `PluginInvokeResponse.Error`
- Produces:
  - test-local `AssertProtocolFailure(ConfigException, string, string)`
  - fixture schema `bukit-plugin-host-error-vocabulary-v1`
  - `G04D2B1PluginHostErrorCodeContractTests`
  - a B1 execution ledger that explicitly leaves B2 unauthorized

- [ ] **Step 1: Add the first Architecture RED for the test-layer CLR dependency**

Create
`tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs`
with only the repository-root helper and this initial test:

```csharp
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D2B1PluginHostErrorCodeContractTests
{
    private const string TargetTypeName =
        "Bukit.PluginHost.PluginHostErrorCodes";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void PluginProtocolClientTests_DoNotConsumeErrorCodeClrType()
    {
        var testSource = File.ReadAllText(Path.Combine(
            RepoRoot,
            "tests",
            "Bukit.PluginHost.Tests",
            "PluginProtocolClientTests.cs"));

        Assert.DoesNotContain(
            TargetTypeName.Split('.').Last(),
            testSource,
            StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException(
            "Could not locate the Bukit repository root.");
    }
}
```

- [ ] **Step 2: Run the first RED and verify the failure reason**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  FullyQualifiedName~G04D2B1PluginHostErrorCodeContractTests.PluginProtocolClientTests_DoNotConsumeErrorCodeClrType
```

Expected: one failed test because
`PluginProtocolClientTests.cs` still contains the identifier
`PluginHostErrorCodes`. Compilation or repository-root errors are not an
acceptable RED.

- [ ] **Step 3: Migrate test expectations to independent protocol literals**

In `PluginProtocolClientTests.cs`, replace the combined handshake theory with:

```csharp
[Theory]
[InlineData(
    "bad-protocol",
    "req-1",
    "plugin.unsupportedProtocol",
    "Plugin response protocol is unsupported.")]
[InlineData(
    "bukit-plugin-v1",
    "other",
    "plugin.invalidResponse",
    "Plugin response requestId did not match request.")]
public async Task HandshakeAsync_RejectsInvalidProtocolOrMismatchedRequestId(
    string protocol,
    string responseRequestId,
    string expectedCode,
    string expectedDetail)
{
    var invoker = new StubPluginProcessInvoker(
        "{\"type\":\"handshakeResponse\",\"protocol\":\"" + protocol +
        "\",\"requestId\":\"" + responseRequestId +
        "\",\"success\":true,\"plugin\":{\"id\":\"echo\",\"name\":\"Echo\",\"version\":\"0.1.0\",\"platform\":\"osx-arm64\"}}");
    var client = new PluginProtocolClient(
        invoker,
        new FixedRequestIdFactory("req-1"));

    ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
        () => client.HandshakeAsync(CreatePlugin(), CancellationToken.None));

    AssertProtocolFailure(exception, expectedCode, expectedDetail);
}
```

Add this helper immediately before `CreatePlugin`:

```csharp
private static void AssertProtocolFailure(
    ConfigException exception,
    string code,
    string detail)
{
    Assert.Equal(DiagnosticCode.PluginExecutionFailed, exception.Code);
    Assert.Equal($"{code}: {detail}", exception.Message);
}
```

Replace every remaining `PluginHostErrorCodes.*` assertion in that test file
with fixed literals and, for the following five representative cases, exact
calls to `AssertProtocolFailure`:

```csharp
AssertProtocolFailure(
    exception,
    "plugin.invalidResponse",
    "Handshake plugin identity does not match resolved plugin.");
```

```csharp
AssertProtocolFailure(
    exception,
    "plugin.invalidResponse",
    "Plugin stdout was not valid protocol JSON.");
Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
```

```csharp
AssertProtocolFailure(
    exception,
    "plugin.timeout",
    "Plugin process timed out.");
```

```csharp
AssertProtocolFailure(
    exception,
    "plugin.outputTooLarge",
    "Plugin process output exceeded configured limits.");
```

```csharp
AssertProtocolFailure(
    exception,
    "plugin.invalidResponse",
    "Plugin artifact path must be a project-relative safe path.");
```

For any remaining pre-existing assertion whose full detail is not part of
the five-value representative matrix, use an independent literal such as:

```csharp
Assert.Contains(
    "plugin.invalidResponse",
    exception.Message,
    StringComparison.Ordinal);
```

Do not leave any `PluginHostErrorCodes` identifier in
`PluginProtocolClientTests.cs`.

- [ ] **Step 4: Add the missing execution-failed public-entry contract**

Add this test after `GetManifestAsync_RejectsInvalidJson`:

```csharp
[Fact]
public async Task GetManifestAsync_RejectsNonZeroProcessExit()
{
    var invoker = new StubPluginProcessInvoker("{}", exitCode: 7);
    var client = new PluginProtocolClient(
        invoker,
        new FixedRequestIdFactory("req-2"));

    ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
        () => client.GetManifestAsync(CreatePlugin(), CancellationToken.None));

    AssertProtocolFailure(
        exception,
        "plugin.executionFailed",
        "Plugin process exited with code 7.");
}
```

This test must use handshake or manifest. Do not change invoke's existing
business-failure/nonzero-valid-response behavior.

- [ ] **Step 5: Add the inbound reserved-vocabulary characterization**

Add this test after
`InvokeAsync_ReturnsBusinessFailureResponseWithDiagnostics`:

```csharp
[Fact]
public async Task InvokeAsync_PreservesInboundPermissionDeniedErrorCode()
{
    var invoker = new StubPluginProcessInvoker(
        """
        {"type":"invokeResponse","protocol":"bukit-plugin-v1","requestId":"req-3","success":false,"exitCode":4,"error":{"code":"plugin.permissionDenied","message":"Permission denied"}}
        """);
    var client = new PluginProtocolClient(
        invoker,
        new FixedRequestIdFactory("req-3"));

    PluginInvokeResponse response = await client.InvokeAsync(
        CreatePlugin(),
        CreateInvokeRequest(),
        CancellationToken.None);

    Assert.False(response.Success);
    Assert.Equal(4, response.ExitCode);
    Assert.Equal("plugin.permissionDenied", response.Error?.Code);
    Assert.Equal("Permission denied", response.Error?.Message);
}
```

The test name and assertions must describe inbound preservation, not Host
emission.

- [ ] **Step 6: Run GREEN for the migrated PluginHost contract and CLR guard**

Run:

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~PluginProtocolClientTests
```

Expected: all `PluginProtocolClientTests` cases pass, including the two new
facts, with zero failures.

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  FullyQualifiedName~G04D2B1PluginHostErrorCodeContractTests.PluginProtocolClientTests_DoNotConsumeErrorCodeClrType
```

Expected: one passed test. Confirm with:

```bash
rg -n "PluginHostErrorCodes" \
  tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs
```

Expected: exit 1 and no matches.

- [ ] **Step 7: Add the second Architecture RED for the vocabulary fixture**

Extend `G04D2B1PluginHostErrorCodeContractTests.cs` with:

```csharp
using System.Text.Json;
```

Add these constants:

```csharp
private const string VocabularySchema =
    "bukit-plugin-host-error-vocabulary-v1";
private static readonly string[] StableVocabulary =
[
    "plugin.unsupportedProtocol",
    "plugin.invalidResponse",
    "plugin.timeout",
    "plugin.executionFailed",
    "plugin.permissionDenied",
    "plugin.outputTooLarge"
];
```

Add this test before creating the fixture:

```csharp
[Fact]
public void ProtocolVocabularyFixture_PreservesExactSixTermsAndActiveDocs()
{
    var fixturePath = Path.Combine(
        RepoRoot,
        "tests",
        "fixtures",
        "plugin-contracts",
        "plugin-host-error-vocabulary.v1.json");
    using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
    var root = document.RootElement;
    var codes = root.GetProperty("codes")
        .EnumerateArray()
        .Select(code => code.GetString())
        .ToArray();

    Assert.Equal(VocabularySchema, root.GetProperty("schema").GetString());
    Assert.Equal(StableVocabulary, codes);
    Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    Assert.DoesNotContain(TargetTypeName, File.ReadAllText(fixturePath), StringComparison.Ordinal);

    var protocol = File.ReadAllText(Path.Combine(
        RepoRoot,
        "docs",
        "plugins",
        "Bukit 插件协议 v1 规范.md"));
    foreach (string code in StableVocabulary)
    {
        Assert.Contains($"`{code}`", protocol, StringComparison.Ordinal);
    }

    var securityAdr = File.ReadAllText(Path.Combine(
        RepoRoot,
        "docs",
        "plugins",
        "Bukit 插件安全模型 ADR.md"));
    foreach (string code in StableVocabulary.Skip(1))
    {
        Assert.Contains($"`{code}`", securityAdr, StringComparison.Ordinal);
    }

    var protocolClientSource = File.ReadAllText(Path.Combine(
        RepoRoot,
        "src",
        "Bukit-Core",
        "Bukit.PluginHost",
        "PluginProtocolClient.cs"));
    Assert.DoesNotContain(
        "PluginHostErrorCodes.PermissionDenied",
        protocolClientSource,
        StringComparison.Ordinal);

    var permissionEvaluatorSource = File.ReadAllText(Path.Combine(
        RepoRoot,
        "src",
        "Bukit-Core",
        "Bukit.PluginHost",
        "PluginPermissionEvaluator.cs"));
    Assert.DoesNotContain(
        "plugin.permissionDenied",
        permissionEvaluatorSource,
        StringComparison.Ordinal);
}
```

The `Skip(1)` is deliberate: the current security ADR uses a different
loader-oriented unsupported-protocol term, while the canonical protocol
document owns `plugin.unsupportedProtocol`.

- [ ] **Step 8: Run the second RED and verify the missing-fixture failure**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  FullyQualifiedName~G04D2B1PluginHostErrorCodeContractTests.ProtocolVocabularyFixture_PreservesExactSixTermsAndActiveDocs
```

Expected: one failed test with `FileNotFoundException` for
`plugin-host-error-vocabulary.v1.json`. A compile error or missing repository
root is not an acceptable RED.

- [ ] **Step 9: Create the exact vocabulary fixture and obtain GREEN**

Create
`tests/fixtures/plugin-contracts/plugin-host-error-vocabulary.v1.json`
with exactly:

```json
{
  "schema": "bukit-plugin-host-error-vocabulary-v1",
  "codes": [
    "plugin.unsupportedProtocol",
    "plugin.invalidResponse",
    "plugin.timeout",
    "plugin.executionFailed",
    "plugin.permissionDenied",
    "plugin.outputTooLarge"
  ]
}
```

Re-run the command from Step 8.

Expected: one passed test.

- [ ] **Step 10: Add current-state protection for B1**

Add these usings:

```csharp
using System.Security.Cryptography;
using System.Text;
```

Add:

```csharp
private const string CandidateManifestBlob =
    "7b07d6890562387010b52301e9f8716e9bf10ed1";
private static readonly string[] BaselineMembers =
[
    "public const System.String! ExecutionFailed = \"plugin.executionFailed\"",
    "public const System.String! InvalidResponse = \"plugin.invalidResponse\"",
    "public const System.String! OutputTooLarge = \"plugin.outputTooLarge\"",
    "public const System.String! PermissionDenied = \"plugin.permissionDenied\"",
    "public const System.String! Timeout = \"plugin.timeout\"",
    "public const System.String! UnsupportedProtocol = \"plugin.unsupportedProtocol\""
];
```

Add:

```csharp
[Fact]
public void CurrentPublicSurface_KeepsErrorCodeTypeAndExactBaseline()
{
    var assembly = typeof(Bukit.PluginHost.PluginConfigLoader).Assembly;
    var type = assembly.GetType(
        TargetTypeName,
        throwOnError: false,
        ignoreCase: false);

    Assert.NotNull(type);
    Assert.True(type.IsPublic);
    Assert.Contains(
        assembly.GetExportedTypes(),
        exported => exported.FullName == TargetTypeName);

    using var document = ReadJson(
        "docs",
        "governance",
        "bukit-core-public-api-baseline.v1.json");
    var root = document.RootElement;
    var types = root.GetProperty("types").EnumerateArray().ToArray();
    var target = Assert.Single(types, entry =>
        entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
        entry.GetProperty("name").GetString() == TargetTypeName);
    var members = target.GetProperty("publicMembers")
        .EnumerateArray()
        .Select(member => member.GetString())
        .ToArray();

    Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
    Assert.Equal(508, types.Length);
    Assert.Equal(104, types.Count(entry =>
        entry.GetProperty("compatibility").GetString() == "2.0-candidate"));
    Assert.Equal("2.0-candidate", target.GetProperty("compatibility").GetString());
    Assert.Equal(BaselineMembers, members);
}
```

Add:

```csharp
[Fact]
public void ClosedManifest_PreservesHistoricalErrorCodeEvidenceAndExactBlob()
{
    var path = Path.Combine(
        RepoRoot,
        "docs",
        "governance",
        "bukit-core-2.0-public-surface-candidates.v1.json");
    var bytes = File.ReadAllBytes(path);
    using var document = JsonDocument.Parse(bytes);
    var root = document.RootElement;
    var candidates = root.GetProperty("candidates").EnumerateArray().ToArray();
    var target = Assert.Single(candidates, entry =>
        entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
        entry.GetProperty("fullName").GetString() == TargetTypeName);

    Assert.Equal("closed", root.GetProperty("declarationState").GetString());
    Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
    Assert.Equal(136, candidates.Length);
    Assert.Equal(
        "consumer-declaration-pending",
        target.GetProperty("declarationStatus").GetString());
    Assert.Equal(
        "unknown-until-voluntary-declaration",
        target.GetProperty("privateConsumerStatus").GetString());
    Assert.Equal(
        "no-public-match-found",
        target.GetProperty("externalEvidence")
            .GetProperty("searchStatus")
            .GetString());

    var prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
    var blobBytes = new byte[prefix.Length + bytes.Length];
    prefix.CopyTo(blobBytes, 0);
    bytes.CopyTo(blobBytes, prefix.Length);

    Assert.Equal(
        CandidateManifestBlob,
        Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
}
```

Add:

```csharp
private static JsonDocument ReadJson(params string[] relativeSegments)
{
    var path = Path.Combine([RepoRoot, .. relativeSegments]);
    return JsonDocument.Parse(File.ReadAllText(path));
}
```

These are current-state characterization tests and should pass immediately.
Do not create a B2-style failing assertion about 507/103 or non-exported type.

- [ ] **Step 11: Run the complete targeted GREEN**

Run:

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~PluginProtocolClientTests
```

Expected: 16 test cases pass: the original theory contributes two cases,
the other twelve original methods contribute twelve, and the two new facts
contribute two. Zero failures and zero skipped.

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~G04D2B1PluginHostErrorCodeContractTests
```

Expected: four passed tests, zero failures.

- [ ] **Step 12: Write the B1 execution ledger without pre-claiming parent proof**

Create
`docs/analysis/bukit-core-g04d2b1-plugin-host-error-code-contract-migration-2026-07-23.zh-CN.md`
with these sections and exact decisions:

```markdown
# Bukit Core G-04D2B1 PluginHost 错误码 diagnostic-contract migration 执行账本

日期：2026-07-23
基线：`2.0@2272156f054cb308028b57ba50cc65268a454e30`
范围：只迁移 `PluginHostErrorCodes` 的测试与协议词汇证据

## 决策

G-04D2B1 只把测试期望从 `PluginHostErrorCodes` public const CLR 引用迁移到
`PluginProtocolClient` public 入口和独立协议词汇 fixture。生产源码、类型与成员
可见性、六个字符串、异常格式、权限语义、public API baseline 和 closed candidate
manifest 均保持不变。

G-04D2B1 不授权 G-04D2B2，也不预先决定
`Bukit.PluginHost.PluginHostErrorCodes` 可以 internalize。

## 契约分类

- Host 当前实际输出：`plugin.unsupportedProtocol`、
  `plugin.invalidResponse`、`plugin.timeout`、`plugin.executionFailed`、
  `plugin.outputTooLarge`。
- 保留协议词汇：`plugin.permissionDenied`。
- 权限拒绝继续使用 `DiagnosticCode.PluginCapabilityMissing`；
  `plugin.permissionDenied` 没有 Host 生产调用点。

## 测试迁移

记录实际执行的 RED 命令、预期失败原因、GREEN 命令和实际通过计数。
明确记录 `PluginProtocolClientTests.cs` 的 `PluginHostErrorCodes` 标识符为零，
以及 `executionFailed` 与 inbound `permissionDenied` 用例的边界。

## 治理当前态

- public API baseline：14 assemblies / 508 types / 104 candidates；
- `PluginHostErrorCodes` 仍为 exported public type，六个 public const 保持不变；
- closed manifest：136 entries；
- closed manifest blob：
  `7b07d6890562387010b52301e9f8716e9bf10ed1`；
- private consumer 仍为 `unknown-until-voluntary-declaration`。

## 明确排除

列出 Global Constraints 中的全部 production、协议、权限、baseline、manifest、
friendship、replacement API、B2 和备份目录禁区。

## 验证边界

记录 Task 1 已实际完成的测试、owner checks 和 focused check。父任务唯一 aggregate
与最终独立只读复审由 controller 在提交后执行；本账本不得提前声明其通过。

## Stop conditions

逐项记录 design spec 第 9 节的 stop conditions。
```

The prose under each heading must be complete Chinese sentences. Do not
leave the instruction words “记录” or “列出” in the final ledger; replace
them with the actual observed commands, counts and exact exclusions.

- [ ] **Step 13: Verify owner contracts and unchanged protected artifacts**

Run:

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off
```

Expected: 170 passed, zero failed, zero skipped.

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off
```

Expected: 130 passed, zero failed, zero skipped.

Run:

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
bash scripts/checks/docs/active-links.sh
bash scripts/checks/docs/no-absolute-paths.sh
```

Expected: all commands exit 0; public API drift reports no warning/error.

Run:

```bash
git diff --exit-code \
  2272156f054cb308028b57ba50cc65268a454e30 \
  -- src/Bukit-Core
git diff --exit-code \
  2272156f054cb308028b57ba50cc65268a454e30 \
  -- docs/governance/bukit-core-public-api-baseline.v1.json
git diff --exit-code \
  2272156f054cb308028b57ba50cc65268a454e30 \
  -- docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
git hash-object \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Expected: the three diff commands produce no output and exit 0; hash output
is exactly `7b07d6890562387010b52301e9f8716e9bf10ed1`.

- [ ] **Step 14: Run the one code-subtask focused check**

Run exactly once:

```bash
bash scripts/checks/post-change-focused.sh -- \
  docs/superpowers/specs/2026-07-23-bukit-core-g04d2b1-plugin-host-error-code-contract-migration-design.md \
  docs/superpowers/plans/2026-07-23-bukit-core-g04d2b1-plugin-host-error-code-contract-migration.md \
  tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs \
  tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs \
  tests/fixtures/plugin-contracts/plugin-host-error-vocabulary.v1.json \
  docs/analysis/bukit-core-g04d2b1-plugin-host-error-code-contract-migration-2026-07-23.zh-CN.md
```

Expected: exit 0. Do not run `post-change-targeted.sh` in Task 1; the parent
controller owns its single aggregate execution after task review.

- [ ] **Step 15: Self-review, stage, and commit**

Run:

```bash
git diff --check
git status --short
rg -n \
  "T[B]D|T[O]DO|implement lat[e]r|fill in detail[s]|待[补]|待完[成]|稍后补[充]" \
  docs/analysis/bukit-core-g04d2b1-plugin-host-error-code-contract-migration-2026-07-23.zh-CN.md
```

Expected: `git diff --check` exits 0; the placeholder scan has no matches;
status contains only the four approved Task 1 paths; the design spec and
implementation plan are already committed before Task 1 starts.

Stage only:

```bash
git add \
  tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs \
  tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs \
  tests/fixtures/plugin-contracts/plugin-host-error-vocabulary.v1.json \
  docs/analysis/bukit-core-g04d2b1-plugin-host-error-code-contract-migration-2026-07-23.zh-CN.md
```

Commit:

```bash
git commit -m "test(pluginhost): migrate error code contracts"
```

The commit must not contain production, public baseline, closed manifest,
consumer declaration, governance guide, protocol document, security ADR,
schema, CI, release or gate changes.
