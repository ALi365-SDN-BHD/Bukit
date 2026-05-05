# Manifest Keupayaan Templat

`layouts/bukit.templates.yaml` mengisytiharkan kebergantungan data templat.

## Struktur Asas
```yaml
templates:
  pages/list.html:
    capabilities:
      needs_page_content: true
      supports_pagination: true
      supports_taxonomy: false
      supports_search_snippets: false
```

## Keupayaan Diiktiraf
- `needs_page_content`: Templat bergantung pada `pages[*].content`
- `supports_pagination`: Sesuai sebagai templat senarai penomboran
- `supports_taxonomy`: Sesuai sebagai templat taksonomi
- `supports_search_snippets`: Sesuai untuk rendering ringkasan carian

## Hubungan dengan `build.listPageContentMode`
- `auto`: Utamakan `bukit.templates.yaml`; sandar kepada heuristik keserasian
- `always`: Sentiasa isi badan halaman senarai
- `never`: Jangan isi badan halaman senarai
