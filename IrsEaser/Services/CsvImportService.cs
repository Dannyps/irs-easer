using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using IrsEaser.Models;

namespace IrsEaser.Services;

public class CsvImportService
{
    private static readonly string[] DateFormats =
        ["dd.MM.yyyy", "M/d/yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy"];

    public List<Transaction> Load(string csvPath)
    {
        var rawText = File.ReadAllText(csvPath);
        var delimiter = DetectDelimiter(rawText);
        var culture = delimiter == ';'
            ? CultureInfo.GetCultureInfo("de-DE")   // comma decimals
            : CultureInfo.InvariantCulture;          // period decimals

        var config = new CsvConfiguration(culture)
        {
            Delimiter = delimiter.ToString(),
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
        };

        var transactions = new List<Transaction>();

        using var reader = new StringReader(rawText);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var type = ParseType(csv.GetField("Type") ?? "");

            var date = ParseDate(csv.GetField("Date") ?? "");
            if (date is null)
                continue;

            // "Net Transaction Value" might be split as "Net Transaction" + "Value" in some exports;
            // try both the merged and split forms.
            var netRaw = csv.TryGetField<string>("Net Transaction Value", out var netFull) && !string.IsNullOrWhiteSpace(netFull)
                ? netFull
                : csv.TryGetField<string>("Net Transaction", out var netPart) && !string.IsNullOrWhiteSpace(netPart)
                    ? netPart
                    : "0";

            transactions.Add(new Transaction(
                Date: date.Value,
                Type: type,
                Security: (csv.GetField("Security") ?? "").Trim(),
                Shares: ParseDecimal(csv.GetField("Shares") ?? "0", culture),
                Quote: ParseDecimal(csv.GetField("Quote") ?? "0", culture),
                Amount: ParseDecimal(csv.GetField("Amount") ?? "0", culture),
                Fees: ParseDecimal(csv.GetField("Fees") ?? "0", culture),
                Taxes: ParseDecimal(csv.GetField("Taxes") ?? "0", culture),
                NetTransactionValue: Math.Abs(ParseDecimal(netRaw, culture)),
                Account: (csv.GetField("Account") ?? "").Trim(),
                OffsetAccount: (csv.GetField("Offset Account") ?? "").Trim(),
                Note: (csv.GetField("Note") ?? "").Trim(),
                Source: (csv.GetField("Source") ?? "").Trim()
            ));
        }

        // Ensure chronological order for FIFO
        transactions.Sort((a, b) => a.Date.CompareTo(b.Date));
        return transactions;
    }

    private static char DetectDelimiter(string text)
    {
        // Check the first line only
        var firstLine = text.AsSpan()[..text.IndexOf('\n')];
        int semicolons = 0, commas = 0;
        foreach (var c in firstLine)
        {
            if (c == ';') semicolons++;
            else if (c == ',') commas++;
        }
        return semicolons > commas ? ';' : ',';
    }

    private static TransactionType ParseType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "buy"  or "kauf"                                    => TransactionType.Buy,
        "sell" or "verkauf"                                 => TransactionType.Sell,
        "dividend" or "dividende"                           => TransactionType.Dividend,
        "delivery (inbound)"  or "einlieferung"             => TransactionType.DeliveryInbound,
        "delivery (outbound)" or "auslieferung"             => TransactionType.DeliveryOutbound,
        "transfer (inbound)"  or "umbuchung (eingang)"      => TransactionType.TransferIn,
        "transfer (outbound)" or "umbuchung (ausgang)"      => TransactionType.TransferOut,
        _                                                   => TransactionType.Other,
    };

    private static DateOnly? ParseDate(string value)
    {
        value = value.Trim();

        // Strip time portion if present (e.g. "2026-03-27 11:55:00" or "2026-03-27T11:55:00")
        var spaceIdx = value.IndexOf(' ');
        if (spaceIdx > 0) value = value[..spaceIdx];
        var tIdx = value.IndexOf('T');
        if (tIdx > 0) value = value[..tIdx];

        foreach (var fmt in DateFormats)
        {
            if (DateOnly.TryParseExact(value, fmt, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
                return date;
        }
        // Last attempt: let the runtime figure it out
        if (DateOnly.TryParse(value, out var fallback))
            return fallback;
        return null;
    }

    private static decimal ParseDecimal(string value, CultureInfo culture)
    {
        value = value.Trim();
        if (decimal.TryParse(value, NumberStyles.Any, culture, out var result))
            return result;
        // Try invariant as fallback
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            return result;
        return 0m;
    }
}
