## Purpose

定义网站业务产物在 HTML、JSON、Markdown、搜索和媒体之间可核验的一致性边界，区分允许的表示差异与真实遗漏。验收复用已有失败关闭规则，保留原始字节和公开路径，确保冷构建、暖缓存及增量构建给出相同公开结果。

## ADDED Requirements

### Requirement: 复用失败关闭门禁
check MUST 复用已识别完成状态、security、publish、trust 必需校验，保留现有发布 checker 接口与错误规则；所有检查完成前不得输出成功。

#### Scenario: 复用失败关闭门禁验收
- **WHEN** 构建状态未知、失败或必需报告校验失败
- **THEN** 拒绝产物而非由旧成功报告推断就绪

### Requirement: 公开身份和正文一致
HTML、JSON、search 的身份、标题和路由 SHALL 一致；资讯/企业正文比较排除导航等 chrome，Markdown 只比较支持的文本/链接语义。

#### Scenario: 公开身份和正文一致验收
- **WHEN** 同一内容输出多种表示
- **THEN** 任何非允许的内容或路由偏差被报告

#### Scenario: 正文或Markdown链接丢失
- **WHEN** JSON 正文在 HTML 业务正文中遗漏，或 Markdown 支持的链接文本或目标改变
- **THEN** check 失败并定位源页面，不把导航等 chrome 差异算作正文错误

### Requirement: 合法表示差异
HTML 推导反向关系与 search snippets 的有意差异 SHALL 被允许，JSON 保留源关系方向；无效关系隐藏仅限定 HTML 导航。

#### Scenario: 合法表示差异验收
- **WHEN** 企业 HTML 含反向报道而源 JSON 没有反向补边
- **THEN** 不把该差异判为错误，也不改写 JSON

### Requirement: 媒体与字节保真
图片 MUST 存在并核对复制哈希；等字节媒体改名只报告，不重写原始构建路径、正文或媒体。

#### Scenario: 媒体与字节保真验收
- **WHEN** 旧新媒体文件字节相同但路径不同
- **THEN** 报告迁移差异，候选保留原始业务字节

#### Scenario: 图片缺失或复制损坏
- **WHEN** 业务正文引用的本地图片不存在，或公开包图片与原始构建哈希不一致
- **THEN** check 或 prepare 失败，不补图、不改名、不改写正文

### Requirement: 空列表索引边界
企业列表 SHALL 仅通过 noindexWhenEmpty 处理空结果；非空企业总列表和马来西亚列表不得沿用交付后补写 noindex。draft/删除撤回与 expiry/noindex 索引排除 MUST 区分。

#### Scenario: 空列表索引边界验收
- **WHEN** 空列表、非空列表或过期内容构建
- **THEN** 空列表 noindex；非空无额外补写；过期不等同于删除文件

### Requirement: 可重放一致性
固定 fixture SHALL 比较冷、暖、增量构建全部公开路径和内容，只有明确内部运行报告可排除；每轮固定相同内容和构建时间前提。

实施证据更新（独立 Core 时钟验证后续）：经用户授权，网站页脚改用已有 site.build_year，固定 fixture 以 candidate4 原输入加该单行修复派生，并记录原始/有效哈希，未修改原候选封存。源版本 Engine 在既有内部 TimeProvider 入口固定 UTC 2040-12-31T16:00:00Z；root 与 /docs 各 131 个公开文件的冷、暖、增量路径及 SHA256 相等，真实页脚显示时区年份 2041。仅将构建时间回拨一小时，真实页脚为 2040，增量与同时间冷构建全公开哈希相等；不再依赖附加探针或公开字段排除。完整 Engine 专项 2410 通过。此证据补齐原 4.2 固定构建语义时间条件，不改变本条 SHALL 合同；不要求冻结 HTTP deadline、性能计时或内部报告时钟。锁定 Native AOT CLI 的既有及本次网站专项与源版本证据分开记录，CLI 没有新增时钟参数，不宣称历史 Notion 可重建或网站已上线。详见 /Users/ali/.codex/worktrees/8f41/Bukit/.cache/srbiz-clock-followup/REPORT.md。

#### Scenario: 可重放一致性验收
- **WHEN** 相同 fixture 以三种缓存状态构建
- **THEN** 公开结果一致，不能以测试数量替代证据
