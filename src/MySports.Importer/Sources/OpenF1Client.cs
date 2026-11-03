using System.Globalization;
using System.Text.Json.Nodes;

namespace MySports.Importer.Sources;

// OpenF1 (https://openf1.org): dati storici gratuiti e senza chiave.
// Usata solo per ciò che Jolpica non fornisce, come i risultati delle qualifiche sprint.
public sealed class OpenF1Client(HttpClient http)
{
    private const string BaseUrl = "https://api.openf1.org/v1";
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(2.1); // limite ~30 richieste al minuto
    private const int MaxRateLimitRetries = 10;

    public sealed record Session(int SessionKey, DateTimeOffset Start);

    // Duration contiene i tempi delle singole fasi (es. SQ1, SQ2, SQ3); null se il pilota non vi ha preso parte.
    public sealed record ResultRow(int? Position, int DriverNumber, double?[] Duration);

    public sealed record Driver(int Number, string FullName, string Acronym, string Team);

    public async Task<List<Session>> GetSessionsAsync(string year, string sessionName, CancellationToken ct)
    {
        var data = await GetJsonAsync($"sessions?year={year}&session_name={Uri.EscapeDataString(sessionName)}", ct);
        return data.AsArray()
            .Where(s => s!["is_cancelled"]?.GetValue<bool>() != true)
            .Select(s => new Session(
                s!["session_key"]!.GetValue<int>(),
                DateTimeOffset.Parse(s["date_start"]!.GetValue<string>(), CultureInfo.InvariantCulture)))
            .ToList();
    }

    public async Task<List<ResultRow>> GetSessionResultAsync(int sessionKey, CancellationToken ct)
    {
        var data = await GetJsonAsync($"session_result?session_key={sessionKey}", ct);
        return data.AsArray()
            .Select(r => new ResultRow(
                r!["position"]?.GetValue<int>(),
                r["driver_number"]!.GetValue<int>(),
                r["duration"] is JsonArray d ? d.Select(x => x?.GetValue<double>()).ToArray() : []))
            .ToList();
    }

    public async Task<List<Driver>> GetDriversAsync(int sessionKey, CancellationToken ct)
    {
        var data = await GetJsonAsync($"drivers?session_key={sessionKey}", ct);
        return data.AsArray()
            .Select(d => new Driver(
                d!["driver_number"]!.GetValue<int>(),
                $"{d["first_name"]?.GetValue<string>()} {d["last_name"]?.GetValue<string>()}".Trim(),
                d["name_acronym"]?.GetValue<string>() ?? "",
                d["team_name"]?.GetValue<string>() ?? ""))
            .ToList();
    }

    private async Task<JsonNode> GetJsonAsync(string path, CancellationToken ct)
    {
        var json = await HttpJson.GetAsync(http, $"{BaseUrl}/{path}", "OpenF1", MaxRateLimitRetries, ct);
        await Task.Delay(Pause, ct);
        return json;
    }
}
