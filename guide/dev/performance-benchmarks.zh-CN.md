# 性能基准测试

> **语言说明**：本页目前仅有中文版本。English version pending. Versi Bahasa Melayu belum tersedia.

Bukit.Theme 提供了 BenchmarkDotNet 基准测试套件，用于验证 `SectionDataResolver` 和 `PageComposer` 在大规模数据下的性能表现。

实现参考：
- `tests/Bukit.Theme.Benchmarks/SectionDataResolverBenchmarks.cs`
- `tests/Bukit.Theme.Benchmarks/PageComposerBenchmarks.cs`
- `tests/Bukit.Theme.Benchmarks/Program.cs`

## 运行基准测试

```bash
cd tests/Bukit.Theme.Benchmarks
dotnet run -c Release -f net10.0
```

按名称过滤：

```bash
dotnet run -c Release -f net10.0 -- --filter "*Resolve*"
```

## 测试场景

### SectionDataResolver

| 场景 | 说明 |
|------|------|
| `Resolve_WithSourceOnly` | 按 `source` 匹配并限制 `limit` 条 |
| `Resolve_WithSourceAndFilter` | 按 `source` + `filter` 条件过滤 |
| `Resolve_WithSourceAndSort` | 按 `source` + `sort` 排序 |
| `Resolve_AllPages` | 通配符 `*` — 匹配全部页面 |

每个场景分别测试 100、1,000、5,000 条数据规模。

### PageComposer

| 场景 | 说明 |
|------|------|
| `ParseAndCompose` | JSON 解析 → Compose 合并主题默认值 |

测试 1、5、10 个 section 的解析和合并性能。

## 典型结果

基于 Apple Silicon M 系列芯片，net10.0 Release 构建：

| 场景 | 100 条 | 1,000 条 | 5,000 条 |
|------|--------|----------|----------|
| Resolve_WithSourceOnly | 15.9 μs | 86.2 μs | 643.6 μs |
| Resolve_WithSourceAndFilter | 18.0 μs | 105.5 μs | 517.4 μs |
| Resolve_WithSourceAndSort | 19.0 μs | 211.4 μs | 1,407 μs |
| Resolve_AllPages | 1.3 μs | 10.1 μs | 110.8 μs |

5,000 条数据最坏场景仅 1.4ms，远低于静态站点生成瓶颈（模板渲染通常占 >95% 构建时间）。

## 内存

| 场景 | 5,000 条 |
|------|----------|
| Resolve_WithSourceOnly | ~1.2 MB |
| Resolve_AllPages | ~0.25 MB |

内存分配主要为结果列表和中间集合拷贝。
