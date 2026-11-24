# Bukit Core 内置 `type: cloudbase` 内容源：实施方案

- 日期：2026-11-23
- 状态：**执行中**。2026-11-23 起按用户授权开始执行。G0-1～G0-5 已记录：G0-1 的业务理由和验收标准待用户补充；G0-4 已在真实环境验证（2026-11-23：网关 + Publishable Key + RLS，草稿行被隐藏，计数、分页、过滤和批量签名都正常）。G0-6 已按文档解决，并已在真实环境验证（2026-11-23 探测）。记录见 openspec `add-cloudbase-content-source` 的 proposal。门禁规则见第 2 节：G0-1～G0-5 不通过就不能开始 T00；G0-6 不通过就不能开始 T06。
- 修订：v4（2026-11-23）。v2 根据第一轮独立审计（Critical 1 / Important 12 / Minor 15）修订；v3 根据第二轮严格审计（Critical 0 / Important 11 / Minor 10）修订；v4 根据用户确认的 PG 模式和 PG 模式公开文档调研修订；v4.1 补入 2026-11-23 的真实环境实测结果。处置和变更记录见附录 A、B、C。
- 依据：[可行性分析](../analysis/bukit-core-cloudbase-builtin-provider-feasibility-2026-11-22.zh-CN.md)，下文简称"分析文档"。分析文档经过三轮独立审计修订，提交 `52699535`。
- 配套：[实施任务计划书](2026-11-23-cloudbase-content-source-task-plan.zh-CN.md)。本文件讲"做成什么样、为什么这样做"；计划书讲"按什么顺序做、怎么做、怎么验收"。
- 代码基线：提交 `5fdc9dd2` 时的仓库内容。正文用文件和符号定位，不依赖行号。
- 范围：只覆盖分析文档中的 P1（最小可用版）。P2（缓存、离线快照）和 P3（HTML 正文、关联字段等）只在第 13 节列出提纲。
- 标注约定：
  - "决策"指本方案的设计选择，需要在 G0 时一并确认；
  - "文档"指 CloudBase 官方文档或 OpenAPI 明确写出的事实，"推断"指文档没有写、由我们推出的结论，"实测"指 2026-11-23 在一个 PG 模式试用环境里探测验证过的事实（证据见 Phase 0 真实环境探测报告）；
  - "估算"指推算出来的数字；
  - "待 G0 定"指要等门禁调研的结果才能确定。

## 0. 先读这一段

1. **这是一个有条件的方案。** 分析文档的结论是：方案 A 技术上可行，治理门槛高，默认推荐的仍是方案 C（同步插件）。用户已授权走 A。v4 确认环境是 **PG 模式**：数据是 `public` schema 里的 PostgreSQL 表或视图，通过 PostgREST 读取；鉴权有文档化的最小权限路径，即 Publishable Key（`anon`）加 `GRANT SELECT` 和 ADMINWRITE 模式的 RLS 策略。
2. **G0 不通过就停止。** 如果真实环境证明只能用管理员级凭据（`service_role`），而负责人又不书面接受这个风险，就不实施 A，改走方案 C。
3. **P1 的边界**：
   - 只支持 PG 模式环境 `public` schema 里的一张表或视图（传统模式环境、数据模型、pgdoc 集合都不支持）；
   - 只读；
   - 只支持 Markdown 正文；
   - 文件字段只支持图片，值是配置的固定桶里的对象名（也接受 `cloud://<env>.<bucket>/<path>` 兼容形式）；
   - 不做缓存，不做离线。
4. **public API 的变化**：
   - 所有配置类型和 transport 类型都是 internal；
   - public-api-drift 会出现两类预期变化：一是 `DiagnosticCode`（public enum）新增成员；二是新程序集 `Bukit.CloudBase` 登记为受治理程序集（D15），它的公开面为空。
5. **测试不连真实网络。** 自动化测试全部使用伪造的 HTTP 处理器。真实环境的冒烟测试要获得授权才做，而且手动执行。

## 1. 目标、非目标与验收

### 1.1 目标

在 site.yaml 里写 `content.sources[].type: cloudbase` 之后，`bukit config check`、`bukit doctor`、`bukit build` 能完成下面这些事：

- 从 CloudBase PG 模式环境读取 `public` schema 里一张表或视图的行（PostgREST）；
- 把行映射成 Bukit 内容文档；
- 用 Markdown 正文渲染页面；
- 把存放在 CloudBase 的图片本地化后发布。

### 1.2 非目标（P1 不做）

- 传统模式环境（数据模型、文档型数据库）、pgdoc 集合、`public` 以外的 schema；
- HTML 富文本正文；
- 非图片文件字段；
- 缓存（`cacheMode` 只接受 `off`）、离线快照、增量指纹；
- 关联字段、跨源关系投影；
- 写回 CloudBase；
- miniprogram 的 body 规则（放到 P3）；
- 把 Notion 专用的隐私、快照设施抽象成通用扩展点（记为技术债）。

### 1.3 验收标准

| 编号 | 验收条件 |
|---|---|
| AC1 | 配置了 `type: cloudbase` 的站点能通过 `config check`。以下情况都要以 CloudBase 诊断码失败：缺少必填项、取值越界、`envId` 不符合字符集、凭据写进 YAML、违反 D8 第 ①②④⑤ 条。D8 第 ③ 条中"占位图存在"这一半只能在构建阶段判断：`build` 以 `CloudBaseConfigInvalid` 失败，`config check`/`doctor` 给出警告 |
| AC2 | 用伪造的 HTTP 响应跑 `build`，端到端生成页面。要求：按 `limit`/`offset` 分页读完全部行（已知 `Content-Range` 总数时读到 offset 达到总数或空页才停，没有总数时以短页判断结束）；按 `maxItems` 截断；查询请求正确带上 `eq.` filter，以及以 `idField` 结尾的 order |
| AC3 | 字段投影默认用白名单。`user_id`、`owner`、`created_by`、`updated_by`、`_openid` 这些列在任何 `fieldPolicy` 模式下都不会进入公开产物；`idField`（默认 `id`）只作内部来源标识，`all` 模式下也不投影（D7） |
| AC4 | Markdown 正文里，链接、自动链接（`<scheme:...>`）、引用式链接定义和图片中的 `javascript:`、`vbscript:`、`data:`、`//host` 被丢弃，大小写变体和 HTML 实体编码（如 `jav&#x61;script:`）也一样；`#片段` 和不带协议的相对链接保留。这些都要有测试覆盖 |
| AC5 | `fileFields` 里的对象名（或 `cloud://` 兼容形式）和正文图片位置里的 `cloud://` 兼容引用，通过 PG 存储批量签名接口换成签名链接，并在同一次构建内下载到本地；其他位置的 `cloud://` 不换链，按 D17 降级并报诊断。发布产物里不能出现 `cloud://`、envId、网关主机、存储 CDN 主机和签名链接 |
| AC6 | 构建阶段的公开产物隐私检查能检测出 CloudBase 标识（envId、`cloud://` 兼容形式、网关主机、签名路径加 `token`、存储 CDN 主机），并让构建失败。部署隐私校验（不读配置，对所有站点生效）只检测两类：URL 位置上的 `cloud://`（定义见 D11），以及出现在任何文本位置的带 `token` 签名链接（网关主机加签名路径加 `token`、存储 CDN 主机加 `token`）；正文、代码块里提到的 `cloud://` 不算；envId 的检测方式按 D11 |
| AC7 | 凭据（Publishable Key 虽然不是秘密，也不输出）、`token`、签名链接不出现在任何日志和异常文本中，并有测试覆盖 |
| AC8 | `doctor` 能报告连通性、权限、表或视图是否可达、字段映射是否有效。凭据的 JWT `role` 是 `service_role` 或无法解析时给出警告；表可达但结果为空时提示可能是 RLS 静默过滤 |
| AC9 | closure 给出的 specialtyTests 全部通过。另外：`dotnet-format.sh` 通过；`code-analysis-ratchet.sh check` 通过，并且 CA1502/CA1506 的计数没有上升；`public-api-drift.sh check Debug` 只报告第 0 节第 4 条列出的两类预期变化，且基线已更新；`Bukit.CloudBase` 行覆盖率不低于 70% |
| AC10 | 以下内容都已同步：用户文档、开发文档、技能文档、排错文档、`guide/dev/cli.md` 的 BukitJalil 消费方契约表、三语 CHANGELOG、openspec 规格（新规格，以及受影响的 4 个现有规格的变更） |

## 2. 前置门禁 G0

G0 的结论由负责人书面记录在 openspec 提案里。**G0-1～G0-5 是 T00 的前置条件；G0-6 是 T06 的前置条件。**

| 门禁 | 内容 | 通过标准 | 不通过时 | 结论（2026-11-23） |
|---|---|---|---|---|
| G0-1 内部消费方 | 具名站点、负责人、业务理由、可验证的验收标准。这是产品定位文档的要求 | 写进 openspec proposal | 不立项 | 部分通过：travel 网站，负责人张三；业务理由和验收标准待用户补充 |
| G0-2 Core 任务授权 | 作为一个独立的、用户可见的 Core 任务，取得明确确认。`AGENTS.md` 的 Website/Core 隔离规则字面上针对的是缺陷修复；把它用在新增能力上是类推，这一点沿用分析文档第 7 节的说法 | 用户明确授权 | 不立项 | 通过：用户已授权 |
| G0-3 数据形态 | 消费方环境的模式，以及数据存放的位置和读取接口 | 调研 S1 给出结论 | 无法确定就不立项 | 通过：PG 模式，`public` schema 的表或视图，PostgREST 读取（PG 模式不支持数据模型，文档） |
| G0-4 鉴权 | 在 G0-3 确定的数据形态下，CI 可用的鉴权方式是什么、权限范围多大 | 能收窄到只读；或者负责人书面接受管理员级凭据的风险，并确定补偿措施 | 改走方案 C | 通过（文档机制）：Publishable Key + `GRANT SELECT` + ADMINWRITE RLS + 存储 `FOR SELECT TO anon` 策略。实测通过（2026-11-23）：网关 + Publishable Key + RLS，草稿行被隐藏，计数、分页、过滤和批量签名都正常 |
| G0-5 Transport | 官方 C# SDK `Tencent.CloudBase` 能否兼容 Native AOT 和裁剪，依赖能否接受 | 调研 S2 给出结论：复用 SDK 或自己写 | 默认自己写 | 通过：自己写（S2 试编译有 5 条 AOT/裁剪警告） |
| G0-6 签名链接 | 签名链接的主机和签名参数名；有效期的单位和上限 | 调研 S3 给出结论 | T06 不能开始。与签名链接主机有关的 D8 校验也放在 T06 里实现 | 通过（文档 + 实测）：网关主机 + `/v1/storages/object/sign/{bucket}/{object}` + `token`；签名响应的 `signedURL` 是相对路径，`fullSignedURL` 可能不存在；兑换不需要 Authorization，境内和境外主机都返回 200；签名响应另有 `fullSignedURLForCDN`（`https://{envId}.storage.tcloudbase.com/{bucket}/{object}?token=…`，实测能兑换），provider 不使用；批量最多 500；`expiresIn` 单位秒，上限没有文档，取 3600 |

### 2.1 Phase 0 调研（只读，不改仓库代码）

调研结论记录在 openspec proposal 的"调研结果"一节，不写实现代码。S1～S3 已按公开文档完成（PG 模式）。S1（网关 + Publishable Key 的 PostgREST 读取）、S2（凭据角色）和 S3 已在真实环境验证（2026-11-23 探测）；S4 的内容样本待消费方提供。

- **S1 数据形态与查询接口**：
  - 确认环境模式和数据存放位置（结论：PG 模式，`public` schema 的表或视图）；
  - 确认 PostgREST 的查询路径、分页参数（`limit`/`offset`）、过滤（`eq.`）和排序（`order`）语法、总数（`Prefer: count=exact` + `Content-Range`）、分页上限（没有文档），以及要剔除的列；
  - 准备一份脱敏后的响应样本，作为测试夹具。
- **S2 鉴权与 SDK**：
  - 针对 PG 模式，确认各凭据对应的角色：Publishable Key（`anon`）、Access Token（`authenticated`，默认 2 小时过期）、API Key（`service_role`，BYPASSRLS）；只读路径是 Publishable Key + `GRANT SELECT` + ADMINWRITE RLS 策略；
  - 新建一个不入库的临时工程，对 `Tencent.CloudBase` 试编译 `PublishAot`，统计裁剪和 AOT 警告的数量。
- **S3 签名链接**：
  - 确认 PG 存储批量签名接口（`POST /v1/storages/object/sign/{bucketId}`）的请求和响应、单次上限（500）、`expiresIn` 的单位（秒）和上限（没有文档）；
  - 确认签名链接的主机和签名参数（网关主机、`/v1/storages/object/sign/` 路径、`token`）。实测结论：`signedURL` 是相对路径 `/v1/storages/object/sign/{bucket}/{object}?token=<JWT>`，CLI 的签名输出里没有 `fullSignedURL`；`token` 是带 `exp`、`iat`、`url` 声明的 JWT，有效期等于 `expiresIn`；篡改或缺少 `token` 时返回 401（`STORAGE_INVALID_JWT` 或 "token is required"）。用 Publishable Key 调 HTTP 批量签名时，响应有 `fullSignedURL`，另有 `fullSignedURLForCDN`（`https://{envId}.storage.tcloudbase.com/{bucket}/{object}?token=…`，能兑换），provider 不使用后者（D17）。
- **S4 内容样本**：
  - 确认正文是否包含 HTML、`#片段` 链接、相对链接、内嵌的 `cloud://` 兼容形式图片，以及 `<scheme:...>` 形式的自动链接；
  - 确认文件字段是否全是图片，值是对象名还是 `cloud://` 兼容形式。

## 3. 总体架构

```text
site.yaml ──> Bukit.Config（internal CloudBaseConfig，读取与校验）
                 │
                 ▼
ContentProviderFactory ──> Bukit.Content/CloudBase/CloudBaseContentSource : IContentProvider
                                  │  分页查询、字段投影、文件对象签名
                                  ▼
                           Bukit.CloudBase（新项目：只依赖 BCL，G0-5 已定自己写）
                                  │  鉴权、重试、限速、错误映射
                                  ▼
                           CloudBase PG 网关：PostgREST /v1/rdb/rest/{table}；存储批量签名 /v1/storages/object/sign/{bucket}
                                  │
CloudBaseBodyStore : IContentBodyStore ──> BasicMarkdownToHtml（不可信链接模式）
                                  │
                                  ▼
                 现有管线：ContentLoadStage → ImageLocalizeStage → 路由 → 渲染（正文图片延迟本地化）→ 隐私检查 → 输出
```

**数据流**：

1. `LoadRawAsync` 用 PostgREST 按 `limit`/`offset` 分页读取全部行（D6），带 `Prefer: count=exact`，order 以唯一的 `idField` 结尾；已知总数时读到 offset 达到总数或空页才停（短页可能只是网关的行数上限），没有总数时读到短页就停，最多取 `maxItems` 行。
2. 对每一行做字段投影：先硬性剔除敏感列，再应用白名单（D7）。
3. 只收集两类位置的文件引用（D17）：`fileFields` 字段里的对象名（或 `cloud://` 兼容形式），以及正文图片位置（`![]()` 和图片引用定义）里的 `cloud://` 兼容引用。按每批最多 500 个调用存储批量签名接口，按 D17 的取值规则得到签名链接（有 `fullSignedURL` 用它，否则网关源加相对的 `signedURL`），校验协议、主机和路径前缀后替换回原文。正文里已经是网关存储路由或存储 CDN 形式的 https 链接走现有媒体本地化；其他位置的 `cloud://` 按 D17 处理。
4. 返回 `RawContentDocument`。
5. `ImageLocalizeStage` 下载 `fieldKeys` 字段里的图片。正文里的图片在渲染阶段延迟本地化，由 `LocalizedContentBodyStore` 负责。
6. 失败分两段处理，各有各的兜底：
   - **换链失败**（第 3 步：单项 `error`、签名请求失败、协议/主机/路径前缀不符、`cloud://` 的 envId 或桶名与配置不一致、对象名不合规）：D8 管不到这一段。`ImageAssetLocalizer.ResolveWithoutDownload` 对非 http(s) 的值原样返回，裸对象名会被当成本地相对路径发布，`cloud://` 会原样留下。所以由 provider 自己兜底：按 D17 把这个值替换成 `content.media.defaultImageUrl`（占位图）。
   - **下载失败**（第 5 步，签名链接已经换好）：D8 保证 `fileFields` 是 `fieldKeys` 的子集、`onFailure` 是 `placeholder`、占位图存在，所以 `ImageAssetLocalizer.FailureResult` 只会返回占位图，不会把签名链接保留下来。

   这两段加上 D17 的换链范围，才保证签名链接、对象名、`cloud://` 和 envId 都不会原样发布；D11 在输出阶段再兜底一次。

**时效风险**：正文图片是在渲染阶段才下载的，构建耗时如果超过签名链接的有效期，下载就会失败。`signExpiresSeconds` 默认 3600（文档没有给上限，所以 P1 的上限也是 3600），写进第 12 节的风险表。

## 4. 设计决策

| 编号 | 决策 | 理由 |
|---|---|---|
| D1 | `ContentSourceConfig` 新增一个 **internal** 属性 `CloudBase`，类型是 internal 的 `CloudBaseConfig` | 有先例：`AppConfig.MiniProgram` 就是 internal，`ConfigContractDriftTests` 的注释写明它不增加 public API |
| D2 | 新建 `src/Bukit-Core/Bukit.CloudBase/` 项目，只依赖 BCL，负责 transport 以及请求和响应模型；provider 放在 `Bukit.Content/CloudBase/`。transport 不引用 `Bukit.Shared`，错误用自己的 `CloudBaseErrorKind`（加 HTTP 状态码）表示；映射到 `DiagnosticCode.CloudBase*` 的工作在 `Bukit.Content/CloudBase`（T04）和 doctor（T08）里做 | 与 `Bukit.Notion` / `Bukit.Content/Notion` 的分层方式对齐：`Bukit.Notion` 也没有项目引用，错误用自己的 `NotionDiagnostics` 表示，由外层格式化 |
| D3 | 自己写 transport（G0-5 已定：S2 试编译 `Tencent.CloudBase` 1.0.1 有 5 条 AOT/裁剪警告，并引入 3 个 Microsoft.Extensions 依赖） | Core 是 Native AOT（`Directory.Build.props` 给所有 Core 项目设置了 `IsAotCompatible`）；`Bukit.Notion` 是只依赖 BCL 的先例 |
| D4 | 没有 `kind`。`table`（必填）是 `public` schema 里的 PostgREST 表或视图名，按 `^[A-Za-z_][A-Za-z0-9_]{0,62}$` 校验 | PG 模式不支持数据模型（文档），只有 PostgREST 一种读取路径；表名会拼进 URL 路径，所以白名单校验 |
| D5 | 鉴权用一个凭据策略接口，P1 只实现 Publishable Key：从环境变量 `CLOUDBASE_PUBLISHABLE_KEY` 或 `--secrets-stdin` 读取，以 `Authorization: Bearer` 发送，YAML 里出现凭据字段就报错。客户端只解码、不验签 JWT payload 的 `role`，仅作诊断：`role` 是 `service_role` 时 doctor 报 `CloudBaseAdminCredentialInUse` 警告，`role` 缺失或无法解析时 doctor 同样报这个警告（无法确认不是管理员级），构建都照常继续。凭据从不输出到日志 | 不给没验证过的方式写代码。Publishable Key 不是秘密，但它实际上不过期（实测 `exp` 声明在约 73 年后），轮换要吊销，所以仍不输出；主要风险是操作者把 `service_role` 的 API Key 填进这个变量，`role` 诊断能发现 |
| D6 | 查询走 PostgREST：`GET /v1/rdb/rest/{table}?select=<列>&order=<sortField>.<dir>,<idField>.asc&limit=<pageSize>&offset=<n>`，带 `Prefer: count=exact`，总数从响应头 `Content-Range` 读取。`filter` 在 P1 只支持单字段等值，配置写成 `{ field, value }`，转成 URL 编码后的 `field=eq.value`，以后可以扩展出 `op` 字段；`sort` 只支持单字段，后面固定追加唯一的 `idField` 升序作 tiebreak。读到行数少于 `pageSize` 的页，或 offset 不小于总数时停止 | 只覆盖实际需要的查询，同时给以后留出兼容的扩展方式；offset 分页要有唯一排序键才稳定；只用文档列出的 `limit`/`offset`，不用没有文档的 `Range` 头 |
| D7 | 字段投影默认 `whitelist`。PG 和鉴权相关的敏感列 `user_id`、`owner`、`created_by`、`updated_by`、`_openid` 一律**硬性剔除**，`fieldPolicy.mode: all` 也不放行；配置可以追加剔除列，不能删除默认列（最终清单以 S1 的真实样本为准）。`idField`（默认 `id`）的值作为内部来源标识（相当于 Notion 的页面 ID）：用来生成稳定的文档标识，日志里只输出哈希；`all` 模式下不投影，只有在 `allowed` 里显式列出才进入公开字段，此时 doctor 给出警告 | 这些列是用户个人标识或操作者标识，不能指望配置写对来保证不泄露；行 ID 是来源标识，默认不公开，和 Notion 页面 ID 的处理方式一致 |
| D8 | `fileFields` 非空时，以下条件必须全部满足：① `content.media.downloadToLocal` 不是 false；② `fileFields ⊆ content.media.fieldKeys`；③ `content.media.onFailure` 是 `placeholder`，并且占位图存在；④ P1 只接受图片字段；⑤ `downloadHosts`/`skipHosts` 不排除网关主机 `{envId}.api.tcloudbasegateway.com`（或 `{envId}.api.intl.tcloudbasegateway.com`），也不排除存储 CDN 主机 `{envId}.storage.tcloudbase.com`（正文里作者写进去的 CDN 签名链接要靠媒体本地化下载；`fileFields` 为空时 D8 不生效，这种情况由 D11 的输出检测兜底，失败即关闭；境外环境的 CDN 主机形状没有实测，未验证）。①②④ 和 ③ 的 `onFailure` 部分在配置校验（T01）里检查。③ 的"占位图存在"只有构建时才知道：`PlaceholderMissing` 由构建设置，`MediaPlaceholderProbe` 是 `Bukit.Engine` 的 internal 类型，`Bukit.Config` 引用不到；所以由 `ContentProviderFactory.CreateCloudBaseProvider` 调用 `MediaPlaceholderProbe.FindMissing`，占位图缺失时以 `CloudBaseConfigInvalid` 让构建失败（T04），`config check`/`doctor` 复用 `MediaPlaceholderProbe.CheckWarning` 给出警告。第 ⑤ 条依赖 G0-6，放在 T06 实现 | 只有同时满足这几条，才能保证走本地化路径的签名链接不会原样发布（不走本地化路径的位置由 D17 处理）：`onFailure: keep` 会保留原链接；占位图缺失时也会保留原链接；不在 `fieldKeys` 里的字段根本不会被本地化 |
| D9 | Markdown 正文用"不可信链接"模式渲染。具体做法：`BasicMarkdownToHtml` 新增一个选项，开启后先 Parse，再遍历语法树里的 `LinkInline`（包括图片和引用式链接）和 `AutolinkInline`，最后用 `HtmlRenderer` 输出，原有的独立图片后处理保持不变。URL 策略只作用于解析后的 `LinkInline.Url` / `AutolinkInline.Url`（Markdig 已经解码过 HTML 实体），不作用于原始 Markdown 或 HTML 文本：① 用 RFC 3986 的语法判断"有没有协议"，即在第一个 `/`、`?`、`#` 之前匹配 `^[A-Za-z][A-Za-z0-9+.-]*:`，不能用 `Uri.TryCreate` 是否成功来判断（在 Unix 上 `/x` 会被解析成 `file:`）；② 有协议的，交给 `RenderingSafeUrl.ForLink` / `ForMedia` 判断（它们是 internal，`Bukit.Content` 可以访问；协议集合本身是 private，不能直接复用）；③ 以 `//` 开头的丢弃；④ `#片段` 和不带协议的相对链接保留。这个策略写成 `CloudBaseUrlPolicy` | `RenderingSafeUrl` 对相对链接和 `#片段` 会返回 null，直接拿来用会误删正常链接；`.DisableHtml()` 不会禁用自动链接；实测 Markdig 0.38.0 会把 `jav&#x61;script:`、`JAVASCRIPT:` 和引用定义里的 `javascript:` 原样输出到 `href` |
| D10 | `SignedUrlIdentity` 新增 CloudBase 规则：主机匹配 `*.api.tcloudbasegateway.com` 或 `*.api.intl.tcloudbasegateway.com`，并且路径以 `/v1/storages/object/sign/` 开头时，去掉参数 `token`（实测：签名链接就是这个形状，境内和境外主机都能兑换）。另有存储 CDN 形式（实测）：主机匹配 `*.storage.tcloudbase.com`（路径是 `/{bucket}/{object}`，没有固定前缀）时，同样去掉 `token`。实测的是境内网关签出的 CDN 链接；境外环境的 CDN 主机形状（例如是否有 `.intl.`）未验证，验证后在集中定义处补充。主机模式、路径前缀和参数名集中定义在一处。`token` 实测是带 `exp`、`iat`、`url` 声明的 JWT，但 P1 把它当作不透明值，不读过期时间，被拒绝时照常重试 | 关系到小程序签名链接拦截和媒体缓存；只看参数名会误伤别的主机上同名的 `token` 参数 |
| D11 | 隐私检查在现有 Notion 逻辑旁边**并列**加上 CloudBase 标识的检测。构建阶段（`PublicOutputPrivacyCheck`，在 `Bukit.Engine.Audit`）从配置读取 envId，并检测 `cloud://` 兼容形式、网关主机、签名路径 `/v1/storages/object/sign/` 加 `token` 的组合，以及存储 CDN 主机 `*.storage.tcloudbase.com`（构建阶段只在站点有 cloudbase 源时运行，所以可以只看主机）；因为 D1 让配置类型是 internal，`Bukit.Config` 的 `InternalsVisibleTo` 要加上 `Bukit.Engine.Audit` 和 `bukit`。部署阶段（`DeploymentPrivacyValidator`）现在不读配置，只从发布审计报告里取 Notion 的标识：P1 让它按固定模式检测，不需要配置；因为不读配置就对所有站点生效，部署阶段检测两类内容：(a) 带 `token` 的签名链接，在所有文本类文件的**全文**里查（包括代码块、`.js`、`.md`）；(b) `cloud://`，**只查 URL 位置**。主机一律要求带 `token` 才算签名链接（与 D10 和 miniprogram-data 一致）：网关主机加签名路径 `/v1/storages/object/sign/` 加 `token`、存储 CDN 主机 `*.storage.tcloudbase.com` 加 `token`；只出现主机（例如没有 CloudBase 源的站点链接到公开、未签名的 CDN 对象）不算泄露（v4 方案中部署阶段对网关主机写的是只看主机，一并改为带 `token`；现有代码里部署阶段还没有任何 CloudBase 检测）。`cloud://` 在部署阶段**只查 URL 位置**（用户决定），正文和代码里提到的 `cloud://` 不算，这样介绍 `cloud://` 的教程站点照常部署。URL 位置是：① HTML 类文件（`.html`、`.htm`、`.svg`、`.xml`、`.rss`）里任意标签的 URL 属性值，即 `src`、`href`、`srcset`（逐个候选）、`poster`、`data-src`、`action`、`formaction`、`cite`、`data`、`xlink:href`、`url`、`<meta>` 的 `content`（覆盖 `og:image`、`twitter:image`），以及 `style` 属性里 CSS `url(...)` 的参数；带引号和不带引号的属性值都算；只看标签属性，不看文本节点，所以 `<code>`、`<pre>` 里的 `cloud://` 不算；② `.css` 文件里 `url(...)` 的参数；CSS `url(...)` 的参数先去掉空白和成对的引号，再判断前缀；③ `.json`、`.jsonld` 里整个字符串值（与 miniprogram-data 一致）；三种都是去掉首尾空白后以 `cloud://` 开头（不区分大小写）才算。其他文本文件（`.md`、`.txt`、`.js`、`.mjs`、`.yaml`、`.yml`、`.csv`）不在部署阶段查 `cloud://`。已知缺口（部署阶段查不到，接受；有 cloudbase 源的站点由构建阶段的全文检测兜底）：RSS/XML 文本节点里转义过的 HTML（如 `<description>` 里的 `&lt;img src="cloud://…"&gt;`）；HTML 里内联的 JSON-LD（`<script type="application/ld+json">` 的内容是文本节点，不是属性，也不是 `.json` 文件）；实体编码的协议（如 `cloud&#58;//`，预筛和扫描都看原始文本，不做 HTML 解码）；JS 字符串。构建阶段（只在有 cloudbase 源时运行）保持全文检测。这样没有 CloudBase 源的站点行为不变；envId 由 deploy 命令读取 site.yaml 后传给校验器（决策，G0 时确认），不改 `.bukit/publish-audit-report.json` 这个 BukitJalil 消费的报告契约。P1 不做通用化 | 控制改动范围；改报告契约会牵动 `core-report-contracts` 和 BukitJalil |
| D12 | 诊断码新增 `CloudBase` 前缀，区间定为 `0x0C01`～`0x0CFF`，格式化后是 `BKT-0C01` 起 | `0x0C` 区间没有被占用。`BuildDiagnosticCodeTests` 用 `StartsWith` 匹配前缀，`CloudBase` 不会和 `Config`、`Content` 冲突 |
| D13 | `cacheMode` 只接受 `off` | 缓存属于 P2 |
| D14 | 内部诊断和日志只输出：行 ID 的哈希、表名、HTTP 状态码。不输出 URL 查询串、请求头、签名链接和响应体原文 | Core 日志不会脱敏来自环境变量的密钥 |
| D15 | `Bukit.CloudBase` 登记为受治理程序集：加入 `tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs` 的 `GovernedAssemblies`，基线里新增这个程序集的条目，公开面为空 | 不登记的话，它的公开面就没人管；`Bukit.Notion` 也是受治理程序集 |
| D16 | `Bukit.CloudBase` 的 `InternalsVisibleTo` 清单：`Bukit.Content`、`bukit`（Bukit.Cli 的程序集名，doctor 要用）、`Bukit.CloudBase.Tests`、`Bukit.Content.Tests`、`Bukit.Engine.Tests`、`Bukit.Cli.Tests`。`Bukit.Cli` 直接引用 `Bukit.CloudBase` | transport 类型全是 internal，doctor 和各个测试项目都要能注入伪造的处理器；参照 `Bukit.Notion.csproj` 的清单 |
| D17 | 换链范围只限两处：`fileFields`（值是 `storageBucket` 里的对象名，或 `cloud://<envId>.<bucket>/<path>` 兼容形式），以及正文图片位置里的 `cloud://` 兼容引用。签名链接的取值规则（实测）：响应里有 `fullSignedURL` 就用它，否则用网关源 `https://{envId}.api[.intl].tcloudbasegateway.com` 拼上相对的 `signedURL`；两种情况都要校验协议是 https、主机等于配置的网关主机、路径以 `/v1/storages/object/sign/{storageBucket}/` 开头，不满足的按 `CloudBaseFileResolveFailed` 处理。`cloud://` 按第一个 `.` 拆出 envId 和桶名（envId 不含 `.`），与配置不一致的不换链，按失败处理。**换链失败时的取值**：任何一种失败（单项 `error`、签名请求失败、协议/主机/路径前缀不符、envId 或桶名不一致、对象名不合规），provider 都把这个值替换成 `content.media.defaultImageUrl`（`MediaConfig.DefaultImageUrl`，默认 `/assets/images/noneimg-news.jpg`），也就是 `ImageAssetLocalizer.FailureResult` 在 `onFailure: placeholder` 下返回的同一个值；`fileFields` 替换字段值，正文替换图片的 URL，并报 `CloudBaseFileResolveFailed`。占位图一定存在：D8 第 ③ 条让占位图缺失的构建直接失败。占位图是本地路径，`ImageAssetLocalizer` 把它当本地资源原样保留。不能把裸对象名、`cloud://` 或 envId 留给 `ImageAssetLocalizer`，因为它对非 http(s) 的值原样返回，不会落到占位图。provider 只用网关形式（`fullSignedURL`，或网关源加 `signedURL`），从不使用响应里的 `fullSignedURLForCDN`（实测存在，形如 `https://{envId}.storage.tcloudbase.com/{bucket}/{object}?token=…`），这样只有一个主机需要校验。正文里作者已经写好的网关存储路由或存储 CDN 形式的 https 链接走现有媒体本地化。其他位置一律不换链：正文里指向非图片的链接（如 `[pdf](cloud://…/a.pdf)`）降级为纯文本；不在 `fileFields` 里的公开字段，值里含 `cloud://` 时整个值被丢弃。两种情况都报 `CloudBaseUnsupportedFileReference` 警告 | 正文的 `<a href>` 只有指向图片时才会被本地化（`HtmlMediaReferenceScanner`），不在 `fieldKeys` 里的字段也不会被本地化；在这些位置换成签名链接，就会原样发布 |
| D18 | `envId` 在配置校验时按白名单检查，规则是 `^[a-z0-9][a-z0-9-]{0,62}$`（v4 从 `{0,63}` 更正为 DNS 标签最长 63 个字符），S1 只能把它收得更严。`storageBucket` 按 `^[a-z0-9][a-z0-9._-]{0,62}$` 校验；对象名不能为空、不能以 `/` 开头、不能有 `//` 或 `..` 段。transport 用 `UriBuilder` 拼网关地址，发送凭据之前断言协议是 https、主机名正好是 `{envId}.api.tcloudbasegateway.com` 或 `{envId}.api.intl.tcloudbasegateway.com`；签名链接（按 D17 的取值规则得到）也要满足同样的协议和主机，路径还要以 `/v1/storages/object/sign/{storageBucket}/` 开头，才会下载 | 网关主机名由 `envId` 拼出来；`envId` 里如果有 `/`、`#`、`?`、`@`、`.`，请求就会发到别的主机，并带上凭据；桶名和对象名会拼进签名请求，`..` 和 `//` 可能改写路径 |

## 5. 配置契约（P1）

```yaml
content:
  sources:
    - name: articles
      type: cloudbase
      mode: content
      collection: posts            # 现有通用字段：Bukit 的内容集合名，不是 CloudBase 的表
      cloudbase:
        envId: prod-xxxx
        region: cn                 # cn 或 intl，决定网关域名（境内或境外）
        table: articles            # public schema 里的表或视图（PostgREST）
        idField: id                # 唯一列：分页 tiebreak 和内部来源标识
        pageSize: 100
        maxItems: 500
        filter: { field: status, value: published }
        sort: { field: publish_at, direction: desc }
        titleField: title
        slugField: slug
        publishAtField: publish_at
        updatedAtField: updated_at
        bodyField: content
        bodyFormat: markdown       # P1 只接受 markdown
        fieldPolicy: { mode: whitelist, allowed: [title, slug, publish_at, cover, tags] }
        fileFields: [cover]        # 必须是 content.media.fieldKeys 的子集，并且只能是图片
        storageBucket: media       # fileFields 非空时必填：对象名所在的桶
        signExpiresSeconds: 3600   # 签名链接有效期（秒）
        cacheMode: off             # P1 只接受 off
        maxRps: 5
        maxRetries: 3
```

| 字段 | 必填 | 默认值 | 校验 |
|---|---|---|---|
| `envId` | 是 | — | 符合 `^[a-z0-9][a-z0-9-]{0,62}$`（D18，S1 只能收得更严）；会被隐私检查当作敏感标识 |
| `region` | 否 | `cn` | 枚举 `cn` 或 `intl`，决定用 `.api.tcloudbasegateway.com` 还是 `.api.intl.tcloudbasegateway.com` |
| `table` | 是 | — | 符合 `^[A-Za-z_][A-Za-z0-9_]{0,62}$`（D4） |
| `idField` | 否 | `id` | 同 `table` 的规则；必须是唯一列（D6） |
| `pageSize` | 否 | 100 | 1～1000（行数上限没有文档；`anon` 语句超时 3 秒） |
| `maxItems` | 否 | 不限 | 大于 0 |
| `filter` | 否 | 无 | P1 只允许 `field` 和 `value` 两个键 |
| `sort` | 否 | 无 | `direction` 只能是 `asc` 或 `desc` |
| `*Field` 映射 | 否 | 同名字段 | 非空字符串。时间字段接受 ISO-8601 字符串（`timestamptz` 的 JSON 形式）和 epoch 数字 |
| `bodyField` | `mode: content` 时必填 | — | 非空 |
| `bodyFormat` | 否 | `markdown` | P1 只接受 `markdown` |
| `fieldPolicy` | 否 | `whitelist`，允许列表为空 | 取值沿用 Notion 的 `whitelist` 和 `all`；硬性剔除列表不受它影响，可以追加不能删除；`idField` 的处理见 D7 |
| `fileFields` | 否 | 空 | 非空时触发 D8 的全部校验，并要求 `storageBucket` |
| `storageBucket` | `fileFields` 非空时必填 | — | 符合 `^[a-z0-9][a-z0-9._-]{0,62}$`（D18） |
| `signExpiresSeconds` | 否 | 3600 | 60～3600（文档没有给上限） |
| `cacheMode` | 否 | `off` | P1 只接受 `off` |
| `maxRps` / `maxRetries` | 否 | 估算 5 / 3 | `maxRps` 大于 0；`maxRetries` 不小于 0 |

**凭据**：Publishable Key，变量名 `CLOUDBASE_PUBLISHABLE_KEY`（D5）。读取统一通过 `SecretStore.Get`。不往 public 的 `EnvironmentHelper` 加任何成员，否则会造成 public-api-drift。

**消费方需要的数据库设置**（写进 T09 的用户文档）：表上 `GRANT SELECT ... TO anon`，加一条 ADMINWRITE 模式的 `FOR SELECT` RLS 策略（例如只放行 `published = true` 的行）；`storage.objects` 上加 `FOR SELECT TO anon USING (bucket_id = '<桶名>')` 策略。

## 6. 诊断码（暂定）

| 名称 | 值 | 场景 |
|---|---|---|
| `CloudBaseConfigInvalid` | 0x0C01 | 配置缺失、越界、出现未知字段、`envId`/`table`/`idField`/`storageBucket` 不合规、违反 D8 的第 ①～④ 条（第 ③ 条的占位图部分在构建时报） |
| `CloudBaseCredentialMissing` | 0x0C02 | 找不到所需的凭据 |
| `CloudBaseAuthFailed` | 0x0C03 | 401（JWT 缺失、无效）或 403（GRANT 或 RLS 不允许） |
| `CloudBaseNotFound` | 0x0C04 | 环境、表或视图不存在（404；码可能是 `RESOURCE_NOT_FOUND`，实测表不存在时是 `DATABASE_PGRST205`） |
| `CloudBaseRateLimited` | 0x0C05 | 重试后仍被限流 |
| `CloudBaseRequestFailed` | 0x0C06 | 其他 HTTP 或网络错误 |
| `CloudBaseResponseInvalid` | 0x0C07 | 响应结构不符合预期、`Content-Range` 无法解析，或超过大小上限 |
| `CloudBaseFileResolveFailed` | 0x0C08 | 文件对象签名失败：批量签名请求失败、单项返回 `error`、对象名或 `cloud://` 引用不合规，或签名链接不是 https、主机不对、路径前缀不对 |
| `CloudBaseMediaLocalizationRequired` | 0x0C09 | 违反 D8 的第 ⑤ 条（`downloadHosts`/`skipHosts` 排除了网关主机或存储 CDN 主机） |
| `CloudBaseAdminCredentialInUse` | 0x0C0A | doctor 警告：凭据的 JWT `role` 是 `service_role`，或者 `role` 缺失、无法解析 |
| `CloudBaseUnsupportedFileReference` | 0x0C0B | 警告：`cloud://` 出现在不换链的位置，已降级或丢弃（D17） |

表可达但结果为空（可能是 RLS 静默过滤）时，doctor 只输出一条提示，不新增诊断码；诊断码仍是 11 个。最终清单在 T02 定稿。`DiagnosticCodeFormatter.Describe` 要为每个码返回 "CloudBase: …" 形式的说明。transport 不产生这些码，只产生 `CloudBaseErrorKind`，映射见 D2。

BukitJalil 按固定表给 BKT 码分类。它维护中的消费方契约在 `guide/dev/cli.md` 的 "BukitJalil consumer contract" 表里，所以要在这张表里登记 `BKT-0Cxx` 一行（这一节在文件里重复出现了两次，第 59 行和第 89 行；两处都要改，重复本身记为范围外发现），契约的负责测试是 `DiagnosticCodeFormatterTests`。BukitJalil 那边要同步更新码表和 `MIN_BUKIT_VERSION`，这一步不在本仓库，由计划书的 Part B 单独交接，并另外取得授权。

## 7. 安全设计

| 威胁 | 对策 | 验收 |
|---|---|---|
| CI 凭据泄露 | Publishable Key 本身是低权限凭据，能力只等于 `anon` 被授予的只读权限（GRANT SELECT + RLS）。仍然只放在 CI 的密钥库里，不输出到日志 | AC7、AC8 |
| 操作者把 `service_role` 的 API Key 填进 `CLOUDBASE_PUBLISHABLE_KEY` | D5：解码 JWT `role`，`service_role` 或无法解析时 doctor 警告；文档说明风险 | AC8 |
| 恶意 `envId`、表名、桶名或对象名改写请求目标 | D4、D18：配置校验白名单，对象名校验，发送前断言主机名 | AC1、AC7 |
| 签名接口返回指向别的主机或别的桶的链接 | D17、D18：签名链接必须是 https，主机必须等于网关主机，路径必须以 `/v1/storages/object/sign/{storageBucket}/` 开头，否则按 `CloudBaseFileResolveFailed` 处理 | AC5 |
| 日志或异常泄露密钥 | 按 D14 处理；transport 的异常信息里不拼接 URL 和请求头 | AC7 |
| 个人数据进入公开产物 | D7 硬性剔除；默认白名单；D11 的隐私检查兜底 | AC3、AC6 |
| 正文链接注入（包括自动链接、引用定义、大小写和实体编码） | D9 | AC4 |
| 签名链接被原样发布 | D8 堵住本地化路径上会保留原链接的情况；D17 不让签名链接出现在不会被本地化的位置；D10 识别签名链接；D11 在输出阶段检测网关主机、签名路径加 `token` 和存储 CDN 主机 | AC5、AC6 |
| 签名链接在构建过程中过期 | `signExpiresSeconds` 默认 3600；下载失败时落到占位图，并报诊断 | AC5 |
| 远程响应异常（超大、畸形） | 限制分页上限和响应体大小，超出时报 `CloudBaseResponseInvalid` | AC2 |
| RLS 静默过滤，构建读到空结果 | doctor 在表可达但结果为空时给出提示；文档写明 GRANT 和 RLS 的设置 | AC8 |

## 8. 组件与文件清单

**新增**（估算 10～14 个文件）：

- `src/Bukit-Core/Bukit.CloudBase/Bukit.CloudBase.csproj`
- `Bukit.CloudBase/Transport/`：`CloudBaseClient`（PostgREST 查询和存储批量签名）、`CloudBaseClientOptions`、`CloudBaseApiException`、Publishable Key 凭据策略、JWT `role` 诊断解码（只解码不验签）
- `Bukit.CloudBase/Models/`：请求和响应模型，加上源生成的 JSON 上下文
- `src/Bukit-Core/Bukit.Content/CloudBase/`：`CloudBaseContentSource`、`CloudBaseContentSourceOptions`、`CloudBaseFieldProjector`、`CloudBaseFileResolver`、`CloudBaseBodyStore`、`CloudBaseUrlPolicy`
- `src/Bukit-Core/Bukit.Cli/Commands/DoctorCloudBaseChecker.cs`
- `tests/Bukit.CloudBase.Tests/`
- `openspec/changes/add-cloudbase-content-source/`：proposal、design、tasks；`specs/cloudbase-content-source/spec.md`；另外为 `core-report-contracts`、`media-localization`、`miniprogram-data`、`incremental-invalidation` 四个现有规格各写一份变更
- 新增一份 CloudBase 用户文档：`guide/user/27-cloudbase-content.md`（现有最大编号是 26；路径在 T00 固定，因为 policy 要写这个路径）

**修改**：

- 分析文档 3.1 节列出的 15 个编辑目标，外加 `BasicMarkdownToHtml`；3.2 节里的 5 个 P1 项；`PublishUrlSnapshot` 视需要修改。
- **新项目登记**（D15、D16）：
  - `bukit-core.slnx`、`bukit-test.slnx`
  - `Bukit.Content.csproj`、`Bukit.Cli.csproj`（加 ProjectReference）
  - `tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs`，以及 public API 基线
  - `tests/Bukit.Architecture.Tests/PublicApiBaselineFacts.cs`：`GovernedAssemblyCount` 从 18 改成 19。有 10 个测试用它和基线里的程序集数量比对；`GovernedTypeCount` 不变，因为新程序集公开面为空，枚举成员也不算类型
  - `tests/Bukit.Architecture.Tests/CoreProjectReferenceBoundaryTests.cs`：Core 项目数从 18 改成 19
  - `tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`：更新 `Bukit.Content` 的引用表
  - `scripts/checks/coverage/list-core-projects.sh`：新增一行 `emit tests/Bukit.CloudBase.Tests/Bukit.CloudBase.Tests.csproj`。CI 只通过这份清单运行 Core 测试
  - `tests/Bukit.Architecture.Tests/CoverageGateTests.cs`、`scripts/checks/coverage/project-list-self-test.sh`：测试项目数从 13 改成 14
- **内部可见性**：`src/Bukit-Core/Bukit.Config/InternalsVisibleTo.cs` 加上 `Bukit.Engine.Audit` 和 `bukit`（D11；隐私检查和 doctor 要读 internal 的 `CloudBase` 配置）。**不扩大** `Bukit.Content` 的友元集合：它由架构测试固定（`ContentBoundaryExpectations`、`G04D3AContentBodyGraphTests`、`Ad03C4ContentNotionBridgeInternalizationTests`）。部署校验要用的 URL 位置扫描放在 `Bukit.Engine` 的一个 internal 类里，它已经是 `Bukit.Content` 的友元，并且对 `bukit` 可见（计划书 T07）。
- **契约测试**：`ConfigContractDriftTests`、`ConfigSchemaNestedContractTests`、`BuildDiagnosticCodeTests`、`DiagnosticCodeFormatterTests`。
- **工作流**：`scripts/checks/codex-workflow-policy.v1.json`（`pathRules` 和 `resourceRules` 都要改，见计划书 T00）。
- **文档**：
  - 用户文档：`guide/user/` 下的 04、07、12、14、16、25，以及索引 `guide/user/README.md`；
  - 开发文档：`guide/dev/config-site-yaml.md`、`guide/dev/cli.md`；
  - 技能文档：`guide/skills/bukit-content-model/SKILL.md`、`guide/skills/bukit-troubleshooting/SKILL.md`；
  - 三语 CHANGELOG。

## 9. 测试策略

- **Transport**（`Bukit.CloudBase.Tests`，用伪造的 `HttpMessageHandler`）：
  - 鉴权头生成、网关域名选择；
  - PostgREST 分页：`limit`/`offset`、`Prefer: count=exact`、`Content-Range` 解析（含畸形值）、已知总数时达到总数或空页才停止、无总数时短页停止；
  - 查询请求里 `eq.` filter 的 URL 编码和以 `idField` 结尾的 order（AC2）；
  - 存储批量签名：每批最多 500 个、单项 `error`；签名链接取值（有 `fullSignedURL` 用它，否则网关源加相对的 `signedURL`）；协议、主机和路径前缀校验；
  - JWT `role` 解码：`anon`、`service_role`、缺失、畸形；
  - 429/5xx 重试与退避、限速；
  - 401/403/404 映射到对应的 `CloudBaseErrorKind`（映射到 `DiagnosticCode` 的测试在 `Bukit.Content.Tests` 和 `Bukit.Cli.Tests`）；
  - 主机名断言：`envId` 含 `/`、`@`、`#`、`?`、`.`、大写或 IDN 时拒绝发送（D18）；对象名为空、以 `/` 开头、含 `//` 或 `..` 时拒绝签名；
  - 响应超大、JSON 畸形；
  - 异常文本里不含凭据、查询串和签名链接（AC7）；
  - 行覆盖率不低于 70%（AC9）。
- **Provider**（`Bukit.Content.Tests`）：
  - 字段投影与硬性剔除（AC3）；ISO-8601 和 epoch 两种时间格式；
  - `maxItems`；
  - 文件对象批量签名（AC5）：对象名和 `cloud://` 兼容形式，`cloud://` 的 envId 或桶名与配置不一致时不换链；以及 D17 的两种降级；
  - D8 第 ③ 条的占位图检查（在 `Bukit.Engine.Tests`，T04）；
  - 不可信链接渲染（AC4）。每种输入的预期输出如下：

    | 输入 | 预期 |
    |---|---|
    | `javascript:` / `vbscript:` / `data:` 链接 | 丢弃 |
    | `<javascript:alert(1)>` 自动链接 | 丢弃 |
    | `JAVASCRIPT:alert(1)` | 丢弃 |
    | `jav&#x61;script:` / `&#x6A;avascript:` | 丢弃 |
    | 引用定义 `[r]: javascript:alert(1)` | 丢弃 |
    | `java&#9;script:`（Markdig 输出为 `java%09script:`，已无效） | 不报错，断言输出里没有可执行的 `javascript:` |
    | 带标题的图片 `![a](https://x/a.png "t")` | 保留，标题照常输出 |
    | 裸 `www.` 自动链接（如果管线开启了 `UseAutoLinks`） | 按 https 处理，保留 |
    | `//host/x` | 丢弃 |
    | `#section` | 保留 |
    | `other-post/` | 保留 |
    | `../img.png` | 保留 |
    | `/abs` | 保留 |
    | `https:` / `mailto:` / `tel:` | 保留 |

    另外要断言：选项关闭时，输出和现在完全一致。
- **配置**（`Bukit.Config.Tests`）：
  - 读取、严格校验、语义校验、schema；
  - D8 第 ①②④ 条和第 ③ 条 `onFailure` 部分的每个失败场景；
  - `envId`、`table`、`idField`、`storageBucket` 白名单的失败场景（D4、D18）；`fileFields` 非空而缺少 `storageBucket`；`pageSize`、`signExpiresSeconds` 越界；`kind` 等旧字段按未知字段报错；
  - 两个契约测试：键快照要覆盖 `cloudbase` 及其下的 `filter`、`sort`、`fieldPolicy`，还有 `ValidateCloudBase` 的路径映射。
- **诊断码**（`Bukit.Shared.Tests`）：
  - `BuildDiagnosticCodeTests` 的区间表加 `CloudBase`，类别测试同步更新；
  - `DiagnosticCodeFormatterTests` 为每个新码补一条 InlineData。
- **端到端与隐私**（`Bukit.Engine.Tests`、`Bukit.Cli.Tests`）：
  - 用伪造的 HTTP 构建一个完整站点；
  - 隐私检查能检测出 CloudBase 标识（AC6），包括部署阶段不依赖配置的检测（URL 位置上的 `cloud://`、网关主机加签名路径加 `token`、存储 CDN 主机加 `token`），以及没有 CloudBase 源的站点链接到未签名 CDN 对象、或在代码块里提到 `cloud://` 时部署照常通过；
  - 没有 cloudbase 源的站点，产物逐字节不变。
- **签名链接**：
  - `SignedUrlIdentity` 能识别 CloudBase 签名链接（网关主机 + 签名路径时去掉 `token`；存储 CDN 主机时去掉 `token`），别的主机上的同名参数不受影响；
  - `MiniProgramJson.IsSignedUrl` 能拦截 CloudBase 签名链接（网关形式和存储 CDN 形式）和 `cloud://` 兼容形式；
  - D8 第 ⑤ 条的校验。
- **架构**：
  - 项目数、引用表、测试项目清单、受治理程序集数（`PublicApiBaselineFacts`）四处的计数测试；
  - 新增一条约束：`Bukit.CloudBase` 不引用任何项目。
- **工具门禁**（AC9）：每个 C# 任务都要跑 `dotnet-format.sh`、`code-analysis-ratchet.sh check` 和 `public-api-drift.sh check Debug`（这个脚本会先构建 `bukit-core.slnx`；直接 `dotnet run` drift 工具会因为缺少 Debug 程序集而失败）。
- **真实环境冒烟**：需要授权，在消费方的测试环境里手动执行。

## 10. 验证闭包

2026-11-23 对 P1 的候选文件集做过预演，结论如下。实施时以实际输出为准。

- **specialtyTests** 会要求跑这些测试项目的全量测试：`Bukit.Architecture.Tests`、`Bukit.Cli.Tests`、`Bukit.Config.Tests`、`Bukit.Content.Tests`、`Bukit.Engine.Tests`、`Bukit.Shared.Tests`。此外还有：public-api-drift 的 self-test 和 check、`docs/active-links.sh`、`docs/size-policy.sh`、`core-report-inventory.py --self-test`，以及 `Bukit.Cli` 的 Debug 构建。改到 public-api-governance 和覆盖率清单时，还会触发相应的规则。
- **unmappedFiles**（没有任何规则覆盖的文件）包括：
  - `src/Bukit-Core/Bukit.CloudBase/**`、`tests/Bukit.CloudBase.Tests/**`
  - `openspec/changes/add-cloudbase-content-source/**`、`openspec/specs/cloudbase-content-source/spec.md`
  - `bukit-test.slnx`
  - `guide/user/07-multi-source-data.md`，以及新的用户文档页
  - 本方案和计划书

  T00 必须让**全部 P1 候选文件**在 closure 里都没有 unmapped 文件，之后才能开始实现。具体规则见计划书 T00。
- **closure 不会自动给出的检查**：`dotnet-format.sh`、`code-analysis-ratchet.sh`、`config-docs-contract.sh`。T00 新增的规则要把它们写进 specialtyTests。
- **执行分类只看 `resourceRules`**：pathRule 里的 `resource` 字段只做必填检查，`classify` 不读它。现有 `resourceRules` 的 `dotnet-serial` 只认 `dotnet test`、`dotnet build src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj` 和 drift 工具，所以 `dotnet-format.sh`、`code-analysis-ratchet.sh` 这类命令会被分到 `static-parallel`。T00 要在 `resourceRules` 里补上它们，否则按分类结果并行跑，会在 `obj/` 上冲突。
- **glob 语义**：匹配用的是 `fnmatch.fnmatchcase`，`*` 也能跨 `/`。规则里的 glob 一律写完整前缀，不能写成裸的 `**/*.cs`（会匹配仓库里所有 C# 文件）或裸文件名（只匹配根目录）。
- **验证边界**：按 `AGENTS.md`，不跑 full/release gate、`scripts/test-all.sh`、`scripts/smoke-all.sh`，也不跑整个解决方案的测试。

## 11. 兼容性与发布

- **public API**：两类增量变化，一是 `DiagnosticCode` 新增成员，二是 `Bukit.CloudBase` 的受治理程序集条目（公开面为空）。配置类型和 transport 类型全部 internal。
- **配置 schema**：`content.sources[].type` 的枚举增加 `cloudbase`，属于增量变化。
- **行为**：没有 cloudbase 源的站点，产物逐字节不变，由端到端测试断言。
- **CI**：新的测试项目通过 `list-core-projects.sh` 进入 CI 的 Core 测试和覆盖率门禁。
- **发布**：常规公开发布目前暂停，本变更只进内部构建，CHANGELOG 记在 Unreleased 下。BukitJalil 的同步由 Part B 交接。

## 12. 风险与回滚

| 风险 | 缓解 |
|---|---|
| 真实环境里 Publishable Key 收窄不到只读（G0-4 只按文档通过） | T10 冒烟验证；不通过时改走方案 C，或者书面接受 `service_role` 的风险，同时启用 doctor 警告、隔离密钥、定期轮换 |
| RLS 静默过滤，`200 []` 被当成"没有内容" | doctor 提示；文档给出 GRANT 和 RLS 的 SQL 示例 |
| PostgREST 单次行数上限没有文档 | `pageSize` 上限 1000，已知 `Content-Range` 总数时按总数或空页判断结束，短页不算结束，因此不依赖服务端上限 |
| 文档里签名兑换路径模板漏了 `/{objectName}`；`fullSignedURL` 可能不存在 | 不自己拼对象路径：有 `fullSignedURL` 用它，否则只把网关源拼在服务端返回的相对 `signedURL` 前面（实测 `signedURL` 带完整的 `{bucket}/{object}` 路径）；两种都校验协议、主机和路径前缀 |
| 传统模式环境不在范围内 | P1 只支持 PG 模式；传统模式（数据模型）可以在 P2 增加 |
| 签名链接在构建过程中过期（正文图片延迟本地化） | `signExpiresSeconds` 默认 3600；失败时落到占位图，并报诊断 |
| 签名链接每次重签，正文指纹每次都变，增量构建总是不命中 | P1 不做增量指纹，这种情况可以接受；P3 用 `updatedAt` 做指纹，替换链接时用桶名加对象名作为稳定标识 |
| 缓存证据跨任务无法复用 | 缓存指纹包含 HEAD，每个任务提交后 HEAD 都会变，T10 实际要重跑全部测试；工期估算已按重跑计算 |
| CloudBase API 变化 | 只调用文档列出的接口；transport 集中放在一个项目里；签名链接的主机、路径前缀和参数名集中定义 |
| 范围蔓延 | 严格按 P1 的边界执行 |
| Notion 硬编码技术债扩大 | 在 openspec design 里登记，另外立项处理 |
| 新项目的覆盖率达不到 70% | 测试先行；T03 的验收要求达标 |
| 工作量被低估 | 每个任务都记录 metrics；偏差超过 50% 就停下来重新评估 |

**回滚**：每个任务单独提交，可以逐个 revert。回滚 T02 或 T03 时，要同步还原 public API 基线和各处的项目计数（包括 `PublicApiBaselineFacts`）。

## 13. P2/P3 提纲（另行立项）

- **P2**：
  - readonly/readwrite 缓存，估算约 400 行；
  - `BuildTransaction` 登记资源；
  - 离线快照，工作量待设计（Notion 的同类机制约 1.5k 行）；
  - 通用化 `NotionSnapshotScope.IsOffline`；
  - 传统模式环境（数据模型、`get-objects-download-info` 换链）。
- **P3**：
  - HTML 正文：新写白名单清洗器，估算 300～500 行，外加测试；
  - 非图片文件字段；
  - 关联字段；
  - miniprogram 的 body 规则；
  - 基于 `updatedAt` 的增量失效；
  - pgdoc 集合等其他数据形态。

## 附录 A：审计处置摘要（v1 → v2）

| 审计项 | 处置 |
|---|---|
| C1 只用 `git commit -- 路径` 会漏掉新文件 | 接受。计划书第 0 节改为先 `git add`，再核对暂存区，最后带路径提交 |
| I1 metrics 用了队列的状态文件 | 接受。计划书改用单独的 metrics 文件 |
| I2 缺少 classify、六行回报格式、报告路径 | 接受。计划书第 0 节补齐 |
| I3 缺少 C# 工具门禁 | 接受。写进 AC9，每个 C# 任务都执行 |
| I4 新项目要登记的地方没列全 | 接受。新增 D15，第 8 节列出全部登记点，并写进 T03 |
| I5 InternalsVisibleTo 不完整 | 接受。新增 D16 |
| I6 自动链接漏过滤；`#片段` 和相对链接被误删 | 接受。重写 D9，测试给出每种输入的预期 |
| I7 D8 没堵住临时链接 | 接受。D8 扩展为五条，隐私检测加入临时链接 |
| I8 closure 结论不完整 | 接受。重写第 10 节；T00 改为检查全部候选文件 |
| I9 policy 规则在归档后失效，也漏了调用方 | 接受。计划书 T00 写出完整规则 |
| I10 缺少受影响现有规格的变更 | 接受。T00 编写 4 份规格变更 |
| I11 BKT 码登记的位置错了 | 接受。改为登记到 `guide/dev/cli.md`，并新增 Part B |
| I12 门禁规则不一致 | 接受。改为 G0-1～G0-5 卡 T00、G0-6 卡 T06，与临时链接域名有关的校验移到 T06 |
| M1～M15 | 全部接受，见两份文档的相应位置。M5 中"`DiagnosticCodeFormatter` 有分类"的说法已更正为它只返回说明文字 |

## 附录 B：第二轮严格审计处置摘要（v2 → v3）

| 审计项 | 处置 |
|---|---|
| I1 T00 规则里的 glob 写法会让 self-test 失败 | 接受。第 10 节说明 glob 语义；计划书 T00 改为可直接复制的 JSON |
| I2 pathRule 的 `resource` 不影响 classify | 接受。第 10 节说明；计划书 T00 补 `resourceRules` 和 self-test 断言 |
| I3 一个缓存记录文件只能存一条记录；文件不存在时退出码是 2 | 接受。计划书改为每条命令一个记录文件，退出码 2 当作未命中；风险表写明 T10 要重跑 |
| I4 `review-scope` 需要的证据 JSON 和问题 JSON 没有步骤生成 | 接受。计划书第 4 节每个任务收尾时生成 |
| I5 T10 的归档提交没有跑 closure，也没有被审查 | 接受。计划书 T10 改为先归档、跑 closure，再做统一审查 |
| I6 漏了 `PublicApiBaselineFacts.GovernedAssemblyCount` | 接受。第 8 节和计划书 T03 补上 |
| I7 D8 第 ③ 条在配置校验阶段查不了 | 接受。改到构建阶段检查（T04），`config check`/`doctor` 给警告；AC1 同步修改 |
| I8 `Bukit.Config` 的内部可见性不够；部署校验拿不到 envId | 接受。D11 补内部可见性，并决定部署阶段的检测方式 |
| I9 transport 映射诊断码，和"不引用任何项目"矛盾 | 接受。D2 改为 transport 只产生 `CloudBaseErrorKind` |
| I10 D8 没覆盖正文非图片链接和非 `fileFields` 字段 | 接受。新增 D17 和诊断码 `0x0C0B` |
| I11 `envId` 可以把凭据发到别的主机 | 接受。新增 D18 |
| M1～M10 | 全部接受：用户文档路径固定为 `27-cloudbase-content.md` 并补 README 索引；规则数量改正；`bukit-test.slnx` 规则不再构建整个测试解决方案；drift 改用 `public-api-drift.sh`；T10 走完队列状态；改正 T01/T03 对 T02 的依赖；`list-core-projects.sh` 写成 `emit` 行；`guide/dev/cli.md` 的重复章节两处都改；D9 的措辞和测试表补齐；`_id` 的处理写进 D7 |

## 附录 C：v3 → v4 变更（PG 模式）

依据：用户确认（2026-11-23）环境是 PG 模式、数据在 `public` schema 的表或视图里、凭据用 Publishable Key、文件字段是固定桶里的对象名；以及 Phase 0 的 PG 模式公开文档调研。

| 位置 | 变更 |
|---|---|
| 第 0、1 节 | 范围改为 PG 模式环境的一张表或视图；AC2、AC3、AC5～AC8 改用 PostgREST、列名、签名链接和 `role` 的说法 |
| 第 2 节 | G0 表新增"结论"列，记录 G0-1～G0-6 的结果；S1～S3 改为 PG 模式的调研内容，并标明已按文档完成 |
| 第 3 节 | 数据流改为 PostgREST 分页和存储批量签名（每批最多 500 个） |
| D3 | 由"待 G0 定"改为已定：自己写 transport |
| D4 | 删除 `kind`/`model`/`collectionName`，改为必填的 `table`，加白名单校验 |
| D5 | 凭据从 API Key（`CLOUDBASE_API_KEY`）改为 Publishable Key（`CLOUDBASE_PUBLISHABLE_KEY`）；解码 JWT `role` 作诊断 |
| D6 | 查询改为 PostgREST：`select`/`order`/`limit`/`offset`、`eq.` filter、`Prefer: count=exact`；`idField` 作唯一排序 tiebreak |
| D7 | 硬性剔除列改为 `user_id`、`owner`、`created_by`、`updated_by`、`_openid`，可追加不可删除；`_id` 改为 `idField` |
| D8 ⑤ | 不排除的对象从"临时链接域名"改为网关主机 |
| D10 | 签名链接规则改为：网关主机 + `/v1/storages/object/sign/` 路径 + `token`；不读过期时间 |
| D11 | 检测对象改为 envId、`cloud://` 兼容形式、网关主机、签名路径加 `token` |
| D17 | 换链对象改为 `fileFields` 的对象名（或 `cloud://` 兼容形式）和正文图片位置的 `cloud://` 兼容引用；换链失败时由 provider 把值替换成 `content.media.defaultImageUrl`（第 3 节第 6 步同步改正，不再说 D8 能管到这一段） |
| D18 | `envId` 正则从 `{0,63}` 更正为 `{0,62}`；新增 `storageBucket` 和对象名校验；签名链接同样校验协议、主机和路径前缀 |
| 第 5 节 | 新增 `table`、`idField`、`storageBucket`、`signExpiresSeconds`；`pageSize` 上限 1000；`region` 枚举 `cn`/`intl`；补充消费方的 GRANT 和 RLS 设置 |
| 第 6 节 | 诊断码名称和数量不变；更新各码的场景；RLS 空结果只出 doctor 提示 |
| 第 7、12 节 | 新增 `service_role` 误用、签名链接主机、RLS 静默过滤、行数上限没有文档、兑换路径文档缺陷、传统模式不在范围内等条目 |
| 第 13 节 | 传统模式环境移到 P2 |
| v4.1 部署阶段 `cloud://` 范围（用户决定） | 部署阶段只查 URL 位置上的 `cloud://`（HTML 类文件的 URL 属性和 `style` 里的 `url(...)`、CSS 的 `url(...)`、JSON 里整个字符串值），正文和代码里的不算；构建阶段仍全文检测。实现复用 `HtmlMediaReferenceScanner` 的标签和属性分词（新入口也返回不带引号的属性值），由 `Bukit.Engine` 里的 internal 包装类对部署校验开放，不扩大 `Bukit.Content` 的友元集合，见计划书 T07。签名链接（带 `token`）在部署阶段仍做全文检测 |
| v4.1 审查修正 | 部署阶段（不读配置、对所有站点生效）的 CloudBase 检测一律要求带 `token`：网关主机加签名路径加 `token`、存储 CDN 主机加 `token`；只出现主机不算泄露，没有 CloudBase 源的站点行为不变。构建阶段（只在有 cloudbase 源时运行）仍只看主机。AC5 补存储 CDN 主机；D8 ⑤ 写明 `fileFields` 为空时由 D11 兜底；境外 CDN 主机形状标为未验证 |
| 实测补充二（2026-11-23，v4.1） | 用 Publishable Key 实测：G0-4 通过（网关 + Publishable Key + RLS，草稿行被隐藏，计数、分页、过滤、批量签名都正常，缺失对象返回单项 `error`）。新发现：① 签名响应还有 `fullSignedURLForCDN`（`https://{envId}.storage.tcloudbase.com/{bucket}/{object}?token=…`，能兑换），D8 ⑤、D10、D11 加入存储 CDN 主机，provider 仍只用网关形式；② 错误体实测是 `{code, message, requestId}`，`details`/`hint` 可选，码可能是 `DATABASE_PGRST*`（表不存在是 404 `DATABASE_PGRST205`，映射到 `CloudBaseNotFound`），诊断码仍是 11 个；③ Publishable Key 的 JWT 带 `role: anon`、`is_anonymous: true` 和约 73 年后的 `exp`，"不过期"改为"实际上不过期"，行为不变 |
| 实测补充（2026-11-23） | 在一个 PG 模式试用环境里探测：`signedURL` 是相对路径，CLI 输出没有 `fullSignedURL`，D17 改为"有 `fullSignedURL` 用它，否则网关源加 `signedURL`"，并校验路径前缀 `/v1/storages/object/sign/{storageBucket}/`；兑换不需要 Authorization，境内和境外主机都返回 200；篡改或缺少 `token` 返回 401（`STORAGE_INVALID_JWT`）；`token` 是带 `exp`、`iat`、`url` 的 JWT，P1 仍不读过期时间。G0-6 实测通过；G0-4 在数据库角色层实测通过（`anon` + RLS 只返回已发布行），网关加 Publishable Key 待用户提供密钥 |

传统模式的事实（数据模型列表接口、`get-objects-download-info`、`*.tcb.qcloud.la`、`_openid`/`createBy`/`updateBy` 系统字段）只作为 P2 的背景保留在 openspec proposal 的调研结果里，本方案不再采用。
