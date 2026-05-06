# AOT 与非 AOT 构建模式

本项目同时支持非 AOT（JIT）与 NativeAOT 两种模式。两者在插件能力、发布链路与运行特性上存在明确差异。

## 模式选择

- 非 AOT（JIT）：开发调试、快速迭代、外部 DLL 插件加载。
- AOT：发布部署、冷启动与内存占用优化、可移植单文件产物。

切换方式见 `src/Bukit.Cli/Bukit.Cli.csproj`：

- `Configuration=AOT` 时启用 `PublishAot=true`，并注入编译常量 `AOT`。

## 插件行为差异

`src/Bukit.Engine/Plugins/PluginRegistry.cs`：

- AOT：`built-in` + `generated` + `external-protocol`。
- 非 AOT：`built-in` + `generated` + `external`（扫描 `<rootDir>/plugins/*.dll`）+ `external-protocol`。

结论：

- 依赖外部 DLL 插件时必须使用非 AOT。
- AOT 下如果需要动态扩展，优先使用 `external-protocol`。
- AOT 下如果需要零运行时依赖的内嵌扩展，可继续使用 generated 插件。

### AOT 下的 external-protocol

`external-protocol` 是 AOT 友好的动态扩展方案：

- 主程序不加载外部 DLL
- 通过 `stdin/stdout + JSON` 调用外部插件
- 当前支持 `runtime: process|wasm`，并支持 `after-build|derive-pages`
- 推荐策略：默认保持 `process`，对第三方或低信任插件优先使用 `wasm`，并按插件粒度灰度启用

协议说明见：[external-plugin-protocol.md](./external-plugin-protocol.zh-CN.md)

## AOT 下的自定义插件方式

以下条件同时满足时会被源生成器注册到 `generated` 插件源：

- 实现 `IBukitPlugin`
- 命名空间前缀为 `Bukit.Plugins.`
- 标注 `[BukitPlugin]`

参考：

- `src/Bukit.PluginSourceGenerator/PluginSourceGenerator.cs`
- `src/Bukit.Engine.Abstractions/Plugins/BukitPluginAttribute.cs`

## Scriban AOT 兼容性

Scriban 模板引擎已从 NuGet 包切换为 vendored 源码（`tools/scriban/`），并完成了全面的 AOT 改造：

- 消除了 `CustomFunction.Generated.cs` 中 101 处 `Type.GetMethod` 反射调用，替换为编译期委托方法组获取 `MethodInfo`
- 修复了 `DynamicCustomFunction.cs` 中的 `GetMethod(GetAwaiter)` 反射和 `(dynamic)result` 动态分发
- 为 `ScriptObjectExtensions.Import`、`ScriptObject` 构造函数添加了 trimmer 注解
- 使用 `[DynamicDependency]` 确保内建函数类型成员被 trimmer 保留
- 为 `DictionaryAccessor`、`TypedObjectAccessor` 等未使用的反射路径添加了 `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` 守护
- 将 `ObjectFunctions.ToJson` 中的 `JsonSerializer.Serialize(writer, value, type)` 替换为直接 `Utf8JsonWriter` 调用

**不再需要 `WarningsNotAsErrors` 白名单**，所有 Scriban AOT 告警已从源头消除。

## 发布依赖与符号剥离

在 Linux 目标下，如启用 NativeAOT 符号剥离，需要 `llvm-objcopy` 或 `objcopy` 可用。

当前默认策略：

- `BukitStripSymbols=false`（默认），保证跨环境 publish 可用。
- 如需剥离符号，显式传入 `-p:BukitStripSymbols=true`。
- 若启用剥离但工具缺失，发布阶段会给出明确错误信息并终止。

## Trim/AOT 告警治理策略

所有 AOT 告警已从源头消除，采用零告警策略：

- Scriban（vendored 源码 `tools/scriban/`）：消除了 101 处 `Type.GetMethod` 反射调用、`dynamic` 分发、`JsonSerializer` 等 AOT 不兼容代码，标记为 `IsAotCompatible=true`
- 图像处理能力已按当前仓库依赖做 AOT 兼容治理（详见根目录 `Directory.Packages.props` 与相关实现）
- 不再需要 `WarningsNotAsErrors` 白名单
- CI 脚本 `scripts/check-aot-warnings.sh` 作为零告警防护网运行，任何新增 AOT/Trim 告警即失败
- 如引入新的第三方依赖，需确保其 AOT 兼容或使用条件编译隔离

## 建议验证命令

```bash
# 本机 AOT（示例）
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c AOT -r osx-arm64 -o out/bukit-aot

# Linux 目标（默认不剥离，跨环境更稳定）
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c AOT -r linux-x64 -o out/bukit-linux

# Linux 目标 + 符号剥离（需 objcopy/llvm-objcopy）
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c AOT -r linux-x64 -o out/bukit-linux -p:BukitStripSymbols=true
```
