namespace IrsEaser.Models;

/// <summary>
/// A single line destined for Anexo J, Quadro 9.2-A.
/// Represents the portion of one sell transaction attributable to one buy lot.
/// </summary>
public record TradeLine(
    int LineNumber,
    /// <summary>ISO 3166-1 numeric country code (e.g. "276" for Germany).</summary>
    string CountryCode,
    string SecurityName,
    DateOnly SellDate,
    /// <summary>Net sell proceeds attributable to this lot (proportional share of the sell).</summary>
    decimal SellValue,
    DateOnly BuyDate,
    /// <summary>Net acquisition cost attributable to this lot (proportional share of the buy).</summary>
    decimal BuyValue,
    /// <summary>Combined fees from both the sell and buy transactions, proportionally allocated.</summary>
    decimal Fees,
    /// <summary>Taxes paid abroad on both transactions, proportionally allocated.</summary>
    decimal TaxesPaid
);
