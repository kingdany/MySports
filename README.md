# MySports

Calendario sportivo senza pubblicità: prossimi eventi e risultati di Serie A, Champions League, Formula 1 e MotoGP.

## Architettura

Nessun server: un importatore C# gira su GitHub Actions ogni 3 ore, scarica i dati dalle fonti esterne
e pubblica file JSON statici su GitHub Pages. L'app mobile legge quei file.

```
GitHub Actions (cron) → MySports.Importer (C#) → JSON → branch gh-pages → GitHub Pages → app
```

## Struttura

```
app/                 app mobile Expo (React Native + TypeScript)
  app/               schermate (Expo Router): (tabs)/index = Prossimi, (tabs)/results = Risultati, (tabs)/standings = Classifiche, event/[id] = dettaglio
  components/        liste e righe evento, scelta di competizione e stagione, video
  lib/               tipi, formattazione, caricamento dei JSON, scelte salvate
src/MySports.Importer/
  Models/        formato comune di eventi e classifiche (SportEvent, Competition, Standings)
  Sources/       fonti dati (JolpicaF1Source, OpenF1Client, FootballDataSource, YouTubeHighlights, ...)
  Output/        scrittura dei file JSON
.github/workflows/update-data.yml   esecuzione programmata e pubblicazione
```

## File pubblicati

| File | Contenuto |
|---|---|
| `meta.json` | competizioni disponibili e stagione corrente |
| `upcoming.json` | eventi programmati o in corso nei prossimi 14 giorni |
| `results.json` | eventi conclusi negli ultimi 14 giorni |
| `{competizione}/{stagione}.json` | stagione completa (eventi e classifiche), es. `f1/2026.json`, `serie-a/2026-27.json`; anche le stagioni passate (F1 dal 1950, Serie A dal 2023-24, Champions League dal 2024-25) |

L'app legge `meta.json` e i file di stagione; `upcoming.json` e `results.json` vengono pubblicati ma non sono usati dall'app.

Ogni evento ha un campo `videos` con gli highlights YouTube (F1, Serie A e Champions League).
I video già trovati vengono conservati tra un'esecuzione e l'altra. A ogni giro si cercano solo quelli delle partite
finite negli ultimi 3 giorni; per quelle più vecchie si veda "Recuperare i video vecchi".

## Eseguire in locale

Richiede .NET 10 SDK.

```
dotnet run --project src/MySports.Importer -- --out out
```

Le chiavi vanno nell'ambiente oppure in un file `.env` nella radice del repository (escluso da Git):

- `FOOTBALL_DATA_API_KEY` per il calcio; senza chiave Serie A e Champions League non si aggiornano e l'importatore termina con errore, ma la F1 viene comunque salvata.
- `YOUTUBE_API_KEY` per gli highlights; senza chiave questo passaggio viene saltato.

## Importare le stagioni passate

Le stagioni concluse non cambiano: si importano una volta sola.
Da GitHub: **Actions → Aggiorna dati → Run workflow**, campo "Stagioni passate" (es. `1950-2025`).
In locale:

```
dotnet run --project src/MySports.Importer -- --competition f1 --seasons 1950-2025 --out out
```

Per aggiungere solo le classifiche a stagioni già importate: opzione "Solo classifiche" nel workflow, oppure `--standings-only`.

Jolpica consente ~500 richieste l'ora: con molte stagioni l'importatore attende da solo lo sblocco.
Per le stagioni vecchie mancano orari (`dateOnly`) e, prima del 1994 circa, le qualifiche.

Per il calcio le stagioni si elencano per nome (il piano gratuito di football-data.org arriva fino al 2023-24):

```
dotnet run --project src/MySports.Importer -- --competition serie-a --seasons 2023-24,2024-25,2025-26 --out out
dotnet run --project src/MySports.Importer -- --competition champions-league --seasons 2024-25,2025-26 --out out
```

Per la Champions League si parte dal 2024-25: per il 2023-24 l'API non separa i gironi e dà un'unica classifica di 32 squadre.

## Recuperare i video vecchi

Serve una volta sola, per una competizione nuova o per le stagioni passate.
Da GitHub: **Actions → Aggiorna dati → Run workflow**, casella "Recupera i video". Con il campo "Stagioni passate" vale
per quelle stagioni della competizione indicata, altrimenti per le stagioni correnti di tutte le competizioni.
In locale:

```
dotnet run --project src/MySports.Importer -- --competition serie-a --seasons 2025-26 --recover-videos --out out
```

Ogni canale viene riletto fino alla stagione richiesta (circa 20 unità di quota YouTube al mese da risalire).

## Provare l'app sul telefono

1. Installa **Expo Go** dallo store (Android/iOS).
2. `cd app`, `npm install` (solo la prima volta), poi `npx expo start`.
3. Inquadra il QR code con Expo Go (Android) o con la fotocamera (iOS).
   Telefono e PC devono essere sulla stessa rete Wi-Fi; in alternativa `npx expo start --tunnel`.

## Pubblicazione su GitHub

1. Crea un repository **pubblico** `MySports` su GitHub e carica il codice.
2. Scheda **Actions** → workflow "Aggiorna dati" → **Run workflow** (crea il branch `gh-pages`).
3. **Settings → Pages** → Source: "Deploy from a branch" → branch `gh-pages`, cartella `/ (root)`.
4. I dati saranno su `https://<utente>.github.io/MySports/upcoming.json`.

GitHub Pages è gratuito per i repository pubblici. Le chiavi API (football-data.org, YouTube) vanno
in **Settings → Secrets and variables → Actions**, mai nel codice.

## Prossimi passi

- [ ] Calcio: altre competizioni
- [ ] Calcio: marcatori nel dettaglio partita: non disponibili nel piano gratuito (piano "Free + Deep Data" a 29 €/mese, oppure un'altra fonte da valutare)
- [ ] MotoGP (video: canale Sky Sport MotoGP)
