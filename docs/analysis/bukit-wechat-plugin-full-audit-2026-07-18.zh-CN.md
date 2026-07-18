# Bukit WeChat 插件全方位深度审计报告

> 审计日期：2026-07-18
>
> 唯一代码基线：`main` / `4103959c9f7ee1b8dfe8db7e34340f4495e7a9ce`
>
> 审计模式：安全只读；除本报告外未修改源码、测试或配置；未读取真实微信密钥，未创建草稿，未提交发布
>
> 审计范围：`Bukit.Plugin.WechatSync`、`Bukit.WechatSyncing` 及其直接依赖的 CLI、PluginHost、协议、权限、路径、SSRF、测试、文档与发行包边界
>
> 排除范围：无关 Core 业务功能、所有 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/` 备份目录

## 1. 执行摘要

本次审计确认：WeChat 插件的基础进程协议、路径约束、符号链接防逃逸、图片下载 SSRF 防护、环境变量隔离、专项测试、Release 编译、NuGet 漏洞检查以及 macOS arm64 自包含/Native AOT 可行性均有有效证据；但插件当前不能判定为可安全发布或正式发行就绪。

正式问题总计 **19 项**：

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现能够直接证明的灾难性缺陷 |
| P1 | 6 | 重复草稿/发布、一致性丢失、图片内容损坏、官方 API 契约漂移、审核门禁缺失、正式包缺失 |
| P2 | 8 | 永久错误重复副作用、`figure` 内容丢失、媒体键碰撞、Unicode 损坏、dry-run 误报、错误体泄漏、异常状态机、静默图片降级 |
| P3 | 5 | 输入资源上限、参数上限、连接复用、远程资源新鲜度、测试/文档/manifest 治理缺口 |

五个独立维度的最终判断：

| 维度 | 结论 | 关键原因 |
|---|---|---|
| 功能正确性 | **不通过** | 懒加载图片源被提前删除；`figure` 内容丢失；标题、作者、图片大小与当前官方契约不一致 |
| 安全性 | **有条件通过，仍需修复** | SSRF、路径和环境隔离有证据；但审核门禁、原始错误体边界、资源上限和静态过度授权仍不满足安全发布要求 |
| 运行可靠性 | **不通过** | 发布不确定状态不持久化；缓存非原子且无并发控制；永久错误和有副作用步骤被统一重试 |
| 协议适配 | **基本通过** | `handshake / manifest / invoke --dry-run` 经真实 AOT 包冒烟通过；但 dry-run 候选计数错误，插件级权限无法表达只读 dry-run |
| 发行就绪度 | **不通过** | 仓库只有 fixture manifest、零 SHA、单 RID；没有根 manifest、真实产物、多 RID 或发行流水线 |

“65 个 WeChat 测试全部通过”只证明现有断言通过，不能覆盖上述状态机、HTML、当前微信契约和正式包缺口。

## 2. 方法、边界与证据等级

### 2.1 审计方法

1. 从 CLI 调度追到 WeChat HTTP 边界，逐层建立代码调用链。
2. 交叉比对程序化 manifest、示例 `plugin.yaml`、`.bukit/plugins.yaml`、协议 DTO、权限、CLI 参数和默认值。
3. 审阅输入、28 步 HTML 转换、图片、缓存、重试、草稿、发布轮询和错误映射。
4. 用 `/tmp` 下临时 .NET 工具直接调用当前 Release 程序集，构造最小反例；临时工具没有进入仓库。
5. 获取 2026-07-18 当前微信服务号官方文档；没有使用第三方文章作为契约依据。
6. 构建临时真实 SHA256 插件包，验证 Host 安装链和只读协议；没有调用微信写接口。

### 2.2 证据等级

| 置信度 | 定义 |
|---|---|
| 高 | 当前 HEAD 源码直接证明，且有测试、临时反例或当前官方契约交叉证明 |
| 中 | 当前 HEAD 可证明局部行为，但最终平台行为受微信服务端、账户能力或输入来源影响 |
| 低 | 只有风险信号，尚无安全可控复现；不会升级成正式缺陷 |

本报告没有把“正则很多”“图片可能很大”“微信可能改变行为”这类单纯推测升级为缺陷。像素解压炸弹和真实账号发布行为仅列为未验证边界。

### 2.3 当前官方契约来源

- [新增草稿](https://developers.weixin.qq.com/doc/service/api/draftbox/draftmanage/api_draft_add)
- [上传发表内容中的图片](https://developers.weixin.qq.com/doc/service/api/material/permanent/api_uploadimage)
- [上传永久素材](https://developers.weixin.qq.com/doc/service/api/material/permanent/api_addmaterial)
- [发布草稿](https://developers.weixin.qq.com/doc/service/api/public/api_freepublish_submit)
- [发布状态查询](https://developers.weixin.qq.com/doc/service/api/public/api_freepublish_get)
- [获取接口调用凭据](https://developers.weixin.qq.com/doc/service/api/base/api_getaccesstoken)
- [发布能力总览](https://developers.weixin.qq.com/doc/service/guide/product/publish.html)

官方页面在审计日确认：`uploadimg` 只接受 JPG/PNG 且必须小于 1 MB；草稿标题不超过 32 字、作者不超过 16 字、正文少于 20,000 字符且小于 1 MB、原文链接不超过 1 KB；发布状态为 `0` 成功、`1` 发布中、`2..6` 为不同失败或成功后失效状态。发布总览还注明，自 2025 年 7 月起部分主体已失去相关接口能力。

## 3. 真实调用链与契约矩阵

```text
Bukit CLI
  -> PluginCliLoader（配置、静态 manifest、RID、SHA256、权限）
  -> PluginProtocolClient / SystemProcessRunner（清空环境、握手、运行时 manifest）
  -> bukit-plugin-wechat-sync
  -> WechatSyncPluginApp
  -> WechatSyncPluginOptionsMapper
  -> WechatSyncInputLoader
  -> WechatSyncWorkflow
       -> ContentProcessor（28 步 HTML 转换）
       -> ThumbResolver / ContentImageProcessor / ImageConverter
       -> SyncCacheManager
       -> WechatDraftGateway
            -> token
            -> material/add_material
            -> media/uploadimg
            -> draft/add
            -> freepublish/submit
            -> freepublish/get
```

关键代码证据：

- `src/Bukit-Core/Bukit.Cli/Cli/PluginCliLoader.cs:121-203`：静态清单、权限、RID、入口、SHA、握手、运行时命令加载。
- `src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs:83-116`：清空继承环境，只注入授权环境变量。
- `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs:45-171`：握手、运行时 manifest 与 invoke。
- `src/Bukit-Plugins/Bukit.Plugin.WechatSync/WechatSyncPluginOptionsMapper.cs:22-110`：上下文、权限、默认参数和目标模式。
- `src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncInputLoader.cs:19-125`：公开构建产物到同步上下文。
- `src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncWorkflow.cs:31-135`：筛选、缓存、同步、发布和最终状态。
- `src/Bukit-Plugins/Bukit.WechatSyncing/WechatDraftGateway.cs:74-425`：微信 API 网关。

### 3.1 manifest / CLI / 权限对照

| 面 | 程序化契约 | 示例静态契约 | 审计结论 |
|---|---|---|---|
| 插件 ID / 版本 / 协议 | `wechat-sync` / `0.2.0` / `bukit-plugin-v1` | 一致 | 通过 |
| 命令 | `wechat-sync sync`，28 个选项 | 一致 | 通过 |
| 默认目标 | `draft` | manifest 只列 allowed values | 可接受，但文档不足 |
| 文件读 | `.`, `dist`, `public`, `output` | 一致 | 范围偏大但与任意输出路径设计一致 |
| 文件写 | cache + plugin report | 一致 | report 是 Host 所有，插件本身不需要；见 WX-P2-08 |
| 网络 | 插件级 `true` | 一致 | dry-run 也必须在安装配置中授权；见 WX-P2-08 |
| 环境 | App ID、Secret、force | 一致 | dry-run 仍需静态授权；运行时不会读取真实值 |
| RID / SHA | 无正式根 manifest | fixture 仅 `osx-arm64` + 全零 SHA | 不通过；见 WX-P1-06 |

## 4. 正式缺陷

### WX-P1-01：发布失败、超时或进程中断后会重复创建草稿并重复提交发布

- **严重度 / 置信度**：P1 / 高
- **代码证据**：`WechatSyncWorkflow.cs:80-105` 先创建草稿，再提交并轮询发布；只有确认成功后才写 `cache.Records`。`WechatSyncWorkflow.cs:222-264` 把失败、超时和异常折叠成 `false`。
- **触发条件**：`--target publish` 下，`freepublish/submit` 已接受，随后状态为失败、轮询超时、网络断开或进程被终止。
- **用户影响**：重跑同一内容会再创建草稿，并可能再次提交发布；真实发布已成功但客户端未观察到成功时，风险扩大为重复发布。
- **根因**：缓存只有“最终成功”状态，没有 `draft-created`、`publish-submitted`、`publish-id`、`unknown` 等中间状态，也没有恢复/对账流程。
- **最小复现**：临时 fake gateway 连续两次返回发布状态 `3`，当前 HEAD 得到 `adds:2; publishes:2; synced:0,0`。
- **现有测试为何未发现**：`WechatSyncWorkflowTests.cs:489-532` 只断言失败不写成功缓存，没有执行第二次运行。
- **最小修复边界**：新增持久化发布状态机；草稿创建后立即原子保存 draft ID，提交后保存 publish ID；重跑先查询/恢复，不直接新建。
- **回归测试**：失败后重跑、submit 成功后断网、轮询超时后重跑、状态成功响应丢失、进程在每个写点终止。
- **兼容与跨模块风险**：需要缓存格式迁移；必须版本化并保留 v2 读取，作为独立实现任务。

### WX-P1-02：缓存非原子、无锁、无合并；并发或损坏会丢失幂等状态

- **严重度 / 置信度**：P1 / 高
- **代码证据**：`SyncCache.cs:45-74` 解析异常直接重置空缓存；`SyncCache.cs:77-85` 使用 `File.WriteAllText` 覆盖目标文件，没有临时文件、`fsync`、原子替换、进程锁或版本冲突检测。
- **触发条件**：两个 CLI 同时运行；写入时进程终止；磁盘短写；用户或工具留下半截 JSON。
- **用户影响**：成功记录或缩略图缓存丢失，后续重复创建草稿、重复上传素材、重复发布；并发运行最后写入者覆盖另一进程结果。
- **根因**：把缓存当单进程普通 JSON，而它实际承担跨进程幂等日志职责。
- **最小复现**：32 个并发任务各写一条独立记录，全部调用成功，但最终缓存只有 1 条记录。
- **现有测试为何未发现**：覆盖了损坏缓存重置和部分缓存保存，但没有并发、原子替换、崩溃恢复或合并测试。
- **最小修复边界**：同目录临时文件 + flush + 原子 rename；项目级互斥锁；保存前重读并按 sync key 合并；损坏文件隔离为 `.corrupt-*`，禁止静默当空缓存继续发布。
- **回归测试**：并发两进程、故障注入到每个写入阶段、损坏缓存、旧版本迁移、锁超时。
- **兼容与跨模块风险**：缓存语义变化，需要明确锁等待和损坏恢复策略。

### WX-P1-03：28 步 HTML 转换提前删除懒加载真实图片源，随后图片上传器无法恢复

- **严重度 / 置信度**：P1 / 高
- **代码证据**：`ContentProcessor.cs:603-637` 第 28 步调用 `CleanLazyLoadAttributes`；`ContentProcessor.cs:185-201` 删除 `data-src`、`data-original`、`data-actualsrc`、`data-lazy-src`、`srcset`。工作流到 `WechatSyncWorkflow.cs:162-170` 才调用图片处理器，而后者在 `ContentImageProcessor.cs:183-225` 明确依赖这些属性优先解析真实 URL。
- **触发条件**：常见的 `<img src="data:占位图" data-src="真实地址">` 或只有 `srcset` 的 HTML，且开启 `--process-images`。
- **用户影响**：真实图片不会上传，正文保留 data 占位图或缺图；同步仍可能被计为成功。
- **根因**：转换顺序违反了图片解析器自己的输入契约。
- **最小复现**：临时工具输入 data 占位图 + `data-src=https://cdn.example/real.jpg`；处理后只剩 data `src`，`ResolveBestImageUrl` 返回 `null`。
- **现有测试为何未发现**：没有直接的 `ContentProcessor` 测试，也没有覆盖“完整 28 步转换后再处理图片”的集成断言。
- **最小修复边界**：先解析/上传图片，再清理懒加载属性；或让清理步骤把最佳候选写回 `src` 后再删除属性。
- **回归测试**：单双引号、无引号、data 占位、`srcset`、畸形标签、多图、上传失败降级。
- **兼容与跨模块风险**：会改变最终 HTML 和内容 hash；修复上线后旧缓存应失效或提升 hash 版本。

### WX-P1-04：当前微信草稿与正文图片契约已漂移，合法输入会被本地错误放行并被服务端拒绝

- **严重度 / 置信度**：P1 / 高
- **代码证据**：`Helpers.cs:218-242` 标题上限为 64 个 UTF-16 单元；`WechatSyncWorkflow.cs:267-290` 不限制作者和 1 KB 原文链接；`WechatSyncWorkflow.cs:175-188` 对正文超限只告警仍发送；`ImageConverter.cs:14-22` 和 `WechatDraftGateway.cs:246-262` 把 `uploadimg` 上限写成 2 MB。
- **当前官方契约**：标题 32 字、作者 16 字、正文少于 20,000 字符且小于 1 MB、原文地址不超过 1 KB；`uploadimg` JPG/PNG 必须小于 1 MB。
- **触发条件**：33-64 字标题、超过 16 字作者、超过 1 KB 原文链接、1-2 MB JPEG/PNG，或超限正文。
- **用户影响**：确定性 API 拒绝；再叠加统一重试会重复耗时和素材副作用。用户看到的是最终笼统失败，不是本地可操作校验。
- **根因**：限值硬编码且没有基于当前官方契约的 golden test；“告警”被误用为强约束。
- **最小复现**：源码常量与审计日官方页面直接冲突；1-2 MB 已支持格式在 `NormalizeForUpload` 中可原样通过。
- **现有测试为何未发现**：测试围绕实现常量编写，没有把官方契约作为独立数据源；没有 32/16/1 MB 边界测试。
- **最小修复边界**：集中化契约常量；按 Unicode text element/官方计数规则校验；超限在任何网络调用前失败；图片目标应严格 `< 1 MB` 并留 multipart/平台余量。
- **回归测试**：边界前后、中文、emoji、组合字符、UTF-8 字节、作者、URL、正文、1 MB 图片。
- **兼容与跨模块风险**：更严格校验可能使过去“尝试发送”的输入本地失败；应给稳定诊断码和迁移说明。

### WX-P1-05：公开投影携带审核状态，但插件不建立可发布审核门禁

- **严重度 / 置信度**：P1 / 高
- **代码证据**：Core 在 `ContentProjectionWriter.cs:104-126` 把 `reviewStatus` 写入 agent manifest；JSON 还包含 `expiresAt`、`syncStatus`、`reviewStatus`。插件在 `WechatSyncInputLoader.cs:82-115` 丢弃 `reviewStatus`、`expiresAt`，只把 `syncStatus` 放入 metadata；`WechatSyncWorkflow.cs:293-328` 仅按 source 和 type 筛选。
- **触发条件**：身份状态仍为 published/indexable，但信任审核状态为 `draft`、`needs-review` 等；或外部/手工构建产物篡改状态。
- **用户影响**：未审核内容可以进入草稿，使用 `--target publish` 时可能直接提交发布。
- **根因**：把“公开可索引”错误等同为“允许同步/发布”，没有显式 allowlist 和二次防线。
- **最小复现**：agent manifest/内容 JSON 设 `reviewStatus=draft`；加载器照常构造 item，工作流没有任何状态判断。
- **现有测试为何未发现**：fixture 全部使用 `approved`，没有状态矩阵；现有 Core 过滤测试只证明 indexability，不证明微信发布策略。
- **最小修复边界**：默认只允许明确批准状态；为草稿同步与直接发布分别定义 allowlist；`--force` 不得绕过审核门禁。
- **回归测试**：所有 schema review status、过期、sync status、篡改 manifest、draft 与 publish 两种目标。
- **兼容与跨模块风险**：属于发布策略契约，应新增显式 CLI/config 选项并给安全默认值；不要隐式改变 Core。

### WX-P1-06：仓库中的 WeChat 插件不是可安装的正式发行包

- **严重度 / 置信度**：P1 / 高（发行就绪维度）
- **代码证据**：包根没有 `src/Bukit-Plugins/Bukit.Plugin.WechatSync/plugin.yaml`；唯一 manifest 位于 example fixture，`examples/minimal/README.md:1-9` 明确声明不可运行、SHA 为占位值；manifest 只有 `osx-arm64` 和 64 个零。`PluginSchemaContractTests.cs:89-99` 对根 manifest 缺失直接 return。
- **触发条件**：用户按正式插件包交付或 PluginCliLoader 安装当前仓库目录。
- **用户影响**：无法通过真实 SHA/RID 校验并执行；Linux/Windows 没有入口声明。
- **根因**：可行性工程与发行产物治理尚未闭环，fixture 测试被误认为包验证。
- **最小复现**：检查包根；不存在正式 manifest/二进制。审计临时组装真实 AOT 包后可通过，反证问题在发行资产而非技术不可行。
- **现有测试为何未发现**：根清单测试是条件式；example 测试只验证 schema，不验证真实 hash、入口和执行。
- **最小修复边界**：独立发行任务生成各 RID 自包含产物、真实 SHA、根 manifest/SBOM/checksum；安装后执行 Host 协议冒烟。
- **回归测试**：每个支持 RID 的 SHA、可执行位、握手、manifest、dry-run、损坏 hash、缺文件、平台不匹配。
- **兼容与跨模块风险**：新增正式发行承诺前必须明确支持 RID；不应把本次临时 osx-arm64 产物提交为正式包。

### WX-P2-01：永久错误与有副作用的完整同步链被无差别重试

- **严重度 / 置信度**：P2 / 高
- **代码证据**：`WechatSyncWorkflow.cs:138-207` 的一个 retry 包围缩略图上传、HTML、正文图上传和草稿创建，并捕获除取消外的所有异常。
- **触发条件**：`48001 api unauthorized`、参数超限、无效媒体、审核拒绝等永久错误，或草稿创建成功但响应丢失。
- **用户影响**：永久错误重复 3 次；响应不确定时可能重复素材或草稿；延迟和配额消耗扩大。
- **根因**：没有错误分类、幂等边界和步骤级恢复 token。
- **最小复现**：fake gateway 每次抛 `WechatApiException(48001)`，当前实现调用 `AddDraftAsync` 3 次。
- **现有测试为何未发现**：测试只断言“会重试”，没有区分永久/瞬态，也没有响应丢失后的服务端副作用。
- **最小修复边界**：按 HTTP、微信 errcode 和步骤分类；只重试明确瞬态、尚未产生不可确认副作用的操作。
- **回归测试**：401/429/5xx/超时/非 JSON/40005/40009/48001/53503-53505，以及“服务器成功、客户端超时”。
- **兼容与跨模块风险**：重试策略属于行为契约，需要稳定诊断和可配置上限。

### WX-P2-02：`figure` 转换会删除第二张图片和非 caption 内容

- **严重度 / 置信度**：P2 / 高
- **代码证据**：`ContentProcessor.cs:45-79` 只取第一个 `<img>` 和第一个 `<figcaption>`，重建后丢弃 figure 中其余节点。
- **触发条件**：一个 figure 包含多图、署名、链接、版权文字或其他内联内容。
- **用户影响**：正文静默丢图/丢文字，且同步可能成功并进入缓存。
- **根因**：正则重建不是保序 DOM 转换。
- **最小复现**：输入含 `one.jpg`、`<strong>kept?</strong>`、`two.jpg` 和 caption；输出只剩 `one.jpg` 与 caption。
- **现有测试为何未发现**：没有 `ContentProcessor` 单测或保真 corpus。
- **最小修复边界**：使用 HTML parser，保留全部子节点，只转换容器语义。
- **回归测试**：多图、嵌套 figure、无 caption、畸形闭合、属性单双引号。
- **兼容与跨模块风险**：输出 HTML/hash 会变化，需要缓存版本处理。

### WX-P2-03：媒体 URL 键丢弃 query，且正文图片去重忽略大小写，可复用错误图片

- **严重度 / 置信度**：P2 / 高
- **代码证据**：`Helpers.cs:371-395` 只保留 scheme/host/path，完全丢弃 query/fragment；`ContentImageProcessor.cs:48-50` 用 `OrdinalIgnoreCase` 去重完整 URL。Core 的媒体本地化当前使用同类规范化，属于跨模块共同风险。
- **触发条件**：`image?id=one` 与 `image?id=two`；或大小写敏感 CDN 路径/query。
- **用户影响**：媒体缓存、缩略图或正文图片命中另一资源，发布错误图片；hash 也可能未感知变化。
- **根因**：把签名 query 误当跟踪参数，并假设 URL 全部大小写不敏感。
- **最小复现**：两条不同 query URL 均规范化为 `https://cdn.example/image`。
- **现有测试为何未发现**：没有 query/case 差异矩阵；测试只覆盖普通 URL。
- **最小修复边界**：保留 query 或仅删除明确 allowlist 的跟踪参数；路径/query 使用 ordinal；host/scheme 才忽略大小写。
- **回归测试**：签名 query、重复参数、顺序、大小写路径、默认端口、percent encoding。
- **兼容与跨模块风险**：需要同时协调 Core 媒体索引键与插件缓存迁移，不能只修一侧。

### WX-P2-04：字符串截断按 UTF-16 单元切割，可产生非法 Unicode

- **严重度 / 置信度**：P2 / 高
- **代码证据**：`Helpers.cs:234-242` 直接使用 range substring；标题和摘要在 `WechatSyncWorkflow.cs:273-280` 调用它。
- **触发条件**：截断边界落在 emoji/补充平面字符的 surrogate pair 中间。
- **用户影响**：生成孤立高代理项；序列化可能替换为 U+FFFD，造成标题/摘要损坏或 API 拒绝。
- **根因**：把 UTF-16 code unit 当成用户可见字符/官方“字”。
- **最小复现**：60 个 `a` + `😀tail`，max=64；输出第 61 位为孤立 `D83D` 后接 `...`。
- **现有测试为何未发现**：没有 emoji、组合字符、CJK 扩展字符边界测试。
- **最小修复边界**：使用 Rune/text element，并按官方计数和 UTF-8 字节双重校验。
- **回归测试**：emoji、ZWJ、变体选择符、组合音标、CJK 扩展。
- **兼容与跨模块风险**：会改变截断结果和 hash。

### WX-P2-05：dry-run 报告加载总数而非实际筛选候选数

- **严重度 / 置信度**：P2 / 高
- **代码证据**：`WechatSyncPluginResponseMapper.cs:9-22` 直接输出 `context.Routed.Count`；真实筛选只在 `WechatSyncWorkflow.cs:293-328` 中执行，而 dry-run 不调用工作流。
- **触发条件**：指定 `--source-names`、`--content-types`、缺失类型 fallback，或存在 `sourceMode=data`。
- **用户影响**：发布前预览数量错误，用户可能基于错误候选数执行真实同步。
- **根因**：dry-run 只完成输入加载，没有复用生产筛选/规划阶段。
- **最小复现**：两个 routed item，过滤只允许一个；dry-run 仍报告 2。
- **现有测试为何未发现**：兼容测试只有一个 item 且无筛选。
- **最小修复边界**：抽取无副作用 planning API，由 dry-run 与真实运行共用并返回逐项原因。
- **回归测试**：所有筛选组合、0/1/N、缺失字段、重复 key。
- **兼容与跨模块风险**：只修正输出语义，不需改协议 DTO。

### WX-P2-06：微信原始错误体无统一大小/脱敏边界，CLI 直接打印未掩码异常

- **严重度 / 置信度**：P2 / 高（敏感内容是否由上游回显为中置信度）
- **代码证据**：`WechatDraftGateway.cs:119-142,204-223,288-307,330-359,379-424,540-570` 对 API 响应使用无上限 `ReadAsStringAsync`；同文件 `WechatDraftGateway.cs:707-741` 的 `WechatApiException` 把完整 raw JSON 拼入异常。`WechatSyncPluginResponseMapper.cs:58-68` 原样放入 diagnostic；`PluginCommandInvoker.cs:38-64` 直接打印。执行报告在 `PluginExecutionReporter.cs:69-88` 会掩码，但 CLI 直出路径不会。
- **触发条件**：代理/服务端返回超大、含换行/控制符、回显请求或敏感业务数据的错误体。
- **用户影响**：内存放大、日志注入、终端/CI 日志泄漏微信响应；若上游回显请求 URL，可能包含 access token。
- **根因**：图片下载已有 200 字符净化，但其他 API 没复用同一有界读取/脱敏策略。
- **最小复现**：构造自定义 HTTP handler 返回超长 JSON/换行字段；异常消息完整携带 raw body。
- **现有测试为何未发现**：测试验证异常文本包含 errcode，没有最大长度、控制字符或 secret masking 断言。
- **最小修复边界**：所有响应流式限长；结构化记录 operation/errcode/request-id；raw body 只保留清洗后短摘要；CLI 输出同样走 secret masker。
- **回归测试**：非 JSON、HTML、1 MB body、换行、ANSI、token/appsecret 回显、缺字段。
- **兼容与跨模块风险**：错误文本会变化，应以稳定诊断码替代字符串依赖。

### WX-P2-07：缺失或未知发布状态被当作“发布中”，直到超时

- **严重度 / 置信度**：P2 / 高
- **代码证据**：`WechatDraftGateway.cs:398-424` 缺失/非数字状态映射为 `-1`；`WechatSyncWorkflow.cs:242-258` 只有 `0` 成功和 `>=2` 失败，其余全部当作进行中。
- **触发条件**：响应缺 `publish_status`、类型错误、未来新增负值/未知值。
- **用户影响**：协议错误被延迟伪装成轮询超时，随后触发 WX-P1-01 的重复风险。
- **根因**：解析层用 sentinel 值吞掉 schema 错误，状态机没有 exhaustive validation。
- **最小复现**：返回缺失字段，gateway 产生 `-1`；轮询循环不会立即失败。
- **现有测试为何未发现**：只覆盖 0、1、2 和轮询超时，没有缺字段/未知状态。
- **最小修复边界**：响应 schema 不满足立即给稳定协议错误；只接受官方集合 0..6，未知正值也标成 unsupported status。
- **回归测试**：缺字段、null、string、溢出、-1、7、未来值。
- **兼容与跨模块风险**：无公共 API 变更；诊断行为更精确。

### WX-P2-08：插件级静态权限使纯 dry-run 也必须获得网络、凭据名和不必要报告目录写权限

- **严重度 / 置信度**：P2 / 高
- **代码证据**：`WechatSyncPluginCommandSpecs.cs:53-59` 声明插件级 network、三个 env 和 report write；`WechatSyncPluginOptionsMapper.cs:37-42` 实际在 dry-run 时不需要网络/凭据。报告目录由 Host 写，不是插件业务写入。
- **触发条件**：用户只想安全加载候选或 CI 只做 dry-run。
- **用户影响**：必须在配置中授予超出本次命令需要的能力；最小权限审计无法区分读预览与真实发布。
- **根因**：当前 manifest 权限粒度与命令/模式不匹配，并混入 Host 自有 artifact 权限。
- **最小复现**：直接协议 dry-run 用 `network=false`、空 env 可成功；经正式 Host 静态加载时 required permission 又要求完整授权。
- **现有测试为何未发现**：测试分别验证 required permission 和 direct invoke，没有端到端最小权限 dry-run 契约。
- **最小修复边界**：优先拆分无网络 preview 命令；或扩展协议支持 command-level 权限。移除插件不使用的 report write 声明。
- **回归测试**：最小权限 dry-run 成功、真实 sync 缺网络/env 拒绝、未授权变量不注入。
- **兼容与跨模块风险**：command-level 权限涉及公共插件协议，应独立设计；拆命令可保持 v1 协议。

### WX-P3-01：manifest、内容 JSON 和回退 HTML 没有文件大小或文档数量上限

- **严重度 / 置信度**：P3 / 高
- **代码证据**：`WechatSyncInputLoader.cs:30-66` 直接反序列化文件；`:240-248` 使用无界 `File.ReadAllText`；没有 schema/version 强校验、单文档大小、总字节数或候选数限制。
- **触发条件**：损坏/恶意构建产物、异常大的站点输出。
- **用户影响**：内存、CPU 和正则处理时间不可控，可能在任何微信调用前耗尽进程资源。
- **根因**：信任本地 build output，缺少防御性资源预算。
- **最小复现**：生成超大 JSON/HTML；加载器没有前置拒绝分支。
- **现有测试为何未发现**：仅覆盖路径和缺文件，没有资源预算。
- **最小修复边界**：流式读取限长、文档数/单项/总预算、schemaVersion allowlist。
- **回归测试**：边界大小、巨量 documents、深层 JSON、取消。
- **兼容与跨模块风险**：需要可配置但安全的默认上限。

### WX-P3-02：重试与轮询参数只校验正整数，没有合理上限

- **严重度 / 置信度**：P3 / 高
- **代码证据**：`WechatSyncPluginOptionsMapper.cs:91-108,208-221` 接受任意正 `int`。
- **触发条件**：误配极大 `--max-attempts`、`--poll-max-attempts` 或 interval。
- **用户影响**：任务可运行数天、产生大量请求或看似挂死；CLI/自动化缺少快速反馈。
- **根因**：只做类型/符号校验，没有操作预算。
- **最小复现**：传入 `2147483647` 可通过 mapper。
- **现有测试为何未发现**：只覆盖 0/负数/非数字。
- **最小修复边界**：为 attempts、delay、factor、poll interval 定义独立上限并计算最大总时长。
- **回归测试**：边界、溢出、总预算。
- **兼容与跨模块风险**：更严格校验需要说明。

### WX-P3-03：每次远程图片下载创建新的 HttpClient/连接池

- **严重度 / 置信度**：P3 / 高
- **代码证据**：`WechatDraftGateway.cs:612-647` 每次调用 `DefaultDownloadImageAsync` 都构造并释放 `HttpClient` 和 `SocketsHttpHandler`。
- **触发条件**：文章含大量远程图片，或批量同步多篇文章。
- **用户影响**：连接重建、DNS/TLS 开销、端口压力，吞吐和稳定性下降。
- **根因**：SSRF-safe handler 没有按工作流生命周期复用。
- **最小复现**：N 张远程图产生 N 个 handler/client。
- **现有测试为何未发现**：没有连接复用/批量图片性能测试。
- **最小修复边界**：每个 gateway/workflow 复用一个 SSRF-safe client；保持每次 redirect 都经过 ConnectCallback。
- **回归测试**：多图、重定向、DNS、取消、释放。
- **兼容与跨模块风险**：低；注意 DNS 更新和连接寿命。

### WX-P3-04：远程 URL 内容在地址不变时不会使内容 hash 或缩略图缓存失效

- **严重度 / 置信度**：P3 / 高
- **代码证据**：`SyncCache.cs:153-228` 只在本地媒体缓存文件存在时加入文件签名；纯远程 URL 只作为原 HTML/封面字符串进入 hash。`ThumbResolver` 的 URL 缓存也没有 ETag/Last-Modified/内容摘要。
- **触发条件**：CDN 在同一 URL 下替换图片。
- **用户影响**：内容被错误跳过，或长期复用旧缩略图 media ID。
- **根因**：远程资源身份等同 URL，没有新鲜度策略。
- **最小复现**：保持 HTML/URL 不变，替换远程响应；本地内容 hash 不变。
- **现有测试为何未发现**：只验证本地文件变化触发 hash。
- **最小修复边界**：明确策略：不可变 URL、可选 HEAD validator、内容 digest 或显式 force；不要在 hash 过程中无界下载。
- **回归测试**：ETag/Last-Modified、同 URL 变更、无 validator、离线。
- **兼容与跨模块风险**：网络成本和确定性需要产品决策。

### WX-P3-05：HTML 转换、发行资产和当前微信能力缺少契约级测试与用户文档

- **严重度 / 置信度**：P3 / 高
- **代码证据**：65 个专项测试没有 `ContentProcessor` 测试类；根 manifest 测试在文件不存在时直接 return；仓库搜索到的微信文档只有 minimal fixture README，没有用户级配置、账号资格、错误码、限值、恢复/重试说明。
- **触发条件**：转换顺序、官方契约、manifest 或发行流程变化。
- **用户影响**：明显回归仍可全绿；用户无法判断账号是否具备发布能力，也无法安全恢复失败发布。
- **根因**：测试以实现为真值，未建立 HTML corpus、官方契约 snapshot 和真实包 smoke。
- **最小复现**：本报告的 WX-P1-03、WX-P1-04、WX-P2-02 在 65/65 通过时仍存在。
- **最小修复边界**：加入独立 corpus/golden、API 契约边界测试、真实包矩阵；主线 `guide/` 增加当前能力和安全运行手册。
- **回归测试**：报告中的只读反例矩阵全部自动化。
- **兼容与跨模块风险**：文档/测试任务不改运行时契约。

## 5. 通过项、历史修复复核与未验证边界

### 5.1 已通过或已修复

| 面 | 状态 | 当前证据 |
|---|---|---|
| 路径穿越 | 通过 | `PathUtils.IsSameOrSubPathOf` 用于 output、manifest、cache、media；专项测试覆盖 `..` |
| 符号链接逃逸 | 通过 | `PathUtils.cs:31-80` 解析现存 link target；input、cache、media 测试覆盖目录/文件 symlink |
| SSRF 私网地址 | 通过 | `SsrfGuard.cs:8-96` DNS 解析后只连接公网 IP；IPv4/IPv6 私网/保留段测试通过 |
| 重定向 SSRF | 通过 | 图片下载使用同一带 `ConnectCallback` 的 handler；重定向后的连接仍重新过 guard |
| 图片下载字节上限 | 通过 | `DefaultDownloadImageAsync` 检查 Content-Length 并流式强制 10 MB 上限 |
| 本地媒体变更 hash | 已修复 | `SyncCache.cs:125-150,198-228` 使用内容 SHA；现有回归测试通过 |
| token 失效重取 | 通过 | 40001/40014/42001 会清缓存并重试一次；注意不要与工作流外层 3 次混淆 |
| 进程环境隔离 | 通过 | Host 清空继承环境，仅传授权变量 |
| 执行报告 secret masking | 通过 | `PluginExecutionReporter` 对 stderr/env/diagnostics/artifact 做掩码；CLI 直出例外见 WX-P2-06 |
| artifact 路径 | 通过 | Host 拒绝绝对路径和 `..` artifact |
| stdout 协议纯净 | 通过 | banner 写 stderr；实际 stdout 是单一 JSON 响应 |
| 当前发布状态 0..6 | 通过 | 实现 `0` 成功、`1` 进行中、`>=2` 失败，与当前官方状态集合一致 |
| 公开 Notion 隐私投影 | 已修复 | 当前输入来自 public projection，相关隐私修复和测试存在；本次未发现重新引入私有 ID 的证据 |

近期提交 `b892bb92`、`60ed32ea`、`972ee484`、`fa169bd8`、`2aa8336c`、`a83c7916` 对图片大小、缓存、路径、HTML 定位、SSRF 和符号链接的修复在当前 HEAD 均仍存在。结论是“已修复但需保留回归测试”，不是重复报旧问题。

### 5.2 因安全边界未验证

| 面 | 状态 | 说明 |
|---|---|---|
| 真实 access token | 未验证 | 未读取真实密钥 |
| 真实草稿创建 | 未验证 | 未调用 `draft/add` |
| 真实素材/正文图片上传 | 未验证 | 未调用微信写接口 |
| 真实发布与事件推送 | 未验证 | 未调用 `freepublish/submit`，未接收事件 |
| 账户主体/认证资格 | 未验证 | 只能依据当前官方说明，未用真实账号补证 |
| 微信最终 HTML 清洗结果 | 未验证 | 官方说明会去除 JS/过滤外部图片，但未在线创建草稿比较 |
| 图片像素/解压炸弹 | 证据不足 | 代码缺少显式像素预算，但未在安全边界内制造高内存样本，因此不升级为正式缺陷 |
| 多 RID 执行 | 证据不足 | 只在当前 `osx-arm64` 执行；没有 Linux/Windows runner 与正式产物 |

## 6. 验证记录

### 6.1 仓库测试与依赖

| 命令/面 | 结果 |
|---|---|
| WeChat 专项测试 | 65 passed / 0 failed / 0 skipped |
| PluginHost 测试 | 168 passed / 0 failed / 0 skipped |
| CLI `PluginCliIntegrationTests` | 39 passed / 0 failed / 0 skipped |
| `PluginBoundaryTests` | 17 passed / 0 failed / 0 skipped |
| WeChat Release build | 0 warnings / 0 errors |
| NuGet direct + transitive vulnerability audit | 当前 nuget.org 数据源未发现已知漏洞 |

漏洞检查第一次因 sandbox 无权访问用户 NuGet HTTP cache 失败；按原命令只读提权重跑后通过。这是环境权限，不是插件缺陷。

### 6.2 自包含、Native AOT 与真实包冒烟

| 项 | 结果 |
|---|---|
| `osx-arm64` self-contained single-file | 成功，约 79 MB |
| `osx-arm64` Native AOT | 成功，约 14 MB |
| self-contained SHA256 | `f8f16860125b63b739c21b5d778ffaabf60ad85845ed1656888cd8a41b216d19` |
| Native AOT SHA256 | `508b04c1b8f340dc22eb548ea94e8cbf8191a4d2725138d5a78196fd294ecf7a` |
| 临时真实 manifest `validate-config` | 通过 |
| 临时真实 manifest `validate-manifest` | 通过 |
| AOT `handshake` | exit 0，identity/version/platform/capability 正确 |
| AOT `manifest` | exit 0，命令、选项、权限正确 |
| AOT direct `invoke --dry-run` | exit 0，未要求密钥，未调用微信 |
| PluginHost/CLI 安装后 `wechat-sync sync --output dist --dry-run` | exit 0，`candidates=1` |

这些临时产物只证明当前机器上的技术可行性，不是仓库正式发行证明。

### 6.3 只读反例矩阵

| 反例 | 结果 | 对应问题 |
|---|---|---|
| data 占位图 + `data-src` | 真实 URL 被删，解析为 null | WX-P1-03 |
| figure 多图 + 额外文本 | 第二图和额外文本丢失 | WX-P2-02 |
| emoji 位于截断边界 | 产生孤立 `D83D` | WX-P2-04 |
| 同 path 不同 query | 归一化键相等 | WX-P2-03 |
| 发布永久失败后运行两次 | 2 次草稿 + 2 次发布提交 | WX-P1-01 |
| `48001` 永久错误 | AddDraft 调用 3 次 | WX-P2-01 |
| 32 个并发缓存 writer | 最终只保留 1 条记录 | WX-P1-02 |
| 缺失发布状态 | 映射 -1 并继续轮询 | WX-P2-07 |
| 私网/保留 IP | 现有 SSRF 测试拒绝 | 通过 |
| 路径/符号链接逃逸 | 现有测试拒绝 | 通过 |

### 6.4 targeted gate 基础设施噪声

计划外尝试 `scripts/checks/post-change-targeted.sh` 时，仓库的 brainstorm server 自测报告 `mv-1 left a live spawned server`。进程检查显示对应 preview server 在本次审计前已运行约 11 小时和 5 天，无法归因于 WeChat 路由；因此只记为基础设施噪声，不计入插件缺陷。根据仓库规则，本报告没有运行完整 solution、`ci-full`、`release`、`test-all` 或 `smoke-all` gate。

## 7. 完整覆盖矩阵

| 审计面 | 状态 | 主要编号/证据 |
|---|---|---|
| CLI -> Host -> plugin 调用链 | 通过 | 真实包 Host dry-run |
| 静态/运行时 manifest 一致性 | fixture 通过，发行缺陷 | WX-P1-06 |
| SHA256 / RID / self-contained | osx-arm64 临时通过，正式缺失 | WX-P1-06 |
| 权限最小化 | 发现缺陷 | WX-P2-08 |
| 环境变量隔离 | 通过 | SystemProcessRunner |
| 输入路径/符号链接 | 通过 | 历史修复复核 |
| 输入 schema/大小/数量 | 发现缺陷 | WX-P3-01 |
| source/type 筛选 | 真实运行通过，dry-run 误报 | WX-P2-05 |
| review/expiry/sync 状态 | 发现缺陷 | WX-P1-05 |
| 28 步 HTML 顺序 | 发现缺陷 | WX-P1-03 |
| figure/畸形 HTML 保真 | 发现缺陷 | WX-P2-02 |
| Unicode 与字段限制 | 发现缺陷 | WX-P1-04、WX-P2-04 |
| HTML 注入最终平台结果 | 因安全边界未验证 | 微信会清理 JS，但未在线比较 |
| 本地/缓存图片路径 | 通过 | 路径与 symlink 测试 |
| 远程图片 SSRF | 通过 | SsrfGuard + redirect handler |
| 图片字节大小 | 部分通过、契约缺陷 | 下载有界；uploadimg 2 MB 错误，WX-P1-04 |
| 图片像素解压预算 | 证据不足 | 未安全复现，不立项为正式缺陷 |
| 媒体键/新鲜度 | 发现缺陷 | WX-P2-03、WX-P3-04 |
| token | 基本通过 | 失效重取；真实 token 未验证 |
| 草稿请求/字段 | 发现缺陷 | WX-P1-04 |
| 发布状态 0..6 | 通过 | 当前官方契约一致 |
| 缺字段/未来状态 | 发现缺陷 | WX-P2-07 |
| 永久/瞬态错误分类 | 发现缺陷 | WX-P2-01 |
| 幂等/中断恢复 | 发现缺陷 | WX-P1-01、WX-P1-02 |
| 日志/错误体/密钥 | 部分通过、发现缺陷 | report masking 通过；CLI/raw 见 WX-P2-06 |
| 诊断码/退出码/artifact | 基本通过 | 异常过度汇总为 failed，见 WX-P2-06 |
| 专项/Host/CLI/架构测试 | 通过但覆盖不足 | WX-P3-05 |
| Release build / NuGet audit | 通过 | 0 warnings；无已知漏洞 |
| Native AOT | 当前 RID 通过 | 临时 AOT 冒烟 |
| 多 RID 正式发行 | 证据不足/未就绪 | WX-P1-06 |
| 真实微信公众号行为 | 因安全边界未验证 | 无真实写操作 |

## 8. 修复路线与依赖顺序

### 阶段 1：阻止重复发布、未审核发布和确定性 API 拒绝

1. WX-P1-01：设计可恢复发布状态机和缓存迁移。
2. WX-P1-02：缓存原子写、锁、合并和损坏隔离。
3. WX-P1-05：为 draft/publish 建立独立审核状态 allowlist。
4. WX-P1-04：同步当前官方 32/16/1 MB/1 KB/正文限值，全部前置 fail-fast。

阶段 gate：故障注入状态机测试 + 并发进程缓存测试 + 官方契约 golden；不得真实发布。

### 阶段 2：幂等、重试分类与 API 状态机

1. WX-P2-01：按步骤和 errcode 分类，永久错误不重试。
2. WX-P2-07：严格响应 schema 和 exhaustive status。
3. WX-P2-06：统一有界读取、结构化错误和 CLI 脱敏。
4. 限制 attempts/interval 总预算（WX-P3-02）。

阶段 gate：fake HTTP handler 的 4xx/5xx/429/超时/非 JSON/响应丢失矩阵。

### 阶段 3：HTML、图片正确性与资源限制

1. WX-P1-03：修正懒加载属性与图片上传顺序。
2. WX-P2-02：用 parser 替换破坏性 figure 重建。
3. WX-P2-03 / WX-P3-04：统一媒体 URL 身份和新鲜度策略。
4. WX-P2-04：Rune/text-element 截断。
5. WX-P3-01 / WX-P3-03：输入预算和 HttpClient 复用。

阶段 gate：HTML corpus golden、Unicode、恶意属性、畸形 HTML、多图、1 MB 边界、SSRF 回归。

### 阶段 4：协议、CLI、诊断和发行包

1. WX-P2-05：抽取 dry-run planning。
2. WX-P2-08：拆分最小权限 preview 或独立设计 command-level 权限。
3. WX-P1-06：生成正式多 RID 包、SBOM、checksum、根 manifest 和安装后协议冒烟。

阶段 gate：每 RID `validate-config / validate-manifest / SHA / handshake / manifest / dry-run`。

### 阶段 5：测试、文档和长期治理

1. 为全部正式问题加入回归测试；尤其建立 `ContentProcessor` corpus。
2. 在主线 `guide/` 写账号资格、权限、限值、错误码、恢复、缓存迁移和安全运行说明。
3. 增加官方契约定期复核；不要让测试常量直接复制实现常量。
4. 根 manifest/发行包缺失必须使 release gate 失败，不能条件式跳过。

## 9. 兼容性与独立任务要求

- 本审计没有改变公共 API、manifest、缓存格式或 CLI。
- 发布状态机和原子缓存涉及格式迁移，必须作为独立实现任务，设计 v2 兼容读取与失败回滚。
- review status 策略、command-level 权限和正式 RID 支持属于公共契约，需明确默认值和迁移文档。
- 媒体 URL 键与 Core 本地化存在共同语义；修复必须跨模块协调，但不得在网站业务任务内顺带修改 Core。
- HTML 处理和 Unicode 修复会改变内容 hash；应提升 hash schema/version，避免错误跳过。

## 10. 最终结论

当前 WeChat 插件是一个**协议和工程骨架可运行、专项测试较完整，但尚不具备安全发布可靠性和正式发行闭环**的实现。最高优先级不是继续增加转换功能，而是先建立可恢复发布状态机、原子并发缓存、审核门禁和当前官方契约校验；否则任何真实账号验证都会把测试流量暴露给重复草稿、重复发布或确定性 API 拒绝风险。

本报告的“全量”仅指所声明代码、契约、测试和发行边界内的穷尽式审计。真实微信公众号草稿、素材和发布行为因为安全只读边界没有线上验证，不能宣称已通过生产发布验证。
