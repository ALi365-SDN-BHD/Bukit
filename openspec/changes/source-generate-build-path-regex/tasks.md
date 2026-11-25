# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 0. 测量（不改仓库文件）

- [ ] 记录参考站点全量构建的报告阶段分项计时，取发布审计一项。
- [ ] 采样确认 `SemanticHtmlAuditRules` 在发布审计中的耗时占比。
- [ ] 据此决定：按性能项推进、仅作为一致性整理推进，或关闭本变更。

## 1. 转换 14 个静态模式

- [ ] `SemanticHtmlAuditRules`（9 个）。
- [ ] `BasicMarkdownToHtml`（2 个）。
- [ ] `ContentImageRewritePipeline`（1 个）。
- [ ] `SeoDocumentTitleResolver`（1 个）。
- [ ] `AnalyticsManagedBlockFilter`（1 个）。
- [ ] 逐个核对：模式文本与选项除去掉 `Compiled` 外逐字一致。
- [ ] 逐个确认 8 个 `IgnoreCase` 且无 `CultureInvariant` 的模式在 JIT 测试环境与 AOT 二进制下的大小写语义与现状一致。

## 2. 可选：内联校验调用（需单独确认后才开始）

- [ ] 上述 6 个文件中的字面量模式内联调用。

## 3. 测试

- [ ] 等价性测试：对每个被转换的模式，用一组有代表性的输入（含大小写变体、跨行内容、空输入、非 ASCII 字符）断言新旧实现的匹配结果相同。旧模式在测试中以运行时构造的方式保留为对照。
- [ ] 受影响区域的现有测试通过。

## 4. 文档

- [ ] 开发文档：构建路径上的静态正则使用 `[GeneratedRegex]` 的约定，以及不适用的情形（运行时拼接的模式）。
- [ ] 中英马 CHANGELOG（若阶段 0 确认有可观测的耗时变化）。

## 5. 验证

- [ ] `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包并解决未映射文件。
- [ ] 闭包给出的专项测试、格式检查、PublicApiDrift。
- [ ] AOT 发布无 IL 警告。
- [ ] 参考站点：逐个比较转换前后发布文件的相对路径和字节，并比较仅归一化已列明易变字段的内部报告。
- [ ] 参考站点：复测报告阶段分项计时，记录变化。
- [ ] `openspec validate source-generate-build-path-regex --strict`。
