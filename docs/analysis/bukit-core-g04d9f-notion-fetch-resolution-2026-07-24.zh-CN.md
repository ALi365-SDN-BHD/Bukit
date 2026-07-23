# Bukit Core G-04D9F Notion Fetch Integration 受控收窄台账

> 日期：2026-07-24
>
> 任务：G-04 Group 4 / Task 38
>
> 状态：implementation-complete / g4-verification-pending

`INotionPageFetcher` 与 `NotionFetchedPage` 作为 interface/record 原子图由
public 变为 internal；`PagesIndexPlugin` 已在 D9E internalize，因而没有 stable
public parent 泄漏。

current baseline 从 `14/449/10` 变为 `14/447/8`；historical manifest 的两项记录、
`136/136` 和 blob `7b07d6890562387010b52301e9f8716e9bf10ed1` 不变。

新增 `G04D9FNotionFetchGraphTests` 锁定 internal type/interface graph、baseline、
historical manifest 和活动文档。Task 42 必须验证 PagesIndex/Notion adapter 的分页、
取消、缓存、输出与 Native AOT 静态可达性。

production 只改两个 modifiers；不新增第二套 Notion client，不修改 schema/config、
plugin protocol、Labs 或外部插件。本任务不单独运行 tests/gates/AOT/review。
