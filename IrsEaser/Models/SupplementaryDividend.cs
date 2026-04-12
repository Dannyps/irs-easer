namespace IrsEaser.Models;

/// <summary>
/// A manually-entered dividend/interest entry from a tracker outside of Portfolio Performance.
/// The country code is supplied directly (ISO 3166-1 numeric) so no lookup is needed.
/// </summary>
public record SupplementaryDividend(
    DateOnly Date,
    string IncomeCode,
    string CountryCode,
    decimal GrossAmount,
    decimal TaxPaid
);