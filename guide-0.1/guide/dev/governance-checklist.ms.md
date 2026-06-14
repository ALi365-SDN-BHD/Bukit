# Senarai Semak Tadbir Urus Penyelenggaraan (P2)

Menukar kesimpulan semakan seni bina kepada tindakan boleh laku secara berkala.

## 1) Garis Dasar Baca Badan dan Cache
Kekerapan: Bulanan + sebelum perubahan Content/Engine/Rendering
```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --no-clean --incremental
```

## 2) Tadbir Urus Koleksi dan Lapisan Keserasian
- Laluan utama: `site.collections`
- Laluan keserasian: Peraturan lalai `post/page`

## 3) Semakan Konsistensi Dokumentasi-Aset
Bulanan + sebelum keluaran: `pwsh ./scripts/check-doc-asset-consistency.ps1`

## 4) Kekerapan: Bulanan laksana bahagian 1+3; Suku tahunan semak strategi koleksi.
