## ADDED Requirements

### Requirement: CloudBase sources can build from an offline snapshot
When a content snapshot is recorded, the build SHALL store each CloudBase source's projected
public rows and commit them only after a successful build. When the build runs offline, CloudBase
sources SHALL read those rows without a Publishable Key or network access, and SHALL fail when the
snapshot lacks a configured source.

#### Scenario: Offline rebuild
- **WHEN** a snapshot recorded with CloudBase source `cities` is used offline
- **THEN** the build reads the recorded rows and makes no CloudBase request

#### Scenario: Missing source
- **WHEN** the offline snapshot has no rows for a configured CloudBase source
- **THEN** the build fails naming the source
