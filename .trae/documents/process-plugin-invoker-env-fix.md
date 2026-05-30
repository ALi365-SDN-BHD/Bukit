# Fix ProcessPluginInvoker environment isolation

## 目标

修复 `ProcessPluginInvoker.ApplyEnvironment()` 过度清空环境变量导致 `dotnet` protocol plugin 在 macOS 上无法启动的问题。同时保持插件进程隔离安全，不泄露敏感环境变量。

---

## 实现步骤

### 步骤 1：重构 `ProcessPluginInvoker.ApplyEnvironment()`

**文件**: `src/Bukit.Engine/Plugins/Protocol/ProcessPluginInvoker.cs`

1. **新增 `DefaultRuntimeEnvironmentAllowlist`** — 最小运行时环境变量白名单：
   - POSIX: `PATH`, `HOME`, `USER`, `SHELL`, `TMPDIR`
   - Windows: `USERPROFILE`, `SystemRoot`, `WINDIR`, `COMSPEC`, `PATHEXT`
   - 跨平台: `TEMP`, `TMP`
   - .NET: `DOTNET_ROOT`, `DOTNET_ROOT_X64`, `DOTNET_ROOT_X86`, `DOTNET_CLI_HOME`

2. **新增 `CopyAllowedEnvironment()`** — 辅助方法，从宿主环境按白名单复制变量到子进程环境。接受 `IDictionary` 和 `IEnumerable<string> names`，仅复制 name 存在且值为 string 的变量。

3. **重构 `ApplyEnvironment()`**：
   - `Clear()` 后先按 `DefaultRuntimeEnvironmentAllowlist` 复制基础运行时变量
   - 再按 `plugin.AllowEnvironment` 复制用户显式允许的变量
   - 注入确定性 .NET CLI 设置：
     - `DOTNET_CLI_TELEMETRY_OPTOUT=1`
     - `DOTNET_NOLOGO=1`
     - `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`
   - 最后注入 `BUKIT_PLUGIN_NAME`、`BUKIT_PLUGIN_HOOK`、`BUKIT_PROJECT_ROOT`、`BUKIT_OUTPUT_DIR`

4. **安全保证**：仅允许白名单变量 + 用户显式 AllowEnvironment。敏感变量（`NOTION_TOKEN`、`OPENAI_API_KEY`、`GITHUB_TOKEN`、`DATABASE_URL` 等）默认不继承。

---

### 步骤 2：添加环境隔离行为测试

**文件**: `tests/Bukit.Engine.Tests/ExternalProtocolPluginTests.cs`（在现有测试类中增加）

| 测试方法 | 验证内容 |
|---------|---------|
| 默认环境保留 PATH | 子进程 `PATH` 非空 |
| 默认环境保留 HOME | 子进程 `HOME` 非空 |
| 默认环境不保留敏感变量 | `NOTION_TOKEN`、`OPENAI_API_KEY` 不在子进程中 |
| `AllowEnvironment` 显式保留自定义变量 | 配置 `allowEnvironment: [MY_VAR]` 后子进程包含 `MY_VAR` |
| `BUKIT_*` 上下文变量始终注入 | 验证 `BUKIT_PLUGIN_NAME` 等 4 个变量 |
| ProtocolEchoPlugin `derive-conflict` 可正常调用 | entry=dotnet + ProtocolEchoPlugin.dll 启动成功 |

---

### 步骤 3：验证

```bash
# 单次 Engine 全量
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release

# 100 轮稳定性
for i in $(seq 1 100); do
  echo "Engine run $i"
  dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release -maxcpucount:1 -nodeReuse:false || exit 1
done

# 安全回归
bash scripts/security-regression.sh Release

# 全量测试
bash scripts/test-all.sh Release
```

---

## 文件变更汇总

| 文件 | 操作 |
|------|------|
| `src/Bukit.Engine/Plugins/Protocol/ProcessPluginInvoker.cs` | **修改** — 新增 `DefaultRuntimeEnvironmentAllowlist`、`CopyAllowedEnvironment`，重构 `ApplyEnvironment` |
| `tests/Bukit.Engine.Tests/ExternalProtocolPluginTests.cs` | **修改** — 新增环境隔离行为测试 |

## 实施顺序

1. 步骤 1 — 重构 `ProcessPluginInvoker`
2. 步骤 2 — 添加测试
3. 步骤 3 — 验证
