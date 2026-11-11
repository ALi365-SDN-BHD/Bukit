## ADDED Requirements

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
