# Senarai Semak Terbitan

Jalankan senarai semak ini sebelum setiap terbitan awam (preview, beta, atau stabil).

## Dokumentasi

- [ ] Versi bahasa README diselaraskan (`.md`, `.zh-CN.md`, `.ms.md`)
- [ ] Rujukan silang `guide/user` divalidasi
- [ ] Rujukan silang `guide/dev` divalidasi
- [ ] Pek prompt `guide/ai` divalidasi
- [ ] Tiada rujukan lapuk kepada SiteGen (nama projek lama)
- [ ] Sempadan BukitJalil adalah jelas (tidak disenaraikan sebagai teras)
- [ ] Dokumen Skills (`src/skills/*`) dipaut dari README tetapi tidak diduplikasi

## Binaan

- [ ] `dotnet build bukit.slnx -c Release` lulus
- [ ] `dotnet test` lulus (semua projek)
- [ ] Skrip smoke lulus pada tapak contoh (`examples/starter/`)
- [ ] Binaan AOT menghasilkan sifar amaran

## Keselamatan

- [ ] Tiada token, kunci, atau rahsia dalam mana-mana fail dokumentasi
- [ ] `NOTION_TOKEN` adalah satu-satunya rujukan auth Notion dalam dokumen
- [ ] Contoh token webhook menggunakan pemegang tempat sahaja (cth. `YOUR_WEBHOOK_SECRET`)
- [ ] Semua URL imej adalah relatif atau dari domain yang dibenarkan

## Skop Kestabilan

- [ ] `public-preview-scope.ms.md` adalah terkini
- [ ] Ciri pratonton ditandakan dengan jelas
- [ ] Peta jalan tidak terlalu menjanjikan keupayaan yang belum dihantar
- [ ] Bahagian status projek (dalam README akar) adalah tepat

## Versi

- [ ] Nombor versi dinaikkan (jika berkenaan)
- [ ] Entri changelog sepadan dengan perubahan sebenar
- [ ] Perubahan yang memecahkan didokumenkan dengan panduan migrasi
