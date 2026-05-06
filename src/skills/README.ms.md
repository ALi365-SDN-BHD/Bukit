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
  bukit-theme/            # Direktori tema dan aset statik
  bukit-templating/       # Pembangunan templat Scriban
  bukit-notion/           # Sumber kandungan Notion
  bukit-routing/          # Routing URL dan permalink
  bukit-i18n/             # Tapak berbilang bahasa
  bukit-plugins-debug/    # Plugin, incremental build, diagnostik
```

## Tanggungjawab Skill

| Skill | Tanggungjawab | Kegunaan biasa |
|---|---|---|
| `using-bukit` | Skill gerbang yang mengenal pasti kerja Bukit dan menghala ke sub-skill | Pengguna menyebut "using bukit" atau tugasan jelas khusus untuk Bukit |
| `bukit-cli-reference` | Pengesanan CLI, panduan pemasangan, rujukan arahan, tafsiran output dan exit code | Menjalankan `bukit build`, `doctor`, `preview`, `theme`, `webhook`, dan arahan berkaitan |
| `bukit-config` | Struktur `site.yaml`, templat senario, dan penerangan medan | Mencipta atau menyunting konfigurasi, menerangkan medan, membaiki ralat validasi |
| `bukit-theme` | Struktur direktori tema, aset statik, dan parameter tema | Mencipta atau memindahkan tema, membaiki isu CSS atau aset statik, menggunakan `theme.params` |
| `bukit-templating` | Sintaks Scriban, pewarisan layout, capaian data, dan corak templat | Menulis templat halaman, halaman senarai, pagination, atau membaiki ralat render templat |
| `bukit-notion` | Integrasi Notion, pemetaan property, render blok, dan penyetempatan imej | Menggunakan Notion sebagai CMS atau menyelesaikan isu fetch dan pemetaan |
| `bukit-routing` | Permalink, route collection, pengekodan URL, dan tingkah laku output path | Menyesuaikan URL, membaiki 404, menyelesaikan konflik route, mengkonfigurasi halaman senarai |
| `bukit-i18n` | Pengesanan bahasa, build berasingan mengikut bahasa, gabungan sitemap/RSS/search | Membina tapak berbilang bahasa dan menyahpepijat isu penukaran bahasa atau output gabungan |
| `bukit-plugins-debug` | Kitar hayat plugin, incremental build, diagnostik prestasi, dan troubleshooting | Plugin tidak berjalan, output build tidak betul, atau prestasi build merosot |

## Peraturan Muatkan

Skill ini direka untuk digabungkan dengan sempadan yang jelas:

1. Mulakan dari `using-bukit` apabila tugasan sudah pasti tugasan Bukit
2. Gunakan `bukit-cli-reference` untuk setiap langkah berkaitan arahan dan elakkan penduaan panduan CLI di tempat lain
3. Anggap `bukit-config` sebagai pengetahuan latar untuk `bukit-theme`, `bukit-notion`, `bukit-routing`, `bukit-i18n`, dan `bukit-plugins-debug`
4. Baca `bukit-theme` sebelum `bukit-templating` apabila kerja templat bergantung pada struktur tema

Satu aliran kerja biasa kelihatan seperti ini:

```text
using-bukit
  -> bukit-cli-reference
  -> bukit-config
  -> bukit-theme / bukit-notion / bukit-routing / bukit-i18n / bukit-plugins-debug
  -> bukit-templating
```

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

### Nyahpepijat isu build atau plugin

1. `using-bukit`
2. `bukit-plugins-debug`
3. `bukit-config`
4. `bukit-cli-reference`

## Nota Penyelenggaraan

- Simpan setiap skill di `src/skills/<skill-name>/SKILL.md`
- Gunakan `description` hanya untuk syarat pencetus, bukan ringkasan umum
- Pusatkan semua arahan CLI dalam `bukit-cli-reference`
- Pastikan laluan tema, medan konfigurasi, dan parameter CLI selari dengan kod dan dokumen untuk pengguna
- Apabila Bukit mendapat kemampuan baharu, tentukan sama ada perlu mengembangkan skill sedia ada atau menambah skill baharu dengan sempadan tanggungjawab yang jelas

## Dokumen Berkaitan

- Pintu masuk repo: [`README.ms.md`](../../README.ms.md)
- Rujukan Cina: [`README.zh-CN.md`](../../README.zh-CN.md)
- Panduan pengguna: [`guide/user`](../../guide/user/README.ms.md)
- Panduan pembangun: [`guide/dev`](../../guide/dev/README.ms.md)
- Dokumen reka bentuk skills: [`docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md`](../../docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md)
