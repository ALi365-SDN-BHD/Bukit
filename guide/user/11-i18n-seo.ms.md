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

## Perangkap Lazim & Senarai Semak Pembaikan

### 1) Kandungan pelbagai bahasa "bersilang"

Pembaikan: Sahkan setiap kandungan mempunyai `language` yang ditetapkan; dalam mod Notion, pastikan nilai adalah konsisten (`en-US` jangan ditulis sebagai `en`).

### 2) URL dalam sitemap salah

Pembaikan: Tetapkan `site.url`; tetapkan `site.baseUrl` yang betul; bina semula.

### 3) 404 selepas penyahgunaan

Pembaikan: GitHub Pages terbitkan direktori menunjuk ke `dist/`; tema menggabungkan awalan bahasa dengan betul.
