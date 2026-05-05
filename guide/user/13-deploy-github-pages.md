# 13 Deploying to GitHub Pages: Minimal Config, baseUrl & Common 404s

This page is aimed at "ordinary user deployment." The goal is to help you stably publish build output to GitHub Pages and clearly explain the most common 404/resource path issues.

The repository provides a Pages workflow template sample [`.github/workflows/pages.yml`](../../.github/workflows/pages.yml), which you can copy directly into your own repository, or follow the steps in this article to create your own.

## What You Will Get

- Minimal steps to enable GitHub Pages
- Correct configuration of baseUrl and site.url (including auto-derivation logic)
- Secure injection of Notion tokens (Secrets)
- Common issues: homepage loads but resources 404, or entire site 404 after deployment

## Step 1: Enable GitHub Pages (Repository Settings)

1. GitHub repository Settings → Pages
2. Under Build and deployment, select "GitHub Actions"

## Step 2: Prepare the Workflow (Create pages.yml in Your Repository)

The recommended workflow does three things:

1. Publish `bukit` (Native AOT)
2. Auto-compute `BASE_URL` and `SITE_URL` based on the repository name
3. Run `bukit build` and upload the output to Pages

Key snippet (annotated):

- Auto-compute URL (user/org pages vs. project pages differ):

```bash
REPO_NAME="${GITHUB_REPOSITORY#*/}"
OWNER="${GITHUB_REPOSITORY%/*}"
if [[ "$REPO_NAME" == *.github.io ]]; then
  BASE_URL=/
  SITE_URL=https://${OWNER}.github.io
else
  BASE_URL=/${REPO_NAME}
  SITE_URL=https://${OWNER}.github.io/${REPO_NAME}
fi
```

- Build command (note `--base-url` and `--site-url`):

```bash
./out/bukit/bukit build --config <your-site.yaml> --output _site --base-url "$BASE_URL" --site-url "$SITE_URL" --ci --clean
```

## Step 3: Adapt the Workflow to "Build Your Site"

You need to change two things:

### 1) Change `--config` to point to your configuration file

For example, if your config is at the repository root `site.yaml`:

```bash
./out/bukit/bukit build --config site.yaml --output _site --base-url "$BASE_URL" --site-url "$SITE_URL" --ci --clean
```

If you use multi-site (`sites/blog.yaml`), it is recommended to use `--site blog` directly (and ensure rootDir is the repository root):

```bash
./out/bukit/bukit build --site blog --output _site --base-url "$BASE_URL" --site-url "$SITE_URL" --ci --clean
```

### 2) Change upload-pages-artifact's path to point to the actual output directory

If your build outputs to `_site`, the upload path should also be `_site`:

```yaml
- uses: actions/upload-pages-artifact@v3
  with:
    path: _site
```

## Notion Sites: Inject NOTION_TOKEN (Secrets)

If you use the Notion provider, you need to inject `NOTION_TOKEN` in the workflow. Recommended approach:

1. GitHub repository Settings → Secrets and variables → Actions
2. Create a new Secret: `NOTION_TOKEN`
3. Inject the environment variable in the workflow build step:

```yaml
env:
  NOTION_TOKEN: ${{ secrets.NOTION_TOKEN }}
```

Security principles:

- Do not write the token into `site.yaml`
- Do not print the token in logs

## baseUrl and site.url: How to Configure to Avoid 404s

### 1) You are a user/org homepage repository: &lt;owner&gt;.github.io

Access URL: `https://<owner>.github.io/`

- `baseUrl`: `/`
- `site.url`: `https://<owner>.github.io`

### 2) You are a project repository: &lt;repo&gt;

Access URL: `https://<owner>.github.io/<repo>/`

- `baseUrl`: `/<repo>`
- `site.url`: `https://<owner>.github.io/<repo>`

The workflow already auto-derives these values using the above rules and overrides via CLI, so you generally do not need to hardcode these values in `site.yaml` (especially if you want to reuse the same site across different repos).

## Common Issues and Fixes

### 1) Homepage loads, but CSS/images 404

Cause: baseUrl is misconfigured or theme templates do not prepend baseUrl.

Fix:

- Confirm the workflow passes the correct `--base-url` (project repos must use `/<repo>`)
- In the theme, ensure resource links account for baseUrl (e.g., `/assets/style.css` will 404 under a sub-path)

### 2) Entire site 404

Check first:

- Whether GitHub Pages has GitHub Actions deployment enabled
- Whether upload-pages-artifact's `path` points to the actual output directory
- Whether `bukit build` successfully generated `index.html`

### 3) URLs in sitemap/rss are wrong

Cause: `site.url` is incorrect.

Fix:

- Pass `--site-url` in the build command (the workflow already auto-derives it)
- Or write the correct `site.url` in `site.yaml`
