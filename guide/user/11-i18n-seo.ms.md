# 11 Pelbagai Bahasa & SEO: languages, Mod Output & Perangkap Lazim

Bahagian paling sukar dalam tapak pelbagai bahasa bukanlah "menterjemah kandungan" tetapi "struktur URL, artifak SEO, dan penghubungan antara bahasa." Halaman ini menerangkan perkara-perkara ini dengan konfigurasi dan contoh sedia salin.

Lihat contoh boleh laku: `examples/starter/site.i18n.yaml`, `examples/starter/site.i18n.merged.yaml`, `examples/starter/site.i18n.index.yaml`, `examples/starter/site.i18n.seo.yaml`.

## Langkah 1: Dayakan Pelbagai Bahasa

```yaml
site:
  language: zh-CN
  languages:
    - zh-CN
    - en-US
  defaultLanguage: zh-CN
```

## Langkah 2: Tandakan Setiap Kandungan dengan language

### Markdown

```yaml
---
type: page
title: Hello
slug: greeting
language: en-US
---
```

### Notion

Tambah medan `language` dalam pangkalan data. Butiran: [06 Kandungan Notion](./06-notion-content.ms.md).

## Struktur URL: Ke Mana Tapak Pelbagai Bahasa Dioutputkan

```text
dist/
  zh-CN/
    index.html
  en-US/
    index.html
  sitemap.xml atau zh-CN/sitemap.xml (bergantung pada mod)
```

## Cara Memilih Mod Output sitemap/rss/search

### split: Satu Setiap Bahasa

```yaml
site:
  sitemapMode: split
  rssMode: split
  searchMode: split
```

### merged: Satu Digabungkan

```yaml
site:
  sitemapMode: merged
  rssMode: merged
  searchMode: merged
```

### index: Output Indeks Akar

```yaml
site:
  sitemapMode: index
  searchMode: index
```

> **Nota**: `rssMode` hanya menyokong `split` / `merged`.

## Tritunggal SEO: site.url, baseUrl, Coretan SEO Tema

### 1) site.url: Menentukan Pautan Mutlak

```yaml
site:
  url: https://user.github.io/my-repo
```

### 2) baseUrl: Menentukan Awalan Sumber dan Pautan

```yaml
site:
  baseUrl: /my-repo
```

### 3) Tema: Sama ada untuk Output canonical/alternates/meta

Bandingkan dengan `examples/starter/themes/seo-best-practice/`.

## Pengoptimuman Enjin Generatif (GEO)

GEO mengoptimumkan laman anda untuk enjin carian berkuasa AI seperti ChatGPT Search, Perplexity, Google AI Overviews, dan Bing Copilot. Ia melangkaui SEO tradisional untuk membantu enjin AI merangkak, memahami, dan memetik kandungan anda dengan tepat.

### Konfigurasi

```yaml
site:
  seo:
    geo:
      enabled: true            # suis utama (default: true)
      llmsTxt: true            # jana llms.txt (default: true)
      llmsFullTxt: false       # jana llms-full.txt dengan kandungan penuh (default: false)
      llmsTxtMaxArticles: 20   # artikel maksimum dalam llms.txt (default: 20)
      aiBotMode: allow          # allow | block | selective
      aiBotAllowList:           # bot dibenarkan (mod selective)
        - GPTBot
      aiBotBlockList:           # bot disekat
        - CCBot
      llmsTxtOptionalLinks:     # pautan luaran dalam bahagian Optional llms.txt
        - title: Repositori GitHub
          url: https://github.com/user/repo
          description: Kod sumber
```

### llms.txt dan llms-full.txt

Apabila diaktifkan, Bukit menjana dua fail dalam direktori output:

- **`llms.txt`** — Indeks laman berformat Markdown mengikut standard [llmstxt.org](https://llmstxt.org). Mengandungi tajuk laman, penerangan, senarai halaman/dokumen, artikel terkini (disusun mengikut tarikh), dan bahagian "Optional" pilihan dengan pautan luaran.
- **`llms-full.txt`** — Versi kandungan penuh yang mengandungi teks lengkap setiap halaman boleh indeks, dipisahkan dengan pengepala Markdown. Berguna untuk enjin AI yang memerlukan konteks lebih kaya.

### Peraturan robots.txt Bot AI

Bukit secara automatik menambah arahan perangkak AI ke `robots.txt` untuk bot berikut:

GPTBot, ChatGPT-User, Google-Extended, Claude-Web, ClaudeBot, Anthropic-AI,
PerplexityBot, Cohere-AI, CCBot, Diffbot, FacebookBot, OAI-SearchBot

Tiga mod tersedia:
- **`allow`** (default) — Semua bot AI dibenarkan
- **`block`** — Semua bot AI dilarang
- **`selective`** — `aiBotAllowList` mendapat `Allow: /`, `aiBotBlockList` mendapat `Disallow: /`

### Medan GEO Front Matter

Tambahkan data berstruktur ke front matter kandungan anda di bawah kunci `geo`:

```yaml
---
title: Cara Membina Blog dengan Bukit
type: post
geo:
  schema_type: HowTo         # BlogPosting | Article | NewsArticle | FAQPage | HowTo
  about: Penjana laman statik
  date_reviewed: "2026-05-19"
  faq:
    - question: Apakah sumber kandungan yang disokong Bukit?
      answer: Notion, Markdown, dan fail tempatan.
    - question: Bagaimana untuk menyahguna?
      answer: GitHub Pages, Vercel, Netlify, dan banyak lagi.
  steps:
    - name: Pasang Bukit
      text: Jalankan dotnet tool install.
      image: https://example.com/step1.png
      url: https://example.com/docs/install
    - name: Mulakan laman
      text: Jalankan bukit init my-site.
  citations:
    - title: Schema.org HowTo
      url: https://schema.org/HowTo
  same_as:
    - https://github.com/user/repo
    - https://twitter.com/user
  author:
    name: Ali
    url: https://ali.dev
    same_as:
      - https://github.com/ali
      - https://linkedin.com/in/ali
  speakable:
    xpath: /html/body/article
---
```

Setiap medan menjana data berstruktur JSON-LD yang sepadan:

| Medan | Jenis Schema Dijana |
|-------|-------------------|
| `faq` | FAQPage dengan Question/Answer |
| `steps` | HowTo dengan HowToStep |
| `author` | Person dengan sameAs |
| `citations` | WebPage dengan mentions |
| `schema_type` | Article / NewsArticle / BlogPosting |
| `about` | sifat about pada artikel |
| `date_reviewed` | dateReviewed pada artikel |
| `same_as` | sameAs pada artikel |
| `speakable` | SpeakableSpecification |

### Audit GEO

Jalankan `bukit geo audit` untuk memeriksa kesediaan GEO laman anda:

```
=== GEO Audit ===
  llms.txt: present
  llms-full.txt: missing
  robots.txt: present
  Geo-enhanced routes: 3
  Schema types: Article, FAQPage, HowTo, Person, WebPage
  GEO Score: 75/100
```

**Skor GEO** (0–100) mengukur kesediaan laman anda untuk enjin carian AI. Mata diberikan untuk:
- Penjanaan llms.txt (25 mata)
- Penjanaan llms-full.txt (15 mata)
- Laluan dipertingkat GEO (10 mata)
- Liputan jenis Schema pada artikel (sehingga 15 mata)
- Penggunaan FAQPage atau HowTo (15 mata)
- Penanda pengarang Person (10 mata)
- Penanda Speakable (5 mata)
- Liputan GEO berbilang laluan (5 mata)

Kod diagnostik (`geo.*`) muncul dalam log binaan dan `seo-report.json`:
- `geo.faq_empty_question` / `geo.faq_empty_answer`
- `geo.howto_step_empty_name` / `geo.howto_step_empty_text`
- `geo.citation_url_invalid`
- `geo.author_no_sameas`
- `geo.speakable_path_invalid`
- `geo.schema_type_missing`
- `geo.llms_txt_missing`

## Perangkap Lazim & Senarai Semak Pembaikan

### 1) Kandungan pelbagai bahasa "bersilang"

Pembaikan: Sahkan setiap kandungan mempunyai `language` yang ditetapkan; dalam mod Notion, pastikan nilai adalah konsisten (`en-US` jangan ditulis sebagai `en`).

### 2) URL dalam sitemap salah

Pembaikan: Tetapkan `site.url`; tetapkan `site.baseUrl` yang betul; bina semula.

### 3) 404 selepas penyahgunaan

Pembaikan: GitHub Pages terbitkan direktori menunjuk ke `dist/`; tema menggabungkan awalan bahasa dengan betul.
