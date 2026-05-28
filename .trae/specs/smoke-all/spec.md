# Smoke Gold Checks Spec

> 来源：`.trae/documents/bukit-audit-report-202605-28.md` P3-1

## Why

CI 已对 7 个示例站点执行 `dotnet run -- build`，但仅验证构建成功，不验证输出完整性（sitemap、RSS、search.json、hreflang、assets）。`smoke.sh` 覆盖 starter 站点但仅限于文件存在性。需要对所有示例站点执行结构化的输出验证，作为低维护成本的 golden check。

## What Changes

- `scripts/smoke-all.sh` — 遍历所有 `examples/*/site.yaml`，执行 build + 输出结构检查
- 集成到 `quality-gate.sh`（现有的 `smoke.sh` 保留兼容）

## Impact

- Affected specs: 无
- Affected code:
  - `scripts/smoke-all.sh` — **新建**
  - `scripts/quality-gate.sh` — 追加 `smoke-all.sh` 调用

## ADDED Requirements

### Requirement: Smoke-all 对所有示例站点执行输出结构验证

`scripts/smoke-all.sh` SHALL 对每个 `examples/*/site.yaml` 执行：

| 检查 | 方法 |
|------|------|
| `bukit build --clean` 退出码 0 | 标准 |
| `dist/index.html` 存在 | `test -f` |
| `dist/sitemap.xml` 存在且含 `<url>` | `grep` |
| `dist/rss.xml` 存在且含 `<channel>` | `grep` |
| `dist/search.json` 合法 JSON | `python3 -m json.tool` |
| `dist/assets/` 非空（如有） | `test -d && ls` |

站点特有检查：
- i18n 站点：验证 `dist/en/` 和 `dist/zh/` 各含 `sitemap.xml`
- taxonomy 站点：验证 taxonomy 页面生成
- modules 站点：验证 `dist/` 含模块数据文件
- plugin 站点：使用 `site.external-plugin.yaml` 配置

#### Scenario: 所有站点通过

- **WHEN** `bash scripts/smoke-all.sh` 在 CI 中执行
- **THEN** 全部示例站点 build 成功 + 输出检查通过，退出码 0

#### Scenario: 单个站点失败不中断

- **WHEN** 某个站点构建失败
- **THEN** 输出 `✖` 并记录失败，继续检查后续站点，最终退出码 1
