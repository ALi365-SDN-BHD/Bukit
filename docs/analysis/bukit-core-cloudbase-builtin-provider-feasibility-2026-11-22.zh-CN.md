# Bukit Core 内置 `type: cloudbase` 内容源：可行性分析

- 日期：2026-11-22
- 状态：分析，未立项，未实施
- 范围：评估把腾讯 CloudBase（云开发）作为 Core 内置内容源（与 `markdown`、`notion` 并列）的技术与治理可行性；不改任何代码
- 修订：2026-11-22 经一轮独立审计（Critical 1 / Important 7 / Minor 14）后修订。修正了鉴权方案、密钥脱敏的错误陈述、接入面清单、最小可用版的范围与工作量估算、统计口径。
- 前置结论：同一轮讨论中，推荐的默认路线是"CloudBase 同步插件（拉取后落成 Markdown，再由 markdown provider 构建）"，见第 8 节。本文档专门分析方案 A。

## 1. 结论

**技术上可行，方向结论经审计成立。难点不在拉数据，而在 Core 里 Notion 专用的配套设施（隐私、签名链接、诊断码、文档契约），CloudBase 都得再做一份；最大的门槛是治理和安全模型。**

- **头号未决项（阻塞决策）**：官方文档写明，数据模型接口和用户认证接口**不支持腾讯云签名 V3**。如果内部消费方的数据在数据模型里，最小权限的鉴权路径就不可用，只能用管理员级 API Key 或需要刷新的 AccessToken。这会直接改变方案 A 的安全论证，见 4.2 节。
- **最小可用版（P1：只读、仅 Markdown 正文、无缓存）**：生产代码约 **2.0k～3.0k 行**；改动现有源文件约 **22～26 个**，新增 8～12 个；外加 6～8 份文档、public API 基线（如果选择 public 配置）、openspec 和三语 CHANGELOG。分项见第 9 节。
- **生产可用（P2：加缓存和离线构建）**：再加约 400 行。不做缓存，构建就依赖实时网络，与 Route 2"确定性构建"冲突。所以 **P1 只能作为开发里程碑，进生产必须到 P2**。
- **如果要支持 HTML 正文**：Core 里没有可复用的网页 HTML 清洗器，需要新写一个安全关键组件（约 300～500 行加测试）。所以"渲染层比 Notion 简单"只在 Markdown 正文时成立。
- **与 Notion 能力对齐**：Notion 的摄取部分约 8k 行。参照基数是 `Bukit.Content/Notion` 5,489 行加 `Bukit.Notion` 3,838 行，扣除约 1k 行只用于导入、回写的 Write/Conversion 代码。对齐的成本是数千行量级，不建议作为目标。

## 2. 现有扩展点

- 内容源类型写死在 [ContentProviderFactory.cs](../../src/Bukit-Core/Bukit.Engine/ContentProviderFactory.cs)：只认 `markdown` 和 `notion`，其他类型直接报 `Unsupported content source type`。
- `IContentProvider` 只有一个方法 `LoadRawAsync`；正文通过 `IContentBodyStore` 延迟加载。
- 外部插件协议 v1 不支持构建钩子，也不支持动态 DLL；再加上 Native AOT，CLI 用户在构建时无法动态加载一个 provider。
- **库嵌入接缝**：`IContentProviderFactory`、`SiteEngine(..., IContentProviderFactory, ...)` 和 `SiteEngine.BuildAsync(IContentProvider, BuildOptions)` 都是 public，openspec `in-process-build-transactions` 也覆盖不透明的自定义 provider。所以自建宿主可以注入 CloudBase provider。但 Bukit 不发布 NuGet 包，这条路只适合自己编译宿主的内部项目，不改变"CLI 用户只能靠 Core 内置"的结论。第 8 节把它列为方案 D。

## 3. Core 接入面盘点

**统计口径**：在 `src/Bukit-Core` 的 `*.cs` 文件中，排除 `obj`、`Bukit.Content/Notion` 和 `Bukit.Notion`，不区分大小写匹配 "notion"，共 **56 个文件**；区分大小写匹配 `Notion` 为 49 个。下面分四档。

### 3.1 必须改（不改就跑不通、违反契约，或者有安全缺口）

| 层 | 文件 | 要做的事 |
|---|---|---|
| 配置模型 | [AppConfig.cs](../../src/Bukit-Core/Bukit.Config/AppConfig.cs) `ContentSourceConfig` | 新增 CloudBase 配置属性。**public 还是 internal 是一项设计决策**：`ContentSourceConfig` 在 public API 基线里，加 public 属性会触发 public-api-drift；但仓库已有先例，`AppConfig.MiniProgram` 就是 internal，`ConfigContractDriftTests` 注释写明"不增加 public API"。产品定位要求"优先受控简化，而不是扩大公开面"，因此**建议用 internal** |
| YAML 读取 | [ConfigCollectionReader.cs](../../src/Bukit-Core/Bukit.Config/ConfigCollectionReader.cs)、[SiteDefaultsApplier.Content.cs](../../src/Bukit-Core/Bukit.Config/SiteDefaultsApplier.Content.cs) | 增加 `type: cloudbase` 分支，以及对应的 `ReadCloudBaseConfigFrom` |
| 严格字段校验 | [ConfigStrictFieldValidator.cs](../../src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs) | 白名单加入 `cloudbase`，并新增 `ValidateCloudBase` |
| 语义校验 | [ConfigValidator.cs](../../src/Bukit-Core/Bukit.Config/ConfigValidator.cs)、[ProviderValidators.cs](../../src/Bukit-Core/Bukit.Config/ProviderValidators.cs) | 校验必填项、取值范围；凭据只允许来自环境变量；`fileFields` 非空但媒体本地化被关闭（`content.media.downloadToLocal: false`，或 `downloadHosts`/`skipHosts` 排除了 CloudBase 域名）时直接报错 |
| JSON Schema | [ConfigJsonSchemaGenerator.cs](../../src/Bukit-Core/Bukit.Config/ConfigJsonSchemaGenerator.cs) | `EnumSchema("markdown", "notion")` 加上 `cloudbase`。schema 只在运行时生成，仓库里没有提交的 schema 文件 |
| 工厂 | [ContentProviderFactory.cs](../../src/Bukit-Core/Bukit.Engine/ContentProviderFactory.cs) | 新增分支和 `CreateCloudBaseProvider` |
| 诊断码 | [DiagnosticCode.cs](../../src/Bukit-Core/Bukit.Shared/DiagnosticCode.cs)、[DiagnosticCodeFormatter.cs](../../src/Bukit-Core/Bukit.Shared/DiagnosticCodeFormatter.cs) | 新增 CloudBase 诊断码。`DiagnosticCode` 是 **public enum**，新增成员属于 public-api-drift；BukitJalil 按固定表给 BKT 码分类（[消费方契约](bukit-bukitjalil-consumer-contract-2026-11-15.zh-CN.md)第 6 条），新码要同步登记 |
| 签名链接识别（安全） | [SignedUrlIdentity.cs](../../src/Bukit-Core/Bukit.Content/Media/SignedUrlIdentity.cs) | 目前识别 AWS SigV4/SigV2、GCS V4/V2、CloudFront 和 Notion；[用户文档](../../guide/user/04-site-yaml-config.md) 写明 Tencent COS 不识别。不加 CloudBase 规则会有两个后果：一是**安全问题**，[MiniProgramJson.cs](../../src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/MiniProgram/MiniProgramJson.cs) 和 [MiniProgramHtmlSanitizer.cs](../../src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/MiniProgram/MiniProgramHtmlSanitizer.cs) 靠它拦截签名链接，认不出来的 CloudBase 临时链接会连同签名一起发布到小程序数据集，incremental-invalidation 的签名链接报告同样失效；二是缓存问题，私有文件的临时链接每次签名不同，图片会每次构建都重新下载（公开读文件的链接是稳定的，不受影响） |
| 公开产物隐私 | [PublicOutputPrivacyCheck.cs](../../src/Bukit-Core/Bukit.Engine.Audit/PublicOutputPrivacyCheck.cs)、[DeploymentPrivacyValidator.cs](../../src/Bukit-Core/Bukit.Cli/Deploy/DeploymentPrivacyValidator.cs)、[PublicContentProjectionPolicy.cs](../../src/Bukit-Core/Bukit.Engine.Primitives/PublicContentProjectionPolicy.cs)、[JsonFeedGenerator.cs](../../src/Bukit-Core/Bukit.Engine.Feeds/JsonFeedGenerator.cs)、[PublishUrlSnapshot.cs](../../src/Bukit-Core/Bukit.Engine.Audit/PublishUrlSnapshot.cs) | 现在只会清除 Notion ID（JSON Feed 只在 `post.Source == "notion"` 时过滤实体）。CloudBase 的 `envId`、`cloud://` 文件 ID（里面含 envId）、`_id`，尤其是 **`_openid`（用户个人标识）**，都需要同样的拦截 |
| 凭据与日志 | 新 transport 代码；[Logger.cs](../../src/Bukit-Core/Bukit.Shared/Logger.cs) | Core 日志只脱敏 `--secrets-stdin` 传入的值（经 `SecretStore.Redact`），**来自环境变量的密钥不会被脱敏**（Notion 现状也是如此）。因此 transport 必须保证凭据、签名、临时链接永远不进异常信息和日志文本，并为此写测试 |
| 文档与契约测试 | `guide/user/` 的 04、07、16、25 号文档，新增一页类似 `06-notion-content.md` 的用户文档；`guide/dev/config-site-yaml.md`；`guide/skills/bukit-content-model/SKILL.md`；`tests/Bukit.Config.Tests/ConfigContractDriftTests.cs`（schema、AppConfig、严格校验三者必须一致，并对照 guide 检查字段）；`ConfigJsonSchemaGeneratorTests`；`scripts/checks/config-docs-contract.sh`；三语 CHANGELOG | 新 `type` 不同步这些，现有契约检查会失败 |
| Provider 本体 | 新目录 `Bukit.Content/CloudBase/`，或者新建一个只依赖 BCL 的 `Bukit.CloudBase` 项目（对标 `Bukit.Notion`） | 实现 `IContentProvider`、`IContentBodyStore`、HTTP 客户端、字段投影、`cloud://` 换链。新建项目要同步更新 `tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs` 里固定的项目引用表 |

### 3.2 应该改（不改能跑，但质量打折）

| 文件 | 问题 |
|---|---|
| [ContentFieldReservations.cs](../../src/Bukit-Core/Bukit.Engine/ContentFieldReservations.cs)、[ContentSchemaValidator.cs](../../src/Bukit-Core/Bukit.Engine/ContentSchemaValidator.cs) | 保留字段表里有 `notionpageid`、`last_edited_time`，CloudBase 需要对应的系统字段 |
| [MiniProgramConfig.cs](../../src/Bukit-Core/Bukit.Config/MiniProgramConfig.cs) | miniprogram 数据集有一条"Notion 数据源未开 renderContent 就不能声明 body"的规则，CloudBase 如果有不渲染正文的模式，需要对应规则 |
| [DoctorCommand.cs](../../src/Bukit-Core/Bukit.Cli/Commands/DoctorCommand.cs)，参照 `DoctorNotionChecker`/`DoctorNotionSchemaChecker` 新增检查 | 连通性、权限、集合或模型是否存在、字段映射是否正确；使用 API Key 时给出管理员权限警告 |
| [SecretsStdinOption.cs](../../src/Bukit-Core/Bukit.Cli/SecretsStdinOption.cs)、[SecretStore.cs](../../src/Bukit-Core/Bukit.Shared/SecretStore.cs) | 功能上**不用改**：`SecretStore.Get` 接受任意合法名字，`--secrets-stdin` 也接受任意键。只需要在帮助文本和示例里补上 CloudBase 的密钥名。不要往 public 的 `EnvironmentHelper` 加常量或 getter，否则也是 public-api-drift |
| [OutputDirectoryCleaner.cs](../../src/Bukit-Core/Bukit.Engine/OutputDirectoryCleaner.cs) | 只有一句报错文案提到 Notion 缓存，最多改一下文案；缓存保护实际在 `BuildTransaction` |

### 3.3 P2（加缓存时）才需要

- [BuildTransaction.cs](../../src/Bukit-Core/Bukit.Engine/BuildTransaction.cs)：缓存目录要登记为可写资源和监视排除资源。不登记的话，源目录/写入目录的重叠检查和资源锁都覆盖不到；而且 `DevCommand` 用 `BuildTransaction.WatchResources` 排除监视目录，未登记的项目内缓存会让 `bukit dev` 陷入重建循环。
- 离线判断：`ImageAssetLocalizer` 判断离线读的是 Notion 专用的 `NotionSnapshotScope.IsOffline`。CloudBase 离线构建要么复用这套机制并把它通用化，要么另做一套。

### 3.4 不需要改（Notion 专属功能，或只是注释）

- **跨源关系投影**：[CompositeContentProvider.cs](../../src/Bukit-Core/Bukit.Content/CompositeContentProvider.cs) 把 `NotionCrossSourceRelationProjector` 和 `INotionRelationFallbackResolverProvider` 硬编码在合并流程里。CloudBase 的关联字段可以在 provider 内部展开，或者先不支持；要通用化就是改 Core 的合并契约，应另立任务。
- **Notion 快照与钉选**：`BukitCliSpecs`、`BuildCommand`、`WorktreeBuildCoordinator`、`ConfigCommand`、`NotionSnapshotOptions`、`ProgramErrorCodes`、`ConfigOverrides`、`NotionSnapshotScope`、`NotionSnapshot`、`NotionPin`、`NotionDiagnostics`。
- **基于 Notion 数据库选项的功能**：`TaxonomyTermsInjector`、`VariantRouteStage`、`PagesIndexPlugin` 及其 helper、`DefaultNotionPageFetcher`。
- **只在注释里出现，或与类型无关**：`BuildDataKeys`、`ContentImageRewritePipeline`、`MiniProgramDatasetBuilder`、`ContentSourceKeys`、`InternalsVisibleTo`、`DocsCheck/FileRefChecker`。
- **可选**：`ContentConfigFactory.SingleNotion` 是 public，增加 `SingleCloudBase` 会造成 public-api-drift，不建议。

## 4. CloudBase 侧的关键事实与难点

已核实的事实标"已核实"，其余**需要在立项前逐一核实**。

1. **两套 API 面，不能混为一谈**
   - **新版 HTTP API**（网关）：主域名 `https://{env}.api.tcloudbasegateway.com`，境外地域用 `.api.intl.tcloudbasegateway.com`（已核实，[HTTP API 概览](https://docs.cloudbase.net/en/http-api/basic/overview)）。文档说明只承诺已列出接口的兼容性。
   - **旧版 OpenAPI**：`tcb-api.tencentcloudapi.com/api/v2/envs/{envId}/databases/{collection}/documents/{docId}`，用 `X-CloudBase-Authorization` 头做 CloudBase 自有签名，**不是**标准的 TC3 API 3.0 签名（[OpenAPI 简介](https://docs.cloudbase.net/api-reference/openapi/introduction)）。
   - 数据形态也有两套：文档型数据库（NoSQL，类 MongoDB）和数据模型（底层可以是 MySQL，SDK 用 `models.<name>.list()`，见[数据模型 SDK](https://docs.cloudbase.net/database/sdk-reference/model)）。CloudBase CMS 和新版控制台偏向数据模型。**先确认内部消费方的数据实际存在哪一种**，两种都支持等于做两个 provider。
2. **鉴权方式取决于数据形态（阻塞项）**
   - HTTP API 列出两种鉴权：AccessToken 和腾讯云签名 V3（已核实）。
   - **数据模型接口和用户认证接口不支持签名 V3**（已核实，见 HTTP API 概览原文）。
   - **API Key**：永久有效、管理员权限、只能在服务端使用（已核实，[AccessToken 文档](https://docs.cloudbase.net/en/http-api/basic/access-token)）。权限范围是整个环境的管理员，这和 Notion 集成令牌只能访问授权过的数据库不是一个级别。
   - **AccessToken**：默认有效期 2 小时，用 RefreshToken 刷新（已核实）；可以通过策略管理按 API 限制用户和角色权限（[客户端凭据](https://docs.cloudbase.net/http-api/auth/auth-client-credential)）。
   - **结论**：
     - 数据在**文档型数据库**：可以走签名 V3 加 CAM 只读子账号，最小权限最好。具体签哪种算法、走哪个 API 面要先核实，实现量待定。
     - 数据在**数据模型**：只能用 API Key（管理员）或 AccessToken（要实现刷新逻辑，权限靠策略管理收窄）。推荐 AccessToken 加最小策略；API Key 只作为显式选择，doctor 要给出警告。
3. **文件不是 URL**：存储字段是 `cloud://env.xxx/path` 形式的文件 ID，Core 的媒体管线只认 http(s)。provider 必须批量换成临时链接，每次最多 50 个（已核实，[CloudBase 存储文档](https://www.tencentcloud.com/document/product/1266/71666)）。有效期可以用 `maxAge` 设置（示例用 120 分钟），但默认值和上限没有查到权威说法。所以**要在加载时换链接、在同一次构建里下载完**，不能写进缓存给下一次用；这也是 3.1 节对媒体本地化做前置校验的原因。
4. **正文格式**：数据模型有富文本（HTML）和 Markdown 两种字段类型。
   - Markdown 可以复用 `BasicMarkdownToHtml`，它已经禁用原始 HTML（`.DisableHtml()`）。
   - HTML 没有现成的清洗器可用：Notion 是从 block 生成 HTML，不做清洗；Core 里唯一的 HTML 清洗器 `MiniProgramHtmlSanitizer` 是 `Bukit.Engine` 内部的、针对微信富文本子集、依赖 AngleSharp，而 `Bukit.Content` 没有 HTML 解析依赖。
   - **因此 P1 只支持 `bodyFormat: markdown`**，HTML 另行评估。
5. **网络**：境外 CI runner 访问网关的延迟和限流需要实测。重试、限速可以照搬 Notion 的 `MaxRps`/`MaxRetries` 模式。

## 5. 配置草案

```yaml
content:
  sources:
    - name: articles
      type: cloudbase
      mode: content            # content | data
      cloudbase:
        envId: prod-xxxx       # 不是密钥，但会被隐私审计屏蔽
        kind: model            # model | collection，二选一，决定可用的鉴权方式
        model: article
        pageSize: 100
        maxItems: 500
        filter: { field: status, op: eq, value: published }
        sort: { field: publishAt, direction: desc }
        bodyField: content
        bodyFormat: markdown   # P1 只支持 markdown
        fieldPolicy: { mode: whitelist, allowed: [title, slug, publishAt, cover, tags] }
        fileFields: [cover]    # cloud:// → 临时链接 → 本地化
        cacheMode: off         # P2 起支持 readonly | readwrite
```

- 凭据只从环境变量或 `--secrets-stdin` 读取。名字待鉴权方案定下后确定，例如 `CLOUDBASE_ACCESS_TOKEN`、`CLOUDBASE_API_KEY`，或者 `TENCENTCLOUD_SECRETID`/`TENCENTCLOUD_SECRETKEY`。
- `fieldPolicy.mode` 沿用 Notion 现有的 `whitelist|all` 词汇，**默认 `whitelist`**，这样 `_openid`、`_createBy` 这类系统字段和个人字段默认进不了公开产物。

## 6. 风险排序

1. **高 · 凭据权限**：如果数据在数据模型里，签名 V3 不可用，CI 里只能放管理员级 API Key，或者带刷新逻辑的 AccessToken。最小权限能否做到，取决于策略管理能收窄到什么程度。**这一项不闭环，A 的安全论证不成立。**
2. **高 · 隐私**：`_openid` 泄露到公开产物、JSON Feed 或发布快照；`cloud://` ID 暴露 envId；认不出的签名链接进入小程序数据集。必须在 P1 闭环。
3. **高 · 可复现构建**：P1 没有缓存，构建依赖实时网络，与 Route 2 冲突，所以 P1 不能进生产。
4. **中 · 日志泄密**：环境变量里的密钥不受 Core 日志脱敏保护，只能靠 transport 自律加测试。
5. **中 · 契约扩散**：诊断码（public enum）、配置 schema 枚举、BKT 码分类表、文档契约检查；如果选 public 配置，还有 public API 基线。这些都要走兼容性治理。
6. **中 · Notion 硬编码技术债**：每加一个 provider，第 3 节里那些 `if notion` 就多一份复制。要同时决定：是接受复制，还是先把快照、隐私标识、保留字段抽成"按 provider 注册"的扩展点。抽象能降低长期成本，但会让这个任务膨胀成 Core 重构。
7. **低 · API 演进**：CloudBase 已经有新旧两套 API 面，又没有官方 .NET SDK，协议细节都要自己维护；只调用文档列出的接口。

## 7. 治理前置条件

按[产品定位文档](../governance/bukit-core-product-positioning.md)和仓库红线（`AGENTS.md`）：

1. **门槛高于"有消费方"**。产品定位明确规定：
   - Route 2 的摄取范围只有 "Markdown and Notion"；
   - 新增 Core 能力需要具名内部消费方、业务理由、负责人和可验证的验收标准；
   - 没有已证实的内部需求，不加通用集成；
   - 优先受控简化，而不是扩大公开面。

   因此方案 A 不只要找到消费方，还要论证为什么必须扩大 Route 2 的摄取范围。
2. **单独确认这是一个 Core 任务**：Website/Core 隔离红线规定，Core 变更要单独授权。
3. **openspec 变更提案**：新增 `cloudbase-content-source` 规格，并写明对下列现有规格的影响：
   - `core-report-contracts`：其中固定了 `source == notion` 的识别逻辑；
   - `media-localization`：域名规则、签名参数；
   - `miniprogram-data`：签名链接拦截、body 规则；
   - `incremental-invalidation`：签名链接报告；
   - `in-process-build-transactions`：缓存与受管写入集合，P2 时涉及。
4. **验证闭包**：用 `python3 scripts/checks/codex-workflow.py closure` 生成；需要跑的有 `Bukit.Config.Tests`（含 `ConfigContractDriftTests`）、`Bukit.Content.Tests`、`Bukit.Engine.Tests`、`Bukit.Cli.Tests` 的相关部分，以及 `Bukit.Architecture.Tests`、`config-docs-contract.sh` 和 public-api-drift（`scripts/checks/public-api-drift.sh`）。

## 8. 与其他路线的关系

| | A. Core 内置 `type: cloudbase` | B. 新建外部 provider 协议，再写 CloudBase 插件 | C. CloudBase 同步插件（推荐的默认路线） | D. 自建宿主注入 provider |
|---|---|---|---|---|
| Core 改动 | 第 3 节所列 | 新协议、宿主、缓存/快照契约，改动最大 | 无 | 无（用现有 public 接缝） |
| 治理门槛 | 第 7 节全部 | 同 A，还要做协议设计评审 | 插件自己的成熟度审计 | 只对自建宿主项目负责 |
| 可复现构建 | P2 起才有 | 同 A | 同步产物可以提交、可审计，构建离线可复现 | 自行负责 |
| 体验 | 一步 `build` | 一步 `build` | 两步：`pull` 再 `build` | 不能用标准 CLI |
| 不选的原因 | 门槛与成本见上 | 成本最高 | — | 没有 NuGet 分发，只适合自编译宿主；放弃 CLI 和所有 Core 审计能力 |

满足下面几条时，A 才优于 C：
- 有具名内部站点必须"构建时直连"，比如内容要秒级更新、流程里不能有同步步骤；
- CloudBase 的重要性接近 Notion；
- 需要跨源 relation 或 `mode: data` 这类同步做不到的能力；
- 4.2 节的鉴权问题有可接受的答案。

## 9. 建议路线与工作量（若立项 A）

满足第 7 节的前提、并解决第 10 节第 1 项之后，分阶段推进，每步单独提交。

**P1 最小可用版（开发里程碑，不进生产）**：只支持一种数据形态、只支持 Markdown 正文、不做缓存。

| 组件 | 估算（行） |
|---|---|
| Transport：鉴权、重试、限速 | 600～800 |
| Provider、字段投影、body store | 500～800 |
| 配置读取、严格校验、schema、语义校验 | 300～400 |
| `cloud://` 换临时链接 | 约 150 |
| 隐私、签名链接、小程序相关修改 | 200～300 |
| Doctor 检查 | 150～250 |
| 诊断码 | 约 50 |
| **合计** | **约 2.0k～2.8k**（按 3.0k 留余量） |

参照：Notion transport 634 行，`NotionContentSource` 657 行，两个 doctor 检查器 83 + 107 行，Markdown provider 6 个文件共 1,186 行。

**P2 生产可用**：readonly/readwrite 缓存加离线构建，约 400 行（参照 `NotionCacheManager` 304 行、`AtomicNotionCacheWriter` 95 行），同时做 3.3 节的改动。

**P3 按需再做**：HTML 正文（新写清洗器，300～500 行加测试）、关联字段、miniprogram 的 body 规则、增量失效。

**协同点**：Core 已经有 miniprogram-data 内置功能（见 `openspec/specs/miniprogram-data/spec.md`），而 CloudBase 本身就是小程序的后端。如果内部消费方的场景是"小程序和 CloudBase 写内容，同时发布网站和小程序数据集"，A 的业务理由会比一般的通用集成强得多。立项时可以把这一点作为核心论据。

## 10. 待核实项（按优先级）

1. **阻塞**：内部消费方的数据在文档型数据库还是数据模型；对应 API 面上可用的鉴权方式，以及能否收窄到只读最小权限。
2. 选定 API 面上查询接口的准确路径、分页参数和过滤语法。
3. CloudBase 临时链接的域名和签名参数名，用于 `SignedUrlIdentity` 规则。
4. 私有文件临时链接 `maxAge` 的默认值和上限。
5. 境外 CI runner 访问网关的延迟和限流。
