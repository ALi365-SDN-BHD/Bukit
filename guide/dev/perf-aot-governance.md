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

## Build Performance

- Incremental builds: skip unchanged pages via hash comparison
- `--metrics <path>`: output structured build timing data
- `--jobs <n>`: control parallel rendering concurrency

## CI Verification Commands

```bash
dotnet publish src/Bukit.Cli -c AOT -r linux-x64 -o out/bukit
./scripts/smoke.ps1
```
