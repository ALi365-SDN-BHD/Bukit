# Bukit Core 1.0.5 / 1.0.6 合并开发修复计划书

> Historical planning record. Do not execute these commands as the current
> coverage or release contract. Use `release-prerelease-template.md` and
> `.github/workflows/ci.yaml` for the active contract.

文档版本：v1.0  
目标版本：Bukit Core 1.0.5 / 1.0.6 合并维护周期  
版本类型：维护补丁 / Regression Hardening / Docs & Schema Drift Hardening  
建议路径：`docs/releases/bukit-core-1.0.5-1.0.6-combined-development-plan.md`  
适用分支：`main` / `release/1.0.x`  
适用范围：Bukit Core 1.0.x 维护线  
不适用范围：Labs 功能扩展、Import / Clone 插件化、Theme Registry、Plugin Marketplace、BukitJalil、真正 HMR

---

## 1. 版本定位

Bukit Core 1.0.5 与 1.0.6 原本可以分别定位为：

```text
1.0.5 = Dev / Preview / Deploy regression hardening
1.0.6 = Docs / Skills / Schema drift hardening
```

合并后建议统一为：

```text
1.0.5-1.0.6 合并维护周期
= Core runtime 边界行为补强
+ Dev / Preview / Deploy 回归测试补强
+ Docs / Skills / Config Schema 防漂移
+ Release 后长期维护稳定性补强
```

这个合并版本不做新功能，不扩大 Core 命令面，不把 Labs 能力带回 Core。

---

## 2. 总体目标

合并开发周期的目标是把 Bukit Core 1.0.x 从“发布链稳定”推进到“用户运行路径稳定”。

前几轮 1.0.x 已经重点处理：

```text
CI / Release Gate
Workflow Evidence
Coverage Gate
Release Manifest
Checksums
Release Asset Strict Validation
Docs / Skills Core 边界
```

本阶段继续处理：

```text
Dev server 行为稳定性
Preview path safety
Deploy failure-path safety
Config / Schema / Docs / Skills drift prevention
CLI docs sync
README 多语言一致性
Release 后回归测试矩阵
```

---

## 3. 非目标范围

以下内容不进入本合并维护周期：

```text
bukit clone
bukit import
theme wizard
theme registry
plugin marketplace
external runtime plugin loading
webhook automation
true HMR
remote theme source
Notion push / migration
BukitJalil GUI
AI 自动生成 skills
```

这些内容只能进入：

```text
Labs
1.1.0+
独立插件化设计
独立产品线
```

---

## 4. 维护原则

### 4.1 Core CLI 不扩展

Core CLI 仍只允许：

```text
build
doctor
config
preview
dev
clean
version
completion
seo
geo
publish
deploy
```

不得加入：

```text
clone
import
theme
plugin
webhook
intent
visual
data
notion command
```

### 4.2 Labs / Importing 继续 tracked-only

Importing 与 Labs 可以继续追踪 coverage，但不阻塞 Core 1.0.x。

推荐策略：

```text
core >= 80 blocking
cli >= 75 blocking
importing tracked only
labs tracked only
```

### 4.3 任何用户可见文案不得回退

禁止在 Core 用户路径中出现：

```text
HMR
Hot Module Replacement
bukit clone
bukit import
bukit theme
bukit plugin
bukit webhook
bukit intent
--allow-external-plugins
```

允许出现的位置仅限：

```text
guide/labs/**
guide/archive/**
历史迁移说明
明确标注为非 Core 的路线图文档
```

---

# Part A：1.0.5 — Dev / Preview / Deploy Regression Hardening

---

## 5. P0：Dev server 回归补强

### P0-1：DevFileWatcher 快速变更 debounce 测试

#### 背景

`bukit dev` 已经实现：

```text
watch file changes
incremental rebuild
WebSocket reload
browser full refresh
```

但真实开发中，编辑器保存文件常会触发多次 file system event。

#### 修复内容

新增测试：

```text
DevFileWatcher_RapidChanges_DebouncedToSingleRebuild
DevFileWatcher_MultipleEventsWithinDebounceWindow_OnlyOneBuild
DevFileWatcher_EventsAfterDebounce_TriggerNewBuild
```

#### 验收标准

连续触发多次事件时：

```text
300ms debounce window 内只触发一次 rebuild
debounce window 后的新事件可以再次触发 rebuild
```

---

### P0-2：DevFileWatcher rebuild 失败后继续工作

#### 背景

模板错误、配置错误、内容错误都可能导致 rebuild 失败。  
dev server 不能因为一次失败就停止 watch。

#### 修复内容

新增测试：

```text
DevFileWatcher_RebuildFailure_DoesNotDisposeWatcher
DevFileWatcher_RebuildFailure_AllowsNextSuccessfulRebuild
DevFileWatcher_RebuildFailure_DoesNotBroadcastReload
```

#### 验收标准

```text
第一次 rebuild 抛异常
watcher 仍然存活
下一次修复文件后可以成功 rebuild
失败时不发送 reload
成功时发送 reload
```

---

### P0-3：`--no-watch` 行为验证

#### 修复内容

新增测试：

```text
DevCommand_NoWatch_DoesNotStartWatcher
DevCommand_NoWatch_ServesStaticOutput
DevCommand_NoWatch_DoesNotPrintLiveReloadWatchingMessage
```

#### 验收标准

```bash
bukit dev --no-watch
```

应：

```text
执行 initial build
启动 static dev server
不启动 FileSystemWatcher
不监听文件变化
不打印 watching directories
```

---

## 6. P0：Preview / Dev path safety fuzz

### P0-4：Preview / Dev 路径穿越 fuzz 测试

#### 背景

Preview / Dev 都会从 output dir 读取静态文件。必须防止 URL 编码、双重编码、Windows path separator、Unicode normalization 造成目录穿越。

#### 新增测试用例

```text
Preview_RejectsEncodedDotDotPath
Preview_RejectsDoubleEncodedDotDotPath
Preview_RejectsBackslashTraversal
Preview_RejectsMixedSeparatorTraversal
Preview_RejectsUnicodeNormalizationTraversal
Preview_RejectsVeryLongPathWithoutCrash
DevRequestHandler_RejectsEncodedDotDotPath
DevRequestHandler_RejectsBackslashTraversal
DevRequestHandler_RejectsNullByteEncodedPath
```

#### 测试路径

```text
/%2e%2e/
/..%2f
/%252e%252e/
/assets/%2e%2e/secret
/foo/../bar
/%5c..%5csecret
/%00
/very-long-path...
```

#### 验收标准

所有非法路径应返回：

```text
403 或 404
不得读取 root 外文件
不得抛出未处理异常
不得泄露本机路径
```

---

## 7. P0：Deploy failure-path safety

### P0-5：GITHUB_TOKEN 泄露防护

#### 背景

Deploy 使用 GitHub token、askpass script、git push。任何错误日志都不能泄露 token。

#### 修复内容

新增测试：

```text
Deploy_ErrorMessage_DoesNotContainGitHubToken
Deploy_GitFailure_DoesNotLogToken
Deploy_PushFailure_SanitizesToken
Deploy_AskpassScript_DoesNotLeakInError
```

#### 验收标准

当 token 为：

```text
ghp_TEST_SECRET_TOKEN_123
```

任何 stderr、logger、DeployResult.Error 中都不得包含该字符串。

---

### P0-6：Deploy 临时目录与 askpass 清理

#### 修复内容

新增测试：

```text
Deploy_AskpassFile_IsDeletedAfterSuccess
Deploy_AskpassFile_IsDeletedAfterFailure
Deploy_TempDir_IsDeletedAfterSuccess
Deploy_TempDir_IsDeletedAfterFailure
```

#### 验收标准

无论 deploy 成功、失败、push rejected、git missing：

```text
tempDir 被清理
askpass script 被清理
不留下 token 文件
```

---

### P0-7：Deploy remote URL 解析矩阵

#### 背景

GitHub remote URL 可能有多种格式。

#### 测试矩阵

```text
git@github.com:owner/repo.git
https://github.com/owner/repo.git
https://github.com/owner/repo
ssh://git@github.com/owner/repo.git
https://github.com/owner/repo/
non-github remote
malformed remote
```

#### 新增测试

```text
Deploy_GitHubRemoteUrlParser_SupportsSshScpStyle
Deploy_GitHubRemoteUrlParser_SupportsHttpsGit
Deploy_GitHubRemoteUrlParser_SupportsHttpsWithoutGitSuffix
Deploy_GitHubRemoteUrlParser_RejectsNonGitHubRemote
Deploy_GitHubRemoteUrlParser_RejectsMalformedRemote
```

#### 验收标准

GitHub remote 可正确解析 owner / repo。  
非 GitHub remote 应返回明确错误：

```text
Unable to determine GitHub repository
```

---

### P0-8：Deploy non-fast-forward 行为

#### 修复内容

新增测试：

```text
Deploy_NonFastForwardWithoutForce_ReturnsFriendlyError
Deploy_NonFastForwardWithForce_UsesForcePush
Deploy_ForceFlag_OnlyAffectsPush
```

#### 验收标准

无 `--force` 时：

```text
Non-fast-forward push rejected...
```

有 `--force` 时：

```text
git push --force origin <branch>
```

---

# Part B：1.0.6 — Docs / Skills / Schema Drift Hardening

---

## 8. P0：CLI facts 单一事实源

### P0-9：CLI 文档自动比对 `BukitCliSpecs.cs`

#### 背景

Core CLI 命令列表同时存在于：

```text
BukitCliSpecs.cs
guide/user/12-cli-reference.md
guide/dev/cli.md
guide/skills/bukit-cli-reference/SKILL.md
README.md
README.zh-CN.md
README.ms.md
```

容易漂移。

#### 修复内容

新增脚本：

```text
scripts/checks/cli-docs-sync.sh
```

脚本应从 `BukitCliSpecs.cs` 提取：

```text
commands
subcommands
options
arguments
```

并检查以下文件是否一致：

```text
guide/user/12-cli-reference.md
guide/dev/cli.md
guide/skills/bukit-cli-reference/SKILL.md
README.md
README.zh-CN.md
README.ms.md
```

#### 验收标准

任何文档：

```text
少写命令
多写命令
写错 option
出现 non-Core command
```

都必须失败。

---

## 9. P0：site.yaml schema / strict validator / docs 三方一致

### P0-10：Config field drift checker

#### 背景

此前最大风险之一是：

```text
AppConfig
ConfigStrictFieldValidator
ConfigLoader
ConfigJsonSchemaGenerator
docs
skills
```

之间字段漂移。

#### 修复内容

新增测试或脚本：

```text
scripts/checks/config-contract-sync.sh
```

或新增测试：

```text
ConfigContract_DocsDoNotReferenceUnknownFields
ConfigContract_SkillsDoNotReferenceUnknownFields
ConfigContract_JsonSchemaMatchesStrictValidator
ConfigContract_LoaderReadsStableFields
```

扫描文件：

```text
guide/user/04-site-yaml-config.md
guide/dev/config-site-yaml.md
guide/skills/bukit-config/SKILL.md
README*.md
```

验证字段来源：

```text
src/Bukit.Config/AppConfig.cs
src/Bukit.Config/ConfigStrictFieldValidator.cs
src/Bukit.Config/ConfigJsonSchemaGenerator.cs
```

#### 验收标准

文档不得引用不存在的字段，例如：

```text
theme.source
theme.extends
site.externalPlugins
content.provider
content.notion
content.markdown
deploy.options
```

---

## 10. P0：Core 用户可见文本防回退

### P0-11：Core text drift guard

#### 修复内容

新增 Architecture Test：

```text
CoreUserFacingText_DoesNotLeakNonCoreCommands
CoreUserFacingText_UsesLiveReloadNotHmr
```

扫描：

```text
src/Bukit.Cli/**
src/Bukit.Config/**
src/Bukit.Engine/**
README*.md
guide/user/**
guide/dev/**
guide/skills/**
```

禁止：

```text
HMR
Hot Module Replacement
bukit clone
bukit import
bukit theme
bukit plugin
bukit webhook
bukit intent
bukit data
bukit visual
bukit theme manifest
--allow-external-plugins
```

允许：

```text
guide/labs/**
guide/archive/**
tests/fixtures/**
scripts/checks/core-cli-contract.sh
```

#### 验收标准

```bash
dotnet test tests/Bukit.Architecture.Tests -c Release
```

必须通过。

---

## 11. P1：README 多语言同步检查

### P1-1：README sync checker

#### 修复内容

新增：

```text
scripts/checks/readme-sync.sh
```

检查：

```text
README.md
README.zh-CN.md
README.ms.md
```

必须同时包含：

```text
Core command list
guide/user
guide/dev
guide/skills
Labs exclusion
LiveReload wording
GitHub Pages deploy
Core/Labs boundary
```

#### 验收标准

三份 README 的 Core 命令集合一致：

```text
build
doctor
config
preview
dev
clean
version
completion
seo
geo
publish
deploy
```

任何 README 出现：

```text
bukit clone
bukit import
bukit theme
bukit plugin
HMR
```

必须失败。

---

## 12. P1：Skills Pack schema 化

### P1-2：skills-index schema

#### 修复内容

新增：

```text
docs/schemas/skills-index.v1.json
docs/schemas/skill-frontmatter.v1.json
scripts/checks/skills-schema.sh
```

检查：

```text
guide/skills/skills-index.yaml
guide/skills/plugin.json
guide/skills/**/SKILL.md
```

验证：

```text
skill_count 准确
core_commands 与 BukitCliSpecs 一致
labs_not_core 不进入 plugin.json
source_anchors 存在
verified_by 存在
guide_chapters 存在
requires 指向有效 skill
workflow chain 指向有效 skill
```

#### 验收标准

```bash
bash scripts/checks/skills-schema.sh
bash guide/skills/scripts/validate-skills-strict.sh
```

都必须通过。

---

## 13. P1：Coverage baseline Python 化

### P1-3：替换 awk JSON parser

#### 背景

当前 coverage baseline 可读性已增强，但若仍使用 shell / awk 解析 JSON，长期不稳。

#### 修复内容

新增：

```text
scripts/checks/read-coverage-baseline.py
```

用法：

```bash
python3 scripts/checks/read-coverage-baseline.py docs/coverage-baselines.json core minimum
python3 scripts/checks/read-coverage-baseline.py docs/coverage-baselines.json labs baseline
```

修改：

```text
scripts/checks/coverage.sh
```

用 Python 读取 baseline。

#### 验收标准

coverage 输出仍为：

```text
Coverage core: >= 80
Coverage cli: >= 75
Coverage importing: baseline 39.70
Coverage labs: baseline 62.96
```

---

## 14. P1：Coverage baseline schema

### P1-4：coverage-baselines schema

新增：

```text
docs/schemas/coverage-baselines.v1.json
scripts/checks/coverage-baseline-schema.sh
```

要求：

```json
{
  "core": {
    "blocking": true,
    "minimum": 80
  },
  "cli": {
    "blocking": true,
    "minimum": 75
  },
  "importing": {
    "blocking": false,
    "baseline": 39.70
  },
  "labs": {
    "blocking": false,
    "baseline": 62.96
  }
}
```

#### 验收标准

以下情况必须失败：

```text
core.blocking=false
cli.blocking=false
core.minimum missing
cli.minimum missing
importing.blocking=true without explicit decision
labs.blocking=true without explicit decision
```

---

# Part C：共同增强项

---

## 15. P1：Release artifact smoke 扩展

### 修复内容

增强：

```text
scripts/smoke/release-artifacts.sh
```

新增检查：

```bash
"$binary" dev --help | grep -q "LiveReload"
! "$binary" dev --help | grep -q "HMR"

"$binary" --help > "$tmp/help.txt"
! grep -E "clone|import|webhook|theme wizard|plugin marketplace" "$tmp/help.txt"

"$binary" deploy --help | grep -q "github-pages"
```

如 fixture 稳定，加入：

```bash
"$binary" deploy --dry-run --skip-build --config "$fixture/site.yaml"
```

#### 验收标准

Release binary smoke 覆盖：

```text
version
help
config schema
config check
doctor
build
seo audit
geo audit
publish audit
dev LiveReload wording
non-Core command absence
deploy help / dry-run
```

---

## 16. P2：Preview / Dev fuzz 扩展

新增测试：

```text
Preview_RejectsEncodedDotDotPath
Preview_RejectsDoubleEncodedTraversal
Preview_RejectsBackslashTraversal
Preview_RejectsNullByteEncodedPath
DevRequestHandler_RejectsEncodedDotDotPath
DevRequestHandler_HandlesVeryLongPath
DevRequestHandler_DoesNotInjectLiveReloadIntoNonHtml
```

测试路径：

```text
/%2e%2e/
/..%2f
/%252e%252e/
/assets/%2e%2e/secret
/%5c..%5csecret
/%00
```

---

## 17. P2：Release checklist 文档化

新增：

```text
guide/dev/release-checklist.md
```

必须包含：

```text
1. Merge to main
2. Wait for ci.yml completed success
3. Confirm workflow evidence
4. Confirm coverage summary
5. Create tag
6. Release workflow
7. Confirm release-assets-check.md
8. Confirm checksums.txt
9. Confirm release-manifest.json
10. Download artifact and run smoke locally
```

---

# Part D：任务拆分

---

## 18. Task 1：Dev / Preview regression tests

范围：

```text
DevFileWatcher debounce
Dev rebuild failure
--no-watch
Preview path traversal
Dev path traversal
LiveReload injection behavior
```

验收：

```bash
dotnet test tests/Bukit.Cli.Tests -c Release --filter Dev
dotnet test tests/Bukit.Cli.Tests -c Release --filter Preview
```

---

## 19. Task 2：Deploy failure-path tests

范围：

```text
token redaction
askpass cleanup
temp dir cleanup
remote URL parsing
empty output
non-fast-forward
force push
```

验收：

```bash
dotnet test tests/Bukit.Cli.Tests -c Release --filter Deploy
```

---

## 20. Task 3：CLI docs sync

范围：

```text
BukitCliSpecs.cs
guide/user/12-cli-reference.md
guide/dev/cli.md
guide/skills/bukit-cli-reference/SKILL.md
README*.md
```

验收：

```bash
bash scripts/checks/cli-docs-sync.sh
```

---

## 21. Task 4：Config contract sync

范围：

```text
AppConfig
Strict validator
Config loader
JSON schema generator
Docs
Skills
README
```

验收：

```bash
bash scripts/checks/config-contract-sync.sh
dotnet test tests/Bukit.Config.Tests -c Release
```

---

## 22. Task 5：Core text drift guard

范围：

```text
HMR wording
non-Core command leakage
old plugin/import/theme references
```

验收：

```bash
dotnet test tests/Bukit.Architecture.Tests -c Release
```

---

## 23. Task 6：README / Skills schema

范围：

```text
README multilingual sync
skills-index schema
skill frontmatter schema
plugin.json consistency
```

验收：

```bash
bash scripts/checks/readme-sync.sh
bash scripts/checks/skills-schema.sh
bash guide/skills/scripts/validate-skills-strict.sh
```

---

## 24. Task 7：Coverage baseline hardening

范围：

```text
Python JSON parser
coverage-baselines schema
coverage summary consistency
```

验收：

```bash
bash scripts/checks/coverage-baseline-schema.sh
bash scripts/checks/coverage.sh Release
```

---

# Part E：合并版发布门禁

1.0.5 / 1.0.6 合并周期完成后，必须通过：

```bash
bash scripts/gates/ci-fast.sh Release
CORE_COVERAGE_THRESHOLD=80 CLI_COVERAGE_THRESHOLD=75 bash scripts/gates/ci-full.sh Release
RELEASE_GATE_RIDS=linux-x64 bash scripts/gates/release.sh Release
```

新增必须通过：

```bash
bash scripts/checks/cli-docs-sync.sh
bash scripts/checks/config-contract-sync.sh
bash scripts/checks/readme-sync.sh
bash scripts/checks/skills-schema.sh
bash scripts/checks/coverage-baseline-schema.sh
```

测试必须通过：

```bash
dotnet test bukit.slnx -c Release
dotnet test tests/Bukit.Architecture.Tests -c Release
dotnet test tests/Bukit.Cli.Tests -c Release
dotnet test tests/Bukit.Config.Tests -c Release
```

---

# Part F：Release Notes 草案

```text
Bukit Core 1.0.5 / 1.0.6 combined maintenance release focuses on runtime regression hardening and documentation/schema drift prevention.

Highlights:
- Strengthened dev server file watcher regression coverage.
- Added preview/dev path traversal fuzz tests.
- Expanded deploy failure-path tests for token redaction, temporary cleanup, remote URL parsing, and non-fast-forward handling.
- Added CLI docs sync checks against BukitCliSpecs.
- Added config contract sync checks across AppConfig, strict validator, schema, docs, and skills.
- Added Core user-facing text drift guards.
- Added README multilingual sync checks.
- Added skills schema validation.
- Replaced fragile coverage baseline parsing with a dedicated parser.
- Added coverage-baseline schema validation.
- Expanded release artifact smoke tests.
```

---

# 最终结论

**1.0.5 / 1.0.6 合并后，重点不是继续修 release pipeline，而是把真实用户运行路径和文档事实源都纳入防回退体系。**

合并周期完成后，Bukit 1.0.x 应达到：

```text
Dev / Preview / Deploy 行为稳定
Config / Schema / Docs / Skills 不漂移
CLI 文档与源码一致
README 多语言一致
Release artifact smoke 更完整
Coverage baseline 可长期维护
Core / Labs 边界继续稳定
```

这会为后续进入 **1.1.0 插件化 / 可选内置功能扩展** 提供更安全的基础。
