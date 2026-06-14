# Seni Bina GEO (Pengoptimuman Enjin Generatif)

Dokumen ini menerangkan pelaksanaan sistem Pengoptimuman Enjin Generatif (GEO) Bukit — bagaimana llms.txt, peraturan perangkak AI, dan data berstruktur GEO dijana semasa binaan.

Rujukan pelaksanaan:
- `src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs`
- `src/Bukit.Cli/Commands/GeoCommand.cs`
- `src/Bukit.Engine/SeoModelBuilder.cs` (penghuraian Front Matter GEO)
- `src/Bukit.Config/AppConfig.cs` (model SeoGeoConfig)

Dokumen berkaitan: [Plugin Terbina Dalam](./built-in-plugins.ms.md), [SEO & i18n](./i18n-seo.ms.md), [Panduan Pengguna: 17 GEO](../../guide/user/17-geo.ms.md)

## Gambaran Keseluruhan

GEO melanjutkan SEO tradisional dengan artifak dan data berstruktur yang dioptimumkan untuk enjin carian berkuasa AI (ChatGPT Search, Perplexity, Google AI Overviews, Bing Copilot). Tiga lapisan:

1. **Artifak statik** — `llms.txt`, `llms-full.txt`, peraturan perangkak AI `robots.txt`
2. **Data berstruktur** — FAQPage, HowTo, Person, Article, Speakable JSON-LD daripada Front Matter
3. **Diagnostik audit** — 7 kod diagnostik `geo.*` + Skor GEO

## Model Konfigurasi

Semua konfigurasi GEO terletak di bawah `site.seo.geo`:

| Medan | Jenis | Lalai | Dilaksanakan Dalam |
|------|------|------|------|
| `enabled` | bool | `true` | LlmsTxtPlugin, SeoModelBuilder |
| `llmsTxt` | bool | `true` | LlmsTxtPlugin |
| `llmsFullTxt` | bool | `false` | LlmsTxtPlugin |
| `llmsTxtMaxArticles` | int | `20` | LlmsTxtPlugin |
| `aiBotMode` | string | `"allow"` | LlmsTxtPlugin (robots.txt) |
| `aiBotAllowList` | string[] | — | LlmsTxtPlugin |
| `aiBotBlockList` | string[] | — | LlmsTxtPlugin |
| `llmsTxtOptionalLinks` | array | — | LlmsTxtPlugin |

## Saluran Binaan

### 1. Pemuatan Kandungan

Front Matter GEO dihuraikan semasa pemuatan kandungan melalui `SeoModelBuilder`. Kunci `geo:` dalam Front Matter dibaca sebagai objek berstruktur. Tiada perubahan fasa binaan pada pemuatan kandungan.

### 2. Fasa Halaman Derived

Tiada kerja khusus GEO dalam fasa ini. Artifak statik GEO ditulis oleh publish projection adapter sebelum publish audit dijalankan.

### 3. Fasa Publish Projection

Publish projection adapter menggunakan semula logik penjanaan `LlmsTxtPlugin`:

1. **Semak didayakan**: Kembali serta-merta jika `!geo.Enabled`
2. **Penjanaan llms.txt** (jika `geo.LlmsTxt`):
   - Mengulangi `context.Routed` + `context.DerivedRouted`
   - Menapis kepada entri `Indexable` daripada `context.SeoIndex`
   - Mengumpulkan kepada **Dokumentasi** (halaman bukan post) dan **Artikel** (post, diisih mengikut `PublishAt` menurun)
   - Mengehadkan artikel kepada `geo.LlmsTxtMaxArticles`
   - Menambah bahagian **Optional** daripada `geo.LlmsTxtOptionalLinks`
   - Menulis ke `<outputDir>/llms.txt`
3. **Penjanaan llms-full.txt** (jika `geo.LlmsFullTxt`):
   - Mengulangi semua laluan boleh indeks
   - Membuang tag HTML daripada kandungan
   - Menggabungkan dengan pemisah `---`
   - Menulis ke `<outputDir>/llms-full.txt`
4. **Peraturan perangkak AI** (ditambah ke `robots.txt` atau sebaris):
   - Mengenali 12 user-agent bot AI
   - Menggunakan mod `allow`/`block`/`selective`

### 4. Integrasi Indeks SEO

`LlmsTxtPlugin` menggunakan semula `context.SeoIndex` sedia ada (dibina oleh `SeoIndexBuilder`) untuk menentukan halaman mana yang boleh diindeks. Halaman dengan `robots: noindex` dikecualikan daripada llms.txt.

## Model GEO Front Matter

Dihuraikan daripada Front Matter kandungan di bawah kunci `geo:`. Pelaksanaan dalam `SeoModelBuilder`:

| Medan Front Matter | Jenis | Output Schema.org |
|------|------|-----------------|
| `schema_type` | string | Menggantikan `@type`: BlogPosting (lalai), Article, NewsArticle, FAQPage, HowTo |
| `faq` | array {question, answer} | `FAQPage` dengan item `Question`/`Answer` |
| `steps` | array {name, text, image?, url?} | `HowTo` dengan item `HowToStep` |
| `author` | {name, url, same_as} | `Person` dengan pautan `sameAs` |
| `citations` | array {title, url} | `WebPage` dengan `mentions` |
| `same_as` | string[] | `sameAs` pada entiti utama |
| `about` | string | sifat `about` |
| `date_reviewed` | string | `dateReviewed` (ISO 8601) |
| `speakable.xpath` | string | `SpeakableSpecification` |

## Audit GEO

Pelaksanaan: `src/Bukit.Cli/Commands/GeoCommand.cs`

Membaca artifak audit binaan dari `.bukit/publish-audit-report.json` secara lalai. Jika perbandingan rentas-skema diperlukan, berikan secara eksplisit `.bukit/seo-report.json` (titik masuk keserasian), kemudian mengira berasaskan laporan yang dipilih:

### Skor GEO (0–100)

| Kriteria | Mata Maks | Sumber |
|-----------|-----------|--------|
| llms.txt dijana | 25 | Semakan kewujudan fail |
| llms-full.txt dijana | 15 | Semakan kewujudan fail |
| Sekurang-kurangnya 1 laluan dipertingkat GEO | 10 | Semakan metadata laluan |
| Liputan skema artikel | 15 | Nisbah laluan GEO kepada jumlah laluan |
| FAQPage/HowTo digunakan | 15 | Pengesanan jenis skema |
| Skema pengarang Person | 10 | Kewujudan medan pengarang |
| SpeakableSpecification | 5 | Kewujudan medan XPath |
| Liputan GEO pelbagai laluan | 5 | Kiraan laluan GEO > 1 |

### Kod Diagnostik

Dijana semasa diagnostik `bukit build` (apabila `site.seo.diagnostics` adalah `warn` atau `strict`):

| Kod | Keterukan | Pencetus |
|------|---------|---------|
| `geo.llms_txt_missing` | warning | GEO didayakan tetapi llms.txt tidak dijumpai |
| `geo.llms_full_txt_missing` | warning | llmsFullTxt didayakan tetapi fail tidak dijumpai |
| `geo.schema_type_missing` | info | Kandungan mempunyai tarikh terbitan tetapi tiada medan GEO |
| `geo.faq_empty_question` | error | Item FAQ mempunyai soalan kosong |
| `geo.faq_empty_answer` | error | Item FAQ mempunyai jawapan kosong |
| `geo.howto_step_empty_name` | error | Langkah HowTo mempunyai nama kosong |
| `geo.howto_step_empty_text` | error | Langkah HowTo mempunyai teks kosong |
| `geo.citation_url_invalid` | warning | URL petikan bukan mutlak |
| `geo.author_no_sameas` | info | Pengarang ditakrifkan tetapi tiada pautan sameAs |
| `geo.speakable_path_invalid` | warning | XPath tidak bermula dengan `/` |

## Senarai Bot Perangkak AI

Dikodkeras dalam `LlmsTxtPlugin`:

```csharp
static readonly string[] AiBots = {
    "GPTBot", "ChatGPT-User",            // OpenAI
    "Google-Extended",                    // Google AI
    "Claude-Web", "ClaudeBot", "Anthropic-AI",  // Anthropic
    "PerplexityBot",                      // Perplexity
    "Cohere-AI",                          // Cohere
    "CCBot", "Diffbot",                   // Common Crawl / Diffbot
    "FacebookBot",                        // Meta
    "OAI-SearchBot"                       // OpenAI Search
};
```

Logik penjanaan peraturan robots.txt:

| `aiBotMode` | Untuk Setiap Bot | Bot Tidak Tersenarai |
|------------|-------------|--------------|
| `allow` | `Allow: /` | (tiada peraturan) |
| `block` | `Disallow: /` | (tiada peraturan) |
| `selective` | Allow jika dalam `aiBotAllowList`, Disallow jika dalam `aiBotBlockList` | `Disallow: /` |

## Titik Masuk CLI

| Arahan | Tujuan | Bendera Utama |
|---------|------|---------|
| `bukit build` | Bina dengan penjanaan artifak GEO | (membaca konfigurasi site.seo.geo) |
| `bukit geo audit` | Audit kesediaan GEO dist sedia ada | `--dir <path>` |

Audit GEO membaca laporan audit yang dijana dari direktori output binaan. Ia tidak memerlukan binaan semula; gunakan `bukit publish audit` untuk pagar kebolehbacaan mesin dan kepercayaan yang lebih luas.

## Output Fail

| Fail | Owner | Konfigurasi Diperlukan |
|------|--------|----------------|
| `llms.txt` | Publish projection adapter via `LlmsTxtPlugin` | `geo.enabled && geo.llmsTxt` |
| `llms-full.txt` | Publish projection adapter via `LlmsTxtPlugin` | `geo.enabled && geo.llmsFullTxt` |
| `robots.txt` (peraturan AI) | Publish projection adapter via crawler policy writer | `geo.enabled && seo.robotsTxt.enabled` |

Struktur kandungan llms.txt mengikuti spesifikasi [llmstxt.org](https://llmstxt.org): `# Tajuk` → `> Penerangan` → `## Dokumentasi` → `## Artikel` → `## Optional`.
