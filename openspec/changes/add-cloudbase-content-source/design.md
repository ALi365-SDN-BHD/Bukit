## Context

完整设计在 [实施方案](../../../docs/plans/2026-11-23-cloudbase-content-source-implementation-design.zh-CN.md)
（决策 D1～D18、验收 AC1～AC10），执行顺序和验证方式在
[任务计划书](../../../docs/plans/2026-11-23-cloudbase-content-source-task-plan.zh-CN.md)。
本文件不复制方案内容，只记录本变更特有的上下文、偏离和技术债。

G0 的结论和 Phase 0 调研见 proposal.md。环境为 PG 模式，数据访问路径和存储换链接口还在调研，
方案会在 T01/T03 之前修订为 v4；与访问路径无关的决策已写进 proposal.md 的"实施期决策"。

## Goals / Non-Goals

**Goals:** 见方案第 1.1 节和 AC1～AC10。

**Non-Goals:** 见方案第 1.2 节。P1 不做缓存、离线快照、增量指纹、HTML 正文、非图片文件字段、
关联字段、写回，也不把 Notion 专用设施抽象成通用扩展点。

## Decisions

沿用方案第 4 节的 D1～D18，以下几点在实施期补充或更正：

- **D18 更正**：`envId` 正则改为 `^[a-z0-9][a-z0-9-]{0,62}$`，与 DNS 标签最大长度 63 一致。
- **D5 / G0-4**：P1 只实现 API Key 一种凭据策略，变量名 `CLOUDBASE_API_KEY`，视为管理员级凭据。
- **D10 / G0-6**：CloudBase 临时链接的域名和签名参数集中在一处定义，真实环境验证后再调整。
- **region**：枚举 `cn`（默认）| `intl`，不接受地域字符串。

## Risks / Trade-offs

见方案第 12 节。另加：

- 鉴权和临时链接都没有在真实环境验证（用户指示的偏离）。T10 冒烟测试前，相关实现以公开文档为准。
- PG 模式的访问路径未定。如果 v4 改为 PostgREST 表接口，transport 的请求形状、分页和系统字段清单都要随之调整。

## 技术债：Notion 硬编码

现有的隐私检查（`PublicOutputPrivacyCheck`、`DeploymentPrivacyValidator`）、公开投影
（`PublicContentProjectionPolicy`、`JsonFeedGenerator` 只在 `post.Source == "notion"` 时过滤实体）、
快照与离线设施（`NotionSnapshotScope.IsOffline` 等）都硬编码了 Notion。P1 在这些位置**并列**加上
CloudBase 的逻辑（D11），不做通用化重构。每多一个远程内容源，这些位置就要再并列一次。

登记为技术债，另行立项：把"远程内容源的敏感标识、实体过滤和离线快照"抽象成 provider 提供的扩展点，
届时迁移 Notion 和 CloudBase 两个实现。本变更不处理。

## Migration Plan

增量变更：没有 cloudbase 源的站点产物逐字节不变。每个任务单独提交，可以逐个 revert；
回滚 T02/T03 时同步还原 public API 基线和项目计数（方案第 12 节）。
