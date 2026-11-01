## ADDED Requirements

### Requirement: One Notion source load shares rate limiting and body concurrency

同一次 `LoadRawAsync` MUST 创建一个客户端，并 MUST 复用于数据库查询、分类关系解析、摘要预取、Composite 跨源关系补查和延迟正文读取，使限速许可调度连续。每次加载 MUST 创建一个正文并发门，容量沿用现有有效 `RenderConcurrency` 配置及默认值；摘要预取与普通延迟正文 MUST 调用同一受限渲染入口，活动正文渲染峰值 MUST NOT 超过有效容量。每页 MUST 创建独立 renderer，避免共享可变渲染状态。`NotionBodyStore` MUST 持有本次加载的客户端和并发门所有权。限速范围 MUST 为同一来源的一次加载及其返回的正文存储生命周期；多个来源即使使用同一 token 也各自拥有预算。

#### Scenario: Continuous permits across pages and stages (NOT-01, NOT-02)
- **WHEN** 多页正文连续请求，或一次加载依次执行查询、关系、摘要和正文
- **THEN** 许可时间序列跨页面连续，客户端计数不随页面数增长，同一加载的请求共享限速状态

#### Scenario: Bounded concurrent body renders (NOT-03)
- **WHEN** 高并发读取正文且 `renderConcurrency=2`
- **THEN** 活动正文渲染峰值不超过 2

#### Scenario: Independent loads (NOT-12)
- **WHEN** 同一个来源对象产生不同加载结果，释放其中一个
- **THEN** 生命周期独立，另一个结果继续可用

### Requirement: Every exit path settles started work before releasing resources

加载成功前发生异常或取消时，加载方 MUST 先收束本次实际启动的任务，再释放客户端和并发门。分类关系调度在读缓存、排队或发起请求时提前退出，MUST 等待已启动任务完成；join MUST NOT 因已取消的调用 token 提前返回，清理异常 MUST NOT 覆盖原异常及原取消 token。加载成功后所有权 MUST 转交 body store。`DisposeAsync` MUST 先关闭新请求接纳、等待已接纳操作结束，再按既有规则取消、收束后台任务，最后释放资源。并发门 MUST 只在成功取得许可后释放，异常与取消路径均执行释放；释放信号量和客户端 MUST 晚于所有使用它们的已启动任务结束。Composite 的结果组装、投影和最终 `CompositeContentBodyStore`/返回结果构造 MUST 纳入现有清理区域，成功返回才移交所有权；最终构造失败时 MUST 释放已完成的子 stores 恰好一次并维持原异常。

#### Scenario: Load, projection or dispose failures (NOT-08, NOT-09, NOT-10)
- **WHEN** 加载失败、投影失败、正常释放、释放时仍有已接纳操作，或释放后读取
- **THEN** 客户端恰好释放一次；操作期间客户端可用；释放后按既有契约拒绝新操作

#### Scenario: Relation scheduling exits after starting work (NOT-13)
- **WHEN** 分类关系 A 已启动，B 的热缓存读取取消或抛错，A 的收尾由屏障控制
- **THEN** 调度退出仍等待 A；A 结束前不释放信号量或客户端；保留原异常及 token，所有已启动任务均被观察

#### Scenario: Final composite construction fails (NOT-15)
- **WHEN** 子来源成功后，最终 Composite 包装因 null source key 等输入构造失败
- **THEN** 每个已拥有的子 store 恰好释放一次，无资源遗留，返回原构造异常

### Requirement: Body reads coalesce and failed tasks never poison later reads

同一正文的多个等待者 MUST 只执行一次实际读取。单个 `GetAsync` 等待者取消 MUST 只取消该等待者，保留其他等待者共享的正文任务。共享正文任务自身失败或取消时，即使已无等待者，MUST 清除对应缓存条目并观察失败；清除 MUST 按条目/任务身份进行，旧任务回调 MUST NOT 删除新尝试。下一次同键读取 MUST 直接新建尝试并可成功，不先重抛旧缓存失败；仅等待者取消 MUST NOT 清除仍在运行的共享任务。预取命中、内存命中或磁盘缓存命中 MUST NOT 产生额外正文 HTTP 请求。成功的空或纯空白正文预取 MUST 先记录正文种子，再决定是否提取摘要；cache off 时后续读取 MUST NOT 重复请求。

#### Scenario: Coalescing and hit paths (NOT-04, NOT-05)
- **WHEN** 同一正文有多个等待者，或预取、内存、磁盘缓存命中
- **THEN** 实际读取只执行一次，命中路径没有额外正文 HTTP 请求

#### Scenario: One waiter cancels (NOT-06)
- **WHEN** 一个等待者取消而其他等待者仍等待
- **THEN** 其他等待者仍成功取得正文

#### Scenario: Cancel or failure while queued (NOT-07)
- **WHEN** 排队期间取消或渲染失败
- **THEN** 许可没有泄漏，后续请求能够继续

#### Scenario: Waiter leaves before delayed failure (NOT-14)
- **WHEN** 唯一等待者取消，共享任务随后失败或取消，随后同键读取；或旧任务与新条目竞争
- **THEN** 下一次立即新尝试并可成功，不先重抛旧缓存失败；factory 次数、按身份移除和许可复用均正确，等待者取消不破坏仍运行的任务

#### Scenario: Empty prefetch with AutoSummary and cache off (NOT-16)
- **WHEN** AutoSummary 开启、cache off，预取得到空或纯空白正文，随后调用 `GetAsync`
- **THEN** 成功空正文被种子复用，正文 HTTP 请求仅一次，不产生新摘要或重复渲染

### Requirement: Existing retry and loading semantics are preserved

429、重试和关系权限失败 MUST 保持既有重试与诊断语义。正文读取 MUST 遵守加载阶段取消与返回后 store 生命周期的既有边界，不扩大取消影响。

#### Scenario: Retry and permission failure (NOT-11)
- **WHEN** 客户端收到 429、触发重试，或关系目标返回权限失败
- **THEN** 既有重试次数、退避、诊断事件和失败降级语义保持
