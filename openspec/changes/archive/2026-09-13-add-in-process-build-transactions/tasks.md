## 1. 合同与实施准备

- [x] 1.1 审阅并批准本提案的具体替代合同；确认本次不实施自动语言隔离，不合并或清理原分支。
- [x] 1.2 实施前重新确认 main/HEAD/dirty state，保留并发工作；选择实际工作位置，不覆盖旧 worktree 的未提交补丁。
- [x] 1.3 清点 Engine、媒体/正文缓存、图片派生物、内置插件、报告和 metrics 的写入路径及句柄生命周期，明确完整事务写集和外部副作用边界。
- [x] 1.4 按最终候选文件生成 `python3 scripts/checks/codex-workflow.py closure`，包括直接和公共/序列化消费者，解决全部 unmapped；classify 精确命令与资源。多任务执行按治理建立 writer queue。
- [x] 1.5 形成设计资源表的实际调用方清单：逐项记录正式路径解析、读写模式、暂存映射、所有权与句柄释放；确认 Notion 缓存和自定义媒体路径不依赖 Engine CacheDir，闭包覆盖实际修改的 provider 及其 owner 专项。

## 2. 最小实现

- [x] 2.1 公共 Engine 入口引入内部事务所有者；输出、缓存、外部 metrics 资源使用规范化且顺序一致的互斥锁，竞争快速失败。
- [x] 2.2 将实际输出与正式身份分离，保留 clean/no-clean、marker、危险路径、migration 和所有权规则；所有受管写入进入 staging。
- [x] 2.3 在暂存中完成现有单/多语言流程、聚合、报告、安全门禁、manifest 与完成状态；关闭句柄后协调提交并将成功日志移至提交之后。
- [x] 2.4 实现交换失败反向恢复、恢复失败保留旧副本及清晰诊断；成功后清理失败不误报未提交；只清理本次拥有资源。
- [x] 2.5 保持配置、模板及 manifest 增量身份稳定；返回值、日志与持久化报告使用正确正式路径。
- [x] 2.6 dev 排除事务目录事件并只在提交后刷新；核实 deploy 内部构建和直接 Engine 调用均经过公共路径。
- [x] 2.7 实现祖先/后代及真实路径别名冲突的原子登记，覆盖同进程和跨进程；登记后再暂存，独立只读资源与写者互斥；归并同事务嵌套提交目标，拒绝不可安全识别或归并的关系。
- [x] 2.8 显式重定向 Notion readwrite 和自定义媒体实际读写路径，保留媒体 URL、只读缓存命中/缺失及 off 行为；预先解析 metrics JSON/HTML 两个目标；嵌套只读子树内容不变，独立只读缓存不复制交换。

## 3. 验收用例

- [x] 3.1 无 Git、dirty checkout、单语言与多语言仍可构建；不创建语言进程或 worktree，两级并发语义不变。
- [x] 3.2 内容/语言/聚合/报告/门禁失败及取消：比较构建前后正式输出、缓存、manifest、外部 metrics，确认未被修改；临时资源精确清理。
- [x] 3.3 后续目录/报告交换故障可恢复前序目标；恢复再次失败保留 backup；成功后的清理故障保持新产物及正确提交状态。
- [x] 3.4 相同输出、共享缓存、共享外部 metrics 的竞争构建被拒绝；不相交资源可同时进行；失败释放全部已持有锁。
- [x] 3.5 clean/no-clean、缺失 marker、危险/重叠/软链接目标及非自有文件保护符合合同。
- [x] 3.6 首次公开输出与原流程等价；第二次增量构建不因 staging 路径失效；移除内容仍清理自有旧输出；报告无失效临时路径。
- [x] 3.7 dev 成功只刷新一次、失败保留旧输出、不产生监听循环；deploy 构建失败不进入部署提供方（本地替身验证，不真实部署）；直接 Engine 两类入口通过。
- [x] 3.8 用本地替身验证默认/自定义 Notion readwrite、自定义媒体路径和独立 Engine CacheDir：缓存更新后失败/取消时正式文件不变，成功时协调提交；默认 `.cache/notion` 随父目录只交换一次，非自有缓存文件保留。
- [x] 3.9 验证 readonly 命中、缺失、缺失目录、父目录不可写及 off；独立 readonly 不创建/交换目录，嵌套 readonly 子树内容不变，读取与重叠写者互斥。
- [x] 3.10 同进程及双进程竞争验收：`public`/`public/en`、`.cache`/`.cache/notion`、另一输出内 metrics、JSON/派生 HTML 交叉冲突、符号链接祖先及尚不存在末段；大小写别名按文件系统能力验证。不相交执行保持并发，冲突检查与占有间不能同时获准；拒绝请求不触碰正式目标。

## 4. 专项证据与收尾

- [x] 4.1 最终文件变化后更新 closure/classify；按 HEAD、闭包、精确命令、环境指纹及 SDK 检查 GREEN cache，未命中才运行相应证据，不保存环境值。
- [x] 4.2 串行运行 `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj` 和 `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj` 并记录 GREEN cache；若最终闭包增加 owner 项目，先明确其专项范围。
- [x] 4.3 完成一次专项复审，修复 Critical/Important 后仅复审受影响部分；记录阶段 metrics。
- [x] 4.4 更新当前 guide 中构建失败、竞争、临时资源和 dev 边界说明；保持无需 Git 与进程内执行的说明一致。
- [x] 4.5 生成最终 delta-only review-scope，完成一次统一复审及 metrics report；运行严格 OpenSpec 校验与 `git diff --check`，披露未执行跨平台、断电和性能证据。
- [x] 4.6 仅在实施与验收完成后同步正式 spec 并归档；不得因提案通过或预算不足勾选完成。提交、推送、合并和发布保持单独授权。

## 实施验收记录（专项复审修复中）

运行时与 guide 已实现，未提交或发布。逐项验收以实际测试为准：生命周期用例覆盖正文失败/取消、单语言投影/报告、多语言聚合和媒体门禁，并比较正式 output/cache/manifest；外部 metrics 的失败保护由事务取消测试独立覆盖，不声称每类 Engine 故障都重复 metrics 用例。Notion 采用本地缓存写集模拟及 provider owner 的 HTTP handler/cache 测试；媒体另有实际本地 HTTP 下载成功/失败用例，不声称访问真实 Notion API。Dev 的失败零刷新/下一次成功一次刷新与事务事件过滤采用本地测试；Deploy 的实际 Engine 异常直接传播，provider 不进入，未改变为捕获异常或伪称命中非零返回日志分支。

最终 Engine 2519/2519、CLI 1021/1021、Content.Notion 36/36、Content 529/529 通过；父 `.cache` 仅安装一次及非自有缓存保留、已有 readonly 子树不变与禁止 staged 修改均有直接回归。证据按各 owner 实际依赖闭包缓存，不因仅 Engine 测试或文档变化重复无效专项。跨平台、断电、真实部署及性能证据未执行。一次专项复审及限定复审已通过，controller 最终 delta review 已通过，四个 owner cache 独立复核 HIT；阶段 metrics report 已执行。4.6 已完成正式 spec 同步与归档，requirements 正文与归档前已审阅内容一致。
