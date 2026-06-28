# Notion Remote Schema Validation Design

## Status

Approved for implementation on 2026-06-28.

## Goal

Add a read-only Notion plugin command that validates every local
`notion-database-map.yaml` entry against its remote Notion data source before a
push reaches page creation or update APIs.

## Scope

The v1.1 command is:

```bash
bukit notion schema validate \
  --database-map ./sites/demo/notion-seed/notion-database-map.yaml \
  --token-env NOTION_TOKEN \
  [--report ./.bukit/reports/plugin-output/notion/notion-schema-validation-report.json]
```

`--database-map` is required. `--token-env` defaults to `NOTION_TOKEN` and may
only name the existing allowlisted token variable. `--report` is optional and
defaults to:

```text
.bukit/reports/plugin-output/notion/notion-schema-validation-report.json
```

The command also writes the Markdown sibling:

```text
.bukit/reports/plugin-output/notion/notion-schema-validation-report.md
```

The existing `validate-seed`, `validate-database-map`, and `push` commands keep
their current names and behavior.

## Alternatives Considered

### `notion schema validate` — selected

This creates one namespace for the planned `schema diff` and `schema sync`
commands. The current plugin command descriptor recursively supports nested
subcommands, so no Core CLI special case is required.

### `notion validate-remote-schema`

This follows the existing flat validation command names, but creates a one-off
surface that does not extend cleanly to diff and sync.

### Expose both forms

This improves discoverability at the cost of duplicate manifest, documentation,
and compatibility obligations. No alias is added in v1.1.

## Compatibility Rules

The existing database-map contract remains authoritative:

- `dataSourceId` takes precedence when it is non-empty.
- `databaseId` remains a legacy alias for a data source identifier.
- The command passes the effective identifier to the data-source retrieve API.
- It does not reinterpret `databaseId` as a database container ID or select one
  of multiple child data sources.

This preserves the same identifier semantics used by the current push service.
Database-container discovery is outside this feature.

## Architecture

The implementation stays within the existing external process plugin boundary:

```text
CLI manifest and invoke request
  -> Notion schema validate option mapper
  -> local database-map validator
  -> token provider and Notion client factory
  -> remote schema validation service
  -> JSON and Markdown report writer
  -> plugin diagnostics and project-relative artifacts
```

Core continues to know only the plugin manifest and protocol. Import remains
local-only. The Notion plugin retains its existing network and `NOTION_TOKEN`
permissions.

### Plugin command layer

`NotionCommandSpecFactory` adds a `schema` subcommand containing `validate`.
`NotionPluginInvoker` dispatches the exact path
`["notion", "schema", "validate"]`. `NotionOptionsMapper` validates options,
resolves the database-map and report paths within the project, and rejects any
report outside the existing Notion report or temporary output roots.

### Notion client layer

`INotionClient` gains a retrieve-data-source operation. `NotionHttpClient`
implements it with:

```http
GET /v1/data_sources/{data_source_id}
Authorization: Bearer <token>
Notion-Version: 2026-03-11
```

The response is reduced to a typed data source identifier and an ordinal map of
property name to property type. Raw response bodies and tokens are not exposed
to reports or diagnostics.

### Remote schema domain layer

A dedicated `RemoteSchema` namespace owns options, result models, diagnostics,
per-data-source comparison records, the validation service, and report writing.
It depends on the existing mapping validator, token provider, and injectable
Notion client factory. The plugin handler only translates its result to protocol
objects.

## Validation Flow

1. Run `NotionDatabaseMapValidator` first. Invalid local maps do not trigger
   network calls.
2. Resolve and validate the allowlisted token. A missing token does not trigger
   network calls.
3. Sort database-map entries by entry name for deterministic requests and
   reports.
4. Retrieve each effective data source identifier in sequence.
5. Compare every mapped property name using ordinal, case-sensitive matching.
6. Compare mapped and remote property types using ordinal, case-sensitive
   matching.
7. Count remote properties whose type is `title`; exactly one must exist.
8. Confirm that `uniqueField` names a property present in the remote schema.
9. Continue through all entries after entry-level mismatch or API failure so the
   report contains the complete requested validation set. Cancellation is the
   only early-exit condition.
10. Write JSON and Markdown reports for both successful and failed validation
    after option mapping has produced a safe report path.

Remote properties not referenced by the local map do not fail validation and
are not reported as differences. That behavior belongs to v1.2 `schema diff`.

## Diagnostics and Exit Codes

The command emits the required stable diagnostics:

- `notion.remoteSchemaDataSourceNotFound`
- `notion.remoteSchemaPropertyMissing`
- `notion.remoteSchemaPropertyTypeMismatch`
- `notion.remoteSchemaTitleMissing`
- `notion.remoteSchemaUniqueFieldMissing`
- `notion.remoteSchemaValidationFailed`

It also emits:

- `notion.remoteSchemaTitleNotUnique` when more than one remote title property
  is observed.
- Existing token diagnostics for missing or disallowed token sources.
- Existing stable Notion API diagnostics for authentication, authorization,
  conflict, rate-limit, server, and transport failures, except that a retrieve
  `404` is normalized to `notion.remoteSchemaDataSourceNotFound`.

Exit codes are:

- `0`: every mapped data source matches.
- `2`: local input, option, token, or schema-validation failure.
- `1`: remote authentication, authorization, transport, or Notion service
  failure other than data-source not found.

`notion.remoteSchemaValidationFailed` is the summary diagnostic whenever the
overall result is unsuccessful. Granular diagnostics remain available in the
same result and report.

## Report Contract

The JSON document uses schema identifier:

```text
bukit.notion.schema.validation.report.v1
```

Its logical shape is:

```json
{
  "schema": "bukit.notion.schema.validation.report.v1",
  "success": false,
  "databaseMap": "sites/demo/notion-seed/notion-database-map.yaml",
  "dataSources": [
    {
      "entry": "pages",
      "collection": "page",
      "dataSourceId": "remote-id",
      "identifierSource": "dataSourceId",
      "success": false,
      "titleProperty": "Name",
      "uniqueField": "Slug",
      "properties": [
        {
          "name": "Slug",
          "expectedType": "rich_text",
          "actualType": "url",
          "status": "type-mismatch"
        }
      ],
      "diagnostics": []
    }
  ],
  "diagnostics": []
}
```

The Markdown report contains a summary followed by one table per data source and
a diagnostics table. Both formats omit the token, authorization headers, raw
HTTP bodies, and unrelated remote property definitions.

The plugin response returns project-relative artifacts with types:

- `notion-schema-validation-report`
- `notion-schema-validation-report-md`

## Error Handling

The service catches `NotionApiException` and `HttpRequestException` per data
source, converts them to stable diagnostics, records the entry failure, and
continues. Unexpected programming exceptions are not converted into successful
validation results. Report I/O failure is a runtime failure and must not be
reported as a valid schema result.

When a mapped property is missing and is also the `uniqueField`, both the
property-missing and unique-field-missing diagnostics are emitted because they
prove different contract requirements.

## Security and Boundaries

- Import remains `network: false` and reads no environment variables.
- No Core project references `Bukit.Plugin.Notion`.
- `Bukit.Plugin.Notion` references neither Labs nor Core CLI implementation.
- The token name is allowlisted and the token value is never serialized.
- Reports remain under the currently granted Notion report directories.
- Plugin stdout remains one protocol JSON response; logs remain on stderr.
- No executable is written under `.bukit/`.
- `guide-0.1/` and `scripts-0.1/` are not modified.

## Testing Strategy

Implementation follows red-green-refactor in these layers:

1. HTTP client tests prove the retrieve endpoint, headers, property parsing, and
   API-error behavior.
2. Domain tests with fake clients prove exact match, missing property, type
   mismatch, missing and duplicate title, missing unique field, legacy
   `databaseId`, multiple-entry aggregation, deterministic ordering, token
   handling, exit codes, and JSON/Markdown redaction.
3. Plugin tests prove runtime and static manifest parity, exact three-segment
   dispatch, option validation, artifacts, and JSON protocol responses.
4. CLI integration tests prove the nested command descriptor forwards the full
   command path and options.
5. Existing Notion and plugin tests run before the repository gate.
6. The repository-appropriate gate runs after implementation, followed by a
   final diff and boundary audit.

Default automated verification uses fake HTTP/client behavior and does not need
live Notion credentials. A live workspace check is optional evidence and is not
allowed to replace deterministic tests or the repository gate.

## Non-Goals

- No remote schema diff or extra-property reporting.
- No remote property creation, deletion, rename, or type migration.
- No database or data-source creation.
- No push-state, resume, or idempotency changes.
- No new seed collection support.
- No package version, release tag, or RC closeout changes.
- No automatic invocation from `notion push` in this task.

## Acceptance Criteria

The feature is complete only when:

1. The runtime and static manifests expose exactly `notion schema validate` with
   the approved options.
2. Every valid map entry causes one retrieve-data-source request using its
   effective identifier.
3. Property existence, property type, one-and-only-one title, and remote
   `uniqueField` existence are all validated.
4. All required reports and stable diagnostic codes are produced on success and
   validation failure.
5. Reports and protocol output contain no token or raw API response body.
6. Targeted HTTP, domain, plugin, and CLI tests pass.
7. The repository gate passes.
8. The final diff audit finds no unresolved scope, security, compatibility, or
   backup-directory issue.
