using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using IrsEaser.Models;

namespace IrsEaser.Services;

public class SupplementaryDividendImportService
{
    public List<SupplementaryDividend> Load(string csvPath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
        };

        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();

        var results = new List<SupplementaryDividend>();
        while (csv.Read())
        {
            if (!DateOnly.TryParse(csv.GetField("Date"), out var date))
                continue;

            var incomeCode = (csv.GetField("IncomeCode") ?? "").Trim();
            if (string.IsNullOrEmpty(incomeCode))
                continue;

            var countryCode = (csv.GetField("CountryCode") ?? "").Trim();
            if (string.IsNullOrEmpty(countryCode))
                continue;

            results.Add(new SupplementaryDividend(
                Date:        date,
                IncomeCode:  incomeCode,
                CountryCode: countryCode,
                GrossAmount: decimal.TryParse(csv.GetField("GrossAmount"), CultureInfo.InvariantCulture, out var g) ? g : 0m,
                TaxPaid:     decimal.TryParse(csv.GetField("TaxPaid"),     CultureInfo.InvariantCulture, out var t) ? t : 0m
            ));
        }

        Console.WriteLine($"  {results.Count} supplementary dividend row(s) loaded");
        return results;
    }
}