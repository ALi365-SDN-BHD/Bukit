# Image backend migration

## Backends

SkiaSharp 4.153.1 decodes JPEG/PNG/GIF/WebP/BMP and encodes JPEG/PNG/WebP.
TiffLibrary 0.6.65 validates standard TIFF only; there is no TIFF-to-Skia bridge.
The existing signature validator stays authoritative, excluding BigTIFF. AVIF
and ICO remain fail-closed. No public API or configuration is added.

## Validation and resource bounds

Signature checking precedes codec work. Skia must report the declared format,
positive dimensions, at most 256 frames, and at most 100,000,000 decoded pixels
before pixel allocation. Each frame uses independent SKCodecOptions (no cached
prior frame) and must return Success, never IncompleteInput. TIFF walks all IFDs
with visited-offset detection and page-count limits. Before decoder creation,
the IFD table and every field must physically fit the stream, paired strip/tile
and legacy JPEG ranges are checked in fixed-size field-reader chunks, and page
dimensions (including padded edge tiles) count toward the total pixel budget.
Full RGBA32 decoding follows preflight. Cancellation and infrastructure failures propagate.

The pinned Skia PNG decoder reports only the default image of a valid two-frame
APNG. Although SkiaSharp offers animated WebP encoding, it cannot recover these
missing source frames. PNG chunk traversal detects acTL and fails closed for
validation and built-in conversion; WeChat's existing under-limit passthrough
still preserves the original bytes. This limitation requires a decoder upgrade
before animated PNG conversion can be enabled safely.

## Conversion and compatibility

Engine JPEG/PNG inputs are fully decoded within the same budgets. A small shared
ImageOptimizer encoding method resizes without upscaling, preserves aspect ratio
and alpha, applies the encoded display orientation, normalizes into explicit sRGB,
retains that color space through resizing/encoding, validates output, and removes
failed output. Candidate planning uses displayed width. Existing external-tool
selection remains unchanged. External resizing applies `-auto-orient` before
`-resize` and uses `<tool>-auto-orient-v1` freshness. Built-in freshness uses
`skiasharp-webp-orientation-v2-<assembly version>` and `skiasharp-orientation-v2`
for fallback resizing, invalidating older owned output with raw PNG orientation.
Ownership and progressive HTML rules remain those of responsive-next-gen-images.

WeChat retains supported-under-limit byte passthrough, PNG preference/compression
levels 6 and 7, JPEG qualities 85/75/65/55/30, byte limits and failure logging.
Conversion requires complete pixel decoding and rejects multi-frame GIF/WebP
instead of flattening animation. Display orientation and sRGB normalization use
the same policy as Engine. Unpremultiplied channels preserve prior JPEG fallback
colors; any orientation rendering is converted back to unpremultiplied pixels,
while PNG output retains alpha.
PNG eXIf is read separately because the pinned codec ignores it. A bounded
pre-IDAT scan allows one eXIf chunk, verifies its full CRC with a fixed buffer,
and reads only IFD0's scalar SHORT orientation in either TIFF byte order.
Offsets/counts must fit the chunk; no untrusted length/count sizes an allocation.
Engine planning and encoding share this effective-origin calculation. WeChat
keeps the small parser local to its independent assembly. Malformed orientation
metadata fails conversion without publishing partial output; byte passthrough
remains unchanged. Existing native matrices handle rotation/reflection and retain
sRGB/alpha behavior for JPEG, PNG and WebP.

## Evidence

The initial regression expects skiasharp-webp freshness and fails against the
previous implementation. Real encoded GIF/BMP fixtures replace encoder-only test
dependencies; TIFF fixtures exercise both byte orders and multiple pages.
Native AOT compatibility requires the separately scheduled real execution proof.
