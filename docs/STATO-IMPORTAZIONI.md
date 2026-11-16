# Stato delle importazioni

Cosa manca da importare nelle stagioni pubblicate: video degli highlights (per canale) e dettaglio delle partite di
calcio. Le tabelle si aggiornano con `node tools/stato-importazioni.mjs` dalla cartella del repository; le note qui sotto
si scrivono a mano.

## Da fare

- Video delle stagioni passate con l'opzione "Recupera i video" (vedi "Recuperare i video vecchi" nel README), poche
  stagioni per volta per la quota YouTube: le ricerche hanno una quota giornaliera (circa 100 pagine) che si azzera alle
  9 italiane. Una stagione intera di Serie A ne richiede circa 140, quindi due giorni.
- Dopo ogni recupero controllare le sessioni rimaste senza video: di solito il titolo non rispetta la regola del canale
  (da correggere), oppure il video non è su YouTube.
- Dettaglio partite (Serie A e Champions League): completo in tutte le stagioni pubblicate dal 12/11/2026; le partite
  nuove arrivano da sole con il giro normale. Per una stagione aggiunta in futuro arriva importandola con "Stagioni
  passate" e "Competizione", senza altre opzioni (ESPN ha le stagioni dal 2023-24 e nessuna quota).

## Note

- Serie A 2023-24: il recupero del 16/11/2026 ha completato la ricerca su tutta la stagione (72 pagine DAZN, 21 Sky).
  Restano 49 partite senza video, sparse in tutta la stagione (es. Lecce-Lazio, Torino-Cagliari, Empoli-Inter,
  Cagliari-Frosinone, Roma-Inter, Inter-Atalanta), da controllare: forse titoli DAZN che la regola non riconosce, ma la
  ricerca di YouTube non dà sempre gli stessi risultati (Bologna-Napoli e Roma-Lecce sono arrivate solo alla seconda
  ricerca). I 7 video scartati perché "pubblicati prima dell'inizio" sono amarcord DAZN, scartati giustamente.
  Roma-Inter (10/02/2024) ha il video DAZN "THURAM, show, POKER e controrimonta: Roma-Inter 2-4 | Serie A TIM | DAZN
  Highlights", che la regola accetta: la ricerca di YouTube non l'ha restituito (ne dà circa un terzo dei caricamenti).
  Da rilanciare il recupero; se restano in tanti, aggiungere una ricerca mirata per squadre.
- Serie A 2024-25: Torino-Lecce ha solo il video Sky, DAZN non ha pubblicato gli highlights. Il resto è DAZN dal
  recupero del 16/11/2026.
- Serie A 2025-26: Parma-Como (25/10/2025) ha il video Sky perché DAZN l'ha intitolato "Como-Parma", con le squadre
  invertite, e la regola lo scarta. Va bene così.
- Champions League 2025-26: Kairat-Paphos e Newcastle-Benfica hanno gli highlights solo su sport.sky.it, non su YouTube.

## Conteggi

<!-- inizio dati: generato da tools/stato-importazioni.mjs, non modificare a mano -->

Dati del 16/11/2026, 15:53:20 (ora italiana).

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
| 1949–2023 | 1424 | 0 | tutte |

- 2024, senza video: TT Assen (qualifying) del 2024-06-29; Motorrad Grand Prix von Österreich (qualifying) del 2024-08-17

## Serie A

| Stagione | Sessioni concluse | DAZN | Sky Sport | Senza video | Dettaglio partite |
|---|---|---|---|---|---|
| 2026-27 | 110 | 110 | 0 | 0 | 110 su 110 |
| 2025-26 | 380 | 379 | 1 | 0 | 380 su 380 |
| 2024-25 | 380 | 379 | 1 | 0 | 380 su 380 |
| 2023-24 | 380 | 330 | 1 | 49 | 380 su 380 |

## Champions League

| Stagione | Sessioni concluse | Sky Sport | Prime Video | Senza video | Dettaglio partite |
|---|---|---|---|---|---|
| 2026-27 | 72 | 72 | 0 | 0 | 72 su 72 |
| 2025-26 | 189 | 187 | 0 | 2 | 189 su 189 |
| 2024-25 | 189 | 189 | 0 | 0 | 189 su 189 |

- 2025-26, senza video: FK Kairat - Paphos FC del 2025-10-21; Newcastle - Benfica del 2025-10-21

<!-- fine dati -->
