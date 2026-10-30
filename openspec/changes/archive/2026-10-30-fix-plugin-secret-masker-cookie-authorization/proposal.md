# I-01: mask COOKIE and Authorization values in plugin execution reports

The user authorized a separate Core security task after the stage 9 specialty review found I-01: `PluginSecretMasker` did not cover the `COOKIE` and `Authorization` values that ADR-BUKIT-PLUGIN-SECURITY-MODEL §22.3 forbids recording in plugin execution reports. This is not authorization for unrelated Core changes, stage 10, commits, pushes or broad gates.

## Approved behavior

Extend the existing secret-key fragment contract so environment values for `COOKIE` and `AUTHORIZATION` (including `HTTP_COOKIE` and `HTTP_AUTHORIZATION`) are written as `***` in plugin execution reports. Keep the existing `NOTION_TOKEN`, `API_KEY`, `PASSWORD`, `TOKEN` and `SECRET` behavior, non-secret key visibility, null handling and report schema unchanged. Do not add a public API, option, dependency or new report field. Over-masking a key that contains `COOKIE` or `AUTHORIZATION` is acceptable and preferred to a plaintext leak.

## Scope and proof

Only `PluginSecretMasker.cs`, `PluginSecretMaskerTests.cs`, the existing report test in `PluginLockAndReportTests.cs`, the stage 9 plan record and this OpenSpec may change. Add failing `COOKIE`/`AUTHORIZATION` tests first, observe behavioral RED, then make the minimum fragment-list change and run the complete PluginHost specialty. No Core CLI, report schema, permissions evaluator or plugin configuration change.

Evidence root: `/Users/ali/.local/state/bukit-verification/stage9-plugin-secret-masker-20261029` (byte-identical durable mirror of the execution temp root; cache records retain the original output path).
