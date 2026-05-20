# 18 Klon Laman Web: Tukar Mana-mana Laman Langsung ke Tema Bukit

Clone menangkap reka bentuk visual laman web — warna, tipografi, jarak, susun atur — dan menjana tema Bukit yang menghasilkan semula rupa yang sama. Aliran kerja tiga fasa: pengekstrakan → penjanaan → pengesahan.

Dokumen berkaitan: [docs/clone.md](../../docs/clone.md)

## Apa yang Anda Akan Dapat

- Direktori tema Bukit yang sepadan secara visual dengan laman sasaran
- Token reka bentuk yang diekstrak (warna, fon, bayang, skala jarak)
- Analisis susun atur seksyen/komponen
- Aset yang dimuat turun (logo, ikon, imej utama)
- Binaan yang disahkan dengan tema baharu

## Bila Perlu Digunakan

| Senario | Alat |
|---------|------|
| Klon reka bentuk laman langsung | `bukit clone` (halaman ini) |
| Cipta tema baharu dari praset | `bukit theme wizard --preset blog` |
| Pasang tema komuniti | `bukit theme install --registry <name>` |

## Cara Ia Berfungsi

### Fasa 1: Pengekstrakan (Pelayar MCP)

Gunakan alat automasi pelayar (Chrome MCP / Playwright MCP) untuk mengekstrak token reka bentuk:

1. **Tangkapan skrin** — Desktop (1440px), tablet (768px), mudah alih (390px)
2. **Token reka bentuk** (`tokens.json`)
3. **Susun atur halaman** (`page.json`)
4. **Analisis seksyen** (`sections.json`)
5. **Aset** (`assets.json`)

### Fasa 2: Penjanaan (CLI)

```bash
bukit clone \
  --tokens tokens.json \
  --page page.json \
  --sections sections.json \
  --assets assets.json \
  --theme tema-saya
```

### Fasa 3: Pengesahan

```bash
bukit doctor
bukit build
bukit clone --verify
```

## Had

- **Interaksi JavaScript** — Hanya HTML/CSS statik diklon. Animasi dan JS klien tidak direplikasi.
- **Kandungan dinamik** — Kandungan yang diambil melalui API tidak akan ditangkap.

## Langkah Seterusnya

- [12 Rujukan CLI](./12-cli-reference.md)
- [08 Tema & Templat](./08-themes-templates.md)
