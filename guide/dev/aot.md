# Native AOT

Bukit Core is designed to ship as a CLI binary. Native AOT work must verify:

- No reflection-only command registration assumptions.
- JSON source generation contexts cover plugin protocol DTOs.
- CLI help and errors work without JIT-only behavior.
- File and path operations are cross-platform.
- Release artifacts include the right RID, version, and smoke evidence.

The docs rebuild does not run AOT publishing by default. Native AOT validation
belongs to release tasks.

## YAML Static Context

`Bukit.Theme` compiles the checked-in deterministic
`ThemeManifestYamlStaticContext.Generated.cs` during normal builds. The
upstream Vecc YamlDotNet static generator package runs only in the explicit
maintenance flow because its generated accessor suffixes are nondeterministic.

After changing the registered theme manifest model or the pinned generator
version, regenerate and review the complete generated diff:

```bash
bash scripts/build/yaml-static-context.sh update
bash scripts/build/yaml-static-context.sh check
```

The `check` command independently regenerates and normalizes the source,
compares it byte-for-byte with the checked-in file, and verifies that a normal
build does not run the generator. `ci-fast` runs this check, so both CI and the
release gate reject stale static registration. Do not edit the generated file
manually.
