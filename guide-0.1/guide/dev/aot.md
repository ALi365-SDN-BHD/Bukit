# AOT and Non-AOT Build Modes

This project supports both Non-AOT (JIT) and NativeAOT modes with clear differences in plugin capabilities, publish paths, and runtime characteristics.

## Mode Selection
- Non-AOT (JIT): Development, fast iteration, external DLL plugin loading.
- AOT: Production deployment, cold-start/memory optimization, portable single-file artifacts.

Switch via `src/Bukit.Cli/Bukit.Cli.csproj`: `Configuration=AOT` enables `PublishAot=true`.

## Plugin Behavioral Differences
`src/Bukit.Engine/Plugins/PluginRegistry.cs`:
- AOT: `built-in` + `generated` + `external-protocol`.
- Non-AOT: `built-in` + `generated` + `external` (scans `<rootDir>/plugins/*.dll`) + `external-protocol`.

Conclusion: external DLL plugins require Non-AOT. Under AOT, use `external-protocol` for dynamic extensions.

## AOT external-protocol
`external-protocol` is the AOT-friendly dynamic extension: main program loads no external DLLs, uses `stdin/stdout + JSON`. Currently supports `runtime: process|wasm` and `after-build|derive-pages`.

See: [external-plugin-protocol.md](./external-plugin-protocol.md)

## Custom Plugins Under AOT
Plugins implementing `IBukitPlugin`, namespace prefix `Bukit.Plugins.`, with `[BukitPlugin]` attribute are source-generated into the `generated` plugin source.

## Scriban AOT Compatibility
Scriban vendored source (`tools/scriban/`) has been fully AOT-patched: 101 `Type.GetMethod` reflection calls removed, `dynamic` dispatch eliminated, `IsAotCompatible=true`. Zero AOT warnings remain.

## Publish Dependencies
Linux targets with NativeAOT symbol stripping require `llvm-objcopy` or `objcopy`. Default: `BukitStripSymbols=false`. Override: `-p:BukitStripSymbols=true`.

## Verification
```bash
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c AOT -r linux-x64 -o out/bukit-linux
```

