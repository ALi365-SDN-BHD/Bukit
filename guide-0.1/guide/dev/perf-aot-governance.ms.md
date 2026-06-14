# Nota Tambahan Prestasi / AOT / Tadbir Urus

## Matlamat dan Sempadan
Tadbir urus ini menangani: Kelestarian AOT, Kebolehukuran prestasi.

## Tadbir Urus AOT
Pemuatan plugin serasi AOT:
- `built-in` + `generated`: sentiasa tersedia
- `external-protocol`: Mesra AOT (process/wasm)
- `external` (`plugins/*.dll`): TIDAK tersedia di bawah AOT

Sumber vendored Scriban (`tools/scriban/`) ditampal AOT sepenuhnya (sifar amaran).

### Peraturan JSON Source-Gen

Semua panggilan `JsonSerializer.Serialize` / `Deserialize` dalam closure publish mesti
menggunakan overload `JsonSerializerContext` source-gen. Overload berasaskan refleksi
`JsonSerializerOptions` mencetus IL2026/IL3050 dalam NativeAOT dan dilarang.

Apabila jenis model mengandungi `IReadOnlyDictionary<string, object>`, jenis nilai di dalam
kamus akan menjadi `JsonElement` selepas nyahserialisasi source-gen. Panggil
`JsonElementMaterializer.Materialize()` di sempadan nyahserialisasi untuk menukar secara
rekursif nilai `JsonElement` kepada primitif CLR (string/bool/long/double/List/Dictionary).

Penguatkuasaan CI: `scripts/check-aot-warnings.sh` mesti menghasilkan sifar baris `ILC : warning IL\d{4}`.

## Prestasi Binaan
- Binaan tokokan: langkau halaman tidak berubah melalui perbandingan hash
- `--metrics <path>`: output data pemasaan binaan berstruktur
- `--jobs <n>`: kawal konkurens rendering selari

## Perintah Pengesahan CI
```bash
dotnet publish src/Bukit.Cli -c AOT -r linux-x64 -o out/bukit
./scripts/smoke.ps1
```
