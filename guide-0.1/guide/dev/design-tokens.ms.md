# Rujukan Token Reka Bentuk

Token Reka Bentuk adalah atom visual tema, ditakrifkan dalam `tokens.yaml` (warna, fon, jejari sempadan, jarak, pembolehubah susun atur), dan dijana secara automatik sebagai CSS custom properties semasa binaan.

Rujukan pelaksanaan:
- `src/Bukit.Theme/Models/ThemeTokens.cs`
- `src/Bukit.Theme/ThemeTokensLoader.cs`
- `src/Bukit.Theme/ThemeTokensProcessor.cs`

## Format tokens.yaml

```yaml
colors:
  primary: "#0b5fff"
  accent: "#0f7b6c"
  bg: "#ffffff"
  surface: "#f8fafc"
  text: "#1a1a2e"
  text_muted: "#6b7280"
  border: "#e5e7eb"

font:
  family_base: "'Inter', system-ui, sans-serif"
  family_heading: "'Inter', system-ui, sans-serif"
  size_base: "1rem"
  size_sm: "0.875rem"
  size_lg: "1.125rem"
  size_xl: "1.25rem"
  size_2xl: "1.5rem"

radius:
  sm: "4px"
  md: "8px"
  lg: "12px"
  full: "9999px"

spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  section: "64px"

layout:
  content_max: "720px"
  wide_max: "1200px"
  header_height: "64px"
```

### Medan Peringkat Atas

| Medan | Penerangan | Awalan CSS |
|---|---|---|
| `colors` | Pembolehubah warna | `--color-*` |
| `font` | Pembolehubah berkaitan fon | `--font-*` |
| `radius` | Pembolehubah jejari sempadan | `--radius-*` |
| `spacing` | Pembolehubah jarak | `--spacing-*` |
| `layout` | Pembolehubah susun atur | `--layout-*` |

Setiap medan menggunakan kekunci `snake_case`; pembolehubah CSS yang dijana menggunakan `kebab-case` (garis bawah diganti dengan tanda sempang).

### Sintaks Token Bersarang (Disyorkan)

Untuk sokongan deep merge, token juga boleh ditulis dalam YAML bersarang:

```yaml
colors:
  brand:
    primary: "#0b5fff"
    accent: "#0f7b6c"
  neutral:
    bg: "#ffffff"
    text: "#1a1a2e"
```

Ini diratakan secara automatik kepada kekunci dipisahkan titik semasa pemuatan (cth., `brand.primary`, `neutral.bg`).

## Peraturan Penjanaan CSS

`ThemeTokensProcessor.GenerateCss()` menukar token kepada:

```css
:root {
  --color-primary: #0b5fff;
  --color-accent: #0f7b6c;
  --color-bg: #ffffff;
  --color-surface: #f8fafc;
  --color-text: #1a1a2e;
  --color-text-muted: #6b7280;
  --color-border: #e5e7eb;
  --font-family-base: 'Inter', system-ui, sans-serif;
  --font-family-heading: 'Inter', system-ui, sans-serif;
  --font-size-base: 1rem;
  --font-size-sm: 0.875rem;
  --font-size-lg: 1.125rem;
  --font-size-xl: 1.25rem;
  --font-size-2xl: 1.5rem;
  --radius-sm: 4px;
  --radius-md: 8px;
  --radius-lg: 12px;
  --radius-full: 9999px;
  --spacing-xs: 4px;
  --spacing-sm: 8px;
  --spacing-md: 16px;
  --spacing-lg: 24px;
  --spacing-xl: 32px;
  --spacing-section: 64px;
  --layout-content-max: 720px;
  --layout-wide-max: 1200px;
  --layout-header-height: 64px;
}
```

Peraturan penukaran kekunci: `snake_case` → `kebab-case`, diawali dengan nama medan. Cth., `colors.primary` → `--color-primary`.

## Laluan Output

Fail CSS yang dijana dikeluarkan ke:

```
dist/assets/css/theme-tokens.css
```

Semasa binaan, enjin mengesan tema berasaskan komponen (`theme.yaml` wujud) dan menjalankan penjanaan token secara automatik, mencatat log:

```
event=tokens.generated output=dist/assets/css/theme-tokens.css
```

## Pewarisan Token & Deep Merge

Apabila tema anak mewarisi tema induk melalui `extends`, token digabungkan menggunakan `ThemeTokens.DeepMerge()`:

- **Keutamaan anak**: token tema anak mengatasi induk dengan kekunci yang sama
- **Tambahan induk**: kekunci yang tidak ditakrifkan dalam anak diwarisi daripada induk
- **Deep merge**: struktur token bersarang (kekunci dipisahkan titik seperti `brand.primary`) dibina semula menjadi pokok dan digabungkan secara rekursif — `brand.primary` anak hanya mengatasi daun tertentu itu, mengekalkan `brand.secondary` induk

### Perbandingan Tingkah Laku Gabungan

Diberikan token induk:
```yaml
colors:
  brand:
    primary: "#000000"
    secondary: "#333333"
```

Dan token anak:
```yaml
colors:
  brand:
    primary: "#ff0000"
```

| Mod Gabungan | Hasil `brand.primary` | Hasil `brand.secondary` |
|---|---|---|
| Cetek (`Merge`) | `#ff0000` | Dikekalkan (`#333333`) |
| Dalam (`DeepMerge`) | `#ff0000` | Dikekalkan (`#333333`) |

Untuk struktur kekunci rata, kedua-dua mod berkelakuan sama. Deep merge menyediakan keselamatan tambahan untuk struktur bersarang di mana kekunci perantaraan mungkin berlanggar dengan nilai daun.

### Aliran Pemuatan

1. Muatkan `tokens.yaml` tema anak
2. Muatkan `tokens.yaml` tema induk (jika `extends` ditetapkan)
3. Ratakan struktur YAML bersarang kepada kekunci dipisahkan titik
4. Panggil `child.DeepMerge(parent)` — nilai anak mengatasi induk pada peringkat daun

## Menggunakan Token dalam Templat Scriban

Token tidak disuntik terus ke dalam templat sebagai pembolehubah Scriban. Pendekatan yang disyorkan adalah memasukkannya melalui `<link>` dalam `base.html`:

```html
<link rel="stylesheet" href="{{ site.base_url }}/assets/css/theme-tokens.css" />
```

Atau CSS custom properties sebaris dalam templat halaman:

```html
<style>
  .custom-banner {
    background: var(--color-primary);
    padding: var(--spacing-lg);
    border-radius: var(--radius-md);
  }
</style>
```

## Menggunakan Token dalam CSS

`style.css` tema boleh merujuk terus CSS custom properties:

```css
.card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
}

.card-title {
  color: var(--color-primary);
  font-family: var(--font-family-heading);
  font-size: var(--font-size-lg);
}

.hero {
  max-width: var(--layout-wide-max);
  padding: var(--spacing-section) var(--spacing-lg);
}
```