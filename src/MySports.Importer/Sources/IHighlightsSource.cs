using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Una fonte di highlights (un canale YouTube, un sito): aggiunge al massimo un video per sessione.
public interface IHighlightsSource
{
    // Nome salvato nel campo Source dei video, diverso per ogni fonte della stessa competizione.
    string SourceName { get; }

    // Un video già salvato resta solo se spetta ancora a quella sessione (vedi YouTubeHighlights.Matches).
    bool Matches(SportEvent e, Video video, IReadOnlyList<SportEvent> events);

    // recoverOld: cerca anche le sessioni vecchie. preferred: fonti che vengono prima di questa; una sessione che ha già
    // un loro video non si cerca.
    Task<IReadOnlyList<SportEvent>> AttachAsync(
        IReadOnlyList<SportEvent> events, bool recoverOld, IReadOnlySet<string> preferred, CancellationToken ct);
}
