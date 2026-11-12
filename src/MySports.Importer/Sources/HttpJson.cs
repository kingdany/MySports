using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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
                    throw new HttpRequestException($"{sourceName} ha risposto {(int)res.StatusCode} su {HideKey(url)}" +
                                                   (await ErrorReasonAsync(res, ct) is { } reason ? $" ({reason})" : ""));
                return JsonNode.Parse(await res.Content.ReadAsStringAsync(ct))!;
            }
        }
    }

    // Il motivo dell'errore scritto dalla fonte, per il log: per YouTube distingue il limite al minuto
    // ("rateLimitExceeded") dalla quota giornaliera finita ("quotaExceeded"). Testo libero accorciato se non è JSON.
    private static async Task<string?> ErrorReasonAsync(HttpResponseMessage res, CancellationToken ct)
    {
        try
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var error = JsonNode.Parse(body)?["error"];
                var reason = error?["errors"]?[0]?["reason"]?.GetValue<string>();
                var message = error?["message"]?.GetValue<string>() ?? error?.ToString();
                if (reason is not null || message is not null)
                    return HideKey(string.Join(": ", new[] { reason, message }.Where(s => !string.IsNullOrEmpty(s))));
            }
            catch (Exception) { } // non è JSON o non ha "error"
            var text = body.Trim().ReplaceLineEndings(" ");
            return HideKey(text.Length > 200 ? text[..200] + "..." : text);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Le chiavi API passate nell'indirizzo non devono finire nei log.
    private static string HideKey(string url) => Regex.Replace(url, "([?&]key=)[^&]+", "$1***");

    private static Task WaitAsync(string reason, TimeSpan wait, CancellationToken ct)
    {
        Console.WriteLine($"{reason}, attendo {wait.TotalSeconds:0} s...");
        return Task.Delay(wait, ct);
    }
}
