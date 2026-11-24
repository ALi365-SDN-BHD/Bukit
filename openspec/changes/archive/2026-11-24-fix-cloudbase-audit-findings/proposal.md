# Fix CloudBase audit findings

## Why

The CloudBase content source is implemented and archived, but an independent audit found one
privacy-check false positive that can block normal sites: the build check matches the raw envId
anywhere in the output (including file paths), and the deploy check matches it as a plain
substring. Short envIds such as `blog` make ordinary pages fail. The same audit found several
correctness and cleanup issues in the new code. This change fixes them without changing the
public API or the CloudBase user contract.

## What Changes

- **P1 privacy matching precision**: the build and deploy checks detect an envId only inside a
  CloudBase identifier context (gateway host, storage CDN host, or `cloud://` reference). A bare
  envId in prose or a file path no longer fails. Deploy checks every CloudBase source envId, not
  only the first.
- **P1 provider-specific messages**: build and deploy privacy errors say "CloudBase" when the
  matched token is CloudBase, and keep "Notion" for Notion matches.
- **P1 deploy JSON scan**: a JSON file that contains `cloud://` but cannot be parsed is treated
  as a leak, not as clean.
- **P2 body rewriting**: body `cloud://` references are resolved from the parsed Markdown image
  nodes, so fenced code, inline code and raw HTML examples are left untouched.
- **P2 sign mapping**: signed URLs are matched to requested objects by the response `path`, not
  by array position. Signed links must use HTTPS and the default port.
- **P3 alignment/cleanup**: whitelist queries request only needed columns; CloudBase host/path
  constants are shared where the layer allows it; dead `InvalidRequest` kind is removed;
  `pageSize`-style type errors report `BKT-0C01` with the full path; doctor reports a blank
  sample idField as an error; the allow-set is built once per source; docs mention `maxItems`.

## Impact

- Affected specs: `cloudbase-content-source` (privacy requirement only).
- Affected code: `Bukit.Engine.Audit`, `Bukit.Cli`, `Bukit.Content/CloudBase`, `Bukkit.Config`,
  `Bukit.CloudBase` transport, build packaging docs, tests.
- No public API additions. No credential or host format changes.
