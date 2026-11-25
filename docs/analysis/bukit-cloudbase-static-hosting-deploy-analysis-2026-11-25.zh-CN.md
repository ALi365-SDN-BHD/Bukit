# Bukit 部署接入 CloudBase 静态网站托管：分析

- 日期：2026-11-25
- 状态：分析，未立项，未实施
- 范围：评估把 Bukit 构建产物部署到腾讯 CloudBase（云开发）静态网站托管的接入方式；不改任何代码
- 标注约定：
  - 经代码或官方文档核对的事实，注明出处或标“已核实”；
  - “经验判断”是未经本次核对的常识性判断；
  - “待核实”是尚未确认的信息。
  - 代码陈述以提交 `2b7353d1` 时的仓库内容为准。

## 1. 结论

**推荐分两步：**

1. **2.x 不改 Core。** 用 CI 编排 `bukit build` 和 `tcb hosting deploy`，需要时补一个示例 workflow 和一段文档。
2. **3.0 原生接入。** 把 CloudBase 静态托管做成一个 `Deployer` 扩展（`cloudbase-hosting`），实现 plan / apply / status，用 BCL 直连 CloudBase 管理 API 和 COS。

不建议在 2.x Core 里加一个调用 tcb CLI 的内置 provider，原因：

- 需要改动的 Core 契约多（见 2.2 节）；
- tcb 本身不清理旧文件，最关键的问题补不上（见 3 节“覆盖语义”）；
- 3.0 会按产物清单重做部署层（见 [3.0 架构设计](../plans/2026-11-24-bukit-3.0-architecture-design.zh-CN.md) “部署平台”一节）。

## 2. Bukit 部署现状（已核实）

### 2.1 只有 GitHub Pages，且写死

- [DeployCommand.cs](../../src/Bukit-Core/Bukit.Cli/Commands/DeployCommand.cs) 直接 `new GitHubPagesDeployProvider()`，不按 `deploy.provider` 选择 provider；`--dry-run` 的输出也写死为 “GitHub Pages”。
- [04 site.yaml 配置](../../guide/user/04-site-yaml-config.md) 写明 `deploy.provider` 只支持 `github-pages`。

### 2.2 配置和公开契约按 GitHub 设计

- [DeployConfig.cs](../../src/Bukit-Core/Bukit.Config/DeployConfig.cs) 与 `ConfigStrictFieldValidator.DeployKeys` 只接受 `provider`、`branch`、`message`、`repo`、`cname`、`keepHistory`。
- [IDeployProvider.cs](../../src/Bukit-Core/Bukit.Cli/Deploy/IDeployProvider.cs) 的 `DeployContext` 是 public，带 `Branch`、`Cname` 等 git 字段；增改字段会触发 public-api-drift。
- 因此 2.x 内置 provider 至少要改：provider 分发、`DeployConfig` 与 JSON schema、strict 字段校验、`DeployContext` 或其内部扩展、用户文档与参数速查。按 AGENTS.md，这些都属于 Core 范围，需单独立 Core 任务并经确认。

### 2.3 可复用的部分

| 能力 | 位置 | 说明 |
|---|---|---|
| 构建完成校验 | [DeploymentReadinessValidator.cs](../../src/Bukit-Core/Bukit.Cli/Deploy/DeploymentReadinessValidator.cs) | 只接受 `.bukit-build-state.json` 状态为 `completed` 的产物 |
| 部署期隐私扫描 | [DeploymentPrivacyValidator.cs](../../src/Bukit-Core/Bukit.Cli/Deploy/DeploymentPrivacyValidator.cs) | 对暂存目录扫描 Notion 标识、CloudBase 网关/存储主机、`cloud://{envId}.` 与带 token 的签名链接 |
| 内部文件排除 | [github-pages-workflow.yml](../../examples/github-pages-workflow.yml) | 排除所有 `.bukit/` 目录及 `.bukit-output-marker`、`.bukit-build-state.json`，与 `GitHubPagesDeployProvider` 一致 |
| 404 页面 | `RenderEntry`、`StaticFileService` | Bukit 产出根目录 `404.html` |

## 3. CloudBase 静态托管与 Bukit 产物的匹配

来源：[静态网站托管文档](https://cloud.tencent.com/document/product/876/47141)、[Manager Node hosting API](https://docs.cloudbase.net/api-reference/manager/node/hosting)、CloudBase CLI 插件随附的 hosting / access / core 参考。

| 项 | 平台行为 | 对 Bukit 的影响 |
|---|---|---|
| 存储与分发 | 文档写明由 COS 和 CDN 支撑 | 本质是 COS 静态网站 + CDN，行为可参照 [COS 静态网站](https://cloud.tencent.com/document/product/436/6249) |
| 目录索引 | 索引文档默认 `index.html`；访问根目录或子目录返回该目录的索引文档 | `/{slug}/` 形式的 permalink 可直接使用。`/foo`（无尾斜杠）是否跳转到 `/foo/`：**待核实** |
| 错误页 | `setWebsiteDocument({ indexDocument, errorDocument, routingRules })` 可设错误文档 | 部署时把 `errorDocument` 设为 `404.html`。共享桶环境（`ExternalStorage`）调用会抛 `INVALID_OPERATION`，需要降级为提示 |
| 重定向 | 仅支持 4xx 错误码重定向和前缀匹配重定向；优先级高于索引文档；条数上限未说明 | Bukit 的默认语言首页跳转页是纯静态页面，不依赖重定向规则，可直接使用 |
| 覆盖语义 | `tcb hosting deploy` 覆盖同路径文件，不删除旧文件，也没有版本 | **最大风险。** 删除或改名的页面会继续可访问并被收录。“先全删再部署”会产生空窗期。正确做法是先上传、后删除清单外文件。Manager Node 的 `uploadFiles` 有 `prune` 参数，tcb CLI 文档未提供对应能力 |
| CDN 缓存 | 文档只说明控制台上传会自动刷新节点缓存；CLI/API 上传是否刷新未说明；删除后旧文件可能仍可访问 5–10 分钟 | HTML 应配置短 TTL，或在部署后主动刷新。自定义域名可用 `tcb domains cache purge`。CLI/API 上传后的刷新行为：**待核实** |
| 域名与 HTTPS | 内置 HTTP/HTTPS；自定义域名需提供证书 `certId`，通过控制台或 `tcb domains add` 绑定 | 不需要 `CNAME`、`.nojekyll` 文件；`deploy.cname` 在 CloudBase 下没有意义。国内站自定义域名一般需要 ICP 备案（经验判断） |
| 默认域名 | SDK 从 `listEnvs()` 的 `StaticStorages[0].StaticDomain` 读取 | 格式与限制（下载头、限频等）：**待核实** |
| 子路径 | 支持部署到 `cloudPath` 子目录 | 部署到子目录时，`--base-url` 必须与之一致 |
| 安全配置 | 防盗链、IP 黑白名单、IP 限频（超限返回 514） | 与 Bukit 无冲突，属运维配置 |
| 文件限制 | 单文件大小、文件数量、dotfile 处理、按文件 Content-Type：文档未说明 | **待核实**。Bukit 产出的 `.bukit*` 内部文件本来就要排除 |
| 凭据 | CI 用腾讯云 SecretId/SecretKey（建议临时 token）或环境 API Key 登录 tcb | 与内容源的 `CLOUDBASE_PUBLISHABLE_KEY` 是两回事，权限高得多，需要单独的 CI secret，且不得写入 `site.yaml` |

### 3.1 隐私检查与默认域名（已核实）

- 构建期 [PublicOutputPrivacyCheck.cs](../../src/Bukit-Core/Bukit.Engine.Audit/PublicOutputPrivacyCheck.cs) 的 `AddCloudBaseTokens` 只把 `cloud://`、`{envId}` 加网关/存储主机后缀、`.storage.tcloudbase.com` 视为敏感；部署期 `DeploymentPrivacyValidator.ContainsCloudBaseIdentifier` 同样只匹配这些后缀和 `cloud://{envId}.`。两者都不拦截裸 envId。
- 默认托管域名多半包含 envId（格式待核实）。用它作 `site.url` 不会被拦截，但 envId 会公开出现在 canonical、sitemap、feed 等产物中。
- 是否接受由站点所有者决定；要隐藏 envId，需使用自定义域名。

## 4. 接入方案

### 方案 A：CI 编排，不改 Core（推荐，2.x）

步骤：

1. `bukit build --config site.yaml --output _site --base-url <path> --site-url <url> --ci --clean`
2. 按 GitHub Pages 示例删除 `.bukit/`、`.bukit-output-marker`、`.bukit-build-state.json`
3. `tcb login --apiKeyId $SECRET_ID --apiKey $SECRET_KEY [--token $SESSION_TOKEN]`（国际站加 `TCB_IS_INTL=true`）
4. `tcb hosting deploy _site --env-id $ENV_ID --json`
5. `tcb hosting list --env-id $ENV_ID --json`，对比本地文件列表，再用 `tcb hosting delete <path> --env-id $ENV_ID --yes` 删除清单外文件
6. 自定义域名时执行 CDN 刷新

优点：零 Core 改动，立即可用。

缺点：

- 只有构建期隐私检查，没有部署期暂存目录的二次扫描；
- 清理旧文件要靠脚本对比，易出错；
- 依赖 Node 和 tcb CLI；
- CDN 刷新行为未知。

### 方案 B：2.x Core 内置 provider，调用 tcb

- 做法与 `GitHubPagesDeployProvider` 调用 git 类似：暂存目录 → 排除内部文件 → `DeploymentPrivacyValidator` → 调用 tcb。
- 收益：能复用就绪校验和部署期隐私扫描。
- 代价：2.2 节列出的全部 Core 契约改动；引入 Node/tcb 运行时依赖；凭据要经 tcb 的配置文件或参数传递，需要额外的泄露防护；清理旧文件仍需自己实现。
- 结论：性价比低，不推荐。

### 方案 C：3.0 原生 `Deployer`（推荐，长期）

与 3.0 设计“输入是产物清单（路径和内容 hash），实现计划、执行、查询状态”的约定一致：

| 操作 | 内容 |
|---|---|
| plan | 产物清单对比远端文件列表：内容不同的上传，清单外的删除；输出变更计划，不写远端 |
| apply | 先上传新增和变化的文件，再删除清单外文件，再设置索引/错误文档，最后刷新 CDN；中途失败可按计划重试 |
| status | 返回远端文件与清单的一致性和最近一次部署结果 |

实现要点：

- 沿用 `Bukit.CloudBase` 只依赖 BCL 的做法，Native AOT 友好，不依赖 Node；
- 部署前强制执行就绪校验和隐私扫描，与 GitHub Pages 目标共用；
- 凭据只从环境变量或 `--secrets-stdin` 读取，错误信息不回显凭据、带签名的 URL 或响应正文（沿用 3.0 继承边界 C3、C8）；
- 对 ETag 与清单 hash 的对应关系做验证：COS 简单上传的 ETag 通常是 MD5（经验判断），分块上传不是，plan 不能只靠 ETag。

风险（均为**待核实**，需先做技术验证）：

- 底层管理 API 名称与签名方式（TC3-HMAC-SHA256），以及获取托管桶和临时凭据的接口；
- COS 请求签名与上传、删除、列举接口的对接；
- CDN 刷新接口与配额；
- 共享桶环境下的 BasePath 与 `setWebsiteDocument` 限制。

## 5. 待核实清单

1. 默认托管域名格式、是否含 envId、有无访问限制。
2. `/foo`（无尾斜杠）的行为：跳转、返回 404 还是返回索引文档。
3. `tcb hosting deploy` 与 Manager API 上传后是否自动刷新 CDN。
4. 单文件大小、文件总数、dotfile 处理、Content-Type 推断规则。
5. 底层托管 API 名称、临时凭据获取方式、COS 桶与地域的获取方式。
6. 重定向规则条数上限。
7. 计费与免费额度（[价格文档](https://cloud.tencent.com/document/product/876/127357) 未读取）。

## 6. 下一步建议

- 2.x：如需立即上线，补 `examples/cloudbase-hosting-workflow.yml`（方案 A）和用户文档中的一节，不改 Core。
- 3.0：在部署阶段的设计中把 `cloudbase-hosting` 列为第二个 `Deployer`，先完成第 5 节的技术验证，再定接口。
