# Bukit Skills 第四轮修复计划

## 问题确认

| # | 严重度 | 问题 | 确认 |
|---|--------|------|------|
| P0 | P0 | `seo` 不在 SOURCE_PARENTS，导致误报 `Commands in source but NOT in CLI reference: - seo` | ✅ seo 从集合中移除但未加回 |
| P1 | P1 | QUALITY_REPORT.md inline subcommand 仍写 "Remaining" | ✅ 仍显示 Remaining |
| P1 | P1 | QUALITY_REPORT.md Next Steps 建议已实现项 | ✅ "Add Markdown table" 和 "Add YAML" 已完成 |
| P1 | P1 | check-yaml-examples.py PyYAML 缺失时静默跳过 | ✅ `except ImportError: exit(0)` |
| P1 | P1 | generate-index-json.sh 最后 else 无 CI 检查 | ✅ 仍 `exit 0` |
| P1 | P1 | MAINTENANCE.md 要求手工同步平台入口文件 | 需核实 |
| P2 | P2 | skills-index.yaml 重复 source_anchors | 需核实具体重复项 |
| P2 | P2 | theme-component-system 能力粒度不足 | ✅ 描述能力但未分状态 |
| P2 | P2 | webhook start 参数 vs CLI spec 不一致 | ✅ 源码有、spec 无 |
| P2 | P2 | quality-gate.sh strict validation 未验证 | ✅ 需实测 |

---

## 修复计划

### P0: seo 加到 SOURCE_PARENTS

**文件**: `src/skills/scripts/check-cli-commands.py` L20-L23

```python
SOURCE_PARENTS_WITH_SUBCOMMANDS = {
    'theme', 'template', 'seo', 'config', 'data', 'route', 'docs',
    'intent', 'visual', 'geo', 'plugin',
}
```

在 `'template',` 后加回 `'seo',`。

### P1-1: QUALITY_REPORT.md 同步

1. `inline subcommand parsing | Remaining` → `Fixed — inline Name: detection added`
2. Next Steps 删除 "Add Markdown table column-count consistency"（已完成）
3. Next Steps 删除 "Add YAML example parsing validation"（已完成）

### P1-2: check-yaml-examples.py 硬失败

```python
except ImportError:
    if os.environ.get('ALLOW_SKIP_YAML', '') == '1':
        print('  Warning: PyYAML not installed, skipping YAML validation')
        sys.exit(0)
    print('  ERROR: PyYAML not installed — YAML validation cannot run. Install: pip3 install pyyaml', file=sys.stderr)
    sys.exit(1)
```

### P1-3: generate-index-json.sh 最后 else 加 CI 检查

```bash
else
  echo "ERROR: Neither yq nor python3 is available — refusing to skip JSON generation" >&2
  exit 1
fi
```

简化为：无论 CI 与否都 fail，因为 JSON 是核心一致性要求。

### P1-4: MAINTENANCE.md / README 平台入口表述对齐

MAINTENANCE.md 和 README.md 都已有 "平台入口文件需手工更新" 的表述。保持当前 Beta 阶段手工同步策略，在 README 中增加一句说明：

> Platform entry files are currently maintained manually. A future release will generate them from skills-index.yaml via `generate-platform-entries.sh`.

### P2-1: skills-index.yaml 去重增强

在 `check-status-consistency.py` 中增加去重检测（或在 validator 中新增 Check 16）：

```python
# Check for duplicate source_anchors/verified_by per skill
```

### P2-2: theme-component-system 能力分拆

在 SKILL.md 开头新增能力状态表：

```markdown
| Capability | Status |
|---|---|
| theme.yaml V2 parsing | beta |
| section/component rendering | beta |
| section schema validation | beta |
| theme-catalog.json export | planned |
| Page Composer | planned |
| data binding auto-resolve | beta |
| theme inheritance chains | beta |
```

### P2-3: webhook start 标注

在 `bukit-cli-reference/SKILL.md` webhook 命令描述中添加注释：

```
Note: The `start` positional argument shown in webhook help text is not registered in the CLI spec and may not be accessible through normal command parsing.
```

### P2-4: quality-gate.sh 验证

执行 `bash src/skills/scripts/validate-skills-strict.sh` 确认 P0 修复后不再误报 `seo`。

---

## 执行顺序

```
P0 (阻断)
└── seo 加回 SOURCE_PARENTS_WITH_SUBCOMMANDS

P1 (同步)
├── QUALITY_REPORT.md 同步 (inline entry + Next Steps)
├── check-yaml-examples.py 硬失败
├── generate-index-json.sh 最后 else 硬失败
└── README 平台入口说明

P2 (增强)
├── validator 重复检测 (Check 16)
├── theme-component-system 能力分拆
└── webhook start 标注

验证
├── validate-skills-strict.sh (确认 P0 修复)
└── dotnet test
```

## 文件变更

| 文件 | 变更 |
|------|------|
| `check-cli-commands.py` | 加 `'seo'` 到 SOURCE_PARENTS |
| `QUALITY_REPORT.md` | inline entry → Fixed, 删除已完成 Next Steps |
| `check-yaml-examples.py` | PyYAML 缺失 → exit 1 |
| `generate-index-json.sh` | 最后 else → exit 1 |
| `README.md` | 平台入口手工同步说明 |
| `validate-skills-strict.sh` | 新增 Check 16（重复检测）|
| `theme-component-system/SKILL.md` | 新增能力状态表 |
| `bukit-cli-reference/SKILL.md` | webhook start 标注 |
