# Environment references in the CloudBase filter value

## Why

The CMS publishes by release, and a build must read one release. `BUKIT_` overrides cannot set a
list item such as a `content.sources` entry. Roadmap item F6
(`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`).

## What Changes

- `cloudbase.filter.value` accepts `${env:NAME}` references, resolved when the build creates the
  content source. An unset or empty variable fails with `ConfigRequiredFieldMissing`.
- A malformed `${...}` in the filter value fails validation.
- Docs: the release workflow, the `security_invoker` live-release view, and the publish-during-build
  trade-off.

## Impact

- Config: `ConfigEnvValue`, `ProviderValidators`. Engine: `ContentProviderFactory`.
- No public API change. Address fields (`envId`, `table`) do not accept references.
- Credential strategy unchanged: the Publishable Key reads the release being built (option (a),
  documented). A CI-only credential (option (b)) is a separate proposal.
