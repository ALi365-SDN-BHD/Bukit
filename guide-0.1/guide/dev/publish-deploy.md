# Publish and Deploy

Two layers: 1) Publish the bukit CLI (optional AOT); 2) Use bukit to build and deploy static sites.

Related: [CLI](./cli.md), [AOT](./aot.md), [i18n & SEO](./i18n-seo.md), [Webhook](./webhook.md)

## Site Artifacts

`bukit build` output directory priority:
1. CLI `--output <dir>`
2. `build.output` in `site.yaml`
3. Default `dist`

## CLI Artifacts

**AOT** (Linux x64):
```bash
dotnet publish src/Bukit.Cli -c Release -r linux-x64 -o out/bukit /p:PublishAot=true
```

**Non-AOT**:
```bash
dotnet publish src/Bukit.Cli -c Release -o out/bukit
```

## GitHub Pages Deployment

Template workflow: [`.github/workflows/release.yml`](../../.github/workflows/release.yml)

Key: auto-compute `BASE_URL` and `SITE_URL`, then `bukit build --base-url "$BASE_URL" --site-url "$SITE_URL"`.

### baseUrl Rules
- User/org site (`owner.github.io`): `baseUrl=/`, `siteUrl=https://owner.github.io`
- Repo site (`owner.github.io/repo`): `baseUrl=/repo`, `siteUrl=https://owner.github.io/repo`

## Other Static Hosts (Nginx/OSS/Netlify/Vercel)

As long as `build.output` is published as the static root. Set `site.baseUrl` for sub-paths, `site.url` for absolute URLs.

## FAQ

1. Pages 404 after deployment: Check baseUrl
2. Incorrect sitemap/rss links: Check `site.url`/`--site-url`
3. Plugin works locally but not after publish: AOT disables external DLL plugin loading

