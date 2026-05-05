# Semakan Seni Bina Projek (Penjajaran P1)

Dokumen ini menyediakan semakan separa formal seni bina repositori semasa dari perspektif penyelenggara.

## 1. Kesimpulan Semakan
Seni bina keseluruhan (disempadani oleh `bukit.slnx` dan `src/Bukit.*`) kekal matang dan boleh diselenggara. Risiko utama telah beralih dari kod teras kepada sempadan keupayaan dan konsistensi tadbir urus.

## 2. Kekuatan Utama

### 2.1 Sempadan Modul Jelas
```text
CLI → Config → Content → Routing → Rendering → Engine → Plugins → Output
```

### 2.2 Abstraksi Plugin Betul
Bahagikan kepada `derive-pages` + `after-build`, selaras dengan model domain penjana tapak statik.

### 2.3 Penambahbaikan Model Badan
Saluran paip utama menggunakan corak bacaan tertunda `BodyStore + BodyKey`.

## 3. Kelemahan Utama

### 3.1 Bacaan/Cache Badan Skala Besar (Sederhana-Tinggi)
Pemuatan badan telah ditangguhkan tetapi peringkat rendering/carian/RSS masih mencetuskan bacaan pada laluan berbeza.

### 3.2 Tadbir Urus collections vs Lapisan Keserasian (Sederhana)
`collections` adalah laluan utama; peraturan lalai `post/page` adalah lapisan keserasian.

### 3.3 Kebolehlanjutan CLI (Sederhana)
Penghuraian argumen ringan adalah mesra AOT tetapi keupayaan deklaratif perlu penambahbaikan.

## 4. Skor
| Dimensi | Skor |
|---|---|
| Kebolehselenggaraan | 8.6/10 |
| Kebolehlanjutan | 8.1/10 |
| Kebolehujian | 7.6/10 |

## 5. Syor Keutamaan
1. Lengkapkan tadbir urus penanda aras bacaan/cache badan
2. Tumpukan strategi koleksi dan lapisan keserasian
3. Wujudkan semakan konsistensi dokumen-aset
