# Guide & Skills 补充计划

基于已完成的修复成果，更新相关文档和技能。

## 变更范围

### 1. guide/dev/testing-smoke.md — 测试体系文档

**当前状态**: 仅 21 行，描述非常简略（列了 smoke.ps1/smoke.sh 和 README 中的验收说明）。

**补充内容**:

- **scripts/smoke-all.sh**: 覆盖 7 个示例站点 + 9 个 fixture 站点的全量冒烟检查，含 dotfile 泄露和危险 URL 验证
- **scripts/security-regression.sh**: 5 模块安全回归测试（Shared/Config/CLI/Engine/Content），可独立运行
- **scripts/test-all.sh**: 一键全量流水线（restore → build → test → quality-gate → smoke → smoke-all → AOT publish）
- **scripts/stress-test.sh**: 可配置轮数的压力测试
- **scripts/quality-gate.sh**: 覆盖率门禁 + 文件大小 + 编码检查 + format + 冒烟
- **tests/fixtures/**: 10 个 fixture 站点，覆盖基础构建、路由安全、URL 安全、插件策略、输出安全、增量构建、i18n、taxonomy、组件验证、dotfile 防泄露
- **CI 结构**: 5 job 矩阵（quality-gate / cross-platform-tests(ubuntu/windows/macos) / smoke-examples / native-aot(跨平台) / stress-cli(手动触发)）
- **ProtocolEchoPlugin 测试模式**: 列出所有可用模式（success/derive-success/derive-conflict/derive-lastwins/derive-plugin-a/derive-plugin-b/env/env-allowlist 等）

**影响**: 也需要更新对应的 `.ms.md` (马来语) 和 `.zh-CN.md` 翻译

---

### 2. guide/dev/external-plugin-protocol.md — 插件协议文档

**当前状态**: 描述了协议结构、执行流程，但环境隔离描述不完整（仅提到 "Only BUKIT_* variables and the AllowEnvironment whitelist"）。

**补充内容**:

- **默认运行时环境白名单**: `PATH`, `HOME`, `USER`, `SHELL`, `TMPDIR` (POSIX) / `USERPROFILE`, `SystemRoot`, `WINDIR`, `COMSPEC`, `PATHEXT` (Windows) / `TEMP`, `TMP` (跨平台) / `DOTNET_ROOT`, `DOTNET_ROOT_X64`, `DOTNET_ROOT_X86`, `DOTNET_CLI_HOME` (.NET)
- **安全保证**: 敏感变量（`NOTION_TOKEN`, `OPENAI_API_KEY`, `GITHUB_TOKEN`, `DATABASE_URL`）默认不继承
- **`AllowEnvironment`**: 用户可显式放行自定义变量
- **确定性 .NET CLI 设置**: `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`, `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1` 每次注入
- **`BUKIT_*` 上下文变量**: `BUKIT_PLUGIN_NAME`, `BUKIT_PLUGIN_HOOK`, `BUKIT_PROJECT_ROOT`, `BUKIT_OUTPUT_DIR` 始终设置

**影响**: 也需要更新 `.ms.md` 和 `.zh-CN.md`

---

### 3. guide/user/20-external-plugins.md — 用户外部插件指南

**当前状态**: 已有安全模型和基础配置说明。第 36 行提到 "Environment isolation: Only BUKIT_* variables and the AllowEnvironment whitelist"。

**补充内容**:

- 更新第 36 行的环境隔离描述，补充默认运行时白名单信息
- 添加 `AllowEnvironment` 的显式使用示例

**影响**: 已有 `.zh-CN.md` 翻译，需同步更新

---

### 4. src/skills/bukit-plugins-debug/SKILL.md — 插件调试技能

**当前状态**: 涵盖了内置插件清单和外部插件基础说明。第 55-60 行提到 externalPluginPolicy 和外部协议插件。

**补充内容**:

- **外部插件环境隔离调试**: 当插件启动失败时，检查环境变量是否被正确传递（PATH, HOME, DOTNET_ROOT）
- **插件执行信息追踪**: `context.PluginExecutions` 检查每个插件的 `Success`, `Error`, `DurationMs` 定位失败点
- **ProtocolEchoPlugin 测试辅助**: 列出可用于集成测试的所有模式，指导如何构建测试 case
- **ProcessPluginInvoker 诊断**: 检查 ExitCode、StdErr、Timeout 状态定位进程启动问题

---

### 5. guide/dev/maintainer-entrypoints.md — 维护者入口文档

**当前状态**: 描述了各变更类型对应的源文件入口，但缺少测试脚本部分。

**补充内容**:

在 "Verification" 或新增章节中添加:
- 全量测试: `bash scripts/test-all.sh Release`
- 安全回归: `bash scripts/security-regression.sh Release`
- 冒烟测试: `bash scripts/smoke-all.sh Release`
- 压力测试: `bash scripts/stress-test.sh 20 Release`
- CI 门禁: `bash scripts/quality-gate.sh Release`

---

## 文件变更汇总

| 文件 | 操作 | 优先级 |
|------|------|--------|
| `guide/dev/testing-smoke.md` | **修改** — 补充完整测试体系 | P1 |
| `guide/dev/testing-smoke.ms.md` | **修改** — 翻译同步 | P2 |
| `guide/dev/testing-smoke.zh-CN.md` | **修改** — 翻译同步 | P2 |
| `guide/dev/external-plugin-protocol.md` | **修改** — 补充环境隔离细节 | P1 |
| `guide/dev/external-plugin-protocol.ms.md` | **修改** — 翻译同步 | P2 |
| `guide/dev/external-plugin-protocol.zh-CN.md` | **修改** — 翻译同步 | P2 |
| `guide/user/20-external-plugins.md` | **修改** — 更新环境隔离描述 | P1 |
| `guide/user/20-external-plugins.zh-CN.md` | **修改** — 翻译同步 | P2 |
| `guide/dev/maintainer-entrypoints.md` | **修改** — 添加测试入口 | P1 |
| `src/skills/bukit-plugins-debug/SKILL.md` | **修改** — 添加外部插件调试 | P1 |

## 实施顺序

1. `testing-smoke.md` — 测试体系文档
2. `external-plugin-protocol.md` — 插件协议环境隔离
3. `20-external-plugins.md` — 用户插件环境隔离
4. `bukit-plugins-debug/SKILL.md` — 插件调试技能
5. `maintainer-entrypoints.md` — 维护者测试入口
6. 翻译文件 (.ms.md, .zh-CN.md) — 主文档定稿后同步
