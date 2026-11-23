## Context

完整设计在 [实施方案](../../../docs/plans/2026-11-23-cloudbase-content-source-implementation-design.zh-CN.md)
（决策 D1～D18、验收 AC1～AC10），执行顺序和验证方式在
[任务计划书](../../../docs/plans/2026-11-23-cloudbase-content-source-task-plan.zh-CN.md)。
本文件不复制方案内容，只记录本变更特有的上下文、偏离和技术债。

G0 的结论和 Phase 0 调研见 proposal.md。环境为 PG 模式（用户确认），方案已修订为 v4：数据通过 PostgREST
读取 `public` schema 的表或视图，文件通过 PG 存储批量签名接口换成签名链接，凭据是 Publishable Key。

## Goals / Non-Goals

**Goals:** 见方案第 1.1 节和 AC1～AC10。

**Non-Goals:** 见方案第 1.2 节。P1 不做缓存、离线快照、增量指纹、HTML 正文、非图片文件字段、
关联字段、写回，也不把 Notion 专用设施抽象成通用扩展点。

## Decisions

沿用方案 v4 第 4 节的 D1～D18。与本变更规格直接相关的几点：

- **D4**：没有 `kind`；`table` 必填，是 `public` schema 的表或视图名。
- **D5 / G0-4**：P1 只实现 Publishable Key 一种凭据策略，变量名 `CLOUDBASE_PUBLISHABLE_KEY`。客户端只解码、不验签
  JWT 的 `role`，仅作诊断：`service_role` 或无法解析时 `doctor` 报 `CloudBaseAdminCredentialInUse`，构建继续。
- **D10 / G0-6**：CloudBase 签名链接有两种形式：网关主机 + `/v1/storages/object/sign/` 路径前缀 + `token`，以及存储 CDN 主机 `*.storage.tcloudbase.com` + `token`（provider 不生成后者），集中在一处定义，
  真实环境验证后再调整。`media-localization` 的签名方案清单和"过期签名不重试"两条现有 requirement 用 MODIFIED 更新；
  CloudBase 不读过期时间，照常重试。
- **D18**：`envId` 正则为 `^[a-z0-9][a-z0-9-]{0,62}$`（v4 已从 `{0,63}` 更正）；`storageBucket` 和对象名也做白名单校验。
- **region**：枚举 `cn`（默认）| `intl`，不接受地域字符串。

## Risks / Trade-offs

见方案第 12 节。另加：

- 签名链接的形状已在 PG 模式试用环境实测（`signedURL` 是相对路径，`fullSignedURL` 可能不存在）；鉴权（网关 + Publishable Key + RLS）已实测。签名响应还有存储 CDN 形式 `fullSignedURLForCDN`，provider 不用它，但签名识别、输出检测和 D8 ⑤ 都覆盖 `*.storage.tcloudbase.com`。
- RLS 可能静默过滤掉全部行，PostgREST 返回 `200 []`；`doctor` 给出提示，`build` 不把空结果当错误。
- PostgREST 单次行数上限和签名链接有效期上限都没有文档；P1 取 `pageSize` ≤ 1000、`signExpiresSeconds` ≤ 3600。
- 传统模式环境（数据模型）不在 P1 范围内，可以在 P2 增加。

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
