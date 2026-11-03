# MySports

Calendario sportivo senza pubblicità: prossimi eventi e risultati di Serie A, Formula 1 e MotoGP.

## Architettura

Nessun server: un importatore C# gira su GitHub Actions ogni 3 ore, scarica i dati dalle fonti esterne
e pubblica file JSON statici su GitHub Pages. L'app mobile legge quei file.

```
GitHub Actions (cron) → MySports.Importer (C#) → JSON → branch gh-pages → GitHub Pages → app
```

## Struttura

```
src/MySports.Importer/
  Models/        formato comune degli eventi (SportEvent, Competition)
  Sources/       una classe per fonte dati (JolpicaF1Source, ...)
  Output/        scrittura dei file JSON
.github/workflows/update-data.yml   esecuzione programmata e pubblicazione
```

## File pubblicati

| File | Contenuto |
|---|---|
| `meta.json` | competizioni disponibili e stagione corrente |
| `upcoming.json` | eventi programmati o in corso nei prossimi 14 giorni |
| `results.json` | eventi conclusi negli ultimi 14 giorni |
| `{competizione}/{stagione}.json` | stagione completa, es. `f1/2026.json` |

Ogni evento ha un campo `videos` (per ora vuoto) che verrà riempito con gli highlights YouTube.
I video già trovati vengono conservati tra un'esecuzione e l'altra.

## Eseguire in locale

Richiede .NET 10 SDK.

```
dotnet run --project src/MySports.Importer -- --out out
```

## Pubblicazione su GitHub

1. Crea un repository **pubblico** `MySports` su GitHub e carica il codice.
2. Scheda **Actions** → workflow "Aggiorna dati" → **Run workflow** (crea il branch `gh-pages`).
3. **Settings → Pages** → Source: "Deploy from a branch" → branch `gh-pages`, cartella `/ (root)`.
4. I dati saranno su `https://<utente>.github.io/MySports/upcoming.json`.

GitHub Pages è gratuito per i repository pubblici. Le chiavi API (football-data.org, YouTube) vanno
in **Settings → Secrets and variables → Actions**, mai nel codice.

## Prossimi passi

- [ ] Serie A (football-data.org, serve la chiave API)
- [ ] Highlights YouTube dai canali ufficiali (feed RSS)
- [ ] App Expo: liste "Prossimi" e "Risultati"
- [ ] MotoGP
