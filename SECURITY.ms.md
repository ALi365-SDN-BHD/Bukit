# Dasar Keselamatan

## Versi Disokong

| Versi  | Disokong           |
|--------|--------------------|
| 1.0.x  | :white_check_mark: |

## Melaporkan Kerentanan

Jika anda menemui kerentanan keselamatan dalam Bukit, sila laporkan secara peribadi.

**Jangan buka isu awam.** Sebaliknya, hantar butiran kepada penyelenggara.

Kami akan mengakui laporan anda dalam masa 7 hari dan bertujuan untuk menyediakan pembaikan dalam masa 30 hari.

## Pertimbangan Keselamatan

### Webhook (`bukit webhook`)

Pelayan webhook menerima permintaan HTTP masuk dan mencetuskan peristiwa `repository_dispatch` GitHub. Untuk menggunakannya dengan selamat:

- Sentiasa tetapkan `BUKIT_WEBHOOK_TOKEN` untuk mengesahkan permintaan masuk
- Gunakan HTTPS dalam penerapan pengeluaran (contohnya, di belakang proksi terbalik)
- Hadkan kadar dengan pengehad kadar terbina dalam (10 permintaan seminit)
- Lihat [guide/dev/webhook.md](guide/dev/webhook.md) untuk panduan penerapan penuh

### Token API Notion

Token integrasi Notion adalah sensitif. Simpan dalam pembolehubah persekitaran atau stor kredensial selamat:

```bash
export BUKIT_NOTION_TOKEN=secret_xxx
```

Jangan commit token ke kawalan versi.

### Plugin Luaran

Plugin luaran berjalan sebagai proses berasingan atau modul WASM. Hanya gunakan plugin dari sumber yang dipercayai.
