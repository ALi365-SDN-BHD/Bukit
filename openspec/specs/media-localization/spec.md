# media-localization Specification

## Purpose
Control which remote images Bukit downloads into the site, what a failed download
leaves in the output, and how images served without a usable content type are
identified, so expiring source URLs can be localized without placeholder images
replacing content after a transient failure.

## Requirements

### Requirement: Download failure keeps the original URL when configured

`content.media.onFailure` MUST accept `placeholder` (default) or `keep`, case-insensitive, and the generated JSON Schema MUST accept the same casings; any other value MUST fail configuration validation. When an HTTP(S) image download fails for any reason (HTTP error, rejected content type, size limit, empty body, signature or decode rejection, SSRF block, timeout, network error, or an unsafe cached file), the localizer MUST record the failure and return `defaultImageUrl` under `placeholder`, or the caller's original source text (before HTML decoding) under `keep`. The original URL MUST be used for front-matter image fields and for body `<img src>` and `srcset` alike. When several references share one in-flight download, each MUST receive its own source spelling under `keep`. The localizer MUST consult its successful-download cache and index before its failure memory, so a reference whose identity was already localized in the build receives the local URL. The failure memory MUST be keyed by the full HTTP request target (scheme, host, path and query, excluding the fragment and host case). A request target whose download failed MUST NOT be downloaded or reported again by the same localizer instance (one build); later references with the same request target MUST receive the `onFailure` result directly and MUST each log a warning. When a shared download fails with HTTP 400, 401 or 403 and a caller's request target differs from the one that was requested, such retries MUST be serialized per shared identity: at most one retry attempt for an identity MAY be in flight, and each waiting caller, when its turn comes, MUST first re-check the successful-download cache, then the failure memory for its own request target, and only when both miss make exactly one request attempt with its own URL, bounded by the configured timeout, without the configured retry loop and outside the shared in-flight download; on success the result MUST be stored under the shared identity, and on failure the caller's request target MUST be recorded in the failure memory and reported once. Waiting for and performing a retry MUST honor the caller's cancellation and the localizer's lifetime; a caller cancelled or released by disposal MUST NOT record a failure for its request target. The added delay is bounded by one timeout per distinct request target that actually reaches a retry attempt. When a shared download fails with any other kind, every caller whose request target differs from the requested one MUST have its own request target recorded in the failure memory and reported once, without an additional request. A download failure MUST be added to `Failures` and the build summary only by the caller that first records its request target in the failure memory, so concurrent callers never report the same request target twice; this does not change the per-reference warning for later references, and failures that are not download failures (such as an unsafe cached file that cannot be removed) keep their existing reporting. Failures recorded after the document pass (lazily localized bodies) MUST be reported in a later build summary. An empty source and missing `downloadDir`/`urlBase` are not download failures and MUST keep returning `defaultImageUrl`.

#### Scenario: Keep mode on HTTP 404
- **WHEN** `onFailure: keep` and an image URL returns 404 after all retries
- **THEN** the field or `<img>` keeps the original URL, and the failure appears in `Failures` and the build summary

#### Scenario: Default stays placeholder
- **WHEN** `onFailure` is not set and the same download fails
- **THEN** the result is `defaultImageUrl`, as before

#### Scenario: Invalid value
- **WHEN** `onFailure: drop` is configured
- **THEN** configuration validation fails and names `content.media.onFailure`

#### Scenario: Expired signature first, fresh signature later
- **WHEN** in one build an expired signed URL of an object is processed first and fails with HTTP 403, and a reference with a fresh signature for the same object is processed afterwards
- **THEN** the fresh reference is requested with its own URL and localized, only the expired URL is reported as a failure, and later references to either URL receive the local file

#### Scenario: Fresh signature first, expired signature later
- **WHEN** a fresh signed URL of an object is localized first and an expired signed URL of the same object is processed afterwards
- **THEN** the expired reference receives the local file without a request and no failure is recorded

#### Scenario: Not-found object is not retried per signature
- **WHEN** several references with different signatures for the same object join a shared download that fails with HTTP 404
- **THEN** no additional requests are made, each reference receives the `onFailure` result, and each distinct request target is reported once

#### Scenario: Same expired URL waiting twice
- **WHEN** a shared download fails with HTTP 403 and two waiting callers carry the same expired request target, different from the one requested
- **THEN** exactly one retry request is made for that target, its failure is reported once, and the second caller receives the `onFailure` result without a request

#### Scenario: Later reference to a failed joiner URL
- **WHEN** a joiner's exact signed URL failed through a shared HTTP 404 download and the same URL is referenced again in the build
- **THEN** no request is made and no second failure is reported for it

#### Scenario: Several fresh signatures after an expired one
- **WHEN** a shared download of an expired signature fails with HTTP 403 and several callers with different fresh signatures wait on it
- **THEN** exactly one retry request is made, it localizes the object, and the other fresh callers receive the local file from the success cache without further requests

#### Scenario: Spelling variants share one failed request
- **WHEN** `https://IMG.example/a.png#hero` joins a failing download of `https://img.example/a.png`
- **THEN** exactly one request is made and each caller receives its own spelling under `keep`

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

### Requirement: Media output validation computes the effective media configuration once

Media output validation MUST compute the effective media configuration once per variant and reuse it for every rewritten URL, without filesystem access per link. Validation results, collected references, fallback handling and exceptions MUST be unchanged.

#### Scenario: Many links validated
- **WHEN** rendered HTML with many non-media links is validated
- **THEN** the effective media configuration is computed once and validation reports the same missing outputs as before

### Requirement: Signed URL parameters do not affect media identity

The localizer MUST compute a downloaded image's cache identity and stored file name from the HTTP request target with the parameters of a detected signing scheme removed. Parameter names MUST be matched as written in the escaped request target as `Uri` normalizes it (escaped unreserved characters such as `%2D` are restored, as in the request actually sent), case-insensitively and without further percent-decoding (a name that keeps a percent-escape is not matched and is therefore kept), and a scheme's parameters MUST be removed only when that scheme is detected:

- AWS SigV4, detected by `X-Amz-Signature`: remove `X-Amz-Algorithm`, `X-Amz-Credential`, `X-Amz-Date`, `X-Amz-Expires`, `X-Amz-SignedHeaders`, `X-Amz-Signature` and `X-Amz-Security-Token`.
- Google Cloud Storage V4, detected by `X-Goog-Signature`: remove `X-Goog-Algorithm`, `X-Goog-Credential`, `X-Goog-Date`, `X-Goog-Expires`, `X-Goog-SignedHeaders` and `X-Goog-Signature`.
- AWS SigV2, GCS V2 and CloudFront, detected by `Signature` together with at least one of `AWSAccessKeyId`, `GoogleAccessId` or `Key-Pair-Id`: remove `Signature`, `Expires`, `Policy`, `AWSAccessKeyId`, `GoogleAccessId` and `Key-Pair-Id`.
- Notion file proxy, detected by a host equal to `notion.so` or matching `*.notion.so` or `*.notion.site`: remove `expirationTimestamp` and `signature`.

Removal MUST operate on the escaped request target string produced by `GetComponents(HttpRequestUrl, UriEscaped)`, deleting whole `name=value` segments and their separators without decoding, re-encoding or reordering the remaining segments. All other query parameters MUST therefore remain part of the identity in their original order and spelling. The download request MUST still use the original, unmodified URL. For a URL from which no parameter is removed (no signing scheme detected, or a detected Notion host whose query carries none of the removable parameters), the identity and stored file name MUST be byte-identical to those produced before this change, and the identity version prefix and media index version MUST stay unchanged.

#### Scenario: Same S3 object re-signed
- **WHEN** two builds reference `https://prod-files-secure.s3.us-west-2.amazonaws.com/ws/file-id/a.png` with different `X-Amz-Date` and `X-Amz-Signature` values
- **THEN** the second build reuses the cached file without a network request and the page references the same local file

#### Scenario: Notion file proxy re-signed
- **WHEN** two `https://file.notion.so/f/f/ws/file-id/a.png` URLs differ only in `expirationTimestamp` and `signature`
- **THEN** both resolve to the same local file

#### Scenario: Object version stays distinct
- **WHEN** two signed S3 URLs share a path but have different `versionId` values
- **THEN** they resolve to different local files

#### Scenario: Lone generic parameter on another host is kept
- **WHEN** `https://cdn.example/a.jpg?signature=x1` and `https://cdn.example/a.jpg?signature=x2` are localized
- **THEN** they resolve to different local files because no signing scheme is detected

#### Scenario: Partial AWS-looking parameters are kept
- **WHEN** `https://img.example/a.jpg?X-Amz-Expires=100` and `https://img.example/a.jpg?X-Amz-Expires=200` are localized without `X-Amz-Signature`
- **THEN** they resolve to different local files

#### Scenario: Business parameters stay distinct
- **WHEN** `https://img.example/a.jpg?w=100` and `https://img.example/a.jpg?w=200` are localized
- **THEN** they resolve to different local files

#### Scenario: File from an earlier version is reused for its signed URL
- **WHEN** the download directory holds a file stored under the earlier key for a signed URL (the full request target, signature included) and that same signed URL is localized
- **THEN** the file is reused without a request and remembered under the new key, even when the signed URL has expired

#### Scenario: Original URL is requested
- **WHEN** a signed URL is not yet cached
- **THEN** the HTTP request carries every original query parameter, including the signature

#### Scenario: Unsigned images survive the upgrade
- **WHEN** a cache populated before this change holds an image whose URL has no signing scheme
- **THEN** the upgraded build reuses the same file name without a network request

#### Scenario: Signed images move once
- **WHEN** a cache populated before this change holds an image fetched through a signed URL
- **THEN** the upgraded build downloads it once under its stable name and later builds reuse it

### Requirement: Refused expired signed URLs are not retried

When an image download receives HTTP 400, 401 or 403 and the requested URL carries a recognized signing scheme whose own expiry time is at or before the current time, the localizer MUST record the failure after that single request instead of retrying up to `content.media.maxRetries`. The expiry MUST be read only for the schemes recognized for media identity: AWS SigV4 (`X-Amz-Date` plus `X-Amz-Expires` seconds), Google Cloud Storage V4 (`X-Goog-Date` plus `X-Goog-Expires` seconds), AWS SigV2, GCS V2 and CloudFront (`Expires`, Unix seconds) and the Notion file proxy (`expirationTimestamp`, Unix milliseconds). A URL with no recognized scheme or an unreadable expiry, an unexpired signature, and every other failure kind MUST keep the configured retries. The failure MUST otherwise be handled like any download failure: the `onFailure` result, failure memory, single reporting and the reuse of an already saved file (including one stored under the earlier signed key) MUST be unchanged.

#### Scenario: Readonly cache with an expired S3 link
- **WHEN** `maxRetries: 3` and an S3 SigV4 URL whose `X-Amz-Date` plus `X-Amz-Expires` lies in the past returns 403
- **THEN** exactly one request is made, the failure is recorded once, and the result follows `onFailure`

#### Scenario: Unexpired signature keeps retries
- **WHEN** `maxRetries: 3` and a signed URL that has not expired returns 403
- **THEN** four requests are made before the failure is recorded

#### Scenario: Saved file wins over an expired link
- **WHEN** the download directory already holds the file for an expired signed URL
- **THEN** the file is reused without any request
