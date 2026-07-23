# Bukit Core G-04D3B `NotionClientStats` transport facade 决议

> 日期：2026-07-23
> 范围：G2 Task 11，仅处理 legacy `Bukit.Content.Notion.NotionClientStats`
> 状态：implementation complete / group-verification-pending

## 1. 决议

Task 11 采用 **canonical facade migration**，而不是保留 duplicate，也不是仅把
duplicate 改为 internal：

1. 删除 `Bukit.Content.Notion.NotionClientStats` 的 legacy CLR identity；
2. 保留 internal `NotionApiClient.GetStats()` facade；
3. 让该 facade 直接返回
   `Bukit.Notion.Transport.NotionClientStats`；
4. canonical `Bukit.Notion.Transport.NotionClient.GetStats()` 继续是唯一统计 owner。

本任务不删除或收窄 `NotionApiClient`，也不改变它的 public constructor、
`GetAsync`、`PostAsync` 或 `Dispose` 签名。迁移只消除重复统计 record 及无意义的
逐字段复制。

## 2. 兼容性边界

两个 stats record 虽然都有以下三个 `long` 字段，但它们原本属于不同 assembly 和
namespace，具有不同 CLR identity：

- `RequestCount`
- `ThrottleWaitCount`
- `ThrottleWaitTotalMs`

因此删除 legacy identity 是明确的 **2.0 source、binary 和 reflection break**：

- 源码中直接构造或引用 legacy record 的消费者必须改用 canonical namespace；
- 已针对 legacy identity 编译的二进制不能把 canonical record 当成同一个类型；
- `typeof(...)`、`Type.GetType(...)` 或按旧 full name 查找类型的代码必须迁移。

已关闭的认证搜索中，legacy full name 与 simple name 均没有已复核的公开消费者匹配。
这不证明不存在 private、未索引或未主动声明的消费者；其状态继续是
`unknown-until-voluntary-declaration`。该剩余风险由 2.0 公共面收窄窗口承担，不能被
表述为无兼容成本。

## 3. 统计语义保持

canonical transport 的语义保持如下：

- `RequestCount` 对实际开始的每个 HTTP attempt 计数；可重放 read 的 retry 会形成新的
  attempt 并再次计数；
- request 在进入 HTTP attempt 前若因 throttle、request delay 或 caller cancellation
  中止，不提前计入 `RequestCount`；
- `ThrottleWaitCount` 只统计由 `MaxRps` 调度产生且大于零的 throttle wait；
- `ThrottleWaitTotalMs` 累加这些 throttle wait 的取整毫秒数；
- `Retry-After`、指数 retry delay 和显式 `RequestDelayMs` 都不是 throttle wait，不进入
  后两个字段；
- 三个计数都继续通过 `Interlocked` 更新或读取，snapshot 的线程安全语义不变。

internal facade 直接返回 canonical snapshot，不修改 request、retry、delay 或 throttle
算法，也不引入额外计数层。

## 4. transport lifetime 与 friendship

本迁移不得改变 HTTP transport 的所有权：

- `new NotionApiClient(options)` 仍创建并拥有内部 canonical `NotionClient`；
- canonical client 仍拥有它内部创建的 `HttpClient`，并在重复 `Dispose` 时只释放一次；
- 注入 `HttpClient` 的 internal 构造路径仍设置 `ownsHttpClient: false`；
- 释放 facade 或 canonical client 不得释放 caller-owned `HttpClient`；
- `GetStats()` 不取得、转移或延长任何 transport 所有权。

`Bukit.Content` 的现有 friend assembly 集合必须保持精确不变：

- `Bukit.Content.Tests`
- `Bukit.Engine`
- `Bukit.Engine.Tests`

Task 11 不新增 `InternalsVisibleTo`，也不借 stats 迁移处理既有 Engine friendship。

## 5. serialization、reflection 与 Native AOT

仓库内没有发现 legacy stats 的 JSON attribute、serializer context、runtime serializer
调用、reflection factory、继承扩展点或 Native AOT 显式注册。现有旧 full-name 与
`typeof(...)` 引用属于测试和治理 root，不是产品运行时动态依赖。

canonical stats 已由公开 `NotionClient.GetStats()` 静态返回，并已被 canonical Content
Notion 路径使用。移除 legacy duplicate 应只减少一个 exported metadata identity，不应
改变 Native AOT 可达业务路径。最终是否满足该结论，仍必须由 Task 20 的真实 Native AOT
与 published fixture 证明；本决议不预称 AOT 已通过。

## 6. 公共面与历史证据

Task 11 的 current baseline 目标为：

| 项目 | Task 11 前 | Task 11 后 |
|---|---:|---:|
| Core assemblies | 14 | 14 |
| public types | 497 | 496 |
| `2.0-candidate` types | 85 | 84 |

关闭的 136 项 consumer-declaration manifest 是历史 cohort，必须保持原字节和原
candidate 条目。其 Git blob 必须继续为：

```text
7b07d6890562387010b52301e9f8716e9bf10ed1
```

不得通过重写历史 manifest 来制造当前 closure。只允许更新 current baseline 和新的
Task 11 决议/验证台账。

## 7. 必须迁移的治理 roots

实现与 G2 关闭时必须同步处理以下当前 root：

1. `tests/Bukit.Content.Tests/LegacyNotionConsumerFixture.cs`：移除 legacy
   `typeof(...)`；
2. `tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`：从 legacy Content Notion
   exact export 集合移除旧 full name；
3. `tests/Bukit.Architecture.Tests/G04D3AContentBodyGraphTests.cs`：把“Task 11 前仍
   public”守卫更新为 Task 11 后不存在/不导出；
4. 新增 `G04D3BNotionStatsTests`，固定 canonical return type、legacy absence、current
   baseline、friendship 和 historical manifest blob；
5. current public API baseline：只删除 legacy stats entry；
6. 现行 consumer declaration 与 public API governance guide：新增 Task 11 的当前
   决议，不回写或篡改早期 D1/D2 的历史时点数字和措辞。

## 8. Task 20 组级验证清单

Task 11 不单独运行测试。G2 Task 20 必须统一验证：

- `Bukit.Content.Tests`：legacy facade 返回 canonical runtime type；三个计数值与
  throttle 行为不变；
- `Bukit.Content.Notion.Tests`：canonical stats 日志读取和 Content Notion 路径不变；
- `Bukit.Notion.Tests`：request/retry 计数、per-client throttle、injected/owned
  `HttpClient` disposal 语义不变；
- `Bukit.Engine.Tests`：现有 Content friendship 与 Engine Notion 使用路径可编译并通过；
- `Bukit.Architecture.Tests`：legacy identity 不存在，canonical identity 仍 public，
  baseline 为 `14/496/84`，历史 manifest blob 不变；
- public API drift：只出现批准的一个 legacy type removal；
- G2 唯一 aggregate targeted gate；
- Native AOT build、release-artifact smoke 与 published basic Markdown/Notion 可达性证明；
- 最后一次 G2 独立轻量只读复审。

在上述证据全部完成之前，本任务保持 `group-verification-pending`，不得写成
qualification complete 或 fully closed。

## 9. 禁止漂移

Task 11 禁止顺带修改：

- Notion API URL、header 或 request semantics；
- retry 次数、429 处理、Retry-After 或指数退避；
- rate-limit、request delay 或计数算法；
- `HttpClient` ownership、dispose 或 transport lifetime；
- stats 日志字段及格式；
- schema、配置、插件协议、媒体、SEO、路径工具或构建报告；
- `NotionApiClient` 之外的 legacy Notion facade；
- closed consumer-declaration manifest。

测试或环境失败不能授权修改上述行为来迎合预期。

## 10. 停止条件

出现任一情况时，Task 11 不得申请关闭：

1. 发现 direct external CLR consumer、public/protected signature、runtime serializer、
   reflection factory、source-generator 或 AOT registration 依赖 legacy identity；
2. canonical migration 需要改变 Notion transport、retry、rate-limit、lifetime 或日志；
3. 三字段统计值、retry attempt 计数、throttle 计数或 cancellation 边界发生变化；
4. owned/injected `HttpClient` disposal 证据回归；
5. current baseline 不是精确 `14/496/84`，或出现第二个未批准的公共面 delta；
6. historical manifest blob 不再等于固定值；
7. Task 20 owner tests、public API drift、唯一 aggregate、Native AOT 或独立复审存在未关闭
   failure/finding。

若发现第 1 项，应停止删除并改为 retained 或独立 obsolete/declaration window；不得通过
扩大 Task 11 范围消除阻断。
