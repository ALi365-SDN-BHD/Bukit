# Notion Schema Diff v1.2 Design

Status: Approved design, pending implementation plan
Date: 2026-06-28
Command: `bukit notion schema diff`

## 1. Purpose

Bukit v1.1 can validate that a local `notion-database-map.yaml` matches the
remote Notion data-source schema. v1.2 adds a read-only diff command that
explains the mismatch rather than only reporting validation failures.

The command answers three questions for every mapped data source:

1. Which locally mapped properties are missing remotely?
2. Which mapped properties exist remotely with a different type?
3. Which remote properties are not represented in the local map?

The result is intended for inspection, support, and future schema-sync planning.
It does not modify Notion or generate an executable patch plan.

## 2. User Interface

The only v1.2 command is:

```bash
bukit notion schema diff \
  --database-map ./sites/demo/notion-seed/notion-database-map.yaml \
  --token-env NOTION_TOKEN \
  [--report ./.bukit/reports/plugin-output/notion/notion-schema-diff-report.json]
```

Options:

- `--database-map` is required.
- `--token-env` is optional and defaults to `NOTION_TOKEN`. It remains
  restricted by the existing allowlist.
- `--report` is optional and names the JSON report. The Markdown report uses
  the same basename with the `.md` extension.

Default artifacts:

- `.bukit/reports/plugin-output/notion/notion-schema-diff-report.json`
- `.bukit/reports/plugin-output/notion/notion-schema-diff-report.md`

Artifact types:

- `notion-schema-diff-report`
- `notion-schema-diff-report-md`

## 3. Scope

### 3.1 Included

- Load and validate the existing database-map contract.
- Resolve `dataSourceId`, with `databaseId` retaining its existing legacy alias
  behavior.
- Retrieve every referenced data source through
  `GET /v1/data_sources/{data_source_id}`.
- Compare exact property names and exact Notion property types.
- Report missing, mismatched, and extra remote properties.
- Continue after a per-entry remote failure and retain partial results.
- Write deterministic JSON and Markdown reports on success and failure.
- Expose the command through the runtime command factory, static manifests,
  plugin invocation protocol, and CLI nested-command forwarding.

### 3.2 Excluded

- Schema sync or any remote write.
- Patch-plan generation.
- Creating, deleting, renaming, or changing the type of a remote property.
- Creating a database or data source.
- Automatic title or `uniqueField` repair.
- Push-state, resume, or idempotency work.
- Changes to `guide-0.1/` or `scripts-0.1/`.

Title cardinality and `uniqueField` validation remain owned by
`notion schema validate`. The diff command reports only the three approved
property-set differences.

## 4. Architecture

### 4.1 Chosen approach

Introduce a dedicated schema-diff service and extract the common remote-schema
inspection pipeline from v1.1. This avoids duplicating map loading, token
validation, API retrieval, entry ordering, and stable remote-error mapping.

Rejected alternatives:

- Building diff directly from the current validation result would couple
  inspection to validation exit semantics and still omit extra remote
  properties.
- Copying the v1.1 retrieval loop would be quicker initially but would create
  two implementations of the same authentication, retry, and failure rules.
- Adding a `--diff` mode to `schema validate` would blur two commands whose
  success semantics intentionally differ.

### 4.2 Components

#### Shared remote-schema inspector

An internal inspector owns only acquisition:

1. Validate the local database map.
2. Validate and resolve the token environment variable.
3. Create one Notion client.
4. Visit valid map entries in ordinal entry-name order.
5. Retrieve each effective data-source identifier.
6. Preserve the full remote property dictionary.
7. Normalize remote failures to the existing stable diagnostic codes.
8. Continue after per-entry failures and return an aggregate inspection result.

The inspector does not compare schemas and does not write reports. Its result
contains the validated map entries, their remote snapshots when available,
per-entry diagnostics, aggregate diagnostics, and the resulting exit
classification.

#### Existing validation service

`NotionRemoteSchemaValidationService` consumes inspection snapshots and applies
the existing v1.1 rules:

- mapped property existence;
- mapped property type equality;
- exactly one title property;
- remote `uniqueField` existence.

Its public models, diagnostics, report schema, artifact types, exit codes, and
observable ordering remain compatible with v1.1.

#### New diff service

`NotionSchemaDiffService` consumes the same inspection snapshots and calculates
the approved diff arrays. It owns v1.2 result models and delegates serialization
to a dedicated `NotionSchemaDiffReportWriter`.

#### Plugin adapter

The plugin adds:

- a `schema diff` runtime command spec;
- mapper logic using the same map, token, and report-path guards as
  `schema validate`;
- a handler that translates domain results to plugin diagnostics and artifacts;
- exact dispatch for `notion schema diff`;
- matching static manifest entries.

The existing recursive CLI subcommand binding remains the transport mechanism;
v1.2 must not add another CLI-specific command path implementation.

## 5. Data Flow

1. CLI parses `notion schema diff` and forwards the three-segment path plus
   typed options to the Notion plugin.
2. The plugin mapper rejects an invalid path, missing map, disallowed token
   variable, invalid option type, or report path outside approved roots.
   Mapper-level rejection returns no report because no safe, complete domain
   request exists.
3. The handler calls the schema-diff service.
4. The shared inspector validates the map and token before any network call.
5. The inspector retrieves every valid data source and retains complete remote
   properties.
6. The diff service computes deterministic difference arrays for successful
   snapshots and preserves failed entries with diagnostics.
7. After mapping succeeds, the report writer emits JSON and Markdown for every
   service result, including invalid map contents, token failures, entry
   failures, and successful inspection.
8. The handler returns relative artifact paths and stable diagnostics.

## 6. Comparison Semantics

Property names use `StringComparer.Ordinal`. Property types use
`StringComparison.Ordinal`.

For one data source:

- `missingProperties` contains local map property names absent from the remote
  dictionary.
- `typeMismatches` contains local properties present remotely whose type is not
  an exact ordinal match.
- `extraRemoteProperties` contains remote property names absent from the local
  map.
- Matching properties appear in none of the three arrays.

A case-only name difference is intentionally represented as one missing local
property and one extra remote property. No fuzzy matching or rename inference is
performed.

Deterministic ordering:

- data-source results: map entry name, ordinal;
- missing properties: property name, ordinal;
- type mismatches: property name, ordinal;
- extra remote properties: property name, ordinal;
- diagnostics: entry traversal order followed by the single summary diagnostic.

## 7. Report Contract

The JSON schema identifier is:

```text
bukit.notion.schema.diff.v1
```

Representative successful report with differences:

```json
{
  "schema": "bukit.notion.schema.diff.v1",
  "success": true,
  "hasDifferences": true,
  "databaseMap": "sites/demo/notion-seed/notion-database-map.yaml",
  "dataSources": [
    {
      "entry": "pages",
      "collection": "page",
      "dataSourceId": "data-source-id",
      "identifierSource": "dataSourceId",
      "success": true,
      "hasDifferences": true,
      "missingProperties": ["Published"],
      "typeMismatches": [
        {
          "property": "Slug",
          "expected": "rich_text",
          "actual": "url"
        }
      ],
      "extraRemoteProperties": ["Owner", "Status"],
      "diagnostics": []
    }
  ],
  "diagnostics": []
}
```

Semantics:

- Top-level `success` means all required inspection operations completed.
- Top-level `hasDifferences` is true when any successfully inspected entry has
  a non-empty diff array.
- Per-entry `success` distinguishes a completed comparison from an entry whose
  schema could not be retrieved.
- A failed entry has `hasDifferences: false`, empty diff arrays, and its stable
  diagnostic. Absence of a snapshot is not misrepresented as a schema diff.
- Top-level diagnostics aggregate entry failures and contain one
  `notion.schemaDiffFailed` summary diagnostic when `success` is false.

The Markdown report contains:

- command success and `hasDifferences` summary;
- database-map path;
- one data-source summary table;
- per-entry missing-property, type-mismatch, and extra-property tables;
- diagnostics when present.

Markdown cells escape pipes and replace line endings, matching the v1.1 report
writer's safety behavior.

## 8. Exit Codes and Diagnostics

Exit-code semantics intentionally differ from validation:

- `0`: all data sources were inspected, including when differences exist;
- `2`: invalid local input, missing/disallowed token, or referenced data source
  not found;
- `1`: authentication, authorization, conflict, rate limit, transport, or
  Notion service failure.

When multiple entries fail, runtime failures take precedence over input/not-
found failures for the aggregate exit code. The command still reports every
completed entry.

Existing stable diagnostics are reused:

- local database-map diagnostics;
- `notion.tokenEnvNotAllowed`;
- `notion.tokenMissing`;
- `notion.remoteSchemaDataSourceNotFound`;
- `notion.apiUnauthorized`;
- `notion.apiForbidden`;
- `notion.apiConflict`;
- `notion.rateLimited`;
- `notion.apiFailed`;
- `notion.apiError`;
- `notion.httpError`.

New v1.2 summary diagnostic:

- `notion.schemaDiffFailed`.

Schema differences are report data, not error diagnostics.

## 9. Security and Path Boundaries

- `--token-env` accepts only the existing allowlisted variable.
- Local map validation and token validation complete before the first network
  request.
- The API token remains in request headers only.
- Reports and diagnostics must not contain token values, Authorization headers,
  or raw API response bodies.
- The default report is under
  `.bukit/reports/plugin-output/notion`.
- Custom reports must resolve under
  `.bukit/reports/plugin-output/notion` or `.bukit/tmp/notion` using the existing
  path guard.
- Plugin artifacts are returned as project-relative paths.
- The command is read-only and uses no Notion mutation endpoint.

## 10. Compatibility

The extraction of the shared inspector is an internal refactor. The following
v1.1 behaviors are invariants:

- `notion schema validate` command shape;
- validation diagnostic codes and summary behavior;
- validation exit-code classification;
- validation report schema and artifact types;
- property, entry, and diagnostic ordering;
- `dataSourceId` precedence and legacy `databaseId` alias;
- partial failure behavior and report secrecy.

Existing validation tests must pass without weakening assertions.

## 11. Testing Strategy

### Domain tests

- no differences;
- missing local-mapped remote properties;
- type mismatches;
- extra remote properties;
- a case-only name mismatch producing missing plus extra;
- exact ordinal type matching;
- multiple entries and deterministic sorting;
- `dataSourceId` precedence and legacy `databaseId` alias;
- partial results after a failed entry;
- differences returning exit code `0`;
- invalid map and missing/disallowed token producing no network calls;
- 404, 401, 403, 409, 429, 5xx, and transport classification;
- exactly one `notion.schemaDiffFailed` summary diagnostic on failure.

### Report tests

- JSON schema and camel-case shape;
- Markdown summary and all three diff tables;
- stable ordinal ordering;
- pipe and newline escaping;
- success and failure artifacts;
- absence of token and raw response values.

### Plugin and CLI tests

- runtime command factory exposes `notion schema diff`;
- static manifests match the runtime command;
- mapper rejects missing/invalid options and unsafe report paths;
- handler maps success, partial failure, artifacts, and diagnostics;
- app dispatch reaches the diff handler;
- three-segment CLI forwarding preserves all options;
- unsupported-command text lists both schema commands.

### Regression and repository proof

- full `Bukit.Notion.Tests`;
- full `Bukit.Plugin.Notion.Tests`;
- focused nested plugin CLI tests;
- formatting and diff checks;
- the repository-owned `scripts/quality-gate.sh Release` gate;
- final requirement-by-requirement code audit.

No live Notion token is required by the automated gate. HTTP contract tests
prove the endpoint, headers, property parsing, and error normalization.

## 12. Documentation

Update only mainline documentation and plugin surfaces:

- `plugins/Bukit.Plugin.Notion/README.md`;
- `docs/plugins/Bukit.Plugin.Notion 开发技术书.md`;
- runtime command metadata;
- static minimal manifest and template.

Backup-only `guide-0.1/` and `scripts-0.1/` remain unchanged.

## 13. Acceptance Criteria

The feature is complete only when all of the following are proven:

1. `bukit notion schema diff` is discoverable and invokable through runtime and
   static plugin manifests.
2. Every valid map entry causes one retrieve-data-source request.
3. Reports contain exact missing, type-mismatch, and extra arrays.
4. Case-sensitive comparison and deterministic ordering are tested.
5. A completed diff with differences exits `0`.
6. Invalid input and remote operational failures use the approved exit classes.
7. Partial failures preserve completed entry results and write both reports.
8. Reports contain no secret or raw response body.
9. v1.1 validation behavior remains compatible.
10. No v1.3 or later feature is introduced.
11. No backup-only path is changed.
12. Focused tests and the repository quality gate pass.
