# Bukit Core 内置 `type: cloudbase` 内容源：实施方案

- 日期：2026-11-23
- 状态：**草案，未批准**。门禁规则见第 2 节：G0-1～G0-5 不通过就不能开始 T00；G0-6 不通过就不能开始 T06。
- 修订：v2（2026-11-23）。根据一轮独立审计（Critical 1 / Important 12 / Minor 15）修订，处置记录见附录 A。
- 依据：[可行性分析](../analysis/bukit-core-cloudbase-builtin-provider-feasibility-2026-11-22.zh-CN.md)，下文简称"分析文档"。分析文档经过三轮独立审计修订，提交 `52699535`。
- 配套：[实施任务计划书](2026-11-23-cloudbase-content-source-task-plan.zh-CN.md)。本文件讲"做成什么样、为什么这样做"；计划书讲"按什么顺序做、怎么做、怎么验收"。
- 代码基线：提交 `5fdc9dd2` 时的仓库内容。正文用文件和符号定位，不依赖行号。
- 范围：只覆盖分析文档中的 P1（最小可用版）。P2（缓存、离线快照）和 P3（HTML 正文、关联字段等）只在第 13 节列出提纲。
- 标注约定：
  - "决策"指本方案的设计选择，需要在 G0 时一并确认；
  - "估算"指推算出来的数字；
  - "待 G0 定"指要等门禁调研的结果才能确定。

## 0. 先读这一段

1. **这是一个有条件的方案。** 分析文档的结论是：方案 A 技术上可行，但鉴权没有已核实的最小权限路径，治理门槛也高，默认推荐的仍是方案 C（同步插件）。本方案只回答"如果 G0 决定走 A，应该怎么做"。
2. **G0 不通过就停止。** 如果鉴权调研证明只能用管理员级凭据，而负责人又不书面接受这个风险，就不实施 A，改走方案 C。
3. **P1 的边界**：
   - 只支持一种数据形态；
   - 只读；
   - 只支持 Markdown 正文；
   - 文件字段只支持图片；
   - 不做缓存，不做离线。
4. **public API 的变化**：
   - 所有配置类型和 transport 类型都是 internal；
   - public-api-drift 会出现两类预期变化：一是 `DiagnosticCode`（public enum）新增成员；二是新程序集 `Bukit.CloudBase` 登记为受治理程序集（D15），它的公开面为空。
5. **测试不连真实网络。** 自动化测试全部使用伪造的 HTTP 处理器。真实环境的冒烟测试要获得授权才做，而且手动执行。

## 1. 目标、非目标与验收

### 1.1 目标

在 site.yaml 里写 `content.sources[].type: cloudbase` 之后，`bukit config check`、`bukit doctor`、`bukit build` 能完成下面这些事：

- 从 CloudBase 读取一个集合或数据模型的记录；
- 把记录映射成 Bukit 内容文档；
- 用 Markdown 正文渲染页面；
- 把存放在 CloudBase 的图片本地化后发布。

### 1.2 非目标（P1 不做）

- 同时支持文档型数据库和数据模型两种形态；
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
| AC1 | 配置了 `type: cloudbase` 的站点能通过 `config check`。以下情况都要以 CloudBase 诊断码失败：缺少必填项、取值越界、凭据写进 YAML、违反 D8 的任一条件 |
| AC2 | 用伪造的 HTTP 响应跑 `build`，端到端生成页面。要求：分页读完全部记录；按 `maxItems` 截断；查询请求正确带上 filter 和 sort |
| AC3 | 字段投影默认用白名单。`_openid`、`_createBy`、`_updateBy` 这类系统字段和个人字段，在任何 `fieldPolicy` 模式下都不会进入公开产物 |
| AC4 | Markdown 正文里，链接、自动链接（`<scheme:...>`）和图片中的 `javascript:`、`vbscript:`、`data:`、`//host` 被丢弃；`#片段` 和不带协议的相对链接保留。这些都要有测试覆盖 |
| AC5 | `cloud://` 文件 ID 被批量换成临时链接，并在同一次构建内下载到本地。发布产物里不能出现 `cloud://`、envId，以及临时链接的域名和签名 |
| AC6 | 公开产物隐私检查和部署隐私校验能检测出 CloudBase 标识（envId、`cloud://`、临时链接域名或签名参数），并让构建或部署失败 |
| AC7 | 凭据、签名、临时链接不出现在任何日志和异常文本中，并有测试覆盖 |
| AC8 | `doctor` 能报告连通性、权限、集合或模型是否存在、字段映射是否有效。使用管理员级凭据时给出警告 |
| AC9 | closure 给出的 specialtyTests 全部通过。另外：`dotnet-format.sh` 通过；`code-analysis-ratchet.sh check` 通过，并且 CA1502/CA1506 的计数没有上升；public-api-drift 只报告第 0 节第 4 条列出的两类预期变化，且基线已更新；`Bukit.CloudBase` 行覆盖率不低于 70% |
| AC10 | 以下内容都已同步：用户文档、开发文档、技能文档、排错文档、`guide/dev/cli.md` 的 BukitJalil 消费方契约表、三语 CHANGELOG、openspec 规格（新规格，以及受影响的 4 个现有规格的变更） |

## 2. 前置门禁 G0

G0 的结论由负责人书面记录在 openspec 提案里。**G0-1～G0-5 是 T00 的前置条件；G0-6 是 T06 的前置条件。**

| 门禁 | 内容 | 通过标准 | 不通过时 |
|---|---|---|---|
| G0-1 内部消费方 | 具名站点、负责人、业务理由、可验证的验收标准。这是产品定位文档的要求 | 写进 openspec proposal | 不立项 |
| G0-2 Core 任务授权 | 作为一个独立的、用户可见的 Core 任务，取得明确确认。`AGENTS.md` 的 Website/Core 隔离规则字面上针对的是缺陷修复；把它用在新增能力上是类推，这一点沿用分析文档第 7 节的说法 | 用户明确授权 | 不立项 |
| G0-3 数据形态 | 消费方的数据存在文档型数据库还是数据模型；如果是数据模型，底层是哪种数据库 | 调研 S1 给出结论 | 无法确定就不立项 |
| G0-4 鉴权 | 在 G0-3 确定的数据形态下，CI 可用的鉴权方式是什么、权限范围多大 | 能收窄到只读；或者负责人书面接受管理员级凭据的风险，并确定补偿措施 | 改走方案 C |
| G0-5 Transport | 官方 C# SDK `Tencent.CloudBase` 能否兼容 Native AOT 和裁剪，依赖能否接受 | 调研 S2 给出结论：复用 SDK 或自己写 | 默认自己写 |
| G0-6 临时链接 | 临时链接的域名和签名参数名；`maxAge` 的单位和上限 | 调研 S3 给出结论 | T06 不能开始。与临时链接域名有关的 D8 校验也放在 T06 里实现 |

### 2.1 Phase 0 调研（只读，不改仓库代码）

调研结论记录在 openspec proposal 的"调研结果"一节，不写实现代码。

- **S1 数据形态与查询接口**：
  - 在消费方的控制台确认数据存放位置；
  - 确认选定 API 面上的查询路径、分页参数、过滤和排序语法、分页上限，以及系统字段清单；
  - 准备一份脱敏后的响应样本，作为测试夹具。
- **S2 鉴权与 SDK**：
  - 针对 G0-3 确定的数据形态，逐一验证能否只读访问：API Key；用户登录 token 配合数据权限；签名 V3 配合 CAM 子账号（仅文档型数据库）；
  - 新建一个不入库的临时工程，对 `Tencent.CloudBase` 试编译 `PublishAot`，统计裁剪和 AOT 警告的数量；
  - clientCredential token 的官方文档已失效，要用它就得实测，或者向腾讯云确认。
- **S3 临时链接**：
  - 对私有文件调用换链接口，记录返回链接的域名、签名参数名、过期行为；
  - 确认 `maxAge` 能否设得足够长，覆盖一次完整构建。
- **S4 内容样本**：
  - 确认正文是否包含 HTML、`#片段` 链接、相对链接、内嵌的 `cloud://` 图片，以及 `<scheme:...>` 形式的自动链接；
  - 确认文件字段是否全是图片。

## 3. 总体架构

```text
site.yaml ──> Bukit.Config（internal CloudBaseConfig，读取与校验）
                 │
                 ▼
ContentProviderFactory ──> Bukit.Content/CloudBase/CloudBaseContentSource : IContentProvider
                                  │  分页查询、字段投影、cloud:// 换链
                                  ▼
                           Bukit.CloudBase（新项目：只依赖 BCL；如果 G0-5 选了 SDK，则改用 SDK）
                                  │  鉴权、重试、限速、错误映射
                                  ▼
                           CloudBase HTTP API
                                  │
CloudBaseBodyStore : IContentBodyStore ──> BasicMarkdownToHtml（不可信链接模式）
                                  │
                                  ▼
                 现有管线：ContentLoadStage → ImageLocalizeStage → 路由 → 渲染（正文图片延迟本地化）→ 隐私检查 → 输出
```

**数据流**：

1. `LoadRawAsync` 按页查询全部记录，最多取 `maxItems` 条。
2. 对每条记录做字段投影：先剔除系统字段，再应用白名单。
3. 收集字段和正文里的 `cloud://` 文件 ID，按 50 个一批换成临时链接，替换回原文。
4. 返回 `RawContentDocument`。
5. `ImageLocalizeStage` 下载 `fieldKeys` 字段里的图片。正文里的图片在渲染阶段延迟本地化，由 `LocalizedContentBodyStore` 负责。
6. 因为有 D8 的约束，`fileFields` 必然是 `fieldKeys` 的子集，下载失败时只会落到占位图，不会把原链接保留下来。

**时效风险**：正文图片是在渲染阶段才下载的，构建耗时如果超过临时链接的有效期，下载就会失败。所以 `maxAge` 要按 S3 的结论设到足够长（例如覆盖构建超时上限），并写进第 12 节的风险表。

## 4. 设计决策

| 编号 | 决策 | 理由 |
|---|---|---|
| D1 | `ContentSourceConfig` 新增一个 **internal** 属性 `CloudBase`，类型是 internal 的 `CloudBaseConfig` | 有先例：`AppConfig.MiniProgram` 就是 internal，`ConfigContractDriftTests` 的注释写明它不增加 public API |
| D2 | 新建 `src/Bukit-Core/Bukit.CloudBase/` 项目，只依赖 BCL，负责 transport 和数据模型；provider 放在 `Bukit.Content/CloudBase/` | 与 `Bukit.Notion` / `Bukit.Content/Notion` 的分层方式对齐，transport 可以单独测试 |
| D3 | 默认自己写 transport。只有 S2 证明 SDK 的 AOT 和裁剪警告为零、依赖可以接受时，才改用 SDK（待 G0 定） | Core 是 Native AOT（`Directory.Build.props` 给所有 Core 项目设置了 `IsAotCompatible`）；`Bukit.Notion` 是只依赖 BCL 的先例 |
| D4 | `kind` 只接受 G0-3 选定的那一种，填另一种直接报配置错误 | 两种都支持，等于做两个 provider |
| D5 | 鉴权用一个凭据策略接口，但 P1 只实现 G0-4 选定的那一种方式。凭据只从环境变量或 `--secrets-stdin` 读取，YAML 里出现凭据字段就报错 | 不给没验证过的方式写代码 |
| D6 | 查询走 HTTP API 网关。`filter` 在 P1 只支持单字段等值，配置写成 `{ field, value }`，以后可以扩展出 `op` 字段；`sort` 只支持单字段 | 只覆盖实际需要的查询，同时给以后留出兼容的扩展方式 |
| D7 | 字段投影默认 `whitelist`。系统字段和个人字段（`_openid`、`_createBy`、`_updateBy` 等，最终清单待 S1）一律**硬性剔除**，`fieldPolicy.mode: all` 也不放行 | `_openid` 是用户个人标识，不能指望配置写对来保证不泄露 |
| D8 | `fileFields` 非空时，以下条件必须全部满足，否则配置校验失败：① `content.media.downloadToLocal` 不是 false；② `fileFields ⊆ content.media.fieldKeys`；③ `content.media.onFailure` 是 `placeholder`，并且占位图存在（`PlaceholderMissing` 为 false）；④ P1 只接受图片字段；⑤ `downloadHosts`/`skipHosts` 不排除临时链接域名。第 ⑤ 条依赖 G0-6，放在 T06 实现 | 只有同时满足这几条，才能保证临时链接在任何情况下都不会原样发布：`onFailure: keep` 会保留原链接；占位图缺失时也会保留原链接；不在 `fieldKeys` 里的字段根本不会被本地化 |
| D9 | Markdown 正文用"不可信链接"模式渲染。具体做法：`BasicMarkdownToHtml` 新增一个选项，开启后先 Parse，再遍历语法树里的 `LinkInline`（包括图片和引用式链接）和 `AutolinkInline`，最后用 `HtmlRenderer` 输出，原有的独立图片后处理保持不变。URL 策略是：有协议的，必须在 `RenderingSafeUrl` 的白名单里（链接允许 http、https、mailto、tel；媒体允许 http、https）；以 `//` 开头的丢弃；`#片段` 和不带协议的相对链接保留。这个策略写成一个 CloudBase 专用的小包装，内部复用 `RenderingSafeUrl` 的协议集合 | `RenderingSafeUrl` 对相对链接和 `#片段` 会返回 null，直接拿来用会误删正常链接；`.DisableHtml()` 不会禁用自动链接；`Bukit.Notion` 已经通过 `InternalsVisibleTo` 对 `Bukit.Content` 开放了内部类型 |
| D10 | `SignedUrlIdentity` 按 S3 的结论新增 CloudBase 临时链接规则 | 关系到小程序签名链接拦截和媒体缓存 |
| D11 | 隐私检查在现有 Notion 逻辑旁边**并列**加上 CloudBase 标识的检测，包括 envId、`cloud://`、临时链接域名和签名参数。P1 不做通用化 | 控制改动范围 |
| D12 | 诊断码新增 `CloudBase` 前缀，区间定为 `0x0C01`～`0x0CFF`，格式化后是 `BKT-0C01` 起 | `0x0C` 区间没有被占用。`BuildDiagnosticCodeTests` 用 `StartsWith` 匹配前缀，`CloudBase` 不会和 `Config`、`Content` 冲突 |
| D13 | `cacheMode` 只接受 `off` | 缓存属于 P2 |
| D14 | 内部诊断和日志只输出：记录 ID 的哈希、集合名、HTTP 状态码。不输出 URL 查询串、请求头和响应体原文 | Core 日志不会脱敏来自环境变量的密钥 |
| D15 | `Bukit.CloudBase` 登记为受治理程序集：加入 `tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs` 的 `GovernedAssemblies`，基线里新增这个程序集的条目，公开面为空 | 不登记的话，它的公开面就没人管；`Bukit.Notion` 也是受治理程序集 |
| D16 | `Bukit.CloudBase` 的 `InternalsVisibleTo` 清单：`Bukit.Content`、`bukit`（Bukit.Cli 的程序集名，doctor 要用）、`Bukit.CloudBase.Tests`、`Bukit.Content.Tests`、`Bukit.Engine.Tests`、`Bukit.Cli.Tests`。`Bukit.Cli` 直接引用 `Bukit.CloudBase` | transport 类型全是 internal，doctor 和各个测试项目都要能注入伪造的处理器；参照 `Bukit.Notion.csproj` 的清单 |

## 5. 配置契约（P1）

```yaml
content:
  sources:
    - name: articles
      type: cloudbase
      mode: content
      collection: posts            # 现有通用字段：Bukit 的内容集合名，不是 CloudBase 的集合
      cloudbase:
        envId: prod-xxxx
        region: ap-shanghai        # 决定网关域名（境内或境外）
        kind: model                # 只接受 G0-3 选定的值
        model: article             # kind=model 时必填
        # collectionName: posts    # kind=collection 时必填，指 CloudBase 的集合名
        pageSize: 100
        maxItems: 500
        filter: { field: status, value: published }
        sort: { field: publishAt, direction: desc }
        titleField: title
        slugField: slug
        publishAtField: publishAt
        updatedAtField: updatedAt
        bodyField: content
        bodyFormat: markdown       # P1 只接受 markdown
        fieldPolicy: { mode: whitelist, allowed: [title, slug, publishAt, cover, tags] }
        fileFields: [cover]        # 必须是 content.media.fieldKeys 的子集，并且只能是图片
        cacheMode: off             # P1 只接受 off
        maxRps: 5
        maxRetries: 3
```

| 字段 | 必填 | 默认值 | 校验 |
|---|---|---|---|
| `envId` | 是 | — | 非空；字符集待 S1 确认；会被隐私检查当作敏感标识 |
| `region` | 否 | 境内默认地域 | 取枚举值，决定用 `.api.tcloudbasegateway.com` 还是 `.api.intl.tcloudbasegateway.com` |
| `kind` | 是 | — | 只接受 G0-3 选定的值 |
| `model` / `collectionName` | 按 `kind` 二选一 | — | 非空 |
| `pageSize` | 否 | 100 | 1 到上限之间，上限待 S1 确认 |
| `maxItems` | 否 | 不限 | 大于 0 |
| `filter` | 否 | 无 | P1 只允许 `field` 和 `value` 两个键 |
| `sort` | 否 | 无 | `direction` 只能是 `asc` 或 `desc` |
| `*Field` 映射 | 否 | 同名字段 | 非空字符串 |
| `bodyField` | `mode: content` 时必填 | — | 非空 |
| `bodyFormat` | 否 | `markdown` | P1 只接受 `markdown` |
| `fieldPolicy` | 否 | `whitelist`，允许列表为空 | 取值沿用 Notion 的 `whitelist` 和 `all`；硬性剔除列表不受它影响 |
| `fileFields` | 否 | 空 | 非空时触发 D8 的全部校验 |
| `cacheMode` | 否 | `off` | P1 只接受 `off` |
| `maxRps` / `maxRetries` | 否 | 估算 5 / 3 | `maxRps` 大于 0；`maxRetries` 不小于 0 |

**凭据**：变量名等 G0-4 定下来再确定，候选有 `CLOUDBASE_API_KEY`、`CLOUDBASE_ACCESS_TOKEN`、`TENCENTCLOUD_SECRETID` / `TENCENTCLOUD_SECRETKEY`。读取统一通过 `SecretStore.Get`。不往 public 的 `EnvironmentHelper` 加任何成员，否则会造成 public-api-drift。

## 6. 诊断码（暂定）

| 名称 | 值 | 场景 |
|---|---|---|
| `CloudBaseConfigInvalid` | 0x0C01 | 配置缺失、越界、形态不支持、违反 D8 的第 ①～④ 条 |
| `CloudBaseCredentialMissing` | 0x0C02 | 找不到所需的凭据 |
| `CloudBaseAuthFailed` | 0x0C03 | 401/403，或者签名错误 |
| `CloudBaseNotFound` | 0x0C04 | 环境、集合或模型不存在 |
| `CloudBaseRateLimited` | 0x0C05 | 重试后仍被限流 |
| `CloudBaseRequestFailed` | 0x0C06 | 其他 HTTP 或网络错误 |
| `CloudBaseResponseInvalid` | 0x0C07 | 响应结构不符合预期，或超过大小上限 |
| `CloudBaseFileResolveFailed` | 0x0C08 | `cloud://` 换链失败 |
| `CloudBaseMediaLocalizationRequired` | 0x0C09 | 违反 D8 的第 ⑤ 条（与临时链接域名有关） |
| `CloudBaseAdminCredentialInUse` | 0x0C0A | doctor 警告：正在使用管理员级凭据 |

最终清单在 T02 定稿。`DiagnosticCodeFormatter.Describe` 要为每个码返回 "CloudBase: …" 形式的说明。

BukitJalil 按固定表给 BKT 码分类。它维护中的消费方契约在 `guide/dev/cli.md` 的 "BukitJalil consumer contract" 表里，所以要在这张表里登记 `BKT-0Cxx` 一行，契约的负责测试是 `DiagnosticCodeFormatterTests`。BukitJalil 那边要同步更新码表和 `MIN_BUKIT_VERSION`，这一步不在本仓库，由计划书的 Part B 单独交接，并另外取得授权。

## 7. 安全设计

| 威胁 | 对策 | 验收 |
|---|---|---|
| CI 凭据泄露 | G0-4 尽量做到只读。如果只能用管理员级凭据，需要书面接受风险，同时 doctor 给出警告，凭据只放在 CI 的密钥库里 | AC8 |
| 日志或异常泄露密钥 | 按 D14 处理；transport 的异常信息里不拼接 URL 和请求头 | AC7 |
| 个人数据进入公开产物 | D7 硬性剔除；默认白名单；D11 的隐私检查兜底 | AC3、AC6 |
| 正文链接注入（包括自动链接） | D9 | AC4 |
| 临时签名链接被原样发布 | D8 堵住所有会保留原链接的路径；D10 识别签名链接；D11 在输出阶段检测临时链接域名和签名 | AC5、AC6 |
| 临时链接在构建过程中过期 | `maxAge` 按 S3 设置到足以覆盖构建时长；下载失败时落到占位图，并报诊断 | AC5 |
| 远程响应异常（超大、畸形） | 限制分页上限和响应体大小，超出时报 `CloudBaseResponseInvalid` | AC2 |

## 8. 组件与文件清单

**新增**（估算 10～14 个文件）：

- `src/Bukit-Core/Bukit.CloudBase/Bukit.CloudBase.csproj`
- `Bukit.CloudBase/Transport/`：`CloudBaseClient`、`CloudBaseClientOptions`、`CloudBaseApiException`，以及一个凭据策略实现
- `Bukit.CloudBase/Models/`：请求和响应模型，加上源生成的 JSON 上下文
- `src/Bukit-Core/Bukit.Content/CloudBase/`：`CloudBaseContentSource`、`CloudBaseContentSourceOptions`、`CloudBaseFieldProjector`、`CloudBaseFileResolver`、`CloudBaseBodyStore`、`CloudBaseUrlPolicy`
- `src/Bukit-Core/Bukit.Cli/Commands/DoctorCloudBaseChecker.cs`
- `tests/Bukit.CloudBase.Tests/`
- `openspec/changes/add-cloudbase-content-source/`：proposal、design、tasks；`specs/cloudbase-content-source/spec.md`；另外为 `core-report-contracts`、`media-localization`、`miniprogram-data`、`incremental-invalidation` 四个现有规格各写一份变更
- 新增一份 CloudBase 用户文档（`guide/user/` 下，编号在 T09 确定）

**修改**：

- 分析文档 3.1 节列出的 15 个编辑目标，外加 `BasicMarkdownToHtml`；3.2 节里的 5 个 P1 项；`PublishUrlSnapshot` 视需要修改。
- **新项目登记**（D15、D16）：
  - `bukit-core.slnx`、`bukit-test.slnx`
  - `Bukit.Content.csproj`、`Bukit.Cli.csproj`（加 ProjectReference）
  - `tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs`，以及 public API 基线
  - `tests/Bukit.Architecture.Tests/CoreProjectReferenceBoundaryTests.cs`：Core 项目数从 18 改成 19
  - `tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`：更新 `Bukit.Content` 的引用表
  - `scripts/checks/coverage/list-core-projects.sh`：CI 只通过这份清单运行 Core 测试
  - `tests/Bukit.Architecture.Tests/CoverageGateTests.cs`、`scripts/checks/coverage/project-list-self-test.sh`：测试项目数从 13 改成 14
- **契约测试**：`ConfigContractDriftTests`、`ConfigSchemaNestedContractTests`、`BuildDiagnosticCodeTests`、`DiagnosticCodeFormatterTests`。
- **工作流**：`scripts/checks/codex-workflow-policy.v1.json`。
- **文档**：
  - 用户文档：`guide/user/` 下的 04、07、12、14、16、25；
  - 开发文档：`guide/dev/config-site-yaml.md`、`guide/dev/cli.md`；
  - 技能文档：`guide/skills/bukit-content-model/SKILL.md`、`guide/skills/bukit-troubleshooting/SKILL.md`；
  - 三语 CHANGELOG。

## 9. 测试策略

- **Transport**（`Bukit.CloudBase.Tests`，用伪造的 `HttpMessageHandler`）：
  - 鉴权头生成、网关域名选择；
  - 分页；
  - 查询请求里 filter 和 sort 的构造（AC2）；
  - 429/5xx 重试与退避、限速；
  - 401/403/404 映射到对应的诊断码；
  - 响应超大、JSON 畸形；
  - 异常文本里不含凭据和查询串（AC7）；
  - 行覆盖率不低于 70%（AC9）。
- **Provider**（`Bukit.Content.Tests`）：
  - 字段投影与硬性剔除（AC3）；
  - `maxItems`；
  - `cloud://` 批量换链（AC5）；
  - 不可信链接渲染（AC4）。每种输入的预期输出如下：

    | 输入 | 预期 |
    |---|---|
    | `javascript:` / `vbscript:` / `data:` 链接 | 丢弃 |
    | `<javascript:alert(1)>` 自动链接 | 丢弃 |
    | `//host/x` | 丢弃 |
    | `#section` | 保留 |
    | `other-post/` | 保留 |
    | `../img.png` | 保留 |
    | `/abs` | 保留 |
    | `https:` / `mailto:` / `tel:` | 保留 |

    另外要断言：选项关闭时，输出和现在完全一致。
- **配置**（`Bukit.Config.Tests`）：
  - 读取、严格校验、语义校验、schema；
  - D8 第 ①～④ 条的每个失败场景；
  - 两个契约测试：键快照要覆盖 `cloudbase` 及其下的 `filter`、`sort`、`fieldPolicy`，还有 `ValidateCloudBase` 的路径映射。
- **诊断码**（`Bukit.Shared.Tests`）：
  - `BuildDiagnosticCodeTests` 的区间表加 `CloudBase`，类别测试同步更新；
  - `DiagnosticCodeFormatterTests` 为每个新码补一条 InlineData。
- **端到端与隐私**（`Bukit.Engine.Tests`、`Bukit.Cli.Tests`）：
  - 用伪造的 HTTP 构建一个完整站点；
  - 隐私检查能检测出 CloudBase 标识（AC6）；
  - 没有 cloudbase 源的站点，产物逐字节不变。
- **签名链接**：
  - `SignedUrlIdentity` 能识别临时链接；
  - `MiniProgramJson.IsSignedUrl` 能拦截临时链接；
  - D8 第 ⑤ 条的校验。
- **架构**：
  - 项目数、引用表、测试项目清单三处的计数测试；
  - 新增一条约束：`Bukit.CloudBase` 不引用任何项目。
- **工具门禁**（AC9）：每个 C# 任务都要跑 `dotnet-format.sh`、`code-analysis-ratchet.sh check` 和 public-api-drift。
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
| G0-4 只能用管理员级凭据 | 改走方案 C；或者书面接受风险，同时启用 doctor 警告、隔离密钥、定期轮换 |
| 临时链接在构建过程中过期（正文图片延迟本地化） | `maxAge` 按 S3 设置；失败时落到占位图，并报诊断 |
| 临时链接每次重签，正文指纹每次都变，增量构建总是不命中 | P1 不做增量指纹，这种情况可以接受；P3 用 `updatedAt` 做指纹，替换链接时用文件 ID 作为稳定标识 |
| CloudBase API 变化 | 只调用文档列出的接口；transport 集中放在一个项目里 |
| 范围蔓延 | 严格按 P1 的边界执行 |
| Notion 硬编码技术债扩大 | 在 openspec design 里登记，另外立项处理 |
| 新项目的覆盖率达不到 70% | 测试先行；T03 的验收要求达标 |
| 工作量被低估 | 每个任务都记录 metrics；偏差超过 50% 就停下来重新评估 |

**回滚**：每个任务单独提交，可以逐个 revert。回滚 T02 或 T03 时，要同步还原 public API 基线和各处的项目计数。

## 13. P2/P3 提纲（另行立项）

- **P2**：
  - readonly/readwrite 缓存，估算约 400 行；
  - `BuildTransaction` 登记资源；
  - 离线快照，工作量待设计（Notion 的同类机制约 1.5k 行）；
  - 通用化 `NotionSnapshotScope.IsOffline`。
- **P3**：
  - HTML 正文：新写白名单清洗器，估算 300～500 行，外加测试；
  - 非图片文件字段；
  - 关联字段；
  - miniprogram 的 body 规则；
  - 基于 `updatedAt` 的增量失效；
  - 第二种数据形态。

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
