using System.Net;
using System.Text.Json.Nodes;

namespace MySports.Importer.Sources;

// GET di un JSON con nuovi tentativi: attende se la fonte segnala troppe richieste (429)
// e riprova dopo errori temporanei di rete o del server (5xx).
public static class HttpJson
{
    private const int MaxTransientRetries = 4;

    public static async Task<JsonNode> GetAsync(
        HttpClient http, string url, string sourceName, int maxRateLimitRetries, CancellationToken ct)
    {
        var rateLimited = 0;
        var transient = 0;
        while (true)
        {
            HttpResponseMessage res;
            try
            {
                res = await http.GetAsync(url, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       && !ct.IsCancellationRequested && transient < MaxTransientRetries)
            {
                transient++;
                await WaitAsync($"{sourceName}: errore di rete ({ex.Message})", TimeSpan.FromSeconds(10 * transient), ct);
                continue;
            }

            using (res)
            {
                if (res.StatusCode == HttpStatusCode.TooManyRequests && rateLimited < maxRateLimitRetries)
                {
                    rateLimited++;
                    var wait = res.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1);
                    await WaitAsync($"Limite richieste {sourceName} raggiunto", wait, ct);
                    continue;
                }
                if ((int)res.StatusCode >= 500 && transient < MaxTransientRetries)
                {
                    transient++;
                    await WaitAsync($"{sourceName}: errore {(int)res.StatusCode}", TimeSpan.FromSeconds(10 * transient), ct);
                    continue;
                }
                if (!res.IsSuccessStatusCode)
                    throw new HttpRequestException($"{sourceName} ha risposto {(int)res.StatusCode} su {url}");
                return JsonNode.Parse(await res.Content.ReadAsStringAsync(ct))!;
            }
        }
    }

    private static Task WaitAsync(string reason, TimeSpan wait, CancellationToken ct)
    {
        Console.WriteLine($"{reason}, attendo {wait.TotalSeconds:0} s...");
        return Task.Delay(wait, ct);
    }
}
