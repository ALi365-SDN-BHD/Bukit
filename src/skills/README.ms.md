# Bukit Agent Skills

`src/skills/` menyimpan panduan khusus Bukit untuk AI Agent, bukan kod sumber runtime. Ia memecahkan tugasan Bukit yang biasa kepada fail `SKILL.md` yang lebih fokus supaya agent boleh memilih sempadan pengetahuan yang betul untuk pembinaan tapak, konfigurasi, tema, kandungan, routing, i18n, dan penyahpepijatan.

Jika anda menggunakan Bukit melalui Trae, Claude Code, Copilot CLI, Codex CLI, Gemini CLI, atau persekitaran lain yang menyokong skill, anggap direktori ini sebagai lapisan navigasi untuk agent:

- Mulakan dengan `using-bukit` apabila tugasan secara jelas menggunakan Bukit
- Gunakan `bukit-cli-reference` sebagai sumber tunggal untuk pelaksanaan arahan
- Muatkan sub-skill yang sepadan untuk konfigurasi, tema, templating, Notion, routing, i18n, atau plugin/debug

## Susun Atur Direktori

```text
src/skills/
  using-bukit/            # Pintu masuk Bukit yang bersatu
  bukit-cli-reference/    # Sumber tunggal untuk operasi CLI
  bukit-config/           # Model konfigurasi site.yaml
  bukit-theme/            # Direktori tema, aset statik, wizard penciptaan, pengedaran
  bukit-templating/       # Pembangunan templat Scriban
  bukit-design-tokens/    # Pembolehubah CSS, palet warna, skala tipografi, jarak, mod gelap
  bukit-content-to-template/  # Penjanaan templat dipacu skema
  bukit-notion/           # Sumber kandungan Notion
  bukit-routing/          # Routing URL dan permalink
  bukit-i18n/             # Tapak berbilang bahasa
  bukit-plugins-debug/    # Plugin, binaan tokokan, diagnostik
  bukit-deploy/           # Penerapan GitHub Pages
  bukit-clone/            # Klon reka bentuk laman web → tema Bukit
  bukit-seo/              # Pengoptimuman enjin carian tradisional (SEO)
  bukit-geo/              # Pengoptimuman enjin generatif (GEO)
```

## Tanggungjawab Skill

| Skill | Tanggungjawab | Kes penggunaan biasa |
|---|---|---|
| `using-bukit` | Skill pintu masuk yang mengenal pasti kerja Bukit dan menghala ke sub-skill | Pengguna secara jelas menyebut "using bukit" atau tugasan jelas khusus Bukit |
| `bukit-cli-reference` | Pengesanan CLI, panduan pemasangan, rujukan arahan, tafsiran output dan kod keluar | Menjalankan sebarang arahan `bukit` termasuk `build`, `clean`, `config`, `doctor`, `preview`, `seo`, `geo`, `publish`, `deploy`, `completion`, `version` |
| `bukit-config` | Struktur `site.yaml`, templat senario, dan penjelasan medan | Mencipta atau mengedit konfigurasi, menjelaskan medan, membetulkan ralat pengesahan |
| `bukit-theme` | Struktur direktori tema, aset statik, penciptaan berasaskan wizard, pengedaran tema (pack/install), carian registri, coretan templat | Mencipta tema melalui wizard/preset, menyenaraikan info/params tema, membungkus tema untuk perkongsian, memasang dari registri, melayari coretan templat |
| `bukit-templating` | Sintaks Scriban, pewarisan layout, akses data, dan corak templat | Menulis templat halaman, halaman senarai, penomboran, atau membetulkan ralat render templat |
| `bukit-design-tokens` | Sistem token reka bentuk untuk tema Bukit: pembolehubah CSS, palet warna, skala tipografi, sistem jarak, dan konfigurasi mod gelap | Mencipta identiti visual yang konsisten, mendefinisikan pembolehubah `:root {}` CSS, menyediakan mod gelap, memilih palet warna |
| `bukit-content-to-template` | Penjanaan templat dipacu skema: memetakan skema koleksi kandungan kepada corak templat Scriban yang tepat | Menjana templat post/page/list/card dari definisi skema koleksi `site.yaml`, memastikan setiap medan dirender dengan betul |
| `bukit-notion` | Integrasi Notion, pemetaan properti, render blok, dan penyetempatan imej | Menggunakan Notion sebagai CMS atau menyelesaikan masalah fetch Notion dan isu pemetaan |
| `bukit-routing` | Permalink, laluan koleksi, pengekodan URL, dan tingkah laku laluan output | Menyesuaikan URL, membetulkan 404, mengendalikan konflik laluan, mengkonfigurasi halaman senarai |
| `bukit-i18n` | Pengesanan bahasa, binaan per-bahasa, penggabungan sitemap/RSS/search | Membina tapak berbilang bahasa dan menyahpepijat pensuisan bahasa atau isu output gabungan |
| `bukit-plugins-debug` | Kitaran hayat plugin, tingkah laku binaan tokokan, diagnostik prestasi, penyelesaian masalah | Plugin tidak berjalan, output binaan kelihatan salah, atau prestasi binaan merosot |
| `bukit-deploy` | Penerapan GitHub Pages melalui arahan `bukit deploy`, konfigurasi deploy site.yaml, pembolehubah persekitaran, integrasi CI/CD | Menerapkan tapak, menolak ke gh-pages, mengkonfigurasi CNAME, menyelesaikan masalah kegagalan penerapan |
| `bukit-clone` | Pengekstrakan MCP pelayar → `bukit clone` CLI → saluran paip pengesahan untuk mengklon reka bentuk visual mana-mana laman web ke dalam tema Bukit | Mengklon penampilan laman web, meniru reka bentuk, mencipta tema dari tapak langsung sedia ada |
| `bukit-seo` | Konfigurasi SEO tradisional (nod site.seo), mod render inject/theme, medan SEO front matter, 6 jenis JSON-LD Schema.org, diagnostik masa bina (11 kod), audit pasca bina (~40 kod), CLI seo audit/diff | Mengkonfigurasi SEO, menjalankan seo audit/diff, mentafsir kod diagnostik seo.*, menyediakan OG/Twitter/JSON-LD/sitemap |
| `bukit-geo` | Pengoptimuman enjin generatif untuk enjin carian AI: penjanaan llms.txt/llms-full.txt, peraturan robots.txt crawler AI, data berstruktur FAQ/HowTo, audit geo dengan Skor GEO (7 kod diagnostik) | Mengoptimumkan untuk carian AI (ChatGPT Search/Perplexity/Google AI Overviews), menjana llms.txt, menambah skema FAQ/HowTo, menjalankan geo audit |

## Peraturan Muatkan

Skill ini direka untuk digabungkan dengan sempadan yang jelas:

1. Mulakan dari `using-bukit` apabila tugasan sudah pasti tugasan Bukit
2. Gunakan `bukit-cli-reference` untuk setiap langkah berkaitan arahan dan elakkan penduaan panduan CLI di tempat lain
3. Anggap `bukit-config` sebagai pengetahuan latar untuk `bukit-theme`, `bukit-design-tokens`, `bukit-content-to-template`, `bukit-notion`, `bukit-routing`, `bukit-i18n`, `bukit-plugins-debug`, `bukit-seo`, dan `bukit-geo`
4. Baca `bukit-theme` sebelum `bukit-templating` apabila kerja templat bergantung pada struktur tema
5. Muatkan `bukit-design-tokens` apabila konsistensi visual adalah matlamat — ia menyediakan palet, skala, dan corak mod gelap
6. Muatkan `bukit-content-to-template` apabila menjana templat dari skema koleksi — ia menghubungkan definisi medan skema kepada kod Scriban
7. Muatkan `bukit-seo` untuk tugasan SEO tradisional dan `bukit-geo` untuk tugasan pengoptimuman carian AI — mereka berkongsi konfigurasi `site.seo` tetapi menyasarkan audiens berbeza

Satu aliran kerja biasa kelihatan seperti ini:

```text
using-bukit
  -> bukit-cli-reference
  -> bukit-config
  -> bukit-theme / bukit-design-tokens / bukit-notion / bukit-routing / bukit-i18n / bukit-plugins-debug
  -> bukit-templating / bukit-content-to-template
```

## Panduan Penggunaan

### Susun Atur Fail

```
src/skills/
├── CLAUDE.md                    ← Entri penuh Claude Code Agent
├── AGENTS.md                    ← Entri penuh Codex CLI Agent
├── GEMINI.md                    ← Entri penuh Gemini CLI Agent
├── copilot-instructions.md      ← Entri penuh Copilot CLI
│
├── plugin.json                  ← Manifes plugin Claude Code / Copilot
├── skills-index.yaml            ← Katalog kemahiran boleh dibaca mesin (sumber tunggal)
├── skills-index.json            ← Versi JSON (dijana automatik dari YAML)
│
├── using-bukit/SKILL.md         ← Gerbang: laluan ke semua sub-kemahiran
├── bukit-*/SKILL.md             ← 18 kemahiran domain
│
└── scripts/
    ├── validate-skills.sh       ← CI: sahkan semua fail kemahiran
    └── generate-index-json.sh   ← CI: penukaran YAML → JSON
```

### Penggunaan Mengikut Platform

| Platform | Cara Memuatkan | Perintah Contoh |
|----------|---------------|----------------|
| **Trae** | Auto-penemuan melalui `.trae/rules/project_rules.md` | `"using bukit, bantu saya bina blog"` |
| **Claude Code** | `claude plugins install src/skills` | `"using bukit, deploy ke GitHub Pages"` |
| **Codex CLI** | Baca fail kemahiran secara natif; lihat `src/skills/AGENTS.md` | `"tolong konfigurasi site.yaml untuk blog"` |
| **Copilot CLI** | `copilot plugin install src/skills` | `"using bukit, cipta tema tersuai"` |
| **Gemini CLI** | `activate_skill("using-bukit")` melalui `src/skills/GEMINI.md` | `"sediakan tapak berbilang bahasa"` |

### Mulakan Dengan Pantas

1. Buka repositori ini dalam AI Agent anda
2. Sebut: **"using bukit, bantu saya bina blog"**
3. Agent akan membaca kemahiran berkaitan dan membina tapak secara automatik
4. Sebut: **"bukit preview"** untuk memulakan pelayan pratonton

### Pengesahan CI

```bash
bash src/skills/scripts/validate-skills.sh   # Sahkan semua fail kemahiran
bash src/skills/scripts/generate-index-json.sh  # Jana semula indeks JSON
```

---

## Laluan Bacaan Disyorkan

### Cipta tapak baharu

1. `using-bukit`
2. `bukit-cli-reference`
3. `bukit-config`
4. `bukit-theme`
5. `bukit-templating`

### Konfigurasi Notion sebagai sumber kandungan

1. `using-bukit`
2. `bukit-notion`
3. `bukit-config`
4. `bukit-cli-reference`

### Ubah routing dan halaman senarai

1. `using-bukit`
2. `bukit-routing`
3. `bukit-config`
4. `bukit-templating`

### Nyahpepijat isu binaan atau plugin

1. `using-bukit`
2. `bukit-plugins-debug`
3. `bukit-config`
4. `bukit-cli-reference`

### Terap tapak ke GitHub Pages

1. `using-bukit`
2. `bukit-deploy`
3. `bukit-config`
4. `bukit-cli-reference`

### Konfigurasi SEO dan jalankan audit

1. `using-bukit`
2. `bukit-seo`
3. `bukit-config` (untuk nod `site.seo`)
4. `bukit-cli-reference` (untuk `bukit seo audit` / `bukit seo diff`)

### Sediakan GEO untuk enjin carian AI

1. `using-bukit`
2. `bukit-geo`
3. `bukit-config` (untuk nod `site.seo.geo`)
4. `bukit-cli-reference` (untuk `bukit geo audit`)

### Klon reka bentuk laman web

1. `using-bukit`
2. `bukit-clone`
3. `bukit-theme`
4. `bukit-cli-reference`

### Cipta tema tersuai (interaktif)

1. `using-bukit`
2. `bukit-theme` (wizard + presets)
3. `bukit-cli-reference`

### Pasang tema dari registri komuniti

1. `using-bukit`
2. `bukit-theme` (carian + pasang)
3. `bukit-cli-reference`

### Bina sistem token reka bentuk yang konsisten

1. `using-bukit`
2. `bukit-design-tokens`
3. `bukit-theme`
4. `bukit-config`

### Jana templat dari skema kandungan

1. `using-bukit`
2. `bukit-content-to-template`
3. `bukit-config` (untuk skema koleksi)
4. `bukit-templating`
5. `bukit-design-tokens` (untuk gaya visual)

## Nota Penyelenggaraan

- Simpan setiap skill di `src/skills/<skill-name>/SKILL.md`
- Gunakan `description` hanya untuk syarat pencetus, bukan ringkasan generik
- Pusatkan arahan CLI dalam `bukit-cli-reference`
- Pastikan laluan tema, medan konfigurasi, dan parameter CLI sejajar dengan kod sumber dan dokumen pengguna
- Apabila Bukit mendapat keupayaan baharu, tentukan sama ada untuk meluaskan skill sedia ada atau menambah yang baharu dengan sempadan tanggungjawab yang jelas

## Dokumen Berkaitan

- Entri repo: [`README.md`](../../README.md)
- Rujukan Bahasa Inggeris: [`README.md`](../../README.md)
- Panduan pengguna: [`guide/user`](../../guide/user/README.md)
- Panduan pembangun: [`guide/dev`](../../guide/dev/README.md)
- Dokumen reka bentuk skills: [`docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md`](../../docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md)
