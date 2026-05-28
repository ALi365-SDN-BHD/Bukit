# External Plugin 生态强化 Spec

> 来源：`.trae/documents/bukit-audit-report-202605-28.md` P1-4

## Why

协议规范和代码实现已完备，但缺少面向非 .NET 开发者的 step-by-step 教程、可运行的示例插件、以及 derive-pages 协议的集成测试。当前 `examples/plugin-site` 仅演示内置插件，`ProtocolEchoPlugin` 只是测试桩。

## What Changes

- **新增用户指南** `guide/user/20-external-plugins.md`（中英双语）— step-by-step 教用户用 Node.js 编写外部插件
- **新增示例插件** `examples/plugin-site/plugins/hello-derive.js` — Node.js derive-pages 插件，生成 `/hello/` 端点
- **新增示例配置** `examples/plugin-site/site.external-plugin.yaml` — 引用外部插件的 site.yaml
- **新增集成测试** — 验证 derive-pages 协议外部插件端到端可用
- **CI 构建** — CI 中加入外部插件示例站点构建

## Impact

- Affected specs: 无
- Affected code:
  - `guide/user/20-external-plugins.md` + `.zh-CN.md` — **新建**
  - `examples/plugin-site/plugins/hello-derive.js` — **新建** Node.js 示例插件
  - `examples/plugin-site/site.external-plugin.yaml` — **新建**
  - `tests/Bukit.Engine.Tests/` — 新增 derive-pages 集成测试
  - `.github/workflows/ci.yml` — 新增外部插件示例站点构建步骤

## ADDED Requirements

### Requirement: 用户指南 — 编写外部插件

新增 `guide/user/20-external-plugins.md` 和 `.zh-CN.md`。

覆盖内容：
1. 什么是外部插件（process 子进程，stdin/stdout JSON）
2. 支持的语言（Node.js / Python / 任意 stdin/stdout 程序）
3. site.yaml 配置（`externalPlugins.<name>.runtime/entry/hooks/capabilities`）
4. 协议概述（hook dispatch → 读取 request → 返回 response）
5. 安全模型（超时、stdout 限制、环境变量白名单）
6. 完整 Node.js 示例代码 walkthrough
7. 故障排查

#### Scenario: 用户按指南操作可成功运行

- **GIVEN** 用户安装 Node.js
- **WHEN** 用户复制指南中的示例代码并配置 `site.external-plugin.yaml`
- **THEN** `bukit build` 成功生成 `/hello/` 页面

### Requirement: Node.js 示例外部插件

`examples/plugin-site/plugins/hello-derive.js` SHALL 实现一个 derive-pages 协议插件：

- 读取 stdin JSON 请求
- 从 `derivePages.routedPages` 统计页面数量
- 返回一个派生页面 `/hello/`，内容为 `Hello from external plugin! Total pages: N`
- 正确的协议格式（`hook`/`ok`/`derivedPages`）

#### Scenario: 示例插件在 CI 中构建成功

- **WHEN** `dotnet run -- build --config examples/plugin-site/site.external-plugin.yaml --clean --site-url https://example.com`
- **THEN** 构建成功，输出 `/hello/index.html`

### Requirement: Derive-pages 协议集成测试

新增集成测试验证外部插件 derive-pages 协议端到端可用。

#### Scenario: 外部 derive-pages 插件生成页面

- **GIVEN** 配置了 external plugin with hook=derive-pages
- **WHEN** 使用 `ProtocolEchoPlugin derive-success` 模式构建
- **THEN** 输出目录包含 derive 插件生成的页面
