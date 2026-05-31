# Bukit Skills 剩余问题修复计划（第三轮）

## 前两轮已修复的问题（无需再次处理）

| 问题 | 状态 |
|------|------|
| 1.1 theme preview 缺失 | ✅ 已添加到 Quick Reference |
| 1.2 seo audit/diff 拆分 | ✅ 已拆分两行 + 更新 SOURCE_PARENTS |
| 4 QUALITY_REPORT.md (10→15, hard-gating, full paths) | ✅ 已同步 |
| 5 bukit-design-tokens theme.params | ✅ 已添加 `params:` 嵌套 |
| 7 check-markdown-tables.py 列数检查 | ✅ 已增强（|| + 列数 + 重复命令） |
| 8 YAML/keyword hard failure | ✅ YAML→ERRORS, keyword→WARNINGS |

---

## 本轮真正待修复的问题

### P0: CLI 解析 Bug

#### 1.3 check-cli-commands.py 漏掉 inline 子命令

**确认**: 源码 L68 和 L276 有两处 inline 写法：
```csharp
new CliCommandSpec(Name: "inspect", ...)  // data inspect
new CliCommandSpec(Name: "dump", ...)      // data dump
```

**当前脚本行为**: 遇到 `new CliCommandSpec(` 直接 `continue`，错过 inline Name。

**修复**: 在 `continue` 前先检查同行是否有 `Name:`：
```python
if 'new CliCommandSpec(' in stripped:
    # Check inline Name before continuing
    m = re.search(r'Name:\s*"([^"]+)"', stripped)
    if m and is_command_name(m.group(1)):
        if in_subcommands and parent_name:
            source_commands.add(f'{parent_name} {m.group(1)}')
        elif not in_subcommands:
            source_commands.add(m.group(1))
    # Then handle var declaration
    m = re.match(r'var\s+(\w+)\s*=', stripped)
    if m:
        parent_name = m.group(1)
        in_subcommands = 0
    continue
```

### P1: 基础设施

#### 2. export REPO_ROOT / SKILLS_DIR

**确认**: 第 4-5 行计算了变量，但从未 export。

**修复**: 在第 4-5 行后添加：
```bash
export SKILLS_DIR
export REPO_ROOT
```

#### 3.1 README File Layout 不准确

**当前**: `├── bukit-*/SKILL.md ← 19 domain skills`

**问题**: using-bukit 和 theme-component-system 不是 `bukit-*`。

**修复**: 改为分三行：
```
├── using-bukit/SKILL.md           ← Gateway skill
├── bukit-*/SKILL.md               ← Bukit domain skills
├── theme-component-system/SKILL.md ← V2 componentized theme skill
```

#### 3.2 README CI Verification 过时

**当前**只列了 `validate-skills.sh` 和 `generate-index-json.sh`，检查项列表是旧版。

**修复**: 添加 `validate-skills-strict.sh`，列出 15 checks。

#### 3.3 README GEO 诊断码 7→10

**确认**: 仍写 "GEO Score (7 diagnostic codes)"。

**修复**: 改为 "10 diagnostic codes"。

#### 4. QUALITY_REPORT.md 部分滞后

当前 Remaining Risks 表已正确（hard-gating=Fixed, full paths=Fixed）。
但需要新增一个 entry：`check-cli-commands.py inline subcommand parsing` = `Remaining`。

#### 6. generate-index-json.sh 静默跳过

**当前**: PyYAML 缺失时 `exit(0)`。

**修复**: 
```bash
if [ "${CI:-}" = "true" ]; then
  echo "ERROR: yq and PyYAML not available in CI — refusing to skip JSON generation" >&2
  exit 1
fi
```

### P2: 语义改进

#### 7. check-markdown-tables.py docstring

**当前**: docstring 仍写 "merged rows and column count mismatches"但实际上已有列数检查。

**修复**: 只需更新 docstring（代码逻辑已增强）。

#### 9. using-bukit 语气过强

**当前**: "IF THE USER MENTIONS BUKIT, YOU HAVE NO CHOICE. ... This is not negotiable."

**修复**: 添加例外说明，允许对比/迁移/架构分析场景使用其他 SSG 知识。

#### 10. theme-component-system "replaces" 语言

**当前**: "replaces the flat theme.yaml V1 format"

**修复**: 改为 "extends and coexists with the flat V1 theme.yaml format"

#### 11. Webhook IP allowlist 建议

**修复**: 改为不假设外部平台公布稳定 IP 段的保守写法。

#### 12. Webhook [start] 参数

这是源码问题而非 skill 问题。在 CLI reference 中标注 webhook 命令不带 `start` 参数即可。

---

## 执行顺序

```
P0 (阻断性)
└── 1.3 修复 inline 子命令解析

P1 (基础设施 + 同步)
├── 2. export REPO_ROOT / SKILLS_DIR
├── 3.1 README File Layout
├── 3.2 README CI Verification
├── 3.3 README GEO 7→10
├── 4. QUALITY_REPORT inline subcommand entry
└── 6. generate-index-json.sh CI mode

P2 (语义改进)
├── 7. check-markdown-tables.py docstring
├── 9. using-bukit 语气
├── 10. theme-component-system replaces
├── 11. Webhook IP allowlist
└── 12. Webhook start 参数标注

验证
└── validate-skills-strict.sh
```

## 文件变更

| 文件 | 变更 |
|------|------|
| `check-cli-commands.py` | 修复 inline Name 解析 |
| `validate-skills-strict.sh` | export REPO_ROOT + SKILLS_DIR |
| `README.md` | Layout、CI、GEO count |
| `QUALITY_REPORT.md` | 新增 inline subcommand entry |
| `generate-index-json.sh` | CI mode 不允许静默跳过 |
| `check-markdown-tables.py` | docstring 更新 |
| `using-bukit/SKILL.md` | 语气调整 |
| `theme-component-system/SKILL.md` | replaces→extends 措辞 |
| `bukit-webhook/SKILL.md` | IP allowlist 保守化 |
| `bukit-cli-reference/SKILL.md` | webhook start 参数标注 |
