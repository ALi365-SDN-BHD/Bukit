# Tasks

## Task 1: 用户指南 — 中英双语外部插件教程 ✅
- [x] 1.1 创建 `guide/user/20-external-plugins.md`（英文）
- [x] 1.2 创建 `guide/user/20-external-plugins.zh-CN.md`（中文）
- [x] 1.3 覆盖：配置、协议概述、安全模型、Node.js walkthrough、故障排查

## Task 2: Node.js 示例外部插件 + 示例站点配置 ✅
- [x] 2.1 创建 `examples/plugin-site/plugins/hello-derive.js`（Node.js derive-pages 插件）
- [x] 2.2 创建 `examples/plugin-site/site.external-plugin.yaml`（引用外部插件）

## Task 3: Derive-pages 协议集成测试 ✅
- [x] 3.1 新增 `ExternalProtocolPlugin_DerivePages_GeneratedPageContent_CanBeRendered` 测试
- [x] 3.2 34/34 ExternalProtocolPlugin tests pass
- [x] 3.3 `dotnet test` 通过

## Task 4: 验证整体正确性 ✅
- [x] 4.1 `dotnet build` 0 警告 0 错误
- [x] 4.2 `dotnet format` 通过
- [x] 4.3 1029 Engine + 524 Content + 730 Cli tests pass
- [x] 4.4 checklist 全部通过
