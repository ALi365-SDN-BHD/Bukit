# Performance / AOT / Governance Supplementary Notes

## Goals and Boundaries

This governance addresses:
- AOT sustainability: NativeAOT publishing is controllable and explainable
- Performance measurability: Build metrics and statistics

## AOT Governance

AOT-compatible plugin loading:
- `built-in` + `generated` plugins: always available
- `external-protocol`: AOT-friendly (process/wasm)
- `external` (`plugins/*.dll`): NOT available under AOT

Scriban vendored source (`tools/scriban/`) is fully AOT-patched (zero warnings).

### Source-Gen JSON Rule

All `JsonSerializer.Serialize` / `Deserialize` calls in the publish closure must use
`JsonSerializerContext` source-gen overloads. Reflection-based `JsonSerializerOptions`
overloads trigger IL2026/IL3050 in NativeAOT and are forbidden.

When a model type contains `IReadOnlyDictionary<string, object>`, the value type inside
the dictionary will be `JsonElement` after source-gen deserialization. Call
`JsonElementMaterializer.Materialize()` at the deserialization boundary to recursively
convert `JsonElement` values into CLR primitives (string/bool/long/double/List/Dictionary).

CI enforcement: `scripts/check-aot-warnings.sh` must produce zero `ILC : warning IL\d{4}` lines.

## Build Performance

- Incremental builds: skip unchanged pages via hash comparison
- `--metrics <path>`: output structured build timing data
- `--jobs <n>`: control parallel rendering concurrency

### Performance Fixes (P0/P1 审计修复)

| Fix | Issue | Description |
|---|---|---|
| **P0-1** | ContentImageRewritePipeline | 12-round regex scan replaced with `HtmlMediaReferenceScanner` single-pass handwritten parser — ~2x per-page CPU reduction |
| **P0-2** | AssetPipeline | Pseudo-async replaced with `Task.WhenAll` true async parallel (4 sub-operations: static/assets/tokens/media). External processes use `await Process.WaitForExitAsync()`. |
| **P1-4** | IncrementalBuildEngine | Removed `GetAwaiter().GetResult()` blocking async call — eliminates potential deadlock |
| **P1-5** | SpecialListRenderer | Nested `Parallel.ForEachAsync` replaced with `Parallel.ForAsync` to prevent thread pool exhaustion; direct `FileWriter.WriteUtf8` writes |
| **P2-4** | PageRenderDispatcher | Removed 5 redundant `lock(stageMetricsLock)` blocks, replaced with `stageMetrics.Merge()` lock-free merge |
| **P3-8** | BodyCacheDecorator | FIFO eviction replaced with real LRU (`LinkedList` + `lock`), with `_inlineBypasses` independent counter |
| **P3-9** | DirectoryHashCache | Added `maxFiles=10000` and `maxTotalSize=100MB` limits to prevent OOM/IO storms on large directories |

## CI Verification Commands

```bash
dotnet publish src/Bukit.Cli -c AOT -r linux-x64 -o out/bukit
./scripts/smoke.ps1
```
