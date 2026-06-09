# Tasks
- [x] Task 1: Create artifact-manifest.v1.schema.json
  - [x] Read `docs/schemas/incremental-manifest.v1.schema.json` and `docs/schemas/assets.v1.schema.json` as style references
  - [x] Read `src/Bukit.Engine/BuildReporter.cs` method `WriteArtifactManifest` (lines 202-236) to identify all fields
  - [x] Create `docs/schemas/artifact-manifest.v1.schema.json` with `$schema`, `$id`, `title`, `type`, `required`, `properties`, `additionalProperties`, matching the field structure in `WriteArtifactManifest`

- [x] Task 2: Create release-bundle-checksums.v1.schema.json
  - [x] Read `src/Bukit.Engine/BuildReporter.cs` method `WriteReleaseBundleChecksums` (lines 238-271) to identify all fields
  - [x] Create `docs/schemas/release-bundle-checksums.v1.schema.json` matching the field structure

- [x] Task 3: Create build-manifest-digest.v1.schema.json
  - [x] Read `src/Bukit.Engine/BuildReporter.cs` method `WriteBuildManifestDigest` (lines 273-315) to identify all fields
  - [x] Create `docs/schemas/build-manifest-digest.v1.schema.json` matching the field structure

- [x] Task 4: Verify validation passes
  - [x] Run `bash scripts/build-repro.sh` — all three `validate-artifacts-json.sh` invocations pass
  - [x] Confirm no `ERROR: * declares ... but ... is missing` output
  - [x] Confirm `OK reproducible-build check` passes
  - [x] Also fixed pre-existing `security-report.v1.schema.json` missing `externalPlugins` property and incorrect `policy` enum

# Task Dependencies
- Tasks 1, 2, 3 are independent and can run in parallel
- Task 4 depends on Tasks 1, 2, 3
