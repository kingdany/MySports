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
  Sources/       fonti dati (JolpicaF1Source, OpenF1Client, MotoGpSource, FootballDataSource, YouTubeHighlights, EspnMatchDetails, ...)
  Output/        scrittura dei file JSON
docs/STATO-IMPORTAZIONI.md          cosa manca da importare (video per canale, dettaglio partite) e note
tools/stato-importazioni.mjs        aggiorna i conteggi di quel documento dai dati pubblicati (node tools/stato-importazioni.mjs)
.github/workflows/update-data.yml   esecuzione programmata e pubblicazione
```

## File pubblicati

| File | Contenuto |
|---|---|
| `meta.json` | competizioni disponibili e stagione corrente |
| `upcoming.json` | eventi programmati o in corso nei prossimi 14 giorni |
| `results.json` | eventi conclusi negli ultimi 14 giorni |
| `{competizione}/{stagione}.json` | stagione completa (eventi e classifiche), es. `f1/2026.json`, `serie-a/2026-27.json`; anche le stagioni passate (F1 dal 1950, Serie A dal 2023-24, Champions League dal 2024-25, MotoGP dal 1949) |

L'app legge `meta.json` e i file di stagione; `upcoming.json` e `results.json` vengono pubblicati ma non sono usati dall'app.

Ogni evento ha un campo `videos` con gli highlights YouTube (F1, MotoGP, Serie A e Champions League), un solo video per
sessione, in italiano: DAZN per la Serie A (Sky se manca), Sky Sport per le altre (se manca: per la F1 il canale ufficiale in inglese, per la Champions League Prime Video).
I video già trovati vengono conservati tra un'esecuzione e l'altra. A ogni giro si cercano solo quelli delle partite
finite negli ultimi 3 giorni; per quelle più vecchie si veda "Recuperare i video vecchi".

Le partite finite di Serie A e Champions League hanno anche un campo `details`, dall'API pubblica di ESPN: marcatori
(con rigore o autogol), rigori sbagliati, cartellini (giallo, doppio giallo, rosso) e formazioni (titolari con numero di
maglia, sostituzioni). Si legge una volta per partita e si conserva come i video; se i gol di ESPN non tornano con il
risultato, la partita si riprova al giro dopo. Arriva con il giro normale per la stagione corrente e importando una stagione
passata (ESPN le ha dal 2023-24), tranne con "Solo classifiche".

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

Per la MotoGP (classe 500cc fino al 2001) l'archivio parte dal 1949:

```
dotnet run --project src/MySports.Importer -- --competition motogp --seasons 1949-2025 --out out
```

Fino al 2000 mancano gli orari (`dateOnly`) e le qualifiche, nelle stagioni vecchie manca la squadra (si mostra la moto)
e alcune gare non hanno la classifica nemmeno nell'archivio ufficiale.

## Recuperare i video vecchi

Serve una volta sola, per una competizione nuova o per le stagioni passate.
Da GitHub: **Actions → Aggiorna dati → Run workflow**, casella "Recupera i video". Con il campo "Stagioni passate" vale
per quelle stagioni della competizione indicata, altrimenti per le stagioni correnti di tutte le competizioni.
In locale:

```
dotnet run --project src/MySports.Importer -- --competition serie-a --seasons 2025-26 --recover-videos --out out
```

Ogni canale viene riletto fino alla stagione richiesta (circa 20 unità di quota YouTube al mese da risalire).
YouTube però dà solo gli ultimi 20.000 video di un canale: DAZN arriva così a novembre 2024, Sky Sport a settembre 2024.
Le partite più vecchie si cercano con la ricerca di YouTube, circa 1-2 pagine ogni 3 giorni da coprire, che usa una quota
giornaliera a parte ("Search Queries per day" nella console di Google Cloud, 100 al giorno). Un'esecuzione fa al massimo
100 pagine di ricerca: una stagione intera, come la Serie A 2023-24 (circa 140 pagine), va recuperata in due giorni,
rilanciando finché il log non dice più "ricerca fermata" o non smette di dare "AVVISO ricerca video".

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

- [ ] Importare video e dettaglio partite delle stagioni passate: stato, conteggi e note in [docs/STATO-IMPORTAZIONI.md](docs/STATO-IMPORTAZIONI.md)
- [ ] Calcio: altre competizioni
- [ ] Calcio: nazionali, solo le partite che contano e con tutte le nazionali, una competizione per torneo: Nations League (intera, tutte le leghe e i gruppi, altrimenti la competizione resterebbe a metà), Europei e Mondiali. Amichevoli escluse: non essendo un torneo non vengono importate. Da decidere le qualificazioni a Europei e Mondiali
  - Europei e Mondiali: football-data.org li ha nel piano gratuito (`EC`, `WC`), quindi bastano `FootballDataSource` e la sua gestione di fasi, rigori e classifiche dei gironi
  - Nations League: football-data.org non ce l'ha; usare l'API pubblica di ESPN (senza chiave, non documentata, risposte a volte compresse con gzip), lega `uefa.nations`: `https://site.api.espn.com/apis/site/v2/sports/soccer/uefa.nations/scoreboard?dates=...` per le partite (da verificare: una prova con l'intervallo `20261101-20261130` non ha restituito eventi, forse serve una data sola o un altro formato), da verificare anche le classifiche dei gruppi. Stagioni come il calcio (`2026-27`, cambio il 1° agosto)
  - per l'Italia, già verificato: `.../soccer/all/teams/162/schedule?fixture=true` dà le prossime partite (12/11/2026 Italia-Francia, 15/11/2026 Belgio-Italia), `?season=2026` quelle giocate
  - highlights da Rai Sport o Sky Sport: verificare i canali YouTube e scrivere la regola sui titoli veri; per le partite senza l'Italia probabilmente pochi video
- [ ] NBA: l'API pubblica di ESPN ha il calendario con orari e risultati (`https://site.api.espn.com/apis/site/v2/sports/basketball/nba/scoreboard?dates=AAAAMMGG`, verificato a novembre 2026), da verificare classifiche e stagioni passate. Una stagione regolare sono 1230 partite più i playoff: nell'app servono il filtro per squadra (già presente) e forse un limite in "Tutti". Highlights: Prime Video Sport IT pubblica le partite NBA che trasmette ("NBA, Chicago Bulls-Philadelphia 76ers | Highlights"), da verificare anche Sky Sport e il canale ufficiale NBA
- [ ] Tennis: l'API di ESPN ha i tornei in corso con gli incontri (`.../sports/tennis/atp/scoreboard`, `.../tennis/wta/scoreboard`: a novembre 2026 Stoccolma con 54 incontri, WTA Finals con 30). Da decidere quali tornei (Slam, Masters 1000, Finals...), se ATP e WTA, e quali incontri (tutti, solo dagli ottavi, solo gli italiani), perché uno Slam da solo sono 127 incontri per tabellone. Classifica: il ranking. Da verificare le stagioni passate e gli highlights (Sky Sport, SuperTennis)
- [ ] WWE: ESPN non la segue. TheSportsDB ha la lega WWE (id 4444) con show settimanali (RAW, SmackDown, NXT...) e grandi eventi, ma con la chiave gratuita dà solo 15 eventi per stagione e il prossimo; quella completa è a pagamento. Da decidere se tenere solo i Premium Live Event (WrestleMania, SummerSlam, Royal Rumble...), pochi all'anno, e in quel caso valutare altre fonti o un elenco tenuto a mano. Senza classifiche; i risultati degli incontri sono anticipazioni, da decidere se mostrarli. Highlights: canale ufficiale WWE (in inglese)
- [ ] Calcio: se il file della stagione diventa troppo pesante per l'avvio dell'app (Serie A: circa 300 KB compressi a fine stagione con il dettaglio partite), spostare il dettaglio in un file a parte, scaricato quando si apre la partita
- [ ] App: migliorare la grafica con immagini: loghi delle squadre e loghi delle competizioni (F1, MotoGP, Serie A, Champions League)
- [ ] App: migliorare la scelta della stagione (`SeasonPicker`) quando una competizione ha molte stagioni: scorrerle tutte è lungo (oggi succede con F1 e MotoGP, che arrivano al 1950 e al 1949, ma vale per qualsiasi competizione con un archivio lungo). Forse scegliere prima il decennio, da decidere