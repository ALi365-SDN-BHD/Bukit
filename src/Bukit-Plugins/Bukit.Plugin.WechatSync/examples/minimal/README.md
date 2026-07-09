# WeChat Sync Plugin Minimal Fixture

This directory is a compatibility fixture for Bukit plugin config and static
manifest loading tests. It is not a runnable release package.

The nested `plugins/wechat-sync/plugin.yaml` intentionally uses a placeholder sha256
and does not include built plugin binaries. A release package must be produced
separately with real per-platform entries and hashes before users can run it
through `PluginCliLoader`.
