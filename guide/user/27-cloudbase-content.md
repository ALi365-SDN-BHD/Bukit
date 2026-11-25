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

In `content` mode a row with a blank `slugField` takes its slug from the slugified title (two rows with the same derived slug fail the build as a route conflict); only when the title is blank too is the row id used, and a blank title falls back to the slug. The build logs one `event=cloudbase.missing_title_or_slug` warning per load that counts these rows. A source may carry only the section that matches its `type`: for example a leftover `cloudbase:` block under `type: notion` fails `config check`.

`table` is the CloudBase table or view; `collection` is the Bukit content collection. `fieldPolicy` `mode` defaults to `whitelist`: `allowed` filters ordinary custom columns only. Explicit mappings (`titleField`, `slugField`, `publishAtField`, `updatedAtField`, `bodyField`) and `fileFields` are published regardless of `allowed`. `all` still drops `user_id`, `owner`, `created_by`, `updated_by` and `_openid`, and `idField` is not published by `all` unless it is explicitly listed in `allowed`.

Character sets: `envId` matches `^[a-z0-9][a-z0-9-]{0,62}$`; `table` and `idField` match `^[A-Za-z_][A-Za-z0-9_]{0,62}$`; `storageBucket` matches `^[a-z0-9][a-z0-9._-]{0,62}$`; object names must not be empty, start with `/`, or contain `//` or `..` segments.

## Structured fields

A jsonb (or json) column holding an object, or an array holding anything other than strings, is kept with its structure, like nested front matter in Markdown:

```yaml
fieldPolicy: { mode: whitelist, allowed: [days, months, contact] }
```

```scriban
{{ for day in page.fields.days.value }}<h3>{{ day.title }}</h3>{{ for a in day.attractions }}{{ a }}{{ end }}{{ end }}
{{ for m in page.fields.months.value }}{{ if m == 4 }}April{{ end }}{{ end }}
{{ page.fields.contact.value.phone }}
```

- Object keys are sorted by ordinal comparison, so the same row always builds the same output. Every object and every array counts as one nesting level, up to 8 levels, and one field's raw JSON may be at most 256 KiB.
- **Types are kept**: `[3, 4, 5]` stays numbers (whole numbers are 64-bit), and booleans and nulls stay booleans and nulls. Compare numbers with `==` as above: `array.contains 4` is false for a CloudBase `[3, 4, 5]`, because Scriban reads the literal `4` as a 32-bit number and that filter needs the exact same type. Markdown front matter list items are strings, so the same Markdown `months: [3, 4, 5]` needs `m == "4"` (or `array.contains "4"`); one template does not serve both sources unchanged. An array of only strings (and nulls) keeps the plain-text list behaviour: blank items and nulls are dropped. An empty array or object is treated as absent.
- **Privacy**: `allowed` names top-level columns, and an allowed column is published whole, nested content included. Do not put personal data inside an allowed jsonb column. The always-dropped names (`user_id`, `owner`, `created_by`, `updated_by`, `_openid`) are also removed from the keys of every nested object. The build privacy check only catches CloudBase identifiers (envId, `cloud://`, gateway and storage hosts, signed links); it does not detect personal data.
- `cloud://` anywhere inside a structured column that is not in `fileFields` (in a value or in a key) drops the whole column with `BKT-0C0B`.
- Upgrading: with `fieldPolicy.mode: all`, object columns and non-text array columns that earlier builds dropped now appear as fields; if `content.modelSchema.rejectUnknownRawKeys` is on, declare them in the schema or switch to `whitelist`.
- A column that is too deep, too large or otherwise invalid is dropped and reported as the content diagnostic `cloudbase.structured_field_invalid`. Its message names the column and key path, never the value, and the log shows the row only as a hash; key names are data too, so do not key objects by personal identifiers. The collection's `schemaFailMode` decides what happens: `off` ignores it, `warn` (the default) logs it, and `strict` fails the build.

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

A `fileFields` value is an object name in `storageBucket` (such as `posts/a.png`) or a `cloud://<envId>.<bucket>/<path>` compatible reference. Any other value (an array, an object or a number) is never signed: it becomes the media placeholder and is counted in `BKT-0C0B`. The build signs batches of up to 500 objects and substitutes the signed links for media localization. In the body only image positions with `cloud://` are rewritten; links to non-images are reduced to their text (autolinks are removed), and any other public field whose value contains `cloud://` is dropped with `BKT-0C0B`. `mode: data` sources rewrite `fileFields` the same way, so data rows such as gallery photos or advisor portraits get local files too; data documents have no body, and any other field containing `cloud://` is dropped. A local file is named after the object, not the signed link, so it keeps its name from build to build. To serve smaller sizes, use `theme.images` (see [04 site.yaml Config](04-site-yaml-config.md)) instead of fixed file names such as `-640.webp`.

When `fileFields` is non-empty, all five conditions must hold (D8):

1. `content.media.downloadToLocal` is not `false`;
2. every field is listed in `content.media.fieldKeys`;
3. `content.media.onFailure: placeholder` and the placeholder image exists;
4. P1 accepts image fields only;
5. `downloadHosts`/`skipHosts` do not exclude `{envId}.api[.intl].tcloudbasegateway.com` or `{envId}.storage.tcloudbase.com`.

Any rewrite failure (invalid object name, `cloud://` env or bucket mismatch, per-item signing error, wrong scheme/host/path) falls back to `content.media.defaultImageUrl` with `BKT-0C08`; in `mode: content`, bare object names and `cloud://` never reach the rewritten content output. `signExpiresSeconds` defaults to 3600 (60–3600).

## P1 limits

- PG-mode tables or views only; no classic mode or data models;
- Markdown bodies only (`bodyFormat: markdown`);
- image fields only, no other file fields;
- no caching (`cacheMode` accepts `off` only);
- `filter` supports a single equality `{ field, value }`, no `op`; `sort` is one field with `asc`/`desc`, and paging always appends `idField` ascending;
- no incremental fingerprints; every build reads the table (bounded by `maxItems`). The build
  keeps every row and body in memory, so set `maxItems` to a practical bound for large tables
  (a few thousand rows is fine; very large `text` bodies multiply memory use).

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
