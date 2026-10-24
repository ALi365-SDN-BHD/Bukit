## ADDED Requirements

### Requirement: Approved image backend packages

Active production and tests SHALL use SkiaSharp 4.153.1 with matching Linux
NoDependencies native assets and TiffLibrary 0.6.65. They MUST NOT depend on
ImageSharp or an ImageSharp adapter. Existing public APIs and configuration SHALL
remain compatible with responsive-next-gen-images.

#### Scenario: Build with the replacement packages
- **WHEN** Content, Engine, or WeChat is built
- **THEN** the dependency graph uses the approved packages and matching native assets

### Requirement: Complete bounded validation

Validation MUST check the existing signature and declared MIME, positive dimensions,
at most 256 frames/pages, and at most 100,000,000 total decoded pixels before
pixel allocation. Skia SHALL fully decode every JPEG/PNG/GIF/WebP/BMP frame with
a Success result. TIFF SHALL preflight every distinct IFD and sum its own page
dimensions including padded tile extents, then fully decode every page. TIFF IFD
tables, fields, and declared strip/tile/legacy-JPEG byte ranges MUST fit the stream
before decoder allocation; array preflight reads MUST be bounded.
Cancellation and infrastructure errors
MUST propagate; malformed, unsupported, cyclic, or over-budget content MUST fail.

#### Scenario: A later frame or TIFF page has truncated pixels
- **WHEN** an earlier frame/page is valid but a later payload cannot fully decode
- **THEN** validation rejects the entire image

#### Scenario: TIFF cycle, excess pages, or excess total pixels
- **WHEN** the IFD chain repeats an offset or exceeds either resource limit
- **THEN** validation rejects it before full pixel decoding

#### Scenario: Unsupported format
- **WHEN** content is BigTIFF, AVIF, or ICO
- **THEN** validation fails closed without expanding the signature contract

#### Scenario: Tiny TIFF declares oversized raw counts or padded tiles
- **WHEN** a field or pixel range exceeds the file, paired counts disagree, or padded tile geometry exceeds the pixel budget
- **THEN** validation rejects it before decoder creation or raw-buffer allocation

#### Scenario: APNG frames are not exposed by the pinned decoder
- **WHEN** PNG declares animation in its acTL chunk
- **THEN** validation and built-in conversion fail closed instead of publishing
  a flattened default image; supported-under-limit WeChat passthrough keeps the original bytes

### Requirement: Engine output and freshness compatibility

Engine SHALL fully decode bounded JPEG/PNG input, preserve alpha and aspect ratio,
avoid upscaling, validate published output, and clean failed temporary output.
Built-in conversion SHALL preserve display orientation including PNG eXIf, normalize profiled
colors to sRGB, and retain the color space through resizing and encoding.
Responsive candidate widths MUST use displayed image dimensions.
WebP freshness MUST include skiasharp-webp and assembly version; fallback resizing
MUST identify skiasharp. Built-in identities MUST include the repaired orientation
semantics so older owned PNG output is regenerated. External resizing MUST apply
`-auto-orient` before `-resize` and invalidate the old raw-orientation tool identity.
External-tool selection and output ownership SHALL retain their existing behavior.

#### Scenario: PNG has a rotated or reflected eXIf orientation
- **WHEN** Engine or WeChat converts a PNG carrying a valid IFD0 scalar SHORT orientation
- **THEN** displayed corners and dimensions are preserved for either TIFF byte order,
  and Engine planning/resizing uses displayed width without upscaling

#### Scenario: PNG orientation metadata is malformed
- **WHEN** pre-IDAT eXIf chunks repeat, CRC fails, or required TIFF offsets/counts/types/values are invalid
- **THEN** conversion fails closed without allocations sized by declared metadata counts or partial published output

#### Scenario: Previously owned output has the old encoder identity
- **WHEN** the next build evaluates that output's freshness
- **THEN** it rebuilds using the new encoder while preserving user-owned files

### Requirement: WeChat normalization compatibility

WeChat SHALL retain supported-under-limit byte passthrough, alpha-preserving PNG
preference, PNG levels 6/7, JPEG qualities 85/75/65/55/30, existing byte limits and
failure logging. Conversion MUST reject incomplete decoded pixels.
Conversion SHALL preserve display orientation including PNG eXIf and normalize profiled colors to sRGB.
JPEG fallback SHALL receive unassociated RGB channels, including after orientation.
Multi-frame GIF/WebP conversion MUST fail closed instead of silently flattening.

#### Scenario: GIF or WebP contains multiple frames
- **WHEN** WeChat normalization requires conversion of a multi-frame source
- **THEN** it returns failure rather than a static PNG or JPEG

#### Scenario: PNG exceeds the upload limit
- **WHEN** PNG compression remains too large
- **THEN** normalization tries the existing JPEG quality sequence and returns only
  output satisfying the inclusive or strict byte boundary requested by the caller
