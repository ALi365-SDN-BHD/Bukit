# 13 Menyahgunakan ke GitHub Pages: Konfigurasi Minimum, baseUrl & 404 Lazim

Halaman ini bertujuan untuk "penyahgunaan pengguna biasa." Matlamatnya adalah untuk membantu anda menerbitkan output binaan secara stabil ke GitHub Pages dan menerangkan dengan jelas isu 404/laluan sumber yang paling lazim.

Repositori ini menyediakan contoh templat aliran kerja Pages [`.github/workflows/pages.yml`](../../.github/workflows/pages.yml).

## Langkah 1: Dayakan GitHub Pages

1. GitHub repositori Settings → Pages
2. Di bawah Build and deployment, pilih "GitHub Actions"

## Langkah 2: Sediakan Aliran Kerja

Aliran kerja yang disyorkan: Terbitkan `bukit` (Native AOT); Auto-kira `BASE_URL` dan `SITE_URL`; Jalankan `bukit build` dan muat naik output.

Auto-kira URL:

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

Perintah bina:

```bash
./out/bukit/bukit build --config site.yaml --output _site --base-url "$BASE_URL" --site-url "$SITE_URL" --ci --clean
```

## Langkah 3: Sesuaikan Aliran Kerja

Tukar `--config` dan `upload-pages-artifact` path.

## Laman Notion: Suntik NOTION_TOKEN

```yaml
env:
  NOTION_TOKEN: ${{ secrets.NOTION_TOKEN }}
```

## baseUrl dan site.url

### Repositori halaman utama pengguna/org: &lt;owner&gt;.github.io
- `baseUrl`: `/`, `site.url`: `https://<owner>.github.io`

### Repositori projek: &lt;repo&gt;
- `baseUrl`: `/<repo>`, `site.url`: `https://<owner>.github.io/<repo>`

## Isu Lazim

### 1) Halaman utama boleh dibuka, tetapi CSS/imej 404
Pembaikan: `--base-url` mesti `/<repo>` untuk repo projek.

### 2) Seluruh laman 404
Semak: GitHub Pages didayakan; `path` menunjuk ke output sebenar; `index.html` dijana.

### 3) URL dalam sitemap/rss salah
Pembaikan: Hantar `--site-url` atau tetapkan `site.url`.
