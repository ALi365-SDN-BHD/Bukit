## MODIFIED Requirements

### Requirement: Signed URL parameters do not affect media identity

The localizer MUST compute a downloaded image's cache identity and stored file name from the HTTP request target with the parameters of a detected signing scheme removed. Parameter names MUST be matched as written in the escaped request target as `Uri` normalizes it (escaped unreserved characters such as `%2D` are restored, as in the request actually sent), case-insensitively and without further percent-decoding (a name that keeps a percent-escape is not matched and is therefore kept), and a scheme's parameters MUST be removed only when that scheme is detected:

- AWS SigV4, detected by `X-Amz-Signature`: remove `X-Amz-Algorithm`, `X-Amz-Credential`, `X-Amz-Date`, `X-Amz-Expires`, `X-Amz-SignedHeaders`, `X-Amz-Signature` and `X-Amz-Security-Token`.
- Google Cloud Storage V4, detected by `X-Goog-Signature`: remove `X-Goog-Algorithm`, `X-Goog-Credential`, `X-Goog-Date`, `X-Goog-Expires`, `X-Goog-SignedHeaders` and `X-Goog-Signature`.
- AWS SigV2, GCS V2 and CloudFront, detected by `Signature` together with at least one of `AWSAccessKeyId`, `GoogleAccessId` or `Key-Pair-Id`: remove `Signature`, `Expires`, `Policy`, `AWSAccessKeyId`, `GoogleAccessId` and `Key-Pair-Id`.
- Notion file proxy, detected by a host equal to `notion.so` or matching `*.notion.so` or `*.notion.site`: remove `expirationTimestamp` and `signature`.
- CloudBase PG storage signed download, detected by a host matching `*.api.tcloudbasegateway.com` or `*.api.intl.tcloudbasegateway.com` together with a path that starts with `/v1/storages/object/sign/`: remove `token`. The host patterns, the path prefix and the parameter name MUST be defined in one central place so they can be corrected after verification on a real environment.

Removal MUST operate on the escaped request target string produced by `GetComponents(HttpRequestUrl, UriEscaped)`, deleting whole `name=value` segments and their separators without decoding, re-encoding or reordering the remaining segments. All other query parameters MUST therefore remain part of the identity in their original order and spelling. The download request MUST still use the original, unmodified URL. For a URL from which no parameter is removed (no signing scheme detected, or a detected Notion or CloudBase URL whose query carries none of the removable parameters), the identity and stored file name MUST be byte-identical to those produced before this change, and the identity version prefix and media index version MUST stay unchanged.

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

#### Scenario: Same CloudBase object re-signed
- **WHEN** two builds reference `https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/posts/a.png` with different `token` values
- **THEN** both resolve to the same local file

#### Scenario: Same parameter on another host
- **WHEN** `https://cdn.example/v1/storages/object/sign/media/a.png?token=x1` and `https://cdn.example/v1/storages/object/sign/media/a.png?token=x2` are localized
- **THEN** they resolve to different local files because the CloudBase scheme is not detected on that host

### Requirement: Refused expired signed URLs are not retried

When an image download receives HTTP 400, 401 or 403 and the requested URL carries a recognized signing scheme whose own expiry time is at or before the current time, the localizer MUST record the failure after that single request instead of retrying up to `content.media.maxRetries`. The expiry MUST be read only for these schemes recognized for media identity: AWS SigV4 (`X-Amz-Date` plus `X-Amz-Expires` seconds), Google Cloud Storage V4 (`X-Goog-Date` plus `X-Goog-Expires` seconds), AWS SigV2, GCS V2 and CloudFront (`Expires`, Unix seconds) and the Notion file proxy (`expirationTimestamp`, Unix milliseconds). The CloudBase PG storage signed download is recognized for media identity, but its expiry MUST NOT be read: its `token` is treated as opaque, so a refused CloudBase signed URL keeps the configured retries. A URL with no recognized scheme or an unreadable expiry, an unexpired signature, a CloudBase signed URL, and every other failure kind MUST keep the configured retries. The failure MUST otherwise be handled like any download failure: the `onFailure` result, failure memory, single reporting and the reuse of an already saved file (including one stored under the earlier signed key) MUST be unchanged.

#### Scenario: Readonly cache with an expired S3 link
- **WHEN** `maxRetries: 3` and an S3 SigV4 URL whose `X-Amz-Date` plus `X-Amz-Expires` lies in the past returns 403
- **THEN** exactly one request is made, the failure is recorded once, and the result follows `onFailure`

#### Scenario: Unexpired signature keeps retries
- **WHEN** `maxRetries: 3` and a signed URL that has not expired returns 403
- **THEN** four requests are made before the failure is recorded

#### Scenario: Saved file wins over an expired link
- **WHEN** the download directory already holds the file for an expired signed URL
- **THEN** the file is reused without any request

#### Scenario: Refused CloudBase signed link keeps retries
- **WHEN** `maxRetries: 3` and a CloudBase PG storage signed URL returns 403
- **THEN** four requests are made before the failure is recorded, and the result follows `onFailure`

## ADDED Requirements

### Requirement: CloudBase file fields keep the localization path closed
For a `cloudbase` source with non-empty `fileFields`, media localization SHALL be the only path by
which a CloudBase signed link can reach output: `content.media.downloadToLocal` MUST NOT be `false`,
every file field MUST be listed in `content.media.fieldKeys`, `content.media.onFailure` MUST be
`placeholder` with an existing placeholder image, file fields MUST be image fields, and
`downloadHosts` or `skipHosts` MUST NOT exclude the environment's CloudBase gateway host
(`{envId}.api.tcloudbasegateway.com` or `{envId}.api.intl.tcloudbasegateway.com`). A failed download
SHALL fall back to the placeholder and SHALL NOT keep the signed link.

#### Scenario: Download failure
- **WHEN** a CloudBase signed link for a file field returns HTTP 403 on every attempt
- **THEN** the field references the placeholder image and the signed link is not written to any output

#### Scenario: Skip rule excludes the gateway host
- **WHEN** `content.media.skipHosts` matches the environment's CloudBase gateway host and `fileFields` is non-empty
- **THEN** configuration validation fails with `CloudBaseMediaLocalizationRequired`
