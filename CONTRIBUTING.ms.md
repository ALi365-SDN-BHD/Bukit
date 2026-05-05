# Menyumbang kepada Bukit

Terima kasih atas minat anda untuk menyumbang kepada Bukit.

## Bermula

1. Pasang [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) atau lebih baru
2. Klon repositori dan bina:

```bash
git clone <repo-url>
cd Bukit
dotnet build bukit.slnx -c Release
```

3. Jalankan ujian:

```bash
dotnet test bukit.slnx -c Release
```

4. Jalankan ujian asap (Windows):

```powershell
powershell -ExecutionPolicy Bypass -File scripts/smoke.ps1
```

Untuk panduan pembangun baharu: [guide/dev/new-developer-30min.md](guide/dev/new-developer-30min.md).

## Gaya Kod

- Projek ini menguatkuasakan `TreatWarningsAsErrors` dan `EnforceCodeStyleInBuild`
- Formatkan kod sebelum commit:

```bash
dotnet format bukit.slnx --verify-no-changes
```

## Seni Bina

- [guide/dev/architecture.md](guide/dev/architecture.md) — tanggungjawab modul
- [guide/dev/code-wiki.md](guide/dev/code-wiki.md) — struktur repositori
- [guide/dev/governance-checklist.md](guide/dev/governance-checklist.md) — senarai semak pra-keluaran

## Pengujian

- Ujian unit dalam `tests/`, menggunakan xUnit
- Ujian asap: `scripts/smoke.ps1`, `scripts/smoke.sh`

## Keserasian AOT

Projek ini diterbitkan sebagai Native AOT. Semua kod baharu mesti serasi AOT.

## Proses Pull Request

1. Kemas kini dokumentasi jika perubahan mempengaruhi tingkah laku pengguna
2. Jalankan ujian penuh dan ujian asap
3. Pastikan pemformatan kod lulus
4. Rebase ke cawangan main sebelum mencipta PR

## Lesen

Dengan menyumbang, anda bersetuju bahawa sumbangan anda akan dilesenkan di bawah Lesen MIT.
