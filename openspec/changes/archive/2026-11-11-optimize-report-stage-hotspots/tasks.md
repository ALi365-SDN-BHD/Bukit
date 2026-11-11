# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 基线

- [x] 报告阶段计时与采样：`projections` 213 秒、`seo-audit` 61 秒、`media-validate` 49 秒；三处热点分别为 `ContainsInvariant`、`AnalyzeAgentManifest` 重复解析、`BuildEffectiveMediaConfig` 逐链接调用。 证据：`observe-report-stage-timing` tasks 4.3；`sample` 输出（会话临时目录，未入库）。

## 2. 汇总文件路由存在性

- [x] 内部多模式匹配器：给定文本与模式集合，一次扫描返回每个模式是否出现；语义与 `Contains(pattern, OrdinalIgnoreCase)` 逐项相同（含代理对的模式退回 `Contains`）。
- [x] `AggregatePublishProjectionBase.BuildRouteOutputs` 每个汇总文件构建一次匹配结果，`IsRoutePresent` 改为查表；`sitemap` 的 loc 键判断不变。
- [x] 测试：随机与边界用例（大小写、重叠/嵌套模式、模式为空白、文本为空、非 ASCII、代理对）与原实现逐项一致；现有发布投影测试通过。

## 3. 代理清单审查

- [x] 一次审查运行内 `agent-manifest.json` 只读取、解析一次，按文档复用；文件缺失、解析失败、缺 `documents` 数组、条目缺失或不一致时，问题列表与改动前完全相同。
- [x] 测试：各分支问题列表与改动前一致；现有审查测试通过。

## 4. 媒体输出校验

- [x] `ContentMediaOutput` 构造时计算一次生效媒体配置，`RewriteUrl` 默认图分支复用；校验结果与异常不变。
- [x] 测试：现有媒体本地化测试通过；默认图回退与缺失输出异常行为不变。

## 5. 文档

- [x] 中英马 CHANGELOG：大站报告阶段提速，输出不变。

## 6. 验证

- [x] `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包并解决未映射文件。
- [x] 闭包专项测试、格式检查、`openspec validate optimize-report-stage-hotspots --strict`、AOT 编译。
- [x] silkroutecc 同条件构建：记录三步耗时前后对比；`dist/` 与报告文件与改动前一致（签名媒体文件名与每次构建本就变化的字段除外）。

证据（2026-11-10）：专项测试与审查见 `/tmp/claude/codex-reports/review-s23/report.md`（0 Critical / 0 Important）；真实站点 A/B 报告阶段 293.0 s → 13.6 s（projections 200.2 → 4.6，seo-audit 54.6 → 8.6，media-validate 38.2 → 0.45），publish-audit-report 与 seo-report 除 `generatedAt` 外一致，其余差异仅为媒体（`/tmp/claude/codex-reports/stage23-ab/report.md`）；AOT `aot-s23` 无 IL 警告。
