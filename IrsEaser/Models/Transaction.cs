namespace IrsEaser.Models;

public enum TransactionType
{
    Buy,
    Sell,
    DeliveryInbound,
    DeliveryOutbound,
    TransferIn,
    TransferOut,
    Other,
}

public record Transaction(
    DateOnly Date,
    TransactionType Type,
    string Security,
    decimal Shares,
    decimal Quote,
    decimal Amount,
    decimal Fees,
    decimal Taxes,
    decimal NetTransactionValue,
    string Account,
    string OffsetAccount,
    string Note,
    string Source
);
