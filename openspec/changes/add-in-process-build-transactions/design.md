## Context

旧 SiteEngine.BuildCoreAsync 在内容获取前调用 BuildPlanner.Plan，后者会清理正式输出并写入 started 状态。语言变体直接写目标目录；门禁之后才保存 pending manifests。因此 manifest 延迟保存尚不能保护先前已被覆盖的页面。

build、deploy 内部构建、dev 初始及增量构建最终都使用 SiteEngine。事务必须位于公共 Engine 路径，避免只保护一个 CLI 入口。

## Decisions

### 1. 单一进程内执行路径

保留现有 BuildVariantAsync、多语言聚合和内容获取。增加一个内部事务所有者即可，不引入多实现接口、可选隔离模式或 Worker。根目录始终为真实项目根，因此无需移植 worktree 模板身份映射。

### 2. 先检查、加锁，再开始写入

解析并校验正式输出、缓存及外部 metrics 路径；拒绝危险目录、输出/缓存重叠及不可安全提交的目标关系。以规范化资源路径分别获取稳定锁，按确定顺序获取，竞争即失败并释放已获取锁。任一相同输出或共享缓存都互斥；外部 metrics 同路径也纳入竞争检查。不能只以 (output, cache) 组合生成一个锁，否则共享单个资源的构建仍会冲突。

锁文件应放在事务交换之外的稳定位置，不包含秘密；不得在释放时删除锁文件造成 inode 竞争。仅本次受控临时资源可清理，不扫描删除未知目录。

### 3. 实际路径与报告身份分离

输出 staging 与正式输出同级；缓存 staging 与正式缓存同级。clean 时输出从空目录开始，但正式输出的 marker/路径检查仍须执行；no-clean 时复制旧输出，保留非自有文件。缓存复制旧状态，所有受管写入转向暂存位置。

清理、migration、recovery 状态处理沿用旧规则，但作用于暂存副本。manifest OutputRoot 在正式身份与 staging 间转换；配置 hash、模板身份、报告和返回 BuildResult 不因随机临时路径变化而失效或泄漏临时位置。

### 4. 明确事务写集

必须逐项核对 manifest、媒体及正文磁盘缓存、图片派生物、内置插件输出、报告、metrics 与完成标记的实际写入路径。能由 CacheDir/Output 重定向的内容进入暂存区；发现绕过路径的受管写入应修正或在实施前补全合同，不能默默排除后仍宣称整个缓存回滚。

输出目录内报告跟随输出提交；外部 metrics 的 JSON/HTML 使用各自目标同文件系统暂存文件，加入提交与锁集合。任意自定义 provider/plugin 写外部系统不属于文件事务；不修改其行为，也不承诺撤销远程副作用。

### 5. 有限的提交与回滚

所有语言、根级聚合、报告、安全门禁、manifest、输出 marker 和 completed 状态完成后才提交。提交前检查取消并关闭本次文件/正文存储句柄。提交阶段不在每次移动间响应取消，避免主动打断恢复过程。

顺序移动正式目标至唯一 backup，再将 staging 移至正式目标。交换失败反向恢复；若恢复也失败，保留可恢复 backup，报告原始错误、恢复错误及受控路径，禁止 finally 删除唯一旧产物。成功后的 backup 清理失败只报告清理问题，不能伪称事务未提交或删除新输出。日志 build.done 和 dev 刷新必须在提交成功后发生。

不承诺多个目录原子切换、崩溃后自动恢复或持续读取无短暂空窗。Windows 文件占用可能使交换失败，需验证回滚和可读诊断。

### 6. dev 与部署

dev 每次构建获取并释放锁；成功提交后 BroadcastReload，失败继续保留旧输出。明确排除本次及受控命名空间的 staging/backup/锁事件，验证不会遮蔽正常内容编辑。服务端读取固定正式目录，交换空窗不属于本次零停顿保证。

deploy 内部构建复用公共事务；构建失败不得进入部署提供方。部署上传期间的并发快照锁和外部部署原子性不在本次构建事务范围内。

## Alternatives

- 逐语言 worktree：无当前必需隔离需求，额外 Git、进程和协议维护成本不成立。
- 只延迟 manifest：页面已写入，不能实现失败保护。
- 只在 BuildCommand 包装：遗漏 dev 和直接 Engine 消费方。
- 单个全局锁：无必要地阻塞完全不相交的站点；采用少量确定资源锁，不做通用调度器。

## Verification Scope

已有候选闭包映射至 Engine 和 CLI 专项；它不是最终写集证明。实施前必须根据实际文件及直接/序列化消费者重新生成 closure，解决 unmapped，并 classify。若新增 Shared/契约/策略文件，必须先更新闭包及其 owner 验证范围，不能假定两项专项足够。

完整专项命令为：

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

共享 build/cache/fixture 资源串行执行，按实际 fingerprint 检查并记录 GREEN cache。专项后一次 specialty review；最终一次 delta-only review，复用未失效证据，不运行未授权全量/发布门禁。性能仅记录复制开销风险，不宣称提速；跨平台未执行项明确保留为未验证。
