# Tasks

唯一实施清单。只有取得实际证据的事项才勾选；证据位置记录在括号内。设计见
`design.md`，验收要求见
`/Users/ali/.opencode/plan/2026-11-01-bukit-core-five-findings-remediation-plan.zh-CN.md`。
外部证据目录：`/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-five-findings/`。

## 0. 准备

- [x] 只读核对 HEAD `de9a5ebf`、干净工作树和治理约束；确定候选文件、消费者、公共/序列化契约与 change 路径。
- [x] 在仓库外状态目录初始化队列；登记 prepare-policy、contract-docs、package-security、package-notion、package-incremental、final-review 的写者与报告位置（`queue.json`）。
- [x] 映射准备任务：对策略文件执行 closure/classify，确认已映射且 owner 命令为 `bash scripts/checks/codex-workflow-self-test.sh`。
- [x] acquire `prepare-policy` 后补齐 Notion/部署指南、事务/i18n 主规范、change 文档、公开表面 API-01/API-02（绝对根）映射与 `dotnet-serial` 分类。
- [x] 策略任务转 `testing`，执行 owner self-test（exit 0，27314 ms），转 `review_wait` 完成专项审查，转 `done`（`reports/prepare-policy-review.md`）。
- [x] 为 change 文件生成 closure/classify，确认 `unmappedFiles=[]`；acquire `contract-docs` 后创建 proposal/design/tasks 和五份 delta。
- [x] 执行 `openspec validate fix-core-five-findings --type change --strict --no-interactive` 并核对五项修复、首轮 1–6、复审 1–2、第三轮 1–11 均有 requirement/scenario/任务承载（`reports/openspec-validate-contract.txt` exit 0）。
- [x] 为业务包重新生成实际闭包并 classify：47 个变更文件、closure 927、`unmappedFiles=[]`；五项目命令、API-01/02、两个 self-test 与 change 验证全部登记（`/tmp/closure.json` 与会话记录）。

## 1. 安全修复包（问题 1、2、4）

### 1.1 回归（先取得 RED）

- [x] 问题 1：来源/写目标冲突、公开 provider 正反例、显式多语言原项目保护、内置 Notion 可写缓存发现，覆盖 SRC-01–SRC-18（`reports/red-evidence.md`：10 Engine + 4 CLI 失败）。
- [x] 问题 2：跨项目部署、真实 Git 继承环境、作用域 origin、私有 remote、条件 include、URL 重写，覆盖 DEP-01–DEP-18（`reports/red-evidence.md`：19 失败）。
- [x] 问题 4：JSON 转义、深度边界、输入解码异常、扫描未完成的 strict/warn/deploy 路径，覆盖 PRI-01–PRI-16（`reports/red-evidence.md`：8 Engine + 4 CLI 失败）。
- [x] 运行登记完整专项，记录目标断言的原实现失败；正向保护场景单独记录，不伪造 RED（`reports/red-evidence.md`）。

### 1.2 实现

- [x] 实现真实来源根/内置可写缓存发现与传递、共享冲突检查、worktree 原项目正式目标预检、clean 全目标预检。
- [x] 实现项目根目录传递、作用域 origin、临时仓库初始化/私有 remote 配置后的同上下文前检、fetch/checkout 与共享 Git 环境隔离；同步 fake git 与旧 clone 断言；默认分支部署改用 FETCH_HEAD。
- [x] 实现共享 JSON 解码、窄边界输入解码异常归类、完成性结果，接入两侧失败映射。
- [x] 更新 `guide/user/05-markdown-content.md`、`guide/user/06-notion-content.md`、`guide/user/13-deploy-github-pages.md` 的迁移与失败模式说明。
- [x] 运行实际闭包的完整专项（含 Content.Notion）、API-01/API-02、report-inventory self-test 及已登记文档验证，记录 GREEN（`reports/final-test-results.txt`、`reports/api-check.txt`）。
- [x] 完成一次专项审查，关闭 Critical/Important，记录证据并按队列移交（`reports/package-security-review.md`：Critical 0 / Important 1 resolved / Minor 1 open）。

## 2. Notion 稳定性包（问题 3）

- [x] 添加来源级限速、正文并发、客户端释放、分类关系混合缓存取消、唯一等待者取消后延迟失败、Composite 最终构造失败、空正文预取回归，覆盖 NOT-01–NOT-16 并取得对应 RED（`reports/red-evidence.md`）。
- [x] 实现加载级共享客户端、正文并发门、body store 所有权、关系任务全退出路径收束、按任务身份清除失败缓存。
- [x] 接入加载结果绑定的关系 resolver；把 Composite 最终构造纳入清理；保存成功的空正文预取；更新 `guide/user/06-notion-content.md` 的限速/并发/模式说明。
- [x] 运行 Content、Content.Notion 完整专项；按实际闭包执行 API 专项和已登记文档验证（`reports/final-test-results.txt`、`reports/api-check.txt`）。
- [x] 完成一次专项审查，关闭 Critical/Important，记录证据并移交（`reports/package-notion-review.md`：Critical 0 / Important 0 / Minor 1 open）。

## 3. 增量一致性包（问题 5）

- [x] 添加真实冷构建→首次热构建 RED、模板变化恢复复用、旧 manifest 兼容、Notion 模式切换、Markdown A→B 时序/事务回归，覆盖 INC-01–INC-11（`reports/red-evidence.md` 及新增 Engine 集成测试）。
- [x] 先补 Notion 有效 `RenderContent` 指纹和 Markdown 同次安全读取的 body-only 核对，确认模式变化与 A→B 拒绝断言。
- [x] 再统一 manifest 保存/比较口径，验证 inline HTML、无指纹回退、媒体限制和旧条目自然收敛；保持正文读取次数约束。
- [x] 更新 `guide/user/05-markdown-content.md` 的正文变化重试说明、`guide/user/06-notion-content.md` 的模式/增量说明及事务 delta。
- [x] 运行 Engine、Content、Content.Notion 完整专项、API-01/API-02 及已登记文档验证（`reports/final-test-results.txt`、`reports/api-check.txt`）。
- [x] 完成一次专项审查，关闭 Critical/Important，记录跨包共享文件导致的证据失效与重新取得结果（`reports/package-incremental-review.md`：Critical 0 / Important 0 / Minor 0）。

## 4. 最终统一审查

- [x] 生成一次 `review-scope`，检查来源保护、事务/指纹/manifest、Git 条件配置/实际传输/隐私阻断、Notion 生命周期/空正文/增量模式的跨包交点（`reports/final-review.md`）。
- [x] 检查公共 API、旧 manifest、未覆盖变更、唯一 `tasks.md` 证据链接及失效情况；核对后续 Engine/Content 修改是否使先前 API 证据失效（最终 API 运行发生在最后一次源码写入之后）。
- [x] 复用有效证据，只对失效部分补跑已登记专项和必要构建前提（`reports/final-test-results.txt`）。
- [x] 对照首轮 1–6、复审 1–2、第三轮 1–11 确认均有实际交付证据，关闭全部 Critical/Important 并记录 Minor（`reports/final-review.md`）。
- [x] 汇总一次 `metrics report`，交付第 10 节证据索引与明确范围内的验收结论（`reports/metrics-report.json`）。

## 4.1 复审补齐（review-gap-closure）

- [x] 先 acquire `review-gap-closure` 队列任务，再写入本轮补齐内容。
- [x] NOT-01：加入跨页面许可时间线证据（fake clock + delay 记录，共享客户端下 4 个后续许可严格递增、跨页连续）。
- [x] NOT-08：加入正常释放、加载失败、Composite 投影/加载失败三条客户端恰好释放一次（handler Dispose 计数）证据。
- [x] INC-09：两种稳定模式分别与独立全量渲染站点比较公开 HTML 全等，而不只断正文有无。
- [x] SRC-08：双 Markdown 来源独立构建与 clean 成功、来源文件保持的对照。
- [x] SRC-12：独立目录注入多层 Composite 成功构建并保留来源的对照。
- [x] PRI-13：64 层无 ID 内容通过，65 层 `.json`/`.jsonld` 与畸形无 ID 均为未完成且不伪称泄露。
- [x] DEP-14/DEP-15：local include origin、无效首值、system 匹配重写三项对照。
- [x] 记录流程偏差：package-security/notion/incremental 的 queue acquire 在初版收尾时补做（单写者由会话独占保证，冲突计数 0）；本轮 gap 任务已先 acquire 后写入。
- [x] 重跑受影响专项、API-01/02、self-test 与 openspec validate 并更新缓存/指标（`reports/final-test-results.txt`、`reports/api-check.txt`）。

## 4.2 审计复审（audit-remediation）

审计报告：`core-execution-audit-s7hd7558/final-audit.zh-CN.md`（PARTIAL，Important 14 / Minor 3）。
本轮先 acquire `audit-remediation` 再写入。

### 实现缺陷

- [x] I-01 Notion 可写缓存 `pages`/`relations`/scope 子目录别名纳入 discovery 与共享 preflight；pages/relations 链接、相对路径真实混合 provider 拒绝，原稿与既存缓存不变。
- [x] I-02 部署 fetch 显式使用 `refs/heads/<branch>`；同名 branch/tag（keep-history 与 depth 1）回归证明取分支头。
- [x] I-03 URL 重写拒绝诊断不再回显 key/base URL；另一套 userinfo 密码不进入错误。
- [x] I-04 已终态失败/取消条目在读取时按身份移除并重新尝试；旧任务回调不删除新条目。
- [x] I-05 预取种子改用 `ExecutionAndPublication`；同键并发首读全部返回种子且不新增请求。
- [x] I-06 Composite 释放继续遍历所有子 store，遍历后传播首个异常；嵌套最终构造失败仍释放后续 Notion store。
- [x] I-07 ContentLoad 的日志与返回构造纳入所有权保护区；日志异常释放 body store 且保留原异常。
- [x] M-01 仅将 `GetString()`/`JsonProperty.Name` 解码异常归类为未完成，其他 `InvalidOperationException` 保持原失败路径。
- [x] M-02 指南 05/06 明确不透明自定义 provider 隐藏磁盘根不在可识别来源保证内。
- [x] M-03 保留初版 acquire 顺序偏差记录，不以最终 done/冲突 0 倒推历史合规；本轮及后续先 acquire 再写。

### 验收证据

- [x] I-08 显式 CLI worktree 冲突（无 marker、marker+clean）、cache/media/metrics 独立冲突、安全 metrics CLI 完成；公开 provider 安全 Composite、Notion cache 相等/父/子/子目录链接/相对别名真实构建拒绝。
- [x] I-09 部署替身在真实 URL 解析后路由到独立 A/B 本地目标；跨项目与 CLI `--config` 部署只访问 B，A refs/工作树/索引不变。
- [x] I-10 真实 Composite 链覆盖 query→taxonomy→summary prefetch→schema fallback→lazy body：单 factory、跨阶段许可序列、最终一次释放。
- [x] I-11 接纳后排队取消、取得许可后渲染失败再成功、活动读取期间 dispose 的 owned client 0→1 释放计数、关系调度非取消异常等待与保留。
- [x] I-12 未完成扫描进入 strict（输出/缓存/manifest 字节不变）与 65 层部署 `.json`/`.jsonld` 阻断；warn 既有证据保留。
- [x] I-13 Markdown A→B/恢复全程 `Incremental=true`，比较 dist+cache+manifest；模板变化后本轮重渲染、下一次稳定构建复用；Notion 稳定步骤按目标 route 断言零渲染。
- [x] I-14 公共表面规则登记 API 基线/schema/18 项目/API 工具/共享构建配置；缓存记录纳入完整输入、相关环境状态与组合工具链版本；策略 owner self-test 通过。
- [x] 重跑 Engine/CLI/Content/Content.Notion/Architecture、API-01/02、report-inventory、workflow self-test、OpenSpec strict validate 并重记缓存（`reports/audit-remediation-tests.txt`、`reports/api-check.txt`）。

## 5. 已知边界

- Windows 真实/fake Git 传输替身未在本机执行；Windows CI 需确认 PowerShell 替身与 batch shim（Minor，记录于安全包专项审查）。
- 任意自定义 provider 或不透明包装器的隐藏磁盘根不在可识别来源保证内（设计边界，已写入用户指南）。
- 主规范同步与 change 归档已于 2026-11-01 完成：五份 delta 合并/新建到 `openspec/specs/`，change 归档为本目录；`openspec validate --all --strict` 21/21 通过。
