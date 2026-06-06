# Checklist

- [x] Spec suite exists and is linked to this change set.
- [x] No production assembly exposes internals to another production assembly.
- [x] Architecture test enforces IVT test-only policy.
- [x] `rg -n "catch\\s*(\\([^)]*\\))?\\s*\\{\\s*\\}" src tests -g '*.cs' -g '!bin' -g '!obj'` has no hits.
- [x] Dependency matrix tests pass.
- [x] Build passes.
- [x] Full test suite passes.
- [x] Compliance review reflects this governance pass.
