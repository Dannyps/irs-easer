using IrsEaser.Models;

namespace IrsEaser.Services;

/// <summary>
/// Matches buy and sell transactions using FIFO (First In, First Out) lot assignment
/// and produces <see cref="TradeLine"/> records for a given tax year.
/// </summary>
public class FifoMatchingService
{
    /// <summary>
    /// Processes all transactions and returns trade lines for sells that occurred in
    /// <paramref name="taxYear"/>.
    /// </summary>
    /// <param name="transactions">All transactions, pre-sorted by date ascending.</param>
    /// <param name="getCountry">Delegate that returns the ISO 3166-1 numeric code for a security name,
    /// or <c>null</c> if unknown.</param>
    /// <param name="taxYear">The tax year to report (only sells in this year are included).</param>
    public List<TradeLine> Match(
        IEnumerable<Transaction> transactions,
        Func<string, string?> getCountry,
        int taxYear)
    {
        // FIFO lot queues per security (oldest lot first)
        var lots = new Dictionary<string, List<BuyLot>>(StringComparer.Ordinal);
        var lines = new List<TradeLine>();
        var lineNumber = 951;

        foreach (var tx in transactions)
        {
            if (!lots.ContainsKey(tx.Security))
                lots[tx.Security] = [];

            if (tx.Type is TransactionType.Buy or TransactionType.DeliveryInbound or TransactionType.TransferIn)
            {
                lots[tx.Security].Add(new BuyLot(tx));
                continue;
            }

            if (tx.Type is not (TransactionType.Sell or TransactionType.DeliveryOutbound or TransactionType.TransferOut))
                continue;

            // Only emit lines for sells in the requested tax year
            if (tx.Date.Year != taxYear)
            {
                // Still consume lots to keep FIFO state consistent
                ConsumeLots(lots[tx.Security], tx.Shares, emitLines: false,
                    tx, getCountry, ref lineNumber, lines);
                continue;
            }

            var countryCode = getCountry(tx.Security);
            if (countryCode is null)
            {
                Console.WriteLine($"  [!] Skipping sell of '{tx.Security}' on {tx.Date} — no country code.");
                ConsumeLots(lots[tx.Security], tx.Shares, emitLines: false,
                    tx, getCountry, ref lineNumber, lines);
                continue;
            }

            ConsumeLots(lots[tx.Security], tx.Shares, emitLines: true,
                tx, getCountry, ref lineNumber, lines);
        }

        return lines;
    }

    private static void ConsumeLots(
        List<BuyLot> lotList,
        decimal sharesToSell,
        bool emitLines,
        Transaction sellTx,
        Func<string, string?> getCountry,
        ref int lineNumber,
        List<TradeLine> lines)
    {
        var remainingToSell = sharesToSell;
        var totalSharesSold = sharesToSell;

        for (int i = 0; i < lotList.Count && remainingToSell > 0; i++)
        {
            var lot = lotList[i];
            var consumed = Math.Min(lot.RemainingShares, remainingToSell);

            if (emitLines)
            {
                var sellFraction = consumed / totalSharesSold;
                var buyFraction  = consumed / lot.Transaction.Shares;

                var sellValue = Math.Round(sellTx.NetTransactionValue * sellFraction, 2);
                var buyValue  = Math.Round(lot.Transaction.NetTransactionValue * buyFraction, 2);
                var fees      = Math.Round(sellTx.Fees * sellFraction + lot.Transaction.Fees * buyFraction, 2);
                var taxes     = Math.Round(sellTx.Taxes * sellFraction + lot.Transaction.Taxes * buyFraction, 2);

                lines.Add(new TradeLine(
                    LineNumber:   lineNumber++,
                    CountryCode:  getCountry(sellTx.Security)!,
                    SecurityName: sellTx.Security,
                    SellDate:     sellTx.Date,
                    SellValue:    sellValue,
                    BuyDate:      lot.Transaction.Date,
                    BuyValue:     buyValue,
                    Fees:         fees,
                    TaxesPaid:    taxes
                ));
            }

            remainingToSell -= consumed;
            lot.RemainingShares -= consumed;
        }

        // Remove fully-consumed lots
        lotList.RemoveAll(l => l.RemainingShares == 0);

        if (remainingToSell > 0)
        {
            Console.WriteLine(
                $"  [!] Sell of {sharesToSell} shares of '{sellTx.Security}' on {sellTx.Date} " +
                $"could not be fully matched to buy lots (missing {remainingToSell} shares). " +
                $"Check for missing buy transactions.");
        }
    }
}
