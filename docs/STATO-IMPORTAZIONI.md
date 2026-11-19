# Stato delle importazioni

Cosa manca da importare nelle stagioni pubblicate: video degli highlights (per canale) e dettaglio delle partite di
calcio. Le tabelle si aggiornano con `node tools/stato-importazioni.mjs` dalla cartella del repository; le note qui sotto
si scrivono a mano.

## Da fare

- Sky Video (video.sky.it, aggiunto il 19/11/2026): "Recupera i video" sull'Europa League 2024-25, 2025-26 e 2026-27 e
  sulla Champions League 2025-26. In locale la 2026-27 ha trovato il video di tutte le 62 partite che ne erano senza, la
  2025-26 di 154 su 161 (le altre 7 non sono sul sito: PAOK-Maccabi Tel-Aviv, Genk-Betis, Sturm Graz-Nottingham Forest,
  Viktoria Plzen-Fenerbahce, Viktoria Plzen-Freiburg, Panathinaikos-Viktoria Plzen, Go Ahead Eagles-Braga), la Champions
  League le sue due. La 2024-25 dell'Europa League non è ancora provata: guardare nel log "highlights senza partita"
  (nomi da aggiungere a `TeamAliases`).
- Video delle stagioni passate con l'opzione "Recupera i video" (vedi "Recuperare i video vecchi" nel README), poche
  stagioni per volta per la quota YouTube: le ricerche hanno una quota giornaliera (circa 100 pagine) che si azzera alle
  9 italiane. Una stagione intera di Serie A ne richiede circa 140, quindi due giorni.
- Dopo ogni recupero controllare le sessioni rimaste senza video: di solito il titolo non rispetta la regola del canale
  (da correggere), oppure il video non è su YouTube.
- Dettaglio partite (Serie A, Champions League ed Europa League): completo in tutte le stagioni pubblicate (Serie A e
  Champions League dal 12/11/2026, Europa League dal 19/11/2026); le partite
  nuove arrivano da sole con il giro normale. Per una stagione aggiunta in futuro arriva importandola con "Stagioni
  passate" e "Competizione", senza altre opzioni (ESPN ha le stagioni dal 2023-24 e nessuna quota).

## Note

- Serie A 2023-24: completa dal 18/11/2026, dopo cinque recuperi tra il 16 e il 18/11/2026. La ricerca per periodo di
  YouTube non restituisce tutti i video, né sempre gli stessi (Roma-Inter è mancata a due recuperi completi): le ultime
  44 partite sono arrivate con la ricerca mirata per squadre. Due titoli DAZN non passavano la regola. Inter-Atalanta
  (28/02/2024, "INTER-ATALANTA 4-0, poker a San Siro | ...") ha la virgola dopo il punteggio, ora accettata; l'altro
  Inter-Atalanta 4-0 di DAZN è la partita del 30/08/2024. Cagliari-Frosinone (29/10/2023, "... | Cagliari-Frosinone |
  Serie A TIM | DAZN") non ha né punteggio né "Highlights", come le interviste: ha il video del canale ufficiale della
  Serie A, aggiunto come terza scelta. Il canale pubblica ogni partita in italiano e in inglese, e la regola tiene solo
  l'italiana (fsCzv8Kik38). Un recupero può fallire per un 404 temporaneo di YouTube (`playlistNotFound`) a
  metà lettura dei caricamenti: ora la pagina si riprova.
- Serie A 2024-25: Torino-Lecce ha solo il video Sky, DAZN non ha pubblicato gli highlights. Il resto è DAZN dal
  recupero del 16/11/2026.
- Serie A 2025-26: Parma-Como (25/10/2025) ha il video Sky perché DAZN l'ha intitolato "Como-Parma", con le squadre
  invertite, e la regola lo scarta. Va bene così.
- Champions League 2025-26: Kairat-Paphos e Newcastle-Benfica hanno gli highlights solo sul sito di Sky, non su YouTube:
  arrivano da Sky Video con il recupero (provato in locale il 19/11/2026).
- Europa League: Sky pubblica su YouTube gli highlights quasi solo per le partite delle italiane; le altre li hanno dal
  sito video.sky.it (Sky Video), che ha tutte le partite. Il canale ufficiale UEFA non è un'alternativa: pubblica solo
  raccolte e clip, non gli highlights delle partite (verificato il 19/11/2026).
- MotoGP 2024: le qualifiche di Assen (29/06/2024) e dell'Austria (17/08/2024) restano senza video, va bene così.

## Conteggi

<!-- inizio dati: generato da tools/stato-importazioni.mjs, non modificare a mano -->

Dati del 19/11/2026, 03:32:17 (ora italiana).

## Formula 1

| Stagione | Sessioni concluse | Sky Sport F1 | Formula 1 | Senza video |
|---|---|---|---|---|
| 2026 | 52 | 50 | 2 | 0 |
| 2025 | 60 | 60 | 0 | 0 |
| 2024 | 60 | 56 | 4 | 0 |
| 1950–2023 | 1589 | 0 | 0 | tutte |

## MotoGP

| Stagione | Sessioni concluse | Sky Sport MotoGP | Senza video |
|---|---|---|---|
| 2026 | 59 | 59 | 0 |
| 2025 | 66 | 66 | 0 |
| 2024 | 60 | 58 | 2 |
| 1949–2023 | 1437 | 0 | tutte |

- 2024, senza video: TT Assen (qualifying) del 2024-06-29; Motorrad Grand Prix von Österreich (qualifying) del 2024-08-17

## Serie A

| Stagione | Sessioni concluse | DAZN | Sky Sport | Serie A | Senza video | Dettaglio partite |
|---|---|---|---|---|---|---|
| 2026-27 | 110 | 110 | 0 | 0 | 0 | 110 su 110 |
| 2025-26 | 380 | 379 | 1 | 0 | 0 | 380 su 380 |
| 2024-25 | 380 | 379 | 1 | 0 | 0 | 380 su 380 |
| 2023-24 | 380 | 379 | 0 | 1 | 0 | 380 su 380 |

## Champions League

| Stagione | Sessioni concluse | Sky Sport | Prime Video | Senza video | Dettaglio partite |
|---|---|---|---|---|---|
| 2026-27 | 72 | 72 | 0 | 0 | 72 su 72 |
| 2025-26 | 189 | 187 | 0 | 2 | 189 su 189 |
| 2024-25 | 189 | 189 | 0 | 0 | 189 su 189 |

- 2025-26, senza video: FK Kairat - Paphos FC del 2025-10-21; Newcastle - Benfica del 2025-10-21

## Europa League

| Stagione | Sessioni concluse | Sky Sport | Senza video | Dettaglio partite |
|---|---|---|---|---|
| 2026-27 | 72 | 10 | 62 | 72 su 72 |
| 2025-26 | 189 | 28 | 161 | 189 su 189 |
| 2024-25 | 189 | 45 | 144 | 189 su 189 |

<!-- fine dati -->
