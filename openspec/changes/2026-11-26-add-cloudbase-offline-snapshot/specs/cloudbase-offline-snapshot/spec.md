## ADDED Requirements

### Requirement: CloudBase sources can build from an offline snapshot
When a content snapshot is recorded (`--content-snapshot`, alias `--notion-snapshot`), the build
SHALL store each CloudBase query's projected public rows and the media identity of each signed
file object, and commit them only after a successful build. When the build runs offline
(`--content-offline`, alias `--notion-offline`), CloudBase sources SHALL read those rows without a
Publishable Key or network access, and SHALL fail when the snapshot lacks a configured table.

#### Scenario: Offline rebuild
- **WHEN** a snapshot recorded with CloudBase table `articles` is used offline
- **THEN** the build reads the recorded rows and makes no CloudBase request

#### Scenario: Missing table
- **WHEN** the offline snapshot has no rows for a configured CloudBase table
- **THEN** the build and `config check` fail with `BKT-0B11` naming the table

### Requirement: The snapshot holds public data only
The snapshot SHALL store only the mapped columns, `fileFields` and the columns the field policy
publishes, without personal or operator columns or nested keys, and SHALL never store signed links
or their tokens.

#### Scenario: Personal columns
- **WHEN** a recorded row has `user_id` and a nested `owner` key
- **THEN** neither appears in the snapshot

### Requirement: A recorded release is not served for another one
The snapshot SHALL key each query by its resolved filter value, and an offline build whose filter
value differs SHALL fail.

#### Scenario: Another release offline
- **WHEN** a snapshot recorded with `${env:RELEASE_ID}` = `r1` is built offline with `r2`
- **THEN** the build fails with `BKT-0B13`

#### Scenario: Two sources on one table
- **WHEN** two sources read the same table and filter with different field policies
- **THEN** each replays its own recorded rows offline

### Requirement: Offline media comes from the local media cache
Offline, file fields and body images SHALL resolve to the media identity recorded for the object,
so media localization reuses the files the recording build stored; an object without a recorded
identity SHALL become the media placeholder.

#### Scenario: Recorded cover
- **WHEN** a recorded row's cover object was signed while recording
- **THEN** the offline build resolves it to the same local media file without signing

#### Scenario: Media cache missing offline
- **WHEN** the offline build has no cached file for a recorded object
- **THEN** it uses the media placeholder and the output never names the CloudBase gateway
