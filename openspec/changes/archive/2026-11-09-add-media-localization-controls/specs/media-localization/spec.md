## ADDED Requirements

### Requirement: Download failure keeps the original URL when configured

`content.media.onFailure` MUST accept `placeholder` (default) or `keep`, case-insensitive, and the generated JSON Schema MUST accept the same casings; any other value MUST fail configuration validation. When an HTTP(S) image download fails for any reason (HTTP error, rejected content type, size limit, empty body, signature or decode rejection, SSRF block, timeout, network error, or an unsafe cached file), the localizer MUST record the failure and return `defaultImageUrl` under `placeholder`, or the caller's original source text (before HTML decoding) under `keep`. The original URL MUST be used for front-matter image fields and for body `<img src>` and `srcset` alike. When several references share one in-flight download, each MUST receive its own source spelling under `keep`. A URL whose download failed MUST NOT be downloaded or reported again by the same localizer instance (one build); later references MUST receive the `onFailure` result directly and MUST each log a warning. Failures recorded after the document pass (lazily localized bodies) MUST be reported in a later build summary. An empty source and missing `downloadDir`/`urlBase` are not download failures and MUST keep returning `defaultImageUrl`.

#### Scenario: Keep mode on HTTP 404
- **WHEN** `onFailure: keep` and an image URL returns 404 after all retries
- **THEN** the field or `<img>` keeps the original URL, and the failure appears in `Failures` and the build summary

#### Scenario: Default stays placeholder
- **WHEN** `onFailure` is not set and the same download fails
- **THEN** the result is `defaultImageUrl`, as before

#### Scenario: Invalid value
- **WHEN** `onFailure: drop` is configured
- **THEN** configuration validation fails and names `content.media.onFailure`

### Requirement: Host rules select which images are downloaded

`content.media.downloadHosts` and `content.media.skipHosts` MUST accept lists of host patterns. A pattern MUST be an exact host, an exact IPv4 address in canonical dotted-decimal form, or `*.` followed by a host name; `*.example.com` MUST match any subdomain of `example.com` but not `example.com` itself. Any other wildcard form (including a wildcard on an IPv4 address), a non-canonical IPv4 form such as `010.0.0.1`, an IPv6 literal, a scheme, a port or a path MUST fail configuration validation. Matching MUST be case-insensitive on the IDN-normalized host of the original URL. Configuration validation and the localizer MUST apply the same pattern rules; a localizer given an invalid `onFailure` or host pattern MUST fail at construction with the same configuration error (`ConfigInvalidValue`). A URL whose host matches `skipHosts`, or that does not match a non-empty `downloadHosts`, MUST be returned unchanged without any network request and MUST NOT be recorded as a failure. `skipHosts` MUST take precedence over `downloadHosts`. An empty or absent `downloadHosts` MUST mean every host is eligible.

#### Scenario: Only Notion hosts are downloaded
- **WHEN** `downloadHosts: ["*.amazonaws.com"]` and a document contains an S3 image and a `silkroute.cc` image
- **THEN** the S3 image is localized and the `silkroute.cc` image keeps its original URL with no request and no failure

#### Scenario: Skip wins over download
- **WHEN** a host matches both `downloadHosts` and `skipHosts`
- **THEN** the URL is returned unchanged without a request

#### Scenario: IPv4 address pattern
- **WHEN** `skipHosts: ["203.0.113.5"]` and an image URL is `https://203.0.113.5/a.png`
- **THEN** configuration validation passes and the URL is returned unchanged without a request

#### Scenario: Invalid pattern
- **WHEN** `skipHosts: ["a*b.com"]` is configured
- **THEN** configuration validation fails and names `content.media.skipHosts`

### Requirement: Generic binary responses are identified by content signature

When the response Content-Type is `application/octet-stream`, `binary/octet-stream`, or missing, the localizer MUST identify the image type from the response's leading bytes, before the body is stored, using the same signatures as the existing Content-Type check, limited to the allowed raster formats. The identified type MUST then pass the existing signature and decode validation, and the stored file extension MUST follow the identified type. If no allowed type is identified, the download MUST fail with a content-type rejection. Responses with any other non-image Content-Type MUST still be rejected before the body is written.

#### Scenario: Notion file served as octet-stream
- **WHEN** a response has `Content-Type: application/octet-stream` and a valid PNG body
- **THEN** the image is stored with a `.png` extension and the local URL is returned

#### Scenario: Octet-stream that is not an image
- **WHEN** a response has `Content-Type: application/octet-stream` and an HTML body
- **THEN** the download fails after reading only the leading bytes, no file is left behind, and the `onFailure` result is returned

#### Scenario: Other content types stay rejected
- **WHEN** a response has `Content-Type: text/html` and a valid PNG body
- **THEN** the download fails as a content-type rejection
