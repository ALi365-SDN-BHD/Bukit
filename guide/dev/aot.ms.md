# Mod Binaan AOT dan Bukan-AOT

Projek ini menyokong mod Non-AOT (JIT) dan NativeAOT dengan perbezaan jelas dalam keupayaan plugin, laluan terbitan, dan ciri runtime.

## Pemilihan Mod
- Non-AOT (JIT): Pembangunan, lelaran pantas, pemuatan plugin DLL luaran.
- AOT: Penerapan pengeluaran, pengoptimuman permulaan-sejuk/memori, artifak fail tunggal mudah alih.

Tukar melalui `src/Bukit.Cli/Bukit.Cli.csproj`: `Configuration=AOT` mendayakan `PublishAot=true`.

## Perbezaan Tingkah Laku Plugin
- AOT: `built-in` + `generated` + `external-protocol`
- Non-AOT: `built-in` + `generated` + `external` (imbas `plugins/*.dll`) + `external-protocol`

Plugin DLL luaran memerlukan Non-AOT. Di bawah AOT, gunakan `external-protocol`.

## Keserasian Scriban AOT
Sumber vendored Scriban (`tools/scriban/`) telah ditampal AOT sepenuhnya: sifar amaran AOT.

Lihat: [external-plugin-protocol.md](./external-plugin-protocol.md), [plugins.md](./plugins.md)
