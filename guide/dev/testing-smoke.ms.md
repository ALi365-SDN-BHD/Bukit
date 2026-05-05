# Pengujian dan Asap (Smoke)

Strategi pengujian repositori ini cenderung kepada "penerimaan boleh laku".

## Titik Masuk Sedia Ada
- Asap satu klik: `scripts/smoke.ps1`, `scripts/smoke.sh`
- Penerimaan terperinci: Bahagian "v2 Acceptance and Testing" dalam `README.md`

## Struktur Minimum untuk Kes Penerimaan Baharu
1. Prasyarat (pembolehubah persekitaran, konfigurasi contoh)
2. Langkah (perintah build/doctor/preview)
3. Penegasan (struktur output, fail utama wujud, laluan boleh diakses)
4. Pembersihan (pengendalian clean dan cache)
