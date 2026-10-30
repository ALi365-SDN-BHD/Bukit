# Complete Core stage 6 validation

User authorization (2026-10-28): execute, in order, behavior inventory → KD-011/012 → FAQ/HowTo and logger contracts → separate KD-007 repair → reports, specialty tests and coverage.

Preserve existing uncommitted stage-6 work. Work in `.worktrees/stage6-completion`; the original worktree remains unchanged. No commit, push, deployment, Notion write, whole-solution test or release gate is authorized by this change.

Scope: current Core output producers, their direct consumers, local report schemas, tests and official documentation. KD-002/003/004 and stage-7 stability/golden work remain outside this repair. KnownDefect skips remain explicit, never counted as passing behavior.

Decisions and verification evidence are recorded before advancing each ordered task. `tasks.md` is the only implementation checklist; the behavior matrix is a coverage inventory, not another task queue.

Additional user authorization (2026-10-28): repair audit I-01/I-02/I-03 and M-01–M-04 with minimum shared changes. Retain incomplete GEO items for existing diagnostics and filter only JSON-LD emission; isolate template anchor scope, preserve noscript literal text and raw-text word boundaries; strengthen RelatedContent/Archive evidence without new plugin policy. Preserve the earlier 44 changed files and baseline evidence. Reopen tasks 3/4/5; final acceptance awaits one controller-owned combined specialty review and one delta-only final review. Exact bounded commands and allowed paths are recorded in the audit repair evidence root and execution plan.
