# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

MySports is an ad-free sports calendar. There is no server: a C# importer runs on a GitHub Actions cron every 3 hours and publishes static JSON to GitHub Pages, and the Expo app reads those files.

```
GitHub Actions (cron) → MySports.Importer (C#) → JSON → gh-pages branch → GitHub Pages → app
```

F1 and Serie A are implemented. Other football competitions (Champions League and others) and MotoGP are planned (see "Prossimi passi" in `README.md`).

Code comments, UI strings, log messages, docs and commit messages are in Italian (commits use the imperative, e.g. "Aggiungi le classifiche piloti e costruttori"). Keep to this.

## Commands

The repo has no automated tests.

Importer (.NET 10), run from the repo root because `.env` is read relative to the working directory:

```
dotnet build MySports.slnx
dotnet run --project src/MySports.Importer -- --out out                                      # current season, feeds and meta
dotnet run --project src/MySports.Importer -- --competition f1 --seasons 1950-2025 --out out # past seasons (also "2019,2021")
dotnet run --project src/MySports.Importer -- --competition f1 --seasons 1950-2025 --standings-only --out out
dotnet run --project src/MySports.Importer -- --competition serie-a --seasons 2024-25,2025-26 --out out # football seasons are listed by name
```

These commands only write locally. To publish past seasons, run the "Aggiorna dati" workflow manually, which takes the same options as inputs. Jolpica allows about 500 requests an hour, so long imports stop and wait for the limit to reset.

App (run from `app/`):

```
npm install
npx expo start           # add --tunnel if the phone is not on the same Wi-Fi
npx expo lint
npx tsc --noEmit
npx expo install <pkg>   # always use this to add packages, so versions match the SDK
```

## Architecture

### Importer (`src/MySports.Importer`)

- **The output folder is also the input.** Each run loads the existing season file and merges it with the fresh data. Sources don't provide videos, so videos are kept from the previous run. Results missing from this run are also kept (e.g. OpenF1 is down), and so are standings if fetching them fails. This is why CI checks out `gh-pages` into `site/` and runs with `--out site`.
- **Event IDs must stay stable** (`f1-{season}-{round}-{session}`, `serie-a-{season}-{matchId}`), because the merge matches on them and the app routes on them. They must also be unique within a season, since the merge builds a dictionary from them and throws on duplicates.
- **Partial failure is expected.** A failing source or season is logged and the run exits with code 1, but the workflow still publishes whatever was saved. Previously published data is reused for the feeds.
- **Adding a competition:** implement `IEventSource` and add it to the `sources` array in `Program.cs`. `meta.json` lists a competition only if its current-season file exists, and its available seasons come from the files in its output folder.
- **F1 uses two sources.** `JolpicaF1Source` is the primary one and uses `OpenF1Client` for official session times (Jolpica can miss rescheduled sessions) and for sprint qualifying results. OpenF1 is only queried from the 2023 season on, and if it fails the importer carries on with Jolpica data alone. Use `HttpJson` for HTTP calls: it handles 429 and transient retries, and hides API keys in error messages.
- **Football uses `FootballDataSource`** (football-data.org, `FOOTBALL_DATA_API_KEY`). It has its own `HttpClient` so the key is only sent to that API. The free plan allows 10 requests a minute and seasons from 2023-24 on. Season names are `2026-27` and roll over on 1 August. Matches without a confirmed kickoff come back at 00:00 UTC on a provisional date, so they are saved as `dateOnly`. Scores are kept only for finished matches, because the 3-hour cron would leave partial scores stale. Standings include a `scorers` table with at least 10 names, completing the last tied group, so it can have more rows. Tied players share a number with no gaps (1, 2, 2, 3). Its `points` field holds the goals. To add it to past seasons use `--standings-only`. Table rows get a `zone` (Champions, Europa, Conference, relegation) from position rules, because the API has none: `SerieAZones` by default, `SerieAZoneExceptions` for seasons whose actual European places were different. The app only draws them.
- **Highlights** (`YouTubeHighlights`) are attached only to the current season, and are skipped if `YOUTUBE_API_KEY` is not set. Each channel has its own title rule: F1 videos match by session type and publish date, not GP name; football videos match by home and away team (`shortName`, so a renamed team must match how channels write it) and publish date. Videos blocked in Italy are dropped. Videos that can't be embedded are saved with `embeddable: false`, and the app opens them directly in YouTube.

API keys come from `.env` locally (gitignored) and from Actions secrets in CI.

### Data contract

`app/lib/types.ts` is a hand-written mirror of the C# records in `src/MySports.Importer/Models` and `JsonPublisher`'s file records. There is no codegen, so change both sides together. Serialization uses camelCase and omits nulls, so nullable C# fields are optional in TypeScript. Season files already published on `gh-pages` may predate newer fields (e.g. `standings`, video `source`/`duration`), so new fields must be nullable or have a default. This matters on the importer side too: it reads those files back through the same records, so a new `required` property makes the load fail and that season's import errors out.

Old F1 seasons have no session times and football matches may not have a kickoff yet: those events set `dateOnly` and use 12:00 UTC as `startsAt`, so the date stays the same in every time zone.

### App (`app/`)

- Routes use Expo Router and live in `app/app/`, not `src/app/` as `app/AGENTS.md` says. Shared code is imported with the `@/` alias.
- `lib/data.tsx` (`EventsProvider`/`useEvents`) is the single data layer. It loads `meta.json` and every competition's current season at startup; past seasons download on demand and stay in memory. `getEvent` only searches seasons already loaded, so a past-season event route shows "Evento non trovato" until that season has been loaded.
- The app does not read `upcoming.json` or `results.json`, even though the importer publishes them. Screens filter the current-season events themselves, so the Prossimi tab shows the rest of the season rather than 14 days.
- Risultati and Classifiche never mix competitions. They share one choice from `CompetitionBar` through `useCompetitionSeason` (`lib/useSelectedSeason.ts`), defaulting to the first competition in `meta.json`; `SeasonPicker` only lists that competition's seasons, and the season goes back to the current one when the competition changes.
- `lib/preferences.tsx` saves the competition choices on the phone with AsyncStorage. Prossimi has its own choice, separate from the other two tabs, with an extra "Tutti" option that is the default.
- Switching competition or season uses `useDeferredValue`: the buttons update first with the screen in its loading state, then the list renders. `EventList` and `EventRow` are memoized and their props are kept stable, so unrelated changes don't redraw the mounted rows.
- Prossimi and Risultati also have a team filter (`useTeamFilter`, `TeamPicker`), built from the teams in the events on screen, so it doesn't appear for F1 or for "Tutti". Each tab keeps its own choice in memory only.
- Prossimi's rows don't open the event detail (`linked={false}`). Risultati's do.
- `EventList` sets a very large `windowSize` so rows stay mounted, as a fix for scrolling that jumps up and down once the list starts unmounting its first rows (about 10 screens down, with sticky headers).
- Practice sessions are in the data but hidden in the app. Prossimi filters them with `isPractice` (`lib/format.ts`); Risultati only lists events that have a result, which practice sessions never do.
- `DATA_URL` in `lib/data.tsx` always points at the published GitHub Pages data, so the app does not see local importer output in `out/`.
- `YouTubePlayer` also derives its WebView origin from `DATA_URL`, because YouTube rejects embeds without a web origin. Changing `DATA_URL` therefore changes the player's origin too.

### Expo guidance

`app/AGENTS.md` applies to app work. In particular, Expo changes between SDK releases, so check the docs for the SDK version in `app/package.json` rather than relying on memory. Run lint and typecheck before calling a task done. Native libraries not bundled in Expo Go require a development build, and `ios/`/`android/` are generated, so never edit them by hand.
