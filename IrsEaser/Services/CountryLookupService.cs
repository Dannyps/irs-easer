using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IrsEaser.Services;

/// <summary>
/// Resolves ISO 3166-1 numeric country codes for securities.
/// Uses a local JSON cache, optionally enriched via the OpenFIGI API,
/// with interactive fallback for any securities that cannot be identified automatically.
/// </summary>
public class CountryLookupService
{
    private readonly string _cacheFilePath;
    private readonly Dictionary<string, string> _cache; // security name → numeric country code

    // Exchange code → ISO 3166-1 numeric country code
    private static readonly Dictionary<string, string> ExchangeToCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        // Germany
        { "GY", "276" }, { "GF", "276" }, { "GM", "276" },
        // USA
        { "UN", "840" }, { "UW", "840" }, { "UA", "840" }, { "UR", "840" }, { "US", "840" },
        // UK
        { "LN", "826" }, { "LI", "826" },
        // France
        { "FP", "250" },
        // Netherlands
        { "NA", "528" },
        // Belgium
        { "BB", "56" },
        // Portugal
        { "PL", "620" },
        // Spain
        { "SM", "724" }, { "SQ", "724" },
        // Italy
        { "IM", "380" },
        // Sweden
        { "SS", "752" },
        // Norway
        { "NO", "578" },
        // Denmark
        { "DC", "208" },
        // Finland
        { "HF", "246" },
        // Austria
        { "AV", "40" },
        // Switzerland
        { "SW", "756" },
        // Japan
        { "JP", "392" },
        // Hong Kong
        { "HK", "344" },
        // Australia
        { "AU", "36" },
        // Canada
        { "CN", "124" },
        // Ireland
        { "ID", "372" },
        // Luxembourg
        { "LX", "442" },
        // Poland
        { "PW", "616" },
        // Czech Republic
        { "CP", "203" },
    };

    public CountryLookupService(string cacheFilePath)
    {
        _cacheFilePath = cacheFilePath;
        _cache = LoadCache(cacheFilePath);
    }

    public string? GetCountry(string securityName) =>
        _cache.TryGetValue(securityName, out var code) ? code : null;

    /// <summary>
    /// Ensures every security in <paramref name="securities"/> has a country code.
    /// Tries: cache → OpenFIGI API → interactive prompt.
    /// </summary>
    public async Task ResolveCountriesAsync(IEnumerable<string> securities, bool skipApiLookup)
    {
        var unknown = securities.Where(s => !_cache.ContainsKey(s)).ToList();
        if (unknown.Count == 0) return;

        Console.WriteLine($"\n{unknown.Count} securities need a country assignment.");

        if (!skipApiLookup)
        {
            await LookupViaOpenFigiAsync(unknown);
            unknown = unknown.Where(s => !_cache.ContainsKey(s)).ToList();
        }

        if (unknown.Count > 0)
            PromptUserForCountries(unknown);

        SaveCache();
    }

    private async Task LookupViaOpenFigiAsync(List<string> securities)
    {
        Console.WriteLine("Looking up securities via OpenFIGI...");
        using var http = new HttpClient { BaseAddress = new Uri("https://api.openfigi.com") };

        foreach (var security in securities)
        {
            try
            {
                var payload = new { query = security };
                using var response = await http.PostAsJsonAsync("/v3/search", payload);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"  [!] OpenFIGI returned {(int)response.StatusCode} for '{security}'.");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OpenFigiSearchResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var match = result?.Data?.FirstOrDefault(d =>
                    string.Equals(d.MarketSector, "Equity", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(d.ExchCode));

                if (match?.ExchCode is not null &&
                    ExchangeToCountry.TryGetValue(match.ExchCode, out var countryCode))
                {
                    Console.WriteLine($"  [+] '{security}' → exchange {match.ExchCode} → country {countryCode}");
                    _cache[security] = countryCode;
                }
                else
                {
                    Console.WriteLine($"  [?] Could not auto-resolve country for '{security}'.");
                }

                // Respect OpenFIGI rate limits (1 req/s without API key)
                await Task.Delay(1100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Error looking up '{security}': {ex.Message}");
            }
        }
    }

    private void PromptUserForCountries(List<string> securities)
    {
        Console.WriteLine("\nThe following securities could not be identified automatically.");
        Console.WriteLine("Enter the ISO 3166-1 numeric country code for each (e.g. 276 = Germany, 840 = USA).");
        Console.WriteLine("Press Enter to skip a security (it will be excluded from the XML output).\n");

        foreach (var security in securities)
        {
            Console.Write($"  Country code for '{security}': ");
            var input = Console.ReadLine()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(input))
                _cache[security] = input;
        }
    }

    private static Dictionary<string, string> LoadCache(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch
        {
            Console.WriteLine($"[!] Could not read countries cache at {path}. Starting fresh.");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void SaveCache()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Could not save countries cache: {ex.Message}");
        }
    }

    // ── OpenFIGI response DTOs ──────────────────────────────────────────────

    private sealed class OpenFigiSearchResponse
    {
        [JsonPropertyName("data")]
        public List<OpenFigiSecurityDto>? Data { get; set; }
    }

    private sealed class OpenFigiSecurityDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("ticker")]
        public string? Ticker { get; set; }

        [JsonPropertyName("exchCode")]
        public string? ExchCode { get; set; }

        [JsonPropertyName("securityType")]
        public string? SecurityType { get; set; }

        [JsonPropertyName("marketSector")]
        public string? MarketSector { get; set; }
    }
}
