# Code quality governance

Bukit separates zero-debt build rules from historical analyzer debt. It does
not enable an entire SDK analyzer mode as a build blocker and does not treat
every informational modernization suggestion as a defect.

## Enforcement tiers

### Build-blocking rules

`Directory.Build.props` pins `AnalysisLevel` to `9.0`. Compiler warnings,
nullable warnings, and explicitly selected analyzer warnings are build errors.
The Core source policy currently blocks:

- `IDE0055` for Roslyn formatting consistency;
- `CA1001`, `CA1063`, `CA1816`, `CA2213`, `CA2215`, and `CA2216` for disposable
  ownership and implementation;
- `CA2012`, `CA2016`, and `CA2250` for async and cancellation correctness.

These rules had zero accepted debt when promoted. A suppression must be scoped
to the narrowest source boundary and explain why the behavior is intentional.

### Report-only ratcheted rules

Rules with existing contract or migration cost remain at `suggestion` and are
checked by the committed non-increase baseline:

- `CA1068`: three current cancellation-token ordering findings include stable
  signatures and are not reordered in a governance-only change;
- `CA1849`: synchronous calls from async methods require behavioral and
  performance review rather than mechanical conversion;
- `CA2000`: current findings include ownership transfers and long-lived
  wrappers that require per-instance lifetime analysis.

The baseline records diagnostic counts only. It does not store source paths,
messages, or line numbers, and a lower count never fails the gate.

## Naming and API design

Core types and ordinary non-private members use PascalCase. Interfaces also use
the `I` prefix. The policy intentionally does not force a private-field style,
because doing so would create a separate migration unrelated to public code
quality.

`SectionRenderHelper.render_section` is the only current naming exception. Its
snake_case CLR member name is consumed as a stable Scriban theme function, so
the exception is scoped to that exact source file instead of renaming the
contract or disabling naming analysis for the project.

`CA1000`, `CA1050`, `CA1068`, `CA1710`, `CA1711`, and `CA1720` remain
suggestion-level API-design inventory. Public API compatibility is enforced by
the separate public API drift gate; analyzer findings do not authorize a stable
signature change.

EditorConfig cannot express a Task/ValueTask-aware `Async` suffix rule without
also matching unrelated methods. Bukit therefore does not approximate it with
a broad suffix rule. A dedicated analyzer or an assembly-aware contract test
requires a separate evidence-backed task before it can become build-blocking.

### Broad informational inventory

All SDK style and analyzer diagnostics at `info` severity are inventoried by:

```bash
bash scripts/checks/code-analysis-ratchet.sh check
```

This catches new diagnostic IDs or count increases without rewriting the
existing modernization backlog. Do not bulk-format or bulk-fix the baseline.

## Policy changes

For an intentional SDK analyzer-wave or `.editorconfig` change:

1. Run the format check and affected project tests.
2. Write a candidate baseline to a new path:

   ```bash
   bash scripts/checks/code-analysis-ratchet.sh snapshot /tmp/code-analysis.candidate.json
   ```

3. Compare every diagnostic ID against the committed baseline.
4. Fix confirmed correctness issues before increasing any allowance.
5. Replace the committed baseline only in the same reviewed policy task.
6. Run the ratchet self-test, real check, and the repository focused gate.

Do not replace `AnalysisLevel` with `latest` in a routine SDK update. Analyzer
wave changes require their own reviewed baseline delta.
