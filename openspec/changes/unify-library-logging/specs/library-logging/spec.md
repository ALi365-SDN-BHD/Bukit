## ADDED Requirements

### Requirement: Caller-owned library diagnostics

All 23 approved Core library warning sites SHALL call the supplied `ILogger.Warn`
with unprefixed payloads. Twenty prior warning texts SHALL retain exact text,
stderr, and newline behavior with the unchanged ConsoleLogger. Security and both
termination warnings SHALL gain the accepted text prefix. JSON and Error-level
filtering SHALL apply without changing validation, fallbacks, process cancellation,
timeouts, output bounds, captured protocol streams, or hash/event ordering.

#### Scenario: Public compatibility
- WHEN an existing public entry is called
- THEN its signature and behavior remain available with Warn/text defaults
- AND only the three approved logger-aware public members are added
- AND new logger parameters reject null.

### Requirement: One-pass command startup

Configured Core commands SHALL load once per invocation, including Deploy's build
and explicit/implicit Preview. A per-call ordered buffer SHALL replay once to the
final configured sink, with the existing CI Warn override. Load failure SHALL
replay with Warn/bootstrap and rethrow the same exception, retaining exception
precedence if output fails. Later validation SHALL not trigger failure replay.

#### Scenario: Startup filtering and failure
- WHEN a successful configuration selects Error
- THEN startup warnings are not displayed
- WHEN a late load failure follows an existing warning
- THEN that warning uses the selected format before the original error
- AND a later command invocation has no pending state from the first.

### Requirement: Explicit global format

Program SHALL select the raw global format before plugin discovery and explicitly
pass it through internal factories/closures. Non-Build normalization SHALL not lose
JSON. Old public defaults, Build's bound format, command stdout, error envelopes,
parser grammar, catches, and exit codes SHALL remain unchanged. Plugin discovery
SHALL use Warn/bootstrap without requiring site configuration or modifying protocol results.

### Requirement: Existing report interval

Each actual Warn/Error inside the existing build counter SHALL count once before
display filtering. Variants SHALL retain the supplied sink and shared counts.
Startup/pre-transaction events SHALL not be backfilled; security warnings SHALL
remain after report writing without rewriting it. Counts and derived report hashes
MAY change, but schema, public page bytes, transactions, and cache algorithms SHALL NOT.

### Requirement: Architectural enforcement

A semantic source guard SHALL forbid direct Core library console output,
redirection, and colors, with only exact Cli/Cli.Shared/Shared Logger and generated
bin/obj exclusions. Its self-cases SHALL distinguish qualified/aliased/static calls
and real interpolation calls from comments, strings, and unrelated Console types.
