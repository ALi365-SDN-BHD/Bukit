# Add built-in CloudBase content source

## Why

内部站点"travel 网站"的内容由编辑在腾讯云开发（CloudBase）PG 模式环境的 PostgreSQL 表里维护，需要 Bukit 在构建时直接读取，
不再先同步成本地 Markdown。现有内容源只有 `markdown` 和 `notion`，没有能读 CloudBase 的路径。

可行性分析（`docs/analysis/bukit-core-cloudbase-builtin-provider-feasibility-2026-11-22.zh-CN.md`）
的默认推荐是同步插件（方案 C）。用户已明确授权按方案 A（Core 内置 provider）实施，范围限于 P1。
设计见 [实施方案](../../../docs/plans/2026-11-23-cloudbase-content-source-implementation-design.zh-CN.md)，
任务顺序见 [任务计划书](../../../docs/plans/2026-11-23-cloudbase-content-source-task-plan.zh-CN.md)。

## What Changes

- `content.sources[].type` 新增 `cloudbase`，配置写在 `content.sources[].cloudbase` 下，读取 PG 模式环境 `public` schema 里的一张表或视图（D4）。配置类型全部 internal（D1）。
- 新项目 `src/Bukit-Core/Bukit.CloudBase/`：只依赖 BCL 的只读 transport（PostgREST 查询和 PG 存储批量签名），JSON 用源生成（D2、D3、D6），
  登记为受治理程序集，公开面为空（D15）。
- `Bukit.Content/CloudBase/`：provider、字段投影（默认白名单，系统字段和个人字段硬性剔除）、
  文件对象批量签名（对象名和 `cloud://` 兼容形式）、Markdown 正文的不可信链接模式（D7、D9、D17）。
- 新增 `BKT-0C01`～`BKT-0C0B` 诊断码（D12）。
- 构建阶段和部署阶段的隐私检查并列加入 CloudBase 标识检测（D11）；签名链接识别加入 CloudBase PG 存储签名链接（D10）。
- `doctor` 新增 CloudBase 检查，凭据的 JWT `role` 是 `service_role` 或无法解析时警告（D5、AC8）。
- P1 不做：传统模式环境（数据模型、文档型数据库）、缓存、离线快照、增量指纹、HTML 正文、非图片文件字段、关联字段、写回。

## Capabilities

### New Capabilities

- `cloudbase-content-source`：CloudBase 内容源的配置、读取、字段投影、正文链接安全、文件换链、凭据与日志、doctor。

### Modified Capabilities

- `core-report-contracts`：公开产物隐私检查覆盖 CloudBase 标识（新增 requirement，现有报告契约不变）。
- `media-localization`：签名链接识别加入 CloudBase PG 存储签名链接（MODIFIED）；该方案不读过期时间，照常重试（MODIFIED）；`fileFields` 非空时的本地化前提（D8，ADDED）。
- `miniprogram-data`："媒体与链接绝对且稳定"的拦截范围扩展到 CloudBase 签名链接和 `cloud://` 兼容形式（MODIFIED）。
- `incremental-invalidation`：CloudBase 来源在 P1 不提供增量指纹。

## Impact

- 代码：`Bukit.Config`、`Bukit.Shared`（诊断码）、`Bukit.Content`、`Bukit.Engine`、`Bukit.Engine.Audit`、
  `Bukit.Engine.Primitives`、`Bukit.Engine.Feeds`、`Bukit.Cli`，以及新项目 `Bukit.CloudBase` 和 `Bukit.CloudBase.Tests`。
- public API：只有两类预期变化，`DiagnosticCode` 新增成员，`Bukit.CloudBase` 的空公开面条目。
- 配置 schema：`content.sources[].type` 枚举增量加入 `cloudbase`。
- 没有 cloudbase 源的站点：产物逐字节不变。
- 跨仓：BukitJalil 的 BKT 码表和 `MIN_BUKIT_VERSION` 由计划书 Part B 另行交接。
- 工作流：`scripts/checks/codex-workflow-policy.v1.json` 新增 CloudBase 相关 pathRules，
  并把会构建的工具门禁归入 `dotnet-serial`。

## G0 决策记录

记录日期 2026-11-23，由控制者根据用户确认记录（方案 v4）。

| 门禁 | 结论 | 说明 |
|---|---|---|
| G0-1 内部消费方 | 部分通过 | 站点：travel 网站；负责人：张三。**业务理由和可验证的验收标准待用户补充**，补齐前本项不算完全通过。 |
| G0-2 Core 任务授权 | 通过 | 用户明确授权把本变更作为一个独立的、用户可见的 Core 任务实施。按方案第 2 节照抄说明：`AGENTS.md` 的 Website/Core 隔离规则字面上针对的是缺陷修复，把它用在新增能力上是类推，不是规则原文。 |
| G0-3 数据形态 | 通过 | 环境为 PG 模式（用户确认）。数据是 `public` schema 里的 PostgreSQL 表或视图，通过 PostgREST 读取。文档写明 PG 模式不支持数据模型（"PG 模式环境暂不支持"），所以不读数据模型；配置用 `table`，不再有 `kind`（D4）。 |
| G0-4 鉴权 | 通过（按文档机制；数据库角色层已实测，网关层待验证） | 凭据是 Publishable Key（JWT `role: anon`），从环境变量 `CLOUDBASE_PUBLISHABLE_KEY` 或 `--secrets-stdin` 读取（D5）。只读靠两层：表上 `GRANT SELECT ... TO anon` 加 ADMINWRITE 模式的 RLS `FOR SELECT` 策略，存储上 `storage.objects` 的 `FOR SELECT TO anon` 策略。机制是**文档**写明的。2026-11-23 在 PG 模式试用环境里**实测**：数据库角色层 `anon` 加 RLS 只返回已发布行；网关加 Publishable Key 的验证等用户提供密钥，在 T10 冒烟前完成。凭据的 `role` 是 `service_role` 时 `doctor` 报 `CloudBaseAdminCredentialInUse`。 |
| G0-5 Transport | 通过 | 自己写只依赖 BCL 的 transport。证据：`Tencent.CloudBase` 1.0.1 的 PublishAot 试编译有 5 条警告（IL3050×3、IL2026×2），并引入 3 个 Microsoft.Extensions 依赖。 |
| G0-6 签名链接 | 通过（文档 + 实测） | 签名链接 = 网关主机 `{envId}.api[.intl].tcloudbasegateway.com` + 路径 `/v1/storages/object/sign/{bucket}/{object}` + 查询参数 `token`（**文档**，2026-11-23 **实测**）。签名响应的 `signedURL` 是相对路径，`fullSignedURL` 可能不存在：有就用它，否则网关源加 `signedURL`，并校验协议、主机和路径前缀。兑换不需要 Authorization，境内和境外主机都返回 200。批量签名单次最多 500 个，`expiresIn` 单位秒；上限没有文档，P1 取默认 3600、范围 60～3600。主机、路径前缀、参数名集中定义在一处，真实环境核实后再调整（D10）。 |

## 调研结果

来源：Phase 0 公开文档调研（2026-11-23，传统模式和 PG 模式两份报告）和 S2 SDK 试编译。标注：**文档** = 官方文档或
OpenAPI 明确写出；**推断** = 未见于任何官方材料的结论。

1. **模式（文档）**：环境模式在创建时固定。PG 模式不支持数据模型，业务数据放在 `public` schema 的表里，用 PostgREST 读取。
   没有文档化的 API 字段区分 PG 模式和传统模式，所以 P1 只支持 PG 模式，不做自动探测。
2. **网关主机（文档）**：境内 `https://{envId}.api.tcloudbasegateway.com`，境外 `https://{envId}.api.intl.tcloudbasegateway.com`。
   envId 字符集没有文档定义，示例都是小写字母、数字和连字符（推断为 DNS 标签）。
3. **PostgREST 读接口（文档）**：`GET /v1/rdb/rest/{table}`，查询参数 `select`、`order`（`field.asc|desc`，可多个）、`limit`、`offset`；
   等值过滤写成 `col=eq.value`，请求前做 URL 编码；请求头 `Prefer: count=exact` 时响应头 `Content-Range` 给出总数（如 `0-9/100`）；
   `HEAD` 同一路径可只取总数。只支持 `public` schema。单次返回行数上限没有文档。错误体 `{code, message, details, hint}`；
   网关码 `PERMISSION_DENIED`（401/403）、`RESOURCE_NOT_FOUND`（404）等。`anon` 角色的 `statement_timeout` 是 3 秒。
   `200 []` 可能是 RLS 静默过滤的结果，不能当作"表里没有内容"的证据。
4. **鉴权（文档）**：`Authorization: Bearer <key>`。PG 模式的 Publishable Key、API Key、Access Token 都是 JWT，网关读其中的
   `role`（`anon`、`authenticated`、`service_role`）。Publishable Key 不过期、可以嵌入前端，权限等于 `anon` 被授予的权限；
   API Key 是 `service_role`，带 BYPASSRLS，严禁暴露。只读的最小权限组合是 Publishable Key + `GRANT SELECT` + ADMINWRITE RLS 策略。
5. **存储批量签名（文档）**：`POST /v1/storages/object/sign/{bucketId}`，请求体 `{expiresIn: 秒, ≥1, paths: 对象名[], ≤500}`，
   响应 `[{error, path, signedURL, fullSignedURL}]`，单项失败在 `error` 里表示。需要 `storage.objects` 上的 SELECT 策略。
   兑换签名链接 `GET .../sign/{bucket}/{object}?token=...` 不需要 Authorization。文档里兑换路径模板漏了 `/{objectName}`，
   所以不自己拼对象路径。**实测**：`signedURL` 是相对路径 `/v1/storages/object/sign/{bucket}/{object}?token=<JWT>`，CLI 的签名输出里没有 `fullSignedURL`；
   兑换不带 Authorization 时境内和境外主机都返回 200，篡改或缺少 `token` 返回 401（`STORAGE_INVALID_JWT`）；`token` 是带 `exp`、`iat`、`url` 的 JWT，有效期等于 `expiresIn`。
   规则：有 `fullSignedURL` 用它，否则网关源加 `signedURL`；都校验 https、主机等于网关主机、路径以 `/v1/storages/object/sign/{storageBucket}/` 开头。
6. **文件引用（文档 + 推断）**：PG 存储 API 的路径参数是桶内对象名，不是 `cloud://`。文档的兼容示例里出现过
   `cloud://<env-id>.<bucket>/<path>`，所以也接受这种形式；`<env>.<bucket>` 的拆分规则没有文档，因为 envId 不含 `.`，按第一个 `.` 拆分（推断）。
   对象名不能为空、不能以 `/` 开头、不能有连续 `/`（文档）。
7. **传统模式（背景，P1 不实现）**：数据模型列表 `POST /v1/model/prod/{modelName}/list`、系统字段 `_openid`/`createBy`/`updateBy`、
   换链接口 `get-objects-download-info`、社区观察到的临时链接域名 `*.tcb.qcloud.la`。这些只适用于传统模式环境，留作 P2 的背景。
8. **限流**：网关没有文档化的默认 QPS；429 码 `EXCEED_RATELIMIT`、`EXCEED_REQUEST_LIMIT`、
   `EXCEED_CONCURRENT_REQUEST_LIMIT`。推断：顺序分页，429/503 遵守 `Retry-After`，有界指数退避。
9. **SDK（S2 试编译）**：`Tencent.CloudBase` 1.0.1，PublishAot 有 5 条警告，依赖 3 个 Microsoft.Extensions 包。结论见 G0-5。

## 实施期决策

以下决策已写进方案 v4：

- `region` 取枚举 `cn`（默认）或 `intl`，分别对应 `.api.tcloudbasegateway.com` 和 `.api.intl.tcloudbasegateway.com`。
- `envId` 正则为 `^[a-z0-9][a-z0-9-]{0,62}$`（DNS 标签最长 63 个字符），D18 已同步更正。
- `table` 和 `idField` 正则为 `^[A-Za-z_][A-Za-z0-9_]{0,62}$`；`idField` 默认 `id`，作为分页排序的唯一 tiebreak 和内部来源标识（D6、D7）。
- `storageBucket` 正则为 `^[a-z0-9][a-z0-9._-]{0,62}$`，`fileFields` 非空时必填（D18）。
- `pageSize` 默认 100、上限 1000（行数上限没有文档，`anon` 语句超时 3 秒）。
- `signExpiresSeconds` 默认 3600、范围 60～3600（上限没有文档）。
- 硬性剔除列 `user_id`、`owner`、`created_by`、`updated_by`、`_openid`，配置只能追加，不能删除（D7）。
- 时间字段接受 ISO-8601 字符串（`timestamptz` 的 JSON 形式）和 epoch 数字。
- RLS 静默过滤导致的空结果由 `doctor` 输出提示，不新增诊断码；诊断码仍是 11 个。

## 批准范围

- 用户授权：独立 Core 任务（G0-2），P1 范围。
- 归档、推送、PR、合并、full/release gate、Native AOT 构建和真实环境冒烟都需要另行明确授权。
