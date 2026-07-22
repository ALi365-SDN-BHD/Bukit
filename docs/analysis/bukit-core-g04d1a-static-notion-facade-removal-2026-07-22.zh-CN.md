# Bukit Core G-04D1A 两个静态 Notion 门面移除记录

状态：实施记录已建立 / 跨边界验证与独立复审待执行

基线：`2.0@1d72384b10dc011388db44042c35daccb0c5411f`

## 已批准的 2.0 决策

本次决定只从 `Bukit.Content.dll` 移除以下两个静态门面：

- `Bukit.Content.Notion.NotionColorPalette`
- `Bukit.Content.Notion.NotionRichTextRenderer`

当前受治理 public API baseline 为 537 个类型，其中 133 个为
`2.0-candidate`。历史 136-entry candidate manifest 保持不可变，仍保留这
两个条目及其 `consumer-declaration-pending`、
`unknown-until-voluntary-declaration` 和 `no-public-match-found` 证据；这不
能证明私有消费者不存在。

两个名称的 canonical replacements 分别是
`Bukit.Notion.Rendering.NotionColorPalette` 和
`Bukit.Notion.Rendering.NotionRichTextRenderer`。专用行为测试已迁移至
`Bukit.Notion.Tests`，混合 legacy block-renderer 测试仅以类型别名绑定到
canonical owner。

## Breaking-change 与边界

这是刻意的 source/binary breaking change：使用旧
`Bukit.Content.Notion` CLR 身份的源码无法再编译，已编译消费者也无法再
解析该导出类型。没有添加 type forwarding、兼容 shim、`Obsolete` 标记、
新 package 或 canonical Notion SDK 承诺。1.x `main` 未改变。

这不是对其余候选的批量授权。其余 28 个 renderer candidates、
`NotionClientStats`、schema、plugin protocol、transport、exceptions、URLs、
paths、reports 和 version 均未改变；configuration defaults、asset URL、
path utilities 和 HTTP/TLS 行为同样未改变。

## Task 1 已完成验证

- 编译程序集 RED guard 在删除前失败，且只因两个旧类型仍可解析；删除后
  同一 guard 通过。
- 已迁移 `NotionColorPaletteTests` 与
  `NotionRichTextRendererExtendedTests` 到 canonical test project，并保留
  原有断言主体。
- Architecture 109 passed / 0 failed。
- Content 670 passed / 0 failed。
- Notion 86 passed / 0 failed。
- `public-api-drift-self-test.sh` 通过：`public API drift self-test OK`。
- baseline 更新后的 `public-api-drift.sh check Release` 通过，build 为 0 warnings / 0 errors。
- focused post-change check 通过；其 owner tests 为 Content 670、Architecture
  109、Notion 86。
- closed candidate manifest 与
  `1d72384b10dc011388db44042c35daccb0c5411f` 的 diff 为 0。
- 当前 baseline snapshot 与“仅删除这两个 `Bukit.Content` entries”的语义预期
  完全一致。
- pre-baseline public API drift 被分类为恰好两条 `breaking:` exported type
  removed，无其它 drift category。

## 待完成的跨边界验证

- Core/Labs/plugins builds 与 `osx-arm64` Native AOT archive smoke；
- 首轮独立只读复审已执行，结论为 Changes requested；1 Important finding 待本修复复审关闭；
- parent task 的 aggregate `post-change-targeted.sh`、最终 aggregate diff 与
  final review/closure。
