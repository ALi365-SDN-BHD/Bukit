# Design

## Privacy matching

Build-time (`PublicOutputPrivacyCheck`):

- Sensitive tokens for a CloudBase source are its envId-bearing identifiers only:
  `{envId}.api.tcloudbasegateway.com`, `{envId}.api.intl.tcloudbasegateway.com`,
  `{envId}.storage.tcloudbase.com`, and `cloud://{envId}.` references (the `cloud://` token is
  kept because it always carries the envId next to it).
- The raw envId is not a token and output file paths are not scanned for it.
- The signed path plus `token` combination and the storage CDN host stay as before.

Deploy-time (`DeploymentPrivacyValidator`):

- The deploy command passes every CloudBase envId from site.yaml.
- An envId matches only inside the same host/`cloud://` contexts. Standalone substring matching
  is removed, so `prod-1` no longer matches `prod-10` or prose.
- Signed links (gateway sign path plus `token`, storage CDN plus `token`) remain detected
  anywhere in text.

Messages:

- The build and deploy checks scan with two token sets: Notion and CloudBase. A CloudBase match
  reports "internal CloudBase provenance"; a Notion match keeps the existing wording. Redaction
  of CloudBase identifiers uses a neutral `[redacted]` marker.

## Body resolution

Today `CloudBaseFileResolver` rewrites raw Markdown with regular expressions, which also rewrites
code. Replace that with:

1. Parse the Markdown with the same Markdig pipeline.
2. Walk `LinkInline` images (`IsImage`) and collect `cloud://` URLs.
3. Sign them; build a map from original URL to signed URL or placeholder.
4. Keep the original Markdown and the map on the body store.
5. At render time, walk the parsed document and set each image/link URL from the map. Non-image
   `cloud://` links lose their URL (rendered as empty href) and are counted as unsupported;
   images with a failed reference use the placeholder.
   Reference definitions are handled by the same walk because Markdig resolves them into
   `LinkInline`.

This keeps code fences, inline code and raw HTML untouched, and matches D17's "image positions
only".

## Sign mapping and URL checks

- Sign response items are matched by the `path` field; a missing or unknown path falls back to
  the request position only when the response length matches and the path is absent. A mismatch
  marks the item failed (placeholder).
- Accept a signed URL only when scheme is HTTPS, the port is the default (443), the host equals
  the gateway host, and the path starts with `/v1/storages/object/sign/{bucket}/`.

## Query columns

For `whitelist` mode, compute `select` from: idField, mapped fields (title/slug/publish/updated/
body), fileFields and the allowed list. For `all`, leave `select` empty (all columns), because
the hard-excluded columns are not known to PostgREST's select syntax and are dropped by the
projector.

## Constants

Keep transport constants in `CloudBaseEndpoints`. `Bukit.Content` and `Bukit.Cli` already
reference `Bukit.CloudBase`; `Bukit.Engine.Audit` gets one friend entry. `Bukit.Config` keeps
literal hosts (it cannot reference the transport without inverting the layer); a Config test
asserts they equal `CloudBaseEndpoints`. No `Config -> CloudBase` project reference.

## Error mapping / validation

- `InvalidRequest` is removed; transport failures use the existing kinds.
- Read-time CloudBase type errors are raised by `ConfigStrictFieldValidator` with the full
  `content.sources[i].cloudbase.<field>` path and `CloudBaseConfigInvalid`.
- Doctor treats a blank id value in the sample row as `BKT-0C07`.
- The field projector receives a precomputed allow-set.
