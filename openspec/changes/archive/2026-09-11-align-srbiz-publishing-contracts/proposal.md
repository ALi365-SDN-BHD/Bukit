## Why

SRBiz 的关系导航、公开产物与发布验证存在合同漂移：模板把标题或不存在的 page.slug 当身份，显式/反向关系去重和六条上限不一致，已交付文件曾被补写 robots 或媒体路径。需要以当前有效页面和原始构建产物为依据，建立可重放、失败关闭的站点交付验证。本变更已获用户批准，Core 可靠性修复已完成，不重新接管该工作。

## What Changes

- 统一同文件关系解析：集合、content_model.slug、当前语言及唯一有效路由；无效隐藏并仅输出固定原因计数的 HTML 注释；目标当前标题作为标签。显式企业相关报道优先，反向 URL 排序，去重后最多六条；正向报道对象不设六条上限。
- 固化 VerifiedAt 独立核验日期、方向性源关系和无隐含商业背书的呈现语义。
- 增加站点专用 check/prepare/verify-online Python 工具，复用已有 completed/security/publish/trust 检查；原始业务产物逐字节复制，媒体改名只报告，不修内容或 robots。
- 固定隔离 fixture 冷/暖/增量公开产物一致性；准备包含 Prasarana 的真实 SRBiz 隔离候选，仅只读 Notion 取数，不进行上线。
- Core 只修 guide/dev/architecture.md 的当前真实文档。OpenSpec 是本跨仓变更唯一规范/任务清单。

## Capabilities

### New Capabilities
- `srbiz-content-presentation`: 页面关系身份、排序、计数、诊断与核验日期。
- `srbiz-artifact-consistency`: HTML/JSON/Markdown/search/媒体的一致性与合法差异。
- `srbiz-delivery-verification`: 隔离候选、不可变基线、篡改拒绝及只读线上核验。

### Modified Capabilities

无已有 OpenSpec capability 需要修改；本次新增站点合同不改变 Core 公共协议。

## Impact

Bukit: openspec/** 和 guide/dev/architecture.md。SRBiz-bukit: 关系 partial、Task4 fixture 与 Task4/6 直接检查、现有发布 checker 的兼容消费者、交付验证脚本及用户指南。保留既有 dirty plugin lock、.qoder/ 和分析报告。无 Core 代码、依赖、二进制或运行时改动，无全量/发布 gate 扩张，无 commit/push/deploy/Notion 写入、密钥轮换或通知。候选 staging 是站点交付目录，不是 Core 整站原子 staging。
