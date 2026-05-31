# Bukit Skills 验证体系 9 项修复实施计划

**日期**: 2026-05-31
**类型**: Bug Fix + 工程完善
**影响范围**: `src/skills/scripts/`、`src/skills/skills-index.yaml`、`src/skills/QUALITY_REPORT.md`、`src/Bukit.Cli/Commands/ThemeCommand.cs`

---

## 概述

修复 skills 验证流水线中 9 个问题（1 个 P0、5 个 P1、2 个 P2），涵盖 CLI 命令误报、脚本退出码缺失、文档滞后、重复数据清洗、索引元数据过期等。

---

## P0：Issue 1 — check-cli-commands.py 误报 seo 为未注册命令

### 确认状态
已读取 [check-cli-commands.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-cli-commands.py) 第 20-23 行：

```python
SOURCE_PARENTS_WITH_SUBCOMMANDS = {
    'theme', 'template', 'config', 'data', 'route', 'docs',
    'intent', 'visual', 'geo', 'plugin',
}
```

**`seo` 不在其中。**

### 根因
`seo` 在 [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs) 中是 parent command（配置了 Options + Subcommands: `audit` 和 `diff`），但 CLI Quick Reference 只写了 `seo audit` 和 `seo diff`，没有单独的 `seo`。

脚本解析 BukitCliSpecs.cs 时会把 `seo` 作为顶级命令加入 `source_commands`，而 CLI reference 中只有 `seo audit` 和 `seo diff`，导致 `seo` 被误报为 "in source but not in reference"。

### 修复

**文件**: [check-cli-commands.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-cli-commands.py) 第 20-23 行

```python
SOURCE_PARENTS_WITH_SUBCOMMANDS = {
    'theme', 'template', 'seo', 'config', 'data', 'route', 'docs',
    'intent', 'visual', 'geo', 'plugin',
}
```

改动：在第 21 行的 `'template',` 之后插入 `'seo',`。

### 验证
```bash
python3 src/skills/scripts/check-cli-commands.py
# 预期 exit 0，无 "Commands in source but NOT in CLI reference" 出现 seo
```

---

## P1：Issue 2 — check-status-keywords.py 不返回非零退出码

### 确认状态
已读取 [check-status-keywords.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-status-keywords.py) 第 40-43 行：

```python
if warnings:
    print(f'  {warnings} keyword/status mismatch(es) found')
else:
    print('  All keyword/status combinations consistent')
```

**没有 `sys.exit(1)`。** 脚本发现 warning 打印后 exit code 仍为 0，`validate-skills-strict.sh` 第 322 行的 `WARNINGS=$((WARNINGS + 1))` 永远不会执行。

### 根因
`||` 操作符只在左边命令 exit code 非零时执行右边命令。脚本 exit 0 → `WARNINGS` 不递增 → strict validator 显示 0 warnings。

### 修复

**文件**: [check-status-keywords.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-status-keywords.py) 第 40-43 行

改为：
```python
if warnings:
    print(f'  {warnings} keyword/status mismatch(es) found')
    sys.exit(1)
else:
    print('  All keyword/status combinations consistent')
    sys.exit(0)
```

### 验证
```bash
python3 src/skills/scripts/check-status-keywords.py; echo "Exit: $?"
# 正常情况下 exit 0，有 mismatch 时 exit 1
```

---

## P1：Issue 3 — check-yaml-examples.py PyYAML 缺失处理

### 确认状态
已读取 [check-yaml-examples.py](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/check-yaml-examples.py) 第 7-14 行。**该问题已基本修复**：

```python
except ImportError:
    if os.environ.get('ALLOW_SKIP_YAML', '') == '1':
        print('  Warning: PyYAML not installed, skipping YAML validation')
        sys.exit(0)
    print('  ERROR: PyYAML not installed — YAML validation cannot run. Install: pip3 install pyyaml', file=sys.stderr)
    sys.exit(1)
```

当前代码在 PyYAML 缺失时**已经 exit 1**（除非显式设置 `ALLOW_SKIP_YAML=1`）。

### 修复（可选微调）
将环境变量名从 `ALLOW_SKIP_YAML` 改为更明确的 `ALLOW_SKIP_YAML_VALIDATION`，使命名与其他脚本一致：

```python
if os.environ.get('ALLOW_SKIP_YAML_VALIDATION', '') == '1':
```

> **注**：这是 cosmetic 改进。当前行为已经正确（CI 中失败，本地可通过环境变量跳过）。如果时间紧张可跳过此改动。

### 验证
```bash
# 模拟 CI（无 ALLOW_SKIP_YAML_VALIDATION 环境变量）
python3 src/skills/scripts/check-yaml-examples.py; echo "Exit: $?"
# 预期 exit 1
```

---

## P1：Issue 4 — generate-index-json.sh 无 python3 时的静默跳过

### 确认状态
已读取 [generate-index-json.sh](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/generate-index-json.sh) 第 39-42 行。**该问题已基本修复**：

```bash
else
  echo "ERROR: Neither yq nor python3 is available — refusing to skip JSON generation" >&2
  exit 1
fi
```

当前代码在既无 yq 也无 python3 时**已经 exit 1**。

### 修复（可选加固）
如果希望 CI 环境下有更严格的区分，可添加显式 CI 检测（但当前行为已经足够严格 - 无论本地还是 CI 都失败）：

> **结论**：当前代码已符合要求。可跳过此改动，或仅做代码审查确认。

### 验证
```bash
# 在没有 python3/yq 的环境中
bash src/skills/scripts/generate-index-json.sh; echo "Exit: $?"
# 预期 exit 1
```

---

## P1：Issue 5 — QUALITY_REPORT.md 滞后

### 确认状态
已读取 [QUALITY_REPORT.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/QUALITY_REPORT.md)。

**Remaining Risks 表（第 82-94 行）**已更新了部分条目（`check-cli-commands.py inline subcommand parsing` 已标为 Fixed），但仍有以下问题：

1. 缺少以下新识别的风险项：
   - `check-cli-commands.py regex-based C# parsing`（仅依赖正则解析 C# 源码）
   - `PyYAML missing causes YAML check skip`（Issue 3 已修复但需标注）
   - `status keyword warnings not propagated`（Issue 2 已修复但需标注）

2. **Recommended Next Steps（第 96-99 行）**已经清除了过时条目，只保留 2 项。此部分已是最新。

3. **第 76 行的 Validation Results** 声称 "0 errors, 0 warnings"，但在 Issue 2/3/4 未修复前，此声明可能不准确。

### 修复

**文件**: [QUALITY_REPORT.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/QUALITY_REPORT.md) 第 82-94 行

将 Remaining Risks 表改为：

```markdown
## Remaining Risks

| Risk | Status |
|------|--------|
| CLI quick reference table merge (clone\|\|geo, docs\|\|version) | Fixed (2026-05-31) |
| clone/docs check/route inspect missing from CLI reference | Fixed |
| propertyMap/filterValue/analytics.disableInPreview docs missing | Fixed |
| CLI semantic validation not hard-gating | Fixed — check-cli-commands.py now hard-gates |
| check-cli-commands.py inline subcommand parsing | Fixed — inline `Name:` detection added |
| check-cli-commands.py regex-based C# parsing | Remaining — replace with generated CLI metadata before stable |
| PyYAML missing causes YAML check skip | Fixed — check-yaml-examples.py exits 1 unless ALLOW_SKIP_YAML_VALIDATION=1 |
| status keyword warnings not propagated to strict validator | Fixed — check-status-keywords.py now exits 1 on mismatch |
| theme planned commands (doctor, list-components, export-catalog) | Mitigated — marked as planned in skill; code handlers exist but CLI specs not registered |
| V2 componentized theme stability | Beta — may need reassessment as implementation stabilizes |
| skills-index.yaml duplicate source_anchors | Remaining — needs deduplication |
| skills-index.yaml generated date stale | Remaining — needs update |
```

### 验证
目视检查 QUALITY_REPORT.md 内容准确性。

---

## P1：Issue 6 — theme doctor/list-components/export-catalog 代码可达性问题

### 确认状态

**ThemeCommand.cs** 第 9-33 行已经有 handler：

```csharp
"doctor" => DoctorAsync(command),
"list-components" => ListComponentsAsync(command),
"export-catalog" => ExportCatalogAsync(command),
```

**BukitCliSpecs.cs** 中 theme 的 Subcommands 只注册了：
```
create, list, use, info, params, preview, wizard, pack, install, search
```

**没有** `doctor`、`list-components`、`export-catalog`。

主程序解析 subcommand 时，如果 subcommand 不在 `CliCommandSpec.Subcommands` 里，parser 找不到 subSpec 会退回 parent 解析，导致这三个命令**实际不可达**。

### 修复（方案 B：保持 planned，添加注释）

> 选择方案 B 是因为：1) 这些功能处于 Beta 阶段，尚未稳定；2) `theme-component-system` SKILL.md 中已标记为 planned；3) Stable 前应清理 unreachable code 或正式启用。

**文件**: [ThemeCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ThemeCommand.cs) 第 28-31 行

在 handler 上方添加注释说明当前不可达：

```csharp
// NOTE: The following subcommands are NOT registered in BukitCliSpecs.cs yet.
// They are unreachable via CLI until registered. See theme-component-system SKILL.md
// which marks them as (planned). Enable them in BukitCliSpecs.cs when ready for stable.
"doctor" => DoctorAsync(command),
"list-components" => ListComponentsAsync(command),
"export-catalog" => ExportCatalogAsync(command),
```

### 后续（Stable 前）
在 BukitCliSpecs.cs 中注册这三个 subcommand，移除注释，并将 theme-component-system 中标记从 (planned) 改为 stable。

### 验证
```bash
# 确认这些命令当前不可达（应报错）
dotnet run -- theme doctor 2>&1 | head -5
# 预期：parser 报 unknown subcommand 或类似错误
```

---

## P2：Issue 7 — skills-index.yaml 重复 source_anchors + Check 16 强化

### 确认状态

**skills-index.yaml** 中确认的重复项：

1. [bukit-theme](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/skills-index.yaml#L107-L108) 的 `source_anchors` 和 `verified_by` 各自写了两次 `ThemeCommand.cs`
2. [theme-component-system](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/skills-index.yaml#L539-L540) 同样重复了 `ThemeCommand.cs`

**validate-skills-strict.sh** 第 325-342 行已有 Check 16，但存在缺陷：
- 只检查 SKILL.md Front Matter（`head -30` 提取），不检查 skills-index.yaml
- 使用 `|| true` 永远不会失败
- 不递增 `WARNINGS` 计数器

### 修复

#### 7a. 清理 skills-index.yaml 重复项

**文件**: [skills-index.yaml](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/skills-index.yaml)

**bukit-theme**（第 106-113 行附近），删除重复行：
```yaml
    source_anchors:
      - "src/Bukit.Cli/Commands/ThemeCommand.cs"
    verified_by:
      - "src/Bukit.Cli/Commands/ThemeCommand.cs"
```

**theme-component-system**（第 538-545 行附近），删除重复行：
```yaml
    source_anchors:
      - "src/Bukit.Cli/Commands/ThemeCommand.cs"
    verified_by:
      - "src/Bukit.Cli/Commands/ThemeCommand.cs"
```

#### 7b. 强化 Check 16

**文件**: [validate-skills-strict.sh](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/scripts/validate-skills-strict.sh) 第 325-342 行

改为检查 skills-index.yaml 中的重复项，并正确计入 WARNINGS：

```bash
# --- Check 16: Duplicate entries in skills-index.yaml ---
echo ""
echo "--- Check 16: No duplicate entries in skills-index.yaml ---"
CHECK16_RESULT=$(python3 -c "
import yaml, os
skills_dir = os.environ.get('SKILLS_DIR', 'src/skills')
index_path = os.path.join(skills_dir, 'skills-index.yaml')
with open(index_path) as f:
    data = yaml.safe_load(f)
errors = 0
for s in data.get('skills', []):
    name = s.get('name', '?')
    for field in ('source_anchors', 'verified_by', 'guide_chapters'):
        vals = s.get(field, [])
        seen = set()
        dup = []
        for v in vals:
            if v in seen:
                dup.append(v)
            else:
                seen.add(v)
        if dup:
            print(f'  WARNING: [{name}] {field} has duplicates: {dup}')
            errors += 1
if errors:
    print(f'  {errors} duplicate entry issue(s) found')
    sys.exit(1)
else:
    print('  No duplicate entries')
    sys.exit(0)
" 2>/dev/null)
if [ $? -ne 0 ]; then
  echo -e "  ${YELLOW}⚠️  Duplicate entries found in skills-index.yaml${NC}"
  WARNINGS=$((WARNINGS + 1))
fi
```

同时检查 SKILL.md Front Matter 中的重复项：

```bash
# --- Check 16b: Duplicate entries in SKILL.md Front Matter ---
echo ""
echo "--- Check 16b: No duplicate entries in SKILL.md Front Matter ---"
CHECK16B_RESULT=$(python3 -c "
import os, glob
skills_dir = os.environ.get('SKILLS_DIR', 'src/skills')
errors = 0
for sf in sorted(glob.glob(os.path.join(skills_dir, '*/SKILL.md'))):
    name = os.path.basename(os.path.dirname(sf))
    with open(sf) as f:
        lines = f.readlines()
    seen = {}; dup = []
    for l in lines:
        if l.startswith('  - '):
            if l in seen: dup.append(l.strip())
            seen[l] = 1
    if dup:
        print(f'  WARNING: [{name}] Duplicate entries: {dup}')
        errors += 1
if errors:
    print(f'  {errors} duplicate entry issue(s) found')
    sys.exit(1)
else:
    print('  No duplicate entries in Front Matter')
    sys.exit(0)
" 2>/dev/null)
if [ $? -ne 0 ]; then
  echo -e "  ${YELLOW}⚠️  Duplicate entries found in SKILL.md Front Matter${NC}"
  WARNINGS=$((WARNINGS + 1))
fi
```

### 验证
```bash
bash src/skills/scripts/validate-skills-strict.sh
# 预期 Check 16 通过（0 warnings）
```

---

## P2：Issue 8 — skills-index.yaml 声称驱动平台入口但无生成器

### 确认状态
[skills-index.yaml](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/skills-index.yaml) 第 3-4 行：

```yaml
# Drives platform entry points (CLAUDE.md, AGENTS.md, GEMINI.md, etc.)
# Single source of truth — update here first, then regenerate derived files.
```

但当前：
- 只有 `generate-index-json.sh` 生成 `skills-index.json`
- **没有** 脚本生成 `CLAUDE.md`、`AGENTS.md`、`GEMINI.md`、`copilot-instructions.md`
- [MAINTENANCE.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/MAINTENANCE.md) 第 57-61 行仍要求手工更新平台入口文件

### 修复

**文件**: [skills-index.yaml](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/skills-index.yaml) 第 1-4 行

改为：
```yaml
# Bukit Agent Skills Index
# Machine-readable catalog of all Bukit agent skills.
# Intended source of truth for skill metadata.
# Platform entry files (CLAUDE.md, AGENTS.md, GEMINI.md, copilot-instructions.md)
# are currently maintained manually and validated for consistency.
# skills-index.json is auto-generated from this file via generate-index-json.sh.
```

### 后续（Stable 前）
新增 `src/skills/scripts/generate-platform-entries.sh` 自动生成各平台入口文件的 skill 列表和 Quick Reference 部分。

### 验证
目视检查 skills-index.yaml 头部注释内容。

---

## P2：Issue 9 — skills-index.yaml 日期过期

### 确认状态
[skills-index.yaml](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/skills-index.yaml) 第 7 行：

```yaml
generated: "2026-05-22"
```

但 QUALITY_REPORT.md 日期是 `2026-05-31`，且索引内容已经过多次更新（添加了 Check 16 等）。

### 修复

**文件**: [skills-index.yaml](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/skills-index.yaml) 第 6-7 行

将 `generated` 改为 `updated`（语义更准确），日期更新为当天：

```yaml
version: "3.0.0"
updated: "2026-05-31"
skill_count: 19
```

同时重新生成 `skills-index.json`：
```bash
bash src/skills/scripts/generate-index-json.sh
```

### 验证
```bash
grep "updated:" src/skills/skills-index.yaml
grep "updated:" src/skills/skills-index.json
# 两者一致
```

---

## 实施顺序

| 顺序 | Issue | 优先级 | 文件 | 依赖 |
|------|-------|--------|------|------|
| 1 | P0-1: seo 排除 | P0 | check-cli-commands.py | 无 |
| 2 | P1-2: exit code | P1 | check-status-keywords.py | 无 |
| 3 | P1-3: PyYAML env var | P1 | check-yaml-examples.py | 无（可选） |
| 4 | P1-4: 确认已修复 | P1 | generate-index-json.sh | 无（验证即可） |
| 5 | P1-5: 文档更新 | P1 | QUALITY_REPORT.md | Issue 1-3 |
| 6 | P1-6: theme 注释 | P1 | ThemeCommand.cs | 无 |
| 7 | P2-7a: 清理重复 | P2 | skills-index.yaml | 无 |
| 8 | P2-7b: 强化 Check 16 | P2 | validate-skills-strict.sh | 无 |
| 9 | P2-8: 头部文案 | P2 | skills-index.yaml | 无 |
| 10 | P2-9: 日期更新 | P2 | skills-index.yaml | Issue 7a, 8 |
| 11 | 最终验证 | — | generate-index-json.sh + validate-skills-strict.sh | 全部 |

---

## 最终验证

所有修复完成后运行：

```bash
# 1. 重新生成 skills-index.json
bash src/skills/scripts/generate-index-json.sh

# 2. 运行 strict validator
bash src/skills/scripts/validate-skills-strict.sh

# 3. 预期输出：0 errors, 0 warnings
```
