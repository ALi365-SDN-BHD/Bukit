# Menyumbang kepada Bukit

Terima kasih atas minat anda untuk menyumbang kepada Bukit.

## Bermula

1. Pasang [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) atau lebih baru
2. Klon repositori dan bina:

```bash
git clone <repo-url>
cd Bukit
dotnet build bukit-core.slnx -c Release
```

3. Jalankan gate sumbangan pantas:

```bash
bash scripts/quality-gate.sh Release
```

`scripts/quality-gate.sh` ialah pembungkus keserasian untuk `scripts/gates/ci-fast.sh`. Ia menyemak konsistensi dokumentasi, sempadan workflow aktif, kontrak dokumentasi konfigurasi, penyegerakan dokumentasi CLI, metadata skill, penyegerakan README, dan kontrak skrip Core CLI. Ia bukan gate keluaran penuh.

4. Untuk perubahan kod, jalankan ujian:

```bash
dotnet test bukit-test.slnx -c Release
```

Peta dokumentasi pembangun semasa ada di [guide/dev/README.md](guide/dev/README.md).

## Gaya Kod

- Projek ini menguatkuasakan `TreatWarningsAsErrors` dan `EnforceCodeStyleInBuild`
- Formatkan kod sebelum commit:

```bash
dotnet format bukit-core.slnx --verify-no-changes
```

- Kod C# mengikut konvensyen dalam [.editorconfig](.editorconfig)
- Fail Markdown, YAML, JSON, Shell, dan PowerShell menggunakan UTF-8 dengan LF

## Seni Bina

Titik masuk pembangun utama didokumenkan dalam [guide/dev/README.md](guide/dev/README.md).

Dokumentasi seni bina utama:
- [guide/dev/architecture.md](guide/dev/architecture.md) — tanggungjawab dan kebergantungan modul
- [guide/dev/release.md](guide/dev/release.md) — sempadan CI, ujian, dan gate keluaran
- [guide/dev/release-checklist.md](guide/dev/release-checklist.md) — senarai semak khusus keluaran
- [guide/dev/documentation-governance.md](guide/dev/documentation-governance.md) — tadbir urus dokumentasi

## Pengujian

- Ujian unit berada dalam `tests/` dan menggunakan xUnit
- Projek ujian Core disenaraikan oleh `scripts/checks/core-tests.sh`
- Titik masuk ujian asap ialah `scripts/smoke.sh` dan `scripts/smoke/core.sh`
- Lihat [guide/dev/testing.md](guide/dev/testing.md) untuk strategi pengujian

## Keserasian AOT

Projek ini diterbitkan sebagai Native AOT. Semua kod baharu mesti serasi AOT:
- Elakkan refleksi terhadap jenis yang terjejas oleh trimming
- Untuk perubahan Scriban, lihat nota AOT dalam [guide/dev/aot.md](guide/dev/aot.md)
- Pembungkusan Native AOT milik keluaran menggunakan `scripts/build/package-native-aot.sh`

## Proses Pull Request

1. Kemas kini dokumentasi jika perubahan mempengaruhi tingkah laku pengguna
2. Jalankan `bash scripts/quality-gate.sh Release` secara tempatan dan pastikan gate dokumentasi serta kontrak pantas lulus
3. Untuk perubahan kod, jalankan ujian sasaran dahulu, kemudian `BUKIT_CI_FULL_SKIP_FAST=1 bash scripts/gates/ci-full.sh Release` sebelum serahan
4. Artifak keluaran, Native AOT, ujian asap, dan pengesahan keselamatan ialah semakan khusus keluaran; jalankan hanya apabila perubahan menyentuh permukaan tersebut
5. GitHub Actions menggunakan `.github/workflows/ci.yaml` untuk pull request dan push cawangan
6. Rebase ke cawangan main sebelum mencipta PR

## Lesen

Dengan menyumbang, anda bersetuju bahawa sumbangan anda akan dilesenkan di bawah Lesen MIT.
