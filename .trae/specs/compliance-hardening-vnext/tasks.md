# Tasks

## 1. Spec Coverage

- [x] Add `.trae/specs/compliance-hardening-vnext/spec.md`
- [x] Add `.trae/specs/compliance-hardening-vnext/tasks.md`
- [x] Add `.trae/specs/compliance-hardening-vnext/checklist.md`

## 2. InternalsVisibleTo

- [x] Remove production assembly targets from `src/*/InternalsVisibleTo.cs`
- [x] Remove duplicate or obsolete IVT declarations where present
- [x] Tighten architecture test allowlist to test assemblies only
- [x] Run architecture tests

## 3. Empty Catch Blocks

- [x] Replace cleanup `catch {}` blocks in CLI theme commands
- [x] Replace cleanup `catch {}` blocks in build manifest cleanup
- [x] Replace HTTP response `catch {}` blocks in dev request handling
- [x] Replace config YAML parse silent catch in theme pack command
- [x] Replace test teardown `catch {}` blocks with shared best-effort cleanup helper
- [x] Replace expected-exception `catch {}` blocks in tests with explicit assertions

## 4. Dependency Matrix

- [x] Re-run architecture dependency matrix tests
- [x] Document remaining intentional project-reference constraints or exceptions
- [x] Avoid changing broad project references unless tests prove a real violation

## 5. Verification

- [x] Run build
- [x] Run architecture tests
- [x] Run full test suite
- [x] Update compliance review document with results
