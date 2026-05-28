# Data 模块调试能力 Spec

> 来源：`.trae/documents/bukit-audit-report-202605-28.md` P2-4

## Why

`mode=data` 将内容注入 `site.modules`（hero/features/services 等企业模块），但当前用户**必须完整构建**才能验证模块是否正确加载。模板报 `undefined site.modules.hero` 时无法快速定位是数据缺失还是模板写错。Doctor 命令不检查数据模块。

## What Changes

- `bukit doctor` 新增数据模块检查段：列出每个模块类型的计数、来源、字段列表
- `bukit data inspect` 命令：打印模块概览（`--module <name>` 显示详情）
- `bukit data dump --format json` 命令：完整模块数据 JSON 输出
- 两者共享内容加载逻辑（复用 DoctorCommand 的 content pipeline + MetaHelpers.IsDataItem）

## Impact

- Affected specs: 无
- Affected code:
  - `src/Bukit.Cli/Commands/DataCommand.cs` — **新建**
  - `src/Bukit.Cli/Commands/DoctorCommand.cs` — 新增 `CheckDataModules` 调用
  - `src/Bukit.Cli/Cli/BukitCliSpecs.cs` — 注册 `data` 命令

## ADDED Requirements

### Requirement: `bukit data inspect` 命令

`bukit data inspect` SHALL 加载站点内容，过滤 `mode=data` 条目，按 `type` 分组输出概览。

输出格式：
```
Data modules:
  hero          ×1  source=modules    fields=[title, subtitle, image, cta_text]
  features      ×3  source=notion     fields=[title, icon, description]
  services      ×0  source=modules    ⚠ (no items)
```

`--module hero` SHALL 显示该模块的所有条目详情（id、title、slug、fields 列表）。

#### Scenario: 有数据模块的站点

- **GIVEN** site.yaml 有 `mode=data` 源且包含 2 个 `type: hero` 条目
- **WHEN** `bukit data inspect`
- **THEN** 输出 `hero ×2  source=...`

### Requirement: `bukit data dump` 命令

`bukit data dump --format json` SHALL 输出所有模块的完整 JSON：

```json
{
  "modules": {
    "hero": [{ "id": "...", "title": "...", "fields": {...} }]
  }
}
```

#### Scenario: JSON 输出

- **WHEN** `bukit data dump --format json`
- **THEN** stdout 输出合法 JSON

### Requirement: Doctor 数据模块检查

`bukit doctor` SHALL 在内容加载后输出数据模块检查段。

- 格式与 `data inspect` 一致
- 0 模块时输出 `(none)`

#### Scenario: Doctor 空模块站点

- **GIVEN** 站点无 `mode=data` 源
- **WHEN** `bukit doctor`
- **THEN** 输出 `Data modules: (none)`
