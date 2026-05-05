# Output Tetap Enjin (Output Bebas Kandungan)

Sebagai tambahan kepada halaman yang dihasilkan oleh penghalaan kandungan, enjin menjana output agregasi tetap.

Pelaksanaan: `src/Bukit.Engine/SiteEngine.cs`

## Output Halaman Tetap
- `/` → `index.html` (templat: `pages/index.html`)
- `/blog/` → `blog/index.html` (templat: `pages/list.html`)
- `/pages/` → `pages/index.html` (templat: `pages/list.html`)

## Salinan Direktori Statik
Setiap varian binaan menyalin `theme.static` sebagaimana adanya ke akar output, dan `theme.assets` ke `assets/`.

Lihat: [built-in-plugins.md](./built-in-plugins.md)
