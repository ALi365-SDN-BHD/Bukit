# Bukit Skills 剩余问题修复计划

## 问题确认

| # | 优先级 | 问题                                                    | 确认         | <br />      | <br /> |
| - | --- | ----------------------------------------------------- | ---------- | :---------- | :----- |
| 1 | P1  | QUALITY\_REPORT.md 过时 — 已修复项仍列为 remaining risks       | L87-100 ✅  | <br />      | <br /> |
| 2 | P1  | check-cli-commands.py 不阻断验证 (\`                       | <br />     | true\`)     | L304 ✅ |
| 3 | P1  | check-cli-commands.py 只匹配 Name: 不区分顶层/子命令             | 全文件 ✅      | <br />      | <br /> |
| 4 | P1  | bukit-design-tokens external\_css 仍含 Tailwind CDN URL | L251-252 ✅ | <br />      | <br /> |
| 5 | P2  | Markdown table 检查只检测 \`                               | <br />     | \`，无列数/重复检查 | ✅      |
| 6 | P2  | source\_anchors 有重复路径                                 | ✅          | <br />      | <br /> |
| 7 | P2  | README 未标注 Skills 状态 (Beta)                           | ✅          | <br />      | <br /> |

***

## P1: 预发布前修复

### 1.1 更新 QUALITY\_REPORT.md

**当前问题**：第 87-100 行仍列出已修复项为 remaining risks，第 97-100 行建议的 next steps 已完成。

**修复**：重写 `## Remaining Risks` 和 `## Recommended Next Steps` 两个章节。

新内容：

```markdown
## Remaining Risks

| Risk | Status |
|------|--------|
| CLI quick reference table merge | Fixed (2026-05-31) |
| clone/docs check/route inspect missing from CLI reference | Fixed |
| propertyMap/filterValue/analytics.disableInPreview docs missing | Fixed |
| CLI semantic validation not hard-gating | Remaining — planned |
| theme planned commands (doctor, list-components, export-catalog) | Mitigated (marked as planned) |
| Tailwind CDN in external_css example may confuse Agent | Mitigated |
| check-cli-commands.py does not parse full command paths | Remaining |

## Recommended Next Steps

1. Upgrade check-cli-commands.py to parse parent.child command paths
2. Add Markdown table column-count consistency to validator
3. Run dotnet test to verify no regressions
4. Add validate-skills-strict.sh to CI quality gate
```

### 1.2 让 check-cli-commands.py 硬阻断

**文件**: `src/skills/scripts/validate-skills-strict.sh` L304

修复：`|| true` → `|| ERRORS=$((ERRORS + 1))`

### 1.3 重写 check-cli-commands.py — 完整命令路径解析

**当前问题**：脚本用 `re.search(r'Name:\s*"([^"]+)"', line)` 提取所有命令，包括子命令名。然后与 CLI reference 的 table 第一列比较，产生大量误报。

**重写策略**：

1. 解析 `BukitCliSpecs.cs`，提取完整命令路径：

   * 跟踪当前 `parentName`（遇到 `new CliCommandSpec` 时更新）

   * 顶层命令：`Name: "build"` → `build`

   * 子命令：parent=`theme`, child=`create` → `theme create`

   * 子命令：parent=`seo`, child=`audit` → `seo audit`

2. 解析 CLI reference 的 Quick Reference table：

   * 提取第一列中 `` `command` `` 格式的内容

   * 跳过参数行（如 `--allow-external-plugins`）、错误消息行

3. 对比：

   * Source 有但 CLI reference 无：报告 missing

   * CLI reference 有但 source 无：报告 extra

   * 忽略 planned 命令白名单：`theme doctor`, `theme list-components`, `theme export-catalog`

4. 返回码：

   * 0 = 完全一致

   * 1 = 有差异 → 在 validator 中计入 ERRORS

### 1.4 添加 planned 命令白名单

在 `check-cli-commands.py` 中添加常量：

```python
PLANNED_COMMANDS = {
    'theme doctor',
    'theme list-components', 
    'theme export-catalog',
}
```

这些命令在 CLI reference 中标记为 `(planned)`，source 中没有注册，不应报告为差异。

### 1.5 修复 bukit-design-tokens Tailwind CDN

**文件**: `src/skills/bukit-design-tokens/SKILL.md` L251-252

当前：

```yaml
    external_css:
      - "https://cdn.tailwindcss.com"  # Not recommended
```

改为：

```yaml
    external_css:
      - "https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700"
```

并在正文增加注释：Tailwind 应通过构建工具集成，不通过 CDN 链接。

***

## P2: 正式版前增强

### 2.1 Markdown table 列数一致性检查

在 `check-markdown-tables.py` 中增加：

* 每个表格块中，所有数据行列数等于 header 行列数

* CLI Quick Reference table 固定 3 列

* 检测重复命令名

* 检测空列

### 2.2 source\_anchors 去重

Python 脚本扫描所有 19 个 SKILL.md，对每个 skill：

* 移除 `source_anchors` 和 `verified_by` 中的重复路径

* 将 using-bukit 的 `source_anchors` 从自引用改为指向实际入口

### 2.3 在 README 标注 Skills 为 Beta

在 `src/skills/README.md` 开头添加状态说明：

```markdown
> **Status: Beta** — These skills are actively maintained and verified against source code,
> but the knowledge base structure and validation tooling may evolve. See [QUALITY_REPORT.md](QUALITY_REPORT.md)
> for known issues.
```

### 2.4 YAML 示例解析验证

新增 `check-yaml-examples.py`：

* 扫描所有 SKILL.md 中的 ` ```yaml ` 代码块

* 对每个块尝试 `yaml.safe_load()`

* 报告解析失败的块

### 2.5 Status 与 planned/future 关键词一致性

新增 `check-status-keywords.py`：

* 扫描 SKILL.md 正文中的关键词：`planned`, `future`, `not yet implemented`, `experimental`

* 如果发现这些词且 Front Matter status 为 `stable`，报告警告

### 2.6 将 validate-skills-strict.sh 加入 CI

在 `scripts/quality-gate.sh` 中添加（已有）：

```bash
# --- Skills strict validation ---
bash src/skills/scripts/validate-skills-strict.sh || { echo "ERROR: Skills strict validation failed"; exit 1; }
```

确认该步骤存在且正常工作。

***

## 执行顺序

```
P1 修复（阻塞预发布）
├── 1.1 更新 QUALITY_REPORT.md
├── 1.2 修复 check-cli-commands.py || true
├── 1.3 重写 check-cli-commands.py（完整命令路径）
├── 1.4 添加 planned 命令白名单
└── 1.5 修复 Tailwind CDN external_css

P2 增强（可并行）
├── 2.1 Markdown table 列数检查
├── 2.2 source_anchors 去重
├── 2.3 README 标注 Beta
├── 2.4 YAML 解析验证（新建脚本）
├── 2.5 Status 关键词一致性（新建脚本）
└── 2.6 CI 集成验证

最终验证
└── validate-skills-strict.sh + dotnet test
```

***

## 文件变更汇总

| 文件                             | 变更                                          |
| ------------------------------ | ------------------------------------------- |
| `QUALITY_REPORT.md`            | 重写 Remaining Risks + Next Steps             |
| `validate-skills-strict.sh`    | `\|\| true` → `\|\| ERRORS=$((ERRORS + 1))` |
| `check-cli-commands.py`        | 完整重写：parent.child 解析 + 白名单                  |
| `bukit-design-tokens/SKILL.md` | 修复 Tailwind CDN 示例                          |
| `check-markdown-tables.py`     | 增强：列数一致性                                    |
| `README.md`                    | 添加 Beta 状态说明                                |
| 19 个 SKILL.md                  | source\_anchors 去重                          |
| `check-yaml-examples.py`       | **新建** — YAML 解析验证                          |
| `check-status-keywords.py`     | **新建** — 关键词一致性                             |

