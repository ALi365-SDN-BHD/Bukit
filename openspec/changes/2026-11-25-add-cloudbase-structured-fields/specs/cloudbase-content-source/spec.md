## ADDED Requirements

### Requirement: Structured columns keep their JSON structure
A projected column whose value is a JSON object, or an array holding anything other than strings
and nulls, SHALL become a structured field of type `list` whose value is an ordinal, key-sorted map
or a list, with JSON scalar types kept (whole numbers, exact decimals, other finite numbers,
booleans, strings and null). Arrays of only strings and nulls SHALL keep the existing text-list
behaviour, and an empty array, or an object that is empty after hard-excluded keys are removed,
SHALL be absent. Every object and list SHALL count as
one nesting level, with at most 8 levels, and a field's raw JSON SHALL be at most 256 KiB. A field
that exceeds a limit or is otherwise invalid SHALL be dropped and reported as an error-severity
content diagnostic `cloudbase.structured_field_invalid` whose message names the field and key path
but no value, so the collection's `schemaFailMode` ignores it (`off`), logs it (`warn`) or fails the
build (`strict`); a `fileFields` column holding such a value SHALL become the media placeholder
without this diagnostic. Content validation logs SHALL name a CloudBase document only by a hash of
its id. The whitelist SHALL apply to top-level columns, keeping an allowed column whole;
the hard-excluded names SHALL also be removed from the keys of every nested object, in every
`fieldPolicy` mode. A structured field outside `fileFields` whose value or any nested key contains
`cloud://` SHALL be dropped whole and counted in the `CloudBaseUnsupportedFileReference` warning.
The same row data SHALL produce the same structured value regardless of JSON key order.

#### Scenario: Itinerary and months keep their structure
- **WHEN** an allowed column `days` holds `[{"title":"D1","attractions":["a1"],"nights":1}]` and `months` holds `[3,4,5]`
- **THEN** `days` is a list of one map with sorted keys and `nights` is a number, and `months` is the numbers 3, 4 and 5

#### Scenario: Nested personal keys are removed
- **WHEN** an allowed column holds `{"name":"n","items":[{"owner":"o","label":"l"}]}`
- **THEN** the projected value has no `owner` key at any level

#### Scenario: Nested cloud reference drops the field
- **WHEN** an allowed column outside `fileFields` holds `[{"photos":[{"src":"cloud://env.media/a.png"}]}]`
- **THEN** the column is absent from public fields and the load warns with `CloudBaseUnsupportedFileReference`

#### Scenario: Too deep under strict schema mode
- **WHEN** an allowed column nests 9 levels and the collection's `schemaFailMode` is `strict`
- **THEN** the column is dropped and the build fails content validation; under `warn` it is dropped and logged with the row named only by a hash
