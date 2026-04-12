namespace IrsEaser.Models;

public record DividendLine(
    int LineNumber,
    string IncomeCode,
    string CountryCode,
    decimal GrossAmount,
    decimal TaxPaid
);
