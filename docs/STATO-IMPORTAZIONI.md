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
- Dettaglio partite (Serie A e Champions League): arriva importando la stagione con "Stagioni passate" e "Competizione",
  senza altre opzioni; ESPN ha le stagioni dal 2023-24 e nessuna quota.

## Note

- Serie A 2023-24: la ricerca del 12/11/2026 si è fermata per la quota al 29/01/2024; da rilanciare per il resto. Le
  partite rimaste senza video prima del 23/01/2024 (19, es. Lecce-Lazio, Torino-Cagliari, Empoli-Inter,
  Cagliari-Frosinone) sono da controllare: forse titoli DAZN che la regola non riconosce, ma la ricerca di YouTube non dà
  sempre gli stessi risultati (Bologna-Napoli e Roma-Lecce sono arrivate solo alla seconda ricerca).
- Serie A 2024-25: Torino-Lecce ha solo il video Sky, DAZN non ha pubblicato gli highlights. Bologna-Genoa,
  Verona-Empoli e Torino-Roma hanno Sky perché la regola DAZN non li riconosceva (corretta il 12/11/2026): arrivano
  rilanciando il recupero.
- Serie A 2025-26: Genoa-Sassuolo ha Sky, il video DAZN arriva rilanciando il recupero.
- Champions League 2025-26: Kairat-Paphos e Newcastle-Benfica hanno gli highlights solo su sport.sky.it, non su YouTube.

## Conteggi

<!-- inizio dati: generato da tools/stato-importazioni.mjs, non modificare a mano -->

Dati del 12/11/2026, 16:47:41 (ora italiana).

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

| Stagione | Sessioni concluse | DAZN | Sky Sport | Senza video | Dettaglio partite |
|---|---|---|---|---|---|
| 2026-27 | 110 | 110 | 0 | 0 | 110 su 110 |
| 2025-26 | 380 | 378 | 2 | 0 | 0 su 380 |
| 2024-25 | 380 | 376 | 4 | 0 | 0 su 380 |
| 2023-24 | 380 | 194 | 6 | 180 | 0 su 380 |

## Champions League

| Stagione | Sessioni concluse | Sky Sport | Prime Video | Senza video | Dettaglio partite |
|---|---|---|---|---|---|
| 2026-27 | 72 | 72 | 0 | 0 | 0 su 72 |
| 2025-26 | 189 | 187 | 0 | 2 | 0 su 189 |
| 2024-25 | 189 | 189 | 0 | 0 | 0 su 189 |

- 2025-26, senza video: FK Kairat - Paphos FC del 2025-10-21; Newcastle - Benfica del 2025-10-21

<!-- fine dati -->
