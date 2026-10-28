# Stage 6 execution design

## Order and compatibility

1. Inventory the ten registered built-in plugins and four aggregate-output families against actual production entrypoints. Map normal, rejection/failure and boundary behavior to existing test methods; distinguish unexecuted inventory from passing evidence.
2. Resolve KD-011/012 without gratuitously changing serialized production output: evaluate preserving null-command omission and declaring the reporter's two optional fields. Observe RED before the schema repair; preserve strict rejection of wrong types and unknown fields.
3. Resolve empty FAQ/HowTo semantics and logger concurrency from their callers and documented guarantees, then add explicit regression and documentation evidence. Do not mistake the thread-safe test logger for a public contract.
4. Address KD-007 as its own writer task. Prior ad hoc scanners were withdrawn after security regressions. Evaluate an established parser against all retained counterexamples before choosing an implementation; preserve safe links, visible text, decoding and whitespace contracts. Do not silently relax security or accept text loss.
5. Close the 24-report ledger, matrix gaps and SCSS diagnostic disposition; run the complete affected specialties and Engine coverage after code stabilizes. Reuse only matching evidence. Run one specialty review per task and one final delta-only review.

## Verification

Each task has an external machine-readable closure and exact commands under the evidence root. Engine/site builds are fixture-exclusive; other .NET commands are serial. Contracts use actual writers/files plus wrong-field/type mutations. Final Engine command includes `--settings coverage.runsettings --collect 'XPlat Code Coverage'`; summarize only the required Engine project floor, not a whole-Core gate.

No test count or historical coverage substitutes for a missing behavior. Existing KD-003/004 skips are listed as inherited unmet behaviors, not additional stage-6 repair authority. Schema URLs are local bindings, not proof of publication.

## KD-011/012 decision

Preserve existing serialized output. `command` stays optional and accepts string/null when present, matching the public renderer's established null omission and historical G-04D5B compatibility decision. Keep schema ID/version and all other required fields unchanged. Declare `resourceLimitExceeded` as an optional string and `networkPermissionGranted` as an optional boolean, matching the existing reporter; do not require them in old reports. Both schemas retain `additionalProperties: false`. Tests require real renderer/writer output to pass and reject wrong field types and unknown fields. This is a separately authorized contract correction, not a rewrite of the historical G-04D5B task.

## Empty FAQ/HowTo and logger decision

Omit an invalid FAQPage/HowTo JSON-LD node when its required list is absent or empty; preserve generic configured WebPage/WebSite nodes, never fabricate an Article fallback. Report `geo.faq_items_missing` or `geo.howto_steps_missing` through the existing warn/strict/off diagnostic policy. Existing nonempty-item validation and citation/mentions compatibility stay unchanged.

Keep existing concurrent logging behavior. Public `ILogger` implementations must tolerate simultaneous calls; no ordering across workers is promised. Document this requirement and verify default ConsoleLogger JSON records remain complete and lossless under concurrent calls. Do not add a global lock, alter counters, or claim arbitrary user loggers were repaired.

## KD-007 parser decision

Reuse the repository-pinned AngleSharp 1.5.2 through an Engine reference; no package/version is added centrally. DOM parsing owns namespaces, CDATA, comments, attributes and raw-text context, with no resource loader or JavaScript execution. Use an iterative text walk and normalize hidden nodes before the existing Markdown safe-link projection. Keep the existing innermost-explicitly-closed-anchor rule for malformed markup.

The prior ten-case Chromium discrepancy is a script-escape boundary defect, not solved by `IsScripting`. Guard only actual HTML script elements identified by the DOM/source positions: remove their source ranges using script-data escape states and the library tokenizer for complete closing tags, then parse the remaining document. SVG scripts remain DOM-owned. Retain the browser-parity regressions and record this dependency limitation explicitly; do not replace it with another whole-HTML hand scanner. Native AOT publication remains outside stage-6 authorization and is not claimed from managed tests.

## Authorized audit repairs (verified; both prescribed reviews accepted)

The parser previously discarded incomplete FAQ/HowTo entries before existing diagnostics could inspect them. The repair preserves entries in the existing models (empty required strings for missing values, including non-map item placeholders), then filters only JSON-LD emission. All-invalid lists omit specialized nodes without Article fallback; valid HowTo positions remain contiguous. Schema/date/citation consumers and public model declarations stay unchanged.

The anchor token state respects actual inert `IHtmlTemplateElement` scope rather than allowing its inner anchor to overwrite an outer one. The pinned parser moves children into Content when closing a template; the callback observes that DOM state and restores the saved outer anchor, with an explicit empty-template case. Foreign templates do not become inert HTML scopes; SVG self-closing controls and true malformed nested anchors retain their policy. With scripting enabled, noscript is literal raw text, not a link source: normalize it before Markdown scanning. Synthetic HTML textarea nodes receive text boundaries even without source references; implicit document wrappers still do not. Keep the pinned parser and existing script guard, not a new scanner framework.

Two evidence-only plugin corrections assert RelatedPages data for disabled/single-item cases and identify Archive's actual blank-collection boundary. The complete affected Engine specialty is the only fresh full specialty; historical CLI/Host/logger/Architecture evidence is not relabeled current-closure HIT. Details and exact RED/GREEN commands: `docs/plans/2026-10-28-core-stage6-audit-repairs.md`.

Implementation proof: GEO 139/139, projection 176/176; full Engine 2881 Passed / 0 Failed / 3 inherited NotExecuted and Engine-only 14702/16351 covered lines. An interrupted infrastructure attempt is preserved separately, never merged into coverage. The repair-root `specialty-review.md` and single `final-unified-review.md` each report 0 Critical / 0 Important / 0 Minor and accept all seven audit repairs. Task 5 is closed within the authorized scope; only acceptance-status documentation changed after review, with the 957 frozen runtime inputs preserved and no fixture rerun.
