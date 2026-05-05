# Sumber Data Modul (mode=data → site.modules)

Modul menyuntik blok data berstruktur ke dalam `site.modules.<type>[]` tanpa menjana laluan.

Pelaksanaan: `src/Bukit.Engine/DataModuleBuilder.cs`

## Konfigurasi
```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

Item dengan `mode: data` tidak menjana laluan; dikumpulkan mengikut `type` dan disuntik ke `site.modules.<type>[]`.

## Medan Modul Disyorkan: `type` (wajib), `title`, `order`, `locale`, `enabled`

## Penggunaan Templat
```scriban
{{ for b in site.modules.banner }}
  <h2>{{ b.title }}</h2>
{{ end }}
```

## Pelbagai Sumber data
Pelbagai sumber `mode: data` digabungkan ke dalam `site.modules`. ID item diawalkan `<sourceKey>:<sourceId>`.

## Tambahan Taksonomi
Sumber `mode: data` dengan `name: categories` atau `name: tags` digunakan untuk taksonomi.
