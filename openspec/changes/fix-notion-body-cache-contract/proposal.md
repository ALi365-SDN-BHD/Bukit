# Bind the Notion body cache and fingerprint to the renderer contract

## Why

内置 Notion 来源有两层复用，都只认 `last_edited_time`：

1. **正文缓存**（`NotionCacheManager`，`.cache/notion/pages/<id>.json`）：`version == 1`、`lastEditedTime` 字符串相同、签名链接未过期即复用。缓存不记录渲染器版本。`Bukit.Notion/Rendering` 自 2026-05 起有 7 次提交，其中至少包括正文投影和媒体输出对齐的修复（如 623a7f4f）；缓存格式从未升级，未编辑的文章会一直使用旧渲染结果。这个问题现在就存在，与是否使用 `--clean` 无关。
2. **稳定正文指纹**（`NotionContentSource`，`bodyFingerprint = "{lastEditedTime}:content={on|off}"`）：未启用媒体本地化的站点，页面内容哈希由它决定（`IncrementalBuildEngine.TryComputeStableContentHash`）。即使正文缓存被修好、重新拉取了正文，只要 `last_edited_time` 没变，页面仍会被判为 `unchanged`，`dist/` 保留旧 HTML。

另外，Notion 的 `last_edited_time` 截断到分钟。同一分钟内先抓取、后编辑，两层都会一直保留编辑前的内容，直到另一分钟再编辑。

## What Changes

- 新增 `NotionBlocksRenderer.ContractVersion`（`internal const int`，`Bukit.Content` 已通过 InternalsVisibleTo 访问该程序集的内部成员，因此不增加公共 API）。渲染输出语义变化时必须递增。
- 渲染器契约测试：固定的块 JSON 语料必须覆盖 `NotionBlockRendererRegistry` 注册的每一种块类型（需新增内部枚举入口），以及注册表之外的列表项分组、富文本注解和未知块回退；语料渲染输出的哈希按「版本号 → 哈希」只追加记录在测试数据中。输出变化而版本号未变时测试失败，失败信息提示递增版本号并追加记录。
- **统一使用 Notion 服务器时钟**：传输层把响应的 `Date` 头交给调用方（`NotionClient.SendAsync` 目前丢弃响应头）。缓存的抓取时间和指纹的落定判断都用它，缺失时才用本机 UTC；`NotionContentSource` 注入时钟，便于测试。本机时钟快于 Notion 时，两处判断不会因此出现不一致。
- **正文缓存格式 2**：新增 `renderer`（契约版本）和 `fetchedAt`。`fetchedAt` 取本页正文抓取过程中**最早**一次响应的 `Date`（正文由多次分页请求组成，取最早值才能保证整篇都是落定后读取的）。
- **readwrite 模式复用条件**：格式为 2；`renderer` 等于当前契约版本；`lastEditedTime` 非空且字符串相同；签名链接未过期；且已「落定」，即 `fetchedAt >= floor_minute(lastEditedTime) + 65s`（一分钟加 5 秒时钟余量）。`lastEditedTime` 为空的条目在 readwrite 下不复用。格式 1 条目一次性失效。
- **readonly 模式**：保持离线冻结语义（未命中即报错、允许过期链接），只要求本引擎能解析的格式且 `lastEditedTime` 相同即复用，忽略渲染器版本与落定条件。没有 `renderer` 字段或版本不一致的条目计入一条汇总警告。
- **稳定正文指纹**改为 `"{lastEditedTime}:renderer={ContractVersion}[:unsettled]:content={on|off}"`（`content=` 保持在末尾，现有断言 `EndsWith(":content=on")` 不受影响）。返回该 `last_edited_time` 的数据库查询响应的 `Date` 早于 `floor_minute(lastEditedTime) + 65s` 时，加入 `:unsettled`。页面从未落定变为落定时指纹变化，强制再渲染一次，之后稳定复用。
- `last_edited_time` 为空时不再输出稳定指纹（现在以 `pageId` 代替，会让指纹永远不变），页面内容哈希改为根据正文计算。
- readonly 构建复用旧契约正文时，指纹仍写当前契约版本；之后在同一份 `dist/` 上切回 readwrite 并用 `--no-clean` 时，可能跳过这些页面。文档写明从 readonly 切回 readwrite 时需要 `--clean`。
- 修改 `in-process-build-transactions` 中「Notion body fingerprints reflect the effective content mode」要求：加入渲染器版本和落定标记，并把「manifest 结构 MUST 保持现状」改为本要求自身不改变 manifest 结构的表述。
- 用户与开发文档（含「改渲染器就递增契约版本」的流程）、中英马 CHANGELOG 同步。

## Impact

- 受影响代码：`Bukit.Notion/Rendering/NotionBlocksRenderer.cs`、`Bukit.Content/Notion/NotionCacheManager.cs`、`NotionContentSource.cs`（指纹），以及让渲染器暴露响应 `Date` 头所需的传输层改动（`Bukit.Notion/Transport`）。
- 受影响测试：Notion 缓存测试、Notion 来源指纹测试（`NotionContentSourceTests` 中 `EndsWith(":content=…")` 的断言因字段顺序保持不变）、INC-09 等增量测试；新增契约语料与契约测试。
- 规格：新增 `notion-body-cache` 能力；修改 `in-process-build-transactions` 的 Notion 指纹要求。
- 公共契约：无公共 API 变化。`NotionClient.SendAsync` 与 `NotionBlocksRenderer` 已列入公共 API 基线，传递 `Date` 头与 `fetchedAt` 只能使用 internal 成员或内部重载，不改变公共签名。Notion 正文缓存格式 1 → 2（内部缓存）。
- 兼容性：升级后第一次 readwrite 构建会重新拉取全部 Notion 正文一次（约 3,500 篇，每篇至少一次块请求；按 Notion 约 3 次/秒的公开限速，至少约 20 分钟，实际取决于站点 `MaxRps` 和子块分页）。之后只在契约版本递增时再全量重拉。未启用媒体本地化的站点首次构建会全量渲染。
- 跨仓影响：SRBiz-bukit 升级后首次构建时间明显变长，需要在发布说明中写明；站点仓库无需改配置。
- 与其他变更的关系：与 `fix-media-signed-url-identity`、`harden-incremental-invalidation` 相互独立，实施顺序不限。
- 用户批准范围：本提案只批准起草；实施、提交、发布需单独确认。

## Non-Goals

- 不在缓存中保存图片本地化后的 HTML，也不改变签名过期后的整页重拉策略。
- 不回收已删除页面的缓存文件。
- 不让 Bukit 引擎版本参与 Notion 缓存失效（避免每次升级都全量重拉）。
