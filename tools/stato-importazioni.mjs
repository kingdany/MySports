// Aggiorna docs/STATO-IMPORTAZIONI.md con i conteggi dei dati pubblicati: per ogni competizione e stagione le sessioni
// con video (per canale), quelle ancora senza e, per il calcio, le partite con il dettaglio.
// Riscrive solo la parte tra i segnaposto "inizio dati" e "fine dati": le note scritte a mano restano.
//
//   node tools/stato-importazioni.mjs              dati pubblicati su GitHub Pages
//   node tools/stato-importazioni.mjs site         dati in una cartella locale (es. l'uscita dell'importatore)

import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const DATA_URL = 'https://kingdany.github.io/MySports'; // come DATA_URL in app/lib/data.tsx
const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const docPath = path.join(root, 'docs', 'STATO-IMPORTAZIONI.md');
const START = '<!-- inizio dati: generato da tools/stato-importazioni.mjs, non modificare a mano -->';
const END = '<!-- fine dati -->';

// Sessioni a cui spetta un video (Sessions in YouTubeHighlights.cs).
const VIDEO_SESSIONS = new Set(['race', 'qualifying', 'sprint', 'sprint_qualifying', 'match']);
// Canali in ordine di preferenza, per l'ordine delle colonne (videoSources in Program.cs, "source" dei video).
// Un canale che compare nei dati ma non qui ha comunque la sua colonna, in fondo.
const CHANNELS = {
  'serie-a': ['DAZN', 'Sky Sport'],
  'champions-league': ['Sky Sport', 'Prime Video'],
  f1: ['Sky Sport F1', 'Formula 1'],
  motogp: ['Sky Sport MotoGP'],
};
const MAX_LISTED = 10; // sessioni senza video elencate per nome, se sono poche

const localDir = process.argv[2];
const load = async (file) => {
  if (localDir) {
    try {
      return JSON.parse(await readFile(path.join(localDir, file), 'utf8'));
    } catch {
      return null;
    }
  }
  const res = await fetch(`${DATA_URL}/${file}`);
  return res.ok ? res.json() : null;
};

const meta = await load('meta.json');
if (!meta) throw new Error('meta.json non trovato');

const out = [`Dati del ${new Date(meta.generatedAt).toLocaleString('it-IT', { timeZone: 'Europe/Rome' })} (ora italiana).`, ''];
for (const c of meta.competitions) {
  const channels = [...(CHANNELS[c.id] ?? [])];
  const football = c.sport === 'football';
  const rows = [];
  for (const season of c.seasons) {
    const file = await load(`${c.id}/${season}.json`);
    if (!file) continue;
    const sessions = file.events.filter((e) => e.status === 'finished' && VIDEO_SESSIONS.has(e.session));
    const bySource = {};
    const missing = [];
    for (const e of sessions) {
      const video = e.videos?.[0];
      if (!video) {
        missing.push(e);
        continue;
      }
      const source = video.source ?? 'canale sconosciuto'; // video salvati prima che si registrasse il canale
      bySource[source] = (bySource[source] ?? 0) + 1;
      if (!channels.includes(source)) channels.push(source);
    }
    const matches = file.events.filter((e) => e.session === 'match' && e.status === 'finished');
    rows.push({
      season,
      sessions: sessions.length,
      bySource,
      missing,
      details: football ? matches.filter((e) => e.details).length : null,
      matches: matches.length,
    });
  }

  out.push(`## ${c.name}`, '');
  const head = ['Stagione', 'Sessioni concluse', ...channels, 'Senza video', ...(football ? ['Dettaglio partite'] : [])];
  out.push(`| ${head.join(' | ')} |`, `|${head.map(() => '---').join('|')}|`);
  const notes = [];
  // Le stagioni consecutive senza nessun video (e senza dettaglio) si raccolgono in una riga sola.
  for (let i = 0; i < rows.length; i++) {
    const r = rows[i];
    const empty = (x) => x.missing.length === x.sessions && !x.details;
    if (empty(r)) {
      let j = i;
      let total = 0;
      while (j < rows.length && empty(rows[j])) total += rows[j++].sessions;
      const label = j - i > 1 ? `${rows[j - 1].season}–${r.season}` : r.season;
      out.push(`| ${[label, total, ...channels.map(() => 0), 'tutte', ...(football ? ['—'] : [])].join(' | ')} |`);
      i = j - 1;
      continue;
    }
    const detail = football ? [`${r.details} su ${r.matches}`] : [];
    out.push(`| ${[r.season, r.sessions, ...channels.map((ch) => r.bySource[ch] ?? 0), r.missing.length, ...detail].join(' | ')} |`);
    if (r.missing.length > 0 && r.missing.length <= MAX_LISTED) {
      notes.push(`- ${r.season}, senza video: ${r.missing.map((e) => `${e.title}${football ? '' : ` (${e.session})`} del ${e.startsAt.slice(0, 10)}`).join('; ')}`);
    }
  }
  out.push('', ...notes, ...(notes.length ? [''] : []));
}

const doc = await readFile(docPath, 'utf8');
const start = doc.indexOf(START);
const end = doc.indexOf(END);
if (start < 0 || end < start) throw new Error(`Segnaposto non trovati in ${docPath}`);
await writeFile(docPath, `${doc.slice(0, start + START.length)}\n\n${out.join('\n').trimEnd()}\n\n${doc.slice(end)}`);
console.log(`Aggiornato ${path.relative(root, docPath)}`);
