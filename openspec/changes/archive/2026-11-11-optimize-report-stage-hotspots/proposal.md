# Optimize report stage hotspots

## Why

阶段 1（`fix-render-shared-model-cost`、`observe-report-stage-timing`）之后，丝路文化站点（3547 条内容、4651 页）一次构建约 390 秒，其中渲染只有约 6 秒墙钟时间，报告阶段约 320 秒。报告阶段计时（非 CI 模式实测）：

| 步骤 | 耗时 | 占变体报告 |
|---|---|---|
| `projections` | 213 秒 | 65.8% |
| `seo-audit` | 61 秒 | 18.9% |
| `media-validate` | 49 秒 | 15.3% |
| 其余（含汇总范围） | 约 6 秒 | — |

用 macOS `sample` 在三个步骤运行时各采样 8 秒，活跃线程几乎全部落在三处重复工作上：

1. **`projections`**：`AggregatePublishProjectionBase.BuildRouteOutputs` 对每个汇总文件（`sitemap.xml`、`search.json`、`agent-manifest.json`、`llms.txt` 等 9 种）逐个路由调用 `IsRoutePresent`，用 `string.Contains(..., OrdinalIgnoreCase)` 在整个文件文本里查找路由地址，找不到时再查一次 canonical。`search.json` 28 MB、`agent-manifest.json` 4.3 MB，扫描量为“路由数 × 文件大小”，三次采样 100% 落在 `ContainsInvariant → IndexOfIgnoreCase`。
2. **`seo-audit`**：`RepresentationAuditRules.AnalyzeAgentManifest` 对每篇可索引文档都 `File.ReadAllText` 并 `JsonDocument.Parse` 整个 `agent-manifest.json`（约 3500 次 × 4.3 MB），采样约 83% 在该方法内的读取与解析。
3. **`media-validate`**：`ContentMediaOutput.RewriteUrl` 对每个不属于媒体目录的链接调用 `ContentProviderFactory.BuildEffectiveMediaConfig` 取默认图地址；该调用经 `BuildTransaction.Physical/Map` 访问文件系统（目录枚举、大小写探测）。重新扫描 HTML 时 `includeAllAnchorHrefs` 使每个链接都走到这里，采样约 97% 在该调用内。

三处都是同一份不变的数据被重复计算，结果与一次计算相同。

## What Changes

- **汇总文件中的路由存在性**：每个汇总文件只扫描一遍，用多模式匹配（Aho-Corasick）一次求出全部路由地址与 canonical 是否出现，替代逐路由的子串查找。匹配语义保持 `StringComparison.OrdinalIgnoreCase` 的子串语义：文本与模式都按 `char.ToUpperInvariant` 逐字符折叠后匹配；含代理对的模式退回原有 `Contains` 判断，保证结果逐项相同。
- **代理清单审查**：`agent-manifest.json` 在一次审查运行内只读取、解析一次，按文档建立查找表供各文档复用；文件不存在、解析失败、缺少 `documents` 数组时，每篇文档报告的问题与现在相同（相同代码、严重级别、路由与消息）。
- **媒体输出校验**：`ContentMediaOutput` 在构造时计算一次生效的媒体配置（与现在已缓存的 `_mediaPrefix` 同源），`RewriteUrl` 的默认图分支改用该值，不再逐链接访问文件系统。
- 阶段 2 同时给出问题 3 的结论：阶段 1 之后渲染阶段墙钟 6 秒、各线程累计 53.6 秒，约 9 个核并行，按计划表“CPU 利用恢复到多核”一行结束，不改 GC 设置，不另立变更。
- 不改变任何输出文件、报告内容、问题列表、日志、`metrics.json` 结构或公共 API。

## Impact

- 受影响代码：`Bukit.Engine/PublishProjectionContract.cs`（`AggregatePublishProjectionBase`），新增内部多模式匹配器；`Bukit.Engine.Audit/PublishAuditRules/RepresentationAuditRules.cs` 及其调用方（`MachineReadabilityTrustAuditBuilder`）传入一次解析的清单；`Bukit.Engine/ContentMediaOutput.cs`。
- 受影响测试：新增匹配器与原 `Contains` 逐项一致的测试（大小写、重叠模式、空文本、代理对）；清单审查各错误分支的问题列表不变；媒体校验结果不变；现有发布投影、审查与媒体测试保持通过。
- 验证：silkroutecc 同条件构建，`projections`、`seo-audit`、`media-validate` 明显下降，`dist/` 与改动前逐字节一致（签名媒体文件名除外），各报告文件除每次构建本就变化的字段外一致。
- 公共契约：无公共 API 变化；输出与报告不变。
- 用户批准范围：本提案只批准起草；实施、提交、发布需单独确认。
