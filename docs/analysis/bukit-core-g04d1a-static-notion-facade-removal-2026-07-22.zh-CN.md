# Bukit Core G-04D1A 两个静态 Notion 门面移除记录

状态：已实施并通过跨边界验证与独立只读复审

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

## Task 2 已完成跨边界验证与首次独立只读实施复审

- 完整受影响测试均在 Release、`--no-restore` 且 `NOTION_TOKEN` unset 下通过：
  Architecture 109 passed / 0 failed / 0 skipped，Content 670 passed / 0 failed /
  0 skipped，Notion 86 passed / 0 failed / 0 skipped。
- Core Release `--no-restore` build：exit 0，0 warnings / 0 errors；Labs Release `--no-restore` build：exit 0，0 warnings / 0 errors。
- Plugins 证据保留完整环境链：受限环境首次原命令因
  `WordCountSectionPlugin/obj/project.assets.json` 缺失而 NETSDK1004（exit 1）；
  随后仅对 `WordCountSectionPlugin` 执行精确 restore（exit 0）；受限环境原命令
  重跑仍由 SDK 10.0.100 Roslyn `MissingFieldException` 阻断（0 warnings / 21 errors）。
  Plugins 原命令在非沙箱环境原样重跑：exit 0，0 warnings / 0 errors。上述受限失败
  没有被误写为编译成功，也没有添加规避性 MSBuild 属性。
- `osx-arm64` Native AOT 证据同样保留环境边界：受限环境首次运行
  `native-aot.sh 2.0.0-alpha.1 osx-arm64` 因 NU1900 及 NuGet vulnerability cache
  `vuln_index.dat-new` 权限被拒绝而 exit 1，未生成 archive；相同命令在非沙箱环境：exit 0，
  生成临时 archive `bukit-2.0.0-alpha.1-osx-arm64.tar.gz`（12,022,035 bytes），
  内含唯一可执行 Mach-O arm64 `bukit`。该临时 artifact 未上传、未发布。
- `release-artifacts.sh` 对该 archive 的 smoke exit 0：Config check passed，fixture build completed，publish audit 精确为 `routes=2 errors=0 warnings=22`。22 条是 basic
  fixture 的既有 publish/SEO warnings；此处关闭的是 smoke 0 errors，绝非 warnings 为零。
- `public-api-drift-self-test.sh` 输出 `public API drift self-test OK`；
  `public-api-drift.sh check Release` exit 0，build 为 0 warnings / 0 errors。当前
  baseline 为 14 assemblies / 537 types / 133 `2.0-candidate`，两个旧名称为 0，且
  语义 delta 只包含两个已批准的删除。
- closed candidate manifest 相对
  `1d72384b10dc011388db44042c35daccb0c5411f` 的 diff 为 0，base/current blob 同为 `7b07d6890562387010b52301e9f8716e9bf10ed1`。其余 28 个 renderer candidates、canonical
  members 与 `NotionClientStats` 不变。
- 第一次独立只读实施复审：Approved / PASS，0 Critical、0 Important、0 Minor；复审范围
  覆盖精确两个类型删除、canonical 类型/成员、行为测试迁移、baseline semantic delta、
  不可变 manifest、其余 28 个 renderer candidates、越界契约和验证证据。

## closure commit 后 parent 待完成

- parent aggregate `post-change-targeted.sh` 尚未执行，且只能在此 closure commit 后按
  parent task 运行一次。
- fresh final aggregate diff review 尚未执行；它与 aggregate gate 同属本 closure commit
  之后的 parent 任务。二者完成前不宣称 merge-ready、不会合并、推送或发布。
