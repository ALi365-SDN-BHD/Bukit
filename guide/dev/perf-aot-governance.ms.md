# Nota Tambahan Prestasi / AOT / Tadbir Urus

## Matlamat dan Sempadan
Tadbir urus ini menangani: Kelestarian AOT, Kebolehukuran prestasi.

## Tadbir Urus AOT
Pemuatan plugin serasi AOT:
- `built-in` + `generated`: sentiasa tersedia
- `external-protocol`: Mesra AOT (process/wasm)
- `external` (`plugins/*.dll`): TIDAK tersedia di bawah AOT

Sumber vendored Scriban (`tools/scriban/`) ditampal AOT sepenuhnya (sifar amaran).

## Prestasi Binaan
- Binaan tokokan: langkau halaman tidak berubah melalui perbandingan hash
- `--metrics <path>`: output data pemasaan binaan berstruktur
- `--jobs <n>`: kawal konkurens rendering selari

## Perintah Pengesahan CI
```bash
dotnet publish src/Bukit.Cli -c AOT -r linux-x64 -o out/bukit
./scripts/smoke.ps1
```
