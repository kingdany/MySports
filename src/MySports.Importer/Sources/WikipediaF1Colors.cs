using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MySports.Importer.Sources;

// Colori delle scuderie di F1 dalla tabella riassuntiva della pagina "Campionato mondiale di Formula 1 {anno}" di
// Wikipedia in italiano: lo sfondo della cella con il numero di gara di ogni pilota. Ci sono dal 1983; prima la tabella
// non ha colori e la stagione resta senza. Si legge il wikitesto (action=raw), perché l'API di Wikipedia ha limiti stretti
// per chi non è registrato. La tabella è scritta a mano e cambia negli anni (colonne, colori per nome come "white",
// numero condiviso con rowspan da più piloti), quindi si cercano solo le celle colorate con un numero e il pilota che segue.
public sealed partial class WikipediaF1Colors(HttpClient http)
{
    private const int MaxRetries = 5;

    public sealed record DriverColor(string Driver, string Color);

    // Elenco vuoto se la pagina non esiste o non ha colori.
    public async Task<List<DriverColor>> GetAsync(string season, CancellationToken ct)
    {
        var url = $"https://it.wikipedia.org/w/index.php?title=Campionato_mondiale_di_Formula_1_{season}&action=raw";
        for (var attempt = 1; ; attempt++)
        {
            using var res = await http.GetAsync(url, ct);
            if (res.StatusCode == HttpStatusCode.NotFound) return [];
            if (res.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxRetries)
            {
                await Task.Delay(res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10 * attempt), ct);
                continue;
            }
            res.EnsureSuccessStatusCode();
            return Parse(await res.Content.ReadAsStringAsync(ct));
        }
    }

    public static List<DriverColor> Parse(string wikitext)
    {
        // La tabella dei piloti: quella con "Costruttore" e "Pilota/i" nell'intestazione e almeno una cella colorata.
        var table = Tables(wikitext.Split('\n')).FirstOrDefault(t =>
        {
            var header = string.Join(' ', t.Where(l => l.StartsWith('!')).Take(15));
            return header.Contains("Costruttor", StringComparison.OrdinalIgnoreCase)
                   && header.Contains("Pilot", StringComparison.OrdinalIgnoreCase)
                   && t.Any(l => NumberCell().IsMatch(l));
        });
        if (table is null) return [];

        var result = new List<DriverColor>();
        string? color = null;
        var left = 0; // piloti ancora da abbinare al colore (più di uno se il numero ha un rowspan)
        foreach (var line in table)
        {
            if (line.StartsWith("|-")) continue;
            if (NumberCell().Match(line) is { Success: true } m)
            {
                color = Normalize(m.Groups[1].Value);
                left = Rowspan().Match(line) is { Success: true } r ? int.Parse(r.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
                continue;
            }
            if (color is null || left == 0 || !line.StartsWith('|')) continue;
            var clean = Template().Replace(Reference().Replace(line, ""), "");
            if (WikiLink().Match(clean) is not { Success: true } link) continue;
            var name = (link.Groups[2].Success ? link.Groups[2].Value : link.Groups[1].Value).Trim();
            result.Add(new DriverColor(name, color));
            left--;
        }
        return result;
    }

    // Tutte le tabelle della pagina (anche annidate), come righe di testo.
    private static IEnumerable<List<string>> Tables(string[] lines)
    {
        var starts = new Stack<int>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("{|")) starts.Push(i);
            else if (lines[i].StartsWith("|}") && starts.Count > 0)
            {
                var start = starts.Pop();
                yield return lines[start..i].ToList();
            }
        }
    }

    // "#00007D", "#fff" o un nome ("white") -> "#RRGGBB"; null per i nomi che non conosciamo.
    private static string? Normalize(string value) =>
        value.StartsWith('#')
            ? value.Length == 4
                ? "#" + string.Concat(value[1..].Select(c => $"{c}{c}")).ToUpperInvariant()
                : value.ToUpperInvariant()
            : NamedColors.GetValueOrDefault(value);

    private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["white"] = "#FFFFFF", ["black"] = "#000000", ["red"] = "#FF0000", ["pink"] = "#FFC0CB", ["yellow"] = "#FFFF00",
        ["blue"] = "#0000FF", ["green"] = "#008000", ["orange"] = "#FFA500", ["silver"] = "#C0C0C0", ["gray"] = "#808080",
        ["grey"] = "#808080", ["navy"] = "#000080", ["gold"] = "#FFD700", ["purple"] = "#800080", ["maroon"] = "#800000",
        ["lime"] = "#00FF00",
    };

    // Nome in minuscolo, senza accenti né punteggiatura: "Carlos Sainz Jr." -> "carlos sainz jr".
    public static string NormalizeName(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetter(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // Cella del numero di gara con lo sfondo: | align="center" style="color:#fff; background:#00007D" |'''1'''
    [GeneratedRegex(@"^[|!].*background\s*:\s*(#[0-9A-Fa-f]{6}|#[0-9A-Fa-f]{3}|[a-zA-Z]+)\b[^|]*\|\s*(?:''')?\s*\d+\s*(?:''')?\s*$")]
    private static partial Regex NumberCell();

    [GeneratedRegex(@"rowspan\s*=\s*""?(\d+)")]
    private static partial Regex Rowspan();

    [GeneratedRegex(@"<ref[^>]*/>|<ref[\s\S]*?</ref>|<ref.*$")]
    private static partial Regex Reference();

    [GeneratedRegex(@"\{\{[^}]*\}\}")]
    private static partial Regex Template();

    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]")]
    private static partial Regex WikiLink();
}
