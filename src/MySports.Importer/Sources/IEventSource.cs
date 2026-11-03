using MySports.Importer.Models;

namespace MySports.Importer.Sources;

// Una fonte dati esterna che produce gli eventi di una competizione.
public interface IEventSource
{
    Competition Competition { get; }

    string CurrentSeason(DateTimeOffset now);

    Task<IReadOnlyList<SportEvent>> FetchSeasonAsync(string season, CancellationToken ct);
}
