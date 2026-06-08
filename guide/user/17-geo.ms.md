# 17 Pengoptimuman Enjin Generatif (GEO): llms.txt, Perangkak AI & Data Berstruktur

GEO menjadikan laman Bukit anda boleh ditemui dan dibaca oleh enjin carian dipacu AI — ChatGPT Search, Perplexity, Google AI Overviews, Bing Copilot — melangkaui SEO tradisional.

Contoh boleh dijalankan: `examples/starter/site.i18n.seo.yaml`

## Apa yang Anda Akan Dapat

- Penjanaan `llms.txt` dan `llms-full.txt` untuk enjin AI
- Peraturan `robots.txt` automatik untuk perangkak AI (12 bot dikenali)
- Data berstruktur FAQPage, HowTo, Article daripada front matter kandungan
- Audit GEO dengan Skor GEO berangka (0–100)
- Amaran diagnostik semasa binaan untuk data GEO yang hilang atau rosak

## Langkah 1: Dayakan GEO

Konfigurasi GEO terletak di bawah `site.seo.geo`. Semua medan mempunyai lalai yang munasabah:

```yaml
site:
  seo:
    enabled: true
    geo:
      enabled: true
```

Ini sahaja menjana `llms.txt` dan membenarkan perangkak AI.

## Langkah 2: Konfigurasi Akses Perangkak AI

```yaml
site:
  seo:
    geo:
      aiBotMode: selective       # allow | block | selective
      aiBotAllowList:
        - GPTBot
        - PerplexityBot
      aiBotBlockList:
        - CCBot
```

**Bot AI yang dikenali**: GPTBot, ChatGPT-User, Google-Extended, Claude-Web, ClaudeBot, Anthropic-AI, PerplexityBot, Cohere-AI, CCBot, Diffbot, FacebookBot, OAI-SearchBot.

## Langkah 3: Tambah Data Berstruktur GEO

### FAQ Page

```yaml
---
title: Soalan Lazim
collection: page
geo:
  schema_type: FAQPage
  faq:
    - question: Apakah sumber kandungan yang disokong oleh Bukit?
      answer: Notion, Markdown, dan fail setempat.
---
```

### HowTo Guide

```yaml
---
title: Cara Membina Blog dengan Bukit
collection: post
geo:
  schema_type: HowTo
  about: Penjanaan Laman Statik
  steps:
    - name: Muat Turun Bukit
      text: Muat turun binari dari GitHub Releases.
    - name: Mulakan Tapak
      text: Jalankan bukit init blog-saya.
---
```

## Langkah 4: Jana llms-full.txt (Pilihan)

```yaml
site:
  seo:
    geo:
      llmsFullTxt: true
```

## Langkah 5: Sesuaikan llms.txt

```yaml
site:
  seo:
    geo:
      llmsTxtMaxArticles: 30
      llmsTxtOptionalLinks:
        - title: Repositori GitHub
          url: https://github.com/user/repo
          description: Kod sumber
```

## Langkah 6: Jalankan Audit GEO

```bash
bukit build
bukit geo audit --dir dist
```

## Isu Lazim

| Isu | Punca | Pembetulan |
|------|------|------|
| llms.txt tidak dijana | `geo.enabled: false` | Dayakan GEO + llmsTxt |
| Schema FAQPage tidak muncul | `geo.faq` kosong | Tambah sekurang-kurangnya satu entri FAQ |
| Skor GEO 0 | Tiada llms.txt, tiada front matter GEO | Dayakan llmsTxt, tambah medan `geo:` |

## Langkah Seterusnya

- [12 Rujukan CLI](./12-cli-reference.md)
- [11 I18n & SEO](./11-i18n-seo.md)
- [Pembangun: Seni Bina GEO](../dev/geo.md)
