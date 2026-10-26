# Replace ImageSharp with SkiaSharp and TiffLibrary

## Authorization and purpose

The user approved this Core dependency migration and the previously discussed
design. Replace ImageSharp completely in active production and test code with
SkiaSharp 4.153.1 and TiffLibrary 0.6.65, retaining existing image contracts.

## Scope

Central package pins, Content validation, Engine encoding/resizing and freshness,
WeChat conversion, affected fixtures/tests, and the two current image guides.
Content and WeChat include Linux native assets; SkiaSharp supplies macOS/Windows
assets. Engine consumes Content transitively. Existing user work and historical
reference areas remain outside this change.

## Acceptance

The scheduled Content image, Engine image/media, and WeChat specialties must pass
under the default warning policy. Controller-owned Native AOT publication and
codec execution, then the scheduled independent review, complete acceptance.
The responsive-next-gen-images contract remains applicable without modification.
