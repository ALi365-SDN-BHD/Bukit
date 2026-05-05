# 10 Ciri Terbina Dalam & Output: sitemap/rss/search, Teg, Kategori & Halaman Terbitan

Selain menjana HTML halaman, Bukit juga menjana satu set "artifak peringkat tapak" berdasarkan kandungan dan konfigurasi, digunakan untuk SEO, langganan, carian, dan agregasi kandungan.

Halaman ini memberi tumpuan kepada "apa yang pengguna boleh kawal, fail apa yang akan dijana"; untuk kontrak dan sempadan plugin yang lebih terperinci, lihat dokumen pembangun: [guide/dev/built-in-plugins](../dev/built-in-plugins.md).

## Apa yang Anda Akan Dapat

- Fail tambahan apa yang dijana dan di mana
- Bagaimana fail-fail ini dioutputkan dalam mod pelbagai bahasa (split/merged/index)
- Apakah "halaman terbitan" seperti tags/categories/arkib/penomboran
- Soalan Lazim: mengapa pautan dalam sitemap salah, mengapa search.json kosong

## Senarai Artifak Peringkat Tapak (Lazim)

Dalam direktori output binaan (`build.output`, lalai `dist/`) anda biasanya akan melihat:

- `sitemap.xml`
- `rss.xml`
- `search.json` (data carian menghadap pelayar)
- `search.index.json` (pilihan: indeks agregat)
- `tags/`, `categories/` (halaman senarai terbitan, khusus kepada tema dan logik terbitan)

## sitemap.xml

Anda boleh mengkonfigurasi: `site.url`, `site.baseUrl`, `site.sitemapMode`.

Perangkap lazim: `site.url` tidak ditetapkan; baseUrl salah konfigurasi.

## rss.xml

RSS biasanya bergantung pada: URL tapak (`site.url`), tajuk/tarikh terbit/jenis kandungan (terutamanya post).

## search.json

search.json biasanya adalah senarai "tajuk/ringkasan/URL untuk setiap halaman" untuk JS hujung depan melaksanakan carian.

## Teg & Kategori (tags / categories)

Apabila kandungan anda mengandungi `tags` atau `categories`, enjin/plugin mengagregatkan maklumat ini; tema biasanya menghasilkan halaman senarai dan halaman butiran tags/categories.

## Halaman Terbitan

Halaman terbitan bukan halaman yang anda karang secara langsung dalam sumber kandungan anda, tetapi halaman "diterbitkan" oleh enjin dari kandungan, contohnya:

- `/tags/<tag>/`: senarai artikel di bawah teg tertentu
- `/categories/<category>/`: senarai artikel di bawah kategori tertentu
- `/blog/page/2/`: halaman senarai bernombor
- `/archive/2026/`: arkib mengikut tahun

## pluginFailMode

```yaml
site:
  pluginFailMode: strict  # strict (lalai) | warn
```

- `strict`: Ralat plugin mengganggu pembinaan (sesuai untuk produksi)
- `warn`: Log ralat tetapi teruskan output (sesuai untuk migrasi/penyahpepijatan)

## Mod Output Pelbagai Bahasa (sitemap/rss/search)

- `split`: Satu setiap bahasa
- `merged`: Diagregatkan menjadi satu
- `index`: Direktori akar output fail indeks, menunjuk ke fail setiap bahasa

Cara memilih: [11 Pelbagai Bahasa & SEO](./11-i18n-seo.ms.md).
