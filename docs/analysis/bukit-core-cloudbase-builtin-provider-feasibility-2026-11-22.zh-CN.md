# Bukit Core 内置 `type: cloudbase` 内容源：可行性分析

- 日期：2026-11-22
- 状态：分析，未立项，未实施
- 范围：评估把腾讯 CloudBase（云开发）作为 Core 内置内容源（与 `markdown`、`notion` 并列）的技术与治理可行性；不改任何代码
- 修订（同日共三轮独立审计）：
  - 第一轮（Critical 1 / Important 7 / Minor 14）：修正了鉴权方案、密钥脱敏的错误陈述、接入面清单、最小可用版范围、工作量估算和统计口径。
  - 第二轮（Critical 1 / Important 6 / Minor 14）：
    - 区分了两种 AccessToken；
    - 修正了 SiteEngine 接缝的可见性和 PublishUrlSnapshot 的性质；
    - 修正了"无缓存不能进生产"的推论和 P2 估算；
    - 改正了"没有官方 .NET SDK"的错误陈述；
    - 补充了 Markdown 链接协议风险。
  - 第三轮（Critical 0 / Important 6 / Minor 17）：
    - clientCredential 官方页面已不存在，相关结论全部降为待核实；
    - 已文档化的 HTTP API 角色策略不覆盖数据库/数据模型；
    - C# SDK 的源码仓库无法访问，补充了 NuGet 事实；
    - 改为诊断码必然导致 public API 基线变化；
    - 补齐被迫修改的现有测试，重算文件计数；
    - 修正若干描述精度问题。
- 前置结论：同一轮讨论中，推荐的默认路线是"CloudBase 同步插件（拉取后落成 Markdown，再由 markdown provider 构建）"，见第 8 节。本文档专门分析方案 A。
- 标注约定：
  - 经代码或官方文档核对的事实，会注明出处或标"已核实"；
  - "估算"是基于参照代码的推算；
  - "待核实"是尚未确认的信息。
  - 代码陈述以提交 `a6b2bb87` 时的仓库内容为准。

## 1. 结论

**技术上可行。难点不在拉数据，而在 Core 里 Notion 专用的配套设施（隐私、签名链接、诊断码、文档契约），CloudBase 都得再做一份；最大的门槛是治理和鉴权。**

- **头号未决项（阻塞决策）**：目前没有找到一条**已核实**的、适合 CI 的最小权限鉴权路径，见第 4 节第 2 项。
  - 已核实：数据模型接口和用户认证接口不支持腾讯云签名 V3。
  - 已核实：API Key 是永久有效的管理员凭据。
  - 已核实：Open API 文档自述"以管理员身份"调用。
  - 待核实：服务端 clientCredential token 的权限和有效期。官方页面已经不存在，搜索摘录的中英文说法互相矛盾。
  - 待核实：用户登录 token 加角色策略。已文档化的 HTTP API 策略只覆盖云存储、云函数和云托管，**没有数据库或数据模型**。
- **最小可用版（P1：只支持一种数据形态、只读、仅 Markdown 正文、无缓存）**：
  - 生产代码约 **2.0k～2.9k 行**；
  - 改动现有源文件约 **20～22 个**，现有测试约 **3～4 个**；
  - 新增约 8～12 个文件；
  - 外加 6～8 份文档、openspec 和三语 CHANGELOG；
  - **public API 基线必然要更新**，因为诊断码是 public enum；如果配置也选 public，基线变化更大；
  - 以上数字均为估算，分项与计数口径见第 3 节和第 9 节。
- **能否进生产是策略问题，不是技术缺陷**：
  - Notion 的 `cacheMode` 默认就是 `off`；除离线快照外，各种缓存模式下都要实时查询数据库（已核实）。
  - 如果以"与 Notion 现状对齐"为标准，P1 就够了。
  - 如果以"离线可复现"为标准，需要一套快照机制，只有缓存不够。
- **生产增强（P2）**：缓存约 400 行（估算）；离线快照的工作量要等设计后再估，Notion 的同类机制约 1.5k 行。
- **正文安全**：
  - Markdown 正文可以复用现有渲染器，但它**不过滤链接协议**。CloudBase 内容可以远程编辑，所以 P1 必须加链接协议白名单。
  - HTML 正文没有可复用的白名单清洗器，P1 不支持。
- **官方有 C# SDK**（NuGet 包 `Tencent.CloudBase`）：
  - 它依赖 Microsoft.Extensions 系列，不是像 `Bukit.Notion` 那样只依赖 BCL；
  - 2026-07 才发布，成熟度有限；
  - 是否兼容 Native AOT 和裁剪，待核实。
- **与 Notion 能力对齐**：Notion 的摄取部分约 8.3k 行（`Bukit.Content/Notion` 5,489 行加 `Bukit.Notion` 3,838 行，减去只用于导入、回写的 Write/Conversion 代码 1,033 行）。对齐的成本是数千行量级，不建议作为目标。

## 2. 现有扩展点

- 内容源类型写死在 [ContentProviderFactory.cs](../../src/Bukit-Core/Bukit.Engine/ContentProviderFactory.cs)：只认 `markdown` 和 `notion`，其他类型直接报 `Unsupported content source type`。
- `IContentProvider` 只有一个方法 `LoadRawAsync`；正文通过 `IContentBodyStore` 延迟加载。
- 外部插件协议 v1 不支持构建钩子，也不支持动态 DLL；再加上 Native AOT，CLI 用户在构建时无法动态加载一个 provider。
- **库嵌入接缝**：
  - `IContentProviderFactory` 和 `SiteEngine.BuildAsync(IContentProvider, BuildOptions)` 是 public。SiteEngine 接收工厂参数的构造函数是 internal，public 的只有 `SiteEngine(ILogger)`。
  - 自建宿主可以通过 `BuildAsync(IContentProvider, …)` 注入 CloudBase provider。这条路径的配置由 internal 的 `BuildOptionsMapper.ToAppConfig` 合成，没有 site.yaml 语义。
  - 对自定义 provider，openspec `in-process-build-transactions` 规定不得猜测其隐藏的根目录，保护范围必须由 provider 声明。换句话说，没有声明的部分不受保护。
  - Bukit 不发布 NuGet 包，所以这条路只适合自己编译宿主的内部项目，不改变"CLI 用户只能靠 Core 内置"的结论。
  - 第 8 节把它列为方案 D。

## 3. Core 接入面盘点

**统计口径**：在 `src/Bukit-Core` 的 `*.cs` 文件中，排除 `obj`、`Bukit.Content/Notion` 和 `Bukit.Notion`，不区分大小写匹配 "notion"，共 **56 个文件**；区分大小写匹配 `Notion` 为 49 个。3.1～3.4 节覆盖了这 56 个文件，外加不含 "notion" 字样、但同样相关的文件，例如 `BasicMarkdownToHtml`、`Logger`、`DevCommand`、`HtmlTextProjection`。

**改动文件计数口径**：
- 计入：3.1 节的编辑目标（15 个，加上链接协议过滤如果改在现有渲染器里，再加 `BasicMarkdownToHtml`），加上 3.2 节标为 P1 的编辑项（5 个，`PublishUrlSnapshot` 如果需要调整，再加 1 个）。
- 不计入：只作为"原因"列出的文件、测试和文档。
- 合计约 **20～22 个现有源文件**。

### 3.1 必须改（不改就跑不通、违反契约，或者有安全缺口）

| 层 | 编辑目标 | 要做的事 |
|---|---|---|
| 配置模型 | [AppConfig.cs](../../src/Bukit-Core/Bukit.Config/AppConfig.cs) `ContentSourceConfig` | 新增 CloudBase 配置属性。**public 还是 internal 是一项设计决策**：`ContentSourceConfig` 在 public API 基线里，加 public 属性会触发 public-api-drift；但仓库已有先例，`AppConfig.MiniProgram` 就是 internal，`ConfigContractDriftTests` 注释写明它"不增加 public API"。产品定位要求优先受控简化、不扩大公开面，因此**建议用 internal** |
| YAML 读取 | [ConfigCollectionReader.cs](../../src/Bukit-Core/Bukit.Config/ConfigCollectionReader.cs)、[SiteDefaultsApplier.Content.cs](../../src/Bukit-Core/Bukit.Config/SiteDefaultsApplier.Content.cs) | 增加 `type: cloudbase` 分支，以及对应的 `ReadCloudBaseConfigFrom` |
| 严格字段校验 | [ConfigStrictFieldValidator.cs](../../src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs) | 白名单加入 `cloudbase`，并新增 `ValidateCloudBase` |
| 语义校验 | [ConfigValidator.cs](../../src/Bukit-Core/Bukit.Config/ConfigValidator.cs)、[ProviderValidators.cs](../../src/Bukit-Core/Bukit.Config/ProviderValidators.cs) | 校验必填项和取值范围；凭据只允许来自环境变量；`fileFields` 非空但媒体本地化被关闭时直接报错（关闭指 `content.media.downloadToLocal: false`，或 `downloadHosts`/`skipHosts` 排除了 CloudBase 域名） |
| JSON Schema | [ConfigJsonSchemaGenerator.cs](../../src/Bukit-Core/Bukit.Config/ConfigJsonSchemaGenerator.cs) | `EnumSchema("markdown", "notion")` 加上 `cloudbase`。schema 只在运行时生成，仓库里没有提交的 schema 文件 |
| 工厂 | [ContentProviderFactory.cs](../../src/Bukit-Core/Bukit.Engine/ContentProviderFactory.cs) | 新增分支和 `CreateCloudBaseProvider` |
| 诊断码 | [DiagnosticCode.cs](../../src/Bukit-Core/Bukit.Shared/DiagnosticCode.cs)、[DiagnosticCodeFormatter.cs](../../src/Bukit-Core/Bukit.Shared/DiagnosticCodeFormatter.cs) | 新增 CloudBase 诊断码。`DiagnosticCode` 是 **public enum**，成员逐个列在 public API 基线里，所以新增成员**必然**造成 public-api-drift；BukitJalil 按固定表给 BKT 码分类（[消费方契约](bukit-bukitjalil-consumer-contract-2026-11-15.zh-CN.md)第 6 条），新码要同步登记 |
| 签名链接识别（安全） | [SignedUrlIdentity.cs](../../src/Bukit-Core/Bukit.Content/Media/SignedUrlIdentity.cs) | 目前识别 AWS SigV4/SigV2、GCS V4/V2、CloudFront 和 Notion；[用户文档](../../guide/user/04-site-yaml-config.md) 写明 Tencent COS 不识别。不加 CloudBase 规则有两个后果，见本表下方说明 |
| 公开产物隐私 | [PublicOutputPrivacyCheck.cs](../../src/Bukit-Core/Bukit.Engine.Audit/PublicOutputPrivacyCheck.cs)、[DeploymentPrivacyValidator.cs](../../src/Bukit-Core/Bukit.Cli/Deploy/DeploymentPrivacyValidator.cs)、[PublicContentProjectionPolicy.cs](../../src/Bukit-Core/Bukit.Engine.Primitives/PublicContentProjectionPolicy.cs)、[JsonFeedGenerator.cs](../../src/Bukit-Core/Bukit.Engine.Feeds/JsonFeedGenerator.cs) | 这些代码现在只针对 Notion 标识：前两个检测到泄露后让构建或部署失败；后两个负责过滤（JSON Feed 只在 `post.Source == "notion"` 时过滤实体）。CloudBase 的 `envId`、`cloud://` 文件 ID（里面含 envId）、`_id`，尤其是 **`_openid`（用户个人标识）**，都需要同样的检测和过滤 |
| 正文链接安全 | [BasicMarkdownToHtml.cs](../../src/Bukit-Core/Bukit.Content/Markdown/BasicMarkdownToHtml.cs)，或者 provider 内新写的 URL 策略 | 渲染器只配置了 `.DisableHtml()`，没有链接和图片的协议过滤；Notion 渲染则通过 `RenderingSafeUrl` 做协议白名单（链接允许 http/https/mailto/tel，媒体允许 http/https）。仓库内的 Markdown 是可信输入，CloudBase 正文是远程可编辑的，`[x](javascript:…)` 这类链接可能进入发布的 HTML。旁证：本地 Markdig 0.38.0 程序集里没有 `javascript`、`vbscript`、`data:` 字符串，`src/` 里也没有协议过滤代码。要对 CloudBase 正文加协议白名单（复用 `RenderingSafeUrl` 的策略），并用测试确认 |
| 文档与契约测试 | 文档：`guide/user/` 的 04、07、16、25 号文档，新增一页类似 `06-notion-content.md` 的用户文档，`guide/dev/config-site-yaml.md`，`guide/skills/bukit-content-model/SKILL.md`，三语 CHANGELOG。会被迫修改的现有测试见本表下方说明 | 文档**漏写** cloudbase 不会被检查发现：`config-docs-contract.sh` 检查的是固定的必需/禁止词，以及 Notion `propertyMap` 字段清单与 AppConfig 是否一致。所以文档是为了完整性，要靠评审保证 |
| Provider 本体 | 新目录 `Bukit.Content/CloudBase/`，或者新建一个只依赖 BCL 的 `Bukit.CloudBase` 项目（对标 `Bukit.Notion`） | 实现 `IContentProvider`、`IContentBodyStore`、HTTP 客户端、字段投影、`cloud://` 换链 |

**签名链接识别不加 CloudBase 规则的两个后果**：
- **安全**：`MiniProgramJson.IsSignedUrl` 用它拦截签名链接，调用方有 [MiniProgramHtmlSanitizer.cs](../../src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/MiniProgram/MiniProgramHtmlSanitizer.cs)（正文）和 [MiniProgramDatasetBuilder.cs](../../src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/MiniProgram/MiniProgramDatasetBuilder.cs)（数据集字段）。
  - `IsSignedUrl` 另外还会拦截带裸 `Signature`、`Expires`、`X-Amz-Credential` 参数的链接，所以 CloudBase 临时链接会不会漏进小程序数据集，取决于它的参数名（第 10 节第 4 项）。
  - incremental-invalidation 的签名链接报告也依赖 `SignedUrlIdentity`。
  - 以上小程序相关文件只是原因，不是编辑目标。
- **缓存**：私有文件的临时链接每次签名不同，图片会每次构建都重新下载。公开读文件的链接是稳定的，不受影响。

**会被迫修改的现有测试（约 3～4 个，估算）**：
- `tests/Bukit.Config.Tests/ConfigContractDriftTests.cs`：硬编码了 `content.sources[]` 的键集合。
- `tests/Bukit.Config.Tests/ConfigSchemaNestedContractTests.cs`：硬编码了 `content/sources[]` 的键快照。
- `tests/Bukit.Shared.Tests/BuildDiagnosticCodeTests.cs`：要求每个诊断码的名字以固定前缀开头、取值落在对应区间。新增 `CloudBase` 前缀和区间就必须改测试；否则只能把诊断码放在现有前缀下。
- `tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`：固定了 `Bukit.Content` 的项目引用表，新建 `Bukit.CloudBase` 项目时需要改。

**不计为编辑目标、但必须遵守的约束**：
- **凭据与日志**：[Logger.cs](../../src/Bukit-Core/Bukit.Shared/Logger.cs) 只脱敏 `--secrets-stdin` 传入的值（经 `SecretStore.Redact`），**来自环境变量的密钥不会被脱敏**，Notion 现状也是如此。因此 transport 必须保证凭据、签名和临时链接永远不进异常信息和日志文本，并为此写测试。

### 3.2 应该改（不改能跑，但质量打折）

| 阶段 | 文件 | 问题 |
|---|---|---|
| P1 | [ContentFieldReservations.cs](../../src/Bukit-Core/Bukit.Engine/ContentFieldReservations.cs)、[ContentSchemaValidator.cs](../../src/Bukit-Core/Bukit.Engine/ContentSchemaValidator.cs) | 保留字段表里有 `notionpageid`、`last_edited_time`，CloudBase 需要对应的系统字段 |
| P1 | [DoctorCommand.cs](../../src/Bukit-Core/Bukit.Cli/Commands/DoctorCommand.cs) | 参照 `DoctorNotionChecker`、`DoctorNotionSchemaChecker`（只作参照，不改）新增检查：连通性、权限、集合或模型是否存在、字段映射是否正确；使用管理员级凭据时给出警告 |
| P1 | [SecretsStdinOption.cs](../../src/Bukit-Core/Bukit.Cli/SecretsStdinOption.cs) | 功能上不用改，只需要在帮助文本和示例里补上 CloudBase 的密钥名。`SecretStore` 不用改：`Get` 接受任意非空名字，`--secrets-stdin` 接受任意环境变量风格的键（`^[A-Za-z_][A-Za-z0-9_]{0,127}$`）。`EnvironmentHelper` 是 public，不要往里加常量或 getter，否则也是 public-api-drift |
| P1 | [OutputDirectoryCleaner.cs](../../src/Bukit-Core/Bukit.Engine/OutputDirectoryCleaner.cs) | 只有一句报错文案提到 Notion 缓存，最多改一下文案；缓存保护实际在 `BuildTransaction` |
| P1（按需） | [PublishUrlSnapshot.cs](../../src/Bukit-Core/Bukit.Engine.Audit/PublishUrlSnapshot.cs)（`PublishUrlSemanticHasher`） | **不是隐私问题**：快照只存 schema、站点 URL，以及每条路由的 URL、是否可索引和语义哈希。哈希前会归一化文本，替换 32 位十六进制 ID 和本地路径。真正要看的是哈希稳定性：正文里每次重签的临时链接会改变语义哈希，导致未变化的 URL 被报告为已变化。需要评估是否加入 CloudBase 链接的归一化规则（`_id` 对同一文档是稳定的，不受影响） |
| P3 | [MiniProgramConfig.cs](../../src/Bukit-Core/Bukit.Config/MiniProgramConfig.cs) | miniprogram 数据集有一条"Notion 数据源未开 renderContent 就不能声明 body"的规则。CloudBase 如果有不渲染正文的模式，需要对应规则 |

### 3.3 P2（加缓存或离线时）才需要

- [BuildTransaction.cs](../../src/Bukit-Core/Bukit.Engine/BuildTransaction.cs)：缓存目录要登记为可写资源和监视排除资源。
  - 不登记的话，源目录/写入目录的重叠检查和资源锁都覆盖不到。
  - `DevCommand` 用 `BuildTransaction.WatchResources` 排除监视目录，未登记的项目内缓存会让 `bukit dev` 陷入重建循环。
- `NotionSnapshotScope`、`ImageAssetLocalizer`：`ImageAssetLocalizer` 判断离线读的是 Notion 专用的 `NotionSnapshotScope.IsOffline`。CloudBase 离线构建要么把这套机制通用化，要么另做一套。

### 3.4 不需要改（Notion 专属功能、已经通用、只作参照或只是注释）

- **跨源关系投影**：[CompositeContentProvider.cs](../../src/Bukit-Core/Bukit.Content/CompositeContentProvider.cs) 把 `NotionCrossSourceRelationProjector` 和 `INotionRelationFallbackResolverProvider` 硬编码在合并流程里。CloudBase 的关联字段可以在 provider 内部展开，或者先不支持；要通用化就是改 Core 的合并契约，应另立任务。
- **Notion 快照与钉选**：`BukitCliSpecs`、`BuildCommand`、`WorktreeBuildCoordinator`、`ConfigCommand`、`NotionSnapshotOptions`、`ProgramErrorCodes`、`ConfigOverrides`、`NotionSnapshot`、`NotionPin`、`NotionDiagnostics`。
- **基于 Notion 数据库选项的功能**：`TaxonomyTermsInjector`、`VariantRouteStage`、`PagesIndexPlugin`、`PagesIndexCacheHelper`、`PagesIndexConfigHelper`、`DefaultNotionPageFetcher`。
- **已经通用**：[PluginSecretMasker.cs](../../src/Bukit-Core/Bukit.PluginHost/PluginSecretMasker.cs) 按 `API_KEY`、`TOKEN`、`SECRET` 等片段匹配，已经能覆盖 CloudBase 的候选密钥名。
- **只作参照**：`DoctorNotionChecker`、`DoctorNotionSchemaChecker`、`SecretStore`、`EnvironmentHelper`（理由见 3.2 节）。
- **只在注释里出现，或与类型无关**：`BuildDataKeys`、`ContentImageRewritePipeline`、`ContentSourceKeys`、`InternalsVisibleTo`、`DocsCheck/FileRefChecker`。
- **`MiniProgramDatasetBuilder`**：文件里的 "notion" 只出现在注释中，不用改；但它是 `IsSignedUrl` 的调用方之一，签名链接风险见 3.1 节。
- **可选**：`ContentConfigFactory.SingleNotion` 是 public，增加 `SingleCloudBase` 会造成 public-api-drift，不建议。

## 4. CloudBase 侧的关键事实与难点

1. **API 面与数据形态**
   - **HTTP API（网关）**：主域名 `https://{env}.api.tcloudbasegateway.com`，境外地域用 `.api.intl.tcloudbasegateway.com`（已核实，[HTTP API 概览](https://docs.cloudbase.net/en/http-api/basic/overview)）。文档说明只承诺已列出接口的兼容性。
   - **Cloudbase Open API**（`tcb-api.tencentcloudapi.com`，路径形如 `/api/v2/envs/{envId}/databases/{collection}/documents/{docId}`，[OpenAPI 简介](https://docs.cloudbase.net/api-reference/openapi/introduction)）。以下均已核实：
     - 使用 `X-CloudBase-Authorization` 头（版本前缀 `1.0`）；
     - 签名用 CAM 密钥，按简化的 TC3-HMAC-SHA256 计算，文档称与 CAM 签名流程"大体相同，但在参数上有所简化"；
     - 临时密钥时附加 `X-CloudBase-SessionToken`；
     - 页面自述"以管理员身份"调用 CloudBase 服务；
     - 页面没有弃用标注。
   - **数据形态**：文档型数据库（NoSQL，类 MongoDB），或者数据模型（`models.<name>.list()` 查询，见[数据模型查询](https://docs.cloudbase.net/api-reference/webv3/model/fetch)）。
     - 数据模型底层可以是文档型数据库，也可以是 MySQL（已核实，[数据字段](https://docs.cloudbase.net/en/model/data-field)）。
     - 底层是文档型数据库的模型能否直接用文档数据库接口读取（那样就能用签名 V3），待核实。
     - CloudBase CMS 的数据在向数据模型迁移：旧版 CMS 仓库已归档，官方文档引导把 CMS 数据迁到数据模型。这一点是间接证据，"新版产品偏向数据模型"属于推断。
     - **先确认内部消费方的数据实际存在哪里**；两种都支持，等于做两个 provider。
2. **鉴权（阻塞项）**
   - HTTP API 列出两种鉴权：AccessToken 和腾讯云签名 V3（已核实）。**数据模型接口和用户认证接口不支持签名 V3**（已核实，HTTP API 概览原文）。
   - **API Key**：永久有效、管理员权限、只能在服务端使用（已核实，[AccessToken 文档](https://docs.cloudbase.net/en/http-api/basic/access-token)）。权限范围是整个环境的管理员，这和 Notion 集成令牌只能访问授权过的数据库不是一个级别。
   - **用户登录 token**：默认 2 小时有效，用 RefreshToken 刷新，权限是登录用户的权限（已核实，同上）。
     - 角色策略方面，已文档化的 HTTP API 策略（[策略管理](https://docs.cloudbase.net/api-reference/webv4/strategy)，控制台路径"云开发平台 → 身份认证 → 权限控制"）只有 `StoragesHttpApiAllow`、`FunctionsHttpApiAllow`、`CloudrunHttpApiAllow` 三项，**不覆盖数据库或数据模型 HTTP API**（已核实）。
     - 数据库或数据模型能否靠自身的数据权限按角色收窄，待核实。
     - 用于 CI 还需要一个专门用户，以及登录和刷新流程。
   - **服务端 token（clientCredential）**：官方页面已经不存在（返回 404 页面），搜索摘录的中英文说法互相矛盾。一种说法是管理员权限、不能刷新、有效期 5 天；另一种说法是可以刷新、有效期 31 天。权限、有效期和是否限流**全部待核实**。
   - **结论**：目前没有已核实的、适合 CI 的最小权限路径。
     - 文档型数据库走签名 V3 加 CAM 只读子账号，是否真能限制为只读，待核实：Open API 文档自述"以管理员身份"调用。
     - 数据模型能确定可用的只有管理员级 API Key；用户登录 token 能否收窄到只读，待核实。
     - **鉴权方案在第 10 节第 1 项核实之前不做推荐**；如果最终只能用管理员级凭据，doctor 必须给出警告。
3. **文件不是 URL**：存储字段保存的是 `cloud://env.xxx/path` 形式的文件 ID，Core 的媒体管线只认 http(s)。
   - provider 必须批量换成临时链接，每次最多 50 个（已核实，[CloudBase 存储文档](https://www.tencentcloud.com/document/product/1266/71666)）。
   - 公开读文件的链接不会过期。
   - 私有文件链接的有效期用 `maxAge` 设置；示例写的是 `120 * 60 * 1000`，单位文档没写明；默认值和上限也没写。
   - 所以私有文件要在加载时换链接、在同一次构建里下载完，不能写进缓存给下一次用。这也是 3.1 节对媒体本地化做前置校验的原因。
4. **正文格式**：数据模型有富文本（HTML，最大 262144 字节）和 Markdown 两种字段类型（已核实，[数据字段](https://docs.cloudbase.net/en/model/data-field)）。
   - Markdown 可以复用 `BasicMarkdownToHtml`：它已经禁用原始 HTML，但不过滤链接协议，见 3.1 节"正文链接安全"。
   - HTML 没有可复用的白名单清洗器：
     - Notion 是从 block 生成 HTML，不做清洗；
     - Core 里唯一的白名单式 HTML 清洗器 `MiniProgramHtmlSanitizer` 是 `Bukit.Engine` 内部的、针对微信富文本子集、依赖 AngleSharp；
     - `HtmlTextProjection.Sanitize` 用于文本投影：它剥离 script/style/template，把 xmp、iframe 等元素转成 textarea，去掉未闭合锚点的 href，但不是白名单清洗；
     - `Bukit.Content` 没有 HTML 解析依赖。
   - **因此 P1 只支持 `bodyFormat: markdown`**，HTML 另行评估。
5. **网络与限流**：CI runner（尤其是境外的）访问网关的延迟和限流需要实测。重试、限速可以照搬 Notion 的 `MaxRps`/`MaxRetries` 模式。
6. **官方 C# SDK**（已核实，[C# SDK 文档](https://docs.cloudbase.net/en/api-reference/csharp) 和 [NuGet 包 Tencent.CloudBase](https://www.nuget.org/packages/Tencent.CloudBase)）：
   - 覆盖文档型数据库、数据模型和存储；
   - 1.0.0 和 1.0.1 都发布于 2026-07-23，MIT 许可；
   - net10.0 目标依赖 Microsoft.Extensions.DependencyInjection.Abstractions、Http、Options；
   - 文档提到的 GitHub 源码仓库公开访问不到。
   - 和只依赖 BCL 的 `Bukit.Notion` 相比，它的依赖面更大、成熟度有限。是否兼容 Native AOT 和裁剪，待核实。
   - 核实结果决定 transport 是复用 SDK 还是自己写。

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

1. **高 · 凭据权限**：没有已核实的最小权限 CI 路径，见第 4 节第 2 项。如果最终只能用管理员级凭据，CI 泄露就等于整个环境失守。**这一项不闭环，A 的安全论证不成立。**
2. **高 · 隐私**：`_openid` 可能泄露到公开 HTML 或 JSON Feed；`cloud://` ID 会暴露 envId；认不出的签名链接可能进入小程序数据集。必须在 P1 闭环。
3. **高 · 正文链接注入**：远程可编辑的 Markdown 正文里，`javascript:` 之类的链接可能进入发布的 HTML。P1 必须加协议白名单和测试。
4. **中 · 构建确定性**：默认实时拉取，与 Notion 现状一致；但 Route 2 把确定性构建列为投资方向。如果把离线可复现作为准入条件，就需要快照机制（P2，工作量待设计）。
5. **中 · 日志泄密**：环境变量里的密钥不受 Core 日志脱敏保护，只能靠 transport 自律加测试。
6. **中 · 契约扩散**：
   - 诊断码是 public enum，必然更新 public API 基线；
   - 配置 schema 枚举和 BKT 码分类表要同步；
   - 约 3～4 个契约测试要改；
   - 如果配置也选 public，基线变化更大；
   - 这些都要走兼容性治理。
7. **中 · Notion 硬编码技术债**：每加一个 provider，第 3 节里那些 `if notion` 就多一份复制。要同时决定：是接受复制，还是先把快照、隐私标识、保留字段抽成"按 provider 注册"的扩展点。抽象能降低长期成本，但会让这个任务膨胀成 Core 重构。
8. **低 · 运营**：
   - 配额与费用：每次构建都会产生数据库读取和存储流量，受套餐限额约束；
   - 限流：网关有限流；
   - 数据驻留：环境地域与 CI runner 所在地不同时，拉取的数据可能含个人数据（`_openid`）；
   - 增量指纹：Notion 按 `last_edited_time` 复用正文，CloudBase 需要对应的更新时间字段。
9. **低 · API 与 SDK 演进**：
   - CloudBase 同时存在 HTTP API 和 Open API 两套接口面，鉴权文档已有页面失效；
   - 官方 C# SDK 很新、依赖面较大；
   - 不用 SDK 就要自己维护协议细节；
   - 只调用文档列出的接口。

## 7. 治理前置条件

按[产品定位文档](../governance/bukit-core-product-positioning.md)和仓库红线（`AGENTS.md`）：

1. **门槛高于"有消费方"**。产品定位写明：
   - Route 2 的投资方向包括 "deterministic builds, Markdown and Notion ingestion"；
   - 新增 Core 能力需要 "a named internal consumer, a concrete business case, an owner, and verifiable acceptance criteria"；
   - "Do not add general-purpose integrations without proven internal demand"；
   - "Prefer controlled simplification over public-surface expansion"。

   Route 2 列出的是投资方向，不是排他清单。但新增一个摄取源属于通用集成，所以方案 A 要拿出已证实的内部需求。
2. **作为独立的 Core 任务确认**：`AGENTS.md` 规定，在具名的网站业务任务中不得修改 Core 自有面；发现的 Core 缺陷只能在一个独立的、用户可见的 Core 任务中、经明确确认后实施。这条规则字面上针对的是缺陷修复。对方案 A 这样的新增 Core 能力，同样作为独立的 Core 任务立项，是对它合理的类推，不是规则原文。
3. **openspec 变更提案**：新增 `cloudbase-content-source` 规格，并写明对下列现有规格的影响：
   - `core-report-contracts`：其中固定了 `source == notion` 的识别逻辑；
   - `media-localization`：域名规则、签名参数；
   - `miniprogram-data`：签名链接拦截、body 规则；
   - `incremental-invalidation`：签名链接报告；
   - `in-process-build-transactions`：缓存与受管写入集合，P2 时涉及。
4. **验证闭包**：用 `python3 scripts/checks/codex-workflow.py closure` 生成。需要跑的有：
   - `Bukit.Config.Tests`（含 `ConfigContractDriftTests`、`ConfigSchemaNestedContractTests`）；
   - `Bukit.Shared.Tests`（含 `BuildDiagnosticCodeTests`）；
   - `Bukit.Content.Tests`、`Bukit.Engine.Tests`、`Bukit.Cli.Tests` 的相关部分；
   - `Bukit.Architecture.Tests`；
   - `config-docs-contract.sh`；
   - public-api-drift（`scripts/checks/public-api-drift.sh`）。

## 8. 与其他路线的关系

| | A. Core 内置 `type: cloudbase` | B. 新建外部 provider 协议，再写 CloudBase 插件 | C. CloudBase 同步插件（推荐的默认路线） | D. 自建宿主注入 provider |
|---|---|---|---|---|
| Core 改动 | 第 3 节所列 | 新协议、宿主、缓存/快照契约，改动最大 | 无 | 无（用 public 的 `BuildAsync(IContentProvider, BuildOptions)`） |
| 治理门槛 | 第 7 节全部 | 同 A，还要做协议设计评审 | 插件自己的成熟度审计 | 只对自建宿主项目负责 |
| 构建确定性 | 默认实时拉取，与 Notion 默认一致；离线要另做快照机制 | 同 A | 同步产物可以提交、可审计，构建离线可复现 | 自行负责 |
| 体验 | 一步 `build` | 一步 `build` | 两步：`pull` 再 `build` | 不能用标准 CLI |
| 不选的原因 | 门槛与成本见上 | 成本最高 | — | 没有 NuGet 分发，只适合自编译宿主；失去 CLI 命令（doctor、publish audit、部署隐私校验）和 site.yaml 配置。构建期的公开产物隐私检查虽然仍会运行，但只检测 Notion 来源，对 CloudBase 起不到保护作用 |

满足下面几条时，A 才优于 C：
- 有具名内部站点必须"构建时直连"，比如内容要秒级更新、流程里不能有同步步骤；
- CloudBase 的重要性接近 Notion；
- 需要跨源 relation 或 `mode: data` 这类同步做不到的能力；
- 第 4 节第 2 项的鉴权问题有可接受的答案。

## 9. 建议路线与工作量（若立项 A）

满足第 7 节的前提、并解决第 10 节第 1 项之后，分阶段推进，每步单独提交。以下行数全部是估算。

**P1 最小可用版**：只支持一种数据形态、只读、只支持 Markdown 正文、不做缓存。默认实时拉取，与 Notion 现状一致；能否进生产，按第 6 节第 4 项的策略决定。

| 组件 | 估算（行） |
|---|---|
| Transport：鉴权、重试、限速（如果能复用官方 SDK 会减少） | 600～800 |
| Provider、字段投影、body store | 500～800 |
| 配置读取、严格校验、schema、语义校验 | 300～400 |
| `cloud://` 换临时链接 | 约 150 |
| 隐私检测与过滤、签名链接识别 | 200～300 |
| 正文链接协议白名单 | 50～100 |
| Doctor 检查 | 150～250 |
| 诊断码 | 约 50 |
| **合计** | **约 2.0k～2.85k**（按 2.9k 计） |

参照的实际行数：Notion transport 634 行，`NotionContentSource` 657 行，两个 doctor 检查器 83 + 107 行，Markdown provider 6 个文件共 1,186 行。

**P2 生产增强**：
- readonly/readwrite 缓存约 400 行（参照 `NotionCacheManager` 304 行、`AtomicNotionCacheWriter` 95 行）；
- 离线快照的工作量待设计；参照 Notion 的同类机制约 1.5k 行（`NotionSnapshotSession` 880、`NotionPin` 356、`NotionSnapshotOptions` 97、`NotionSnapshot` 85、`NotionSnapshotScope` 54、`NotionSnapshotException` 29）；
- 同时做 3.3 节的改动。

**P3 按需再做**：
- HTML 正文：新写白名单清洗器，300～500 行加测试；
- 关联字段；
- miniprogram 的 body 规则；
- 基于更新时间字段的增量失效。

**协同点**：Core 已经有 miniprogram-data 内置功能（见 `openspec/specs/miniprogram-data/spec.md`），而 CloudBase 本身就是小程序的后端。如果内部消费方的场景是"小程序和 CloudBase 写内容，同时发布网站和小程序数据集"，A 的业务理由会比一般的通用集成强得多。立项时可以把这一点作为核心论据。

## 10. 待核实项（按优先级）

1. **阻塞**：
   - 内部消费方的数据在文档型数据库还是数据模型；如果是数据模型，底层是哪种数据库，能否走文档数据库接口；
   - 对应 API 面上可用的鉴权方式；
   - 签名 V3 加 CAM，或者用户登录 token 加数据权限，能否真正收窄到只读；
   - clientCredential token 的权限、有效期和刷新方式（官方页面已失效）。
2. 官方 C# SDK 是否兼容 Native AOT 和裁剪；它的 Microsoft.Extensions 依赖能否接受。依赖清单已从 NuGet 核实。
3. 选定 API 面上查询接口的准确路径、分页参数和过滤语法。
4. CloudBase 临时链接的域名和签名参数名，用于 `SignedUrlIdentity` 规则，也决定 `MiniProgramJson.IsSignedUrl` 能否兜底。
5. 私有文件临时链接 `maxAge` 的单位、默认值和上限。
6. Markdig 对 `javascript:` 等协议的默认行为。程序集里没有找到相关字符串，旁证是不过滤，仍需用测试确认。
7. CI runner 访问网关的延迟、限流和配额消耗；环境地域与数据驻留要求。
