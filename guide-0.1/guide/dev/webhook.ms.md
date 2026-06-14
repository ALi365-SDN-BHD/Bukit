# Webhook (Pencetus dan Kekangan Keselamatan)

Subperintah webhook menukar peristiwa luaran kepada `repository_dispatch` GitHub.

Pelaksanaan: `src/Bukit.Cli/Commands/WebhookCommand.cs`

## Penggunaan Asas
```bash
dotnet run --project src/Bukit.Cli -c Release -- webhook --repo owner/repo --port 8787 --path /webhook/notion --event bukit_notion
```

## Parameter
| Parameter | Lalai | Penerangan |
|---|---|---|
| `--host <host>` | `localhost` | Alamat dengar |
| `--port <port>` | `8787` | Port dengar |
| `--path <path>` | `/webhook/notion` | Laluan permintaan |
| `--repo <owner/repo>` | - | Repositori GitHub |
| `--event <event_type>` | `bukit_notion` | Jenis peristiwa |

## Pembolehubah Persekitaran Diperlukan
| Pembolehubah | Tujuan |
|---|---|
| `BUKIT_WEBHOOK_TOKEN` | Token pengesahan masuk |
| `BUKIT_GITHUB_TOKEN` (atau `GITHUB_TOKEN`) | Token API GitHub |

## Kekangan Keselamatan
Mesti POST; padanan laluan tepat; `X-Sitegen-Token` mesti sama dengan `BUKIT_WEBHOOK_TOKEN`.
