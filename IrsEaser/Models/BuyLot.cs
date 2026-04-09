namespace IrsEaser.Models;

/// <summary>
/// A buy lot tracked during FIFO matching.
/// </summary>
public class BuyLot
{
    public Transaction Transaction { get; }
    public decimal RemainingShares { get; set; }

    public BuyLot(Transaction transaction)
    {
        Transaction = transaction;
        RemainingShares = transaction.Shares;
    }
}
