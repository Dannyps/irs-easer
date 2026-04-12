using IrsEaser.Models;
using IrsEaser.Services;
using Xunit;

namespace IrsEaser.Tests;

public class DividendServiceTests
{
    private static readonly DividendService Svc = new();

    private static Transaction Dividend(string security, DateOnly date, decimal amount, decimal taxes = 0m)
        => new(date, TransactionType.Dividend, security, 0m, 0m, amount, 0m, taxes, amount, "", "", "", "");

    private static string? CountryMap(string name) => name switch
    {
        "US Corp" => "840",
        "DE Corp" => "276",
        _ => null,
    };

    [Fact]
    public void Build_ReturnsEmpty_WhenNoTransactionsAndNoSupplementary()
    {
        var lines = Svc.Build([], CountryMap, 2025);
        Assert.Empty(lines);
    }

    [Fact]
    public void Build_FiltersByTaxYear()
    {
        var txs = new List<Transaction>
        {
            Dividend("US Corp", new DateOnly(2024, 3, 1), 100m), // wrong year
            Dividend("US Corp", new DateOnly(2025, 5, 1), 200m), // correct year
        };

        var lines = Svc.Build(txs, CountryMap, 2025);

        var line = Assert.Single(lines);
        Assert.Equal(200m, line.GrossAmount);
    }

    [Fact]
    public void Build_GroupsByCountry_SumsAmountsAndTaxes()
    {
        var txs = new List<Transaction>
        {
            Dividend("US Corp", new DateOnly(2025, 3, 1), 100m, taxes: 15m),
            Dividend("US Corp", new DateOnly(2025, 9, 1),  50m, taxes:  7.50m),
        };

        var lines = Svc.Build(txs, CountryMap, 2025);

        var line = Assert.Single(lines);
        Assert.Equal("840",   line.CountryCode);
        Assert.Equal(150m,    line.GrossAmount);
        Assert.Equal(22.50m,  line.TaxPaid);
    }

    [Fact]
    public void Build_TwoCountries_TwoLines_OrderedByCountryCode()
    {
        var txs = new List<Transaction>
        {
            Dividend("US Corp", new DateOnly(2025, 1, 1), 100m),
            Dividend("DE Corp", new DateOnly(2025, 1, 1),  80m),
        };

        var lines = Svc.Build(txs, CountryMap, 2025);

        Assert.Equal(2, lines.Count);
        Assert.Equal("276", lines[0].CountryCode); // "276" sorts before "840"
        Assert.Equal("840", lines[1].CountryCode);
    }

    [Fact]
    public void Build_LineNumbers_StartAt801()
    {
        var txs = new List<Transaction>
        {
            Dividend("US Corp", new DateOnly(2025, 1, 1), 50m),
            Dividend("DE Corp", new DateOnly(2025, 1, 1), 80m),
        };

        var lines = Svc.Build(txs, CountryMap, 2025);

        Assert.Equal(2, lines.Count);
        Assert.Equal(801, lines[0].LineNumber);
        Assert.Equal(802, lines[1].LineNumber);
    }

    [Fact]
    public void Build_SkipsSecuritiesWithNoCountry()
    {
        var txs = new List<Transaction>
        {
            Dividend("US Corp", new DateOnly(2025, 1, 1), 100m),
            Dividend("Unknown", new DateOnly(2025, 1, 1),  50m), // no country
        };

        var lines = Svc.Build(txs, CountryMap, 2025);

        var line = Assert.Single(lines);
        Assert.Equal("840", line.CountryCode);
    }

    [Fact]
    public void Build_IncomeCode_IsE21_ForTransactionDividends()
    {
        var txs = new List<Transaction>
        {
            Dividend("US Corp", new DateOnly(2025, 6, 1), 100m),
        };

        var lines = Svc.Build(txs, CountryMap, 2025);

        Assert.Equal("E21", Assert.Single(lines).IncomeCode);
    }

    [Fact]
    public void Build_MergesSupplementaryDividends_SameCountry()
    {
        var txs = new List<Transaction>
        {
            Dividend("US Corp", new DateOnly(2025, 3, 1), 100m, taxes: 15m),
        };
        var supp = new List<SupplementaryDividend>
        {
            new(new DateOnly(2025, 6, 1), "E21", "840", 50m, 7.50m),
        };

        var lines = Svc.Build(txs, CountryMap, 2025, supp);

        var line = Assert.Single(lines);
        Assert.Equal(150m,   line.GrossAmount);
        Assert.Equal(22.50m, line.TaxPaid);
    }

    [Fact]
    public void Build_SupplementaryDividends_FilteredByTaxYear()
    {
        var supp = new List<SupplementaryDividend>
        {
            new(new DateOnly(2024, 6, 1), "E21", "840", 50m, 7.50m), // wrong year
            new(new DateOnly(2025, 6, 1), "E21", "840", 30m, 4.50m), // correct year
        };

        var lines = Svc.Build([], _ => null, 2025, supp);

        var line = Assert.Single(lines);
        Assert.Equal(30m, line.GrossAmount);
    }

    [Fact]
    public void Build_SupplementaryDifferentIncomeCode_ProducesSeparateLines()
    {
        var supp = new List<SupplementaryDividend>
        {
            new(new DateOnly(2025, 1, 1), "E21", "840", 100m, 15m),
            new(new DateOnly(2025, 1, 1), "E10", "840",  50m,  0m),
        };

        var lines = Svc.Build([], _ => null, 2025, supp);

        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void Build_ReturnsEmpty_WhenOnlyOutOfYearSupplementary()
    {
        var supp = new List<SupplementaryDividend>
        {
            new(new DateOnly(2024, 1, 1), "E21", "840", 100m, 15m),
        };

        var lines = Svc.Build([], _ => null, 2025, supp);

        Assert.Empty(lines);
    }

    [Fact]
    public void Build_IgnoresBuyAndSellTransactions()
    {
        var txs = new List<Transaction>
        {
            new(new DateOnly(2025, 1, 1), TransactionType.Buy,  "US Corp", 10, 0m, 500m, 0m, 0m, 500m, "", "", "", ""),
            new(new DateOnly(2025, 2, 1), TransactionType.Sell, "US Corp", 10, 0m, 600m, 0m, 0m, 600m, "", "", "", ""),
        };

        var lines = Svc.Build(txs, CountryMap, 2025);

        Assert.Empty(lines);
    }
}
