using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using IrsEaser.Models;

namespace IrsEaser.Services;

public class SecuritiesImportService
{
    public Dictionary<string, SecurityInfo> Load(string csvPath)
    {
        var rawText = File.ReadAllText(csvPath);

        // Securities exports from Portfolio Performance always use ';' as delimiter
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
        };

        var result = new Dictionary<string, SecurityInfo>(StringComparer.Ordinal);

        using var reader = new StringReader(rawText);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var name = (csv.GetField("Name") ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                continue;

            var symbol        = (csv.GetField("Symbol")                    ?? "").Trim();
            var isin          = (csv.GetField("ISIN")                      ?? "").Trim();
            var sourceCountry = (csv.GetField("Source Country (Level 3)")  ?? "").Trim();

            result[name] = new SecurityInfo(name, symbol, isin, sourceCountry);
        }

        return result;
    }
}
