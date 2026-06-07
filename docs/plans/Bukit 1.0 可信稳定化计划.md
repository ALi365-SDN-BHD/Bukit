# Bukit 1.0 可信稳定化计划

## 摘要

Bukit 1.0 的目标不是继续扩展功能面，而是把“可以放心用于正式网站”的信任基础做实。`BukitJalil` 明确不纳入本次 1.0 范围；除此之外，Bukit 的核心与对外能力都要进入 1.0 规划，但发布门槛分层处理：核心面必须先达到完整 GA 标准，生态面可以继续随产品演进，但必须有清晰的支持等级、兼容承诺和风险边界，不能再以模糊状态对外。

当前仓库已经具备不少 1.0 基础：诊断码、输出路径安全、远程主题锁定、插件能力约束、构建报告、安全 fixture、smoke/security 脚本、架构治理文档都已存在。当前 `dotnet test bukit.slnx -c Release --no-restore` 可以通过，但 `bash scripts/smoke.sh Release` 仍会因为 starter/example、doctor 规则、模板清单之间不一致而失败，这说明 1.0 的首要问题之一是“契约、示例、文档、治理规则尚未完全对齐”。

## 关键变更

### 1. 定义并冻结 1.0 兼容面
- 发布一份统一的 Bukit 1.0 契约矩阵，覆盖：
  - `site.yaml` 配置契约
  - `ContentItem` 与内容元数据模型
  - 路由优先级与派生规则
  - 主题接口与模板能力声明
  - 插件接口、协议、失败语义
  - `.bukit/` 构建与审计产物格式
  - 诊断码与 CLI 退出码
- 将所有公开能力划分为三类：
  - `GA-locked`：1.0 冻结面，后续仅允许兼容性演进
  - `GA-limited`：正式支持，但能力边界较窄，需明确约束
  - `Experimental`：不纳入 1.0 稳定承诺
- 在 1.0 前允许一次集中式 breaking cleanup；清理完成后，核心契约严格按 SemVer 冻结。

### 2. 先消除“仓库自相矛盾”
- 把 starter theme、example sites、fixtures、README、guide、skills、doctor/docs-check/template-sync 的预期统一到同一份契约上。
- 将“仓库自带示例无法通过官方 smoke/doctor”视为 1.0 阻塞项。
- 修复当前已暴露的典型不一致：
  - starter/example 与模板能力清单不一致
  - doctor 规则与实际模板上下文能力不一致
  - 文档、技能说明、CLI 帮助、示例配置对同一行为描述不同
  - 主题/插件示例依赖未正式承诺的行为
- 建立一条规则：示例、starter、skills 只能展示当前支持等级明确覆盖的能力。

### 3. 配置契约稳定
- 冻结 `site.yaml` 的字段语义、默认值、覆盖优先级、校验规则、弃用策略。
- 给每个公开字段定义生命周期状态：稳定、限制使用、弃用、实验。
- 所有配置 breaking cleanup 必须配套：
  - 明确迁移说明
  - 兼容窗口或迁移器
  - fixture/文档/技能同步更新
- `config check`、`doctor`、`schema` 输出要成为配置契约的官方判定入口，而不是“部分规则散落在实现里”。

### 4. 内容模型稳定
- 冻结统一内容模型：`ContentItem`、reserved meta keys、字段归一化、schema 校验结果语义。
- 明确 Markdown / Notion / composite source 在以下方面的对齐规则：
  - `title`、`slug`、`type`、`collection`、`language`、`publishAt`
  - tags/categories/i18n 等 meta 字段
  - custom fields 如何进入 `page.fields.*`
  - schema default / validate / strict fail 行为
- 将“内容模型变化”视为公开接口变化，必须通过契约测试和迁移说明管理。

### 5. 路由行为稳定
- 冻结路由解析优先级：显式 route 覆盖、collection、permalink、theme template accepts 的关系。
- 冻结 list/pagination/taxonomy/archive 等派生路由的输出规则与冲突策略。
- 明确“哪些行为绝不会 fallback”，避免用户依赖隐式规则。
- 为路由产物建立 golden inventory 测试，确保同样输入得到稳定的 URL、outputPath、template 绑定结果。

### 6. 主题接口版本化
- 正式定义主题 1.0 契约，包括：
  - `theme.yaml` 字段与语义
  - `extends` 继承规则
  - `min_engine_version`
  - `layouts/bukit.templates.yaml` 模板能力声明
  - required templates / kind accepts / page templates
  - theme source 锁定与缓存复现策略
- 对主题接口做版本化管理：
  - 主题声明兼容的 engine 范围
  - engine 对低版本主题的兼容策略
  - 主题 breaking changes 必须通过 manifest version 或 capability gate 控制
- `theme doctor`、`theme info`、`theme preview` 的输出要能直接帮助定位接口不兼容点。

### 7. 插件接口版本化
- 正式定义插件 1.0 契约，包括：
  - 内建插件生命周期
  - source-generated 插件接口
  - external protocol plugin 请求/响应 schema
  - handshake/version negotiation
  - capability names 与 hook 权限映射
  - env isolation、output limits、stale cleanup、错误语义
- 为插件协议与 capability 建立明确版本策略，避免“协议字段继续长但没有冻结规则”。
- 区分核心插件机制与开放插件生态：
  - 核心插件机制必须达到 GA
  - 更开放的生态分发/发现能力可以保留较低支持等级，但必须说明边界

### 8. 构建结果可复现、可审计、可回滚
- 定义 1.0 的可复现标准：
  - 同一输入树、同一版本、同一配置，输出文件集稳定
  - 路由清单稳定
  - `.bukit/` 报告结构稳定
  - hash/manifest 行为稳定
- 把 `.bukit/` 产物提升为正式审计面，至少包括：
  - `build-report.json`
  - `routes.json`
  - `assets.json`
  - `incremental-manifest.json`
  - `security-report.json`
  - `seo-report.json`
  - `geo-report.json`
- 定义发布产物包与回滚依据，让每次发布都能被比较、审计、回退。
- 将并行构建/共享输出争用视为稳定性问题，明确 CI 编排规则，避免 `dotnet` 输出或构建目录争用导致偶发不稳定。

### 9. 错误信息明确可定位
- 将稳定诊断码扩展到所有 GA-locked 用户路径，不允许关键失败类别仍落为“无 code 的普通异常”。
- 标准化错误呈现格式：
  - 诊断码
  - 问题对象/路径/模板/字段位置
  - 原因摘要
  - 修复建议
- 统一 Config / Content / Render / Plugin / Build / SEO / GEO 的错误体验，减少“同类问题不同命令输出完全不同”的情况。

### 10. 安全边界完善
- 将现有安全能力收敛成正式 1.0 安全边界文档，覆盖：
  - 配置路径与输出路径逃逸
  - 路由路径安全
  - 主题继承与主题名清洗
  - 插件 entry/capability/env/output 边界
  - 远程拉取、SSRF、媒体下载
  - 敏感文件泄漏、危险 URL 输出
- `security-regression.sh` 升级为发布阻塞门，不再只是补充性校验。
- 对任何新增能力要求先定义边界，再允许进入正式支持面。

### 11. 升级兼容策略清晰
- 为 1.0 建立统一升级策略：
  - 什么算 breaking change
  - 什么可以通过 warning + deprecation 过渡
  - 什么必须 major bump
  - 文档、示例、skills、fixture 在版本切换时如何同步
- 提供“从 public preview 到 1.0”的迁移指南，覆盖：
  - 配置字段变化
  - 路由/模板规则变化
  - 主题 manifest 变化
  - 插件协议变化
  - 构建报告路径或 schema 变化
- 发布节奏上明确：1.0 前做一次集中收口，1.0 后只做兼容演进。

### 12. 自动化测试与回归体系升级
- 保留当前大规模单测/集成测试基础，并补齐契约型回归：
  - 配置契约快照测试
  - 内容归一化与 provider parity 测试
  - 路由清单 golden tests
  - 主题 manifest / template capability 兼容测试
  - 插件协议版本协商回归测试
  - `.bukit/` JSON schema 校验测试
- 将以下命令提升为 release gates：
  - `dotnet test bukit.slnx -c Release --no-restore`
  - `bash scripts/smoke.sh Release`
  - `bash scripts/smoke-all.sh Release`
  - `bash scripts/security-regression.sh Release`
- 增加稳定性测试：
  - clean build 两次对比
  - clean build vs incremental build 对比
  - 重复 smoke/stress 以捕获 flaky 行为
  - 旧版本输入在新引擎下的兼容性回归

## 需要明确版本化的公开接口

- `site.yaml` schema、默认值、override precedence、validator 语义
- `ContentItem` 对外行为：reserved meta、field normalization、schema error 语义
- routing contract：优先级、派生规则、冲突处理、output encoding
- theme contract：`theme.yaml`、`bukit.templates.yaml`、required templates、`extends`、`min_engine_version`、remote lock 行为
- plugin contract：hook 名称、protocol schema、handshake/version negotiation、capabilities、env policy、output 限制、失败语义
- `.bukit/` 报告与审计 JSON schema
- diagnostic code 范围与 CLI exit code mapping

## 测试与验收场景

- 所有官方 starter/example site 必须能通过 doctor、build、smoke，不能依赖人工修补。
- 所有 fixture 站点继续覆盖并稳定通过：
  - output safety
  - route security
  - plugin policy
  - dotfile leak
  - i18n
  - taxonomy
  - incremental build
  - component/theme validation
- 必须新增以下验收：
  - clean build 输出与 repeated build 输出一致
  - clean vs incremental 输出一致
  - 远程主题 lock 与 plugin stale output cleanup 行为正确
  - 旧支持输入在 1.0 引擎下要么保持兼容，要么给出受控迁移路径
  - 所有关键失败都有稳定诊断码与定位信息

## 假设与默认决策

- `BukitJalil` 不属于 Bukit 1.0 范围。
- 允许在 1.0 前做一次集中 breaking cleanup，之后核心契约冻结。
- 核心构建面必须先达到完整 GA；生态能力可以继续存在，但必须标注支持等级，不能继续模糊承诺。
- 第一执行阶段应优先修复“契约、示例、文档、治理规则漂移”，而不是新增功能。
- `smoke.sh` 当前失败暴露的是 1.0 级信任问题，应作为第一批治理对象处理。
