# Tadbir Urus Dokumentasi

## Tanggungjawab Direktori

| Direktori | Tujuan |
|---|---|
| `README.*` | Halaman pendaratan projek awam |
| `guide/user/*` | Manual operasi untuk pengguna |
| `guide/dev/*` | Rujukan penyelenggara dan penyumbang |
| `guide/ai/*` | Pek prompt AI untuk manusia |
| `src/skills/*` | Lapisan pengetahuan Agent AI |
| `docs/*` | Cadangan produk, laporan audit, nota tadbir urus, analisis panjang |

## Peraturan

1. **README mesti kekal ringkas.** Ia adalah pintu masuk projek, bukan manual.
2. **Rujukan CLI penuh berada dalam `guide/user` atau `guide/dev`.** Jangan salin ke README.
3. **Skema konfigurasi penuh berada dalam `guide/dev`.** Jangan salin ke README atau `guide/user`.
4. **Dokumentasi Skills tidak boleh diduplikasi dalam README atau guide.** `src/skills/*` adalah sumber tunggal untuk pengetahuan agent.
5. **Semua versi bahasa README akar mesti berkongsi susunan bahagian yang sama.**
6. **Semua versi bahasa guide README harus berkongsi hierarki maklumat yang sama.**
7. **Nilai rahsia tidak boleh muncul dalam contoh dokumentasi.** Sentiasa guna nama pemegang tempat seperti `NOTION_TOKEN` atau `YOUR_KEY`.
8. **Token Notion mesti sentiasa didokumenkan sebagai `NOTION_TOKEN`.** Jangan sekali-kali tunjukkan nilai token sebenar.

## Peraturan Fallback Bahasa

Apabila dokumen setempat tidak wujud:

- **Bahasa Inggeris (en)**: "Currently available in [language] only"
- **Bahasa Cina (zh-CN)**: Tiada nota fallback diperlukan kecuali merujuk bahan bukan Cina
- **Bahasa Melayu (ms)**: "Pada masa ini hanya tersedia dalam bahasa [language]"

Gunakan perkataan yang konsisten. Jangan guna label sementara seperti "(Chinese)" dalam tajuk navigasi.

## Prinsip Rujukan Silang

- `guide/user` boleh merujuk `guide/dev` untuk butiran medan/kontrak autoritatif
- `guide/dev` boleh merujuk `docs/` untuk konteks peringkat produk
- `guide/ai` harus merujuk `guide/user` dan `guide/dev` untuk aliran kerja validasi
- `src/skills` tidak boleh merujuk dokumentasi sementara atau provisional
