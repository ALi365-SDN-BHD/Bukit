# P2-2 设计规范：DevCommand 责任拆分

> **关联审计**：`.trae/documents/bukit-deep-audit-report-2026-05-29.md` 第七节 P2-2
> **目标文件**：`src/Bukit.Cli/Commands/DevCommand.cs`（501 行 God Static Class）
> **方案选型**：方案 A 保守拆分 + 接口化 + 顺带修复 FileSystemWatcher GC 泄漏 / async void / sync IO
> **并行策略**：2 个 sub coding agent（Agent-A: Server+WS；Agent-B: Watcher+Handler）

---

## 一、问题陈述

`DevCommand` 当前承担 6 类正交职责，且包含 3 个新发现的隐患：

| 序号 | 职责 | 行号 | 隐患 |
|---|---|---|---|
| ① | CLI 选项解析（CliBoundCommand + 手写 switch） | 27–68 | 与 P3-7 历史债重叠（不在本 PR 范围） |
| ② | 主编排（Build→Listener→Watcher→AcceptLoop） | 70–135 | 保留为 orchestrator |
| ③ | HTTP Listener 创建 + 端口探测 | 137–173 | 独立性强 |
| ④ | HTTP AcceptLoop + 路由分发 | 186–215 | 与 ⑤⑦ 强耦合 |
| ⑤ | WebSocket 升级 / 广播 / 客户端注册表 | 24, 217–280 | **`_wsClients` / `_devPort` 是 `static`**（违反 BKT-01 修复原则） |
| ⑥ | 文件监视 + 防抖 + 增量重建 | 282–385 | **`FileSystemWatcher` 未保留引用**（GC 后事件丢失）+ **`async void`** 异常上抛崩溃 |
| ⑦ | 静态文件请求 + livereload 注入 + MIME + 越界防御 | 387–474 | **同步 IO** 阻塞 `ThreadPool`；**裸 `catch`** 吞错误；手写 `StartsWith` 越界校验（未复用 `BuildPathUtils`） |
| ⑧ | 分析脚本配置遍历 | 476–500 | 工具方法，可保留或独立 |

---

## 二、设计目标

1. **每个类单一职责**，可独立理解与单测
2. **消除静态可变状态**（与 ComponentFunctions / 主题系统已重构的方向一致）
3. **修复 3 个关联 bug**：FileSystemWatcher GC 泄漏、async void、同步 IO
4. **保留测试反射访问的方法签名**：`ExtractOptions(CliBoundCommand)` 与 `CreateBuildOverrides(bool, string?, string)` 必须保持原签名（被 `DevCommandTests.cs` 反射调用）
5. **不引入跨程序集依赖变化**（保持 `DependencyMatrixTests` 通过）
6. **代码风格遵循**：`Nullable enable`、`internal sealed class` 默认、构造函数注入

---

## 三、目标架构

```
src/Bukit.Cli/Commands/
├── DevCommand.cs                 (~140 行，orchestrator + 选项解析 + 分析配置遍历)
└── Dev/
    ├── IDevWebSocketHub.cs        (接口：BroadcastReloadAsync / TryUpgradeAsync)
    ├── DevWebSocketHub.cs         (~80 行，实例化 client 注册表 + 升级 + 广播)
    ├── IDevServerHost.cs          (接口：StartListener / RunAcceptLoopAsync)
    ├── DevServerHost.cs           (~80 行，HttpListener + 端口探测 + AcceptLoop)
    ├── DevRequestHandler.cs       (~110 行，静态文件 + livereload 注入 + MIME + 越界防御)
    └── DevFileWatcher.cs          (~120 行，FileSystemWatcher 集合 + 防抖 + IDisposable)
```

### 3.1 `IDevWebSocketHub` / `DevWebSocketHub`

```csharp
internal interface IDevWebSocketHub
{
    Task HandleUpgradeAsync(HttpListenerContext context, CancellationToken ct);
    Task BroadcastReloadAsync();
    int ClientCount { get; }  // 用于测试断言
}

internal sealed class DevWebSocketHub : IDevWebSocketHub
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger _logger;
    public DevWebSocketHub(ILogger logger) { ... }
    // 实现：所有静态状态变实例字段；catch 改 catch (Exception ex) 并 logger.Warn
}
```

**修复点**：
- `_wsClients` static → 实例 `_clients`
- `HandleWebSocketUpgradeAsync` 裸 `catch {}` → `catch (Exception ex) { _logger.Warn(...) }`
- 死客户端清理逻辑保留

### 3.2 `IDevServerHost` / `DevServerHost`

```csharp
internal interface IDevServerHost : IDisposable
{
    int Port { get; }
    string Prefix { get; }
    Task RunAcceptLoopAsync(Func<HttpListenerContext, Task> dispatchAsync, CancellationToken ct);
}

internal sealed class DevServerHost : IDevServerHost
{
    public static DevServerHost Start(string host, int requestedPort, ILogger logger);
    // 内部：PickFreePort + 端口冲突探测（最多 20 次）+ HttpListener
}
```

**修复点**：
- 把 `_devPort` 静态变实例 `Port` 属性
- AcceptLoop 通过委托接收请求分发函数（解耦 WS 路由 vs 文件路由）

### 3.3 `DevRequestHandler`

```csharp
internal sealed class DevRequestHandler
{
    public DevRequestHandler(string outputDir, int livereloadPort, bool disableAnalytics, ILogger logger);
    public Task HandleAsync(HttpListenerContext context, CancellationToken ct);
}
```

**修复点**：
- `File.ReadAllText` → `await File.ReadAllTextAsync(ct)`
- `fs.CopyTo` → `await fs.CopyToAsync(stream, ct)`
- 手写 `StartsWith` 越界 → `BuildPathUtils.MakeAbsolute(outputDir, relative, enforceWithinRoot: true)` 配 `try/catch (ConfigException)` → 403
- 裸 `catch {}` → `catch (Exception ex) { _logger.Warn(...) }`
- `disableAnalytics` 通过构造函数注入（不在请求处理内做配置遍历）

> **决策**：`BuildPathUtils` 位于 `Bukit.Engine` 命名空间且为 `internal static`，跨程序集不可见。本 PR **不依赖** `BuildPathUtils`，而是在 `src/Bukit.Cli/Commands/Dev/DevPathGuard.cs` 新增等价小工具（约 15 行：规范化路径 + boundary check + trailing separator 检查）。这样避免破坏 `DependencyMatrixTests` 的层间约束，且 `DevPathGuard` 可单独测试。

### 3.4 `DevFileWatcher`

```csharp
internal sealed class DevFileWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();   // ★ 保留引用修 GC 泄漏
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);
    private int _pending;

    public DevFileWatcher(IReadOnlyList<string> dirs, ILogger logger, Func<string, CancellationToken, Task> onRebuildAsync);
    public void Start(CancellationToken ct);
    public void Dispose();   // 释放所有 watcher + semaphore
}
```

**修复点**：
- `var watcher = new ...` (GC 风险) → 加入 `_watchers` 列表
- `async void ScheduleRebuild` → `private Task ScheduleRebuildAsync(string)` + 调用点 `_ = ScheduleRebuildAsync(...)`，内部 `try/catch (Exception ex) { _logger.Error(...) }`（仍是 fire-and-forget，但异常被捕获不再上抛崩溃）
- 防抖逻辑（`Interlocked.Increment/Decrement` + `Task.Delay(300)`）保留
- `Dispose` 释放所有 FileSystemWatcher

### 3.5 `DevCommand`（orchestrator，约 140 行）

```csharp
public static class DevCommand
{
    // ★ 保留测试反射依赖的签名
    internal static (...) ExtractOptions(CliBoundCommand command) { ... }
    internal static ConfigOverrides CreateBuildOverrides(bool clean, string? outputOverride, string cacheDir) { ... }

    public static async Task<int> RunAsync(CliBoundCommand command) { ... }
    public static async Task<int> RunAsync(string[] args) { ... }   // 手写解析保留（P3-7 不在范围）

    private static async Task<int> RunCoreAsync(...)
    {
        // 1. 解析配置
        // 2. 初次构建
        // 3. var hub = new DevWebSocketHub(logger);
        // 4. using var host = DevServerHost.Start(host, port, logger);
        // 5. var handler = new DevRequestHandler(outputDir, host.Port, ResolveDisableAnalytics(rootDir), logger);
        // 6. using var watcher = new DevFileWatcher(dirs, logger, async (path, ct) => { await engine.BuildAsync(...); await hub.BroadcastReloadAsync(); });
        //    if (!noWatch) watcher.Start(ct);
        // 7. _ = host.RunAcceptLoopAsync(ctx => ctx.Request.Url?.AbsolutePath == "/__ws__"
        //                                       ? hub.HandleUpgradeAsync(ctx, ct)
        //                                       : handler.HandleAsync(ctx, ct), ct);
        // 8. await Task.Delay(Timeout.Infinite, ct);
    }

    private static bool ResolveDisableAnalytics(string dir) { ... }  // 保留（小工具）
    private static List<string> ResolveWatchDirs(string rootDir, AppConfig config) { ... }  // 保留
}
```

---

## 四、数据流

```
[user CLI] ─► DevCommand.RunAsync
                │
                ├─► initial BuildAsync (engine)
                │
                ├─► DevServerHost.Start  ──► HttpListener (port discovery)
                │
                ├─► DevWebSocketHub      ──► ConcurrentDictionary<string, WebSocket>
                │
                ├─► DevRequestHandler    ──► File IO (async) + livereload script
                │
                ├─► DevFileWatcher       ──► FileSystemWatcher × N (debounced)
                │       on change ──► engine.BuildAsync ──► hub.BroadcastReloadAsync
                │
                └─► host.RunAcceptLoopAsync(dispatch)
                          ├─► /__ws__   ──► hub.HandleUpgradeAsync
                          └─► other     ──► handler.HandleAsync
```

---

## 五、错误处理

| 场景 | 处理策略 |
|---|---|
| 端口占用 | `DevServerHost` 尝试 20 个候选端口，全失败抛 `InvalidOperationException` |
| FileSystemWatcher 错误事件 | `logger.Warn("dev.filewatcher: ...")` |
| 重建失败 | `logger.Error("dev.rebuild.error: ...")`，不影响后续重建 |
| WebSocket 升级失败 | `logger.Warn("dev.ws.upgrade: ...")` |
| 文件 404 / 路径越界 | HTTP 状态码 403/404，无堆栈泄漏 |
| OperationCanceledException | 静默吞掉（Ctrl+C 流程） |

---

## 六、测试策略

### 6.1 保留并通过现有测试
- `DevCommandTests.cs` 中通过反射访问的 `ExtractOptions` / `CreateBuildOverrides` **必须保持原签名**
- `DevCommandExtendedTests.cs` 现有断言不能回归

### 6.2 新增单元测试（必需）

| 测试类 | 覆盖场景 |
|---|---|
| `DevServerHostTests` | 端口探测：请求 0 → 拿到随机端口；端口被占 → 自动 +1；20 次失败抛异常 |
| `DevWebSocketHubTests` | 实例隔离（两个 hub 不互相串）；广播向所有 OPEN 客户端发送；CLOSED 客户端自动清理；裸 catch 有 logger 调用记录 |
| `DevRequestHandlerTests` | livereload script 注入到 `</head>` 前；无 `</head>` 时追加末尾；MIME 映射全表；路径越界返回 403；404 不抛 |
| `DevFileWatcherTests` | 多次连续触发防抖只调用一次重建；Dispose 后不再触发；watcher 列表 GC-safe（持有引用） |

### 6.3 测试基础设施
- 复用 `RecordingLogger` 验证日志
- 复用 `xunit` + 临时目录隔离
- HTTP/WS 测试用 `HttpListener` + `ClientWebSocket` loopback

---

## 七、范围明确

**纳入本 PR：**
- 4 个新文件 + 2 个接口的提取
- FileSystemWatcher GC 泄漏修复
- async void → Task 修复
- 同步 IO → async IO 修复
- 裸 catch 添加日志（P2-5 一致性）
- 复用 `BuildPathUtils.MakeAbsolute(enforceWithinRoot:true)` 或等价工具
- 新增 4 个单元测试类

**不纳入本 PR（保留为独立工作）：**
- ❌ CLI 双解析路径合并（属 P3-7）
- ❌ PreviewCommand MIME 映射共享（属 P2 优化但非 P2-2 范围）
- ❌ ResolveDisableAnalytics 重构（功能正确，仅风格优化）
- ❌ CloneCommand 拆分（属 P2-1，不在本次 P2-2）

---

## 八、并行执行计划

### Agent-A 负责（无文件冲突）
- `src/Bukit.Cli/Commands/Dev/IDevWebSocketHub.cs`
- `src/Bukit.Cli/Commands/Dev/DevWebSocketHub.cs`
- `src/Bukit.Cli/Commands/Dev/IDevServerHost.cs`
- `src/Bukit.Cli/Commands/Dev/DevServerHost.cs`
- `tests/Bukit.Cli.Tests/Dev/DevWebSocketHubTests.cs`
- `tests/Bukit.Cli.Tests/Dev/DevServerHostTests.cs`

### Agent-B 负责（无文件冲突）
- `src/Bukit.Cli/Commands/Dev/DevRequestHandler.cs`
- `src/Bukit.Cli/Commands/Dev/DevFileWatcher.cs`
- `src/Bukit.Cli/Commands/Dev/DevPathGuard.cs`
- `tests/Bukit.Cli.Tests/Dev/DevRequestHandlerTests.cs`
- `tests/Bukit.Cli.Tests/Dev/DevFileWatcherTests.cs`
- `tests/Bukit.Cli.Tests/Dev/DevPathGuardTests.cs`

### 主 agent 收尾（顺序）
1. 等待两个 sub agent 全部完成报告
2. 改写 `src/Bukit.Cli/Commands/DevCommand.cs` orchestrator，把原 5 个职责委托给新组件：
   - `new DevWebSocketHub(logger)` 替代 `_wsClients` / `HandleWebSocketUpgradeAsync` / `BroadcastReloadAsync`
   - `DevServerHost.Start(host, port, logger)` 替代 `CreateListener` / `PickFreePort` / `AcceptLoop`
   - `new DevRequestHandler(outputDir, host.Port, disableAnalytics, logger)` 替代 `HandleFileRequest`
   - `new DevFileWatcher(dirs, logger, onRebuildAsync)` 替代 `StartFileWatchers`
   - `_wsClients` / `_devPort` static 字段被删除
3. 保持 `ExtractOptions` / `CreateBuildOverrides` / `RunAsync` / `ResolveWatchDirs` / `ResolveDisableAnalytics` 签名不变
4. 运行 `dotnet build bukit.slnx -c Release` 验证 0 warning 0 error
5. 运行 `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release` 验证全通过
6. 运行 `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release` 验证架构约束
7. 调用 `code-reviewer` subagent 审查

---

## 九、验收基线

| 验收项 | 通过标准 |
|---|---|
| 编译 | `dotnet build bukit.slnx -c Release` 0 警告 0 错误 |
| 现有测试 | `Bukit.Cli.Tests` 100% 通过（含 DevCommandTests / DevCommandExtendedTests） |
| 新测试 | 4 个新测试类全通过 |
| 架构测试 | `Bukit.Architecture.Tests` 全通过 |
| 静态状态 | DevCommand.cs 内 `static` 可变字段数 = 0（搜索 `private static \w+ _` 应为 0） |
| FileSystemWatcher | DevFileWatcher 持有 watcher 引用列表，Dispose 释放 |
| async void | 全仓库 `git diff` 中 dev 相关代码无 `async void` |
| 同步 IO | `DevRequestHandler` 内无 `File.ReadAllText` / `CopyTo`（非异步版本） |
| 文件行数 | `DevCommand.cs` ≤ 150 行 |

---

## 十、风险与缓解

| 风险 | 缓解 |
|---|---|
| BuildPathUtils 在 Bukit.Engine 内部，CLI 不可见 | 实现阶段先验证；若不可见则在 Cli 层提供等价 `DevPathGuard` 小工具（10 行内） |
| Agent 同时改 DevCommand.cs 冲突 | DevCommand.cs orchestrator 由主 agent 在 Agent A/B 完成后改写，避免并行冲突 |
| 反射测试因签名漂移失败 | spec 明确保留 `ExtractOptions` / `CreateBuildOverrides` 签名 |
| FileSystemWatcher 行为差异（macOS/Linux/Windows） | 仅修引用持有 bug，不改触发逻辑；现有 e2e 测试维持 |
| HttpListener 在 macOS root 权限要求 | DevServerHost 仅绑定 localhost，无需 root |

---

*Spec 完成。等待用户审阅后调用 writing-plans skill 进入实现计划阶段。*
