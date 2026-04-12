using IrsEaser.Models;

namespace IrsEaser.Services;

public class DividendService
{
    /// <summary>
    /// Groups dividend transactions for <paramref name="taxYear"/> by country and
    /// returns one <see cref="DividendLine"/> per country (NLinha starts at 801).
    /// Optionally merges supplementary dividend entries that already carry a country code.
    /// </summary>
    public List<DividendLine> Build(
        IEnumerable<Transaction> transactions,
        Func<string, string?> getCountry,
        int taxYear,
        IEnumerable<SupplementaryDividend>? supplementary = null)
    {
        var dividends = transactions
            .Where(t => t.Type == TransactionType.Dividend && t.Date.Year == taxYear)
            .ToList();

        var suppList = (supplementary ?? [])
            .Where(s => s.Date.Year == taxYear)
            .ToList();

        if (dividends.Count == 0 && suppList.Count == 0)
            return [];

        var unknown = dividends
            .Select(t => t.Security)
            .Distinct(StringComparer.Ordinal)
            .Where(s => getCountry(s) is null)
            .ToList();

        foreach (var s in unknown)
            Console.WriteLine($"  [!] Skipping dividends from '{s}' — no country code.");

        var fromTransactions = dividends
            .Where(t => getCountry(t.Security) is not null)
            .Select(t => (CountryCode: getCountry(t.Security)!, IncomeCode: "E21", GrossAmount: t.Amount, TaxPaid: t.Taxes));

        var fromSupplementary = suppList
            .Select(s => (s.CountryCode, s.IncomeCode, s.GrossAmount, s.TaxPaid));

        return fromTransactions
            .Concat(fromSupplementary)
            .GroupBy(e => (e.CountryCode, e.IncomeCode))
            .OrderBy(g => g.Key.CountryCode).ThenBy(g => g.Key.IncomeCode)
            .Select((g, idx) => new DividendLine(
                LineNumber:   801 + idx,
                IncomeCode:   g.Key.IncomeCode,
                CountryCode:  g.Key.CountryCode,
                GrossAmount:  g.Sum(e => e.GrossAmount),
                TaxPaid:      g.Sum(e => e.TaxPaid)))
            .ToList();
    }
}
