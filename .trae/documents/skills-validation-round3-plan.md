# Bukit Skills 验证体系第三轮修复计划

**日期**: 2026-05-31
**类型**: Bug Fix + 工程增强
**影响范围**: `src/skills/scripts/`、`src/skills/bukit-cli-reference/SKILL.md`、`src/skills/QUALITY_REPORT.md`、`src/skills/skills-index.yaml`、`src/skills/bukit-content-to-template/SKILL.md`、`src/skills/theme-component-system/SKILL.md`

---

## 概述

在第二轮修复（全部 strict validations 通过）后，新发现 10 个问题，其中 2 个 P0（CLI Reference 参数缺失）、5 个 P1（校验深度不足 + 文档滞后）、3 个 P2（source anchors 偏窄 + 错误表述 + 校验范围偏窄）。

---

## P0-1：CLI Reference `theme pack` 漏掉 `--output`

### 确认状态

| 来源 | `theme pack` 参数 |
|------|------------------|
| [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs#L183-L192) | `[name]` `--output` `--config` `--site` |
| CLI 用法行 ([SKILL.md:L329](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-cli-reference/SKILL.md#L329)) | `[name] [--output <path>] [--config <path>] [--site <name>]` |
| Quick Reference 表格 ([SKILL.md:L83](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-cli-reference/SKILL.md#L83)) | `[name] --config --site` ← **缺 `--output`** |

### 修复

**文件**: [bukit-cli-reference/SKILL.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-cli-reference/SKILL.md) 第 83 行

```
| `theme pack` | Package theme as `<name>-<version>.tar.gz` | `[name]` `--output` `--config` `--site` |
```

---

## P0-2：CLI Reference `theme install` 漏掉 `--registry-url`

### 确认状态

| 来源 | `theme install` 参数 |
|------|---------------------|
| [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs#L193-L204) | `<source>` `--registry` `--registry-url` `--force` `--config` `--site` |
| CLI 用法行 ([SKILL.md:L330](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-cli-reference/SKILL.md#L330)) | `<path\|url> [--registry <name>] [--registry-url <url>] [--force] [--config <path>] [--site <name>]` |
| Quick Reference 表格 ([SKILL.md:L84](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-cli-reference/SKILL.md#L84)) | `<path\|url> --registry <name> --force --config --site` ← **缺 `--registry-url`** |

### 修复

**文件**: [bukit-cli-reference/SKILL.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-cli-reference/SKILL.md) 第 84 行

```
| `theme install` | Install theme from local file, URL, or registry | `<path\|url>` `--registry <name>` `--registry-url` `--force` `--config` `--site` |
```

---

## P1-3：check-cli-commands.py 只校验命令名，不校验参数

### 确认状态

当前 Check 12 的逻辑（[check-cli-commands.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-cli-commands.py)）：

```python
# Phase 2: 只提取命令名
m = re.match(r'\| `([^`]+)` \|', clean_line)
...
ref_commands.add(cmd)  # 只存 "theme pack"，不存参数

# Phase 3: 只比较命令集合
source_only = sorted(source_normalized - ref_normalized - PLANNED_COMMANDS)
ref_only = sorted(ref_normalized - source_normalized - PLANNED_COMMANDS)
```

这意味着 `theme pack --output` 和 `theme install --registry-url` 的缺失**完全不可见**。

### 修复方案

分两个阶段实施：

**阶段 A（本次）**：修复 Quick Reference 表格中已发现的两个参数缺失（P0-1、P0-2）。

**阶段 B（计划）**：增强 check-cli-commands.py 参数校验能力：

1. Phase 2 改造：解析 Quick Reference 表格时，同时提取 `Key Parameters` 列中列出的参数名，构建 `ref_options: dict[str, set[str]]` 映射（命令 → 参数集合）。

2. Phase 3 改造：对每个匹配的命令，比较 `source_options[cmd]` vs `ref_options[cmd]`，报告缺失/多出/拼写错误的参数。

3. 需要处理的情况：
   - `<name>` / `<path>` 等 argument 占位符（不是 option，不需要比较）
   - `--registry <name>` 中的 `<name>` 是 option 的值名，不是独立 option
   - Flag 类型 option（如 `--force`）没有 value name
   - `--incremental / --no-incremental` 互斥写法

> **注**：阶段 B 的完整实现工作量较大，建议在本轮修复中只做阶段 A + 在 QUALITY_REPORT.md Remaining Risks 中添加参数校验 gap 条目。阶段 B 应在新 PR 中独立实现。

---

## P1-4：skills-index.json sync 校验过浅

### 确认状态

[validate-skills-strict.sh](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/validate-skills-strict.sh) Check 8 当前逻辑（第 215-245 行）：

```python
# 只比较 skill_count
if ydata.get('skill_count') != jdata.get('skill_count'):
    print(f'DISCREPANCY: YAML skill_count=... vs JSON skill_count=...')

# 只比较 skill names
ynames = sorted(s['name'] for s in ydata.get('skills',[]))
jnames = sorted(s['name'] for s in jdata.get('skills',[]))
```

这意味着如果 YAML 中修改了 `status`、`description`、`triggers`、`requires`、`source_anchors`、`guide_chapters`、`workflow chain`、`platform_loading`，但 JSON 未重新生成，只要 skill 数量和名称没变，**当前检查仍会通过**。

### 修复

**文件**: [validate-skills-strict.sh](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/validate-skills-strict.sh) Check 8（第 213-245 行）

改为深度对比（canonical JSON dump 后逐字比对）：

```bash
python3 -c "
import json, yaml, sys
with open('$INDEX_YAML') as f:
    ydata = yaml.safe_load(f)
with open('$INDEX_JSON') as f:
    jdata = json.load(f)

# Canonical comparison ignoring key order
ycanon = json.dumps(ydata, sort_keys=True, ensure_ascii=False)
jcanon = json.dumps(jdata, sort_keys=True, ensure_ascii=False)

if ycanon != jcanon:
    print('DISCREPANCY: skills-index.json does not exactly match skills-index.yaml')
    # Show first diff line
    ylines = ycanon.split('\n')
    jlines = jcanon.split('\n')
    for i, (yl, jl) in enumerate(zip(ylines, jlines)):
        if yl != jl:
            print(f'  First diff at line {i+1}:')
            print(f'    YAML: {yl[:100]}')
            print(f'    JSON: {jl[:100]}')
            break
    sys.exit(1)
print('MATCH')
" 2>/dev/null
```

同时确保 Check 8 在 `generate-index-json.sh` 未运行时也能准确检测。

---

## P1-5：plugin.json sync 只校验 skill 名，不校验路径

### 确认状态

[validate-skills-strict.sh](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/validate-skills-strict.sh) Check 2（第 56-85 行）：

```python
pset = set(s.replace('/SKILL.md','') for s in pj.get('skills',[]))
iset = set(s['name'] for s in idx.get('skills',[]))
missing_in_plugin = iset - pset
extra_in_plugin = pset - iset
```

只比较 **name 集合**，不校验：
- plugin.json 中的路径是否等于 index 中的 `path` 字段
- plugin.json 的 `version` 是否等于 index 的 `version`
- plugin.json `description` 是否仍然准确

### 修复

**文件**: [validate-skills-strict.sh](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/validate-skills-strict.sh) Check 2（第 56-85 行）

增强为：

```python
import json, yaml, sys

with open(PLUGIN_JSON) as f:
    pj = json.load(f)
with open(INDEX_YAML) as f:
    idx = yaml.safe_load(f)

errors = 0

# 1. name match (existing)
pset = set(s.replace('/SKILL.md','') for s in pj.get('skills',[]))
iset = set(s['name'] for s in idx.get('skills',[]))
if iset - pset:
    print(f'MISSING_IN_PLUGIN:{",".join(sorted(iset-pset))}')
    errors += 1
if pset - iset:
    print(f'EXTRA_IN_PLUGIN:{",".join(sorted(pset-iset))}')
    errors += 1

# 2. path match
idx_paths = {s['name']: s['path'] for s in idx.get('skills',[])}
for ps in pj.get('skills',[]):
    name = ps.replace('/SKILL.md','')
    expected = idx_paths.get(name, '')
    if expected and ps != expected:
        print(f'PATH_MISMATCH: plugin.json has {ps}, index has {expected} for {name}')
        errors += 1

# 3. version match
if pj.get('version') != idx.get('version'):
    print(f'VERSION_MISMATCH: plugin.json={pj.get("version")} vs index={idx.get("version")}')
    errors += 1

if errors:
    sys.exit(1)
print('MATCH')
```

---

## P1-6：check-cli-commands.py 源文件不存在时静默通过

### 确认状态

[check-cli-commands.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-cli-commands.py) 第 34-36 行和第 114-116 行：

```python
if not os.path.exists(specs_path):
    print(f'  Warning: {specs_path} not found')
    sys.exit(0)  # ← 应该 exit 1

if not os.path.exists(ref_path):
    print(f'  Warning: {ref_path} not found')
    sys.exit(0)  # ← 应该 exit 1
```

`validate-skills-strict.sh` 中 Check 12 使用 `|| ERRORS=$((ERRORS + 1))`，依赖脚本 exit 1 才计入错误。Exit 0 → 错误被静默吞掉。

### 修复

**文件**: [check-cli-commands.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-cli-commands.py)

```python
if not os.path.exists(specs_path):
    print(f'ERROR: {specs_path} not found — cannot verify CLI consistency', file=sys.stderr)
    sys.exit(1)

if not os.path.exists(ref_path):
    print(f'ERROR: {ref_path} not found — cannot verify CLI consistency', file=sys.stderr)
    sys.exit(1)
```

---

## P1-7：QUALITY_REPORT.md 多处过时信息

### 确认状态

| 位置 | 当前写 | 实际状态 | 
|------|--------|----------|
| 第 28 行 | `10 strict validation checks` | 现为 17 checks（16 + 16b） |
| 第 67 行 | `15 checks` | 同上，应改为 17 |
| 第 75 行 | `15/15 checks passed` | 同上 |
| 第 96 行 | `skills-index.yaml duplicate source_anchors \| Remaining` | 已修复（第二轮清理了重复） |
| 第 97 行 | `skills-index.yaml generated date stale \| Remaining` | 已修复（`generated` → `updated: 2026-05-31`） |
| 第 89 行 | P0-1 seo 误报 | 缺失——未记录新增的 CLI 参数级校验 gap |
| 第 98 行 | Recommended Next Steps 只有 2 项 | 应新增本轮后续建议 |

### 修复

**文件**: [QUALITY_REPORT.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/QUALITY_REPORT.md)

1. 第 28 行：`10` → `17`（16 checks + 16b sub-check）
2. 第 67 行：`15 checks` → `17 checks（16 + 16b）`
3. 第 75 行：`15/15` → `17/17`
4. 第 96 行：`duplicate source_anchors \| Remaining — needs deduplication` → `Fixed — duplicates cleaned (2026-05-31)`
5. 第 97 行：`generated date stale \| Remaining — needs update` → `Fixed — generated → updated: 2026-05-31`
6. 新增 Remaining Risk 条目：
   ```
   | CLI parameter-level validation | Remaining — check-cli-commands.py only checks command names, not options |
   | theme pack --output missing from CLI reference | Fixed (2026-05-31) |
   | theme install --registry-url missing from CLI reference | Fixed (2026-05-31) |
   ```
7. Recommended Next Steps 新增：
   ```
   3. **Implement CLI parameter-level validation**: Extend check-cli-commands.py to cross-check options/arguments
   4. **Expand Markdown/YAML checkers to auxiliary docs**: README.md, QUALITY_REPORT.md, MAINTENANCE.md, platform entry files
   5. **Generate CLI metadata from CLI itself**: Replace regex-based C# parsing with `bukit --metadata` or similar machine-readable output
   ```

---

## P2-8：Markdown / YAML 校验只覆盖 `*/SKILL.md`

### 确认状态

[check-markdown-tables.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-markdown-tables.py) 第 8 行和 [check-yaml-examples.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-yaml-examples.py) 第 17 行：

```python
glob.glob(os.path.join(skills_dir, '*/SKILL.md'))
# 不覆盖: README.md, QUALITY_REPORT.md, MAINTENANCE.md, 
#          AGENTS.md, CLAUDE.md, GEMINI.md, copilot-instructions.md
```

### 修复

**文件**: [check-markdown-tables.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-markdown-tables.py) 和 [check-yaml-examples.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-yaml-examples.py)

扩展文件扫描范围：

```python
patterns = [
    os.path.join(skills_dir, '*/SKILL.md'),
    os.path.join(skills_dir, 'README.md'),
    os.path.join(skills_dir, 'QUALITY_REPORT.md'),
    os.path.join(skills_dir, 'MAINTENANCE.md'),
    os.path.join(skills_dir, 'AGENTS.md'),
    os.path.join(skills_dir, 'CLAUDE.md'),
    os.path.join(skills_dir, 'GEMINI.md'),
    os.path.join(skills_dir, 'copilot-instructions.md'),
]

files = []
for pattern in patterns:
    files.extend(glob.glob(pattern))
```

同时需适配 `skill_name` 的获取逻辑（对非 `*/SKILL.md` 路径取文件名）。

---

## P2-9：theme-component-system source anchors 偏窄

### 确认状态

[theme-component-system SKILL.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/theme-component-system/SKILL.md) Front Matter：

```yaml
source_anchors:
  - "src/Bukit.Cli/Commands/ThemeCommand.cs"
verified_by:
  - "src/Bukit.Cli/Commands/ThemeCommand.cs"
```

但该 skill 的描述涵盖：V2 manifest 解析、section/component 渲染、section schema 验证、theme-catalog.json 导出、data binding 自动解析、继承链。

实际核心代码至少还包括：

| 文件 | 职责 |
|------|------|
| [ThemeManifestLoader.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Theme/ThemeManifestLoader.cs) | V2 manifest 解析（ParseManifest、ParseSections、ParseComponents） |
| [ThemeComponentRegistry.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Theme/ThemeComponentRegistry.cs) | Section/component/template/inheritance 注册与解析 |

### 修复

**文件**: [theme-component-system/SKILL.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/theme-component-system/SKILL.md) Front Matter

```yaml
source_anchors:
  - "src/Bukit.Cli/Commands/ThemeCommand.cs"
  - "src/Bukit.Theme/ThemeManifestLoader.cs"
  - "src/Bukit.Theme/ThemeComponentRegistry.cs"
verified_by:
  - "src/Bukit.Cli/Commands/ThemeCommand.cs"
  - "src/Bukit.Theme/ThemeManifestLoader.cs"
  - "src/Bukit.Theme/ThemeComponentRegistry.cs"
```

同时更新 [skills-index.yaml](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/skills-index.yaml) 中 theme-component-system 的对应条目。

---

## P2-10：bukit-content-to-template IContentStage 表述风险

### 确认状态

[bukit-content-to-template SKILL.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-content-to-template/SKILL.md#L189-L190)：

```markdown
Plugin developers can inject custom stages by implementing `IContentStage`:
```

`IContentStage` 接口确实存在，`ContentPipeline` 也确实支持通过构造函数传入 stages（引擎级别注入）。但**没有公开的插件注册机制**让外部插件通过配置文件或插件系统注入自定义 content stage。当前表述容易让 Agent 误以为外部插件可以直接注册 stage。

### 修复

**文件**: [bukit-content-to-template/SKILL.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-content-to-template/SKILL.md) 第 187-225 行

改为更保守的表述：

```markdown
## Content Pipeline Extension (IContentStage)

Bukit internally models content loading as a pipeline of `IContentStage` implementations.
Engine-level contributors can add custom stages in code by implementing `IContentStage`
and registering them via the `ContentPipeline` constructor.

**Important**: There is currently no public configuration mechanism for external plugins
to inject content stages. Stage injection is an engine-internal extension point.
External plugin injection of content stages should only be described if a public
registration mechanism is confirmed to exist.

### Built-in Stages

| Stage | Purpose |
|-------|---------|
| ...
```

同时将管道架构图中的 `(custom stages injectable)` 改为 `(custom stages — engine internal)`。

---

## 实施顺序

| 序号 | Issue | 优先级 | 文件 | 改动难度 |
|------|-------|--------|------|----------|
| 1 | P0-1: theme pack --output | P0 | bukit-cli-reference/SKILL.md | 低 |
| 2 | P0-2: theme install --registry-url | P0 | bukit-cli-reference/SKILL.md | 低 |
| 3 | P1-6: missing source/ref → exit 1 | P1 | check-cli-commands.py | 低 |
| 4 | P1-4: JSON deep sync | P1 | validate-skills-strict.sh Check 8 | 中 |
| 5 | P1-5: plugin.json path/version check | P1 | validate-skills-strict.sh Check 2 | 中 |
| 6 | P1-7: QUALITY_REPORT.md update | P1 | QUALITY_REPORT.md | 低 |
| 7 | P2-9: theme-component-system anchors | P2 | theme-component-system/SKILL.md + skills-index.yaml | 低 |
| 8 | P2-10: IContentStage wording | P2 | bukit-content-to-template/SKILL.md | 低 |
| 9 | P2-8: expand checker scope | P2 | check-markdown-tables.py + check-yaml-examples.py | 中 |
| 10 | 最终验证 | — | generate-index-json.sh + validate-skills-strict.sh | — |

---

## 暂缓 / 后续 PR

| Issue | 原因 |
|-------|------|
| CLI 参数级校验（check-cli-commands.py 阶段 B） | 实现复杂，需要独立评估方案。建议先在 Remaining Risks 记录 gap。 |
| generate-platform-entries.sh | 低优先级，平台入口文件仍手工维护。 |
| check-markdown-tables.py 对 `--registry <name>` 中 `<name>` vs option 名的区分 | 需要更复杂的 Markdown inline code 解析。 |
| QUICK_REFERENCE_EXCLUDED_COMMANDS 中 `theme` 等也因 is_command_name 被排除 | 无实际影响（parent command 本身也被 SOURCE_PARENTS 排除）。 |

---

## 最终验证

```bash
# 1. 重新生成 skills-index.json
bash src/skills/scripts/generate-index-json.sh

# 2. 运行 strict validator
bash src/skills/scripts/validate-skills-strict.sh

# 3. 预期输出：All strict validations passed (0 errors, 0 warnings)
```
