# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

MySports is an ad-free sports calendar. There is no server: a C# importer runs on a GitHub Actions cron every 3 hours and publishes static JSON to GitHub Pages, and the Expo app reads those files.

```
GitHub Actions (cron) → MySports.Importer (C#) → JSON → gh-pages branch → GitHub Pages → app
```

F1, MotoGP, Serie A and Champions League are implemented. Other football competitions are planned (see "Prossimi passi" in `README.md`).

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
dotnet run --project src/MySports.Importer -- --recover-videos --out out                     # also look for old matches' videos; with --seasons, in those seasons
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
- **MotoGP uses `MotoGpSource`**, built on the public but undocumented API behind motogp.com (`api.motogp.pulselive.com`, no key). Session times come from the calendar (`/events`), because the results archive (`/results`) has provisional future session dates and local times labelled as UTC; classifications and rider standings (the only standings the API has) come from the archive, linked to the calendar through `toad_api_uuid`. The round is counted in date order among the GPs the premier class raced, because the calendar's `sequence` has duplicates and gaps in past seasons. Seasons go back to 1949: the premier class is "500cc" in the archive until 2001 (same `MGP` code in the calendar). Before 2001 (`FirstSeasonWithTimes`) the calendar's times are placeholders, so events are `dateOnly`. Qualifying is one `qualifying` event: from 2013 Q1 and Q2 merged in grid order (Q2, then the rest of Q1) with `q1`/`q2` times; 2005–2012 a single `QP` session with the time in `q1`; earlier QP1/QP2 have no classifications. Old seasons have no team (the bike is used instead) and 0 points in race classifications (left out), and some races have an empty classification in the archive itself. Pre-qualifying and warm-up are saved as `practice` and `warm_up`, FP4 (2015–2022) as `fp4`, all hidden in the app like F1 practice. A restarted race also has `RAC2`/`SPR2`: it stays the same event and takes the last start's classification. Cancelled sessions get `Cancelled`. GPs whose calendar times are wrong in the source itself are fixed in `TimeCorrections` (Jerez 2024: everything 2 hours late).
- **Football uses `FootballDataSource`** (football-data.org, `FOOTBALL_DATA_API_KEY`). It has its own `HttpClient` so the key is only sent to that API. The free plan allows 10 requests a minute and seasons from 2023-24 on. Season names are `2026-27` and roll over on 1 August. Matches without a confirmed kickoff come back at 00:00 UTC on a provisional date, so they are saved as `dateOnly`. Scores are kept only for finished matches, because the 3-hour cron would leave partial scores stale. Standings include a `scorers` table with at least 10 names, completing the last tied group, so it can have more rows. Tied players share a number with no gaps (1, 2, 2, 3). Its `points` field holds the goals. To add it to past seasons use `--standings-only`. Table rows get a `zone` (Champions, Europa, Conference, relegation) from position rules, because the API has none: `SerieAZones` by default, `SerieAZoneExceptions` for seasons whose actual European places were different. The app only draws them. The Champions League (`champions-league`, from 2024-25 because the API merges the 2023-24 groups into one table) uses `ChampionsLeagueZones`. Knockout matches set `stage`, with `round` 1/2 for the two legs; a team not yet known is left out and the title says "Da definire". The API's `fullTime` includes shootout goals, so they are subtracted and saved as `homePenalties`/`awayPenalties`.
- **Highlights** (`YouTubeHighlights`) are attached to the current season, or to past seasons with `--recover-videos`, and are skipped if `YOUTUBE_API_KEY` is not set. Each session gets one video (`videoSources` in `Program.cs`, channels in order of preference): Serie A takes DAZN, then Sky for matches without a DAZN video; F1 takes Sky, then the official English channel; Champions League takes Sky, then Prime Video (which only has the Wednesday match it broadcasts); MotoGP takes Sky. A channel only searches sessions that have no video from a channel before it, and a video from a preferred channel published later replaces the fallback one. Every run drops saved videos whose channel is no longer used or that no longer belong to their session (`YouTubeHighlights.Matches`: the title must pass the channel's rule and the session must be the one `SessionFor` would pick today, i.e. the latest finished session the title matches that started up to 3 days before publication), so fixing a rule or a session time also removes the wrong videos it had picked; videos dropped from old sessions only come back with `--recover-videos`. Each channel has its own title rule: F1 and MotoGP videos match by session type and publish date, not GP name; football videos match by home and away team (`shortName`, so a renamed team must match how channels write it; `TeamAliases` covers titles that use another name, and accents are ignored) and publish date. DAZN titles put free text before the teams, so its rule looks for "Home-Away" followed by the score (or "Highlights" in the title) instead of extracting names. A video is looked for only during the 3 days after its session, so a session left without one no longer lengthens every run's read of the channel's uploads. To fill in older sessions (a new competition or title rule, past seasons) run with `--recover-videos`, which is a manual option because it reads the channels far back. The uploads playlist only returns a channel's latest 20,000 videos (DAZN reaches November 2024, Sky Sport September 2024), so when it stops at that limit the recovery searches the older sessions' windows with `search.list` (`SearchBeforeAsync`: no keywords, since they made it miss videos; `regionCode=IT`, since GitHub runners are in the US and Italy-only videos would be left out; 3-day periods; a separate daily quota, "Search Queries per day"). It is capped at `SearchBudget` pages per run for all channels together; past the cap it saves what it found and the next recovery carries on. Videos blocked in Italy are dropped. Videos that can't be embedded are saved with `embeddable: false`, and the app opens them directly in YouTube.

API keys come from `.env` locally (gitignored) and from Actions secrets in CI.

### Data contract

`app/lib/types.ts` is a hand-written mirror of the C# records in `src/MySports.Importer/Models` and `JsonPublisher`'s file records. There is no codegen, so change both sides together. Serialization uses camelCase and omits nulls, so nullable C# fields are optional in TypeScript. Season files already published on `gh-pages` may predate newer fields (e.g. `standings`, video `source`/`duration`), so new fields must be nullable or have a default. This matters on the importer side too: it reads those files back through the same records, so a new `required` property makes the load fail and that season's import errors out.

Old F1 seasons have no session times and football matches may not have a kickoff yet: those events set `dateOnly` and use 12:00 UTC as `startsAt`, so the date stays the same in every time zone.

### App (`app/`)

- Routes use Expo Router and live in `app/app/`, not `src/app/` as `app/AGENTS.md` says. Shared code is imported with the `@/` alias.
- `lib/data.tsx` (`EventsProvider`/`useEvents`) is the single data layer. It loads `meta.json` and every competition's current season at startup; past seasons download on demand and stay in memory. `getEvent` only searches seasons already loaded, so a past-season event route shows "Evento non trovato" until that season has been loaded.
- The app does not read `upcoming.json` or `results.json`, even though the importer publishes them. Screens filter the current-season events themselves: with one competition chosen the Prossimi tab shows the rest of the season, while "Tutti" is limited to the next 30 days (`ALL_DAYS` in `app/(tabs)/index.tsx`) so the list doesn't grow with every sport added.
- Risultati and Classifiche never mix competitions. They share one choice from `CompetitionBar` through `useCompetitionSeason` (`lib/useSelectedSeason.ts`), defaulting to the first competition in `meta.json`; `SeasonPicker` only lists that competition's seasons, and the season goes back to the current one when the competition changes.
- `lib/preferences.tsx` saves the competition choices on the phone with AsyncStorage. Prossimi has its own choice, separate from the other two tabs, with an extra "Tutti" option that is the default.
- Switching competition or season uses `useDeferredValue`: the buttons update first with the screen in its loading state, then the list renders. `EventList` and `EventRow` are memoized and their props are kept stable, so unrelated changes don't redraw the mounted rows.
- Classifiche also shows a "Tabellone" (`lib/bracket.ts`, `components/Bracket.tsx`) when a season has knockout matches. It is not published data: the app builds it from the events' `stage`, summing the legs and using penalties for the winner. It is a tree scrolled sideways that shows who goes through, without scores, since results and videos live in Risultati. Ties are linked through the winners, because the API has no bracket positions: rounds not yet known are not drawn, and matches with a team not yet known are skipped.
- Prossimi and Risultati also have a team filter (`useTeamFilter`, `TeamPicker`), built from the teams in the events on screen, so it doesn't appear for F1 or for "Tutti". Each tab keeps its own choice in memory only.
- Prossimi's rows don't open the event detail (`linked={false}`). Risultati's do.
- `EventList` sets a very large `windowSize` so rows stay mounted, as a fix for scrolling that jumps up and down once the list starts unmounting its first rows (about 10 screens down, with sticky headers).
- Practice sessions are in the data but hidden in the app. Prossimi filters them with `isPractice` (`lib/format.ts`); Risultati only lists events that have a result, which practice sessions never do.
- `DATA_URL` in `lib/data.tsx` always points at the published GitHub Pages data, so the app does not see local importer output in `out/`.
- `YouTubePlayer` also derives its WebView origin from `DATA_URL`, because YouTube rejects embeds without a web origin. Changing `DATA_URL` therefore changes the player's origin too.

### Expo guidance

`app/AGENTS.md` applies to app work. In particular, Expo changes between SDK releases, so check the docs for the SDK version in `app/package.json` rather than relying on memory. Run lint and typecheck before calling a task done. Native libraries not bundled in Expo Go require a development build, and `ios/`/`android/` are generated, so never edit them by hand.
