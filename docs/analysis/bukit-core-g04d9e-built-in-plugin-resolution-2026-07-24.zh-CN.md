# Bukit Core G-04D9E Built-in Plugin Graph 受控收窄台账

> 日期：2026-07-24
>
> 任务：G-04 Group 4 / Task 37
>
> 状态：implementation-complete / g4-verification-pending

## 1. 终态

13 个 built-in implementation classes 全部由 public 变为 internal：
Alias、Archive、DataFiles、Feed、ImageProcessing、LlmsTxt、Menu、
PagesIndex、Pagination、RelatedContent、SearchIndex、Sitemap、Taxonomy。

真实 ownership 不被简化：

- 9 registry-owned candidates：DataFiles、PagesIndex、Taxonomy、Pagination、
  Archive、RelatedContent、Alias、Menu、ImageProcessing；
- 4 aggregate-only：Feed、LlmsTxt、SearchIndex、Sitemap；
- `AnalyticsPlugin` 是非候选 registry entry。

## 2. Baseline 与断言

current baseline 从 D9D `14/462/23` 变为 `14/449/10`。historical manifest
保持 `closed / 136 / 136` 与 blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`。

新增 `G04D9EBuiltInPluginGraphTests` 锁定 13 项 internal/exported 终态、稳定
plugin interface、registry 的 10 项顺序（Analytics + 9 candidates）、4 项未被错误
描述为 registry-owned、baseline、历史记录和活动治理文档。

## 3. 行为与边界

Task 42 必须覆盖 built-in registry、ordering、derive/after-build、
template requirements、reports、output ownership 和 Native AOT static
registration tests。production 只改变 13 个 type modifiers；constructor、Name、
Version、hook、capability 和输出逻辑均不修改。

不引入 reflection/dynamic assembly、通用 CLR plugin SDK 或 process plugin source；
不修改 Engine.Abstractions、plugin protocol、schema、config、Labs 或外部插件。

按 G4 规则，本任务不单独运行 tests/gates/AOT/review，统一留到 Task 42。
