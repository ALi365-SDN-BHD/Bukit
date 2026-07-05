# Bukit 全仓清理审计报告

日期：2026-06-28  
范围：`/Users/ali/mydev/Git/Github/Bukit` 全仓目录、源码、脚本、测试、门禁、文档、生成物和备份区。  
方式：只读扫描与轻量门禁检查；本报告生成前未删除、移动或修改任何项目文件。  

历史文档状态标注（2026-07-05）：本文保留 2026-06-28 审计时的历史路径和旧机制名词。文中的 `experimental/Bukit.Labs.Protocol`、`site.externalPlugins`、`ExternalProtocolPluginSource`、`ProtocolEchoPlugin` 或 sample plugin 描述不代表当前实现；当前正式外部插件路径为 `Bukit.PluginHost` + `bukit-plugin-v1`，legacy Labs Protocol source、sample plugin、protocol echo fixture 已删除。

## 1. 审计结论

Bukit 当前主线是 Core 1.0：稳定 CLI、严格配置、Markdown/Notion 内容源、路由、主题、Scriban 渲染、内建插件、部署、报告和门禁脚本。仓库整体结构可继续支撑当前项目，但存在明显清理对象和边界噪音：

- 大量 ignored 生成物留在工作树，尤其 `TestResults/` 约 1.1G。
- `.DS_Store` 等系统文件散落在主线、备份和测试 fixture 目录。
- Notion 插件相关目录当前主要是 `bin/obj` 构建残留，不是可构建源码目录。
- `guide-0.1/` 与 `scripts-0.1/` 已被仓库规则定义为备份区，不应再作为默认修复目标。
- `.trae/` 是代理工具规则和技能包，是否仍属于仓库资产需要团队决策。
- `.gitignore` 与已跟踪 `tests/fixtures/**` 的策略冲突，后续清理容易误伤 fixture。

本次审计没有发现需要立即删除的 Core 源码目录；优先清理对象是生成物、系统垃圾、残留构建目录和历史/工具边界。

## 2. 当前主线资产：应保留

### 2.1 Core 源码

保留：

- `src/Bukit.Cli`
- `src/Bukit.Cli.Shared`
- `src/Bukit.Config`
- `src/Bukit.Content`
- `src/Bukit.Engine`
- `src/Bukit.Engine.Abstractions`
- `src/Bukit.Plugin.Abstractions`
- `src/Bukit.PluginHost`
- `src/Bukit.Rendering`
- `src/Bukit.Routing`
- `src/Bukit.Shared`
- `src/Bukit.Theme`

理由：这些目录对应 README 与架构文档中的 Core 1.0 主线：CLI、配置、内容、路由、渲染、主题、引擎、插件 host 和共享基础设施。

### 2.2 主线插件源码

保留：

- `plugins/Bukit.Plugin.Echo`
- `plugins/Bukit.Plugin.Import`

理由：`bukit.slnx` 包含这两个插件项目；`scripts/checks/official-plugin-packages.sh` 也围绕正式插件包合约检查 Import 插件。

### 2.3 测试与测试工具

保留：

- `tests/Bukit.*.Tests`
- `tests/PluginProcessProbe`
- `tests/ThrowingPlugin`
- `tests/fixtures`

说明：

- `tests/ThrowingPlugin` 和 `tests/PluginProcessProbe` 是测试辅助项目，不应按普通插件清理。
- `tests/fixtures/dotfile-leak-site/static/.env`、`.npmrc`、`.yarnrc`、`private.key`、`cert.p12`、`cert.pfx` 是 dotfile/security leak 测试夹具，不应按真实密钥删除。

### 2.4 主线脚本、门禁与 CI

保留：

- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
- `scripts/gates/ci-fast.sh`
- `scripts/gates/ci-full.sh`
- `scripts/gates/release.sh`
- `scripts/checks/*`
- `scripts/smoke/*`
- `scripts/build/*`
- `scripts/release/*`
- `scripts/security/*`
- 顶层兼容 wrapper：`scripts/quality-gate.sh`、`scripts/release-gate.sh`、`scripts/smoke.sh`、`scripts/smoke-all.sh`、`scripts/test-all.sh`

理由：这些是当前 CI、release、smoke、coverage、docs 和安全回归的主线入口。

### 2.5 主线文档与技能

保留：

- `README*.md`
- `CHANGELOG*.md`
- `SECURITY*.md`
- `CONTRIBUTING*.md`
- `CODE_OF_CONDUCT*.md`
- `guide/`
- `docs/schemas/`
- `docs/coverage-baselines.json`
- `docs/release/`
- `docs/plugins/`
- `docs/analysis/`
- `guide/skills/`
- `guide/labs-skills/`

说明：`guide/skills` 是 Core 1.0 agent skill 主线；`guide/labs-skills` 是显式 opt-in 的 Labs 技能区。

## 3. 立即移除标记：无需保留的生成物和系统文件

以下对象建议在单独清理任务中删除。它们当前不应作为项目语义资产存在。

| 标记 | 路径/类型 | 判断 | 理由 |
|---|---|---|---|
| REMOVE-GEN-001 | `TestResults/` | 移除 | 约 1.1G，包含 `.trx`、coverage、release-gate、native-aot、notion package 产物；已被 `.gitignore` 忽略。 |
| REMOVE-GEN-002 | `.smoke-all-run/` | 移除 | smoke/build 运行产物；已被 `.gitignore` 忽略。 |
| REMOVE-GEN-003 | 全仓 `.DS_Store` | 移除 | macOS 系统文件，无项目价值；已被 `.gitignore` 忽略。 |
| REMOVE-GEN-004 | 全仓 `bin/`、`obj/` | 移除 | .NET 构建/还原产物；已被 `.gitignore` 忽略。 |
| REMOVE-GEN-005 | `tests/*/TestResults/` | 移除 | VSTest 本地结果目录，不应常驻。 |
| REMOVE-GEN-006 | `tests/fixtures/**/.smoke-all-run/` | 移除 | fixture 内部运行产物，已被忽略。 |
| REMOVE-GEN-007 | `tests/fixtures/*/dist/` 中未作为 golden snapshot 管理的输出 | 条件移除 | 需先确认哪些 `dist` 是故意跟踪的 golden fixture，避免误删。 |

扫描到的 `.DS_Store` 包括：

- `.DS_Store`
- `examples/.DS_Store`
- `guide/.DS_Store`
- `guide/ai/.DS_Store`
- `guide/ai/bukit-ai-demo-to-cms/**/.DS_Store`
- `guide-0.1/guide/**/.DS_Store`
- `src/.DS_Store`
- `tests/.DS_Store`
- `tests/fixtures/.DS_Store`
- `tests/fixtures/html-demo-import/chinese-demo/**/.DS_Store`

## 4. 高优先级结构漂移：需要清理或重新定责

### 4.1 Notion 插件残留目录

标记：

- `src/Bukit.Notion/`
- `plugins/Bukit.Plugin.Notion/`
- `tests/Bukit.Notion.Tests/`
- `tests/Bukit.Plugin.Notion.Tests/`

当前观察：这些目录在工作树中主要包含 `bin/` 和 `obj/`，未扫描到可构建的 `.csproj` 与源码文件。它们应作为生成物残留清理，而不是作为当前源码删除。

建议：

1. 先删除这些目录下的 `bin/obj` 残留。
2. 清理后若目录为空，则目录自然消失。
3. 若未来恢复 Notion 插件源码，应明确加入 `bukit.slnx` 或单独 solution，并同步 `scripts/checks/official-plugin-packages.sh`。

### 4.2 `src/plugins/` 旧样例插件残留

标记：

- `src/plugins/PathReportPlugin/`
- `src/plugins/SampleAfterBuildPlugin/`
- `src/plugins/VisualFeedbackPlugin/`
- `src/plugins/WordCountSectionPlugin/`

当前观察：

- `PathReportPlugin`、`SampleAfterBuildPlugin`、`VisualFeedbackPlugin` 在 `src/plugins` 下主要是 `bin/obj` 残留；对应源码已在 `experimental/Bukit.Labs.Protocol/SamplePlugins/`。
- `WordCountSectionPlugin` 仍有源码和 `.csproj`，但不在 `bukit.slnx`，且当前插件目录 ADR 倾向顶层 `plugins/Bukit.Plugin.<Name>/`。

建议：

- 删除 `src/plugins/*/bin` 和 `src/plugins/*/obj`。
- 将 `src/plugins/WordCountSectionPlugin` 标为人工决策项：若仍是 Core 内建 section plugin 测试资产，应移动到测试/实验区；若已无引用，应删除。

### 4.3 `tests/fixtures/**` 与 `.gitignore` 冲突

当前 `.gitignore` 忽略：

- `tests/fixtures/`
- `tests/fixtures/*`

同时又白名单：

- `tests/fixtures/release-assets/`
- `tests/fixtures/workflow-evidence/`

但仓库里实际有大量已跟踪 fixture，包括 basic markdown、i18n、taxonomy、html demo import 等。这会导致未来新增/恢复 fixture 时行为混乱。

建议：

1. 先明确 fixture 策略：全部 fixture 都允许跟踪，还是只允许 release/workflow-evidence。
2. 若全部允许跟踪，修正 `.gitignore`，改为只忽略 fixture 下的生成输出。
3. 若只允许少数 fixture，则需要单独评估并移除非白名单 fixture。当前不建议直接删除，因为很多测试可能依赖它们。

## 5. 备份区与历史区：保留但不得默认修改

### 5.1 `guide-0.1/`

状态：备份/reference documentation only。  
处理建议：保留为历史参考；默认不修复、不重构、不作为文档同步目标。

### 5.2 `scripts-0.1/`

状态：备份/reference scripts only。  
处理建议：保留为历史参考；质量门禁、CI、runtime 修复只应改 `scripts/` 主线。

### 5.3 `guide/archive/`

状态：主线归档区。  
处理建议：保留；如果后续清理历史文档，应优先把仍有参考价值但非当前主线的文档迁入归档区，而不是散落在主线文档入口。

## 6. Labs 与实验区：保留但必须显式 opt-in

保留：

- `experimental/Bukit.Labs.Cli`
- `bukit.experimental.slnx`
- `guide/labs`
- `guide/labs-skills`

当前补充（2026-07-05）：legacy Labs Protocol 已删除，不再属于保留实验区；对应 source、sample plugin、protocol echo fixture、solution 引用和 coverage filter 入口都已移除。

理由：

- Core 1.0 明确只暴露稳定 CLI 命令。
- clone/import/webhook/theme registry/theme wizard/template command 等属于 Labs 或实验能力。
- 架构测试已锁定 Core CLI 不依赖 `Bukit.Importing` 与 Labs。

风险：

- Labs 与 Core 边界若文档表达不清，会导致误把实验能力当作 Core release blocker。
- `bukit.slnx` 与 `bukit.experimental.slnx` 的覆盖范围应继续在 README/architecture 中明确。

## 7. 需人工确认项

| 标记 | 路径 | 当前判断 | 需确认问题 |
|---|---|---|---|
| REVIEW-001 | `.trae/` | 工具/代理规则资产 | 团队是否仍将 Trae 作为一等协作工具？若否，应迁出仓库或并入 `guide/skills` 治理。 |
| REVIEW-002 | `src/plugins/WordCountSectionPlugin/` | 旧插件位置源码 | 是否仍有测试/文档依赖？是否应迁入 `experimental/` 或顶层 `plugins/`？ |
| REVIEW-003 | `labs/.gitkeep` | 空目录占位 | 是否仍需要顶层 `labs/`，还是由 `experimental/` 与 `guide/labs` 完全承担 Labs 边界？ |
| REVIEW-004 | `schemas/.gitkeep` | 空目录占位 | 主线 schema 已在 `docs/schemas/`；顶层 `schemas/` 是否仍需保留？ |
| REVIEW-005 | `examples/github-pages-workflow.yml` | 用户站点 workflow 示例 | 仍有文档引用，应保留；如未来 examples 扩展，应避免把生成站点输出提交进来。 |
| REVIEW-006 | `docs/plans/` | 历史计划与执行记录 | 建议按 active/done/archive 分层，但不建议直接删除。 |
| REVIEW-007 | `docs/superpowers/` | 旧规划/规格资料 | 是否仍作为长期治理资料？若否，迁入归档。 |

## 8. 门禁覆盖评估

### 8.1 已验证通过的轻量检查

本次报告生成前执行并通过：

```bash
bash scripts/checks/file-size.sh
bash scripts/checks/repo-hygiene.sh
bash scripts/checks/core-cli-contract.sh
bash scripts/checks/ci-workflow-action-pin.sh
bash scripts/checks/coverage-baseline-schema.sh
bash scripts/checks/docs-consistency.sh
bash scripts/checks/readme-sync.sh
bash scripts/checks/skills-schema.sh
bash guide/skills/scripts/validate-skills-strict.sh
bash scripts/checks/cli-docs-sync.sh
```

### 8.2 未运行的门禁

未运行：

```bash
dotnet restore bukit.slnx
dotnet build bukit.slnx -c Release
dotnet test bukit.slnx -c Release
bash scripts/gates/ci-fast.sh Release
bash scripts/gates/ci-full.sh Release
bash scripts/gates/release.sh Release
```

原因：本任务要求只读审计并生成报告；完整 build/test/release gate 会写入 `bin/`、`obj/`、`TestResults/` 等产物，不适合作为本轮扫描动作。

### 8.3 门禁缺口

`scripts/checks/repo-hygiene.sh` 当前只检查被 Git 跟踪的构建产物，例如 `.smoke-all-run/`、`.bukit-build-state.json`、`.bukit-output-marker`。它不会阻止 ignored 产物在工作树长期堆积，因此当前 `TestResults/` 约 1.1G 并不会导致该检查失败。

建议新增一个只读检查：

- 默认报告 ignored artifact 体积。
- 对 `TestResults/`、`.smoke-all-run/`、全仓 `bin/obj`、`.DS_Store` 给出清理提示。
- 不作为 CI 必须项，或仅在本地 maintenance gate 中启用。

## 9. 推荐清理顺序

### P0：无争议清理

1. 删除 ignored 生成物：`TestResults/`、`.smoke-all-run/`、全仓 `bin/obj`。
2. 删除全仓 `.DS_Store`。
3. 删除 `tests/*/TestResults/`。
4. 清理后运行：

```bash
git status --short
bash scripts/checks/repo-hygiene.sh
bash scripts/checks/docs-consistency.sh
```

### P1：规则与边界修正

1. 修正 `.gitignore` 与 `tests/fixtures/**` 的策略冲突。
2. 决定 `src/plugins/WordCountSectionPlugin` 去向。
3. 决定空目录 `labs/`、`schemas/` 是否仍需要。
4. 增加 ignored artifact 本地检查脚本。

### P2：文档归档治理

1. `docs/plans/` 标注 active/done/archive。
2. `docs/superpowers/` 判断是否迁入 archive。
3. `.trae/` 若仍保留，补充当前用途说明；若不保留，迁出或合并进主线技能体系。

## 10. 最终判定

当前 Bukit 仓库不需要进行大规模源码删除。最严格的清理判断如下：

- Core 主线源码、主线测试、主线脚本、主线文档应保留。
- Labs/experimental 应保留，但必须维持显式 opt-in，不得混入 Core 默认承诺。
- `guide-0.1/`、`scripts-0.1/` 应保留为备份，不得默认修改。
- `TestResults/`、`.smoke-all-run/`、`.DS_Store`、全仓 `bin/obj` 是当前最明确的移除对象。
- Notion 插件相关目录在当前工作树中的可见内容主要是构建残留；清理前不应误判为“删除 Notion 源码”。
- 下一步若执行清理，应先做 P0 无争议清理，再单独处理 `.gitignore`、`src/plugins/WordCountSectionPlugin`、`.trae/` 和历史文档归档策略。
