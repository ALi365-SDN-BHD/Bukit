# Bukit WeChat 插件全方位深度审计复核（2026-07-21）

## 1. 执行摘要

本报告以当前 `main` 的提交 `151d28aa118e8a081ee2902f614474f295e3cc5d` 为唯一代码基线，是 2026-07-18 审计的完整复核版。审计开始时 HEAD 为 `6e7d899b687994611aa329e24b00900da9a3c81d`；共享仓库随后由外部任务快进到当前 HEAD，新增提交只涉及 Analytics 与本报告初稿，复核确认两个 HEAD 之间的 WeChat、PluginHost、`PluginCliLoader`、SSRF 和对应专项测试均无差异。旧基线 `4103959c9f7ee1b8dfe8db7e34340f4495e7a9ce` 到当前 HEAD 在上述 WeChat 调用链上也没有修复；另有与 WeChat 路由无关的 CLI 开发/预览响应处理和架构测试变更，未作为本报告行为证据。因此旧报告的 19 项正式缺陷全部仍存在。本次又以静态证据和 `/tmp` 反例确认 5 项新缺陷，共 24 项：P0 0、P1 7、P2 11、P3 6。

| 严重度 | 数量 | 核心风险 |
|---|---:|---|
| P0 | 0 | 未发现无需前置条件即可造成灾难性影响的缺陷 |
| P1 | 7 | 重复草稿/发布、幂等状态丢失、未审核内容发布、正文/图片确定性失败或丢失、正式包缺失 |
| P2 | 11 | 无差别重试、HTML/Unicode/媒体错误、dry-run 误报、日志泄漏、权限过宽、参数组合失效 |
| P3 | 6 | 输入预算、极端参数、连接复用、远程资源新鲜度、诊断字段、测试与文档治理 |

五个维度的结论互相独立；专项测试全绿不能替代审计结论。

| 维度 | 结论 | 摘要 |
|---|---|---|
| 功能正确性 | **不通过** | HTML 会丢图/丢内容/破坏属性，Unicode 可损坏，dry-run 候选数错误，默认外链图会被微信过滤 |
| 安全性 | **有条件不通过** | SSRF、路径和环境隔离有效；但发布审核门禁缺失、签名 URL 与原始错误体可能进入日志、权限不最小 |
| 运行可靠性 | **不通过** | 无可恢复发布状态机，缓存非原子且无锁，永久错误和不确定副作用被重试 |
| 协议适配 | **基本通过** | 当前 `handshake / manifest / invoke --dry-run` 成功；但 dry-run 语义和权限模型不正确 |
| 发行就绪度 | **不通过** | Native AOT 技术可行，但没有正式根 manifest、真实多 RID 产物、SHA/SBOM 和强制发行 gate |

安全边界：没有读取真实微信凭据，没有获取真实 token，没有上传素材、创建草稿或提交发布。关于微信最终清洗、账号资格和线上状态事件，只依据审计日官方文档，不宣称经过真实账号验证。

## 2. 范围、方法与基线

### 2.1 范围

- `src/Bukit-Plugins/Bukit.Plugin.WechatSync/`
- `src/Bukit-Plugins/Bukit.WechatSyncing/`
- 直接相连的 `Bukit.Cli`、`Bukit.PluginHost`、插件协议 DTO、权限/路径/SHA/进程执行和 `SsrfGuard`
- WeChat、PluginHost、CLI plugin integration、架构边界测试
- 程序化 manifest、minimal fixture、`.bukit/plugins.yaml` 契约、Release/NuGet/Native AOT/临时安装包
- 当前官方 access token、草稿、正文图片、发布提交、发布状态和发布资格说明

排除无关 Core 业务功能，以及 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/` 等备份目录。审计只新增本报告；审计期间出现的 Analytics 工作树修改属于其他任务，未读取为证据、未修改、未覆盖。

### 2.2 真实调用链

```text
Bukit.Cli
  -> PluginCliLoader（plugins.yaml、RID、SHA256、权限）
  -> PluginProtocolClient / SystemProcessRunner（清空环境、handshake、manifest、invoke）
  -> WechatSyncPluginApp
  -> WechatSyncPluginOptionsMapper
  -> WechatSyncInputLoader（agent-manifest -> content JSON/HTML）
  -> WechatSyncWorkflow（筛选、转换、图片、缓存、重试、发布状态）
  -> WechatDraftGateway
       token -> material/add_material -> media/uploadimg -> draft/add
             -> freepublish/submit -> freepublish/get
```

关键代码入口：`src/Bukit-Core/Bukit.Cli/Cli/PluginCliLoader.cs:147-203`、`src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs:45-171`、`src/Bukit-Plugins/Bukit.Plugin.WechatSync/WechatSyncPluginApp.cs`、`src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncWorkflow.cs:20-136`。

### 2.3 当前官方契约

审计日重新读取以下腾讯官方页面：

- [获取 access token](https://developers.weixin.qq.com/doc/service/api/base/api_getaccesstoken)
- [新增草稿](https://developers.weixin.qq.com/doc/service/api/draftbox/draftmanage/api_draft_add)
- [上传正文图片](https://developers.weixin.qq.com/doc/service/api/material/permanent/api_uploadimage)
- [提交发布](https://developers.weixin.qq.com/doc/service/api/public/api_freepublish_submit)
- [查询发布状态](https://developers.weixin.qq.com/doc/service/api/public/api_freepublish_get)
- [发布能力说明](https://developers.weixin.qq.com/doc/service/guide/product/publish.html)

已确认的当前边界：标题 32 字、作者 16 字、正文少于 20,000 字符且小于 1 MB、原文 URL 小于 1 KB；正文图片只接受 JPG/PNG 且小于 1 MB；正文中的外部图片会被过滤；提交接口成功只表示发布任务提交成功；发布状态集合为 0..6。草稿正文页面同一说明还出现与“小于 1M”矛盾的“2kb”，本报告不据此推导 2 KB 限制。自 2025 年 7 月起，个人主体、未认证企业主体和不支持认证的主体可能失去发布接口权限；真实账号资格未验证。

### 2.4 manifest / CLI / 权限矩阵

| 面 | 程序化 manifest / mapper | fixture / Host | 结论 |
|---|---|---|---|
| 命令 | `wechat-sync sync`，28 个 options | fixture 同步 | 结构一致 |
| 必填 output | manifest required；mapper 解析并做路径约束 | Host 校验 | 通过 |
| 默认 target | mapper 为 `draft` | manifest 只列 `draft,publish` | 可运行，文档不足 |
| dry-run | mapper 不读凭据、不需要网络 | 插件级权限仍要求网络/env/write | 缺陷 WX-P2-08 |
| `process-images` / `passthrough` | 两个 flag 可同时为 true | 无 `conflictWith` | 缺陷 WX-P2-10 |
| 环境变量 | 默认 3 个，可由 option 改名 | manifest 只授权默认名 | 自定义名存在配置约束；本轮未单列新缺陷 |
| 文件权限 | 读根/output；写 cache | 额外声明 Host report write | 过度声明，WX-P2-08 |
| RID / SHA | 无正式根 manifest | fixture 单 `osx-arm64` + 零 SHA | 发行不通过，WX-P1-06 |

## 3. 正式缺陷

### WX-P1-01：发布失败、超时或进程中断后重复创建草稿并重复提交发布

- **严重度 / 置信度**：P1 / 高。
- **证据、触发、影响**：`WechatSyncWorkflow.cs:80-105` 先建草稿、再提交和轮询，只有最终成功才写 cache；`:222-264` 把失败/超时折叠为 false。`--target publish` 在 submit 已被接受后断网、失败或中断，重跑会重新建草稿并可能再次发布。
- **根因 / 最小复现**：缓存没有 `draft-created`、`publish-submitted`、publish ID 或 unknown 中间态。fake gateway 连跑两次状态 3，得到 `adds:2; publishes:2; synced:0,0`。
- **测试缺口**：`WechatSyncWorkflowTests.cs:489-532` 只运行一次失败，不覆盖失败后重跑、响应丢失或每个持久化点的中断。
- **最小修复 / 回归**：建立可恢复、原子持久化的发布状态机；覆盖 submit 成功后断网、轮询超时、服务端成功而客户端丢响应、逐写点 kill。
- **兼容风险**：缓存格式必须版本化迁移并保留旧版读取；应作为独立实现任务。

### WX-P1-02：缓存非原子、无锁、无合并，损坏或并发会丢失幂等状态

- **严重度 / 置信度**：P1 / 高。
- **证据、触发、影响**：`SyncCache.cs:45-74` 将读取/解析异常静默重置为空；`:77-85` 直接 `File.WriteAllText` 覆盖。并发 CLI、短写或半截 JSON 会丢成功记录，继而重复草稿、素材或发布。
- **根因 / 最小复现**：普通 JSON 被当作跨进程幂等日志。32 个 writer 各写 1 条，调用均未失败，最终只剩 1 条。
- **测试缺口**：没有并发进程、崩溃注入、原子替换、损坏隔离和合并测试。
- **最小修复 / 回归**：同目录临时文件、flush、原子 replace、项目锁、保存前重读合并；损坏文件隔离并阻止无提示继续发布。覆盖双进程和每个写入故障点。
- **兼容风险**：需定义锁等待、冲突和旧缓存迁移语义。

### WX-P1-03：HTML 第 28 步先删除懒加载真实源，图片上传器随后无法恢复

- **严重度 / 置信度**：P1 / 高。
- **证据、触发、影响**：`ContentProcessor.cs:603-637` 最后清理 lazy 属性，`:185-201` 删除 `data-src/data-original/data-actualsrc/data-lazy-src/srcset`；工作流到 `WechatSyncWorkflow.cs:162-170` 才运行依赖这些属性的 `ContentImageProcessor.cs:183-225`。data 占位图 + `data-src` 会变成占位/缺图，但仍可能记录同步成功。
- **根因 / 最小复现**：转换顺序违背图片解析契约。反例处理后只剩 data `src`，`ResolveBestImageUrl` 返回 null。
- **测试缺口**：65 个专项测试没有 `ContentProcessor` 测试，也没有完整 28 步后处理 lazy 图的集成测试。
- **最小修复 / 回归**：先解析上传再清理，或把最佳源写回 `src`；覆盖单双/无引号、srcset、data 占位、失败降级、多图。
- **兼容风险**：最终 HTML/hash 改变，需要失效旧 cache 或提升 hash 版本。

### WX-P1-04：本地字段和正文图片限制与当前微信契约漂移

- **严重度 / 置信度**：P1 / 高。
- **证据、触发、影响**：`Helpers.cs:218-242` 标题允许 64 UTF-16 单元；`WechatSyncWorkflow.cs:267-290` 不限制 16 字作者和 1 KB URL；`:175-188` 对正文超限只警告仍提交；`ImageConverter.cs:14-22`、`WechatDraftGateway.cs:246-262` 允许 uploadimg 2 MB。33-64 字标题、长作者/URL、1-2 MB 图片和超限正文将被确定性拒绝。
- **根因 / 最小复现**：硬编码限制且测试以实现为真值；1-2 MB 支持格式可通过本地规范化。
- **测试缺口**：无 32/16、20,000、1 MB、1 KB 的前后边界及 Unicode/UTF-8 双预算测试。
- **最小修复 / 回归**：集中契约常量，网络前 fail-fast；按官方字符规则和 UTF-8 大小验证；图片严格小于 1 MB。覆盖中文、emoji、组合字符和 multipart 余量。
- **兼容风险**：过去会“尝试发送”的输入改为本地失败；需稳定诊断码和迁移说明。

### WX-P1-05：公开投影带审核/过期状态，但插件没有同步与发布门禁

- **严重度 / 置信度**：P1 / 高。
- **证据、触发、影响**：Core `ContentProjectionWriter.cs:104-126` 写 `reviewStatus`；`WechatSyncInputLoader.cs:82-115` 丢弃 review/expiry，`WechatSyncWorkflow.cs:293-328` 只按 source/type 筛选。实测 `needs-review` 且已过期内容仍 Routed=1，`--target publish` 可直接提交。
- **根因 / 最小复现**：把公开可索引等同允许微信发布；fixture 设非批准和过去 `expiresAt` 即可通过。
- **测试缺口**：loader fixture 固定 approved，未断言状态传播；无 draft/publish 状态矩阵。
- **最小修复 / 回归**：为草稿和发布分别定义安全 allowlist，过期拒绝，`--force` 不绕过；覆盖所有 schema 状态和篡改产物。
- **兼容风险**：发布策略是公共行为；新增显式选项与安全默认，不应隐式修改 Core。

### WX-P1-06：仓库中的 WeChat 插件不是可安装的正式发行包

- **严重度 / 置信度**：P1 / 高（发行维度）。
- **证据、触发、影响**：包根没有 `plugin.yaml`；`examples/minimal/README.md:3-9` 明示 fixture 不可运行，`plugin.yaml:7-10` 只有 osx-arm64 与零 SHA；`PluginSchemaContractTests.cs:89-99` 在根 manifest 缺失时 return。用户无法取得受校验的多平台正式包。
- **根因 / 最小复现**：可行性和发行治理未闭环；仓库根检查即可复现。临时真实 AOT 包可运行，证明是发行资产缺失而非技术不可行。
- **测试缺口**：fixture 测 schema，不测真实 hash/入口；根 manifest 缺失不会使 gate 失败。
- **最小修复 / 回归**：发行任务生成支持 RID 的自包含产物、真实 SHA、根 manifest、SBOM/checksum；每 RID 安装后验证 hash、权限、握手、manifest、dry-run 和损坏包。
- **兼容风险**：必须先定义支持平台；临时 osx-arm64 包不能冒充正式发行。

### WX-P1-07：默认关闭正文图片转存，外链图片会被微信过滤而同步仍成功

- **严重度 / 置信度**：P1 / 高。
- **证据、触发、影响**：`WechatSyncPluginOptionsMapper.cs:104` 与 `WechatSyncModels.cs:52` 令 `ProcessImages=false`；`WechatSyncWorkflow.cs:56-58,167-170` 只有显式开启才转存。官方明确说明正文外部图片会被过滤。普通 Bukit HTML 含外链 `<img>`、用户采用默认参数时，草稿/发布正文丢图但 cache 可记成功。
- **根因 / 最小复现**：平台正确性所必需的步骤被设计为无警告 opt-in。临时 workflow 得到 `uploads:0`，发送内容仍是 `https://cdn.example.com/external.png`。
- **测试缺口**：无默认选项 + 外链图的端到端契约测试；文档未将 `--process-images` 标为必需或解释平台过滤。
- **最小修复 / 回归**：默认自动转存，或在检测到非微信外链图时 fail-fast/强警告且不写成功 cache；覆盖上传失败、已有微信 CDN、data/local/relative URL。
- **兼容风险**：改变默认值会引入网络与素材副作用；应提供显式 opt-out、迁移说明和 dry-run 计划输出。

### WX-P2-01：永久错误与有副作用的完整同步链被无差别重试

- **严重度 / 置信度**：P2 / 高。
- **证据、触发、影响**：`WechatSyncWorkflow.cs:138-207` 一个 retry 包住封面、正文图和 AddDraft，并捕获全部非取消异常。48001、参数错误或“服务端成功但响应丢失”会被重复，消耗配额并可能重复副作用。
- **根因 / 最小复现**：没有 HTTP/errcode/步骤分类和幂等边界；fake `WechatApiException(48001)` 使 AddDraft 调用 3 次。
- **测试缺口**：只证明一般失败会 retry，未区分永久/瞬态和不确定结果。
- **最小修复 / 回归**：只重试明确瞬态且未产生不确定副作用的步骤；覆盖 429/5xx/超时/非 JSON/40005/48001 与成功后断流。
- **兼容风险**：重试行为变化需稳定诊断和可配置上限。

### WX-P2-02：`figure` 转换删除第二张图片及非 caption 内容

- **严重度 / 置信度**：P2 / 高。
- **证据、触发、影响**：`ContentProcessor.cs:45-79` 只取第一个 img 和第一个 caption 后重建。多图、署名、链接或版权节点会静默丢失并可能写入成功缓存。
- **根因 / 最小复现**：正则重建不保序；含 two images、strong credit 与 caption 的反例只剩第一图和 caption。
- **测试缺口**：无 figure/HTML corpus、嵌套/畸形/多图测试。
- **最小修复 / 回归**：用 HTML parser 保留全部子节点，仅转换容器；覆盖无 caption、多 caption、属性引号和畸形闭合。
- **兼容风险**：输出/hash 变化，需 cache 版本处理。

### WX-P2-03：媒体 URL 键丢弃 query 且正文去重忽略大小写

- **严重度 / 置信度**：P2 / 高。
- **证据、触发、影响**：`Helpers.cs:371-395` 只保留 scheme/host/path；`ContentImageProcessor.cs:48-50,124-169` 用 OrdinalIgnoreCase 去重。签名/query 不同或大小写敏感 CDN 路径可能命中错误图片。
- **根因 / 最小复现**：把所有 query 当跟踪参数并假定整个 URL 大小写不敏感；`?id=one` 与 `?id=two` 都变为同一个 key。
- **测试缺口**：无 query、重复参数、percent encoding、case-sensitive path 矩阵。
- **最小修复 / 回归**：保留 query，或只移除明确追踪参数；仅 host/scheme 忽略大小写；覆盖签名 URL。
- **兼容风险**：需协调 Core 媒体索引与插件 cache 迁移。

### WX-P2-04：UTF-16 单元截断可产生非法 Unicode

- **严重度 / 置信度**：P2 / 高。
- **证据、触发、影响**：`Helpers.cs:234-242` 使用 range substring，标题/摘要调用在 `WechatSyncWorkflow.cs:273-280`；`StripHtml` 的逐 char 截断也有同根问题。emoji 边界会留下孤立 surrogate，序列化可能变 U+FFFD 或被 API 拒绝。
- **根因 / 最小复现**：把 code unit 当字；60 个 a + emoji、max 64 输出末尾孤立 `D83D`。
- **测试缺口**：无 Rune、ZWJ、组合字符、CJK 扩展和有效 UTF-16 断言。
- **最小修复 / 回归**：按 Rune/text element 与 UTF-8 双预算截断；覆盖所有上述边界。
- **兼容风险**：截断结果和 hash 改变。

### WX-P2-05：dry-run 报告总加载数而非真实筛选候选数

- **严重度 / 置信度**：P2 / 高。
- **证据、触发、影响**：`WechatSyncPluginInvoker.cs:25-27` dry-run 跳过 workflow；`WechatSyncPluginResponseMapper.cs:9-22` 输出 `context.Routed.Count`；筛选只在 `WechatSyncWorkflow.cs:293-328`。指定不存在 source 仍报告 candidates=1，发布前预览误导用户。
- **根因 / 最小复现**：没有共享无副作用 planning 阶段；`--source-names=no-such-source` 可复现。
- **测试缺口**：兼容测试只有单 item 且无过滤。
- **最小修复 / 回归**：抽取 dry-run/真实运行共用 planner，输出逐项纳入/排除原因；覆盖 0/1/N、所有筛选和缺字段。
- **兼容风险**：只修正输出语义，无需改协议 DTO。

### WX-P2-06：API 原始错误体缺少统一大小/脱敏边界，CLI 可直出

- **严重度 / 置信度**：P2 / 高；上游是否回显具体密钥为中置信度。
- **证据、触发、影响**：`WechatDraftGateway.cs:119,338,388,543` 等使用无界 `ReadAsStringAsync`，`:707-741` 将 raw JSON 拼入异常；`WechatSyncPluginResponseMapper.cs:58-68` 原样映射，`PluginCommandInvoker.cs:38-64` 打印。超大/含 CRLF、ANSI、敏感业务字段的响应可放大内存、注入日志或泄漏。
- **根因 / 最小复现**：只有图片错误使用有限截断，其他 API 未共享有界读取和净化；自定义 handler 返回大 body 即可观察完整异常。
- **测试缺口**：无非 JSON、HTML、1 MB body、控制符、token/appsecret 回显和 CLI masking 测试。
- **最小修复 / 回归**：流式限长；只记录 operation/errcode/request-id 和清洗短摘要；所有 CLI 输出走 masker。
- **兼容风险**：错误文本变化，调用方应依赖稳定诊断码。

### WX-P2-07：缺失、非数字或负发布状态被当作进行中直到超时

- **严重度 / 置信度**：P2 / 高。
- **证据、触发、影响**：`WechatDraftGateway.cs:398-400` 将缺失/非数字映射 -1；`WechatSyncWorkflow.cs:242-258` 仅 0 成功、1 继续、`>=2` 失败，负值也继续轮询。协议错误被伪装为超时并放大 WX-P1-01。
- **根因 / 最小复现**：sentinel 吞掉 schema 错误；响应缺 `publish_status` 即复现。
- **测试缺口**：无 missing/null/string/overflow/-1/7。本报告修正旧报告的一个描述：未知正值 7 会被 `>=2` 立即判失败，不会轮询超时。
- **最小修复 / 回归**：解析不满足 schema 立即失败；显式处理官方 0..6，其他值给 unsupported-status 诊断。
- **兼容风险**：只改变错误诊断时机。

### WX-P2-08：插件级静态权限使纯 dry-run 也必须获网络、凭据名和多余写权限

- **严重度 / 置信度**：P2 / 高。
- **证据、触发、影响**：`WechatSyncPluginCommandSpecs.cs:53-59` 总是要求 network、3 env、2 write；`WechatSyncPluginOptionsMapper.cs:32-42` dry-run 实际不读凭据/网络；`PluginCliLoader.cs:147-194` 调用前无条件校验。最小权限 CI dry-run 被 exit 2 拒绝。
- **根因 / 最小复现**：v1 权限粒度与模式不匹配并混入 Host report 目录。直接协议 `network=false/env=[]/write=[]` 成功，经 Host 同权限失败。
- **测试缺口**：分别测 manifest 和 invoke，没有端到端最小权限 dry-run。
- **最小修复 / 回归**：拆无网络 preview 命令，或独立设计 command-level 权限；去除插件未写的 report 目录。覆盖 preview 最小权限及真实 sync 缺授权拒绝。
- **兼容风险**：command-level 权限会改变公共协议；拆命令可保持 v1。

### WX-P2-09：签名或带令牌的媒体 URL 被完整写入日志

- **严重度 / 置信度**：P2 / 高。
- **证据、触发、影响**：`ThumbResolver.cs:127,253,485` 和 `ContentImageProcessor.cs:134,144,152,164,170,176` 直接插值完整 URL。SAS、OSS/COS/S3 签名或 `?token=SECRET` 会进入 stderr、CI 和执行报告。
- **根因 / 最小复现**：`PluginSecretMasker.cs:28-49` 只替换已注入环境值，不会通用删除 URL query；任意非 env 的 query secret 会原样保留。
- **测试缺口**：无 query/userinfo/fragment/控制字符和 URL 日志脱敏测试。
- **最小修复 / 回归**：日志只保留 origin + 安全 path 摘要或不可逆 ID，删除 userinfo/query/fragment；覆盖常见云签名格式和 CRLF。
- **兼容风险**：仅日志文本变化；应同时保留可关联的稳定 hash。

### WX-P2-10：`--passthrough --process-images` 可同时指定，但图片处理被静默禁用

- **严重度 / 置信度**：P2 / 高。
- **证据、触发、影响**：mapper 在 `WechatSyncPluginOptionsMapper.cs:104-105` 同时接受两 flag；`WechatSyncPluginCommandSpecs.cs:45-46` 没有 conflict；`WechatSyncWorkflow.cs:163-170` 却把图片处理嵌套在 `!Passthrough` 内。用户明确要求上传图片，最终 0 次上传并保留外链，继而被微信过滤。
- **根因 / 最小复现**：passthrough 的范围未定义且覆盖显式图片选项；临时 workflow 得到 `uploads:0` 和原外链 HTML。
- **测试缺口**：options 测试只独立解析 passthrough；无组合矩阵和 manifest conflict 断言。
- **最小修复 / 回归**：让 passthrough 仅跳过 ContentProcessor、仍执行显式图片处理；若产品不允许则在 mapper/manifest fail-fast。覆盖四种 bool 组合。
- **兼容风险**：明确既有歧义；若选择执行上传，会新增网络副作用，需写入帮助和版本说明。

### WX-P2-11：实体解码在 HTML 属性内部运行，可生成无效属性边界

- **严重度 / 置信度**：P2 / 高。
- **证据、触发、影响**：`ContentProcessor.cs:45-79` 将 figure caption 转义写入 alt；`:92-118` 的 `[^<]+` 同时匹配标签属性并解码；`:603-637` 在第 27 步调用。caption 含引号会生成 `alt="He said "hello""`，平台可能删除或重解释属性/标签。
- **根因 / 最小复现**：正则声称处理 text node，实际不解析 HTML token；输入 `<figcaption>He said &quot;hello&quot;</figcaption>` 即复现。
- **测试缺口**：无 ContentProcessor、属性实体、caption 引号或最终结构有效性测试。
- **最小修复 / 回归**：使用 HTML parser，仅解码文本节点；属性保持正确编码。覆盖单双引号、数字/双重实体、protected blocks 和畸形标签。
- **兼容风险**：会改变已处理 HTML/hash；没有真实微信写验证，因此不将其升级为可执行注入。

### WX-P3-01：manifest、内容 JSON 和回退 HTML 无大小/文档数预算

- **严重度 / 置信度**：P3 / 高。
- **证据、触发、影响**：`WechatSyncInputLoader.cs:30-66` 无长度/总量/schemaVersion allowlist；`:240-248` 无界 `File.ReadAllText`。异常输出可在联网前消耗不可控内存/CPU/正则时间。
- **根因 / 最小复现**：信任本地 build output；2 MiB body 被完整加载为 2,097,152 chars。
- **测试缺口**：路径/符号链接测试多，manifest/JSON/HTML/document count 预算为零。
- **最小修复 / 回归**：流式限长、单项/总字节/候选数/depth 上限及 schema allowlist；覆盖边界、巨量 documents、取消。
- **兼容风险**：需可配置但安全的默认值。

### WX-P3-02：重试和轮询参数只校验正整数，没有合理上限

- **严重度 / 置信度**：P3 / 高。
- **证据、触发、影响**：`WechatSyncPluginOptionsMapper.cs:91-108,208-221` 接受任意正 int；`2147483647` 可令任务运行极久或制造巨大请求预算。
- **根因 / 最小复现**：只做类型/符号校验，不计算总时长。
- **测试缺口**：只测 0/负数/非数字，没有独立上限、组合预算和溢出矩阵。
- **最小修复 / 回归**：为 attempts/delay/factor/poll interval 设置上限并验证总预算；覆盖最大值和乘法溢出。
- **兼容风险**：更严格校验需用户说明。

### WX-P3-03：每次远程图片下载创建新的 HttpClient/连接池

- **严重度 / 置信度**：P3 / 高。
- **证据、触发、影响**：`WechatDraftGateway.cs:612-647` 每次 `DefaultDownloadImageAsync` 新建/释放 `SocketsHttpHandler` 和 `HttpClient`。多图/批量文章重复 DNS/TLS，增加端口与吞吐压力。
- **根因 / 最小复现**：SSRF-safe handler 没按 workflow 生命周期复用；N 图产生 N 个连接池。
- **测试缺口**：无连接复用、批量性能和 handler 生命周期测试。
- **最小修复 / 回归**：每 gateway/workflow 复用带安全 ConnectCallback 的 client，设置连接寿命；覆盖 redirect、DNS、取消、释放。
- **兼容风险**：低，但要保持每次重定向继续经过 SSRF 防护。

### WX-P3-04：远程 URL 内容在地址不变时不会失效 hash 或缩略图缓存

- **严重度 / 置信度**：P3 / 高。
- **证据、触发、影响**：`SyncCache.cs:153-229` 只有本地/媒体缓存文件才加入摘要；`ThumbResolver.cs:60-90` 以 URL key 复用 media ID。CDN 同 URL 换图时同步被错误跳过或继续用旧缩略图。
- **根因 / 最小复现**：远程资源身份等同 URL，没有 ETag/Last-Modified/content digest；保持 HTML/URL 不变替换响应即可。
- **测试缺口**：只有本地文件变化测试，无远程同 URL 变化。
- **最小修复 / 回归**：明确不可变 URL、validator、digest 或 force 策略，避免 hash 阶段无界下载；覆盖离线和无 validator。
- **兼容风险**：网络成本与构建确定性需产品决策。

### WX-P3-05：HTML、官方契约、发行资产缺少契约级测试和用户文档

- **严重度 / 置信度**：P3 / 高。
- **证据、触发、影响**：65 个专项测试没有 `ContentProcessor` 测试；`PluginSchemaContractTests.cs:89-103` 在根 manifest 不存在时跳过；`guide/` 没有 WeChat 用户手册，minimal README 反而明确不可运行。明显回归仍可全绿，用户无法安全恢复失败发布。
- **根因 / 最小复现**：测试以实现为真值，没有 HTML corpus、官方契约 snapshot 和真实包 gate；本报告多项反例在 65/65 通过时存在。
- **测试缺口**：即本项；缺审核门禁、输入预算、Unicode、dry-run、远程媒体和多 RID smoke。
- **最小修复 / 回归**：把本报告反例自动化；主线 guide 增加账号资格、权限、限制、错误分类、恢复/重试和发行说明。
- **兼容风险**：测试/文档本身不改运行时契约。

### WX-P3-06：顶层 `article_id` 被错误赋给 `ArticleUrl`

- **严重度 / 置信度**：P3 / 高。
- **证据、触发、影响**：`WechatDraftGateway.cs:402-405` 将 `article_id` 写入 articleUrl，仅在 `article_detail.item[].article_url` 存在时于 `:408-419` 覆盖；`WechatSyncWorkflow.cs:244` 以 `articleUrl=` 记录。官方明确区分 ID 与 URL。成功响应缺 article_detail 时，诊断/自动化把 ID 冒充 URL。
- **根因 / 最小复现**：响应模型混合两个字段；`{"publish_status":0,"article_id":"ARTICLE_ID"}` 产生 `ArticleUrl="ARTICLE_ID"`。
- **测试缺口**：Gateway 无发布响应解析测试，workflow fake 的 ArticleUrl 固定 null。
- **最小修复 / 回归**：模型分别保存 ArticleId/ArticleUrl；只有合法 absolute URL 才写 ArticleUrl；覆盖 detail 缺失/空/多 item/畸形 URL。
- **兼容风险**：若该 record 是公共契约，新增字段并保留旧成员一个弃用周期；否则内部直接纠正。

## 4. 已通过、历史修复与未验证边界

### 4.1 当前通过或历史修复仍有效

| 面 | 结论 | 证据摘要 |
|---|---|---|
| output/manifest/cache/media 路径穿越 | 通过 | 路径均经 same/subpath 校验，专项测试覆盖 `..` |
| 符号链接逃逸 | 通过 | 现存路径按 realpath 约束；相关回归测试通过 |
| 远程图片 SSRF | 通过 | `SsrfGuard` + 解析 IP 绑定的 ConnectCallback；loopback/私网/重定向测试通过 |
| 图片下载字节预算 | 通过 | Content-Length 和流式 10 MB 上限均存在；像素预算另见未验证 |
| 子进程环境隔离 | 通过 | `SystemProcessRunner.cs:83-116` 清空继承环境，只注入授权变量 |
| SHA256 / RID 校验链 | 通过 | `PluginHashVerifier.cs:7-31`；临时真实包 Host 验证成功 |
| stdout 协议纯净 | 通过 | banner 写 stderr；stdout 是单一 JSON 响应 |
| token 失效刷新 | 通过 | 40001/40014/42001 触发一次刷新重试 |
| 当前发布状态 0..6 | 通过 | 0 成功、1 进行中、2..6 失败；缺字段见 WX-P2-07 |
| 公开 Notion 隐私投影 | 历史修复仍有效 | 未发现重新引入私有 ID 的证据 |

近期 SSRF、缓存键、路径、HTML 定位、隐私、图片大小和符号链接修复在当前 HEAD 仍在。这里的“通过”只覆盖相应安全性质，不否定相邻缺陷。

### 4.2 因安全边界未验证或证据不足

| 面 | 状态 | 说明 |
|---|---|---|
| 真实 access token | 未验证 | 未读取任何真实密钥 |
| 真实素材/正文图/草稿/发布 | 未验证 | 没有调用微信写接口 |
| 账号主体与认证资格 | 未验证 | 只核对官方说明 |
| 微信最终 HTML 清洗 | 未验证 | 未在线比较草稿，外链过滤依据官方契约 |
| 图片像素/解压炸弹 | 证据不足 | 缺显式像素预算，但本轮未制造高内存样本，不升级正式缺陷 |
| 特殊 IPv6/NAT64 SSRF | 证据不足 | 分类器未覆盖所有特殊用途前缀，但未证明当前网络可达绕过 |
| 多 RID 执行 | 证据不足 | 只在当前 osx-arm64 执行，未用 Linux/Windows runner |

## 5. 验证记录

### 5.1 专项测试、构建和依赖

| 验证面 | 结果 |
|---|---|
| WeChat 专项测试 | 65 passed / 0 failed / 0 skipped |
| PluginHost 测试 | 168 passed / 0 failed / 0 skipped |
| CLI `PluginCliIntegrationTests` | 39 passed / 0 failed / 0 skipped |
| `PluginBoundaryTests` | 17 passed / 0 failed / 0 skipped |
| WeChat Release build | 0 warnings / 0 errors |
| NuGet direct + transitive vulnerability audit | 当前 nuget.org 数据源未发现已知漏洞 |

遵守仓库规则，没有运行完整 solution、`ci-full`、`release`、`test-all` 或 `smoke-all` gate。

可复现命令账本（SDK `10.0.100`，仓库根目录执行；输出全部在 `/tmp`）：

```bash
dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj -c Release --no-restore -p:NuGetAudit=false
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release --no-restore -p:NuGetAudit=false
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~PluginCliIntegrationTests
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~PluginBoundaryTests
dotnet build src/Bukit-Plugins/Bukit.Plugin.WechatSync/Bukit.Plugin.WechatSync.csproj -c Release --no-restore -p:NuGetAudit=false
dotnet list src/Bukit-Plugins/Bukit.Plugin.WechatSync/Bukit.Plugin.WechatSync.csproj package --vulnerable --include-transitive
dotnet list src/Bukit-Plugins/Bukit.WechatSyncing/Bukit.WechatSyncing.csproj package --vulnerable --include-transitive
```

独立 clean-archive 复核第一次访问本机 NuGet HTTP cache 时受 sandbox 权限限制；按相同 `dotnet list ... --vulnerable --include-transitive` 命令只读复跑后成功。这是环境噪声，不是插件缺陷。

共享 `main` 快进后，先确认 `6e7d899b..151d28aa` 在 WeChat/Host/`PluginCliLoader`/SSRF/专项测试范围 diff 为空，再从 `git archive 151d28aa` 的干净 `/tmp/bukit-wechat-main-151d28` 重跑 `PluginBoundaryTests`，结果仍为 17/17。共享工作树另有未提交的 Analytics boundary test，直接运行会多发现 1 个无关测试，因此不计入本基线。四路并发重跑一度因多个 `dotnet` 进程争用同一 `obj/bin` 产生临时 `MSB3026/CS0436`；改为串行后 CLI 39/39、架构基线 17/17 均通过，故归类为验证方式噪声，不计入 WeChat 缺陷。

### 5.2 自包含、Native AOT 与协议冒烟

| 项 | 当前结果 |
|---|---|
| osx-arm64 self-contained single-file | 成功，约 18 MB |
| osx-arm64 Native AOT | 成功，约 14 MB |
| self-contained SHA256 | `628142c7b13589442749e0b3f57a87e1993d5f78c6ec7986260f16ef04780516` |
| Native AOT SHA256 | `417b1d95a9abe7109dadac5cb7badb2884dfca634b6febdc63271a60b8a2505c` |
| clean-archive Host 安装包 SHA256 | `c9eceb3e2029937a82f98ef4fa40439e25883145b1583aa4bea60a907868da8b`；lock 记录 `sha256Verified: true` |
| raw `handshake` | exit 0；identity/version/platform/capability 正确 |
| raw `manifest` | exit 0；命令、28 options、权限正确输出 |
| raw `invoke --dry-run` | exit 0；未要求密钥、未调用微信、`candidates=1` |
| 临时真实 SHA 包经 Host/CLI 安装 | list/lock/report/hash/handshake/manifest/dry-run 均成功 |

这些 `/tmp` 产物只证明当前机器的技术可行性，不是正式发行证明，也没有提交到仓库。

发行与协议复现命令：

```bash
dotnet publish src/Bukit-Plugins/Bukit.Plugin.WechatSync/Bukit.Plugin.WechatSync.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:NuGetAudit=false -o /tmp/bukit-wechat-audit-20260721/self-contained
dotnet publish src/Bukit-Plugins/Bukit.Plugin.WechatSync/Bukit.Plugin.WechatSync.csproj -c Release -r osx-arm64 --self-contained true -p:PublishAot=true -p:NuGetAudit=false -o /tmp/bukit-wechat-audit-20260721/native-aot
shasum -a 256 /tmp/bukit-wechat-audit-20260721/self-contained/bukit-plugin-wechat-sync /tmp/bukit-wechat-audit-20260721/native-aot/bukit-plugin-wechat-sync
/tmp/bukit-wechat-audit-20260721/native-aot/bukit-plugin-wechat-sync < /tmp/bukit-wechat-audit-20260721/handshake.json
/tmp/bukit-wechat-audit-20260721/native-aot/bukit-plugin-wechat-sync < /tmp/bukit-wechat-audit-20260721/manifest-request.json
/tmp/bukit-wechat-audit-20260721/native-aot/bukit-plugin-wechat-sync < /tmp/bukit-wechat-audit-20260721/invoke-dry-run.json
```

clean-archive 另以 `PublishAot=true`、`PublishSingleFile=true` 生成 `/tmp/bukit-wechat-package/osx-arm64`，用真实 SHA 写入临时 `plugin.yaml` 后，在 `/tmp/bukit-wechat-package/site` 执行：

```bash
dotnet /Users/ali/mydev/Git/Github/Bukit/src/Bukit-Core/Bukit.Cli/bin/Release/net10.0/bukit.dll plugin validate-config
dotnet /Users/ali/mydev/Git/Github/Bukit/src/Bukit-Core/Bukit.Cli/bin/Release/net10.0/bukit.dll plugin validate-manifest plugins/wechat-sync
dotnet /Users/ali/mydev/Git/Github/Bukit/src/Bukit-Core/Bukit.Cli/bin/Release/net10.0/bukit.dll plugin list
dotnet /Users/ali/mydev/Git/Github/Bukit/src/Bukit-Core/Bukit.Cli/bin/Release/net10.0/bukit.dll wechat-sync sync --output dist --dry-run
```

### 5.3 只读反例矩阵

| 反例 | 实际结果 | 对应问题 |
|---|---|---|
| 发布状态 3 后重跑两次 | 2 draft adds / 2 publish submits / synced 0,0 | WX-P1-01 |
| 32 个并发 cache writer | 无调用失败，最终仅 1 record | WX-P1-02 |
| data placeholder + data-src | data-src 被删，最佳 URL 为 null | WX-P1-03 |
| needs-review + 已过期 | Routed=1，metadata 无 review/expiry | WX-P1-05 |
| 默认参数 + 外链图 | 0 upload，外链原样进入请求 | WX-P1-07 |
| `WechatApiException(48001)` | AddDraft 被调用 3 次 | WX-P2-01 |
| figure 两图 + credit | 第二图与 credit 消失 | WX-P2-02 |
| 同 path 不同 query | 两个 key 相等 | WX-P2-03 |
| emoji 在截断边界 | 输出孤立 `D83D` | WX-P2-04 |
| 不存在的 source + dry-run | 仍报告 candidates=1 | WX-P2-05 |
| 缺 publish_status | 映射 -1 并继续轮询 | WX-P2-07 |
| Host 最小权限 dry-run | network=false 被 exit 2 拒绝 | WX-P2-08 |
| passthrough + process-images | 0 upload，图片处理静默跳过 | WX-P2-10 |
| caption 含 `&quot;` | 生成 `alt="He said "hello""` | WX-P2-11 |
| 2 MiB content body | 完整加载 2,097,152 chars | WX-P3-01 |
| 私网/保留 IP 与 symlink escape | 现有测试拒绝 | 通过 |

## 6. 完整覆盖矩阵

| 审计面 | 状态 | 主要证据/编号 |
|---|---|---|
| CLI -> Host -> plugin 调用链 | 通过 | 临时真实包端到端 dry-run |
| 静态/运行时 manifest | fixture 通过，发行失败 | WX-P1-06 |
| SHA/RID/self-contained | 当前 RID 临时通过，正式缺失 | WX-P1-06 |
| 权限最小化 | 发现缺陷 | WX-P2-08 |
| 输入路径/符号链接 | 通过 | 历史修复与专项测试 |
| 输入 schema/大小/数量 | 发现缺陷 | WX-P3-01 |
| source/type 筛选 | 真实运行可用，dry-run 错误 | WX-P2-05 |
| review/expiry/sync 状态 | 发现缺陷 | WX-P1-05 |
| 28 步 HTML 顺序 | 发现缺陷 | WX-P1-03 |
| figure/畸形 HTML 保真 | 发现缺陷 | WX-P2-02、WX-P2-11 |
| Unicode/字段边界 | 发现缺陷 | WX-P1-04、WX-P2-04 |
| 默认外链图片行为 | 发现缺陷 | WX-P1-07 |
| 图片 option 组合 | 发现缺陷 | WX-P2-10 |
| 本地/缓存图片路径 | 通过 | path/symlink tests |
| 远程图片 SSRF | 通过 | SsrfGuard + redirect handler |
| 图片字节/像素预算 | 字节部分通过，像素证据不足 | WX-P1-04；未验证边界 |
| 媒体键/新鲜度 | 发现缺陷 | WX-P2-03、WX-P3-04 |
| token | 基本通过 | 失效刷新；真实 token 未验证 |
| 草稿请求字段 | 发现缺陷 | WX-P1-04 |
| 发布状态与恢复 | 发现缺陷 | WX-P1-01、WX-P2-07、WX-P3-06 |
| 永久/瞬态错误分类 | 发现缺陷 | WX-P2-01 |
| cache 并发/原子性 | 发现缺陷 | WX-P1-02 |
| 日志/错误体/密钥 | 部分通过，发现缺陷 | WX-P2-06、WX-P2-09 |
| diagnostics/exit/artifact | 基本通过 | Host report masking；错误过度汇总 |
| 专项/Host/CLI/架构测试 | 全绿但覆盖不足 | WX-P3-05 |
| Release build/NuGet | 通过 | 0 warning；无已知漏洞 |
| Native AOT | 当前 osx-arm64 通过 | 临时协议冒烟 |
| 多 RID 正式发行 | 未就绪 | WX-P1-06 |
| 真实公众号写行为 | 因安全边界未验证 | 无真实写操作 |

## 7. 修复路线与依赖顺序

### 阶段 1：阻止重复发布、未审核发布、凭据泄漏与确定性数据丢失

1. WX-P1-01：可恢复发布状态机；草稿和 publish ID 立即原子持久化。
2. WX-P1-02：cache 原子写、锁、合并、损坏隔离。
3. WX-P1-05：draft/publish 独立审核 allowlist 与过期门禁。
4. WX-P2-06、WX-P2-09：统一有界响应和日志脱敏。
5. WX-P1-07：外链图片预检、默认策略和成功条件。
6. WX-P1-04：官方 32/16/20,000/1 MB/1 KB 限制全部前置失败。

阶段 gate：故障注入状态机、双进程 cache、审核矩阵、secret/log golden、官方契约边界；仍不得真实发布。

### 阶段 2：幂等、重试和 API 状态机

1. WX-P2-01：按 HTTP/errcode/步骤分类，隔离不确定副作用。
2. WX-P2-07：发布响应严格 schema 和穷尽状态机。
3. WX-P3-06：分离 article ID 与 URL。
4. WX-P3-02：参数上限和总执行预算。
5. WX-P3-04：远程资源新鲜度策略。

阶段 gate：API fake 覆盖 HTTP/errcode/非 JSON/缺字段/0..6/未来状态；逐步骤验证仅瞬态错误重试，所有 unknown-side-effect 场景可恢复且不新增草稿。

### 阶段 3：HTML、图片正确性与资源限制

1. WX-P1-03：图片源解析前移。
2. WX-P2-02、WX-P2-11：用 parser 替换破坏性正则重建/属性解码。
3. WX-P2-04：Rune/text-element 截断。
4. WX-P2-03：媒体 key 语义与跨模块迁移。
5. WX-P2-10：明确 passthrough 与 process-images 契约。
6. WX-P3-01、WX-P3-03：输入总预算与安全 client 复用。

阶段 gate：完整 HTML corpus/golden 必须结构有效且无节点静默丢失；图片来源/option 四象限、Unicode、输入字节/数量/深度预算和多图连接复用测试全部通过。

### 阶段 4：协议、CLI、诊断和发行包

1. WX-P2-05：共享 planning API，修正 dry-run。
2. WX-P2-08：无网络 preview 或 command-level 权限设计。
3. WX-P1-06：正式多 RID 包、SBOM/checksum、根 manifest 和安装后 smoke。

阶段 gate：每 RID `validate-config / validate-manifest / SHA / handshake / manifest / dry-run`，根资产缺失必须失败。

### 阶段 5：测试、文档和长期维护

1. WX-P3-05：HTML corpus/golden、官方契约 snapshot、反例矩阵自动化。
2. 主线 `guide/` 补充账号资格、权限、限制、失败恢复、重试和发布安全说明。
3. 建立官方契约定期复核；fixture 永远不得替代正式包证明。

阶段 gate：本报告只读反例全部自动化；主线用户文档逐项覆盖账号资格、字段/图片限制、权限、失败恢复与重试；正式根 manifest 和每 RID 安装 smoke 成为不可跳过的发行 gate。

## 8. 兼容与实施边界

- 本次审计没有修改公共 API、manifest、cache 格式、CLI、源码、测试或配置。
- 发布状态机和 cache 修复需要版本化迁移，不能原地解释旧记录。
- media key 修复必须协调 Core 与插件，避免两套键继续漂移。
- command-level 权限若扩展协议，必须作为独立公共契约任务；优先考虑拆无网络 preview。
- 改变 `process-images` 默认值会增加网络/素材副作用，应提供显式 opt-out、dry-run 计划和版本说明。
- “全量”仅指本报告定义的代码、协议、输入、转换、图片、API、缓存、测试和发行边界；真实公众号写行为因安全只读模式不在验证声明内。
