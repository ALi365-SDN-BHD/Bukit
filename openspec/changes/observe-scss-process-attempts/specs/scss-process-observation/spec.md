# SCSS process-attempt observation

## Requirement: Correlate real attempts at the existing runner boundary

Each probe and compile runner invocation SHALL emit Debug start/end events with one unique attempt ID, fixed phase/logical candidate, original timeout and elapsed call-boundary milliseconds. Returned exit codes SHALL be actual runner results; a returned nonzero code SHALL not be confused with an exception. Timeout/cancellation/error SHALL preserve the original exception and existing caller behavior, with unavailable exit/stderr rather than invented results. Candidate order, 3s/5s budgets, CSS validity/output checks, cleanup and old warning/Info payloads SHALL remain unchanged.

## Requirement: Fully suppress untrusted stderr in the new events

New events SHALL contain no raw stderr/stdout, secrets/environment values, arguments, paths, hashes or exception messages. For a returned result, stderr SHALL be empty or redacted and include its returned diagnostic string's UTF-16 character count. For a thrown invocation it SHALL be unavailable. Existing legacy warning content is not newly guaranteed masked by this observation feature. No general-purpose masking framework, new dependency/public API/configuration/schema or runner behavior change is authorized.

## Requirement: Validate observation without claiming historical repair

Real controlled success/fallback/rejection/compile-error/missing-output/timeout/cancellation cases SHALL preserve old outcomes and assert paired observation metadata and suppression. Retain the original real multilingual source/compile-once/CSS and failure-retention assertions. Observe missing-event RED before implementation, then run the exact bounded coverage specialty and prescribed reviews. Injected faults are not reproduction of the historical missing-log failure; physical executable resolution, raw stderr, thrown-process results and OS scheduling latency remain unobserved. No full Engine, stability loop, cross-platform or stage-7 proof is implied.
