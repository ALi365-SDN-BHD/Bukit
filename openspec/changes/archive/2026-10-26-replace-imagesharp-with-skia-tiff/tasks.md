# Implementation checklist

- [x] Capture the failing encoder-identity migration regression.
- [x] Replace active package references with the pinned SkiaSharp/native/TIFF packages.
- [x] Migrate bounded full image validation and TIFF page traversal.
- [x] Migrate Engine encoding/resizing and freshness identities.
- [x] Migrate WeChat conversion while retaining limits and fallbacks.
- [x] Migrate image fixtures and add backend contract regressions.
- [x] Update current image guides.
- [x] Pass the corrected three specialty commands with default warning policy, including actual MediaLifecycle_ cases.
- [x] Complete controller-owned Native AOT execution evidence and scheduled review.

## Acceptance — 2026-10-25

**SUCCESS.** Content 132 (unchanged evidence reused), Engine 152, and WeChat 280 tests passed: **564 passed, zero failed/skipped**. Engine includes 23 lifecycle cases across all 13 `MediaLifecycle_` methods. The default warning policy remained enabled.

Native AOT CLI/probe publication and execution passed on macOS arm64: **20 checks**, including TIFF decoding/rejection and a real CLI image-site build with no PATH image tools. The scoped repair reviews and one final delta-only review resolved IMG-001–IMG-006; no Critical or Important finding remains.

Nonblocking **IMG-002-M1 (Minor/open)**: width planning does not forward cancellation into PNG eXIf scanning, so cancellation may wait for one large metadata scan. Recorded for follow-up under the repository's nonblocking-Minor policy.

Evidence: `/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/skia-tiff-migration-qM48Lo/`, especially `acceptance-evidence.md`, `orientation-review.md`, `native-final-summary.json`, and `final-review.md`.

Verification boundaries: Linux/Windows native image execution, real ImageMagick, and full/release gates were not run. APNG conversion and animated GIF/WebP WeChat conversion remain fail-closed as documented. Shared phase 1 SUCCESS and its three-platform evidence remain unchanged.
