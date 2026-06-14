# Sumber Tema Git

Bukit menyokong penarikan tema dari repositori Git, membolehkan pengedaran tema dan pengurusan versi. Tiada pendaftaran terpusat diperlukan — hanya isytiharkan URL Git dalam `site.yaml`.

Pelaksanaan:
- `src/Bukit.Engine/ThemeSourceManager.cs`
- `src/Bukit.Config/AppConfig.cs` (ThemeConfig.Source)
- `src/Bukit.Engine/SiteEngine.cs` (BuildVariantAsync)

## Konfigurasi `site.yaml`

```yaml
theme:
  source: "https://github.com/user/bukit-theme.git@v1.2.0"
  name: my-custom    # pilihan: nama subdirektori dalam repo
```

| Medan | Diperlukan | Penerangan |
|------|------|------|
| `theme.source` | Ya | URL repo Git + tag versi pilihan (`@v1.0.0`) |
| `theme.name` | Tidak | Subdirektori tema dalam repo. Jika tidak dinyatakan, gunakan akar repo |

## Penetapan Versi

Versi ditentukan melalui akhiran `@` dalam URL:

```
https://github.com/user/theme.git@v1.0.0   # Tag Git
https://github.com/user/theme.git@abc1234   # hash commit
https://github.com/user/theme.git           # cawangan main/master lalai
```

Apabila tiada versi dinyatakan, cawangan lalai digunakan.

## Cache dan Kebolehhasilan Semula

- **Binaan pertama**: `git clone` ke `.cache/themes/{repo-name}/`
- **Binaan seterusnya**: tema yang dicache **tidak** dikemas kini secara automatik (`git pull` tidak dipanggil). Commit yang telah diperiksa sebelum ini digunakan semula — ini memastikan binaan boleh dihasilkan semula.
- Apabila `@ref` (contohnya, `@v1.0.0`) dinyatakan, Bukit melakukan checkout ke tag/cawangan tepat tersebut dan merekodkan commit yang diselesaikan.
- Tag versi yang hilang menyebabkan kegagalan binaan serta-merta (tiada pengunduran senyap ke cawangan lain).

## Fail Kunci Tema

Selepas checkout berjaya, Bukit menulis `bukit-theme.lock.json` ke direktori cache setempat:

```json
{
  "themes": [
    {
      "source": "https://github.com/user/theme.git",
      "ref": "v1.0.0",
      "commit": "abc123def456..."
    }
  ]
}
```

Pada binaan seterusnya, Bukit mengesahkan bahawa commit yang diperiksa sepadan dengan commit fail kunci yang direkodkan. Jika berbeza, binaan gagal dengan ralat yang jelas — ini mencegah perubahan tema jauh yang tidak dijangka.

Untuk mengemas kini tema terkunci: padam direktori cache atau fail kunci dan bina semula.

## Keutamaan dengan Tema Tempatan

Apabila kedua-dua `theme.source` dan direktori `themes/` tempatan dikonfigurasi:

- `theme.source` diutamakan — tarikan Git dicuba dahulu
- Jika tarikan Git gagal (ralat rangkaian, repo tidak sah), pengunduran ke direktori `themes/` tempatan
- `theme.name` hanya mencari subdirektori dalam repo dan tidak mempengaruhi keutamaan tempatan

## Keperluan Persekitaran

- Persekitaran binaan mesti mempunyai CLI `git` dipasang
- Repositori mesti boleh diakses secara awam (atau kunci SSH dikonfigurasi)
- Tamat masa klon/checkout: 120 saat
