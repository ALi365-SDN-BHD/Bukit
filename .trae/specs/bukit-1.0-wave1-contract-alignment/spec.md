# Bukit 1.0 Wave 1: 仓库自一致性 Spec

## Why

Bukit 1.0 可信稳定化的第一波目标是消除仓库内部的自相矛盾。当前 `smoke.sh`、`smoke-all.sh`、`security-regression.sh`、`dotnet test` 均通过，但 `docs check` 报告 1832 个错误和 167 个警告，且 starter、示例站点、doctor 规则、技能文档之间存在大量契约漂移。这些不一致直接伤害用户和 AI 代理对 Bukit 的信任。

## What Changes

### 任务包 1：修复 `smoke.sh` 当前失败链条
- **状态：已完成。** `smoke.sh`、`smoke-all.sh`、`security-regression.sh`、`dotnet test` 均已通过。
- 无需额外修复，但需将当前通过状态固化为回归。

### 任务包 2：统一 starter / example / doctor / docs 契约
- 修复 `docs check` 中的文件引用错误（技能文档中引用了不存在的文件路径）
- 修复技能文档中引用的 CLI 命令与实际 CLI 参考不一致的问题
- 修复 `CLAUDE.md` 和 `GEMINI.md` 中技能数量错误（写 19，实际 20）
- 为缺失 `bukit.templates.yaml` 的示例站点补齐该文件
- 为缺失 `theme.yaml` 的示例站点补齐该文件
- 补齐 `starter` 的 `theme.yaml` 中缺失的 `engine` 和 `min_engine_version` 字段
- 确保所有示例站点能干净通过 `bukit doctor`
- 修复 `bukit-config` 技能中声称 `deploy` 是顶级节点但无示例使用的问题

### 任务包 3：把 Wave 1 修复固化为回归
- 为 starter/example/doctor/template contract 增加测试或 fixture 覆盖
- 确保 `docs check` 成为 CI 门禁的一部分

## Impact

- Affected specs: bukit-config, bukit-cli-reference, bukit-theme, bukit-templating, bukit-seo, bukit-geo, bukit-import, bukit-notion, bukit-deploy, bukit-dev, bukit-preview, bukit-plugins-debug, bukit-webhook, bukit-design-tokens, theme-component-system, using-bukit, bukit-clone
- Affected code: examples/starter/, examples/*/layouts/, src/skills/*/SKILL.md, AGENTS.md, CLAUDE.md, GEMINI.md

## ADDED Requirements

### Requirement: 示例站点必须能干净通过 doctor
所有 `examples/` 下的示例站点在运行 `bukit doctor` 时，SHALL 不产生 ERROR 级别诊断，WARNING 级别仅允许内容质量类警告（如缺少 author、summary 等），不允许配置/模板结构类警告。

#### Scenario: starter 站点 doctor 通过
- **WHEN** 对 `examples/starter/site.yaml` 运行 `bukit doctor`
- **THEN** 无 ERROR，无配置/模板结构类 WARNING

#### Scenario: 所有示例站点 doctor 通过
- **WHEN** 对任意 `examples/*/site.yaml` 运行 `bukit doctor`
- **THEN** 无 ERROR，无配置/模板结构类 WARNING

### Requirement: 示例站点必须包含 bukit.templates.yaml
所有 `examples/` 下的示例站点 SHALL 包含 `layouts/bukit.templates.yaml`，声明其模板能力。

#### Scenario: blog-site 有 bukit.templates.yaml
- **WHEN** 检查 `examples/blog-site/layouts/`
- **THEN** `bukit.templates.yaml` 存在且声明了所有模板

### Requirement: 示例站点必须包含 theme.yaml
所有 `examples/` 下的示例站点 SHALL 包含 `layouts/theme.yaml`，声明主题元数据。

#### Scenario: corporate-site 有 theme.yaml
- **WHEN** 检查 `examples/corporate-site/layouts/`
- **THEN** `theme.yaml` 存在且包含 name、version、description

### Requirement: starter theme.yaml 必须是最完整的
`examples/starter/layouts/theme.yaml` SHALL 包含所有标准主题元数据字段，包括 `engine` 和 `min_engine_version`。

#### Scenario: starter theme.yaml 完整性
- **WHEN** 检查 `examples/starter/layouts/theme.yaml`
- **THEN** 包含 `engine: bukit` 和 `min_engine_version` 字段

### Requirement: 技能文档文件引用必须有效
所有 `src/skills/*/SKILL.md` 中的文件路径引用 SHALL 指向仓库中实际存在的文件。

#### Scenario: docs check 文件引用错误清零
- **WHEN** 运行 `dotnet run --project src/Bukit.Cli -c Release -- docs check`
- **THEN** 无 "File reference not found" 错误

### Requirement: 技能文档 CLI 命令引用必须与 CLI 参考一致
所有 `src/skills/*/SKILL.md` 中引用的 CLI 命令 SHALL 在 `bukit-cli-reference/SKILL.md` 中有文档记录。

#### Scenario: CLI 命令引用一致性
- **WHEN** 运行 `dotnet run --project src/Bukit.Cli -c Release -- docs check`
- **THEN** 无 "CLI command ... is not documented" 警告

### Requirement: 代理入口文件技能数量必须准确
`AGENTS.md`、`CLAUDE.md`、`GEMINI.md` 中声明的技能数量 SHALL 与实际 `src/skills/` 目录中的技能数量一致。

#### Scenario: 技能数量准确
- **WHEN** 检查 `AGENTS.md`、`CLAUDE.md`、`GEMINI.md`
- **THEN** 技能数量声明与实际一致（当前为 20）

## MODIFIED Requirements

### Requirement: bukit-config 技能 deploy 节点文档
`bukit-config` 技能中关于 `deploy` 顶级节点的描述 SHALL 明确标注其当前状态（如 "planned" 或 "experimental"），避免用户误以为该功能已可用。

#### Scenario: deploy 节点文档准确
- **WHEN** 阅读 `src/skills/bukit-config/SKILL.md` 中关于 `deploy` 的描述
- **THEN** 明确标注该节点的支持层级
