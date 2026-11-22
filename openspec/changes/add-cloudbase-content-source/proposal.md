# Add built-in CloudBase content source

## Why

内部站点"travel 网站"的内容由编辑在腾讯云开发（CloudBase）里维护，需要 Bukit 在构建时直接读取，
不再先同步成本地 Markdown。现有内容源只有 `markdown` 和 `notion`，没有能读 CloudBase 的路径。

可行性分析（`docs/analysis/bukit-core-cloudbase-builtin-provider-feasibility-2026-11-22.zh-CN.md`）
的默认推荐是同步插件（方案 C）。用户已明确授权按方案 A（Core 内置 provider）实施，范围限于 P1。
设计见 [实施方案](../../../docs/plans/2026-11-23-cloudbase-content-source-implementation-design.zh-CN.md)，
任务顺序见 [任务计划书](../../../docs/plans/2026-11-23-cloudbase-content-source-task-plan.zh-CN.md)。

## What Changes

- `content.sources[].type` 新增 `cloudbase`，配置写在 `content.sources[].cloudbase` 下。配置类型全部 internal（D1）。
- 新项目 `src/Bukit-Core/Bukit.CloudBase/`：只依赖 BCL 的只读 transport，JSON 用源生成（D2、D3），
  登记为受治理程序集，公开面为空（D15）。
- `Bukit.Content/CloudBase/`：provider、字段投影（默认白名单，系统字段和个人字段硬性剔除）、
  `cloud://` 换链、Markdown 正文的不可信链接模式（D7、D9、D17）。
- 新增 `BKT-0C01`～`BKT-0C0B` 诊断码（D12）。
- 构建阶段和部署阶段的隐私检查并列加入 CloudBase 标识检测（D11）；签名链接识别加入 CloudBase 临时链接（D10）。
- `doctor` 新增 CloudBase 检查，使用管理员级凭据时警告（AC8）。
- P1 不做：缓存、离线快照、增量指纹、HTML 正文、非图片文件字段、关联字段、写回。

## Capabilities

### New Capabilities

- `cloudbase-content-source`：CloudBase 内容源的配置、读取、字段投影、正文链接安全、文件换链、凭据与日志、doctor。

### Modified Capabilities

- `core-report-contracts`：公开产物隐私检查覆盖 CloudBase 标识（新增 requirement，现有报告契约不变）。
- `media-localization`：识别 CloudBase 临时链接，以及 `fileFields` 非空时的本地化前提（D8）。
- `miniprogram-data`：小程序数据集拦截 CloudBase 临时链接。
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

记录日期 2026-11-23，由控制者根据用户指示记录。

| 门禁 | 结论 | 说明 |
|---|---|---|
| G0-1 内部消费方 | 部分通过 | 站点：travel 网站；负责人：张三。**业务理由和可验证的验收标准待用户补充**，补齐前本项不算完全通过。 |
| G0-2 Core 任务授权 | 通过 | 用户明确授权把本变更作为一个独立的、用户可见的 Core 任务实施。按方案第 2 节照抄说明：`AGENTS.md` 的 Website/Core 隔离规则字面上针对的是缺陷修复，把它用在新增能力上是类推，不是规则原文。 |
| G0-3 数据形态 | 通过（访问路径待定） | 环境为 PG 模式（用户确认）；数据形态用户称为"数据模型"。PG 模式下的数据访问路径（数据模型 HTTP API 还是 PostgREST 表接口）与存储换链接口（PG 预签名接口，批量上限 500）正在调研，确定后在 T01/T03 前修订方案（方案 v4）。`kind` 只接受 `model`（D4）。 |
| G0-4 鉴权 | **用户指示的偏离** | 未在真实环境验证。用户指示"先实现代码，稍后提供（测试环境）"。P1 只实现一种凭据策略：通过环境变量 `CLOUDBASE_API_KEY` 读取 API Key，按公开文档以 `Authorization: Bearer <key>` 发送。API Key 按文档是管理员权限，因此视为管理员级凭据，`doctor` 报 `CloudBaseAdminCredentialInUse` 警告。PG 模式可能可以用 publishable/anon key 加 `GRANT SELECT` 加 RLS 只读策略实现最小权限，是否采用在方案 v4 中决定。真实验证推迟到 T10 冒烟测试。 |
| G0-5 Transport | 通过 | 自己写只依赖 BCL 的 transport。证据：`Tencent.CloudBase` 1.0.1 的 PublishAot 试编译有 5 条警告（IL3050×3、IL2026×2），并引入 3 个 Microsoft.Extensions 依赖。 |
| G0-6 临时链接 | **用户指示的偏离** | 临时链接的域名和签名参数只来自公开文档和社区观察，未在真实环境验证。实现时集中在一处、便于调整的定义里，在真实环境上重新核实。 |

## 调研结果

来源：Phase 0 公开文档调研（2026-11-23）和 S2 SDK 试编译。标注：**文档** = 官方文档或 OpenAPI 明确写出；
**推断** = 未见于任何官方材料的结论。以下第 1～6 项中标为"传统模式"的事实只适用于传统模式环境。

1. **网关主机（文档）**：境内 `https://{envId}.api.tcloudbasegateway.com`（另有 `.cn`、`.com.cn` 备用域名），
   境外 `https://{envId}.api.intl.tcloudbasegateway.com`。SDK 的 `region` 参数不改变主机，只有境内/境外之分。
   envId 字符集没有文档定义，示例都是小写字母、数字和连字符（推断为 DNS 标签）。
2. **鉴权（文档）**：`Authorization: Bearer <access_token/apikey/publishable_key>`。API Key 为"管理员权限"、长期有效、
   严禁客户端使用；Publishable Key 为匿名用户权限。**只读范围的 API Key 没有文档**，按管理员级密钥对待。
3. **数据模型列表（传统模式，文档）**：`POST /v1/model/prod/{modelName}/list`，请求体字段
   `filter.where`、`select`、`pageSize`、`pageNumber`（从 1 开始）、`getCount`、`orderBy`（最多 3 个字段）。
   成功响应 `{data:{records,total},requestId}`；业务错误在 2xx 响应体里以 `code`/`message`/`requestId` 表示，
   要按是否有 `code` 判断，不能只看状态码。单次最多 200 条（文档）。网关级错误（401、402、403、404、429）仍用 4xx。
4. **系统字段（传统模式数据模型，文档）**：`_id`、`createdAt`、`updatedAt`、`owner`、`createBy`、`updateBy`、
   `_departmentList`、`_openid`。示例中 `createdAt`/`updatedAt` 是 epoch 毫秒数字，关联字段是字符串用户 ID。
5. **换链接口（传统模式，文档 + 推断）**：`POST /v1/storages/get-objects-download-info`，请求和响应都是裸数组，
   按下标对应结果，失败项带 `code`/`message`。`maxAge` 不在 OpenAPI 里，官方 JS SDK 会下发，单位秒，
   超过 3600 只警告。批量上限没有文档，按 50 一批（推断，参照小程序接口）。
6. **临时链接（传统模式，推断）**：社区观察到的域名是 `*.tcb.qcloud.la`，签名参数 `sign`、`t` 是**推断**，
   官方文档和 SDK 里都没有。实现上把返回链接当作不透明值，域名和参数名集中定义，真实环境再核实。
7. **PG 模式（文档，细节待补）**：PG 模式用 PostgREST 加 RLS，角色有 `anon`、`authenticated`、`service_role`；
   存储提供预签名下载链接接口（单个和批量，批量最多 500）。详细调研见 Phase 0 PG 报告（进行中），
   结论写进方案 v4。
8. **限流**：网关没有文档化的默认 QPS；429 码 `EXCEED_RATELIMIT`、`EXCEED_REQUEST_LIMIT`、
   `EXCEED_CONCURRENT_REQUEST_LIMIT`。推断：顺序分页，429/503 遵守 `Retry-After`，有界指数退避。
9. **SDK（S2 试编译）**：`Tencent.CloudBase` 1.0.1，PublishAot 有 5 条警告，依赖 3 个 Microsoft.Extensions 包。结论见 G0-5。

## 实施期决策

以下决策与访问路径无关，现在确定，与方案一致：

- `region` 取枚举 `cn`（默认）或 `intl`，分别对应 `.api.tcloudbasegateway.com` 和 `.api.intl.tcloudbasegateway.com`。
- `envId` 正则为 `^[a-z0-9][a-z0-9-]{0,62}$`。这把 D18 的 `{0,63}` 更正为 DNS 标签的最大长度 63 个字符。
- 硬性剔除原则：系统字段和个人字段在任何 `fieldPolicy` 模式下都不进入公开产物；记录主键按 D7 处理，
  只作内部来源标识。
- 临时链接 `maxAge` 以 3600 秒为上限。

以下事项**待 v4 按访问路径确定**：

- `pageSize` 的默认值和上限（传统模式数据模型 API 为默认 100、上限 200 的候选）。
- 硬性剔除的具体字段清单（传统模式数据模型为 `_openid`、`owner`、`createBy`、`updateBy`、`_departmentList`）
  和时间字段格式（传统模式为 epoch 毫秒）。

## 批准范围

- 用户授权：独立 Core 任务（G0-2），P1 范围。
- 归档、推送、PR、合并、full/release gate、Native AOT 构建和真实环境冒烟都需要另行明确授权。
