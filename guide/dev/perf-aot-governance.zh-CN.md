# 性能 / AOT / 规范治理补充说明

本文补充说明本仓库近期引入的工程治理策略，目标是让维护者快速理解"为什么这样做、现在怎么用、后续怎么演进"。

## 1. 目标与边界

本轮治理关注四类问题：

- 可复现缺陷：先修复会导致测试/发布失败的问题。
- AOT 可持续：保证 NativeAOT 发布可控、可解释。
- 性能可度量：提供统一基线脚本，减少"主观快慢"争议。
- 编码规范门禁：把风格与质量要求前移到 CI。

不包含：

- 模板引擎（Scriban）大规模替换或架构重写。
- 功能语义变更（CLI 命令行为保持兼容）。

## 2. 当前门禁策略

### 编译与告警

- 全局配置位于 `Directory.Build.props`
- 默认启用：
  - `TreatWarningsAsErrors=true`
  - `AnalysisLevel=latest`
  - `EnforceCodeStyleInBuild=true`

### 代码风格

- 统一规则位于 `.editorconfig`
- CI 使用：
  - `dotnet format bukit.slnx --verify-no-changes`

### AOT 告警治理

Scriban（vendored 源码）和 ImageSharp（vendored 源码）均已完成 AOT 兼容改造，所有 AOT/Trim 告警已从源头消除，不再需要白名单。

- 检查脚本：`scripts/check-aot-warnings.sh`
- 零告警策略：任何 AOT/Trim 告警均导致检查失败
- 跨平台 RID（如在 macOS 上检查 `linux-x64`）会明确跳过并提示原因

#### Source-Gen JSON 序列化规则

Publish 闭包中的所有 `JsonSerializer.Serialize` / `Deserialize` 调用必须使用
`JsonSerializerContext` 源生成重载。基于反射的 `JsonSerializerOptions` 重载会在 NativeAOT
中触发 IL2026/IL3050 告警，禁止使用。

当模型类型包含 `IReadOnlyDictionary<string, object>` 时，经过 source-gen 反序列化后，
字典内的值类型为 `JsonElement`。必须在反序列化边界调用
`JsonElementMaterializer.Materialize()` 递归将 `JsonElement` 值转换为 CLR 原语
（string/bool/long/double/List/Dictionary）。

CI 强制规则：`scripts/check-aot-warnings.sh` 必须输出零条 `ILC : warning IL\d{4}` 行。

## 3. AOT 发布行为约定

### 符号剥离（StripSymbols）

- 默认：`BukitStripSymbols=false`
- 原因：避免本地缺少 `objcopy/llvm-objcopy` 时发布失败。
- 需要体积优化时可显式开启：

```bash
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c AOT -r linux-x64 -o out/bukit -p:BukitStripSymbols=true
```

开启剥离但缺少工具时，会给出明确报错，不再隐式失败。

## 4. 性能基线使用方式

基线脚本：

- `scripts/perf-baseline.sh`

示例：

```bash
bash scripts/perf-baseline.sh Release osx-arm64 examples/starter/site.yaml
```

输出包含：

- JIT 与 AOT 各自的 `time -l` 指标（real/user/sys/RSS）
- 对应 `metrics.json` 路径

解读建议：

- 冷启动优先看 `real`。
- 内存占用看 `maximum resident set size`。
- 插件耗时与渲染数量以 `metrics.json` 为准。

## 5. CI 流程顺序（smoke workflow）

CI 质量门顺序如下：

1. `dotnet build ... -warnaserror`
2. `dotnet format --verify-no-changes`
3. Engine / Content / CLI / Rendering 单元测试（含 coverage 收集）
4. WASM protocol 测试
5. Coverage gate（分项目阈值）
6. Vulnerable package gate（阻断 High/Critical）
7. AOT 告警检查脚本（零告警策略）
8. 仓库 smoke 测试

该顺序保证"快失败"：先在低成本阶段阻断问题，再进入慢步骤。

## 6. 维护建议

- 新增第三方库时，优先在 AOT 模式下跑一次发布验证。
- 如引入新的第三方依赖，需确保其 AOT 兼容或使用条件编译隔离。
- 性能优化建议先以脚本基线记录前后对比，再提交改动。
