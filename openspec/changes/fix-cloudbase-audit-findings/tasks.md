# Tasks

## A. Privacy matching

- [x] A1 Change the CloudBase privacy spec: envId only in host/cloud:// contexts; deploy covers
      every source envId; bare envId and paths pass.
- [x] A2 `PublicOutputPrivacyCheck`: remove the raw envId token and path scan; keep host and
      `cloud://` tokens.
- [x] A3 `DeployCommand` / `DeployContext` / `DeploymentPrivacyValidator`: pass all envIds and
      match only in CloudBase contexts.
- [x] A4 Split Notion and CloudBase matchers so messages name the right provider.
- [x] A5 `CloudBaseDeployUrlScanner.ContainsInJson`: parse failure after the `cloud://` prefilter
      is a leak.
- [x] A6 Tests: short envId in prose/path passes build and deploy; `prod-1` vs `prod-10`;
      multi-source deploy; provider-specific message; malformed JSON with `cloud://` fails.

## B. Parsing correctness

- [ ] B1 Parse the Markdown body; collect image `cloud://` URLs from `LinkInline`; keep a URL map.
- [ ] B2 Render through the map; leave fenced/inline code and raw HTML untouched; non-image
      `cloud://` links and failed images keep the placeholder/degradation rules.
- [ ] B3 Map sign results by `path`; mismatch becomes a per-item failure.
- [ ] B4 Require HTTPS and the default port in signed-link validation.
- [ ] B5 Tests: code fence/inline code examples unchanged; reordered sign response maps by path;
      non-default port rejected.

## C. Alignment and cleanup

- [ ] C1 Whitelist mode sends a `select` list; `all` keeps all columns.
- [ ] C2 Share host/path constants with Content, CLI and Engine.Audit; keep Config literals and
      assert them in Config tests.
- [ ] C3 Remove `CloudBaseErrorKind.InvalidRequest` and its mapping/test.
- [ ] C4 CloudBase type errors in the strict validator use 0C01 and the full path.
- [ ] C5 Doctor reports a blank sample idField as a fatal error.
- [ ] C6 Build the projector allow-set once per source.

## D. Docs and packaging

- [ ] D1 User doc recommends `maxItems` for large tables.
- [ ] D2 AOT restore stamp uses `TZ=UTC`.
- [ ] D3 Verify all batches: closure, specialty tests, gates, one review per batch; archive this
      change and update the archived spec.

## Verification

- Closure and classify for each batch; cache check before every run.
- Build/Config/Cli/Content/Engine/CloudBase/Architecture suites as the closure requires.
- Real-environment smoke reused where the change affects runtime behavior (`doctor`, `build`).
