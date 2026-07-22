# Bukit Core G-04D1C：Content Notion extension graph migration-contract / eligibility audit

日期：2026-07-23
审计分支：`codex/g04d1c-extension-graph-eligibility-audit`
源码基线：`2.0@685e6aa3698df6caff8470d5a38b9fa98e91ca46`
任务性质：独立只读资格审计；除本报告外不修改生产代码、测试、治理基线、访问级别或公共契约

## 1. 执行结论

G-04D1C **当前不具备直接删除资格**，但具备进入独立 migration-contract
准备任务的资格。五个类型应继续作为一个原子扩展图处理：

1. `Bukit.Content.Notion.INotionBlockRenderer`；
2. `Bukit.Content.Notion.NotionBlockTransformer`；
3. `Bukit.Content.Notion.NotionBlockRendererRegistry`；
4. `Bukit.Content.Notion.NotionRenderContext`；
5. `Bukit.Content.Notion.NotionBlocksRenderer`。

仓库生产路径已经使用 canonical `Bukit.Notion.Rendering`，Labs 和官方插件也没有发现
上述 legacy 类型的直接消费。闭合的 136 项消费者证据中，这五项均为
`no-public-match-found`。这些证据支持继续收敛 ownership，但不能代替迁移契约，也不能
证明私有、未索引或未自愿声明的消费者不存在。

直接 namespace 替换不能构成有效迁移：前三个 callback/registry 类型在名称规范化后
具有相同成员形状，但其 callback context identity 已变化；后两个类型还改变了 client
类型和异常语义。legacy `NotionApiClient` 到 canonical `NotionClient` 没有公开的一键
转换入口，custom renderer 若使用 `context.Client.PostAsync`，还必须显式选择 canonical
`NotionRequestSemantics`。

本轮还确认一个 legacy compatibility graph 的既有正确性风险：同一个 legacy registry
被两个 renderer/client 复用后，registry 的单一 `_client` 会被后一个 renderer 覆盖。
前一个 renderer 的 callback 随后会得到“inner rendering 仍使用 client A、公开
`context.Client` 却是 client B”的 split-brain context。当前 Core 生产路径不触发该条件，
但外部自定义 renderer/transformer 可能触发；这项行为不得被写成需要保留的兼容语义。

推荐将后续实施拆成两个独立任务：

- **G-04D1C-M1：canonical migration-contract fixtures and guide**：只补齐迁移契约、
  canonical 参数校验和测试证据，不删除 legacy CLR 类型；
- **G-04D1C-M2：five-type atomic 2.0 removal**：M1 通过并获得独立 public API
  授权后，再原子删除五个类型、迁移剩余测试并更新治理基线。

在没有新增直接 CLR 消费者证据的前提下，M1 闭环后可优先申请 2.0 原子删除；当前
没有足够收益在 2.0 分支新增长期 obsolete shim。若出现真实消费者、决定维护可编译的
2.0 prerelease 过渡期，或准备在 1.x 主动发出弃用告警，则必须重新选择 obsolete
方案，不能由本报告自动授权。

## 2. 审计范围与非目标

### 2.1 审计范围

- 五个 legacy CLR 类型及 canonical 对应类型的 public signature；
- callback、registry、context、renderer 和 transport 的运行时控制流；
- Core、Labs、官方插件、测试和活动文档中的仓内消费者；
- 现行 public API baseline、闭合 candidate manifest 和 G-04B3 消费者证据；
- 自定义 renderer/transformer、fallback、嵌套 rendering、分页、取消、异常和 disposal；
- 可执行的迁移路径、测试缺口及后续原子批次边界。

### 2.2 非目标

- 不删除、internalize、重命名或标记 obsolete；
- 不修改 `NotionApiClient`、`NotionProviderOptions`、`NotionClientStats`；
- 不修改 canonical transport、HTTP 重试、Notion API version 或异常体系；
- 不修改 schema、插件协议、CLI、配置、asset URL、路径工具或 report contract；
- 不重新打开消费者声明窗口，不改写闭合的 136-entry manifest；
- 不将 CLI/config/theme/process-plugin 消费误判为候选 CLR 消费；
- 不运行 full、release、`test-all`、`smoke-all` 或整仓库 gate。

## 3. 当前事实基线

### 3.1 治理状态

当前 public API baseline 包含 14 个程序集、514 个类型和 110 个
`2.0-candidate`。五个 D1C 类型都归属 `Bukit.Content`，分类均为
`implementation-public / 2.0-candidate / 2.0-review`。

若未来仅删除这五个类型，预期 baseline 变为 509 个类型、105 个候选；该数字只是
资格审计投影，不是本任务对 baseline 的修改授权。

闭合 manifest 对五项都记录：

| legacy CLR 类型 | 公开搜索 | declaration | private consumer |
|---|---|---|---|
| `INotionBlockRenderer` | `no-public-match-found` | `consumer-declaration-pending` | `unknown-until-voluntary-declaration` |
| `NotionBlockTransformer` | `no-public-match-found` | `consumer-declaration-pending` | `unknown-until-voluntary-declaration` |
| `NotionBlockRendererRegistry` | `no-public-match-found` | `consumer-declaration-pending` | `unknown-until-voluntary-declaration` |
| `NotionRenderContext` | `no-public-match-found` | `consumer-declaration-pending` | `unknown-until-voluntary-declaration` |
| `NotionBlocksRenderer` | `no-public-match-found` | `consumer-declaration-pending` | `unknown-until-voluntary-declaration` |

搜索时间为 2026-07-22。简单名称产生的 TypeScript、PHP、Java 等同名命中均已在
G-04B3 中按固定 commit 复核为 lexical false positive；完整 CLR 名称没有命中。
本报告复用闭合 manifest 的历史证据，不改变其字节，也不把历史阴性结果表述为实时、
全网或私有代码的无消费者证明。

### 3.2 仓内生产消费者

仓库级源码追踪得到：

- `src/Bukit-Core/Bukit.Content.Notion/NotionContentSource.cs:209-211` 直接构造
  canonical `Bukit.Notion.Rendering.NotionBlocksRenderer`；
- `src/Bukit-Core/Bukit.Content.Notion/NotionCacheManager.cs:38-48` 接收的也是 canonical
  renderer；
- Engine 仍使用 legacy `NotionApiClient` 处理其他内容与 taxonomy 场景，但没有使用
  五个 D1C extension-graph 类型；
- Labs 和官方插件的活动 C#/project 文件没有五个 legacy 类型或其 namespace 的消费；
- 五个 legacy 类型的生产引用只存在于 `Bukit.Content/Notion` compatibility graph
  自身，不是 Core 主业务调用方；
- 未发现以五个完整类型名进行反射、序列化、AOT 注册或 public signature 传播的仓内
  生产代码。

因此，删除候选不会改变当前 Core 的 Notion 内容加载主链，但会破坏任何外部或测试中
按 legacy namespace 编译的 extension consumer。

### 3.3 已知产品消费者

G-04B3 已核对 SRBiz-bukit、sitegen 和 ALi365WebSiteBuilder：它们分别通过 Bukit
CLI/配置/主题、旧 SiteGen process 或随站点携带的 Bukit 可执行程序构建网站；没有
确认的当前 Bukit 候选 CLR 类型引用。这些项目证明产品命令边界需要稳定，不构成五个
D1C 类型的保留或删除证据。

## 4. 扩展图与原子性

五个类型不是五个独立 facade，而是以下公共与实现依赖图：

```text
INotionBlockRenderer ───────┐
  RenderAsync(..., Context) │
                            ├──> NotionBlockRendererRegistry
NotionBlockTransformer ─────┘          │
  Invoke(..., Context)                 │
                                       v
NotionApiClient ───────────────> NotionBlocksRenderer
       │                               │
       └──────────────> NotionRenderContext
                              Client + RenderChildrenAsync
```

`INotionBlockRenderer` 和 delegate 的 public signatures 直接暴露 legacy context；
registry 的 public signatures又暴露 interface 和 delegate；renderer 构造器暴露 legacy
client 和 registry。虽然 `NotionRenderContext` 没有 public 构造器，它是 callback 必须
接收的 public 类型，并公开返回 legacy client。

因此不得先删 context、只留 callback，也不得只删 renderer、留下无法通过公共路径取得的
context。未来 removal 应把五个 CLR identity 作为一个原子批次，测试和治理变更可以按
owner 分文件实施，但不能产生可发布的半删除状态。

## 5. Public signature 与迁移差异

| legacy 类型 | canonical 类型 | 成员形状 | 实际迁移差异 | 当前资格 |
|---|---|---|---|---|
| `Content.Notion.INotionBlockRenderer` | `Notion.Rendering.INotionBlockRenderer` | namespace 规范化后相同 | 方法参数的 context CLR identity 改变；外部实现类必须重编译和改签名 | 条件合格 |
| `Content.Notion.NotionBlockTransformer` | `Notion.Rendering.NotionBlockTransformer` | namespace 规范化后相同 | delegate CLR identity 与 callback context 改变；已有 delegate 二进制不可复用 | 条件合格 |
| `Content.Notion.NotionBlockRendererRegistry` | `Notion.Rendering.NotionBlockRendererRegistry` | namespace 规范化后相同 | 参数校验和 adapter lifecycle 不完全相同；legacy registry 持有可变 client binding | 暂缓 |
| `Content.Notion.NotionRenderContext` | `Notion.Rendering.NotionRenderContext` | 方法相同，`Client` 不同 | `NotionApiClient` 变为 `NotionClient`；可用方法、异常和 write semantics 改变 | 阻塞 |
| `Content.Notion.NotionBlocksRenderer` | `Notion.Rendering.NotionBlocksRenderer` | 名称相同，构造器类型不同 | constructor client/registry identity 改变；不再翻译为 `ContentException` | 阻塞 |

这五项都发生 assembly identity 和 namespace 变化。已经编译的 binary consumer 不能只
替换 DLL；源码 consumer 也不能只改 `using` 后假定行为完全相同。因为目标类型 full name
不同，标准 CLR type forwarding 不能把旧 full name 直接转发到 canonical full name；若
保留旧 full name，则仍需保留 facade/adapter，并没有完成 public surface 收窄。

## 6. Migration contract 逐项分析

### 6.1 Client/options 构造映射

legacy `NotionApiClient(NotionProviderOptions)` 在
`src/Bukit-Core/Bukit.Content/Notion/NotionApiClient.cs:121-131` 内部映射：

| legacy `NotionProviderOptions` | canonical `NotionClientOptions` | 说明 |
|---|---|---|
| `Token` | `Token` | 必须显式迁移 |
| 固定 `NotionApiUrls.NotionVersion` | `ApiVersion` | canonical 默认值当前相同，但迁移示例应明确契约来源 |
| `RequestDelayMs` | `RequestDelayMs` | 一对一 |
| `MaxRetries` | `MaxRetries` | 一对一；只作用于 idempotent read |
| `MaxRps` | `MaxRps` | 一对一；throttle state 按 client instance 隔离 |
| 无对应字段 | `Timeout = 30s` 默认值 | 当前 legacy 映射也间接取得此默认值 |
| `DatabaseId`、内容投影/缓存字段 | 无 transport 对应 | renderer 的 page/block id 由调用参数提供，不应错误复制到 transport |

该映射是 internal 方法，外部 consumer 无法从一个已存在的 `NotionApiClient` 取得其
internal `Transport`。D1C consumer 必须新建并自行 dispose canonical `NotionClient`，或
同时迁移自己对 legacy client 的其他使用。若同一 consumer 同时保留 legacy content
client 又新增 canonical renderer client，会形成两个独立的 timeout、throttle、stats 和
disposal scope；迁移文档必须显式说明，不能把它当作无行为差异的 namespace 替换。

### 6.2 Context client API 变化

legacy `NotionRenderContext.Client` 是 `NotionApiClient`，canonical property 是
`NotionClient`。对只调用 `RenderChildrenAsync` 的 renderer，源码迁移接近机械替换；一旦
custom renderer 直接使用 client，差异如下：

- legacy `GetAsync(string, token)` 把 `NotionApiException` 翻译为 `ContentException`；
- canonical `GetAsync` 直接抛出结构化 `NotionApiException`；
- legacy `PostAsync(string, json, token)` 自动根据 URL 判断 database query 是否可重放；
- canonical 没有同形 `PostAsync`，调用方必须构造 `HttpRequestMessage` 并在
  `SendAsync` 中显式选择 `IdempotentRead` 或 `NonReplayableWrite`；
- canonical `GetStats()` 是 public，而 legacy stats accessor 是 internal；这不是迁移阻塞，
  但证明两个 client 不是同一个公共契约。

特别是 write semantics 不能由 migration facade 猜测。错误地把 create/update/append
请求标成 idempotent read 可能引入危险重放；全部标成 non-replayable 又会改变 database
query 的 429 retry。M1 必须提供显式、可编译的迁移示例和测试，M2 不得顺带重写 HTTP
策略。

### 6.3 Renderer 异常语义

legacy renderer 在
`src/Bukit-Core/Bukit.Content/Notion/NotionBlocksRenderer.cs:40-61` 捕获 canonical
`NotionRenderingException` 和 `NotionApiException`，以相同 message 包装为
`ContentException` 并保留 inner exception。canonical renderer 不执行该翻译：

- 缺少 `results`：legacy 为 `ContentException(inner=NotionRenderingException)`，
  canonical 为 `NotionRenderingException`；
- HTTP、429、invalid JSON、transport：legacy 为
  `ContentException(inner=NotionApiException)`，canonical 为 `NotionApiException`；
- custom renderer/transformer 自己抛出的其他异常：两侧均不应被误包裹；
- caller cancellation：两侧都应原样传播 `OperationCanceledException` 和原 token。

当前 canonical 测试已覆盖 rendering exception 和 cancellation，legacy 测试没有成对
证明上述差异。M1 应把“异常类型有意变化”写成 2.0 migration contract，不应为了让旧
断言继续通过而在 canonical owner 中重新引入 `ContentException` 依赖。

### 6.4 Registry callback、fallback 与参数校验

legacy registry 通过 adapter 把 custom renderer 和 transformer 转交 canonical registry。
custom transformer 返回 `null` 时的 fallback、重复注册覆盖、remove 和未知 block 返回
`null` 已有 legacy 测试；canonical owner 当前只直接覆盖 non-null override，没有测试
transformer 返回 `null` 后转入 built-in renderer 的路径。canonical null-fallback 因此仍是
M1 必须补齐的 replacement contract，而不是已有通过证据。

但存在两项未闭环差异：

1. legacy `Register` 与 `SetCustomTransformer` 立即对 renderer/transformer 执行
   `ArgumentNullException.ThrowIfNull`；canonical registry 当前会先把 null 写入字典，
   直到实际 render 才失败。迁移会改变失败时点和异常位置；
2. 没有一项 cross-entrypoint fixture 证明同一个 custom renderer/transformer 在 legacy
   和 canonical 图中收到正确 client、相同 block JSON、相同 token，并保持
   override/null-fallback/remove/unknown 行为。

M1 可以在 canonical owner 中补齐与 legacy 一致的 null 参数校验；这属于 replacement
契约加固，不授权其他 registry 行为变化。

### 6.5 已确认的 shared-registry split-brain 风险

legacy registry 在
`src/Bukit-Core/Bukit.Content/Notion/NotionBlockRendererRegistry.cs:12` 只保存一个
`_client`。每次构造 legacy renderer，
`src/Bukit-Core/Bukit.Content/Notion/NotionBlocksRenderer.cs:18-24` 都调用
`registry.BindClient(client)` 覆盖该字段。adapter callback 又在 registry 的
`CreateContext` 中把当前 `_client` 与 canonical callback context 组合。

确定性触发顺序：

1. 建立 shared legacy registry，并注册 custom renderer 或 transformer；
2. 用 client A 和 shared registry 构造 renderer A；
3. 用 client B 和同一 registry 构造 renderer B；
4. 再通过 renderer A 渲染 custom block。

第 4 步的 canonical inner context 仍属于 renderer A/client A，故
`RenderChildrenAsync` 通过 client A 请求；adapter 创建的 legacy context 的
`Client` 却来自最后一次 bind，即 client B。若两个 client 使用不同 token、account、
throttle 或 handler，custom callback 会在同一 context 中跨 client 执行。并发构造或
复用还会使结果取决于最后写入时序。

当前 Core 生产路径直接使用 canonical renderer，不共享这个 legacy adapter；现有测试也
没有 dual-client/shared-registry 场景。因此将其定级为：**已由控制流确认的 compatibility
graph 缺陷，仓内生产不可达，外部触发证据未知**。

canonical registry 不持有 client，client 始终来自每次 renderer 创建的 canonical
context，天然不存在这一绑定字段。M1 应新增 canonical dual-client fixture，明确
split-brain 不是需要保留的 legacy 行为。若 1.x 仍要支持共享 legacy registry，应另立
窄修复任务；不得在 D1C removal 中顺带发明跨版本修复。

### 6.6 嵌套 rendering、分页、取消与 disposal

实现审查结果：

- legacy `RenderChildrenAsync` 直接委托 canonical inner context，嵌套 rendering 主逻辑
  已单一归属 canonical owner；
- pagination、list switching、missing cursor 停止和嵌套 list 已分别在 Content/Notion
  测试覆盖；
- canonical renderer 在 block loop 中显式检查 cancellation，transport 也保留 caller
  cancellation；legacy catch filter 不捕获取消；
- 两侧 renderer 都不 dispose client，client lifetime 由创建者管理；
- injected `HttpClient` 与 internally owned `HttpClient` 的 ownership 由 canonical
  `NotionClient` 处理，迁移文档必须要求 consumer dispose `NotionClient`，不能把 renderer
  当作 owner。

这些行为的实现基础已经存在，但没有一个面向外部 extension consumer 的完整 migration
fixture 将它们组合起来。实现单元测试通过不能替代 source migration proof。

## 7. 测试证据与缺口

### 7.1 本轮真实基线

新 worktree 最初没有 `obj/project.assets.json`。带 `--no-restore` 的独立 project
`dotnet test` 只执行空的 MSBuild `VSTest` target，虽返回 0，但没有 test run 输出；该组
结果已明确废弃，不计为通过证据。

完成实际 restore 后，本轮基线为：

| 测试项目 | 结果 |
|---|---:|
| `Bukit.Architecture.Tests` | 116 passed / 0 failed / 0 skipped |
| `Bukit.Content.Tests` | 486 passed / 0 failed / 0 skipped |
| `Bukit.Notion.Tests` | 270 passed / 0 failed / 0 skipped |
| `Bukit.Content.Notion.Tests` | 6 passed / 0 failed / 0 skipped |

合计 878 passed / 0 failed / 0 skipped。以上只证明当前基线健康，不证明 D1C 已具备删除
资格。

### 7.2 已覆盖

- legacy registry：default、unknown、override、null fallback、remove、duplicate replace；
- legacy renderer/context：registry identity、pagination、list、nested children、client
  identity、missing cursor 停止；
- canonical renderer：pagination、HTML escaping、custom transformer、rendering exception、
  caller cancellation；
- adapter production：canonical renderer 的 cache/cancellation 路径；
- architecture：legacy 类型当前 assembly identity、legacy default registry 委托
  canonical owner、D1A/D1B 已删除类型和 D1C 保留边界。

### 7.3 M1 必须新增的契约测试

1. **compile/source migration fixture**：同一 custom renderer 与 transformer 分别用旧、
   新 public API 编译，记录必须修改的 namespace、context/client 和异常 catch；
2. **callback identity**：canonical callback 收到构造 renderer 时的同一
   `NotionClient`、原 block、原 cancellation token；
3. **child rendering**：custom renderer 通过 context 渲染嵌套 children，输出、分页和
   token 传播符合 canonical 契约；
4. **registry behavior**：override、`null` fallback、remove、unknown、duplicate replace；
5. **argument validation**：`Register(..., null)` 和
   `SetCustomTransformer(..., null)` 在调用点立即失败；
6. **exception matrix**：missing results、HTTP status、429、invalid JSON、transport、custom
   callback exception、caller cancellation；明确 old/new exception type，而非只比较 message；
7. **dual-client shared registry**：renderer A 和 B 共享 canonical registry 时，每次
   callback context 都返回各自 client，不发生 split-brain；
8. **options mapping**：Token、API version、delay、retries、max RPS 和 30 秒 timeout；
9. **write semantics migration**：database query 的 idempotent read 与真正 write 的
   non-replayable 路径均有编译示例，不新增自动重放；
10. **ownership/disposal**：renderer 不拥有 client；internally owned 和 injected
    `HttpClient` 的现有 disposal 契约保持；
11. **public API guard**：M1 期间五个 legacy 类型仍存在且 baseline 数量不变；
12. **no Content dependency**：canonical renderer/tests 不为兼容旧异常重新引用
    `Bukit.Content` 或 `Bukit.Shared.ContentException`。

## 8. 资格 blockers

| ID | 级别 | blocker | 原因 | 关闭条件 |
|---|---|---|---|---|
| D1C-B01 | 阻塞 | context client 契约不等价 | 类型、方法、异常、write semantics 均变化 | 可编译迁移示例及 options/write tests |
| D1C-B02 | 阻塞 | renderer 异常契约未声明 | `ContentException` 变为 canonical typed exceptions | old/new exception matrix 和迁移文档 |
| D1C-B03 | 阻塞 | extension graph 没有端到端迁移 fixture | 当前测试分散验证实现，不验证 consumer migration | M1 的 12 项契约测试通过 |
| D1C-B04 | 重要 | legacy shared registry split-brain | 单一可变 `_client` 与 callback inner context 可指向不同 client | canonical dual-client test；明确不保留该缺陷 |
| D1C-B05 | 重要 | canonical null 参数失败时点漂移 | replacement 比 legacy 更晚失败 | canonical owner 补齐窄参数校验和测试 |
| D1C-B06 | 治理约束 | 私有消费者仍未知 | 公开搜索阴性不是无消费者证明 | 保留 major-version 决策、迁移说明和新证据回退规则 |

D1C-B06 不要求证明世界上没有消费者；该证明不可获得。它要求保持诚实的 major-version
策略：出现具体 CLR 消费证据时回退到 retain/obsolete 评估，而不是覆盖或改写历史证据。

## 9. 方案比较与决策

### 方案 A：现在直接删除五个类型

**拒绝。** 仓内生产无调用只能证明主链低风险，不能补齐 client、exception、write
semantics 和 callback migration contract。直接删除会把不可编译和行为差异推给消费者，
且无法用现有测试区分预期 2.0 break 与意外漂移。

### 方案 B：长期保留 compatibility graph

**不推荐作为默认方向。** canonical owner 已稳定承担生产 rendering，legacy graph 继续
暴露错误 ownership，并持有 shared-registry split-brain 风险。当前也没有正式 CLR SDK
分发路径或已确认直接消费者证据支持永久维护双公共面。

### 方案 C：先 M1 建立迁移契约，再在 2.0 原子删除

**推荐。** M1 只强化 canonical replacement 的可迁移性和证据，不触碰 legacy CLR
identity；M2 在明确授权后一次删除五个类型，避免半套 extension graph。1.x visibility
保持不变，2.0 通过 migration guide 明确 source/binary break。

### 方案 D：先 obsolete，再晚于 2.0 删除

**条件备选。** 若 M1 期间出现真实 extension consumer，或产品决定发布可供 CLR
consumer 编译的过渡 SDK，则需要设计 obsolete window。当前 CLI archive 分发、关闭的
公开搜索和 2.0 major boundary 下，新增长期 shim 的收益不足；不能只因“可能存在私有
consumer”无限期保留所有 candidate。

## 10. 推荐实施顺序

### G-04D1C-M1：migration-contract fixtures and guide

独立分支、独立测试、独立只读复审。允许范围：

- 在 `Bukit.Notion.Tests` 增加 canonical extension-consumer contract fixtures；
- 为 canonical registry 增加与 legacy 一致的 renderer/transformer null 参数校验；
- 增加专门的 D1C migration guide/ledger，给出 options、client、context、exceptions 和
  write semantics 的 old/new 示例；
- 增加 architecture guard，证明五个 legacy 类型在 M1 仍保持原 public identity；
- 不改 legacy implementation，不修复 split-brain，不改 baseline/candidate manifest。

M1 验收：第 7.3 节适用测试全部通过；canonical project 不新增 Bukit project reference；
五个 legacy 类型和 514/110 baseline 不变；focused gate 及一次 parent aggregate 通过；
独立复审为 0 Critical / 0 Important / 0 Minor。

### G-04D1C-M2：five-type atomic 2.0 removal

必须在 M1 合并后重新申请 deliberate public API approval。允许范围：

- 原子删除五个 legacy 类型及只为其存在的 adapter；
- 将仍有价值的 legacy 行为测试迁移/改写为 canonical consumer contract test；
- 更新 `LegacyNotionConsumerFixture`、`NotionBoundaryTests`、G-04D1B 保留断言；
- 更新 current public API baseline 为预期 509/105；
- 更新 active governance 和正式 removal ledger；
- 保持闭合 136-entry candidate manifest 字节不变，作为历史 cohort；
- 不删除 `NotionApiClient`、`NotionProviderOptions` 或 `NotionClientStats`。

M2 验收除 focused/aggregate gate 外，还必须包括：Architecture、Content、Notion、
Content.Notion 测试；public API drift self-test 和真实 check；Core/Labs/plugins 跨边界 build；
Native AOT/发布 smoke 是否执行仍由独立任务按变更边界申请，不由本报告预授权。

## 11. 新证据回退规则

在 M2 删除提交前，如出现以下任一证据，停止直接删除并回到方案 D 或保留方案：

- 可识别程序集直接实现 legacy `INotionBlockRenderer`；
- public/protected signature 暴露任一 legacy 类型；
- delegate、reflection、serialization、Native AOT、source generator 或 binary plugin 依赖；
- custom renderer 使用 `context.Client.PostAsync` 且不能安全映射 request semantics；
- 现有 consumer 不能在 2.0 时间窗内迁移到 canonical exceptions/client；
- 1.x 与 2.0 需要同时由同一个二进制插件运行。

普通 `bukit build`、配置、主题、HTML 输出和 process plugin 使用仍不构成 CLR candidate
证据，除非同时提供具体程序集和类型依赖。

## 12. 复现命令

```bash
# 当前五项治理状态
jq -r '.types[] | select(.name == "Bukit.Content.Notion.INotionBlockRenderer" or
  .name == "Bukit.Content.Notion.NotionBlockTransformer" or
  .name == "Bukit.Content.Notion.NotionBlockRendererRegistry" or
  .name == "Bukit.Content.Notion.NotionRenderContext" or
  .name == "Bukit.Content.Notion.NotionBlocksRenderer") |
  [.assembly,.name,.classification,.compatibility] | @tsv' \
  docs/governance/bukit-core-public-api-baseline.v1.json

# 完整 legacy CLR 名称：治理、反射式 fixture 和明确限定引用
rg -n 'Bukit\.Content\.Notion\.(INotionBlockRenderer|NotionBlockTransformer|NotionBlockRendererRegistry|NotionRenderContext|NotionBlocksRenderer)' \
  src tests guide docs

# 实际源码消费者还会通过 using/同 namespace 使用简单名称；必须结合文件所属项目、
# namespace 和 using 指令区分 legacy 与 canonical identity
rg -n '\b(INotionBlockRenderer|NotionBlockTransformer|NotionBlockRendererRegistry|NotionRenderContext|NotionBlocksRenderer)\b|using Bukit\.(Content\.Notion|Notion\.Rendering);' \
  src/Bukit-Core src/Bukit-Labs src/Bukit-Plugins tests \
  --glob '*.cs' --glob '*.csproj' --glob '!**/bin/**' --glob '!**/obj/**'

# 真实 baseline tests；新 worktree 不得在 assets 缺失时把 --no-restore 空目标算作通过
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --tl:off
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --tl:off
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --tl:off
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj -c Release --tl:off
```

## 13. 最终判定

| 判定项 | 结论 |
|---|---|
| canonical production ownership | 已满足 |
| 仓内生产无 legacy extension-graph consumer | 已满足 |
| 公开搜索未发现直接 CLR consumer | 已满足，但私有状态未知 |
| public member 一对一替换 | 仅前三项表面满足；context/renderer 不满足 |
| client/options migration contract | 未满足 |
| exception migration contract | 未满足 |
| custom extension end-to-end fixture | 未满足 |
| shared registry 正确性 | legacy 存在 split-brain 缺陷；canonical 需防回归证据 |
| 现在直接删除资格 | **不通过** |
| 进入 G-04D1C-M1 资格 | **通过** |
| M1 后申请五类型原子删除 | **有条件推荐，需再次明确授权** |

G-04D1C 的正确下一步不是修补所有 transport 或一次删除五个文件，而是先把 extension
consumer 的迁移契约变成可编译、可测试、可复审的事实。M1 完成以前，五个类型继续
保留；M1 完成也不自动授权 M2。

## 14. 本任务验证与独立复审

- 报告路径的 `post-change-focused.sh` 在修正文档后 exit 0；
- 独立只读初审发现 1 项 Important（复现命令遗漏简单名称/`using` 消费）和 1 项
  Minor（误写 canonical null-fallback 已覆盖）；两项均完成文档修正；
- 独立只读复审确认两项关闭、无回归，最终为
  Critical / Important / Minor = `0 / 0 / 0`，结论可提交；
- parent aggregate 按要求只执行一次
  `post-change-targeted.sh --base 685e6aa3698df6caff8470d5a38b9fa98e91ca46 -- <报告路径>`：
  docs、format、analyzer、public API drift、portability 等此前步骤通过，随后在未变更的
  `brainstorm server self-test` 以 `mv-1 left a live spawned server` 失败；
- 单独原样重跑 `bash scripts/checks/brainstorm-server-self-test.sh` 仍在同一断言失败。
  本分支没有修改该 self-test、`.trae` server 脚本或任何运行时代码，因此该结果记录为
  **既有独立门禁阻塞，aggregate 未通过**；本报告不修复、不抑制，也不把它表述为环境
  通过或 G-04D1C 回归。

该 blocker 不改变本报告的 eligibility 结论，但在它由独立 owner 任务关闭前，不能声称
本任务获得了完整 aggregate PASS。
