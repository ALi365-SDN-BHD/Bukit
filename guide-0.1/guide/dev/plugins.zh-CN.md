# 插件体系（derive-pages / after-build）

插件是 Bukit 的主要扩展点。它允许在不修改引擎主流程的前提下增加：派生页面、以及非核心 publish projection 的构建后附加产物。

实现参考：
- 插件接口：`src/Bukit.Engine.Abstractions/Plugins/*`
- 执行器：`src/Bukit.Engine/Plugins/PluginRunner.cs`
- 注册与加载：`src/Bukit.Engine/Plugins/PluginRegistry.cs`
- 插件源生成：`src/Bukit source generation`

## 生命周期与能力边界

### 1) DerivePages（派生页）

接口：`IDerivePagesPlugin.DerivePages(BuildContext)`

作用：
- 基于现有的 routed 内容，派生额外页面（例如 tags/categories 列表页）
- 派生页返回 `(ContentDocument, RouteInfo, LastModified)`，会进入渲染队列

注意：
- 派生页的 `RouteInfo` 应避免与已有路由冲突
- 派生页可被纳入 sitemap/rss/search（取决于对应插件与配置策略）

冲突策略：
- 可通过 `site.deriveConflictPolicy` 配置冲突行为：`fail|warn|last-wins`
- `fail`：发现 URL 或 outputPath 冲突即抛错
- `warn`：记录警告并跳过冲突派生页
- `last-wins`：允许冲突派生页继续进入渲染队列（后写覆盖）

### 2) AfterBuild（构建后）

接口：`IAfterBuildPlugin.AfterBuild(BuildContext)`

作用：
- 在所有页面渲染完成后，生成自定义附加文件。核心机器可读产物（sitemap/feed/search/llms/robots/agent-manifest）由 publish projection pipeline 统一生成。

### 3) Publish Projections（发布投影）

接口：`IPublishProjection`

作用：
- 从同一个 content graph / route inventory 生成 canonical publish representations
- 内置 aggregate outputs 通过 `PublishRepresentationRegistry` 注册，包括 `sitemap.xml`、RSS/Atom/JSON Feed、`search.json`、`llms.txt`、`llms-full.txt`、`robots.txt`、`agent-manifest.json`

## 失败策略：site.pluginFailMode

插件执行器遵循 `site.pluginFailMode`：
- `strict`：插件抛错会中断构建
- `warn`：记录错误并继续后续插件

实现点：`PluginRunner` 中会根据 failMode 决定是否 rethrow。

## 插件发现与加载方式

插件来源分四类（`PluginRegistry`）：

1. built-in：内置插件（引擎自带）
2. generated：编译期生成的插件源（用于 AOT 与内置插件开发）
3. external：运行时加载 `plugins/*.dll`（非 AOT 模式才启用）
4. external-protocol：通过 `stdin/stdout + JSON` 调用的外部协议插件（AOT 兼容）

关于 AOT 与非 AOT 的行为差异（尤其是 external 插件加载在 AOT 下不可用），见 [AOT 与非 AOT 构建模式](./aot.zh-CN.md)。

### generated（编译期发现规则）

源生成器会扫描满足以下条件的类型并生成 `GeneratedPluginSource`：

- 实现 `IBukitPlugin`
- 命名空间以 `Bukit.Plugins.` 开头
- 标注 `[BukitPlugin]` 特性

这意味着：如果你在仓库内开发插件，可将源码放在 `plugins/<PluginName>/` 或 `src/plugins/`，并遵循上述命名空间与特性要求。

### external（运行时加载）

非 AOT 模式下，引擎会扫描 `<rootDir>/plugins/*.dll`：

- 加载程序集
- 遍历可加载类型
- 找出实现 `IBukitPlugin` 的非抽象类型
- 通过无参构造 `Activator.CreateInstance` 实例化
- 可选启用 DLL 信任治理：`site.externalAssemblyTrustMode`（`warn|strict`）+ `site.externalAssemblyAllowlist`（`文件名 -> SHA256`）

### external-protocol（AOT 兼容）

`external-protocol` 是 AOT 下推荐的动态扩展方式：

- 通过配置 `site.externalPlugins`
- 当前支持 `runtime: process|wasm`
- 当前支持 `after-build` 与 `derive-pages`
- after-build 支持 `handshake` 协商，当前默认以 `protocol-v2` 为主
- `site.externalProtocolIncludeRoutedPages` 默认 `false`，可按需开启 after-build 全量 routedPages 传输
- 同一构建上下文内，after-build 握手协商结果会缓存复用
- `options.arguments` 已禁用；请改用 `options.processArgs`
- 若启用 `site.externalAssemblyTrustMode: strict`，必须提供 `site.externalAssemblyAllowlist`
- wasm 资源治理：`maxMemoryMb`、`wasmFsMode`、`wasmAllowNetwork`（当前仅允许禁网）

示例配置：

```yaml
site:
  externalProtocolIncludeRoutedPages: true
  externalPlugins:
    sample:
      runtime: process
      entry: plugins/sample-plugin.exe
      hooks: [after-build]
      timeoutMs: 5000
```

详见：[external-plugin-protocol.md](./external-plugin-protocol.md)

### 外部协议插件安全

外部协议插件在**环境隔离**下运行：宿主环境变量被清空，仅注入 `BUKIT_PLUGIN_NAME`、`BUKIT_PLUGIN_HOOK`、`BUKIT_PROJECT_ROOT`、`BUKIT_OUTPUT_DIR`。使用 `allowEnvironment` 可显式透传额外宿主变量。

输出限制（`maxStdoutBytes` / `maxStderrBytes`）可限制插件 stdout/stderr 最大字节数；超限则 kill 进程。所有插件输出以 plugin/hook/path/hash 元数据记录在构建清单中，增量构建时自动清理旧输出。

### 插件能力强制

外部插件可以声明 `capabilities` 列表作为沙箱：

```yaml
site:
  externalPlugins:
    my-plugin:
      capabilities:
        - derive-pages   # hooks: [derive-pages] 必需
        - emit-outputs   # hooks: [after-build] 必需
```

实现：`src/Bukit.Engine/Plugins/PluginCapability.cs`、`src/Bukit.Engine/Plugins/PluginCapabilityEnforcer.cs`。

**执行规则：**
- `capabilities` 未声明 → 报错（`ConfigException` / `BKT-0701`）
- `capabilities` 已声明 → 运行时检查每个 hook 是否匹配能力列表
- Hook 缺少所需能力 → `ConfigException`，错误码 `BKT-0701`
- 无效能力名称 → 配置验证时 `ConfigException`

能力检查已集成到 `ExternalProtocolPlugin.DerivePagesAsync()` 和 `ExternalProtocolPlugin.AfterBuildAsync()` 中，在调用协议调用器之前执行。

详见 [External Plugin Protocol](./external-plugin-protocol.md)。

## 内置插件一览（BuiltIn）

内置插件当前包括（见 `BuiltInPluginSource`）：

- `taxonomy`：根据 record.tags/record.categories 派生 `/tags/` 与 `/categories/`（IDerivePagesPlugin）
- `pagination`：分页类派生/输出（视实现）
- `archive`：归档类派生/输出（视实现）

说明：
- 具体输出策略与文件名以各插件实现为准：`src/Bukit.Engine/Plugins/BuiltIn/*`
- sitemap/feed/search/llms/robots/agent-manifest 的默认 owner 是 publish projection adapter，不是 after-build 插件。
- 输出契约与多语言边界的汇总见 [内置插件（BuiltIn）产物与边界](./built-in-plugins.zh-CN.md)。

## 插件开发建议（契约优先）

- 把“对外可配置项”放进 `site.yaml` 的稳定字段（或以 `theme.params` 形式注入模板）
- 插件输出的 URL 与 outputPath 建议固定规则，并与 baseUrl/i18n 兼容
- 插件执行耗时会被记录到 metrics（若启用 `--metrics`），便于 CI 性能回归

## 插件执行顺序（Order）

当插件实现 `IOrderedPlugin` 时，会按 `Order` 从小到大执行；未实现时默认为 `0`。同优先级时按 `Name`、`Version` 排序。

```csharp
public sealed class MyPlugin : IBukitPlugin, IAfterBuildPlugin, IOrderedPlugin
{
    public int Order => 100;
}
```

例如 `path-report` 这类 after-build 插件也可以通过实现 `IOrderedPlugin` 放到执行链后段。

## 插件配置（site.plugins）

插件开关和参数统一由 `site.plugins` 提供：

```yaml
site:
  plugins:
    path-report:
      enabled: true
      options: {}
```

- `enabled`：是否启用插件。
- `options`：插件自定义参数字典，具体键由插件实现决定。
- 兼容旧写法：`site.plugins.sitemap: false`。
