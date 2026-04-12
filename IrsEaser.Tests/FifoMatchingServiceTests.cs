using IrsEaser.Models;
using IrsEaser.Services;
using Xunit;

namespace IrsEaser.Tests;

public class FifoMatchingServiceTests
{
    private static readonly FifoMatchingService Svc = new();

    private static string? CountryFor(string _) => "276";

    private static Transaction Buy(string security, DateOnly date, decimal shares, decimal net,
        decimal fees = 0m, decimal taxes = 0m)
        => new(date, TransactionType.Buy, security, shares, 0m, net, fees, taxes, net, "", "", "", "");

    private static Transaction Sell(string security, DateOnly date, decimal shares, decimal net,
        decimal fees = 0m, decimal taxes = 0m)
        => new(date, TransactionType.Sell, security, shares, 0m, net, fees, taxes, net, "", "", "", "");

    [Fact]
    public void SimpleFullSell_ProducesOneLine()
    {
        var txs = new List<Transaction>
        {
            Buy("ACME",  new DateOnly(2024, 1, 1), 100, 500m),
            Sell("ACME", new DateOnly(2025, 6, 1), 100, 600m),
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        var line = Assert.Single(lines);
        Assert.Equal(951, line.LineNumber);
        Assert.Equal("276", line.CountryCode);
        Assert.Equal(600m, line.SellValue);
        Assert.Equal(500m, line.BuyValue);
        Assert.Equal(new DateOnly(2025, 6, 1), line.SellDate);
        Assert.Equal(new DateOnly(2024, 1, 1), line.BuyDate);
    }

    [Fact]
    public void TwoLots_SellAll_ProducesTwoLines_FifoOrder()
    {
        // Spec example: Lot A 60 shares @ €400, Lot B 40 shares @ €300, sell 100 @ €1000
        var txs = new List<Transaction>
        {
            Buy("ACME",  new DateOnly(2024, 3,  3),   60, 400m),
            Buy("ACME",  new DateOnly(2024, 7,  9),   40, 300m),
            Sell("ACME", new DateOnly(2024, 11, 14), 100, 1000m),
        };

        var lines = Svc.Match(txs, CountryFor, 2024);

        Assert.Equal(2, lines.Count);

        Assert.Equal(951, lines[0].LineNumber);
        Assert.Equal(new DateOnly(2024, 3, 3), lines[0].BuyDate);
        Assert.Equal(600m, lines[0].SellValue); // 60/100 * 1000
        Assert.Equal(400m, lines[0].BuyValue);  // 60/60  * 400

        Assert.Equal(952, lines[1].LineNumber);
        Assert.Equal(new DateOnly(2024, 7, 9), lines[1].BuyDate);
        Assert.Equal(400m, lines[1].SellValue); // 40/100 * 1000
        Assert.Equal(300m, lines[1].BuyValue);  // 40/40  * 300
    }

    [Fact]
    public void PartialSell_ConsumesOldestLotFirst()
    {
        var txs = new List<Transaction>
        {
            Buy("ACME",  new DateOnly(2024, 1, 1), 100, 500m),
            Buy("ACME",  new DateOnly(2024, 6, 1), 100, 600m),
            Sell("ACME", new DateOnly(2025, 3, 1),  60, 360m), // only touches first lot
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        var line = Assert.Single(lines);
        Assert.Equal(new DateOnly(2024, 1, 1), line.BuyDate); // oldest lot consumed
        Assert.Equal(360m, line.SellValue);                   // full sell (one lot touched)
        Assert.Equal(300m, line.BuyValue);                    // 60/100 * 500
    }

    [Fact]
    public void PartialSell_LotRemainsForSubsequentSell()
    {
        var txs = new List<Transaction>
        {
            Buy("ACME",  new DateOnly(2024, 1, 1), 100, 500m),
            Sell("ACME", new DateOnly(2025, 3, 1),  40, 200m),
            Sell("ACME", new DateOnly(2025, 6, 1),  60, 360m),
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        Assert.Equal(2, lines.Count);
        Assert.Equal(200m, lines[0].SellValue);
        Assert.Equal(200m, lines[0].BuyValue); // 40/100 * 500
        Assert.Equal(360m, lines[1].SellValue);
        Assert.Equal(300m, lines[1].BuyValue); // 60/100 * 500
    }

    [Fact]
    public void SellSpanningTwoLots_CorrectProportions()
    {
        // Buy 60 @ 300, buy 60 @ 360; sell 90 @ 540
        // Lot A: consumed=60, sell=540*(60/90)=360, buy=300*(60/60)=300
        // Lot B: consumed=30, sell=540*(30/90)=180, buy=360*(30/60)=180
        var txs = new List<Transaction>
        {
            Buy("ACME",  new DateOnly(2024, 1, 1), 60, 300m),
            Buy("ACME",  new DateOnly(2024, 6, 1), 60, 360m),
            Sell("ACME", new DateOnly(2025, 3, 1), 90, 540m),
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        Assert.Equal(2, lines.Count);
        Assert.Equal(360m, lines[0].SellValue);
        Assert.Equal(300m, lines[0].BuyValue);
        Assert.Equal(180m, lines[1].SellValue);
        Assert.Equal(180m, lines[1].BuyValue);
    }

    [Fact]
    public void SellOutsideTaxYear_StillConsumesLots_NoLinesEmitted()
    {
        var txs = new List<Transaction>
        {
            Buy("ACME",  new DateOnly(2023, 1, 1), 100, 500m),
            Sell("ACME", new DateOnly(2024, 6, 1), 100, 600m), // 2024 sell; reporting 2025
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        Assert.Empty(lines);
    }

    [Fact]
    public void SellOutsideTaxYear_LotsConsumed_SubsequentTaxYearSellHasNoLots()
    {
        var txs = new List<Transaction>
        {
            Buy("ACME",  new DateOnly(2023, 1, 1), 100, 500m),
            Sell("ACME", new DateOnly(2024, 6, 1), 100, 600m), // consumes all lots
            Sell("ACME", new DateOnly(2025, 3, 1), 100, 700m), // no lots remain
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        Assert.Empty(lines); // all lots were consumed by the 2024 sell
    }

    [Fact]
    public void SellWithNoCountryCode_IsSkipped()
    {
        var txs = new List<Transaction>
        {
            Buy("UNKNOWN",  new DateOnly(2024, 1, 1),  50, 200m),
            Sell("UNKNOWN", new DateOnly(2025, 3, 1),  50, 250m),
        };

        var lines = Svc.Match(txs, _ => null, 2025);

        Assert.Empty(lines);
    }

    [Fact]
    public void DeliveryInbound_TreatedAsBuyLot()
    {
        var txs = new List<Transaction>
        {
            new(new DateOnly(2024, 1, 1), TransactionType.DeliveryInbound,
                "ACME", 100, 0m, 500m, 0m, 0m, 500m, "", "", "", ""),
            Sell("ACME", new DateOnly(2025, 6, 1), 100, 600m),
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        var line = Assert.Single(lines);
        Assert.Equal(500m, line.BuyValue);
    }

    [Fact]
    public void LineNumbers_StartAt951_IncrementAcrossAllSells()
    {
        var txs = new List<Transaction>
        {
            Buy("ACME", new DateOnly(2024, 1, 1), 100, 400m),
            Buy("ZINC", new DateOnly(2024, 2, 1), 100, 300m),
            Sell("ACME", new DateOnly(2025, 1, 1), 100, 500m),
            Sell("ZINC", new DateOnly(2025, 2, 1), 100, 400m),
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        Assert.Equal(2, lines.Count);
        Assert.Equal(951, lines[0].LineNumber);
        Assert.Equal(952, lines[1].LineNumber);
    }

    [Fact]
    public void Fees_AllocatedProportionally()
    {
        // Buy 100 shares fee €4.00, sell 60 shares fee €2.00.
        // sellFraction = 60/60 = 1.0, buyFraction = 60/100 = 0.6
        // fees = round(2.00 * 1.0 + 4.00 * 0.6, 2) = 4.40
        var txs = new List<Transaction>
        {
            Buy("ACME",  new DateOnly(2024, 1, 1), 100, 500m, fees: 4m),
            Sell("ACME", new DateOnly(2025, 6, 1),  60, 360m, fees: 2m),
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        Assert.Equal(4.40m, Assert.Single(lines).Fees);
    }

    [Fact]
    public void Taxes_AllocatedProportionally()
    {
        // Buy 100 shares taxes €2.00, sell 50 shares taxes €1.00.
        // sellFraction = 50/50 = 1.0, buyFraction = 50/100 = 0.5
        // taxes = round(1.00 * 1.0 + 2.00 * 0.5, 2) = 2.00
        var txs = new List<Transaction>
        {
            Buy("ACME",  new DateOnly(2024, 1, 1), 100, 500m, taxes: 2m),
            Sell("ACME", new DateOnly(2025, 3, 1),  50, 300m, taxes: 1m),
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        Assert.Equal(2.00m, Assert.Single(lines).TaxesPaid);
    }

    [Fact]
    public void MultipleSecurities_TrackedIndependently()
    {
        var txs = new List<Transaction>
        {
            Buy("ACME", new DateOnly(2024, 1, 1), 100, 500m),
            Buy("ZINC", new DateOnly(2024, 1, 1), 200, 800m),
            Sell("ACME", new DateOnly(2025, 6, 1), 100, 600m),
            Sell("ZINC", new DateOnly(2025, 6, 1), 200, 900m),
        };

        var lines = Svc.Match(txs, CountryFor, 2025);

        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.SecurityName == "ACME" && l.SellValue == 600m);
        Assert.Contains(lines, l => l.SecurityName == "ZINC" && l.SellValue == 900m);
    }
}
