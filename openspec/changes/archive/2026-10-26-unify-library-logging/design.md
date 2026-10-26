# Design

Existing `ILogger` and `ConsoleLogger` remain unchanged. Required internal logger
parameters connect the 23 warning sites to existing asset, content, cache, variant,
report, and process contexts. Old public entries delegate to Warn/text defaults;
new public logger parameters reject null. The Markdown provider's logger path and
plugin default factory stay internal. Notion fetching receives the runtime logger
at its existing internal call. Directory hash caches retain `Func<string, string>`.

`CommandConfigLoader` uses a private per-call ordered buffer and exactly one parse.
Success replays once to the configured level/format (`--ci` still selects Warn).
Load failure replays once to Warn/bootstrap in the selected format, clears pending
events, and bare-rethrows the same exception even if replay fails. Its catch does
not surround later command validation. No static context, AsyncLocal, or second read.
Deploy builds the already-loaded config using the existing engine API. Preview
retains explicit priority and nearest implicit config/error-result behavior.

Program captures raw global format before discovery; internal descriptor closures
and the internal plugin factory carry it explicitly. Public command/factory APIs
keep text defaults and Build's existing bound format option. Error envelopes,
business stdout, parser grammar, and plugin protocol capture stay unchanged.

One existing `BuildDiagnosticLogger` counts actual calls before display filtering.
Variants reuse it rather than creating text-only sinks. Pre-transaction and
post-report security diagnostics use the original logger and are not backfilled.
Reports are not rewritten after the security gate.

A test-only SDK Roslyn semantic guard scans each current Core project, excluding
bin/obj, Cli, Cli.Shared, and exactly Shared/Logger.cs. No Theme exemption remains.
The canonical public API snapshot must show 426 types/19 mappings and exactly the
three approved additive members. Verification is restricted to the eight exact
parent-authorized specialties and their direct prerequisites.
