using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IrsEaser.Models;

namespace IrsEaser.Services;

/// <summary>
/// Resolves ISO 3166-1 numeric country codes for securities.
/// Resolution order: cache (keyed by ISIN) → Source Country text from the securities CSV
/// → OpenFIGI API (ISIN-based when available, otherwise name search) → interactive prompt.
/// </summary>
public class CountryLookupService
{
    private readonly string _cacheFilePath;
    private readonly IReadOnlyDictionary<string, SecurityInfo> _securities;
    private readonly Dictionary<string, string> _cache; // ISIN (or name fallback) → numeric country code

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

    // Human-readable country name → ISO 3166-1 numeric code (from Portfolio Performance)
    private static readonly Dictionary<string, string> CountryNameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Germany",        "276" },
        { "United States",  "840" },
        { "United Kingdom", "826" },
        { "France",         "250" },
        { "Netherlands",    "528" },
        { "Belgium",        "56"  },
        { "Portugal",       "620" },
        { "Spain",          "724" },
        { "Italy",          "380" },
        { "Sweden",         "752" },
        { "Norway",         "578" },
        { "Denmark",        "208" },
        { "Finland",        "246" },
        { "Austria",        "40"  },
        { "Switzerland",    "756" },
        { "Japan",          "392" },
        { "Hong Kong",      "344" },
        { "China",          "156" },
        { "Australia",      "36"  },
        { "Canada",         "124" },
        { "Ireland",        "372" },
        { "Luxembourg",     "442" },
        { "Poland",         "616" },
        { "Czech Republic", "203" },
        // Alternate names used by Portfolio Performance
        { "Great Britain",  "826" },
    };

    public CountryLookupService(string cacheFilePath, IReadOnlyDictionary<string, SecurityInfo> securities)
    {
        _cacheFilePath = cacheFilePath;
        _securities    = securities;
        _cache         = LoadCache(cacheFilePath);
    }

    /// <summary>Returns the cached country code for a security, looked up by ISIN when available.</summary>
    public string? GetCountry(string securityName)
    {
        var key = GetCacheKey(securityName);
        return _cache.TryGetValue(key, out var code) ? code : null;
    }

    /// <summary>
    /// Ensures every security in <paramref name="securityNames"/> has a country code.
    /// Tries: cache → Source Country CSV field → OpenFIGI API → interactive prompt.
    /// </summary>
    public async Task ResolveCountriesAsync(IEnumerable<string> securityNames, bool skipApiLookup)
    {
        var unknown = securityNames.Where(s => GetCountry(s) is null).ToList();
        if (unknown.Count == 0) return;

        Console.WriteLine($"\n{unknown.Count} securities need a country assignment.");

        // Step 1: resolve from Source Country field in the securities CSV
        ResolveFromSecuritiesCsv(unknown);
        unknown = unknown.Where(s => GetCountry(s) is null).ToList();

        // Step 2: OpenFIGI API
        if (!skipApiLookup && unknown.Count > 0)
        {
            await LookupViaOpenFigiAsync(unknown);
            unknown = unknown.Where(s => GetCountry(s) is null).ToList();
        }

        // Step 3: interactive fallback
        if (unknown.Count > 0)
            PromptUserForCountries(unknown);

        SaveCache();
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>Returns the ISIN for the security when available, otherwise its name.</summary>
    private string GetCacheKey(string securityName)
    {
        if (_securities.TryGetValue(securityName, out var info) && !string.IsNullOrEmpty(info.Isin))
            return info.Isin;
        return securityName;
    }

    private void ResolveFromSecuritiesCsv(List<string> securityNames)
    {
        foreach (var name in securityNames)
        {
            if (!_securities.TryGetValue(name, out var info))
                continue;

            if (string.IsNullOrEmpty(info.SourceCountry))
                continue;

            if (CountryNameToCode.TryGetValue(info.SourceCountry, out var code))
            {
                var key = GetCacheKey(name);
                Console.WriteLine($"  [+] '{name}' → Source Country '{info.SourceCountry}' → {code}");
                _cache[key] = code;
            }
            else
            {
                Console.WriteLine($"  [?] Unknown country name '{info.SourceCountry}' for '{name}'.");
            }
        }
    }

    private async Task LookupViaOpenFigiAsync(List<string> securityNames)
    {
        Console.WriteLine("Looking up securities via OpenFIGI...");
        using var http = new HttpClient { BaseAddress = new Uri("https://api.openfigi.com") };

        foreach (var name in securityNames)
        {
            try
            {
                _securities.TryGetValue(name, out var info);
                var isin = info?.Isin;

                List<OpenFigiSecurityDto>? candidates;

                if (!string.IsNullOrEmpty(isin))
                {
                    // Precise: ISIN-based mapping
                    var payload = new[] { new { idType = "ID_ISIN", idValue = isin } };
                    using var response = await http.PostAsJsonAsync("/v3/mapping", payload);

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"  [!] OpenFIGI returned {(int)response.StatusCode} for '{name}' (ISIN {isin}).");
                        await Task.Delay(1100);
                        continue;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var wrappers = JsonSerializer.Deserialize<List<OpenFigiMappingResult>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    candidates = wrappers?.FirstOrDefault()?.Data;
                }
                else
                {
                    // Fallback: name search
                    var payload = new { query = name };
                    using var response = await http.PostAsJsonAsync("/v3/search", payload);

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"  [!] OpenFIGI returned {(int)response.StatusCode} for '{name}'.");
                        await Task.Delay(1100);
                        continue;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OpenFigiSearchResponse>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    candidates = result?.Data;
                }

                var match = candidates?.FirstOrDefault(d =>
                    string.Equals(d.MarketSector, "Equity", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(d.ExchCode));

                if (match?.ExchCode is not null &&
                    ExchangeToCountry.TryGetValue(match.ExchCode, out var countryCode))
                {
                    var key = GetCacheKey(name);
                    Console.WriteLine($"  [+] '{name}' → exchange {match.ExchCode} → country {countryCode}");
                    _cache[key] = countryCode;
                }
                else
                {
                    Console.WriteLine($"  [?] Could not auto-resolve country for '{name}'.");
                }

                // Respect OpenFIGI rate limits (1 req/s without API key)
                await Task.Delay(1100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Error looking up '{name}': {ex.Message}");
            }
        }
    }

    private void PromptUserForCountries(List<string> securityNames)
    {
        Console.WriteLine("\nThe following securities could not be identified automatically.");
        Console.WriteLine("Enter the ISO 3166-1 numeric country code for each (e.g. 276 = Germany, 840 = USA).");
        Console.WriteLine("Press Enter to skip a security (it will be excluded from the XML output).\n");

        foreach (var name in securityNames)
        {
            Console.Write($"  Country code for '{name}': ");
            var input = Console.ReadLine()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(input))
                _cache[GetCacheKey(name)] = input;
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

    private sealed class OpenFigiMappingResult
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
