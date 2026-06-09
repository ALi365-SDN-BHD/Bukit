# Fix Missing Build Artifact Schemas Spec

## Why
`scripts/validate-artifacts-json.sh` validates every `.bukit/*.json` artifact produced by a build against its corresponding JSON Schema in `docs/schemas/`. The build now generates three additional artifact files — `artifact-manifest.json`, `release-bundle-checksums.json`, and `build-manifest-digest.json` — whose schema files are missing, causing CI to fail with exit code 1.

## What Changes
- Add `docs/schemas/artifact-manifest.v1.schema.json` — schema for the artifact manifest that enumerates all `.bukit/` report files with their SHA-256 hashes
- Add `docs/schemas/release-bundle-checksums.v1.schema.json` — schema for the release bundle checksums that enumerate all public output files with their SHA-256 hashes
- Add `docs/schemas/build-manifest-digest.v1.schema.json` — schema for the build manifest digest that provides a signed summary of the other .bukit reports

## Impact
- Affected specs: none (new schema files only; no code changes)
- Affected code: `docs/schemas/` (three new files)

## ADDED Requirements

### Requirement: Artifact Manifest Schema
The system SHALL provide a valid JSON Schema document at `docs/schemas/artifact-manifest.v1.schema.json` that validates `artifact-manifest.json` output.

#### Scenario: Build produces valid artifact-manifest.json
- **WHEN** a build completes and writes `.bukit/artifact-manifest.json`
- **THEN** the file validates successfully against `artifact-manifest.v1.schema.json`

### Requirement: Release Bundle Checksums Schema
The system SHALL provide a valid JSON Schema document at `docs/schemas/release-bundle-checksums.v1.schema.json` that validates `release-bundle-checksums.json` output.

#### Scenario: Build produces valid release-bundle-checksums.json
- **WHEN** a build completes and writes `.bukit/release-bundle-checksums.json`
- **THEN** the file validates successfully against `release-bundle-checksums.v1.schema.json`

### Requirement: Build Manifest Digest Schema
The system SHALL provide a valid JSON Schema document at `docs/schemas/build-manifest-digest.v1.schema.json` that validates `build-manifest-digest.json` output.

#### Scenario: Build produces valid build-manifest-digest.json
- **WHEN** a build completes and writes `.bukit/build-manifest-digest.json`
- **THEN** the file validates successfully against `build-manifest-digest.v1.schema.json`
