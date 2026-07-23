# Bukit Core G-04D3 Content 决策汇总

日期：2026-07-23

分支：`codex/g04-group2-content-b-shared-cli`

G2 `GROUP_BASE`：`27dcc456d5f6a614d2a7bc9a35fb93bd938a9766`

状态：implementation decisions complete；`group-verification-complete`

## 1. 汇总结论

G-04D3 的五个原始候选已全部形成明确终态：

| 候选 | 传播图 | 终态 | 支持边界 | G2 待验证 |
|---|---|---|---|---|
| `Bukit.Content.CompositeContentBodyStore` | Body store implementation | `internalized` | `IContentBodyStore`、Content provider | fallback、identity、async disposal |
| `Bukit.Content.DictionaryContentBodyStore` | Test/helper store | `internalized` | `IContentBodyStore` | dictionary comparer、body identity |
| `Bukit.Content.Markdown.BasicMarkdownToHtml` | Markdown helper | `internalized` | Markdown provider/CLI rendered output | HTML、TOC、危险 scheme 现状 characterization |
| `Bukit.Content.Markdown.MarkdownBodyStore` | Markdown body implementation | `internalized` | `IContentBodyStore`、Markdown provider | file/front matter/cancellation |
| `Bukit.Content.Notion.NotionClientStats` | duplicate transport facade DTO | `removed-with-canonical-migration` | `Bukit.Notion.Transport.NotionClientStats` | 三字段计数、retry/throttle、transport lifetime |

五项没有 retained 或 blocked 项。D3A 四项只收窄 type accessibility；D3B 删除重复
legacy stats identity，并让 internal `NotionApiClient.GetStats()` 直接返回 canonical
transport snapshot。

## 2. 公共面与兼容性

G1 D3A 后 current baseline 为 14 assemblies / 497 public types / 85 candidates。D3B
只移除一个 legacy stats record，当前投影为 14 / 496 / 84。

这些都是明确的 2.0 source/binary/reflection breaking changes。公开搜索未确认 direct CLR
消费者，但 private、未索引和未声明消费者仍为
`unknown-until-voluntary-declaration`。关闭的 136 项历史 manifest 不改，Git blob
继续为：

```text
7b07d6890562387010b52301e9f8716e9bf10ed1
```

canonical replacements 和行为边界不是同名 type-forward：

- Body/Markdown implementation 迁移到 provider 与 `IContentBodyStore`；
- legacy stats 迁移到不同 assembly/namespace 的
  `Bukit.Notion.Transport.NotionClientStats`。

## 3. 行为与架构边界

D3A 没有改动方法体、fallback、exception、cancellation、comparer、Markdown pipeline、
HTML、TOC、URL policy、file I/O、front matter 或 disposal ownership。危险 URL scheme
passthrough 是明确保留的 residual-risk characterization，不是安全批准。

D3B 没有改动：

- request、retry、429、Retry-After 或指数退避；
- rate limit、request delay 或三个统计计数算法；
- owned/injected `HttpClient` lifetime；
- Notion API、日志、schema、配置或 public `NotionApiClient` members。

Content 现有 friend assembly 边界保持不变；D3 不新增 production friendship。canonical
`Bukit.Notion` 与 `Bukit.Content.Notion` 的既有精确 IVT 也不扩张。

## 4. Group 2 待验证输入

Task 20 必须统一消费：

1. Content、Content.Notion、Notion、Engine 和 Architecture owner tests；
2. D3A 的 fallback、body identity、Markdown、安全现状与 disposal 证据；
3. D3B 的 canonical return identity、request/throttle 三字段、retry 非 throttle 语义；
4. canonical transport 的 caller-owned/owned `HttpClient` disposal 证据；
5. current baseline 14 / 496 / 84 与 public API drift；
6. historical manifest 136 entries 与固定 blob；
7. G2 唯一 aggregate targeted gate；
8. Native AOT 与 published Content/Notion 可达性；
9. G2 唯一轻量只读复审。

Task 20 的 Core owner tests、public API drift、Native AOT、发布产物 smoke、aggregate
targeted gate 与独立轻量复审均已完成；五项 decision 和 G2 Core 范围正式关闭。

## 5. 禁止漂移与后续边界

本汇总不授权修改媒体、SEO、配置、插件协议、asset URL、TLS、Notion API、retry、
rate-limit、日志、全局路径工具或持久化格式。它也不授权提前处理 Shared、CLI Shared、
Rendering、Routing、Theme 或 Engine 候选。

Task 13 必须以 Shared 自己的 17 项传播图重新调查；不能把 D3 的 canonical migration
结论直接复制到 `Bukit.Shared.Notion`。
