# Bukit Skills 最新修复计划

## 问题确认

| #    | 严重度 | 问题                                                                        | 确认                                                       |
| ---- | --- | ------------------------------------------------------------------------- | -------------------------------------------------------- |
| P0-1 | P0  | `theme preview` 在源码存在但 quick reference 缺失                                 | ✅ grep 确认无此行                                             |
| P0-2 | P0  | `seo audit`/`seo diff` 源码是子命令但 reference 只写 `seo`                         | ✅ 只有 `\| \`seo\` \|\` 一行                                 |
| P1-3 | P1  | QUALITY\_REPORT.md 滞后 — 仍写 "10 checks"、"not hard-gating"、"does not parse" | ✅ 3 处确认                                                  |
| P1-4 | P1  | bukit-design-tokens YAML 示例字段不在 ThemeConfig 中                             | ✅ external\_css/primary\_color/font\_family 直接放在 theme 下 |
| P2-5 | P2  | check-markdown-tables.py 只检查 `\|\|` 不检查列数                                 | ✅ docstring 写了但未实现                                       |
| P2-6 | P2  | YAML/keyword 检查是软检查（`\|\| true`），不阻断 quality gate                         | ✅ 2 处确认                                                  |

***

## P0: 立即修复

### P0-1: 添加 theme preview 到 Quick Reference

**文件**: `src/skills/bukit-cli-reference/SKILL.md`

在 `theme search` 行后添加：

```
| `theme preview` | Display detailed theme anatomy | `[name]` `--config` `--site` |
```

### P0-2: 拆分 seo 为 seo audit / seo diff

**文件**: `src/skills/bukit-cli-reference/SKILL.md`

当前（单行）：

```
| `seo` | SEO audit and regression detection | `audit` `--dir` `--strict` `--external`; `diff` `--baseline` `--current` `--max-new-*` `--fail-on-*` |
```

改为两行：

```
| `seo audit` | Audit SEO health from build report | `--dir` `--report` `--strict` `--external` |
| `seo diff` | Compare SEO reports for regression budgets | `--baseline` `--current` `--max-new-errors N` `--max-new-warnings N` `--max-new-issues N` `--fail-on-new-code c1,c2` `--fail-on-route-removed` `--fail-on-indexable-drop` |
```

同时更新 `SOURCE_PARENTS_WITH_SUBCOMMANDS` 在 `check-cli-commands.py` 中：从集合中移除 `seo`（因为 reference 现在用子命令名而非父命令名）。

***

## P1: 同步修复

### P1-3: QUALITY\_REPORT.md 再次同步

更新 3 处滞后内容：

1. `validate-skills-strict.sh: 10 checks` → `15 checks`
2. `CLI semantic validation not hard-gating | Remaining` → `Fixed`
3. `check-cli-commands.py does not parse full command paths | Remaining` → `Fixed`
4. Next Steps 中删除 "Upgrade check-cli-commands.py to parse parent.child..."（已完成）

### P1-4: 修复 bukit-design-tokens YAML 示例

**文件**: `src/skills/bukit-design-tokens/SKILL.md` L248-255

当前（错误 — 字段不在 ThemeConfig 中）：

```yaml
theme:
    external_css:
      - "..."
    primary_color: "#7c3aed"
    font_family: "Inter, system-ui, sans-serif"
```

改为（正确 — 通过 theme.params）：

```yaml
theme:
  params:
    external_css:
      - "https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700"
      - "https://cdn.jsdelivr.net/npm/modern-normalize/modern-normalize.min.css"
    primary_color: "#7c3aed"
    font_family: "Inter, system-ui, sans-serif"
```

***

## P2: 增强

### P2-5: check-markdown-tables.py 增加列数检查

增强 `src/skills/scripts/check-markdown-tables.py`：

* 每个表格块中，所有数据行 `|` 数量等于 header 行 `|` 数量

* CLI Quick Reference 固定 3 列

* 检测重复命令名（Quick Reference 第一列）

* 检测空列（`| |`）

### P2-6: YAML/keyword 检查改为 hard failure

**文件**: `src/skills/scripts/validate-skills-strict.sh`

两处修改：

* `check-yaml-examples.py || true` → `check-yaml-examples.py || ERRORS=$((ERRORS + 1))`

* `check-status-keywords.py || true` → `check-status-keywords.py || WARNINGS=$((WARNINGS + 1))`

（YAML 错误为 ERROR — 阻止构建；关键词不匹配为 WARNING — 不阻止构建但需关注）

***

## 执行顺序

```
P0 (立即修复)
├── P0-1: 添加 theme preview 到 CLI quick reference
├── P0-2: 拆分 seo → seo audit / seo diff
└── 更新 SOURCE_PARENTS_WITH_SUBCOMMANDS

P1 (同步修复)
├── P1-3: QUALITY_REPORT.md 同步（10→15 checks, hard-gating, full paths）
└── P1-4: bukit-design-tokens theme.params 修复

P2 (增强)
├── P2-5: check-markdown-tables.py 列数检查
└── P2-6: YAML/keyword hard failure

验证
└── validate-skills-strict.sh + dotnet test
```

## 文件变更汇总

| 文件                             | 变更                                      |
| ------------------------------ | --------------------------------------- |
| `bukit-cli-reference/SKILL.md` | 添加 theme preview + 拆分 seo 为 audit/diff  |
| `check-cli-commands.py`        | 从 SOURCE\_PARENTS 移除 seo                |
| `QUALITY_REPORT.md`            | 同步 3 处滞后：10→15, hard-gating, full paths |
| `bukit-design-tokens/SKILL.md` | 修复 YAML 示例为 theme.params 嵌套             |
| `check-markdown-tables.py`     | 增加列数一致性检查                               |
| `validate-skills-strict.sh`    | YAML 检查改为 ERROR，keyword 检查改为 WARNING    |

