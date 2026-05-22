# Bukit AOT-Only + 插件系统简化 实施计划

## 概述

两个目标：

1. **仅 AOT 编译**：去除所有 JIT 代码路径和 `#if AOT` / `#if !AOT` 条件编译
2. **插件系统精简**：仅保留 `built-in`（8个内置插件）+ `external-protocol`（process 运行时），移除 generated/外部程序集/wasm 插件类型，将已实现的示例插件转为 process 协议插件

> **重要原则**：废弃的代码仅做注释处理（`#if false` 包裹），**不删除任何代码文件**。

***

## 第一阶段：AOT-Only 化（去除 JIT 条件编译）

### 1.1 Bukit.Cli.csproj — 永久启用 AOT 属性

**文件**： [Bukit.Cli.csproj](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Bukit.Cli.csproj)（L56-L66）

**改动**：

* 移除 `Condition="'$(Configuration)' == 'AOT'"`，将 `<PublishAot>`、`<PublishSingleFile>`、`<InvariantGlobalization>` 等属性设为无条件启用

* 移除 `#define AOT` 的 `<DefineConstants>`（不再需要条件编译常量）

* 移除 `ValidateStripSymbolsTooling` Target 中 `Condition` 的 `== 'AOT'` 检查

### 1.2 Bukit.Engine.csproj — 去除条件依赖和条件编译

**文件**： [Bukit.Engine.csproj](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Bukit.Engine.csproj)（L17-L28）

**改动**：

* 注释掉 `<PackageReference Include="Wasmtime" Condition="'$(Configuration)' != 'AOT'" />`

* 注释掉 `<ItemGroup Condition="'$(Configuration)' == 'AOT'">` 中对 `..\plugins\*\*.cs` 的编译包含（插件不再内联编译）

* 注释掉 `<ItemGroup Condition="'$(IncludeSamplePlugins)' == 'true'">` 中的 `<Compile Include="..\plugins\*\*.cs" />`

### 1.3 Directory.Packages.props — 注释 Wasmtime 包版本

**文件**：`Directory.Packages.props`

**改动**：

* 注释掉 `Wasmtime` 的 `<PackageVersion>` 条目（保留以备将来参考）

### 1.4 PluginRegistry.cs — 统一插件源列表

**文件**： [PluginRegistry.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/PluginRegistry.cs)

**改动**：

* 顶部 `#if !AOT` 的 using 语句改为 `#if false` 注释（L1-L6：`ConcurrentDictionary`、`Reflection`、`SHA256`、`AssemblyLoadContext`）

* 整个 `ExternalAssemblyPluginSource` 类（L34-L231）用 `#if false` 包裹注释

* `BuildPlugins` 方法中（L279-L294）移除 `#if AOT / #else / #endif`，统一使用以下插件源列表：

  ```csharp
  var sources = new (IPluginSource Source, string Name)[]
  {
      (new BuiltInPluginSource(), "built-in"),
      (new ExternalProtocolPluginSource(context), "external-protocol")
  };
  ```

  * **注意**：`GeneratedPluginSource` 不再出现在插件源列表中

### 1.5 GeneratedPluginSource.cs — 用 `#if false` 注释整个文件内容

**文件**： [GeneratedPluginSource.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/GeneratedPluginSource.cs)

**改动**：

* 将 `#if !AOT ... #endif` 改为 `#if false ... #endif`，保留代码

### 1.6 WasmPluginInvoker.cs — 用 `#if false` 注释整个文件内容

**文件**： [WasmPluginInvoker.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/WasmPluginInvoker.cs)

**改动**：

* 将整个文件内容用 `#if false ... #endif` 包裹注释（包含 AOT 存根和 Wasmtime 实现）

* 文件头部添加注释说明：wasm 运行时已禁用，仅保留 process 运行时

### 1.7 ExternalProtocolPluginSource.cs — 移除 wasm 分支条件编译

**文件**： [ExternalProtocolPluginSource.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ExternalProtocolPluginSource.cs)

**改动**：

* `CreateInvoker` 方法（L82-L98）：移除 `#if AOT` 条件编译

* 将 `wasm` 分支改为 `#if false` 注释保留

* 仅保留 `process` 分支作为有效路径，非 process 抛出 `NotSupportedException`

### 1.8 VersionCommand.cs — 移除版本信息的条件编译

**文件**： [VersionCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/VersionCommand.cs)（L18-L22）

**改动**：

* 移除 `#if AOT / #else / #endif`，始终输出 `runtime: native-aot`

### 1.9 PluginSourceGenerator.cs — 移除生成代码中的 `#if AOT` 包装

**文件**： [PluginSourceGenerator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.PluginSourceGenerator/PluginSourceGenerator.cs)（L101-L126）

**改动**：

* `GenerateSource` 方法中移除 `#if AOT` / `#endif` 包装

* **注意**：整个 PluginSourceGenerator 项目在第二阶段将整体用 `#if false` 注释

### 1.10 测试文件 — 注释外部程序集测试

**文件**： [ExternalAssemblyPluginSourceTests.cs](file:///Users/ali/mydev/Git/Github/Bukit/tests/Bukit.Engine.Tests/ExternalAssemblyPluginSourceTests.cs)

**改动**：

* 用 `#if false` 包裹整个文件内容注释

### 1.11 CI/CD 更新

**文件**：`.github/workflows/ci.yml`

**改动**：

* 添加 AOT publish 验证步骤：`dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release -r linux-x64`

**文件**：`.github/workflows/release.yml`

**改动**：

* 将 `-c AOT` 改为 `-c Release`（AOT 属性已无条件启用）

**文件**：`scripts/smoke.sh`

**改动**：

* 检查并将 `-c AOT` 改为 `-c Release`

**文件**：`scripts/check-aot-warnings.sh`

**改动**：

* 将 `dotnet publish -c AOT` 改为 `dotnet publish -c Release`

***

## 第二阶段：插件系统精简

### 2.1 注释 Generated Plugin Source

**文件**： [GeneratedPluginSource.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/GeneratedPluginSource.cs)

**改动**：

* 已在 1.5 中处理（`#if false` 注释）

**文件**： [Bukit.Engine.csproj](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Bukit.Engine.csproj)

**改动**：

* 用 XML 注释 `<!-- ... -->` 包裹对 `Bukit.PluginSourceGenerator` 的分析器引用

### 2.2 注释 PluginSourceGenerator 项目

**文件**： [PluginSourceGenerator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.PluginSourceGenerator/PluginSourceGenerator.cs)

**改动**：

* 用 `#if false ... #endif` 包裹整个文件内容注释

### 2.3 注释 BukitPluginAttribute

**文件**： [BukitPluginAttribute.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine.Abstractions/Plugins/BukitPluginAttribute.cs)

**改动**：

* 用 `#if false ... #endif` 包裹文件内容注释

* 添加说明：`[BukitPlugin]` 标记属性已废弃，插件改用 process 协议

### 2.4 注释 PluginSourceGenerator 测试

**文件**： [PluginSourceGeneratorTests.cs](file:///Users/ali/mydev/Git/Github/Bukit/tests/Bukit.PluginSourceGenerator.Tests/PluginSourceGeneratorTests.cs)

**改动**：

* 用 `#if false ... #endif` 包裹整个文件内容注释

### 2.5 清理 External Plugin 配置模型（移除 wasm 专用字段）

**文件**： [AppConfig.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs)

**改动**：

* `ExternalPluginConfig` 记录中注释掉以下 wasm 专用字段（用 `//` 单行注释）：

  * `WasmProfile`

  * `MaxMemoryMb`

  * `WasmFsMode`

  * `WasmAllowNetwork`

  * `Capabilities`

**文件**： [ConfigValidator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigValidator.cs)（L533-L632）

**改动**：

* 将 wasm runtime 的验证逻辑（L587-L631）用 `#if false` 包裹注释

* 将 `runtime` 合法值检查从 `"process" || "wasm"` 改为仅 `"process"`

### 2.6 注释 SiteConfig 废弃字段

**文件**： [AppConfig.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs)

**改动**：

* 注释掉 `SiteConfig` 中的字段：

  * `ExternalAssemblyTrustMode`

  * `ExternalAssemblyAllowlist`

### 2.7 将示例插件转为 Process 协议插件

这是最核心的转换工作。两个现有示例插件需要改造为独立可执行程序，通过 stdin/stdout JSON 协议与 Bukit 引擎通信。

#### 2.7.1 创建 process 插件协议基类/辅助库

为了让 process 插件开发更容易，在 `src/Bukit.Engine.Abstractions/Plugins/Protocol/` 下新增一个辅助类 `ProcessPluginHost`，封装协议通信细节（stdin 读取、握手、hook 分发、stdout 写入），供 process 插件继承使用。

**新增文件**：`src/Bukit.Engine.Abstractions/Plugins/Protocol/ProcessPluginHost.cs`

**核心功能**：

```csharp
public abstract class ProcessPluginHost
{
    // 子类实现：返回插件名称
    protected abstract string PluginName { get; }
    // 子类实现：返回插件版本
    protected abstract string PluginVersion { get; }
    // 子类实现：返回支持的 hook 列表
    protected abstract IReadOnlyList<string> SupportedHooks { get; }
    // 子类实现：处理 after-build hook
    protected virtual Task AfterBuildAsync(AfterBuildPayload payload, CancellationToken ct)
        => Task.CompletedTask;
    // 子类实现：处理 derive-pages hook
    protected virtual Task<DerivePagesResponse> DerivePagesAsync(DerivePagesPayload payload, CancellationToken ct)
        => Task.FromResult(new DerivePagesResponse { Items = [] });

    // 内置方法：启动协议主循环
    public async Task RunAsync(CancellationToken ct = default)
    {
        // 1. 等待 stdin 输入
        // 2. 解析 JSON 请求
        // 3. 如果是握手请求，返回握手响应
        // 4. 如果是 hook 调用，分发到对应的虚方法
        // 5. 将响应写入 stdout
    }
}
```

#### 2.7.2 SampleAfterBuildPlugin → 独立 process 插件

**当前状态**：

* 位置：`src/plugins/SampleAfterBuildPlugin/`

* 内联编译到 Bukit.Engine

* 使用 `[BukitPlugin]` 标记

* `IAfterBuildPlugin` 空实现

**转换后**：

* 重写 `SampleAfterBuildPlugin.cs`：

  * 继承 `ProcessPluginHost`

  * 实现 `PluginName`、`PluginVersion`、`SupportedHooks`

  * 移除 `[BukitPlugin]` 和 `IBukitPlugin` / `IAfterBuildPlugin` 接口

* 用户通过 `site.externalPlugins` 配置：

  ```yaml
  externalPlugins:
    sample-after-build:
      runtime: process
      entry: plugins/SampleAfterBuildPlugin
      hooks:
        - after-build
  ```

#### 2.7.3 PathReportPlugin → 独立 process 插件

**当前状态**：

* 位置：`src/plugins/PathReportPlugin/`

* 内联编译到 Bukit.Engine

* 使用 `[BukitPlugin]` 标记

* `IBukitPlugin + IAfterBuildPlugin + IOrderedPlugin + IDisposable`

* 生成 `_debug/paths-report.json` 和可选的微信素材上传

**转换后**：

* 重写 `PathReportPlugin.cs`：

  * 继承 `ProcessPluginHost`

  * 实现 `PluginName`、`PluginVersion`、`SupportedHooks`

  * 将 `AfterBuild` 方法逻辑迁移到 `AfterBuildAsync`，通过 `AfterBuildPayload` 获取 `outputDir`/`rootDir`/`config` 等信息

  * 移除 `[BukitPlugin]` 和 Bukit 插件接口

  * 保留 `WechatMaterialUploader` 依赖

* 用户通过 `site.externalPlugins` 配置：

  ```yaml
  externalPlugins:
    path-report:
      runtime: process
      entry: plugins/PathReportPlugin
      hooks:
        - after-build
      options:
        processArgs: {}
        wechatMaterialUpload:
          enabled: false
  ```

### 2.8 更新解决方案文件

**文件**：`bukit.slnx`

**改动**：

* 用 XML 注释 `<!-- ... -->` 包裹 `PluginSourceGenerator` 和 `PluginSourceGenerator.Tests` 项目引用（保留条目，仅注释）

***

## 第三阶段：测试与验证

### 3.1 更新测试

#### 注释的测试文件（`#if false`）：

* `tests/Bukit.Engine.Tests/ExternalAssemblyPluginSourceTests.cs` — 外部程序集源不再启用

* `tests/Bukit.PluginSourceGenerator.Tests/PluginSourceGeneratorTests.cs` — 源码生成器不再启用

#### 需修改的测试文件：

* `tests/Bukit.Engine.Tests/ConfigValidatorTests.cs` — 注释掉 wasm runtime 验证测试（`#if false`）

* `tests/Bukit.Config.Tests/ConfigLoaderTests.cs` — 注释掉 ExternalAssembly 相关测试（`#if false`）

* `tests/Bukit.Cli.Tests/PluginCommandTests.cs` — 注释掉外部程序集输出验证（`#if false`）

* `tests/Bukit.Engine.Tests/ExternalProtocolPluginTests.cs` — 确保 wasm runtime 测试被注释

### 3.2 编译验证

```bash
dotnet build bukit.slnx -c Release
dotnet test bukit.slnx -c Release
```

### 3.3 AOT 发布验证

```bash
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release -r osx-arm64 -o out/aot-test
./scripts/check-aot-warnings.sh
```

### 3.4 process 插件协议验证

* 编译 `PathReportPlugin` 和 `SampleAfterBuildPlugin` 为独立可执行文件

* 在测试站点中配置 `externalPlugins` 使用 process 运行时

* 验证插件正确接收请求并返回响应

***

## 第四阶段：文档清理

### 4.1 开发者文档更新

需要更新的文件：

* `guide/dev/aot.md` — 移除 JIT 描述，仅保留 AOT 说明

* `guide/dev/aot.zh-CN.md` — 同上

* `guide/dev/plugins.md` — 移除 generated/external-assembly/wasm 插件描述，添加 "已废弃" 标注

* `guide/dev/plugins.zh-CN.md` — 同上

* `guide/dev/external-plugin-protocol.md` — 移除 wasm 运行时说明，标注 wasm 已废弃

* `guide/dev/external-plugin-protocol.zh-CN.md` — 同上

* `guide/dev/external-plugin-protocol.ms.md` — 同上

* `guide/dev/config-site-yaml.md` — 标注 `externalAssemblyAllowlist`、`externalAssemblyTrustMode` 为废弃字段

* `guide/dev/new-developer-30min.md` — 移除 `[BukitPlugin]` 相关内容

* `guide/dev/new-developer-30min.ms.md` — 同上

### 4.2 Skills 更新

* `src/skills/bukit-plugins-debug/SKILL.md` — 标注 generated/external-assembly 源为已废弃

* `src/skills/bukit-config/SKILL.md` — 标注 `externalAssemblyAllowlist` 为废弃配置项

***

## 文件变更清单汇总

### 注释处理（`#if false` 包裹，保留代码）

| 文件                                                                          | 内容                                       |
| --------------------------------------------------------------------------- | ---------------------------------------- |
| `src/Bukit.Engine/Plugins/PluginRegistry.cs` L1-L6                          | `ExternalAssemblyPluginSource` 所需的 using |
| `src/Bukit.Engine/Plugins/PluginRegistry.cs` L34-L231                       | `ExternalAssemblyPluginSource` 整个类       |
| `src/Bukit.Engine/Plugins/GeneratedPluginSource.cs`                         | 整个类                                      |
| `src/Bukit.Engine/Plugins/Protocol/WasmPluginInvoker.cs`                    | 整个类（含 AOT 存根和 JIT 实现）                    |
| `src/Bukit.Engine/Plugins/Protocol/ExternalProtocolPluginSource.cs` wasm 分支 | `CreateInvoker` 中 wasm case              |
| `src/Bukit.PluginSourceGenerator/PluginSourceGenerator.cs`                  | 整个类                                      |
| `src/Bukit.Engine.Abstractions/Plugins/BukitPluginAttribute.cs`             | 整个类                                      |
| `tests/Bukit.Engine.Tests/ExternalAssemblyPluginSourceTests.cs`             | 整个文件                                     |
| `tests/Bukit.PluginSourceGenerator.Tests/PluginSourceGeneratorTests.cs`     | 整个文件                                     |
| `tests/Bukit.Engine.Tests/ConfigValidatorTests.cs` wasm 测试                  | 相关测试方法                                   |
| `tests/Bukit.Config.Tests/ConfigLoaderTests.cs` ExternalAssembly 测试         | 相关测试方法                                   |

### XML 注释（`.csproj` 和 `.slnx`）

| 文件                                     | 内容                         |
| -------------------------------------- | -------------------------- |
| `src/Bukit.Engine/Bukit.Engine.csproj` | Wasmtime 包引用、插件编译包含、分析器引用  |
| `Directory.Packages.props`             | Wasmtime 版本条目              |
| `bukit.slnx`                           | PluginSourceGenerator 项目引用 |

### 单行注释（`//`）

| 文件                              | 内容                            |
| ------------------------------- | ----------------------------- |
| `src/Bukit.Config/AppConfig.cs` | wasm 专用字段、ExternalAssembly 字段 |

### 需修改的源代码

| 文件                                                                  | 变更                      |
| ------------------------------------------------------------------- | ----------------------- |
| `src/Bukit.Cli/Bukit.Cli.csproj`                                    | 无条件启用 AOT               |
| `src/Bukit.Engine/Bukit.Engine.csproj`                              | 注释条件依赖                  |
| `src/Bukit.Engine/Plugins/PluginRegistry.cs`                        | 统一插件源列表，注释废弃代码          |
| `src/Bukit.Engine/Plugins/Protocol/ExternalProtocolPluginSource.cs` | 移除 wasm 分支              |
| `src/Bukit.Cli/Commands/VersionCommand.cs`                          | 移除条件编译                  |
| `src/Bukit.Config/AppConfig.cs`                                     | 注释废弃字段                  |
| `src/Bukit.Config/ConfigValidator.cs`                               | 注释 wasm 验证              |
| `.github/workflows/ci.yml`                                          | 添加 AOT publish 验证       |
| `.github/workflows/release.yml`                                     | `-c AOT` → `-c Release` |
| `scripts/smoke.sh`                                                  | `-c AOT` → `-c Release` |
| `scripts/check-aot-warnings.sh`                                     | `-c AOT` → `-c Release` |

### 需重写的文件

| 文件                                                                    | 变更                                      |
| --------------------------------------------------------------------- | --------------------------------------- |
| `src/Bukit.Engine.Abstractions/Plugins/Protocol/ProcessPluginHost.cs` | **新增**：process 插件协议基类                   |
| `src/plugins/SampleAfterBuildPlugin/SampleAfterBuildPlugin.cs`        | 改为继承 `ProcessPluginHost` 的 process 协议插件 |
| `src/plugins/PathReportPlugin/PathReportPlugin.cs`                    | 改为继承 `ProcessPluginHost` 的 process 协议插件 |

