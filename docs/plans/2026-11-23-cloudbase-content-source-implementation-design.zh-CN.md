# Bukit Core 内置 `type: cloudbase` 内容源：实施方案

- 日期：2026-11-23
- 状态：**草案，未批准**。本方案以 G0 门禁通过为前提（见第 2 节）；G0 未通过前，不得开始任何实现任务。
- 依据：[可行性分析](../analysis/bukit-core-cloudbase-builtin-provider-feasibility-2026-11-22.zh-CN.md)（经三轮独立审计修订，提交 `52699535`）。下文简称"分析文档"。
- 配套：[实施任务计划书](2026-11-23-cloudbase-content-source-task-plan.zh-CN.md)。本文件回答"做成什么样、为什么这样做"，计划书回答"按什么顺序、怎么做、怎么验收"。
- 代码基线：提交 `52699535` 时的仓库内容。正文用文件和符号定位，不依赖行号。
- 范围：
  - 只覆盖分析文档中的 P1（最小可用版）。
  - P2（缓存、离线快照）和 P3（HTML 正文、关联字段等）只在第 13 节列出提纲，另行立项。
- 标注约定：
  - "决策"是本方案的设计选择，需在 G0 时一并确认；
  - "估算"是推算；
  - "待 G0 定"表示取决于门禁调研的结果。

## 0. 先读这一段

1. **这是一个有条件的方案。** 分析文档的结论是：方案 A 技术上可行，但鉴权没有已核实的最小权限路径，治理门槛也很高；默认推荐的仍是方案 C（同步插件）。本方案描述的是"如果 G0 决定走 A，应该怎么做"，不代表已经决定走 A。
2. **G0 不通过就停止。** 鉴权调研如果证明只能使用管理员级凭据，而负责人又不书面接受这个风险，就不实施 A，改走方案 C。
3. **P1 的边界**：只支持一种数据形态、只读、只支持 Markdown 正文、不做缓存、不做离线。任何超出这些边界的需求都进入 P2/P3。
4. **不扩大公开面**：
   - 配置类型全部用 internal；
   - 唯一不可避免的 public 变化是 `DiagnosticCode` 新增成员（public enum）。
5. **测试不连真实网络。** 自动化测试全部使用伪造的 HTTP 处理器。真实环境的冒烟测试只在获得授权后手动进行。

## 1. 目标、非目标与验收

### 1.1 目标

在 site.yaml 中可以这样声明：

```yaml
content:
  sources:
    - type: cloudbase
      cloudbase: { ... }
```

`bukit config check`、`bukit doctor`、`bukit build` 要能从 CloudBase 读取一个集合或数据模型的记录，把记录映射成 Bukit 内容文档，用 Markdown 正文渲染页面，并把存储在 CloudBase 里的图片本地化后发布。

### 1.2 非目标（P1 不做）

- 同时支持文档型数据库和数据模型两种形态；
- HTML 富文本正文；
- 缓存（`cacheMode` 只接受 `off`）、离线快照、增量指纹；
- 关联字段，以及跨源关系投影；
- 写回 CloudBase；
- miniprogram 的 body 规则（P3）；
- 把 Notion 专用的隐私、快照设施抽象成通用扩展点（记为技术债，另立任务）。

### 1.3 验收标准

| 编号 | 验收条件 |
|---|---|
| AC1 | 配置 `type: cloudbase` 的站点，`config check` 通过；缺少必填项、取值越界、凭据写进 YAML、`fileFields` 非空但媒体本地化被关闭，这些情况都以 CloudBase 诊断码失败 |
| AC2 | `build` 用伪造的 HTTP 响应端到端生成页面：分页读完全部记录，按 `maxItems` 截断，按 filter/sort 查询 |
| AC3 | 字段投影默认使用白名单；`_openid`、`_createBy`、`_updateBy` 这类系统和个人字段，在任何 `fieldPolicy` 模式下都不会进入公开产物 |
| AC4 | Markdown 正文里 `javascript:`、`vbscript:`、`data:` 等协议的链接和图片被丢弃，有测试覆盖 |
| AC5 | `cloud://` 文件 ID 被批量换成临时链接，并在同一次构建内下载到本地；发布产物里不含 `cloud://`、envId 和临时链接签名 |
| AC6 | 公开产物隐私检查和部署隐私校验能检测出 CloudBase 标识（envId、`cloud://`），并让构建或部署失败 |
| AC7 | 凭据、签名、临时链接不出现在任何日志和异常文本中，有测试覆盖 |
| AC8 | `doctor` 能报告连通性、权限、集合或模型是否存在、字段映射是否有效；使用管理员级凭据时给出警告 |
| AC9 | closure 给出的 specialtyTests 全部通过；public-api-drift 只报告预期的 `DiagnosticCode` 新增成员，且基线已更新 |
| AC10 | 用户文档、开发文档、技能文档、三语 CHANGELOG、openspec 规格都已同步 |

## 2. 前置门禁 G0

G0 是开工前的决策点，需要负责人在 openspec 提案里书面记录结论。

| 门禁 | 内容 | 通过标准 | 不通过时 |
|---|---|---|---|
| G0-1 内部消费方 | 具名站点、负责人、业务理由、可验证的验收标准（产品定位文档要求） | 写进 openspec proposal | 不立项 |
| G0-2 Core 任务授权 | 作为独立的、用户可见的 Core 任务，获得明确确认（`AGENTS.md` 的 Website/Core 隔离规则） | 用户明确授权 | 不立项 |
| G0-3 数据形态 | 消费方的数据在文档型数据库还是数据模型；如果是数据模型，底层是哪种数据库 | 调研 S1 给出结论 | 无法确定就不立项 |
| G0-4 鉴权 | 在 G0-3 确定的数据形态下，找到 CI 可用的鉴权方式及其权限范围 | 能收窄到只读；或者负责人书面接受管理员级凭据的风险，并同时确定补偿措施 | 改走方案 C |
| G0-5 Transport | 官方 C# SDK `Tencent.CloudBase` 是否兼容 Native AOT 和裁剪，依赖能否接受 | 调研 S2 给出"复用 SDK"或"自己写"的结论 | 默认自己写 |
| G0-6 临时链接 | 临时链接的域名和签名参数名、`maxAge` 的单位和上限 | 调研 S3 给出结论 | 不阻塞立项，但 T06 不能开始 |

### 2.1 Phase 0 调研（只读，不改仓库代码）

调研只产出结论，记录到 openspec proposal 的"调研结果"一节，不写实现代码。

- **S1 数据形态与查询接口**：在消费方环境的控制台里确认数据位置；确认选定 API 面上的查询路径、分页参数和过滤语法；准备一份脱敏的响应样本，作为 T03/T04 的测试夹具。
- **S2 鉴权与 SDK**：
  - 对 G0-3 确定的数据形态，逐一验证 API Key、用户登录 token（配合数据权限）、签名 V3 加 CAM 子账号（仅文档型数据库）能否只读访问；
  - 用一个不入库的临时工程，对 `Tencent.CloudBase` 做 `PublishAot` 试编译，统计裁剪和 AOT 警告；
  - clientCredential token 的官方文档已失效，如果要考虑它，需要实测或向腾讯云确认。
- **S3 临时链接**：对一个私有文件调用换链接口，记录域名、签名参数名和过期行为。
- **S4 内容样本**：从消费方取 Markdown 正文样本，确认是否含 HTML、相对链接或 `cloud://` 内嵌图片。

## 3. 总体架构

```text
site.yaml ──> Bukit.Config（internal CloudBaseConfig，读取与校验）
                 │
                 ▼
ContentProviderFactory ──> Bukit.Content/CloudBase/CloudBaseContentSource : IContentProvider
                                  │  分页查询、字段投影、cloud:// 换链
                                  ▼
                           Bukit.CloudBase（新项目，只依赖 BCL；或经 G0-5 改用 SDK）
                                  │  鉴权、重试、限速、错误映射
                                  ▼
                           CloudBase HTTP API
                                  │
CloudBaseBodyStore : IContentBodyStore ──> BasicMarkdownToHtml（不可信链接模式）
                                  │
                                  ▼
                 现有管线：媒体本地化 → 路由 → 渲染 → 隐私检查 → 输出
```

**数据流**：

1. `LoadRawAsync` 先按页查询全部记录（不超过 `maxItems`）。
2. 对记录做字段投影；系统字段先剔除，再应用白名单。
3. 收集投影后字段和正文里的 `cloud://` 文件 ID，按 50 个一批换成临时链接，替换回原文。
4. 返回 `RawContentDocument` 列表。正文交给 `CloudBaseBodyStore` 延迟渲染。
5. 现有的媒体本地化管线下载图片；由于第 4 节 D8 的配置校验，本地化一定处于开启状态。

## 4. 设计决策

| 编号 | 决策 | 理由 |
|---|---|---|
| D1 | `ContentSourceConfig` 新增 **internal** 属性 `CloudBase`，类型是 internal 的 `CloudBaseConfig` | 先例：`AppConfig.MiniProgram` 是 internal，`ConfigContractDriftTests` 注释写明它不增加 public API；产品定位要求不扩大公开面 |
| D2 | 新建 `src/Bukit-Core/Bukit.CloudBase/` 项目，只依赖 BCL，负责 transport 和数据模型；`Bukit.Content/CloudBase/` 负责 provider | 对标 `Bukit.Notion` 与 `Bukit.Content/Notion` 的分层；transport 可以独立测试；`NotionBoundaryTests` 的项目引用表要相应更新 |
| D3 | 默认自己写 transport；只有 S2 证明 SDK 的 AOT/裁剪警告为零、依赖可接受时才改用 SDK（待 G0 定） | Core 是 Native AOT；`Bukit.Notion` 的先例是只依赖 BCL；SDK 2026-07 才发布，成熟度有限 |
| D4 | `kind` 只接受 G0-3 选定的一种形态，另一种值报配置错误 | 两种都支持等于做两个 provider（分析文档第 4 节第 1 项） |
| D5 | 鉴权用一个凭据策略接口，但 P1 只实现 G0-4 选定的一种方式；凭据只从环境变量或 `--secrets-stdin` 读取，YAML 里出现凭据字段直接报错 | 避免为未验证的方式写代码；和 Notion 的凭据规则一致 |
| D6 | 查询走 HTTP API 网关；`filter` 只支持单字段等值，`sort` 只支持单字段；分页大小 1～100（上限待 S1 确认） | P1 只覆盖消费方的实际查询需求，避免造一套查询语言 |
| D7 | 字段投影默认 `whitelist`；系统字段和个人字段（`_openid`、`_createBy`、`_updateBy` 等，最终清单待 S1 确认）**硬性剔除**，`fieldPolicy.mode: all` 也不放行 | `_openid` 是用户个人标识，不能靠配置正确来保证不泄露 |
| D8 | `fileFields` 非空时，要求媒体本地化开启（`content.media.downloadToLocal` 不为 false，且 `downloadHosts`/`skipHosts` 不排除临时链接域名），否则配置校验失败 | 私有文件的临时链接会过期，只能在同一次构建内下载（分析文档第 4 节第 3 项） |
| D9 | Markdown 正文用"不可信链接"模式渲染：在 `BasicMarkdownToHtml` 增加一个选项，遍历 Markdig 语法树里的链接和图片，复用 `Bukit.Notion.Rendering.RenderingSafeUrl` 的协议白名单 | `Bukit.Notion` 已经对 `Bukit.Content` 开放 internal（`InternalsVisibleTo`），直接复用不会产生重复代码；Markdown provider 的默认行为保持不变 |
| D10 | `SignedUrlIdentity` 按 S3 的结果增加 CloudBase 临时链接规则 | 关系到小程序签名链接拦截和媒体缓存（分析文档 3.1 节） |
| D11 | 隐私检查在现有 Notion 逻辑旁边**并列**加上 CloudBase 标识检测，P1 不做通用化 | 控制范围；通用化记为技术债 |
| D12 | 诊断码新增 `CloudBase` 前缀，区间暂定 `0x0C01`～`0x0CFF`（格式化后是 `BKT-0C01` 起），同步更新 `BuildDiagnosticCodeTests` 的区间表 | 与现有的按前缀分区规则一致；`0x0C` 区间目前没有被占用 |
| D13 | `cacheMode` 只接受 `off`，其他值报"P1 不支持" | 缓存属于 P2；先占住配置键，避免以后改语义 |
| D14 | 内部诊断和日志只输出记录 ID 的哈希、集合名和 HTTP 状态码，不输出 URL 查询串、请求头和响应体原文 | Core 日志不会脱敏来自环境变量的密钥（分析文档 3.1 节） |

## 5. 配置契约（P1）

```yaml
content:
  sources:
    - name: articles
      type: cloudbase
      mode: content
      collection: posts            # 现有通用字段
      cloudbase:
        envId: prod-xxxx
        region: ap-shanghai        # 决定网关域名（境内 / 境外）
        kind: model                # 只接受 G0-3 选定的值
        model: article             # kind=model 时必填；kind=collection 时改用 collection
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
        fileFields: [cover]
        cacheMode: off             # P1 只接受 off
        maxRps: 5
        maxRetries: 3
```

| 字段 | 必填 | 默认值 | 校验 |
|---|---|---|---|
| `envId` | 是 | — | 非空，字符集待 S1 确认；会被隐私检查当作敏感标识 |
| `region` | 否 | 境内默认地域 | 枚举值，决定使用 `.api.tcloudbasegateway.com` 还是 `.api.intl.tcloudbasegateway.com` |
| `kind` | 是 | — | 只接受 G0-3 选定的值 |
| `model` / `collection` | 按 `kind` 二选一 | — | 非空 |
| `pageSize` | 否 | 100 | 1～上限（待 S1） |
| `maxItems` | 否 | 不限 | 大于 0 |
| `filter` | 否 | 无 | 只允许 `field` + `value` |
| `sort` | 否 | 无 | `direction` 只能是 `asc` 或 `desc` |
| `*Field` 映射 | 否 | 同名字段 | 非空字符串 |
| `bodyField` | `mode: content` 时必填 | — | 非空 |
| `bodyFormat` | 否 | `markdown` | P1 只接受 `markdown` |
| `fieldPolicy` | 否 | `whitelist`、空列表 | 词汇与 Notion 一致（`whitelist` 或 `all`）；硬性剔除列表不受它影响 |
| `fileFields` | 否 | 空 | 非空时触发 D8 校验 |
| `cacheMode` | 否 | `off` | P1 只接受 `off` |
| `maxRps` / `maxRetries` | 否 | 待定（估算 5 / 3） | 大于 0 / 不小于 0 |

**凭据**：变量名待 G0-4 确定，候选是 `CLOUDBASE_API_KEY`、`CLOUDBASE_ACCESS_TOKEN`、`TENCENTCLOUD_SECRETID`/`TENCENTCLOUD_SECRETKEY`。凭据通过 `SecretStore.Get` 读取，不往 public 的 `EnvironmentHelper` 加任何成员。

## 6. 诊断码（暂定）

| 名称 | 值 | 场景 |
|---|---|---|
| `CloudBaseConfigInvalid` | 0x0C01 | 配置缺失、取值越界、形态不支持 |
| `CloudBaseCredentialMissing` | 0x0C02 | 找不到所需的凭据 |
| `CloudBaseAuthFailed` | 0x0C03 | 401/403，或签名错误 |
| `CloudBaseNotFound` | 0x0C04 | 环境、集合或模型不存在 |
| `CloudBaseRateLimited` | 0x0C05 | 重试后仍被限流 |
| `CloudBaseRequestFailed` | 0x0C06 | 其他 HTTP 或网络错误 |
| `CloudBaseResponseInvalid` | 0x0C07 | 响应结构不符合预期 |
| `CloudBaseFileResolveFailed` | 0x0C08 | `cloud://` 换链失败 |
| `CloudBaseMediaLocalizationRequired` | 0x0C09 | 违反 D8 |
| `CloudBaseAdminCredentialInUse` | 0x0C0A | doctor 警告：正在使用管理员级凭据 |

最终清单在 T02 定稿。BukitJalil 按固定表给 BKT 码分类（[消费方契约](../analysis/bukit-bukitjalil-consumer-contract-2026-11-15.zh-CN.md)第 6 条），需要通知对方，并在契约文档里登记 `0Cxx` 区间。

## 7. 安全设计

| 威胁 | 对策 | 验收 |
|---|---|---|
| CI 凭据泄露 | G0-4 尽量只读；如果只能用管理员级凭据，要书面接受风险、doctor 给出警告、凭据只放 CI 密钥库 | AC8 |
| 日志或异常泄露密钥 | D14；transport 的异常信息不拼接 URL 和请求头 | AC7 |
| 个人数据进入公开产物 | D7 硬性剔除；默认白名单；隐私检查兜底（D11） | AC3、AC6 |
| 正文链接注入 | D9 协议白名单 | AC4 |
| 签名链接发布到小程序数据集，或公开产物 | D10；D8 保证本地化；隐私检查检测 `cloud://` | AC5 |
| 远程响应异常（超大、畸形） | 限制分页上限和响应体大小；JSON 解析失败报 `CloudBaseResponseInvalid` | AC2 |
| 缓存或目录误删 | P1 不做缓存，不涉及 | — |

## 8. 组件与文件清单

**新增**（估算 8～12 个文件）：

- `src/Bukit-Core/Bukit.CloudBase/Bukit.CloudBase.csproj`
- `Bukit.CloudBase/Transport/`：`CloudBaseClient`、`CloudBaseClientOptions`、`CloudBaseApiException`、凭据策略（只实现一种）
- `Bukit.CloudBase/Models/`：查询请求和响应模型，以及源生成的 JSON 上下文
- `src/Bukit-Core/Bukit.Content/CloudBase/`：`CloudBaseContentSource`、`CloudBaseContentSourceOptions`、`CloudBaseFieldProjector`、`CloudBaseFileResolver`、`CloudBaseBodyStore`
- `src/Bukit-Core/Bukit.Cli/Commands/DoctorCloudBaseChecker.cs`
- 测试项目 `tests/Bukit.CloudBase.Tests/`（transport），provider 测试放进现有的 `Bukit.Content.Tests`
- `openspec/changes/add-cloudbase-content-source/`：proposal、design、tasks、specs
- 用户文档 `guide/user/` 新增一页（编号在 T09 确定）

**修改**：

- 分析文档 3.1 节的 15 个编辑目标，加上 `BasicMarkdownToHtml`；
- 3.2 节 P1 的 5 项，`PublishUrlSnapshot` 按需调整；
- `Bukit.Content.csproj`（引用 `Bukit.CloudBase`）；
- 解决方案文件；
- `scripts/checks/codex-workflow-policy.v1.json`（新增规则）；
- `docs/governance/bukit-core-public-api-baseline.v1.json`；
- 文档和 CHANGELOG。

## 9. 测试策略

- **Transport**（`Bukit.CloudBase.Tests`，使用伪造的 `HttpMessageHandler`）：
  - 鉴权头的生成；
  - 分页；
  - 429/5xx 重试与退避；
  - 401/403/404 映射到对应的诊断码；
  - 超大响应和畸形 JSON；
  - 异常文本中不含凭据和查询串（AC7）。
- **Provider**（`Bukit.Content.Tests`）：
  - 字段投影：白名单、`all` 模式、硬性剔除（AC3）；
  - `maxItems` 截断；
  - `cloud://` 批量换链：每批 50 个、部分失败的处理（AC5）；
  - 不可信链接渲染，覆盖 `javascript:`、`vbscript:`、`data:`、`//host`、相对路径（AC4）。
- **配置**（`Bukit.Config.Tests`）：
  - 读取、严格字段校验、语义校验、schema；
  - `ConfigContractDriftTests` 和 `ConfigSchemaNestedContractTests` 的键快照（AC1）。
- **诊断码**（`Bukit.Shared.Tests`）：`BuildDiagnosticCodeTests` 的前缀和区间。
- **端到端**（`Bukit.Engine.Tests`）：用伪造的 provider 或 HTTP 构建完整站点；隐私检查能检测 CloudBase 标识（AC2、AC6）。
- **签名链接**：`SignedUrlIdentity` 识别 CloudBase 临时链接；`MiniProgramJson.IsSignedUrl` 能拦截（AC5）。
- **架构**：`NotionBoundaryTests` 更新 `Bukit.Content` 的项目引用表；新增约束，要求 `Bukit.CloudBase` 不引用任何项目。
- **真实环境冒烟**：需要授权，在消费方的测试环境手动运行 `doctor` 和 `build`，结果记录在计划书 T10。

## 10. 验证闭包

对候选文件集预演 `codex-workflow.py closure`，结果如下（2026-11-23 预演；以实施时的实际输出为准）：

- specialtyTests 覆盖 `Bukit.Architecture.Tests`、`Bukit.Cli.Tests`、`Bukit.Config.Tests`、`Bukit.Content.Tests`、`Bukit.Engine.Tests`、`Bukit.Shared.Tests` 的完整测试项目，外加 public-api-drift（self-test 和 check）、`docs/active-links.sh`、`docs/size-policy.sh`、`core-report-inventory.py --self-test`，以及 `Bukit.Cli` 的 Debug 构建。
- `unmappedFiles` 包含 `src/Bukit-Core/Bukit.CloudBase/**` 和 `openspec/changes/add-cloudbase-content-source/**`。因此 T00 必须在 policy 里新增 `bukit-cloudbase-source`、`bukit-cloudbase-tests`、`cloudbase-content-source-change` 三条规则，才能开始实现。
- 按 `AGENTS.md`：不跑 full/release gate、`scripts/test-all.sh`、`scripts/smoke-all.sh` 和整个解决方案的测试；只跑 closure 的输出。GREEN 证据用 `cache record` 记录。

## 11. 兼容性与发布

- **public API**：只有 `DiagnosticCode` 新增成员，属于增量变化，基线随 T02 更新。配置类型全部 internal。
- **配置 schema**：`content.sources[].type` 的枚举增加 `cloudbase`，属于增量变化，对 BukitJalil 的 `config schema` 读取没有破坏。
- **行为**：没有声明 `type: cloudbase` 的站点，构建产物与变更前逐字节相同。端到端测试应断言这一点。
- **发布**：按产品定位，常规公开发布已暂停，本变更只进入内部构建；CHANGELOG 记在 Unreleased 下。

## 12. 风险与回滚

| 风险 | 缓解 |
|---|---|
| G0-4 只能用管理员级凭据 | 改走方案 C；或者书面接受风险，并启用 doctor 警告、CI 密钥隔离、定期轮换 |
| CloudBase API 变化 | 只调用文档列出的接口；transport 集中在 `Bukit.CloudBase` |
| 范围蔓延（HTML、缓存、关联字段） | 严格按 P1 边界执行，新需求进入 P2/P3 |
| Notion 硬编码技术债扩大 | 在 openspec design 里登记，另立通用化任务 |
| 计划低估 | 每个任务完成后记录 metrics；工作量偏差超过 50% 时停下来重新评估 |

**回滚**：每个任务单独提交，可以逐个 revert。功能只在声明 `type: cloudbase` 时生效，回滚不影响现有站点。如果 public API 基线已经更新，回滚时要同步还原基线。

## 13. P2/P3 提纲（另行立项）

- **P2**：
  - readonly/readwrite 缓存，估算约 400 行；
  - `BuildTransaction` 登记缓存资源；
  - 离线快照，工作量待设计，Notion 的同类机制约 1.5k 行；
  - 通用化 `NotionSnapshotScope.IsOffline`。
- **P3**：
  - HTML 正文，新写白名单清洗器，估算 300～500 行加测试；
  - 关联字段；
  - miniprogram 的 body 规则；
  - 基于 `updatedAt` 的增量失效；
  - 第二种数据形态。
