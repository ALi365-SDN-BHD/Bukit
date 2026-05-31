# Bukit Skills P0/P1 修复计划

## 问题确认

| # | 严重度 | 问题 | 确认行号 |
|---|--------|------|---------|
| P0-1 | P0 | CLI 命令表 merge 错误 — clone+geo audit 拼一行，docs check+version 拼一行 | L98, L105 |
| P0-2 | P0 | theme-component-system 写了 3 个不存在的 CLI 命令 | L340, L358, L380 |
| P1-3 | P1 | CLI reference 缺失 build --allow-external-plugins，preview --config/--site，geo audit 多了 --config | 全文件 |
| P1-4 | P1 | skills-index.yaml 缺少 status/source_anchors 等元数据 | 全 YAML |
| P1-5 | P1 | source_anchors 太粗（`src/Bukit.Engine/`），theme-component-system 重复 | 多个 SKILL.md |
| P1-6 | P1 | bukit-design-tokens Tailwind CDN 用 `<link>` 而非 `<script>` | L231, L252 |
| P2-7 | P2 | validate-skills-strict.sh 仍是结构校验，缺语义校验 | 整个脚本 |

---

## Phase 1: P0 立即修复

### 1.1 修复 CLI 表格合并错误（命令行修复）

**文件**: `src/skills/bukit-cli-reference/SKILL.md`

**当前（错误）**:
- L98: `| clone (beta) | ... || geo audit | GEO audit on dist output | --dir --config |`
- L105: `| docs check (beta) | ... || version | Output version number | No parameters |`

**目标（正确）**:
```
| `clone` (beta) | Generate Bukit theme and content from target website | `--tokens` `--theme` `--layout` `--page` `--sections` `--behaviors` `--icons` `--assets` `--brand` `--use` `--force` `--verify` `--visual-threshold` `--fail-on-visual-diff` `--fidelity` `--config` `--site` |
...
| `geo audit` | GEO audit on dist output | `--dir` |
...
| `docs check` (beta) | Check documentation consistency (README/guide/skills) | `--cli` `--config-fields` `--file-refs` `--examples` `--skills` |
| `version` | Output version number | No parameters |
```

**修复方法**: 用 sed 将 `||` 拆分为独立的行。

### 1.2 修复 geo audit 参数

geo audit 真实 CLI 只支持 `--dir`，没有 `--config`（来自 [BukitCliSpecs.cs:L428-L434](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs#L428-L434)）。

修复：`--dir --config` → `--dir`

### 1.3 修复 theme-component-system 不存在的命令

**文件**: `src/skills/theme-component-system/SKILL.md`

三个不存在的命令：
- `bukit theme doctor` (L340-356)
- `bukit theme list-components` (L358-378)
- `bukit theme export-catalog` (L380-396)

以及相关引用行：L543, L545, L656

**处理方式**: 
由于这些命令在源码 ThemeCommand.cs 中存在实现逻辑（通过名称匹配分发）但未在 BukitCliSpecs.cs 注册，属于隐藏/内部命令。将它们标记为 `(planned/internal)` 并加警告：
- 保留章节标题改为 `### bukit theme doctor (planned)`
- 添加注释：「此命令存在内部实现但未在 CLI 注册表中注册，可能不可用」
- 所有命令示例加 `# (planned - not yet available in CLI)` 注释

---

## Phase 2: P1 修复

### 2.1 CLI reference 补齐缺失参数

#### 2.1.1 build --allow-external-plugins

**来源**: [BukitCliSpecs.cs:L28](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs#L28)

在 build 命令的 Quick Reference 行和详细参数表中添加:
```
| `--allow-external-plugins` | false | Allow loading external protocol plugins (overrides site.externalPluginPolicy) |
```

#### 2.1.2 preview --config / --site

**来源**: [BukitCliSpecs.cs:L41-L42](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs#L41-L42)

在 preview Quick Reference 行末尾添加 `--config` `--site`：
```
| `preview` | Static preview of dist/ | `--dir` `--host` `--port` `--strict-port` `--config` `--site` |
```

### 2.2 将 status 元数据同步到 skills-index.yaml

在每个 skill entry 添加：
```yaml
status: stable|beta
since: "v3.0.0"
source_anchors:
  - "具体源码路径"
verified_by:
  - "具体测试路径"
```

**文件**: `src/skills/skills-index.yaml`

对 19 个 skill 逐一添加。同步后需重新生成 `skills-index.json`。

同时在 `validate-skills-strict.sh` 中添加检查：YAML 的 status 必须与 SKILL.md 的 status 一致。

### 2.3 精细化 source_anchors

对 19 个 SKILL.md，将粗粒度路径改为具体文件：

| Skill | 当前（太粗） | 改为 |
|-------|-------------|------|
| bukit-cli-reference | `src/Bukit.Cli/` | `src/Bukit.Cli/Cli/BukitCliSpecs.cs` |
| bukit-config | `src/Bukit.Config/` | `src/Bukit.Config/AppConfig.cs`, `src/Bukit.Config/ConfigLoader.cs` |
| bukit-theme | `src/Bukit.Engine/` | `src/Bukit.Cli/Commands/ThemeCommand.cs` |
| bukit-templating | `src/Bukit.Engine/` | `src/Bukit.Engine/Plugins/BuiltIn/PagesIndexPlugin.cs` |
| bukit-design-tokens | `src/Bukit.Engine/` | `src/Bukit.Cli/Commands/ThemeCommand.cs` |
| bukit-content-to-template | `src/Bukit.Engine/` | `src/Bukit.Engine/ContentSchemaValidator.cs` |
| bukit-notion | `src/Bukit.Engine/` | `src/Bukit.Engine/ContentProviderFactory.cs` |
| bukit-routing | `src/Bukit.Engine/` | `src/Bukit.Engine/BuildPlanner.cs` |
| bukit-i18n | `src/Bukit.Engine/I18nOutputMerger.cs` | 已精确 ✅ |
| bukit-plugins-debug | `src/Bukit.Engine/Plugins/` | `src/Bukit.Engine/Plugins/PluginRegistry.cs`, `src/Bukit.Engine/Plugins/PluginRunner.cs` |
| bukit-deploy | `src/Bukit.Cli/Commands/DeployCommand.cs` | 已精确 ✅ |
| bukit-clone | `src/Bukit.Cli/Commands/CloneCommand.cs` | 已精确 ✅ |
| bukit-seo | `src/Bukit.Engine/SeoDiagnostics.cs` | 已精确 ✅ |
| bukit-geo | `src/Bukit.Engine/SeoDiagnostics.cs`, `src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs` | 已精确 ✅ |
| bukit-preview | `src/Bukit.Cli/Commands/PreviewCommand.cs` | 已精确 ✅ |
| bukit-dev | `src/Bukit.Cli/Commands/DevCommand.cs` | 已精确 ✅ |
| bukit-webhook | `src/Bukit.Cli/Commands/WebhookCommand.cs` | 已精确 ✅ |
| theme-component-system | `src/Bukit.Engine/` (重复2次) | `src/Bukit.Cli/Commands/ThemeCommand.cs`, `src/Bukit.Cli/Cli/BukitCliSpecs.cs` |
| using-bukit | `src/skills/using-bukit/` | `src/skills/using-bukit/SKILL.md` |

### 2.4 修复 Tailwind CDN 示例

**文件**: `src/skills/bukit-design-tokens/SKILL.md`

L231: `<link rel="stylesheet" href="https://cdn.tailwindcss.com" />`
改为: `<script src="https://cdn.tailwindcss.com"></script>`

L252: 从 `external_css` 列表中移除 `https://cdn.tailwindcss.com`，或改为在正文注释「如需 Tailwind，建议通过构建工具集成而非 CDN」。

---

## Phase 3: P2 增强验证

### 3.1 新增 Markdown 表格校验到 strict validator

在 `validate-skills-strict.sh` 中添加：
- 检查 CLI reference 表格每行的 `|` 数量一致
- 检查不允许 `||` 出现在表格行中
- 检查命令名不重复
- 检查 Quick Reference table 的 `||` 分割符问题

### 3.2 新增 CLI 命令验证

在 `validate-skills-strict.sh` 中添加：
- 从 `src/Bukit.Cli/Cli/BukitCliSpecs.cs` 提取所有命令
- 检查 CLI reference 中的命令是否与源一致

### 3.3 新增 status 一致性检查

在 `validate-skills-strict.sh` 中添加：
- 每个 skill 的 SKILL.md status 与 skills-index.yaml status 一致

---

## 执行顺序

```
Phase 1 (P0)
├── 1.1 修复 CLI 表格合并
├── 1.2 修复 geo audit 参数
└── 1.3 修复 theme-component-system 假命令

Phase 2 (P1)
├── 2.1 CLI reference 补齐缺失参数
├── 2.2 skills-index.yaml 同步元数据
├── 2.3 精细化 source_anchors
└── 2.4 修复 Tailwind CDN 示例

Phase 3 (P2)
├── 3.1 Markdown 表格校验
├── 3.2 CLI 命令验证
└── 3.3 status 一致性检查

验证
└── validate-skills-strict.sh + dotnet test
```

## 文件变更汇总

| 文件 | 变更类型 |
|------|---------|
| `bukit-cli-reference/SKILL.md` | 修复表格合并、geo audit 参数、补齐缺失参数 |
| `theme-component-system/SKILL.md` | 标记假命令为 planned |
| `bukit-design-tokens/SKILL.md` | 修复 Tailwind CDN 示例 |
| `skills-index.yaml` | 添加 status/source_anchors 等元数据 |
| `skills-index.json` | 重新生成 |
| `19 个 SKILL.md` | 精细化 source_anchors |
| `scripts/validate-skills-strict.sh` | 添加语义校验 |
