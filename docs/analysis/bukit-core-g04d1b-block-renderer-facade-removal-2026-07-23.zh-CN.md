# G-04D1B Block Renderer Facade 删除实施记录

状态：实施记录已建立 / 跨边界验证与独立复审待执行

## 范围与基点

- 基点 commit：`b2cc47211dc8c6f12f02fb358f50afd22bdd1d56`。
- Task 分支：`codex/g04d1b-block-renderer-facade-removal`。
- 本记录只批准 G-04D1B 的 23 个 legacy facade 删除，不是对候选清单的批量授权。
- canonical namespace：`Bukit.Notion.Rendering.BlockRenderers`。

## 精确删除身份与 canonical 映射

下列每个 `Bukit.Content.Notion.BlockRenderers.<Name>` 身份都迁移到
`Bukit.Notion.Rendering.BlockRenderers.<Name>`；名称一一对应：

1. `Bukit.Content.Notion.BlockRenderers.AudioBlockRenderer`
2. `Bukit.Content.Notion.BlockRenderers.BookmarkBlockRenderer`
3. `Bukit.Content.Notion.BlockRenderers.CalloutBlockRenderer`
4. `Bukit.Content.Notion.BlockRenderers.ChildEntityBlockRenderer`
5. `Bukit.Content.Notion.BlockRenderers.CodeBlockRenderer`
6. `Bukit.Content.Notion.BlockRenderers.ColumnBlockRenderer`
7. `Bukit.Content.Notion.BlockRenderers.ColumnListBlockRenderer`
8. `Bukit.Content.Notion.BlockRenderers.DividerBlockRenderer`
9. `Bukit.Content.Notion.BlockRenderers.EmbedBlockRenderer`
10. `Bukit.Content.Notion.BlockRenderers.EquationBlockRenderer`
11. `Bukit.Content.Notion.BlockRenderers.FileBlockRenderer`
12. `Bukit.Content.Notion.BlockRenderers.ImageBlockRenderer`
13. `Bukit.Content.Notion.BlockRenderers.LinkPreviewBlockRenderer`
14. `Bukit.Content.Notion.BlockRenderers.LinkToPageBlockRenderer`
15. `Bukit.Content.Notion.BlockRenderers.NoOpBlockRenderer`
16. `Bukit.Content.Notion.BlockRenderers.PdfBlockRenderer`
17. `Bukit.Content.Notion.BlockRenderers.RichTextContainerRenderer`
18. `Bukit.Content.Notion.BlockRenderers.SyncedBlockRenderer`
19. `Bukit.Content.Notion.BlockRenderers.TableBlockRenderer`
20. `Bukit.Content.Notion.BlockRenderers.TableOfContentsBlockRenderer`
21. `Bukit.Content.Notion.BlockRenderers.ToDoBlockRenderer`
22. `Bukit.Content.Notion.BlockRenderers.ToggleBlockRenderer`
23. `Bukit.Content.Notion.BlockRenderers.VideoBlockRenderer`

`BlockRendererFacades.cs` 已原子删除。原文件末尾的 internal
`NotionBlockHelpers` bridge 以独立 `NotionBlockHelpers.cs` 保留，仍仅转发到
canonical helper；canonical production renderer 文件没有修改。

## 测试所有权迁移与保留边界

六个原 Content 测试文件按所有权处理：

- `BlockRendererExtendedTests.cs`、`BlockRendererColorEncodingTests.cs`、
  `BlockRendererUrlSafetyTests.cs`、`NotionBlockRenderersTests.cs` 整体迁移到
  `Bukit.Notion.Tests`；测试 body、输入和断言保持不变。
- `BlockRendererMediaAndContainerTests.cs` 按 canonical renderer 与 Content
  internal helper 两侧拆分。
- `NotionBlockRendererEdgeCasesTests.cs` 按 direct renderer 与 D1A/D1C
  compatibility 边界拆分。
- canonical context/client 测试共用唯一 test-only
  `CanonicalBlockRendererTestSupport`，没有复制 handler/client helper。

D1C 仍由 `Bukit.Content.Tests` 保留的三个测试方法直接覆盖：

- `NotionBlocksRenderer_Registry_ReturnsRegistry`
- `NotionBlocksRenderer_NullType_BlockSkipped`
- `NotionBlocksRenderer_HasMoreNoCursor_StopsPagination`

五个 D1C CLR 身份均未删除：`Bukit.Content.Notion.INotionBlockRenderer`、
`Bukit.Content.Notion.NotionBlockTransformer`、
`Bukit.Content.Notion.NotionBlockRendererRegistry`、
`Bukit.Content.Notion.NotionBlocksRenderer`、
`Bukit.Content.Notion.NotionRenderContext`。四个 D1A rich-text edge tests 也继续
留在 Content owner。

Task 1 初次 GREEN 结果为 Content 486 passed / 0 failed / 0 skipped 与
Notion 270 passed / 0 failed / 0 skipped，合计 756，精确保持变更前
670 + 86 的 756-test 总数；Architecture 为 111 passed / 0 failed / 0 skipped。

## 基线批准与不可变历史

- 更新前真实 `public-api-drift.sh check Release` 退出 1，只报告上述 23 条
  `breaking: ... exported type removed`，没有非目标 breaking 或任何 review/error
  分类。
- 生成快照与“旧基线只删除这 23 个身份”的期望 JSON 经 `jq -S` 后语义 diff
  为 0。
- 当前 baseline 为 14 assemblies、514 types、110 个 `2.0-candidate`；旧
  facade 身份为 0，23 个 canonical renderer 身份全部存在。
- 闭合的 136-entry candidate manifest 仍是历史且不可变，base/current blob
  均为 `7b07d6890562387010b52301e9f8716e9bf10ed1`。其中 23 个历史记录继续保持
  `consumer-declaration-pending`、`unknown-until-voluntary-declaration` 和
  `no-public-match-found`；这些字段不是“没有私有消费者”的证明。
- G-04C 的 135 与 G-04D1A 的 133 是历史快照；当前未批量批准余量是 110。

## 迁移说明

源代码消费者应把 `Bukit.Content.Notion.BlockRenderers` 引用改为
`Bukit.Notion.Rendering.BlockRenderers`，并在需要 context/client 的地方使用
canonical `Bukit.Notion.Rendering.NotionRenderContext`、
`Bukit.Notion.Rendering.NotionBlocksRenderer` 与
`Bukit.Notion.Transport.NotionClient`。已经针对旧 23 个 CLR 类型编译的二进制
消费者不能只替换 DLL；它们必须更新引用并重新编译。这是明确批准的 2.0
source/binary breaking boundary，不改变 1.x CLR visibility。

公开检索只能说明当前没有找到公开匹配。私有、未索引或未披露消费者仍是
`unknown-until-voluntary-declaration`，后续证据必须在独立任务中处理。

## 非目标

- 不删除或修改五个 D1C 扩展图类型。
- 不处理 `NotionClientStats` 或其他 transport facade。
- 不修改 canonical production renderer 行为或公开契约。
- 不修改 schema、plugin protocol、transport、exceptions、URLs、paths 或 reports 契约。
- 不修改项目文件、版本、CI、release、gate script 或 verification policy。
- 不修改闭合的 136-entry candidate manifest。
- 不授权剩余 110 个候选的批量删除，也不改变任何 1.x CLR visibility。

## 剩余跨边界验证与独立复审

以下项目仍待 parent task 在独立步骤完成；本实施记录不声称它们已通过：

- [ ] Core 跨边界 Release 验证。
- [ ] Labs 跨边界 Release 验证。
- [ ] plugins 跨边界 Release 验证。
- [ ] Native AOT 与 release-artifact smoke。
- [ ] 独立只读实施复审。
- [ ] parent aggregate `post-change-targeted.sh` 与最终 aggregate diff 复审。

Task 1 仅负责 Architecture、Content、Notion owner tests、public API drift
self-test/check 及一次覆盖全部 Task 1 路径的 focused post-change check；其结果在
Task 1 报告与 commit 中记录。跨边界验证与独立复审 remain pending。
