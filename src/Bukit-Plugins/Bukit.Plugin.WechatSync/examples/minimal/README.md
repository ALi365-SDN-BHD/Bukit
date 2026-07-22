# WeChat Sync Plugin Minimal Fixture

This directory is a compatibility fixture for Bukit plugin config and static
manifest loading tests. It is not a runnable release package.

The nested `plugins/wechat-sync/plugin.yaml` intentionally uses a placeholder sha256
and does not include built plugin binaries. A release package must be produced
separately with real per-platform entries and hashes before users can run it
through `PluginCliLoader`.

Version `0.4.0` adds target-specific review gates. Draft sync defaults to
`reviewed,verified,approved`; direct publish defaults to `verified,approved`.
`published` is intentionally not an implicit approval because Bukit may derive it
when an explicit review status is absent. Sites with custom review vocabularies
must pass explicit `--draft-review-statuses` and `--publish-review-statuses` values;
the publish set must remain a subset of the draft set.
