# 27 CloudBase Content

`type: cloudbase` reads one table or view in the `public` schema of a Tencent CloudBase PostgreSQL-mode environment as read-only content at build time. P1 supports PG mode only; it does not support the classic mode (data models or document databases).

## Configuration

```yaml
content:
  sources:
    - name: articles
      type: cloudbase
      mode: content
      collection: posts          # Bukit collection name, not the CloudBase table
      cloudbase:
        envId: prod-abc
        region: cn               # cn or intl
        table: articles          # CloudBase (PostgreSQL) table or view
        idField: id              # unique column for paging and source identity
        pageSize: 100
        maxItems: 500
        filter: { field: status, value: published }
        sort: { field: publish_at, direction: desc }
        titleField: title
        slugField: slug
        publishAtField: publish_at
        updatedAtField: updated_at
        bodyField: content
        bodyFormat: markdown
        fieldPolicy: { mode: whitelist, allowed: [title, slug, publish_at, cover, tags] }
        fileFields: [cover]
        storageBucket: media
        signExpiresSeconds: 3600
        cacheMode: off
        maxRps: 5
        maxRetries: 3
```

`table` is the CloudBase table or view; `collection` is the Bukit content collection. `fieldPolicy` `mode` defaults to `whitelist`: `allowed` filters ordinary custom columns only. Explicit mappings (`titleField`, `slugField`, `publishAtField`, `updatedAtField`, `bodyField`) and `fileFields` are published regardless of `allowed`. `all` still drops `user_id`, `owner`, `created_by`, `updated_by` and `_openid`, and `idField` is not published by `all` unless it is explicitly listed in `allowed`.

Character sets: `envId` matches `^[a-z0-9][a-z0-9-]{0,62}$`; `table` and `idField` match `^[A-Za-z_][A-Za-z0-9_]{0,62}$`; `storageBucket` matches `^[a-z0-9][a-z0-9._-]{0,62}$`; object names must not be empty, start with `/`, or contain `//` or `..` segments.

## Credentials

Use a Publishable Key only, from `CLOUDBASE_PUBLISHABLE_KEY` or `--secrets-stdin`:

```bash
export CLOUDBASE_PUBLISHABLE_KEY="<publishable key>"
```

It carries exactly the read-only permissions of the `anon` role (`GRANT SELECT` plus RLS). **Never put a `service_role` API Key in this variable**; `doctor` decodes the JWT `role` and warns with `BKT-0C0A` for `service_role` or an unreadable role. Credential fields written in YAML fail validation.

## Database setup

```sql
-- read-only grant
GRANT SELECT ON TABLE public.articles TO anon;

-- only published rows (ADMINWRITE-mode policy, SELECT only)
ALTER TABLE public.articles ENABLE ROW LEVEL SECURITY;
CREATE POLICY articles_read ON public.articles
  FOR SELECT TO anon USING (status = 'published');

-- readable storage bucket
CREATE POLICY media_read ON storage.objects
  FOR SELECT TO anon USING (bucket_id = 'media');
```

RLS filters silently: when `doctor` sees a reachable table with a total count of 0 it prints a hint, and the build succeeds with zero documents. Check the policies and `GRANT`, then run an equivalent query from the console with the same credential.

## Image and file fields

A `fileFields` value is an object name in `storageBucket` (such as `posts/a.png`) or a `cloud://<envId>.<bucket>/<path>` compatible reference. The build signs batches of up to 500 objects and substitutes the signed links for media localization. In the body only image positions with `cloud://` are rewritten; links to non-images are degraded to plain text, and any other public field whose value contains `cloud://` is dropped with `BKT-0C0B`.

When `fileFields` is non-empty, all five conditions must hold (D8):

1. `content.media.downloadToLocal` is not `false`;
2. every field is listed in `content.media.fieldKeys`;
3. `content.media.onFailure: placeholder` and the placeholder image exists;
4. P1 accepts image fields only;
5. `downloadHosts`/`skipHosts` do not exclude `{envId}.api[.intl].tcloudbasegateway.com` or `{envId}.storage.tcloudbase.com`.

Any rewrite failure (invalid object name, `cloud://` env or bucket mismatch, per-item signing error, wrong scheme/host/path) falls back to `content.media.defaultImageUrl` with `BKT-0C08`; bare object names and `cloud://` never reach the output. `signExpiresSeconds` defaults to 3600 (60–3600).

## P1 limits

- PG-mode tables or views only; no classic mode or data models;
- Markdown bodies only (`bodyFormat: markdown`);
- image fields only, no other file fields;
- no caching (`cacheMode` accepts `off` only);
- `filter` supports a single equality `{ field, value }`, no `op`; `sort` is one field with `asc`/`desc`, and paging always appends `idField` ascending;
- no incremental fingerprints; every build reads the table (bounded by `maxItems`).

## Troubleshooting

| Code | Meaning and handling |
|---|---|
| `BKT-0C01` | Invalid config: check required fields, character sets, ranges, unknown or credential fields, and the `fileFields` preconditions. |
| `BKT-0C02` | Missing credential: set `CLOUDBASE_PUBLISHABLE_KEY` or pass `--secrets-stdin`. |
| `BKT-0C03` | 401/403: the key is invalid, or `GRANT`/RLS denies the read; re-check the grants and policies. |
| `BKT-0C04` | 404: verify `envId`, `region` and `table`; create the table/view or fix the name. |
| `BKT-0C05` | Still rate limited after retries: lower `maxRps` or retry later. |
| `BKT-0C06` | Other HTTP or network failure: check connectivity, proxy and firewall, then retry. |
| `BKT-0C07` | Unexpected response, unparseable `Content-Range`, or size limit: inspect the endpoint (gateway host and region) and reduce `pageSize` or `maxItems`. |
| `BKT-0C08` | File signing failed: check the bucket `SELECT` policy, object names, and that `cloud://` env/bucket match the config. |
| `BKT-0C09` | `downloadHosts`/`skipHosts` exclude the gateway or storage CDN host: add `{envId}.api[.intl].tcloudbasegateway.com` and `{envId}.storage.tcloudbase.com`. |
| `BKT-0C0A` | The credential role is `service_role` or unreadable: replace it with a Publishable Key. |
| `BKT-0C0B` | `cloud://` appeared where it is never rewritten and was degraded or dropped: move it to `fileFields` or a body image position, or remove it. |

Run `bukit doctor` first: it checks the credential role, table reachability, RLS-empty results, mapped fields and storage signing, and never prints the credential.
