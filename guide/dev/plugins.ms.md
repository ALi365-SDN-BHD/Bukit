# Plugin Sistem (derive-pages / after-build)

Plugin adalah titik sambungan utama Bukit.

Pelaksanaan: `src/Bukit.Engine/Plugins/PluginRunner.cs`, `src/Bukit.Engine/Plugins/PluginRegistry.cs`

## Kitaran Hayat
- **DerivePages** (`IDerivePagesPlugin`): Menerbitkan halaman tambahan dari kandungan routed. Dasar konflik: `fail|warn|last-wins`.
- **AfterBuild** (`IAfterBuildPlugin`): Menjana fail tambahan selepas semua halaman dihasilkan.

## Dasar Kegagalan: `site.pluginFailMode`
- `strict`: Ralat plugin menggugurkan binaan
- `warn`: Log ralat dan teruskan

## Sumber Plugin
1. **built-in**: Dibundle dengan enjin (taxonomy/sitemap/rss/search-index/pagination/archive)
2. **generated**: Plugin dijana masa kompilasi (serasi AOT)
3. **external**: Pemuatan `plugins/*.dll` runtime (Non-AOT sahaja)
4. **external-protocol**: Plugin protokol `stdin/stdout + JSON` (serasi AOT)

## Keselamatan external-protocol

Plugin protokol luaran berjalan dengan **pengasingan persekitaran**: pemboleh ubah persekitaran hos dikosongkan, dan hanya `BUKIT_PLUGIN_NAME`, `BUKIT_PLUGIN_HOOK`, `BUKIT_PROJECT_ROOT`, dan `BUKIT_OUTPUT_DIR` disuntik. Gunakan `allowEnvironment` dalam `site.externalPlugins` untuk mendedahkan pemboleh ubah hos tambahan secara eksplisit.

Had output (`maxStdoutBytes` / `maxStderrBytes`) mengehadkan stdout/stderr plugin; melebihi had membunuh proses. Semua output plugin dikesan dengan metadata plugin/hook/path/hash dalam manifes binaan, dan output lapuk dari binaan sebelumnya dibersihkan secara automatik semasa binaan tambahan.

Lihat [External Plugin Protocol](./external-plugin-protocol.md) untuk skema permintaan/tindak balas penuh dan butiran perundingan protokol.

## Penemuan generated

## Tertib Pelaksanaan Plugin
Plugin yang melaksanakan `IOrderedPlugin` mengikut `Order` dari terkecil ke terbesar (lalai 0).

## Konfigurasi Plugin (`site.plugins`)
```yaml
site:
  plugins:
    path-report:
      enabled: true
      options: {}
```

## Gambaran Keseluruhan Plugin Terbina Dalam
| Plugin | Jenis | Output |
|---|---|---|
| taxonomy | DerivePages + AfterBuild | Halaman `/tags/`, `/categories/` |
| sitemap | AfterBuild | `sitemap.xml` |
| rss | AfterBuild | `rss.xml` |
| search-index | AfterBuild | `search.json` |
