# Terbit dan Sebarkan

Dua lapisan: 1) Terbitkan CLI bukit; 2) Gunakan bukit untuk membina dan menyebarkan tapak statik.

## Artifak Tapak
Keutamaan direktori output `bukit build`:
1. CLI `--output <dir>`
2. `build.output` dalam `site.yaml`
3. Lalai `dist`

## Artifak CLI
**AOT** (Linux x64): `dotnet publish src/Bukit.Cli -c Release -r linux-x64 -o out/bukit /p:PublishAot=true`
**Non-AOT**: `dotnet publish src/Bukit.Cli -c Release -o out/bukit`

## Penyahgunaan GitHub Pages
Templat aliran kerja: [`.github/workflows/release.yml`](../../.github/workflows/release.yml)

### Peraturan baseUrl
- Tapak pengguna/org (`owner.github.io`): `baseUrl=/`
- Tapak repo (`owner.github.io/repo`): `baseUrl=/repo`

## Hos Statik Lain
Selagi `build.output` diterbitkan sebagai akar statik. Tetapkan `site.baseUrl` untuk sub-laluan.

## Soalan Lazim
1. Halaman 404 selepas penyahgunaan: Semak baseUrl
2. Pautan sitemap/rss salah: Semak `site.url`/`--site-url`
3. Plugin berfungsi setempat tetapi tidak selepas terbit: AOT melumpuhkan pemuatan plugin DLL luaran

