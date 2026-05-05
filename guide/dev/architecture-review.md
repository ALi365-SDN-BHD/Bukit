# Project Architecture Review (P1 Alignment)

This document provides a semi-formal review of the current repository architecture from a maintainer's perspective.

## 1. Review Conclusion

The overall architecture (bounded by `bukit.slnx` and `src/Bukit.*`) remains mature and maintainable. Key strengths: clear layering, stable main pipeline, healthy unidirectional dependencies, clear plugin boundaries. Main risks have shifted from core code to capability boundaries and governance consistency.

## 2. Key Strengths

### 2.1 Clear Module Boundaries
```text
CLI -> Config -> Content -> Routing -> Rendering -> Engine -> Plugins -> Output
```

### 2.2 Correct Plugin Abstraction
Divide into `derive-pages` + `after-build`, aligning with the static site generator domain model.

### 2.3 Body Model Improvement
Main pipeline now uses `BodyStore + BodyKey` deferred body reading pattern, no longer a "body HTML must be pre-loaded in memory" model.

## 3. Key Weaknesses

### 3.1 Large-Scale Body Read/Cache Needs Benchmarking (Medium-High)
Body loading has been deferred, but rendering/search/RSS/pagination stages still trigger reads on different paths; read amplification needs quantification.

### 3.2 collections vs Compatibility Layer Governance (Medium)
`collections` is the primary path; `post/page` default rules are the compatibility layer. Strategy needs convergence.

### 3.3 CLI Extensibility (Medium)
Lightweight argument parsing is AOT-friendly but declarative capabilities and unified error experience need improvement.

### 3.4 AOT Contracts Dynamic Plugin Ecosystem (Medium-High)
AOT eliminates external DLL loading; protocol-based (external-protocol) extensions need continued strengthening.

## 4. Scores

| Dimension | Score |
|---|---|
| Maintainability | 8.6/10 |
| Extensibility | 8.1/10 |
| Testability | 7.6/10 |
| Deliverability | 7.4/10 |
| Scalability | 7.2/10 |
| Open Ecosystem | 6.8/10 |

## 5. Priority Recommendations

1. Complete body read/cache benchmark governance
2. Converge collections and compatibility layer strategy
3. Establish doc-asset consistency checks
