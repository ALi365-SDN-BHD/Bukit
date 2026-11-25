## ADDED Requirements

### Requirement: Publish and GC settings are adopted only with measured evidence

Every Native AOT publish property and GC setting added to `Bukit.Cli.csproj` by this change MUST be backed by a recorded measurement on a real site build showing an improvement of at least 10% in its target metric (binary size for publish properties, peak resident memory for GC settings) and a total full-build time regression of no more than 5%. A candidate setting that does not meet both conditions MUST NOT be added.

#### Scenario: Setting meets the thresholds
- **WHEN** a candidate GC setting lowers peak resident memory of the reference full build by 10% or more and total build time grows by 5% or less
- **THEN** the setting is added to the project file and its measurement is recorded in the developer documentation

#### Scenario: Setting misses a threshold
- **WHEN** a candidate setting improves its target metric by less than 10%, or slows the reference full build by more than 5%
- **THEN** the setting is not added and the measurement is recorded as the reason

### Requirement: Runtime profile changes do not change build output

A build run with the adopted publish properties and GC settings MUST produce identical published file paths and bytes for the same inputs. Internal reports MUST match after normalizing only the documented run timestamps, durations, and inventory digests and sizes derived from those fields. Derived values MUST be verified against their original inputs before normalization.

#### Scenario: Output compared before and after
- **WHEN** the reference site is built with the previous binary and with the tuned binary from the same inputs
- **THEN** each published file has the same relative path and bytes, and all normalized reports retain the same diagnostics, counts, and other contract fields

### Requirement: Release packages stay reproducible

Packaging the same commit twice with `scripts/build/package-native-aot.sh` MUST still produce identical binaries after the publish properties are changed.

#### Scenario: Repeated packaging
- **WHEN** the same commit is packaged twice for the same runtime identifier
- **THEN** the two `bukit` binaries are identical

### Requirement: Diagnostic messages are preserved

The publish profile MUST NOT enable a setting that replaces framework exception messages with resource keys or removes stack trace data.

#### Scenario: Framework exception surfaced to the user
- **WHEN** a command reports an error whose message comes from a framework exception
- **THEN** the message text is the same as before this change

### Requirement: GC settings remain overridable

A GC setting fixed in the project file MUST remain overridable at run time through the corresponding `DOTNET_` environment variable.

#### Scenario: Operator overrides a setting
- **WHEN** an operator sets the environment variable for a GC setting that the binary fixes by default
- **THEN** the run uses the operator's value
