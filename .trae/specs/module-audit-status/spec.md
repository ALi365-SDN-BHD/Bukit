# 模块整理与模型审计状态 Spec

## Why
当前项目包含 12 个核心源码模块、12 个 CLI 命令、8 个内置插件、22 个 Notion Block 渲染器、6 个测试模块，以及大量数据模型定义。需要一份集中化的清单，清晰列出所有模块及其职责边界，并对每个模型的审计相关字段进行盘点，以便后续评估是否需要引入内容审核工作流。

## What Changes
- 在 `.trae/documents/` 下生成一份 `模块与模型审计状态清单.md` 文档
- 列出所有源码模块（14 个 `src/` 模块），含命名空间、项目文件、职责简述
- 列出所有数据模型（record/class），标注所属模块和审计相关字段
- 对每个模型的审计状态给出明确结论：无审计字段 / 等效发布控制 / SEO 审计模型
- 记录现有 SEO 审计报告模型（SeoAuditReport 系列）作为唯一内置审计能力

## Impact
- Affected specs: 无（纯文档产出，不修改任何代码）
- Affected code: 无

## ADDED Requirements

### Requirement: 模块清单
系统 SHALL 提供一份包含所有源码模块的完整清单。

#### Scenario: 查阅模块列表
- **WHEN** 开发者或 AI Agent 阅读 `.trae/documents/模块与模型审计状态清单.md`
- **THEN** 可以看到所有 12 个源码模块、12 个 CLI 命令、8 个内置插件、22 个 Block 渲染器的列表及其职责说明

### Requirement: 模型审计状态盘点
系统 SHALL 列出所有数据模型的审计相关字段及其当前状态。

#### Scenario: 查阅 ContentItem 审计状态
- **WHEN** 查看文档中的 "核心数据模型审计状态" 表格
- **THEN** ContentItem 行显示 `PublishAt`（发布日期控制）和 `Meta` 字典（可存储自定义字段），审计状态为 "等效发布控制"

#### Scenario: 查阅 SEO 审计模型
- **WHEN** 查看文档中的审计相关章节
- **THEN** SeoAuditReport / SeoAuditRoute / SeoAuditIssue / SeoAuditSummary 显示为内置 SEO 审计模型，包含 Severity/Code/Route/Message 等审计字段
